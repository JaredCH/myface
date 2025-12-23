using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class OnionStatusService
{
    private readonly ApplicationDbContext _context;
    private readonly HttpClient _httpClient;

    public OnionStatusService(ApplicationDbContext context, IHttpClientFactory httpClientFactory)
    {
        _context = context;
        _httpClient = httpClientFactory.CreateClient("TorClient");
    }

    public async Task<OnionStatus> AddAsync(string onionUrl)
    {
        var status = new OnionStatus
        {
            OnionUrl = onionUrl,
            Status = "Unknown",
            LastChecked = null,
            ResponseTime = null
        };

        _context.OnionStatuses.Add(status);
        await _context.SaveChangesAsync();
        return status;
    }

    public async Task<List<OnionStatus>> GetAllAsync()
    {
        return await _context.OnionStatuses
            .OrderBy(m => m.OnionUrl)
            .ToListAsync();
    }

    public async Task CheckAllAsync()
    {
        var items = await _context.OnionStatuses.ToListAsync();
        foreach (var item in items)
        {
            await CheckAsync(item.Id);
        }
    }

    public async Task<bool> CheckAsync(int id)
    {
        var item = await _context.OnionStatuses.FindAsync(id);
        if (item == null) return false;

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await _httpClient.GetAsync(item.OnionUrl);
            sw.Stop();
            item.Status = response.IsSuccessStatusCode ? "Online" : $"HTTP {(int)response.StatusCode}";
            item.ResponseTime = sw.Elapsed.TotalMilliseconds;
        }
        catch (Exception ex)
        {
            sw.Stop();
            item.Status = "Offline";
            item.ResponseTime = null;
        }

        item.LastChecked = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RemoveAsync(int id)
    {
        var item = await _context.OnionStatuses.FindAsync(id);
        if (item == null) return false;

        _context.OnionStatuses.Remove(item);
        await _context.SaveChangesAsync();
        return true;
    }
}
