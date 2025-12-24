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
        // Optional: timeout to avoid hanging checks
        _httpClient.Timeout = TimeSpan.FromSeconds(15);
    }

    public async Task<OnionStatus> AddAsync(string name, string description, string onionUrl)
    {
        var status = new OnionStatus
        {
            Name = name,
            Description = description,
            OnionUrl = onionUrl,
            Status = "Unknown",
            LastChecked = null,
            ResponseTime = null,
            ReachableAttempts = 0,
            TotalAttempts = 0,
            AverageLatency = null
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

        int successes = 0;
        int attempts = 5;
        double totalLatency = 0;

        for (int i = 0; i < attempts; i++)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                var response = await _httpClient.GetAsync(item.OnionUrl);
                sw.Stop();
                if (response.IsSuccessStatusCode)
                {
                    successes++;
                    totalLatency += sw.Elapsed.TotalMilliseconds;
                }
            }
            catch
            {
                sw.Stop();
                // Failed attempt
            }
            // Small delay between attempts? Maybe not needed for this simple check
        }

        item.ReachableAttempts = successes;
        item.TotalAttempts = attempts;
        
        if (successes == 0)
        {
            item.Status = "Offline";
            item.AverageLatency = null;
            item.ResponseTime = null;
        }
        else
        {
            item.AverageLatency = totalLatency / successes;
            item.ResponseTime = item.AverageLatency; // Keep backward compatibility if needed
            
            if (successes == attempts)
            {
                item.Status = "Online";
            }
            else
            {
                item.Status = "DEGRADED";
            }
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

    public async Task<bool> UpdateAsync(int id, string name, string description, string onionUrl)
    {
        var item = await _context.OnionStatuses.FindAsync(id);
        if (item == null) return false;

        item.Name = name;
        item.Description = description;
        item.OnionUrl = onionUrl;
        
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<OnionStatus?> GetByIdAsync(int id)
    {
        return await _context.OnionStatuses.FindAsync(id);
    }
}
