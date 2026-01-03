using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MyFace.Web.Models;

namespace MyFace.Web.Layout;

public static class ProfileLayoutEngine
{
    public const int GridColumns = 6;
    public const int GridRows = 8;
    public const int GridCellCount = GridColumns * GridRows;

    private static readonly Dictionary<string, PlacementBlueprint> BasePlacementBlueprints = new(StringComparer.OrdinalIgnoreCase)
    {
        ["lb1"] = new("lb1", "1×1", 1, 2, 2, 3, FillRect(1, 1, 2, 2)),
        ["lb2"] = new("lb2", "1×2", 1, 2, 2, 4, FillRect(1, 1, 2, 3)),
        ["lb3"] = new("lb3", "1×3", 1, 2, 2, 5, FillRect(1, 1, 2, 4)),
        ["lb4"] = new("lb4", "1×4", 1, 2, 2, 6, FillRect(1, 1, 2, 5)),
        ["rb1"] = new("rb1", "1×1", 4, 5, 2, 3, FillRect(6, 6, 2, 2)),
        ["rb2"] = new("rb2", "1×2", 4, 5, 2, 4, FillRect(6, 6, 2, 3)),
        ["rb3"] = new("rb3", "1×3", 4, 5, 2, 5, FillRect(6, 6, 2, 4)),
        ["rb4"] = new("rb4", "1×4", 4, 5, 2, 6, FillRect(6, 6, 2, 5)),
        ["rail2-left"] = new("rail2-left", "Rail L · 2×2", 1, 3, 2, 4, FillRect(1, 2, 2, 3)),
        ["rail2-right"] = new("rail2-right", "Rail R · 2×2", 4, 6, 2, 4, FillRect(5, 6, 2, 3)),
        ["c1-slim"] = new("c1-slim", "4×1", 1, 5, 2, 3, FillRect(2, 5, 2, 2)),
        ["c1"] = new("c1", "4×2", 1, 5, 2, 4, FillRect(2, 5, 2, 3)),
        ["c1-tall"] = new("c1-tall", "4×3", 1, 5, 2, 5, FillRect(2, 5, 2, 4)),
        ["c1-grand"] = new("c1-grand", "4×4", 1, 5, 2, 6, FillRect(2, 5, 2, 5)),
        ["c1-max"] = new("c1-max", "4×5", 1, 5, 2, 7, FillRect(2, 5, 2, 6)),
        ["csq-left"] = new("csq-left", "L · 2×2", 2, 4, 4, 6, FillRect(2, 3, 4, 5)),
        ["csq-right"] = new("csq-right", "R · 2×2", 3, 5, 4, 6, FillRect(4, 5, 4, 5))
    };

    private static readonly Dictionary<string, List<PlacementBlueprint>> PlacementVariants = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, PlacementBlueprint> PlacementVariantLookup = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, HashSet<string>> PlacementConflicts = new(StringComparer.OrdinalIgnoreCase);

    private static readonly List<SectionBlueprint> SectionBlueprintsList = new()
    {
        new("about", "About me", "aboutEnabled", "aboutPlacement", true, "c1", new[] { "c1-slim", "c1", "c1-tall", "c1-grand", "c1-max", "csq-left", "csq-right", "rail2-left", "rail2-right", "lb1", "lb2", "lb3", "lb4", "rb1", "rb2", "rb3", "rb4" }),
        new("extra", "Extra / Misc", "shopEnabled", "extraPlacement", true, "csq-left", new[] { "c1-slim", "c1", "c1-tall", "c1-grand", "c1-max", "csq-left", "csq-right", "rail2-left", "rail2-right", "lb1", "lb2", "lb3", "lb4", "rb1", "rb2", "rb3", "rb4" }),
        new("policies", "Policies", "policiesEnabled", "policiesPlacement", false, "csq-right", new[] { "c1-slim", "c1", "c1-tall", "c1-grand", "c1-max", "csq-left", "csq-right", "rail2-left", "rail2-right", "lb1", "lb2", "lb3", "lb4", "rb1", "rb2", "rb3", "rb4" }),
        new("payments", "Payments", "paymentsEnabled", "paymentsPlacement", false, "rb1", new[] { "c1-slim", "c1", "c1-tall", "c1-grand", "c1-max", "csq-left", "csq-right", "rail2-left", "rail2-right", "lb1", "lb2", "lb3", "lb4", "rb1", "rb2", "rb3", "rb4" }),
        new("external", "External references", "referencesEnabled", "externalPlacement", false, "rb2", new[] { "c1-slim", "c1", "c1-tall", "c1-grand", "c1-max", "csq-left", "csq-right", "rail2-left", "rail2-right", "lb1", "lb2", "lb3", "lb4", "rb1", "rb2", "rb3", "rb4" }),
        new("news", "News reel", "newsEnabled", "newsPlacement", false, "csq-left", new[] { "c1-slim", "c1", "c1-tall", "c1-grand", "c1-max", "csq-left", "csq-right", "rail2-left", "rail2-right", "lb1", "lb2", "lb3", "lb4", "rb1", "rb2", "rb3", "rb4" }),
        new("chat", "Profile chat", "chatEnabled", "chatPlacement", false, "csq-right", new[] { "c1-slim", "c1", "c1-tall", "c1-grand", "c1-max", "csq-left", "csq-right" }),
        new("reviews", "Reviews pulse", "reviewsEnabled", "reviewsPlacement", true, "csq-left", new[] { "c1-slim", "c1", "c1-tall", "c1-grand", "c1-max", "csq-left", "csq-right", "rail2-left", "rail2-right", "lb1", "lb2", "lb3", "lb4", "rb1", "rb2", "rb3", "rb4" }),
        new("summary", "Profile summary", "summaryEnabled", "summaryPlacement", true, "csq-right", new[] { "c1-slim", "c1", "c1-tall", "c1-grand", "c1-max", "csq-left", "csq-right", "rail2-left", "rail2-right", "lb1", "lb2", "lb3", "lb4", "rb1", "rb2", "rb3", "rb4" }),
        new("pgp", "PGP panel", "pgpEnabled", "pgpPlacement", false, "c1", new[] { "c1-slim", "c1", "c1-tall", "c1-grand", "csq-left", "csq-right", "rail2-left", "rail2-right", "lb2", "rb2" })
    };

    private static readonly Dictionary<string, SectionBlueprint> SectionLookup = SectionBlueprintsList.ToDictionary(s => s.Key, StringComparer.OrdinalIgnoreCase);
    private sealed record LayoutPresetStep(string Section, bool Enabled, string? BaseSlot);

    private sealed record LayoutPresetMeta(string Key, string Title, string Description, string Badge);

    private static readonly Dictionary<string, LayoutPresetStep[]> LayoutPresetPlans = new(StringComparer.OrdinalIgnoreCase)
    {
        ["minimal"] = new[]
        {
            new LayoutPresetStep("about", true, "c1-slim"),
            new LayoutPresetStep("reviews", true, "lb2"),
            new LayoutPresetStep("pgp", true, "c1")
        },
        ["expanded"] = new[]
        {
            new LayoutPresetStep("about", true, "c1-slim"),
            new LayoutPresetStep("extra", true, "c1-slim"),
            new LayoutPresetStep("reviews", true, "lb2"),
            new LayoutPresetStep("pgp", true, "c1"),
            new LayoutPresetStep("summary", true, "rb2")
        },
        ["pro"] = new[]
        {
            new LayoutPresetStep("about", true, "c1-slim"),
            new LayoutPresetStep("extra", true, "c1-slim"),
            new LayoutPresetStep("reviews", true, "lb2"),
            new LayoutPresetStep("pgp", true, "c1"),
            new LayoutPresetStep("summary", true, "rb2"),
            new LayoutPresetStep("policies", true, "rb2"),
            new LayoutPresetStep("chat", true, "c1-tall")
        }
    };

    private static readonly Dictionary<string, Dictionary<string, SectionLayoutState>> PresetLayouts;
    private static readonly IReadOnlyList<LayoutPresetManifest> PresetCatalog;
    private static readonly Dictionary<string, LayoutPresetManifest> PresetManifestLookup;
    private static readonly LayoutPresetMeta[] LayoutPresetMetadata =
    {
        new("minimal", "Minimal template", "Hero strip, review pillar, and a dedicated PGP proof block.", "Minimal"),
        new("expanded", "Expanded template", "Adds an extra showcase, right-rail summary, and PGP proofs.", "Expanded"),
        new("pro", "Pro template", "Adds policy rail plus a tall chat window under the PGP block.", "Pro")
    };

    private static readonly JsonSerializerOptions PresetSerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly string PresetCatalogJson;

    static ProfileLayoutEngine()
    {
        foreach (var baseEntry in BasePlacementBlueprints.Values)
        {
            var variants = ExpandPlacementVariants(baseEntry).ToList();
            PlacementVariants[baseEntry.Id] = variants;
            foreach (var variant in variants)
            {
                PlacementVariantLookup[variant.Id] = variant;
            }
        }

        var allVariants = PlacementVariantLookup.Values.ToList();
        foreach (var variant in allVariants)
        {
            PlacementConflicts[variant.Id] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        for (var i = 0; i < allVariants.Count; i++)
        {
            for (var j = i + 1; j < allVariants.Count; j++)
            {
                if (!allVariants[i].Overlaps(allVariants[j]))
                {
                    continue;
                }

                PlacementConflicts[allVariants[i].Id].Add(allVariants[j].Id);
                PlacementConflicts[allVariants[j].Id].Add(allVariants[i].Id);
            }
        }

        PresetLayouts = BuildPresetLayouts();
        PresetCatalog = BuildPresetCatalog();
        PresetManifestLookup = PresetCatalog.ToDictionary(p => p.Key, p => p, StringComparer.OrdinalIgnoreCase);

        var jsonPayload = PresetCatalog
            .Select(manifest => new
            {
                manifest.Key,
                manifest.Title,
                manifest.Description,
                manifest.Badge,
                layout = manifest.Layout.ToDictionary(
                    entry => entry.Key,
                    entry => new { enabled = entry.Value.Enabled, placement = entry.Value.Placement },
                    StringComparer.OrdinalIgnoreCase)
            });

        PresetCatalogJson = JsonSerializer.Serialize(jsonPayload, PresetSerializerOptions);
    }

    public static IReadOnlyDictionary<string, PlacementBlueprint> BasePlacements => BasePlacementBlueprints;
    public static IReadOnlyDictionary<string, List<PlacementBlueprint>> PlacementVariantGroups => PlacementVariants;
    public static IReadOnlyDictionary<string, PlacementBlueprint> VariantLookup => PlacementVariantLookup;
    public static IReadOnlyDictionary<string, HashSet<string>> ConflictMap => PlacementConflicts;
    public static IReadOnlyList<SectionBlueprint> SectionBlueprints => SectionBlueprintsList;

    public static Dictionary<string, SectionLayoutState> CreateDefaultLayout()
    {
        return SectionBlueprintsList.ToDictionary(
            section => section.Key,
            section => new SectionLayoutState
            {
                Enabled = section.DefaultEnabled,
                Placement = ResolveDefaultPlacementVariant(section.DefaultPlacement)
            },
            StringComparer.OrdinalIgnoreCase);
    }

    public static string ResolveDefaultPlacementVariant(string baseKey)
    {
        if (string.IsNullOrWhiteSpace(baseKey))
        {
            return baseKey;
        }

        if (PlacementVariants.TryGetValue(baseKey, out var variants) && variants.Count > 0)
        {
            if (BasePlacementBlueprints.TryGetValue(baseKey, out var baseBlueprint))
            {
                var aligned = variants.FirstOrDefault(v => v.RowStart == baseBlueprint.RowStart);
                if (aligned != null)
                {
                    return aligned.Id;
                }
            }

            return variants[0].Id;
        }

        return baseKey;
    }

    public static string NormalizePlacementSelection(string sectionKey, string? selection, string fallbackVariant)
    {
        if (!SectionLookup.TryGetValue(sectionKey, out var section))
        {
            return string.IsNullOrWhiteSpace(selection) ? fallbackVariant : selection;
        }

        var allowedVariants = section.Placements
            .SelectMany(baseKey => PlacementVariants.TryGetValue(baseKey, out var variants)
                ? variants
                : Enumerable.Empty<PlacementBlueprint>())
            .ToList();

        if (!allowedVariants.Any())
        {
            return fallbackVariant;
        }

        if (!string.IsNullOrWhiteSpace(selection))
        {
            var exactMatch = allowedVariants.FirstOrDefault(v => string.Equals(v.Id, selection, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
            {
                return exactMatch.Id;
            }

            var baseMatch = allowedVariants.FirstOrDefault(v => string.Equals(v.BaseKey, selection, StringComparison.OrdinalIgnoreCase));
            if (baseMatch != null)
            {
                var preferredVariantId = ResolveDefaultPlacementVariant(selection);
                var alignedBase = allowedVariants.FirstOrDefault(v => string.Equals(v.Id, preferredVariantId, StringComparison.OrdinalIgnoreCase));
                return alignedBase?.Id ?? baseMatch.Id;
            }
        }

        if (allowedVariants.Any(v => string.Equals(v.Id, fallbackVariant, StringComparison.OrdinalIgnoreCase)))
        {
            return fallbackVariant;
        }

        return allowedVariants[0].Id;
    }

    public static IReadOnlyList<SectionRenderSlot> ComposeLayout(Dictionary<string, SectionLayoutState> layout)
    {
        var slots = new List<SectionRenderSlot>();

        foreach (var section in SectionBlueprintsList)
        {
            var fallback = ResolveDefaultPlacementVariant(section.DefaultPlacement);
            var state = layout.TryGetValue(section.Key, out var entry)
                ? entry
                : new SectionLayoutState { Enabled = section.DefaultEnabled, Placement = fallback };

            var normalizedPlacement = NormalizePlacementSelection(section.Key, state.Placement, fallback);
            if (!PlacementVariantLookup.TryGetValue(normalizedPlacement, out var variant))
            {
                variant = PlacementVariantLookup[fallback];
            }

            var normalizedState = state with
            {
                Placement = normalizedPlacement,
                CustomTitle = NormalizeTitle(state.CustomTitle),
                TitleAlignment = NormalizeAlignment(state.TitleAlignment),
                ContentAlignment = NormalizeAlignment(state.ContentAlignment),
                PanelBackground = NormalizeColor(state.PanelBackground),
                HeaderBackground = NormalizeColor(state.HeaderBackground),
                HeaderTextColor = NormalizeColor(state.HeaderTextColor),
                ContentTextColor = NormalizeColor(state.ContentTextColor)
            };

            slots.Add(new SectionRenderSlot(section, normalizedState, normalizedPlacement, variant));
        }

        return slots;
    }

    public static bool TryGetPresetLayout(string? key, out Dictionary<string, SectionLayoutState> layout)
    {
        layout = null!;
        if (string.IsNullOrWhiteSpace(key) || !PresetLayouts.TryGetValue(key, out var preset))
        {
            return false;
        }

        layout = CloneLayout(preset);
        return true;
    }

    public static Dictionary<string, SectionLayoutState> CreatePresetLayout(string key)
    {
        return TryGetPresetLayout(key, out var layout) ? layout : CreateDefaultLayout();
    }

    public static IReadOnlyList<LayoutPresetManifest> GetPresetCatalog() => PresetCatalog;

    public static string GetPresetCatalogJson() => PresetCatalogJson;

    public static bool TryGetPresetManifest(string? key, out LayoutPresetManifest? manifest)
    {
        manifest = null;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        return PresetManifestLookup.TryGetValue(key, out manifest);
    }

    private static string? NormalizeTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string NormalizeAlignment(string? value)
    {
        return value?.Trim().ToLowerInvariant() switch
        {
            "center" => "center",
            "right" => "right",
            _ => "left"
        };
    }

    private static string? NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 7 && trimmed[0] == '#' && trimmed.Skip(1).All(Uri.IsHexDigit))
        {
            return trimmed;
        }

        return null;
    }

    private static IEnumerable<PlacementBlueprint> ExpandPlacementVariants(PlacementBlueprint blueprint)
    {
        var variants = new List<PlacementBlueprint>();
        var rowSpan = Math.Max(1, blueprint.RowEnd - blueprint.RowStart);
        var maxRowStart = GridRows - rowSpan + 1;

        for (var rowStart = 1; rowStart <= maxRowStart; rowStart++)
        {
            var rowOffset = rowStart - blueprint.RowStart;
            var shiftedCells = ShiftHighlightCells(blueprint.HighlightCells, rowOffset, 0);
            var variantId = $"{blueprint.Id}-r{rowStart}";
            var rowEnd = rowStart + rowSpan;
            variants.Add(new PlacementBlueprint(variantId, blueprint.DimensionLabel, blueprint.ColStart, blueprint.ColEnd, rowStart, rowEnd, shiftedCells, blueprint.Id));
        }

        return variants;
    }

    private static IReadOnlyCollection<int> ShiftHighlightCells(IReadOnlyCollection<int> cells, int rowOffset, int colOffset)
    {
        var shifted = new HashSet<int>();
        foreach (var index in cells)
        {
            var originalRow = (index / GridColumns) + 1;
            var originalCol = (index % GridColumns) + 1;
            var newRow = originalRow + rowOffset;
            var newCol = originalCol + colOffset;

            if (newRow < 1 || newRow > GridRows || newCol < 1 || newCol > GridColumns)
            {
                continue;
            }

            var newIndex = (newRow - 1) * GridColumns + (newCol - 1);
            shifted.Add(newIndex);
        }

        return shifted;
    }

    private static HashSet<int> FillRect(int colStart, int colEnd, int rowStart, int rowEnd)
    {
        var normalizedColStart = Math.Clamp(colStart, 1, GridColumns);
        var normalizedColEnd = Math.Clamp(colEnd, 1, GridColumns);
        var normalizedRowStart = Math.Clamp(rowStart, 1, GridRows);
        var normalizedRowEnd = Math.Clamp(rowEnd, 1, GridRows);

        if (normalizedColEnd < normalizedColStart)
        {
            (normalizedColStart, normalizedColEnd) = (normalizedColEnd, normalizedColStart);
        }

        if (normalizedRowEnd < normalizedRowStart)
        {
            (normalizedRowStart, normalizedRowEnd) = (normalizedRowEnd, normalizedRowStart);
        }

        var miniSet = new HashSet<int>();
        for (var row = normalizedRowStart; row <= normalizedRowEnd; row++)
        {
            for (var col = normalizedColStart; col <= normalizedColEnd; col++)
            {
                var index = (row - 1) * GridColumns + (col - 1);
                miniSet.Add(index);
            }
        }

        return miniSet;
    }

    private static Dictionary<string, Dictionary<string, SectionLayoutState>> BuildPresetLayouts()
    {
        var presets = new Dictionary<string, Dictionary<string, SectionLayoutState>>(StringComparer.OrdinalIgnoreCase);
        foreach (var plan in LayoutPresetPlans)
        {
            presets[plan.Key] = BuildPresetLayout(plan.Value);
        }

        return presets;
    }

    private static IReadOnlyList<LayoutPresetManifest> BuildPresetCatalog()
    {
        var catalog = new List<LayoutPresetManifest>();

        foreach (var meta in LayoutPresetMetadata)
        {
            if (!PresetLayouts.TryGetValue(meta.Key, out var layout))
            {
                continue;
            }

            catalog.Add(new LayoutPresetManifest(meta.Key, meta.Title, meta.Description, meta.Badge, CloneLayout(layout)));
        }

        return catalog;
    }

    private static Dictionary<string, SectionLayoutState> BuildPresetLayout(LayoutPresetStep[] steps)
    {
        var layout = CreateDisabledLayout();
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var step in steps)
        {
            if (!layout.TryGetValue(step.Section, out var current))
            {
                continue;
            }

            var placement = current.Placement;
            if (!string.IsNullOrWhiteSpace(step.BaseSlot))
            {
                placement = ResolvePresetVariant(step.BaseSlot!, reserved);
            }

            var normalizedPlacement = NormalizePlacementSelection(step.Section, placement, current.Placement);
            layout[step.Section] = current with
            {
                Enabled = step.Enabled,
                Placement = normalizedPlacement
            };

            if (step.Enabled)
            {
                reserved.Add(normalizedPlacement);
            }
        }

        return layout;
    }

    private static Dictionary<string, SectionLayoutState> CreateDisabledLayout()
    {
        return SectionBlueprintsList.ToDictionary(
            section => section.Key,
            section => new SectionLayoutState
            {
                Enabled = false,
                Placement = ResolveDefaultPlacementVariant(section.DefaultPlacement)
            },
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolvePresetVariant(string baseKey, HashSet<string> reservedPlacements)
    {
        if (string.IsNullOrWhiteSpace(baseKey))
        {
            return ResolveDefaultPlacementVariant(baseKey);
        }

        if (!PlacementVariants.TryGetValue(baseKey, out var variants) || variants.Count == 0)
        {
            return ResolveDefaultPlacementVariant(baseKey);
        }

        var preferred = ResolveDefaultPlacementVariant(baseKey);
        if (PlacementVariantLookup.TryGetValue(preferred, out var preferredVariant)
            && !reservedPlacements.Contains(preferred)
            && !HasConflicts(preferred, reservedPlacements))
        {
            return preferredVariant.Id;
        }

        foreach (var variant in variants)
        {
            if (variant.Id == preferred)
            {
                continue;
            }

            if (reservedPlacements.Contains(variant.Id))
            {
                continue;
            }

            if (HasConflicts(variant.Id, reservedPlacements))
            {
                continue;
            }

            return variant.Id;
        }

        return variants[0].Id;
    }

    private static bool HasConflicts(string variantId, HashSet<string> reservedPlacements)
    {
        if (!PlacementConflicts.TryGetValue(variantId, out var conflicts) || conflicts.Count == 0)
        {
            return false;
        }

        foreach (var reserved in reservedPlacements)
        {
            if (conflicts.Contains(reserved))
            {
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, SectionLayoutState> CloneLayout(Dictionary<string, SectionLayoutState> source)
    {
        return source.ToDictionary(entry => entry.Key, entry => entry.Value with { }, StringComparer.OrdinalIgnoreCase);
    }
}

public sealed record LayoutPresetManifest(
    string Key,
    string Title,
    string Description,
    string Badge,
    IReadOnlyDictionary<string, SectionLayoutState> Layout);

public sealed class PlacementBlueprint
{
    private readonly HashSet<int> _highlightCells;

    public PlacementBlueprint(string id, string dimensionLabel, int colStart, int colEnd, int rowStart, int rowEnd, IReadOnlyCollection<int> highlightCells, string? baseKey = null)
    {
        Id = id;
        BaseKey = baseKey ?? id;
        DimensionLabel = dimensionLabel;
        ColStart = colStart;
        ColEnd = colEnd;
        RowStart = rowStart;
        RowEnd = rowEnd;
        _highlightCells = new HashSet<int>(highlightCells);
        Bounds = CalculateBounds(_highlightCells);
    }

    public string Id { get; }
    public string BaseKey { get; }
    public string DimensionLabel { get; }
    public int ColStart { get; }
    public int ColEnd { get; }
    public int RowStart { get; }
    public int RowEnd { get; }
    public IReadOnlyCollection<int> HighlightCells => _highlightCells;
    public GridBounds Bounds { get; }

    public bool ContainsCell(int index) => _highlightCells.Contains(index);
    public bool Overlaps(PlacementBlueprint other) => _highlightCells.Overlaps(other._highlightCells);

    private static GridBounds CalculateBounds(HashSet<int> highlight)
    {
        if (highlight.Count == 0)
        {
            return new GridBounds(1, 2, 1, 2);
        }

        var minCol = highlight.Min(idx => (idx % ProfileLayoutEngine.GridColumns) + 1);
        var maxCol = highlight.Max(idx => (idx % ProfileLayoutEngine.GridColumns) + 1);
        var minRow = highlight.Min(idx => (idx / ProfileLayoutEngine.GridColumns) + 1);
        var maxRow = highlight.Max(idx => (idx / ProfileLayoutEngine.GridColumns) + 1);
        return new GridBounds(minCol, maxCol + 1, minRow, maxRow + 1);
    }
}

public sealed class SectionBlueprint
{
    public SectionBlueprint(string key, string title, string toggleName, string placementField, bool defaultEnabled, string defaultPlacement, string[] placements)
    {
        Key = key;
        Title = title;
        ToggleName = toggleName;
        PlacementField = placementField;
        DefaultEnabled = defaultEnabled;
        DefaultPlacement = defaultPlacement;
        Placements = placements;
    }

    public string Key { get; }
    public string Title { get; }
    public string ToggleName { get; }
    public string PlacementField { get; }
    public bool DefaultEnabled { get; }
    public string DefaultPlacement { get; }
    public string[] Placements { get; }
}

public sealed class SectionRenderSlot
{
    public SectionRenderSlot(SectionBlueprint blueprint, SectionLayoutState state, string placementId, PlacementBlueprint placement)
    {
        Blueprint = blueprint;
        State = state;
        Key = blueprint.Key;
        Title = blueprint.Title;
        DisplayTitle = string.IsNullOrWhiteSpace(state.CustomTitle) ? blueprint.Title : state.CustomTitle!;
        Enabled = state.Enabled;
        PlacementId = placementId;
        Placement = placement;
        BaseSlot = placement.BaseKey;
        DimensionLabel = placement.DimensionLabel;
        Bounds = placement.Bounds;
        TitleAlignment = state.TitleAlignment;
        ContentAlignment = state.ContentAlignment;
        PanelBackground = state.PanelBackground;
        HeaderBackground = state.HeaderBackground;
        HeaderTextColor = state.HeaderTextColor;
        ContentTextColor = state.ContentTextColor;
    }

    public SectionBlueprint Blueprint { get; }
    public SectionLayoutState State { get; }
    public string Key { get; }
    public string Title { get; }
    public string DisplayTitle { get; }
    public bool Enabled { get; }
    public string PlacementId { get; }
    public string BaseSlot { get; }
    public string DimensionLabel { get; }
    public PlacementBlueprint Placement { get; }
    public GridBounds Bounds { get; }
    public string TitleAlignment { get; }
    public string ContentAlignment { get; }
    public string? PanelBackground { get; }
    public string? HeaderBackground { get; }
    public string? HeaderTextColor { get; }
    public string? ContentTextColor { get; }
    public int ColumnStart => Bounds.ColumnStart;
    public int ColumnEnd => Bounds.ColumnEnd;
    public int RowStart => Bounds.RowStart;
    public int RowEnd => Bounds.RowEnd;
    public int ColumnSpan => Math.Max(1, ColumnEnd - ColumnStart);
    public int RowSpan => Math.Max(1, RowEnd - RowStart);
}

public readonly record struct GridBounds(int ColumnStart, int ColumnEnd, int RowStart, int RowEnd);
