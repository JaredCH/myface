using System;

namespace MyFace.Core.Entities;

public class UserProfileSettings
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ProfileTemplate TemplateType { get; set; } = ProfileTemplate.Minimal;
    public string? ThemePreset { get; set; }
    public string ThemeOverridesJson { get; set; } = string.Empty;
    public bool IsCustomHtml { get; set; }
    public string? CustomHtmlPath { get; set; }
    public DateTime? CustomHtmlUploadDate { get; set; }
    public bool CustomHtmlValidated { get; set; }
    public string? CustomHtmlValidationErrors { get; set; }
    public int CustomHtmlVersion { get; set; }
    public DateTime LastEditedAt { get; set; } = DateTime.UtcNow;
    public int? LastEditedByUserId { get; set; }

    public User? User { get; set; }
    public User? LastEditedByUser { get; set; }
}
