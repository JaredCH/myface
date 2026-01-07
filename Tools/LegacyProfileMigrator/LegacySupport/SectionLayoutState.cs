namespace LegacyProfileMigrator.LegacySupport;

internal record SectionLayoutState
{
    public bool Enabled { get; init; }
    public string Placement { get; init; } = "c1";
    public string? CustomTitle { get; init; }
    public string TitleAlignment { get; init; } = "left";
    public string ContentAlignment { get; init; } = "left";
    public string? PanelBackground { get; init; }
    public string? HeaderBackground { get; init; }
    public string? HeaderTextColor { get; init; }
    public string? ContentTextColor { get; init; }
}
