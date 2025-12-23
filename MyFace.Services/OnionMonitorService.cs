using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class OnionMonitorService
{
    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;

    public OnionMonitorService(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClient = httpClientFactory.CreateClient("TorClient");
    }

    public async Task<OnionMonitor> AddMonitorAsync(string onionUrl, string? friendlyName = null, string? notes = null)
    {
        var monitor = new OnionMonitor
        {
            OnionUrl = onionUrl,
            FriendlyName = friendlyName,
            Notes = notes,
            CreatedAt = DateTime.UtcNow,
            IsOnline = false
        };

        _context.OnionMonitors.Add(monitor);
        await _context.SaveChangesAsync();
        return monitor;
    }

    public async Task<List<OnionMonitor>> GetAllMonitorsAsync()
    {
        return await _context.OnionMonitors
            .OrderBy(m => m.FriendlyName ?? m.OnionUrl)
            .ToListAsync();
    }

    public async Task CheckAllMonitorsAsync()
    {
        var monitors = await _context.OnionMonitors.ToListAsync();

        foreach (var monitor in monitors)
        {
            await CheckMonitorAsync(monitor.Id);
        }
    }

    public async Task<bool> CheckMonitorAsync(int monitorId)
    {
        var monitor = await _context.OnionMonitors.FindAsync(monitorId);
        if (monitor == null) return false;

        bool isOnline = false;
        try
        {
            var response = await _httpClient.GetAsync(monitor.OnionUrl);
            isOnline = response.IsSuccessStatusCode;
        }
        catch
        {
            isOnline = false;
        }

        monitor.IsOnline = isOnline;
        monitor.LastChecked = DateTime.UtcNow;
        
        if (isOnline)
        {
            monitor.LastOnline = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
        return isOnline;
    }

    public async Task<bool> RemoveMonitorAsync(int monitorId)
    {
        var monitor = await _context.OnionMonitors.FindAsync(monitorId);
        if (monitor == null) return false;

        _context.OnionMonitors.Remove(monitor);
        await _context.SaveChangesAsync();
        return true;
    }
}
