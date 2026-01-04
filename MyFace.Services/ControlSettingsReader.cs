using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace MyFace.Services;

public class ControlSettingsReader
{
    private readonly ControlSettingsCache _cache;

    public ControlSettingsReader(ControlSettingsCache cache)
    {
        _cache = cache;
    }

    public async Task<int> GetIntAsync(string key, int fallback, CancellationToken ct = default)
    {
        var fallbackString = ControlSettingsCatalog.TryGetDefinition(key, out var definition)
            ? definition.DefaultValue
            : fallback.ToString(CultureInfo.InvariantCulture);

        var raw = await GetValueOrDefaultAsync(key, fallbackString, ct);
        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    public async Task<bool> GetBoolAsync(string key, bool fallback, CancellationToken ct = default)
    {
        var fallbackString = ControlSettingsCatalog.TryGetDefinition(key, out var definition)
            ? definition.DefaultValue
            : (fallback ? "true" : "false");

        var raw = await GetValueOrDefaultAsync(key, fallbackString, ct);
        return bool.TryParse(raw, out var parsed) ? parsed : fallback;
    }

    public async Task<string> GetStringAsync(string key, string fallback, CancellationToken ct = default)
    {
        var fallbackString = ControlSettingsCatalog.TryGetDefinition(key, out var definition)
            ? definition.DefaultValue
            : fallback;

        return await GetValueOrDefaultAsync(key, fallbackString, ct);
    }

    private async Task<string> GetValueOrDefaultAsync(string key, string fallback, CancellationToken ct)
    {
        var snapshot = await _cache.GetSnapshotAsync(ct);
        if (snapshot.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value.Value))
        {
            return value.Value;
        }

        return fallback;
    }
}
