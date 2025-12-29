using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MyFace.Core.Entities;
using MyFace.Data;
using MyFace.Services;
using MyFace.Services.Networking;
using System.Net;
using System.Net.Security;

var sites = new (string Name, string Url)[]
{
    ("Tech Learning Collective", "http://lpiyu33yusoalp5kh3f4hak2so2sjjvjw5ykyvu2dulzosgvuffq6sad.onion"),
    ("Dread", "https://dreadytofatroptsdj6io7l3xptbet6onoyno2yv7jicoxknyazubrad.onion/"),
    ("Drug Hub", "http://drughub666py6fgnml5kmxa7fva5noppkf6wkai4fwwvzwt4rz645aqd.onion/")
};

var services = new ServiceCollection();
services.AddLogging();
services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase("monitor-probe-test"));
services.AddSingleton(new Socks5ProxyConnector("127.0.0.1", 9052));
services.AddSingleton<MonitorLogService>();
services.AddHttpClient("TorClient")
    .ConfigurePrimaryHttpMessageHandler(sp =>
    {
        var connector = sp.GetRequiredService<Socks5ProxyConnector>();
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = true,
            MaxAutomaticRedirections = 5,
            AutomaticDecompression = DecompressionMethods.All,
            ConnectTimeout = TimeSpan.FromSeconds(60),
            ConnectCallback = (context, cancellationToken) =>
            {
                var target = context.DnsEndPoint ??
                    new DnsEndPoint(
                        context.InitialRequestMessage?.RequestUri?.Host
                            ?? throw new InvalidOperationException("Missing request host."),
                        context.InitialRequestMessage?.RequestUri?.Port ?? 80);

                return connector.ConnectAsync(target, cancellationToken);
            }
        };

        handler.SslOptions = new SslClientAuthenticationOptions
        {
            RemoteCertificateValidationCallback = (_, _, _, _) => true
        };

        return handler;
    });
services.AddScoped<OnionStatusService>();

await using var provider = services.BuildServiceProvider();
await using var scope = provider.CreateAsyncScope();
var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
var monitor = scope.ServiceProvider.GetRequiredService<OnionStatusService>();

Console.WriteLine("=== Onion Monitor Probe Simulation ===");

foreach (var site in sites)
{
    var entity = new OnionStatus
    {
        Name = site.Name,
        Description = "Probe Simulation",
        OnionUrl = site.Url,
        Status = "Unknown"
    };

    db.OnionStatuses.Add(entity);
    await db.SaveChangesAsync();

    Console.WriteLine($"\nTesting: {site.Name}\nLink: {site.Url}");

    try
    {
        var ok = await monitor.CheckAsync(entity.Id);
        var refreshed = await db.OnionStatuses.FindAsync(entity.Id);

        if (!ok || refreshed is null)
        {
            Console.WriteLine("  Result: Probe failed to execute.");
            continue;
        }

        var latency = refreshed.AverageLatency.HasValue ? $"{refreshed.AverageLatency:F0} ms" : "n/a";

        Console.WriteLine($"  Status: {refreshed.Status}");
        Console.WriteLine($"  Reachable Attempts: {refreshed.ReachableAttempts}/{refreshed.TotalAttempts}");
        Console.WriteLine($"  Avg Latency: {latency}");
        Console.WriteLine($"  Last Checked: {refreshed.LastChecked:u}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  Result: Exception during probe - {ex.Message}");
    }
    finally
    {
        db.OnionStatuses.Remove(entity);
        await db.SaveChangesAsync();
    }
}

Console.WriteLine("\nSimulation complete.");
