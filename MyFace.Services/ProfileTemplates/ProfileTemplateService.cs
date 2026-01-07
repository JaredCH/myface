using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services.ProfileTemplates;

public class ProfileTemplateService : IProfileTemplateService
{
    private const int MaxContentLength = 8000;
    private static readonly TimeSpan EditTimestampSkew = TimeSpan.FromSeconds(1);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly HashSet<string> AllowedContentFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "markdown",
        "plaintext",
        "html"
    };

    private static readonly Dictionary<ProfileTemplate, IReadOnlyList<ProfilePanelType>> TemplatePanels = new()
    {
        [ProfileTemplate.Minimal] = new[]
        {
            ProfilePanelType.Summary,
            ProfilePanelType.About,
            ProfilePanelType.Contact
        },
        [ProfileTemplate.Expanded] = new[]
        {
            ProfilePanelType.Summary,
            ProfilePanelType.About,
            ProfilePanelType.Skills,
            ProfilePanelType.Projects,
            ProfilePanelType.Activity,
            ProfilePanelType.Contact
        },
        [ProfileTemplate.Pro] = new[]
        {
            ProfilePanelType.Summary,
            ProfilePanelType.About,
            ProfilePanelType.Projects,
            ProfilePanelType.Skills,
            ProfilePanelType.Testimonials,
            ProfilePanelType.Contact
        },
        [ProfileTemplate.Vendor] = new[]
        {
            ProfilePanelType.Summary,
            ProfilePanelType.Shop,
            ProfilePanelType.Policies,
            ProfilePanelType.Payments,
            ProfilePanelType.References,
            ProfilePanelType.Contact
        },
        [ProfileTemplate.Guru] = new[]
        {
            ProfilePanelType.Summary,
            ProfilePanelType.About,
            ProfilePanelType.Activity,
            ProfilePanelType.Testimonials,
            ProfilePanelType.Contact
        },
        [ProfileTemplate.CustomHtml] = Array.Empty<ProfilePanelType>()
    };

    private readonly ApplicationDbContext _context;
    private readonly ILogger<ProfileTemplateService> _logger;

    public ProfileTemplateService(ApplicationDbContext context, ILogger<ProfileTemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<UserProfileSettings> GetOrCreateSettingsAsync(int userId, int? editorUserId = null, CancellationToken cancellationToken = default)
    {
        var settings = await _context.UserProfileSettings
            .AsTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId, cancellationToken);

        if (settings != null)
        {
            return settings;
        }

        settings = new UserProfileSettings
        {
            UserId = userId,
            TemplateType = ProfileTemplate.Minimal,
            ThemeOverridesJson = "{}",
            CustomHtmlVersion = 0,
            LastEditedAt = DateTime.UtcNow,
            LastEditedByUserId = editorUserId
        };

        _context.UserProfileSettings.Add(settings);
        await _context.SaveChangesAsync(cancellationToken);
        await EnsurePanelsForTemplateAsync(userId, settings.TemplateType, cancellationToken);
        return settings;
    }

    public async Task<UserProfileSettings> UpdateTemplateAsync(int userId, ProfileTemplate template, int? editorUserId = null, CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(userId, editorUserId, cancellationToken);
        if (settings.TemplateType == template)
        {
            return settings;
        }

        settings.TemplateType = template;
        settings.IsCustomHtml = template == ProfileTemplate.CustomHtml;
        settings.LastEditedAt = DateTime.UtcNow;
        settings.LastEditedByUserId = editorUserId;

        if (template != ProfileTemplate.CustomHtml)
        {
            settings.CustomHtmlPath = null;
        }

        await _context.SaveChangesAsync(cancellationToken);
        await EnsurePanelsForTemplateAsync(userId, template, cancellationToken);
        return settings;
    }

    public async Task<UserProfileSettings> ApplyThemeAsync(int userId, string? preset, IDictionary<string, string>? overrides, int? editorUserId = null, CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(userId, editorUserId, cancellationToken);
        settings.ThemePreset = string.IsNullOrWhiteSpace(preset) ? null : preset.Trim();
        settings.ThemeOverridesJson = SerializeOverrides(overrides);
        settings.LastEditedAt = DateTime.UtcNow;
        settings.LastEditedByUserId = editorUserId;
        await _context.SaveChangesAsync(cancellationToken);
        return settings;
    }

    public async Task<ProfileTemplateSnapshot> GetProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(userId, null, cancellationToken);
        IReadOnlyList<ProfilePanel> panels;

        if (settings.TemplateType == ProfileTemplate.CustomHtml)
        {
            panels = Array.Empty<ProfilePanel>();
        }
        else
        {
            panels = await GetPanelsAsync(userId, settings.TemplateType, includeHidden: false, cancellationToken);
        }

        var theme = new ProfileTheme(settings.ThemePreset, ParseOverrides(settings.ThemeOverridesJson));
        return new ProfileTemplateSnapshot(settings, panels, theme);
    }

    public async Task<IReadOnlyList<ProfilePanel>> GetPanelsAsync(int userId, ProfileTemplate? template = null, bool includeHidden = false, CancellationToken cancellationToken = default)
    {
        var query = _context.ProfilePanels
            .AsNoTracking()
            .Where(p => p.UserId == userId);

        if (template.HasValue)
        {
            query = query.Where(p => p.TemplateType == template.Value);
        }

        if (!includeHidden)
        {
            query = query.Where(p => p.IsVisible);
        }

        return await query
            .OrderBy(p => p.Position)
            .ThenBy(p => p.PanelType)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProfilePanel> CreatePanelAsync(int userId, ProfilePanelType panelType, string content, string contentFormat, int? editorUserId = null, CancellationToken cancellationToken = default)
    {
        var settings = await GetOrCreateSettingsAsync(userId, editorUserId, cancellationToken);
        if (settings.TemplateType == ProfileTemplate.CustomHtml)
        {
            throw new InvalidOperationException("Panels cannot be created when the user is using custom HTML.");
        }

        ValidatePanelTypeForTemplate(settings.TemplateType, panelType);
        ValidatePanelContent(content, contentFormat);

        var existingType = await _context.ProfilePanels.AnyAsync(
            p => p.UserId == userId && p.TemplateType == settings.TemplateType && p.PanelType == panelType,
            cancellationToken);

        if (existingType)
        {
            throw new InvalidOperationException($"Panel {panelType} already exists for the active template.");
        }

        var position = await _context.ProfilePanels
            .Where(p => p.UserId == userId && p.TemplateType == settings.TemplateType)
            .Select(p => p.Position)
            .DefaultIfEmpty(0)
            .MaxAsync(cancellationToken) + 1;

        var now = DateTime.UtcNow;
        var panel = new ProfilePanel
        {
            UserId = userId,
            TemplateType = settings.TemplateType,
            PanelType = panelType,
            Content = content.Trim(),
            ContentFormat = NormalizeFormat(contentFormat),
            Position = position,
            IsVisible = true,
            LastEditedByUserId = editorUserId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _context.ProfilePanels.Add(panel);
        await _context.SaveChangesAsync(cancellationToken);
        return panel;
    }

    public async Task<ProfilePanel> UpdatePanelAsync(int userId, int panelId, string content, string contentFormat, int? editorUserId = null, CancellationToken cancellationToken = default)
    {
        var panel = await _context.ProfilePanels
            .FirstOrDefaultAsync(p => p.Id == panelId && p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Panel not found.");

        ValidatePanelContent(content, contentFormat);
        panel.Content = content.Trim();
        panel.ContentFormat = NormalizeFormat(contentFormat);
        panel.LastEditedByUserId = editorUserId;
        panel.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
        return panel;
    }

    public async Task DeletePanelAsync(int userId, int panelId, CancellationToken cancellationToken = default)
    {
        var panel = await _context.ProfilePanels
            .FirstOrDefaultAsync(p => p.Id == panelId && p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Panel not found.");

        _context.ProfilePanels.Remove(panel);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task TogglePanelVisibilityAsync(int userId, int panelId, bool isVisible, int? editorUserId = null, CancellationToken cancellationToken = default)
    {
        var panel = await _context.ProfilePanels
            .FirstOrDefaultAsync(p => p.Id == panelId && p.UserId == userId, cancellationToken)
            ?? throw new InvalidOperationException("Panel not found.");

        panel.IsVisible = isVisible;
        panel.LastEditedByUserId = editorUserId;
        panel.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task ReorderPanelsAsync(int userId, IReadOnlyDictionary<int, int> positions, CancellationToken cancellationToken = default)
    {
        if (positions == null || positions.Count == 0)
        {
            return;
        }

        var ids = positions.Keys.ToList();
        var panels = await _context.ProfilePanels
            .Where(p => p.UserId == userId && ids.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (panels.Count != ids.Count)
        {
            throw new InvalidOperationException("One or more panels could not be found for reordering.");
        }

        foreach (var panel in panels)
        {
            panel.Position = positions[panel.Id];
            panel.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    public IReadOnlyList<ProfilePanelType> GetPanelTypesForTemplate(ProfileTemplate template)
    {
        return TemplatePanels.TryGetValue(template, out var list) ? list : Array.Empty<ProfilePanelType>();
    }

    private async Task EnsurePanelsForTemplateAsync(int userId, ProfileTemplate template, CancellationToken cancellationToken)
    {
        if (template == ProfileTemplate.CustomHtml)
        {
            return;
        }

        if (!TemplatePanels.TryGetValue(template, out var requiredPanels) || requiredPanels.Count == 0)
        {
            return;
        }

        var existing = await _context.ProfilePanels
            .Where(p => p.UserId == userId && p.TemplateType == template)
            .ToListAsync(cancellationToken);

        var existingTypes = existing.Select(p => p.PanelType).ToHashSet();
        var missing = requiredPanels.Where(pt => !existingTypes.Contains(pt)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        var position = existing.Count == 0
            ? 1
            : existing.Max(p => p.Position) + 1;

        _logger.LogInformation("Seeding {MissingCount} profile panels for user {UserId} template {Template}", missing.Count, userId, template);

        foreach (var type in missing)
        {
            var now = DateTime.UtcNow;
            _context.ProfilePanels.Add(new ProfilePanel
            {
                UserId = userId,
                TemplateType = template,
                PanelType = type,
                Content = string.Empty,
                ContentFormat = "markdown",
                Position = position++,
                CreatedAt = now,
                UpdatedAt = now,
                IsVisible = true
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static void ValidatePanelTypeForTemplate(ProfileTemplate template, ProfilePanelType panelType)
    {
        if (template == ProfileTemplate.CustomHtml)
        {
            throw new InvalidOperationException("Custom HTML template does not support panels.");
        }

        if (!TemplatePanels.TryGetValue(template, out var types) || !types.Contains(panelType))
        {
            throw new InvalidOperationException($"Panel type {panelType} is not allowed for template {template}.");
        }
    }

    private static void ValidatePanelContent(string content, string contentFormat)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (Encoding.UTF8.GetByteCount(content) > MaxContentLength)
        {
            throw new InvalidOperationException($"Panel content exceeds {MaxContentLength} bytes.");
        }

        if (string.IsNullOrWhiteSpace(contentFormat))
        {
            throw new ArgumentException("Content format is required.", nameof(contentFormat));
        }

        if (!AllowedContentFormats.Contains(contentFormat.Trim()))
        {
            throw new InvalidOperationException($"Content format '{contentFormat}' is not supported.");
        }
    }

    private static string NormalizeFormat(string format)
    {
        return string.IsNullOrWhiteSpace(format) ? "markdown" : format.Trim().ToLowerInvariant();
    }

    private static string SerializeOverrides(IDictionary<string, string>? overrides)
    {
        if (overrides == null || overrides.Count == 0)
        {
            return "{}";
        }

        var sanitized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in overrides)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                continue;
            }

            if (sanitized.Count >= 32)
            {
                break;
            }

            sanitized[kvp.Key.Trim()] = kvp.Value?.Trim() ?? string.Empty;
        }

        return JsonSerializer.Serialize(sanitized, JsonOptions);
    }

    private static IReadOnlyDictionary<string, string> ParseOverrides(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            return parsed ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }
}
