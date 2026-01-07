using System.Collections.Generic;
using MyFace.Core.Entities;
using MyFace.Web.Models.CustomHtml;

namespace MyFace.Web.Models.ProfileTemplates;

public class ProfileStudioViewModel
{
    public string Username { get; init; } = string.Empty;
    public ProfileSnapshotResponse Snapshot { get; init; } = null!;
    public IReadOnlyDictionary<string, string> ThemeTokens { get; init; } = new Dictionary<string, string>();
    public IReadOnlyList<PanelTypeOptionViewModel> PanelTypeOptions { get; init; } = new List<PanelTypeOptionViewModel>();
    public IReadOnlyDictionary<ProfilePanelType, string> PanelLabels { get; init; } = new Dictionary<ProfilePanelType, string>();
    public IReadOnlyList<ThemePresetViewModel> ThemePresets { get; init; } = new List<ThemePresetViewModel>();
    public CustomHtmlStatusResponse? CustomHtmlStatus { get; init; }
    public TemplateRenderViewModel? PreviewProfile { get; init; }
    public string? SuccessMessage { get; init; }
    public string? ErrorMessage { get; init; }
}

public sealed record PanelTypeOptionViewModel(ProfilePanelType Type, string Label, bool IsAvailable);

public sealed record ThemePresetViewModel(string Key, string Label, string Background, string Text, string Accent);
