using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class ContentFilteringService
{
    private readonly ApplicationDbContext _context;
    private List<WordListEntry>? _cachedFilters;
    private DateTime _cacheExpiry = DateTime.MinValue;
    private readonly TimeSpan _cacheLifetime = TimeSpan.FromMinutes(5);
    private readonly object _cacheLock = new object();

    public ContentFilteringService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Filter content and return both sanitized version and detected infractions
    /// </summary>
    public async Task<ContentFilterResult> FilterContentAsync(
        string content,
        ContentScope scope,
        int userId,
        string? sessionFingerprint = null,
        string? torFingerprint = null)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return new ContentFilterResult
            {
                SanitizedContent = content,
                OriginalContent = content,
                WasModified = false,
                TriggeredFilters = new List<WordListEntry>()
            };
        }

        var filters = await GetActiveFiltersAsync();
        var applicableFilters = filters.Where(f => f.AppliesTo.HasFlag(scope)).ToList();

        Console.WriteLine($"[ContentFilter] Scope: {scope} ({(int)scope}), Total filters: {filters.Count}, Applicable: {applicableFilters.Count}");
        foreach (var f in applicableFilters)
        {
            Console.WriteLine($"[ContentFilter] Filter: Pattern='{f.WordPattern}', AppliesTo={f.AppliesTo} ({(int)f.AppliesTo}), MatchType={f.MatchType}, Replace='{f.ReplacementText}'");
        }

        if (!applicableFilters.Any())
        {
            return new ContentFilterResult
            {
                SanitizedContent = content,
                OriginalContent = content,
                WasModified = false,
                TriggeredFilters = new List<WordListEntry>()
            };
        }

        var sanitizedContent = content;
        var triggeredFilters = new List<WordListEntry>();
        var wasModified = false;

        foreach (var filter in applicableFilters)
        {
            var match = CheckMatch(sanitizedContent, filter);
            if (match.IsMatch)
            {
                triggeredFilters.Add(filter);

                // Apply replacement if specified
                if (filter.ReplacementText != null)
                {
                    sanitizedContent = ApplyReplacement(sanitizedContent, filter, match);
                    wasModified = true;
                }
                else if (filter.ActionType == WordActionType.InfractionAndMute)
                {
                    // If no replacement specified for infraction, block the entire content
                    sanitizedContent = "[Content removed by automated filter]";
                    wasModified = true;
                }
            }
        }

        return new ContentFilterResult
        {
            SanitizedContent = sanitizedContent,
            OriginalContent = content,
            WasModified = wasModified,
            TriggeredFilters = triggeredFilters
        };
    }

    /// <summary>
    /// Check if content matches a filter pattern
    /// </summary>
    private MatchResult CheckMatch(string content, WordListEntry filter)
    {
        try
        {
            var regexOptions = RegexOptions.None;
            if (!filter.CaseSensitive)
            {
                regexOptions |= RegexOptions.IgnoreCase;
            }

            string pattern;
            switch (filter.MatchType)
            {
                case WordMatchType.Exact:
                    pattern = Regex.Escape(filter.WordPattern);
                    break;
                case WordMatchType.WordBoundary:
                    pattern = @"\b" + Regex.Escape(filter.WordPattern) + @"\b";
                    break;
                case WordMatchType.Regex:
                    pattern = filter.WordPattern;
                    break;
                default:
                    return new MatchResult { IsMatch = false };
            }

            var regex = new Regex(pattern, regexOptions | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
            var match = regex.Match(content);

            return new MatchResult
            {
                IsMatch = match.Success,
                MatchedText = match.Success ? match.Value : null,
                Regex = regex
            };
        }
        catch (Exception)
        {
            // Invalid regex or timeout
            return new MatchResult { IsMatch = false };
        }
    }

    /// <summary>
    /// Apply replacement to content
    /// </summary>
    private string ApplyReplacement(string content, WordListEntry filter, MatchResult match)
    {
        if (match.Regex == null || filter.ReplacementText == null)
        {
            return content;
        }

        try
        {
            return match.Regex.Replace(content, filter.ReplacementText);
        }
        catch
        {
            return content;
        }
    }

    /// <summary>
    /// Get active filters from cache or database
    /// </summary>
    private async Task<List<WordListEntry>> GetActiveFiltersAsync()
    {
        lock (_cacheLock)
        {
            if (_cachedFilters != null && DateTime.UtcNow < _cacheExpiry)
            {
                return _cachedFilters;
            }
        }

        var filters = await _context.WordListEntries
            .Where(w => w.Enabled)
            .OrderBy(w => w.Id)
            .AsNoTracking()
            .ToListAsync();

        lock (_cacheLock)
        {
            _cachedFilters = filters;
            _cacheExpiry = DateTime.UtcNow.Add(_cacheLifetime);
        }

        return filters;
    }

    /// <summary>
    /// Invalidate the filter cache (call when filters are modified)
    /// </summary>
    public void InvalidateCache()
    {
        lock (_cacheLock)
        {
            _cachedFilters = null;
            _cacheExpiry = DateTime.MinValue;
        }
    }

    /// <summary>
    /// Check if user is currently muted
    /// </summary>
    public async Task<MuteStatus> GetMuteStatusAsync(int userId)
    {
        var now = DateTime.UtcNow;
        var activeMute = await _context.UserInfractions
            .Where(i => i.UserId == userId && i.MuteExpiresAt > now)
            .OrderByDescending(i => i.MuteExpiresAt)
            .FirstOrDefaultAsync();

        if (activeMute != null)
        {
            return new MuteStatus
            {
                IsMuted = true,
                ExpiresAt = activeMute.MuteExpiresAt,
                Reason = $"Automatic mute for: {activeMute.MatchedPattern}"
            };
        }

        return new MuteStatus { IsMuted = false };
    }
}

public class ContentFilterResult
{
    public string SanitizedContent { get; set; } = string.Empty;
    public string OriginalContent { get; set; } = string.Empty;
    public bool WasModified { get; set; }
    public List<WordListEntry> TriggeredFilters { get; set; } = new();
}

public class MatchResult
{
    public bool IsMatch { get; set; }
    public string? MatchedText { get; set; }
    public Regex? Regex { get; set; }
}

public class MuteStatus
{
    public bool IsMuted { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public string? Reason { get; set; }
}
