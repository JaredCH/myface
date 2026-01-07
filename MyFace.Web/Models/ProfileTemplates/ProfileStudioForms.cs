using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MyFace.Core.Entities;

namespace MyFace.Web.Models.ProfileTemplates;

public class TemplateSelectionForm
{
    [Required]
    public ProfileTemplate Template { get; init; }
}

public class PanelCreateForm
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

public class PanelEditForm
{
    [Required]
    public int PanelId { get; init; }

    [Required]
    [MaxLength(8000)]
    public string Content { get; init; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string ContentFormat { get; init; } = "markdown";

    [Range(1, int.MaxValue)]
    public int Position { get; init; }

    public bool IsVisible { get; init; }
}

public class PanelDeleteForm
{
    [Required]
    public int PanelId { get; init; }
}

public class ThemeForm
{
    [MaxLength(64)]
    public string? Preset { get; init; }

    [MaxLength(64)]
    public string? Bg { get; init; }

    [MaxLength(64)]
    public string? Text { get; init; }

    [MaxLength(64)]
    public string? Accent { get; init; }

    [MaxLength(64)]
    public string? Border { get; init; }

    [MaxLength(64)]
    public string? Panel { get; init; }

    [MaxLength(64)]
    public string? ButtonBg { get; init; }

    [MaxLength(64)]
    public string? ButtonText { get; init; }

    [MaxLength(64)]
    public string? ButtonBorder { get; init; }

    public IDictionary<string, string> BuildOverrides()
    {
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        AddIfPresent(overrides, "bg", Bg);
        AddIfPresent(overrides, "text", Text);
        AddIfPresent(overrides, "accent", Accent);
        AddIfPresent(overrides, "border", Border);
        AddIfPresent(overrides, "panel", Panel);
        AddIfPresent(overrides, "buttonBg", ButtonBg);
        AddIfPresent(overrides, "buttonText", ButtonText);
        AddIfPresent(overrides, "buttonBorder", ButtonBorder);
        return overrides;
    }

    private static void AddIfPresent(IDictionary<string, string> store, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            store[key] = value.Trim();
        }
    }
}

public class CustomHtmlForm
{
    private const int MaxInlineBytes = 512_000;

    [Required]
    [MaxLength(MaxInlineBytes)]
    public string Html { get; init; } = string.Empty;
}
