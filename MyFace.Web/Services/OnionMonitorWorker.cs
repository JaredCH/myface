using Microsoft.Extensions.Hosting;
using MyFace.Services;

namespace MyFace.Web.Services;

public class OnionMonitorWorker : BackgroundService
{
    private readonly IServiceProvider _services;

    public OnionMonitorWorker(IServiceProvider services)
    {
        _services = services;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Run every 5 minutes
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<OnionStatusService>();
            try
            {
                await svc.CheckAllAsync();
            }
            catch
            {
                // swallow errors in MVP
            }
            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}
