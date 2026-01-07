using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using MyFace.Core.Entities;

namespace MyFace.Web.Models.ProfileTemplates;

public static class TemplateRenderViewModelFactory
{
    public static TemplateRenderViewModel Create(PublicProfileResponse response, IDictionary<string, string>? fallbackTokens = null)
    {
        var cssTokens = TemplateThemeTokens.Create(response.Snapshot.Theme, fallbackTokens);
        return new TemplateRenderViewModel(response, cssTokens);
    }
}

public sealed class TemplateRenderViewModel
{
    private readonly IReadOnlyDictionary<ProfilePanelType, ProfilePanelDto> _panelMap;

    public TemplateRenderViewModel(PublicProfileResponse payload, IReadOnlyDictionary<string, string> cssVariables)
    {
        Payload = payload;
        CssVariables = cssVariables;
        Template = payload.Snapshot.Settings.TemplateType;
        _panelMap = payload.Snapshot.Panels
            .GroupBy(p => p.PanelType)
            .Select(g => g.OrderBy(p => p.Position).First())
            .ToDictionary(p => p.PanelType, p => p);
    }

    public PublicProfileResponse Payload { get; }
    public ProfileTemplate Template { get; }
    public IReadOnlyDictionary<string, string> CssVariables { get; }

    public string Username => Payload.Username;
    public string DisplayName => Payload.DisplayName;
    public bool UsesCustomHtml => Payload.UsesCustomHtml;
    public string Mode => Payload.Mode;
    public string? CustomHtmlUrl => Payload.CustomHtmlUrl;
    public IReadOnlyList<ProfilePanelDto> Panels => Payload.Snapshot.Panels;

    public ProfilePanelDto? GetPanel(ProfilePanelType type)
    {
        return _panelMap.TryGetValue(type, out var panel) ? panel : null;
    }

    public IReadOnlyList<ProfilePanelDto> GetPanels(params ProfilePanelType[] types)
    {
        var items = new List<ProfilePanelDto>();
        foreach (var type in types)
        {
            var panel = GetPanel(type);
            if (panel != null)
            {
                items.Add(panel);
            }
        }

        return items;
    }
}

public static class TemplateThemeTokens
{
    private static readonly Dictionary<string, string> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bg"] = "#050816",
        ["text"] = "#e5e7eb",
        ["muted"] = "#94a3b8",
        ["accent"] = "#60a5fa",
        ["border"] = "#1f2937",
        ["panel"] = "#0f172a",
        ["buttonBg"] = "#2563eb",
        ["buttonText"] = "#ffffff",
        ["buttonBorder"] = "#2563eb"
    };

    // Preset token maps mirror the options surfaced in Profile Studio.
    // Keys align with ThemePresetViewModel entries and UserController preset mapping.
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> PresetTokens
        = new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["classic-light"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bg"] = "#f6f7fb",
                ["text"] = "#1f2933",
                ["accent"] = "#2f6fe4",
                ["border"] = "#d9dee7",
                ["panel"] = "#ffffff",
                ["buttonBg"] = "#2f6fe4",
                ["buttonText"] = "#ffffff",
                ["buttonBorder"] = "#2f6fe4"
            },
            ["midnight"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bg"] = "#0f172a",
                ["text"] = "#e5e7eb",
                ["accent"] = "#8b5cf6",
                ["border"] = "#1f2937",
                ["panel"] = "#0f172a",
                ["buttonBg"] = "#8b5cf6",
                ["buttonText"] = "#ffffff",
                ["buttonBorder"] = "#8b5cf6"
            },
            ["forest"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bg"] = "#0f2214",
                ["text"] = "#e3f4e3",
                ["accent"] = "#34d399",
                ["border"] = "#14532d",
                ["panel"] = "#0f2214",
                ["buttonBg"] = "#22c55e",
                ["buttonText"] = "#0b1f12",
                ["buttonBorder"] = "#22c55e"
            },
            ["sunrise"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bg"] = "#2b1b12",
                ["text"] = "#fef3c7",
                ["accent"] = "#f59e0b",
                ["border"] = "#7c2d12",
                ["panel"] = "#2b1b12",
                ["buttonBg"] = "#f59e0b",
                ["buttonText"] = "#2b1b12",
                ["buttonBorder"] = "#f59e0b"
            },
            ["ocean"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bg"] = "#0b1f2a",
                ["text"] = "#e0f2fe",
                ["accent"] = "#06b6d4",
                ["border"] = "#0c4a6e",
                ["panel"] = "#0b1f2a",
                ["buttonBg"] = "#06b6d4",
                ["buttonText"] = "#082f49",
                ["buttonBorder"] = "#06b6d4"
            },
            ["ember"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bg"] = "#111827",
                ["text"] = "#e5e7eb",
                ["accent"] = "#f97316",
                ["border"] = "#1f2937",
                ["panel"] = "#111827",
                ["buttonBg"] = "#f97316",
                ["buttonText"] = "#0b0f19",
                ["buttonBorder"] = "#f97316"
            },
            ["pastel"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bg"] = "#f5f3ff",
                ["text"] = "#312e81",
                ["accent"] = "#a5b4fc",
                ["border"] = "#cbd5e1",
                ["panel"] = "#ffffff",
                ["buttonBg"] = "#a5b4fc",
                ["buttonText"] = "#0b1220",
                ["buttonBorder"] = "#a5b4fc"
            },
            ["slate"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bg"] = "#0f172a",
                ["text"] = "#e2e8f0",
                ["accent"] = "#38bdf8",
                ["border"] = "#1f2937",
                ["panel"] = "#0f172a",
                ["buttonBg"] = "#38bdf8",
                ["buttonText"] = "#0b1220",
                ["buttonBorder"] = "#38bdf8"
            },
            ["mono"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["bg"] = "#0b0b0f",
                ["text"] = "#f8fafc",
                ["accent"] = "#ffffff",
                ["border"] = "#1f1f24",
                ["panel"] = "#0b0b0f",
                ["buttonBg"] = "#ffffff",
                ["buttonText"] = "#0b0b0f",
                ["buttonBorder"] = "#ffffff"
            }
        };

    public static IReadOnlyDictionary<string, string> Create(ProfileThemeDto? theme, IDictionary<string, string>? fallbackTokens)
    {
        var buffer = new Dictionary<string, string>(Defaults, StringComparer.OrdinalIgnoreCase);

        if (fallbackTokens != null)
        {
            foreach (var kvp in fallbackTokens)
            {
                ApplyToken(buffer, kvp.Key, kvp.Value);
            }
        }

        // Apply preset tokens if a known preset is selected; this intentionally
        // overrides the fallback/user tokens before applying explicit overrides.
        var presetKey = theme?.Preset;
        if (!string.IsNullOrWhiteSpace(presetKey) && PresetTokens.TryGetValue(presetKey.Trim(), out var preset))
        {
            foreach (var kvp in preset)
            {
                ApplyToken(buffer, kvp.Key, kvp.Value);
            }
        }

        if (theme?.Overrides != null)
        {
            foreach (var kvp in theme.Overrides)
            {
                ApplyToken(buffer, kvp.Key, kvp.Value);
            }
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["--profile-bg"] = buffer["bg"],
            ["--text-main"] = buffer["text"],
            ["--text-muted"] = buffer["muted"],
            ["--accent"] = buffer["accent"],
            ["--panel-border"] = buffer["border"],
            ["--panel-surface"] = buffer["panel"],
            ["--btn-bg"] = buffer["buttonBg"],
            ["--btn-text"] = buffer["buttonText"],
            ["--btn-border"] = buffer["buttonBorder"]
        };
    }

    private static void ApplyToken(IDictionary<string, string> buffer, string? key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = NormalizeKey(key);
        if (normalized == null)
        {
            return;
        }

        buffer[normalized] = value.Trim();
    }

    private static string? NormalizeKey(string raw)
    {
        return raw.Trim().ToLowerInvariant() switch
        {
            "bg" or "background" or "backgroundcolor" or "background-color" => "bg",
            "text" or "font" or "fontcolor" or "font-color" => "text",
            "muted" or "secondary" => "muted",
            "accent" => "accent",
            "border" or "panelborder" or "panel-border" => "border",
            "panel" or "panelbg" or "panel-background" => "panel",
            "button" or "buttonbg" or "button-background" => "buttonBg",
            "buttontext" or "button-text" => "buttonText",
            "buttonborder" or "button-border" => "buttonBorder",
            _ => null
        };
    }
}

public static class TemplatePanelContentFormatter
{
    private static readonly HtmlEncoder Encoder = HtmlEncoder.Default;

    public static string Render(ProfilePanelDto? panel)
    {
        if (panel == null)
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(panel.Content))
        {
            return "<p class=\"template-panel-placeholder\">Awaiting content.</p>";
        }

        if (string.Equals(panel.ContentFormat, "html", StringComparison.OrdinalIgnoreCase))
        {
            return panel.Content;
        }

        var encoded = Encoder.Encode(panel.Content);
        var segments = encoded
            .Replace("\r\n", "\n")
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(chunk => $"<p>{chunk.Replace("\n", "<br />")}</p>");

        return string.Join(Environment.NewLine, segments);
    }

    public static string RenderHeadline(ProfilePanelDto? panel)
    {
        if (panel == null || string.IsNullOrWhiteSpace(panel.Content))
        {
            return string.Empty;
        }

        var firstLine = panel.Content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return firstLine == null ? string.Empty : Encoder.Encode(firstLine);
    }
}

public sealed record TemplatePanelCardModel(string Title, ProfilePanelDto? Panel, string? Icon = null, bool Emphasized = false);

public static class TemplateThemeTokenHelper
{
    public static IDictionary<string, string> FromUser(User user)
    {
        static string Normalize(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bg"] = Normalize(user.BackgroundColor, "#050816"),
            ["text"] = Normalize(user.FontColor, "#e5e7eb"),
            ["muted"] = Normalize(user.FontColor, "#94a3b8"),
            ["accent"] = Normalize(user.AccentColor, "#60a5fa"),
            ["border"] = Normalize(user.BorderColor, "#1f2937"),
            ["panel"] = Normalize(user.BackgroundColor, "#0f172a"),
            ["buttonBg"] = Normalize(user.ButtonBackgroundColor, "#2563eb"),
            ["buttonText"] = Normalize(user.ButtonTextColor, "#ffffff"),
            ["buttonBorder"] = Normalize(user.ButtonBorderColor, "#2563eb")
        };
    }
}
