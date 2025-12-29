using Microsoft.Extensions.Hosting;
using MyFace.Services;
using System.Diagnostics;

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
        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _services.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<OnionStatusService>();
            var log = scope.ServiceProvider.GetRequiredService<MonitorLogService>();
            var stopwatch = Stopwatch.StartNew();

            try
            {
                await svc.CheckAllAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.Append($"Monitor sweep failed: {ex.Message}");
            }
            finally
            {
                stopwatch.Stop();
                var process = Process.GetCurrentProcess();
                var workingSetMb = process.WorkingSet64 / (1024d * 1024d);
                var cpuSeconds = process.TotalProcessorTime.TotalSeconds;
                log.Append($"Resource snapshot -> duration {stopwatch.Elapsed.TotalSeconds:F1}s, working set {workingSetMb:F1} MB, CPU total {cpuSeconds:F1}s, threads {process.Threads.Count}.");
            }
        }
    }
}
