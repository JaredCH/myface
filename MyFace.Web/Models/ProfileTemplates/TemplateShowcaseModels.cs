using System.Collections.Generic;

namespace MyFace.Web.Models.ProfileTemplates;

public class TemplateShowcaseViewModel
{
    public List<TemplateShowcaseTemplate> Templates { get; init; } = new();
}

public class TemplateShowcaseTemplate
{
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public string ThemeClass { get; init; } = string.Empty;
    public TemplateShowcaseHero Hero { get; init; } = new();
    public List<TemplateShowcasePanel> PrimaryPanels { get; init; } = new();
    public List<TemplateShowcasePanel> SecondaryPanels { get; init; } = new();
    public List<TemplateShowcaseMetric> Metrics { get; init; } = new();
    public List<TemplateShowcaseAction> Actions { get; init; } = new();
    public List<string> FeatureTags { get; init; } = new();
}

public class TemplateShowcaseHero
{
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string Tagline { get; init; } = string.Empty;
    public string Location { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string AvatarUrl { get; init; } = string.Empty;
}

public class TemplateShowcasePanel
{
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public string Icon { get; init; } = string.Empty;
    public bool Emphasized { get; init; }
}

public class TemplateShowcaseMetric
{
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string? Hint { get; init; }
}

public class TemplateShowcaseAction
{
    public string Label { get; init; } = string.Empty;
    public string Variant { get; init; } = "solid";
}
