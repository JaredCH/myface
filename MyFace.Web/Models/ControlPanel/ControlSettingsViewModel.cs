using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using MyFace.Services;

namespace MyFace.Web.Models.ControlPanel;

public class ControlSettingsViewModel
{
    private ControlSettingsViewModel(IReadOnlyList<ControlSettingCategoryViewModel> categories, string? highlightKey)
    {
        Categories = categories;
        HighlightKey = highlightKey;
    }

    public IReadOnlyList<ControlSettingCategoryViewModel> Categories { get; }
    public string? HighlightKey { get; }

    public static ControlSettingsViewModel From(IReadOnlyList<ControlSettingDetail> details, string? highlightKey = null)
    {
        var groups = details
            .GroupBy(d => d.Definition.Category)
            .OrderBy(g => g.Key)
            .Select(group => new ControlSettingCategoryViewModel(
                group.Key,
                group
                    .OrderBy(detail => detail.Definition.Label)
                    .Select(detail => new ControlSettingDisplayViewModel(detail, highlightKey))
                    .ToList()))
            .ToList();

        return new ControlSettingsViewModel(groups, highlightKey);
    }
}

public class ControlSettingCategoryViewModel
{
    public ControlSettingCategoryViewModel(string name, IReadOnlyList<ControlSettingDisplayViewModel> settings)
    {
        Name = name;
        Settings = settings;
    }

    public string Name { get; }
    public IReadOnlyList<ControlSettingDisplayViewModel> Settings { get; }
}

public class ControlSettingDisplayViewModel
{
    public ControlSettingDisplayViewModel(ControlSettingDetail detail, string? highlightKey)
    {
        Definition = detail.Definition;
        Key = detail.Definition.Key;
        Label = detail.Definition.Label;
        Description = detail.Definition.Description;
        DefaultValue = detail.Definition.DefaultValue;
        DataType = detail.Definition.DataType;
        Min = detail.Definition.Min;
        Max = detail.Definition.Max;
        CurrentValue = detail.CurrentValue?.Value ?? detail.Definition.DefaultValue;
        UpdatedAtUtc = detail.CurrentValue?.UpdatedAt;
        UpdatedBy = detail.CurrentValue?.UpdatedByUsername;
        IsHighlighted = !string.IsNullOrEmpty(highlightKey) && string.Equals(Key, highlightKey, StringComparison.OrdinalIgnoreCase);
        History = detail.History
            .OrderByDescending(h => h.CreatedAt)
            .Take(5)
            .Select(h => new ControlSettingHistoryViewModel(h.Id, h.Value, h.Reason, h.UpdatedByUsername, h.CreatedAt))
            .ToList();
    }

    public ControlSettingDefinition Definition { get; }
    public string Key { get; }
    public string Label { get; }
    public string Description { get; }
    public string DefaultValue { get; }
    public ControlSettingDataType DataType { get; }
    public double? Min { get; }
    public double? Max { get; }
    public string CurrentValue { get; }
    public DateTime? UpdatedAtUtc { get; }
    public string? UpdatedBy { get; }
    public bool IsHighlighted { get; }
    public IReadOnlyList<ControlSettingHistoryViewModel> History { get; }

    public string CurrentValueDisplay => DataType == ControlSettingDataType.Boolean
        ? (bool.TryParse(CurrentValue, out var parsed) && parsed ? "Enabled" : "Disabled")
        : CurrentValue;

    public string DefaultValueDisplay => DataType == ControlSettingDataType.Boolean
        ? (bool.TryParse(DefaultValue, out var parsed) && parsed ? "Enabled" : "Disabled")
        : DefaultValue;

    public string UpdatedAtDisplay => UpdatedAtUtc?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) ?? "Never";

    public bool IsBoolean => DataType == ControlSettingDataType.Boolean;
}

public record ControlSettingHistoryViewModel(int Id, string Value, string? Reason, string? UpdatedBy, DateTime CreatedAt)
{
    public string ValueDisplay => Value;
    public string CreatedAtDisplay => CreatedAt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
};
