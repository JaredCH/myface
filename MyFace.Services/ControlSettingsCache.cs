using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using MyFace.Data;

namespace MyFace.Services;

public sealed record ControlSettingValueSnapshot(string Key, string Value, DateTime UpdatedAt, string? UpdatedByUsername);

public sealed class ControlSettingsSnapshot
{
    public ControlSettingsSnapshot(DateTime generatedAtUtc, IReadOnlyDictionary<string, ControlSettingValueSnapshot> values)
    {
        GeneratedAtUtc = generatedAtUtc;
        Values = values;
    }

    public DateTime GeneratedAtUtc { get; }
    public IReadOnlyDictionary<string, ControlSettingValueSnapshot> Values { get; }

    public bool TryGetValue(string key, out ControlSettingValueSnapshot value)
    {
        return Values.TryGetValue(key, out value);
    }
}

public class ControlSettingsCache
{
    private const string CacheKey = "control-settings:snapshot";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;

    public ControlSettingsCache(IMemoryCache cache, IServiceScopeFactory scopeFactory)
    {
        _cache = cache;
        _scopeFactory = scopeFactory;
    }

    public async Task<ControlSettingsSnapshot> GetSnapshotAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKey, out var cached) && cached is ControlSettingsSnapshot snapshot)
        {
            return snapshot;
        }

        snapshot = await LoadSnapshotAsync(ct);
        _cache.Set(CacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    public void Invalidate() => _cache.Remove(CacheKey);

    private async Task<ControlSettingsSnapshot> LoadSnapshotAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var settings = await db.ControlSettings
            .AsNoTracking()
            .ToListAsync(ct);

        var values = settings
            .ToDictionary(
                setting => setting.Key ?? throw new InvalidOperationException("Setting key cannot be null."),
                setting => new ControlSettingValueSnapshot(
                    setting.Key ?? string.Empty,
                    setting.Value,
                    setting.UpdatedAt,
                    setting.UpdatedByUsername),
                StringComparer.OrdinalIgnoreCase);

        return new ControlSettingsSnapshot(DateTime.UtcNow, values);
    }
}
