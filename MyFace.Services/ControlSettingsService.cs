using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public sealed record ControlSettingCurrentValue(string Key, string Value, DateTime UpdatedAt, string? UpdatedByUsername, int? UpdatedByUserId);
public sealed record ControlSettingHistoryRecord(int Id, string Key, string Value, string? Reason, DateTime CreatedAt, string? UpdatedByUsername);
public sealed record ControlSettingDetail(ControlSettingDefinition Definition, ControlSettingCurrentValue? CurrentValue, IReadOnlyList<ControlSettingHistoryRecord> History);

public class ControlSettingsService
{
    private readonly ApplicationDbContext _db;
    private readonly ControlSettingsCache _cache;

    public ControlSettingsService(ApplicationDbContext db, ControlSettingsCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public async Task<IReadOnlyList<ControlSettingDetail>> GetAllAsync(CancellationToken ct = default)
    {
        var definitions = ControlSettingsCatalog.All;
        var settings = await _db.ControlSettings.AsNoTracking().ToListAsync(ct);
        var historyEntries = await _db.ControlSettingHistories
            .AsNoTracking()
            .OrderByDescending(h => h.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        var currentLookup = settings.ToDictionary(
            s => s.Key,
            s => new ControlSettingCurrentValue(s.Key, s.Value, s.UpdatedAt, s.UpdatedByUsername, s.UpdatedByUserId),
            StringComparer.OrdinalIgnoreCase);

        var historyLookup = historyEntries
            .GroupBy(h => h.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ControlSettingHistoryRecord>)g
                    .Select(entry => new ControlSettingHistoryRecord(entry.Id, entry.Key, entry.Value, entry.Reason, entry.CreatedAt, entry.UpdatedByUsername))
                    .ToList(),
                StringComparer.OrdinalIgnoreCase);

        var details = new List<ControlSettingDetail>(definitions.Count);
        foreach (var definition in definitions)
        {
            currentLookup.TryGetValue(definition.Key, out var current);
            historyLookup.TryGetValue(definition.Key, out var history);
            details.Add(new ControlSettingDetail(definition, current, history ?? Array.Empty<ControlSettingHistoryRecord>()));
        }

        return details;
    }

    public async Task<ControlSettingDetail> UpdateAsync(string key, string value, int? actorUserId, string? actorUsername, string? reason, CancellationToken ct = default)
    {
        var definition = ControlSettingsCatalog.GetDefinition(key);
        ValidateValue(definition, value);

        var existing = await _db.ControlSettings.FirstOrDefaultAsync(s => s.Key == key, ct);
        var previousValue = existing?.Value ?? definition.DefaultValue;

        if (existing == null)
        {
            existing = new ControlSetting
            {
                Key = key,
                Value = value,
                Description = definition.Description,
                UpdatedAt = DateTime.UtcNow,
                UpdatedByUserId = actorUserId,
                UpdatedByUsername = actorUsername
            };
            _db.ControlSettings.Add(existing);
        }
        else
        {
            existing.Value = value;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedByUserId = actorUserId;
            existing.UpdatedByUsername = actorUsername;
            existing.Description = definition.Description;
        }

        _db.ControlSettingHistories.Add(new ControlSettingHistory
        {
            ControlSettingId = existing.Id == 0 ? null : existing.Id,
            Key = key,
            Value = previousValue,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedByUserId = actorUserId,
            UpdatedByUsername = actorUsername
        });

        await _db.SaveChangesAsync(ct);
        _cache.Invalidate();

        return (await GetAllAsync(ct)).First(detail => detail.Definition.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<ControlSettingDetail> RestoreFromHistoryAsync(int historyId, int? actorUserId, string? actorUsername, string? reason, CancellationToken ct = default)
    {
        var history = await _db.ControlSettingHistories.FirstOrDefaultAsync(h => h.Id == historyId, ct)
            ?? throw new InvalidOperationException($"History entry {historyId} was not found.");

        var description = string.IsNullOrWhiteSpace(reason)
            ? $"Rollback to snapshot {historyId}"
            : reason.Trim();

        return await UpdateAsync(history.Key, history.Value, actorUserId, actorUsername, description, ct);
    }

    private static void ValidateValue(ControlSettingDefinition definition, string value)
    {
        if (definition.IsBoolean)
        {
            if (!bool.TryParse(value, out _))
            {
                throw new InvalidOperationException($"{definition.Label} expects a boolean value.");
            }
            return;
        }

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            throw new InvalidOperationException($"{definition.Label} expects a numeric value.");
        }

        if (definition.Min.HasValue && parsed < definition.Min.Value)
        {
            throw new InvalidOperationException($"{definition.Label} must be >= {definition.Min.Value}.");
        }

        if (definition.Max.HasValue && parsed > definition.Max.Value)
        {
            throw new InvalidOperationException($"{definition.Label} must be <= {definition.Max.Value}.");
        }
    }
}
