using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFace.Core.Entities;
using MyFace.Data;
using MyFace.Services.ProfileTemplates;
using LegacyProfileMigrator.LegacySupport;
using MyFace.Web.Services;

namespace LegacyProfileMigrator;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var options = MigrationOptions.Parse(args);

        Console.WriteLine("Legacy Profile Migrator");
        Console.WriteLine(options);

        try
        {
            var factory = new ApplicationDbContextFactory();
            await using var context = factory.CreateDbContext(Array.Empty<string>());

            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddSimpleConsole(o =>
                {
                    o.SingleLine = true;
                    o.TimestampFormat = "HH:mm:ss ";
                });

                if (options.Verbose)
                {
                    builder.SetMinimumLevel(LogLevel.Information);
                }
            });

            var templateService = new ProfileTemplateService(context, loggerFactory.CreateLogger<ProfileTemplateService>());
            var migratorLogger = loggerFactory.CreateLogger<LegacyProfileMigrator>();
            var migrator = new LegacyProfileMigrator(context, templateService, migratorLogger, options);
            await migrator.RunAsync();
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Migration failed: {ex.Message}");
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

internal sealed record MigrationOptions(string? UsernameFilter, bool Force, bool Verbose)
{
    public static MigrationOptions Parse(string[] args)
    {
        string? username = null;
        var force = false;
        var verbose = false;

        foreach (var arg in args)
        {
            switch (arg)
            {
                case "--force":
                case "-f":
                    force = true;
                    break;
                case "--verbose":
                case "-v":
                    verbose = true;
                    break;
                case "--help":
                case "-h":
                    PrintHelp();
                    Environment.Exit(0);
                    break;
                default:
                    if (arg.StartsWith("--user=", StringComparison.OrdinalIgnoreCase))
                    {
                        username = arg.Substring("--user=".Length).Trim();
                    }
                    break;
            }
        }

        return new MigrationOptions(username, force, verbose);
    }

    private static void PrintHelp()
    {
        Console.WriteLine("Usage: dotnet run --project Tools/LegacyProfileMigrator -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --user=<username>   Limit migration to a single username");
        Console.WriteLine("  --force             Overwrite users that already filled out the new template");
        Console.WriteLine("  --verbose           Print per-user diagnostics");
        Console.WriteLine("  -h, --help          Show this help message");
    }

    public override string ToString()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"  Username filter : {UsernameFilter ?? "(all)"}");
        builder.AppendLine($"  Force overwrite : {(Force ? "yes" : "no")}");
        builder.AppendLine($"  Verbose output  : {(Verbose ? "yes" : "no")}");
        return builder.ToString();
    }
}

internal sealed class LegacyProfileMigrator
{
    private static readonly JsonSerializerOptions LayoutSerializer = new(JsonSerializerDefaults.Web);

    private readonly ApplicationDbContext _context;
    private readonly IProfileTemplateService _templateService;
    private readonly ILogger<LegacyProfileMigrator> _logger;
    private readonly MigrationOptions _options;
    private readonly BBCodeFormatter _bbCode = new();

    public LegacyProfileMigrator(
        ApplicationDbContext context,
        IProfileTemplateService templateService,
        ILogger<LegacyProfileMigrator> logger,
        MigrationOptions options)
    {
        _context = context;
        _templateService = templateService;
        _logger = logger;
        _options = options;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        var query = _context.Users.AsNoTracking().OrderBy(u => u.Id);
        if (!string.IsNullOrWhiteSpace(_options.UsernameFilter))
        {
            var normalized = _options.UsernameFilter!.Trim();
            query = query.Where(u => u.Username == normalized);
        }

        var processed = 0;
        var migrated = 0;
        var skipped = 0;

        await foreach (var user in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
        {
            processed++;
            var outcome = await MigrateUserAsync(user, cancellationToken);
            if (outcome.Skipped)
            {
                skipped++;
                if (_options.Verbose && outcome.Reason != null)
                {
                    _logger.LogInformation("Skipping {User}: {Reason}", user.Username, outcome.Reason);
                }
                _context.ChangeTracker.Clear();
                continue;
            }

            if (outcome.HasChanges)
            {
                migrated++;
                _logger.LogInformation("Migrated {User}: template={Template}, theme={Theme}, panels={Panels}",
                    user.Username,
                    outcome.TemplateChanged ? "updated" : "unchanged",
                    outcome.ThemeChanged ? "updated" : "unchanged",
                    outcome.PanelUpdates);
            }

            _context.ChangeTracker.Clear();
        }

        Console.WriteLine($"Processed {processed} users · migrated {migrated} · skipped {skipped}.");
    }

    private async Task<MigrationOutcome> MigrateUserAsync(User user, CancellationToken cancellationToken)
    {
        var hasModernPanels = await _context.ProfilePanels
            .AsNoTracking()
            .AnyAsync(p => p.UserId == user.Id && !string.IsNullOrWhiteSpace(p.Content), cancellationToken);

        if (hasModernPanels && !_options.Force)
        {
            return MigrationOutcome.Skipped($"{user.Username} already uses the new template");
        }

        var hasLegacyContent = HasLegacyContent(user) || await _context.UserContacts.AnyAsync(c => c.UserId == user.Id, cancellationToken);
        if (!hasLegacyContent && !_options.Force)
        {
            return MigrationOutcome.Skipped("no legacy content to migrate");
        }

        var settings = await _templateService.GetOrCreateSettingsAsync(user.Id, null, cancellationToken);
        var targetTemplate = ProfileTemplate.Expanded;

        var templateChanged = false;
        if (settings.TemplateType != targetTemplate)
        {
            await _templateService.UpdateTemplateAsync(user.Id, targetTemplate, null, cancellationToken);
            templateChanged = true;
        }

        var themeChanged = false;
        if (HasCustomTheme(user))
        {
            var overrides = BuildThemeOverrides(user);
            await _templateService.ApplyThemeAsync(user.Id, preset: null, overrides, null, cancellationToken);
            themeChanged = true;
        }

        var visibility = BuildVisibilityMap(user.ProfileLayout);
        var contacts = await _context.UserContacts
            .Where(c => c.UserId == user.Id)
            .OrderBy(c => c.ServiceName)
            .ToListAsync(cancellationToken);
        var payments = ProfileStructuredFields.ParsePayments(user.VendorPayments);
        var references = ProfileStructuredFields.ParseReferences(user.VendorExternalReferences);

        var primarySeeds = BuildPrimarySeeds(user, contacts, visibility);
        var vendorSeeds = BuildVendorSeeds(user, contacts, payments, references, visibility);

        var panelUpdates = 0;
        panelUpdates += await ApplyPanelSeedsAsync(user.Id, targetTemplate, primarySeeds, cancellationToken);
        if (vendorSeeds.Count > 0)
        {
            panelUpdates += await ApplyPanelSeedsAsync(user.Id, ProfileTemplate.Vendor, vendorSeeds, cancellationToken);
        }

        if (panelUpdates > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        if (templateChanged || themeChanged || panelUpdates > 0)
        {
            return MigrationOutcome.Changed(templateChanged, themeChanged, panelUpdates);
        }

        return MigrationOutcome.Skipped("no updates applied");
    }

    private static bool HasLegacyContent(User user)
    {
        return !string.IsNullOrWhiteSpace(user.AboutMe)
            || !string.IsNullOrWhiteSpace(user.VendorShopDescription)
            || !string.IsNullOrWhiteSpace(user.VendorPolicies)
            || !string.IsNullOrWhiteSpace(user.VendorPayments)
            || !string.IsNullOrWhiteSpace(user.VendorExternalReferences);
    }

    private static bool HasCustomTheme(User user)
    {
        return !string.Equals(user.BackgroundColor, "#0f172a", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(user.FontColor, "#e5e7eb", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(user.AccentColor, "#3b82f6", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(user.BorderColor, "#334155", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(user.ButtonBackgroundColor, "#0ea5e9", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(user.ButtonTextColor, "#ffffff", StringComparison.OrdinalIgnoreCase)
            || !string.Equals(user.ButtonBorderColor, "#0ea5e9", StringComparison.OrdinalIgnoreCase);
    }

    private static Dictionary<string, bool> BuildVisibilityMap(string? payload)
    {
        var map = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["about"] = true,
            ["extra"] = false,
            ["policies"] = false,
            ["payments"] = false,
            ["external"] = false
        };

        if (string.IsNullOrWhiteSpace(payload))
        {
            return map;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, SectionLayoutState>>(payload, LayoutSerializer);
            if (parsed != null)
            {
                foreach (var kvp in parsed)
                {
                    map[kvp.Key] = kvp.Value?.Enabled ?? map.GetValueOrDefault(kvp.Key, false);
                }
            }
        }
        catch (JsonException)
        {
            // Ignore malformed layout payloads.
        }

        return map;
    }

    private List<PanelSeed> BuildPrimarySeeds(User user, IReadOnlyList<UserContact> contacts, IReadOnlyDictionary<string, bool> visibility)
    {
        var seeds = new List<PanelSeed>();
        var aboutHtml = RenderAboutHtml(user.AboutMe);
        if (!string.IsNullOrWhiteSpace(aboutHtml))
        {
            seeds.Add(new PanelSeed(ProfilePanelType.About, aboutHtml, "html", IsSectionEnabled(visibility, "about", true)));
        }

        var contactMarkdown = BuildContactMarkdown(contacts);
        if (!string.IsNullOrWhiteSpace(contactMarkdown))
        {
            seeds.Add(new PanelSeed(ProfilePanelType.Contact, contactMarkdown, "markdown", true));
        }

        return seeds;
    }

    private List<PanelSeed> BuildVendorSeeds(
        User user,
        IReadOnlyList<UserContact> contacts,
        IReadOnlyList<PaymentRow> payments,
        IReadOnlyList<ReferenceRow> references,
        IReadOnlyDictionary<string, bool> visibility)
    {
        var seeds = new List<PanelSeed>();

        var shop = NormalizeMarkdown(user.VendorShopDescription);
        if (!string.IsNullOrWhiteSpace(shop))
        {
            seeds.Add(new PanelSeed(ProfilePanelType.Shop, shop, "markdown", IsSectionEnabled(visibility, "extra", true)));
        }

        var policies = NormalizeMarkdown(user.VendorPolicies);
        if (!string.IsNullOrWhiteSpace(policies))
        {
            seeds.Add(new PanelSeed(ProfilePanelType.Policies, policies, "markdown", IsSectionEnabled(visibility, "policies", true)));
        }

        var paymentsMarkdown = BuildPaymentMarkdown(payments);
        if (!string.IsNullOrWhiteSpace(paymentsMarkdown))
        {
            seeds.Add(new PanelSeed(ProfilePanelType.Payments, paymentsMarkdown, "markdown", IsSectionEnabled(visibility, "payments", true)));
        }

        var referencesMarkdown = BuildReferenceMarkdown(references);
        if (!string.IsNullOrWhiteSpace(referencesMarkdown))
        {
            seeds.Add(new PanelSeed(ProfilePanelType.References, referencesMarkdown, "markdown", IsSectionEnabled(visibility, "external", true)));
        }

        var contactMarkdown = BuildContactMarkdown(contacts);
        if (!string.IsNullOrWhiteSpace(contactMarkdown))
        {
            seeds.Add(new PanelSeed(ProfilePanelType.Contact, contactMarkdown, "markdown", true));
        }

        return seeds;
    }

    private async Task<int> ApplyPanelSeedsAsync(int userId, ProfileTemplate template, IReadOnlyList<PanelSeed> seeds, CancellationToken cancellationToken)
    {
        if (seeds.Count == 0)
        {
            return 0;
        }

        var panels = await _context.ProfilePanels
            .Where(p => p.UserId == userId && p.TemplateType == template)
            .ToListAsync(cancellationToken);

        var nextPosition = panels.Count == 0 ? 1 : panels.Max(p => p.Position) + 1;
        var mutations = 0;

        foreach (var seed in seeds)
        {
            var panel = panels.FirstOrDefault(p => p.PanelType == seed.PanelType);
            if (panel == null)
            {
                panel = new ProfilePanel
                {
                    UserId = userId,
                    TemplateType = template,
                    PanelType = seed.PanelType,
                    Position = nextPosition++,
                    Content = seed.Content,
                    ContentFormat = seed.Format,
                    IsVisible = seed.Visible,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                panels.Add(panel);
                _context.ProfilePanels.Add(panel);
                mutations++;
                continue;
            }

            var mutated = false;
            if (!string.Equals(panel.Content, seed.Content, StringComparison.Ordinal))
            {
                panel.Content = seed.Content;
                mutated = true;
            }

            if (!string.Equals(panel.ContentFormat, seed.Format, StringComparison.OrdinalIgnoreCase))
            {
                panel.ContentFormat = seed.Format;
                mutated = true;
            }

            if (panel.IsVisible != seed.Visible)
            {
                panel.IsVisible = seed.Visible;
                mutated = true;
            }

            if (mutated)
            {
                panel.UpdatedAt = DateTime.UtcNow;
                mutations++;
            }
        }

        return mutations;
    }

    private string RenderAboutHtml(string? aboutMe)
    {
        if (string.IsNullOrWhiteSpace(aboutMe))
        {
            return string.Empty;
        }

        var writer = new StringWriter();
        _bbCode.Format(aboutMe).WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static IDictionary<string, string> BuildThemeOverrides(User user)
    {
        string Normalize(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

        var buttonBorderFallback = Normalize(
            string.IsNullOrWhiteSpace(user.ButtonBorderColor) ? user.ButtonBackgroundColor : user.ButtonBorderColor,
            "#0ea5e9");

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["bg"] = Normalize(user.BackgroundColor, "#0f172a"),
            ["text"] = Normalize(user.FontColor, "#e5e7eb"),
            ["muted"] = Normalize(user.FontColor, "#94a3b8"),
            ["accent"] = Normalize(user.AccentColor, "#3b82f6"),
            ["border"] = Normalize(user.BorderColor, "#334155"),
            ["panel"] = Normalize(user.BackgroundColor, "#0f172a"),
            ["buttonBg"] = Normalize(user.ButtonBackgroundColor, "#0ea5e9"),
            ["buttonText"] = Normalize(user.ButtonTextColor, "#ffffff"),
            ["buttonBorder"] = buttonBorderFallback
        };
    }

    private static string NormalizeMarkdown(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string BuildPaymentMarkdown(IReadOnlyList<PaymentRow> rows)
    {
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Details))
            {
                continue;
            }

            builder.Append("- **")
                .Append(string.IsNullOrWhiteSpace(row.Label) ? "Payment" : row.Label.Trim())
                .Append("**: ")
                .Append(row.Details.Trim())
                .AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static string BuildReferenceMarkdown(IReadOnlyList<ReferenceRow> rows)
    {
        if (rows.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.Label) && string.IsNullOrWhiteSpace(row.Url) && string.IsNullOrWhiteSpace(row.Notes))
            {
                continue;
            }

            builder.Append("- **")
                .Append(string.IsNullOrWhiteSpace(row.Label) ? "Reference" : row.Label.Trim())
                .Append("**");

            if (!string.IsNullOrWhiteSpace(row.Url))
            {
                builder.Append(" — ").Append(row.Url.Trim());
            }

            if (!string.IsNullOrWhiteSpace(row.Notes))
            {
                builder.Append(" — ").Append(row.Notes.Trim());
            }

            builder.AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static string BuildContactMarkdown(IReadOnlyList<UserContact> contacts)
    {
        if (contacts.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        foreach (var contact in contacts)
        {
            if (string.IsNullOrWhiteSpace(contact.AccountId))
            {
                continue;
            }

            var label = string.IsNullOrWhiteSpace(contact.ServiceName) ? "Contact" : contact.ServiceName.Trim();
            builder.Append("- **")
                .Append(label)
                .Append("**: ")
                .Append(contact.AccountId.Trim())
                .AppendLine();
        }

        return builder.ToString().Trim();
    }

    private static bool IsSectionEnabled(IReadOnlyDictionary<string, bool> visibility, string key, bool fallback)
    {
        return visibility.TryGetValue(key, out var enabled) ? enabled : fallback;
    }
}

internal readonly record struct PanelSeed(ProfilePanelType PanelType, string Content, string Format, bool Visible);

internal readonly record struct MigrationOutcome(bool Skipped, string? Reason, bool TemplateChanged, bool ThemeChanged, int PanelUpdates)
{
    public static MigrationOutcome Skipped(string reason) => new(true, reason, false, false, 0);

    public static MigrationOutcome Changed(bool templateChanged, bool themeChanged, int panelUpdates)
        => new(false, null, templateChanged, themeChanged, panelUpdates);

    public bool HasChanges => TemplateChanged || ThemeChanged || PanelUpdates > 0;
}
