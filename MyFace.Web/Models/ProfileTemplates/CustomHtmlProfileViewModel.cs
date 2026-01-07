namespace MyFace.Web.Models.ProfileTemplates;

public sealed class CustomHtmlProfileViewModel
{
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string ProfileUrl { get; init; } = string.Empty;
    public string WarningText { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
}
