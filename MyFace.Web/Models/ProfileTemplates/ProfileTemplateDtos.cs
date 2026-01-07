using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using MyFace.Core.Entities;
using MyFace.Services.ProfileTemplates;

namespace MyFace.Web.Models.ProfileTemplates;

public sealed record ProfileSnapshotResponse(
    ProfileSettingsDto Settings,
    IReadOnlyList<ProfilePanelDto> Panels,
    ProfileThemeDto Theme)
{
    public static ProfileSnapshotResponse FromSnapshot(ProfileTemplateSnapshot snapshot)
    {
        return new ProfileSnapshotResponse(
            ProfileSettingsDto.FromEntity(snapshot.Settings),
            snapshot.Panels.Select(ProfilePanelDto.FromEntity).ToList(),
            ProfileThemeDto.FromTheme(snapshot.Theme));
    }
}

public sealed record ProfileSettingsDto
{
    public int Id { get; init; }
    public int UserId { get; init; }
    public ProfileTemplate TemplateType { get; init; }
    public string? ThemePreset { get; init; }
    public string ThemeOverridesJson { get; init; } = string.Empty;
    public bool IsCustomHtml { get; init; }
    public string? CustomHtmlPath { get; init; }
    public DateTime? CustomHtmlUploadDate { get; init; }
    public bool CustomHtmlValidated { get; init; }
    public string? CustomHtmlValidationErrors { get; init; }
    public int CustomHtmlVersion { get; init; }
    public DateTime LastEditedAt { get; init; }
    public int? LastEditedByUserId { get; init; }

    public static ProfileSettingsDto FromEntity(UserProfileSettings settings)
    {
        return new ProfileSettingsDto
        {
            Id = settings.Id,
            UserId = settings.UserId,
            TemplateType = settings.TemplateType,
            ThemePreset = settings.ThemePreset,
            ThemeOverridesJson = settings.ThemeOverridesJson,
            IsCustomHtml = settings.IsCustomHtml,
            CustomHtmlPath = settings.CustomHtmlPath,
            CustomHtmlUploadDate = settings.CustomHtmlUploadDate,
            CustomHtmlValidated = settings.CustomHtmlValidated,
            CustomHtmlValidationErrors = settings.CustomHtmlValidationErrors,
            CustomHtmlVersion = settings.CustomHtmlVersion,
            LastEditedAt = settings.LastEditedAt,
            LastEditedByUserId = settings.LastEditedByUserId
        };
    }
}

public sealed record ProfilePanelDto
{
    public int Id { get; init; }
    public ProfilePanelType PanelType { get; init; }
    public string Content { get; init; } = string.Empty;
    public string ContentFormat { get; init; } = "markdown";
    public int Position { get; init; }
    public bool IsVisible { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public int? LastEditedByUserId { get; init; }
    public string? ValidationMessage { get; init; }
    public bool RequiresModeration { get; init; }

    public static ProfilePanelDto FromEntity(ProfilePanel panel)
    {
        return new ProfilePanelDto
        {
            Id = panel.Id,
            PanelType = panel.PanelType,
            Content = panel.Content,
            ContentFormat = panel.ContentFormat,
            Position = panel.Position,
            IsVisible = panel.IsVisible,
            CreatedAt = panel.CreatedAt,
            UpdatedAt = panel.UpdatedAt,
            LastEditedByUserId = panel.LastEditedByUserId,
            ValidationMessage = panel.ValidationMessage,
            RequiresModeration = panel.RequiresModeration
        };
    }
}

public sealed record ProfileThemeDto(string? Preset, IReadOnlyDictionary<string, string> Overrides)
{
    public static ProfileThemeDto FromTheme(ProfileTheme theme)
    {
        return new ProfileThemeDto(theme.Preset, theme.Overrides);
    }
}

public class SelectTemplateRequest
{
    [Required]
    public ProfileTemplate Template { get; init; } = ProfileTemplate.Minimal;
}

public class ApplyThemeRequest
{
    [MaxLength(64)]
    public string? Preset { get; init; }

    public IDictionary<string, string>? Overrides { get; init; }
}

public class CreatePanelRequest
{
    [Required]
    public ProfilePanelType PanelType { get; init; }

    [Required]
    [MaxLength(8000)]
    public string Content { get; init; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string ContentFormat { get; init; } = "markdown";
}

public class UpdatePanelRequest
{
    [Required]
    [MaxLength(8000)]
    public string Content { get; init; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string ContentFormat { get; init; } = "markdown";
}

public class TogglePanelVisibilityRequest
{
    public bool IsVisible { get; init; }
}

public class ReorderPanelRequest
{
    [Range(1, int.MaxValue)]
    public int Position { get; init; }
}

public sealed record PublicProfileResponse(
    string Username,
    string DisplayName,
    bool UsesCustomHtml,
    string Mode,
    string? CustomHtmlUrl,
    ProfileSnapshotResponse Snapshot);
