using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class InfractionsService
{
    private readonly ApplicationDbContext _context;

    public InfractionsService(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Log an infraction and apply mute if necessary
    /// </summary>
    public async Task<UserInfraction> LogInfractionAsync(
        int userId,
        int? contentId,
        string contentType,
        WordListEntry wordListEntry,
        string? originalContent = null,
        string? sessionFingerprint = null,
        string? torFingerprint = null,
        bool contentModified = false)
    {
        var now = DateTime.UtcNow;
        
        // Determine mute duration with escalation
        DateTime? muteExpiresAt = null;
        bool isEscalation = false;

        if (wordListEntry.ActionType == WordActionType.InfractionAndMute && 
            wordListEntry.MuteDurationHours.HasValue)
        {
            var escalatedDuration = await CalculateEscalatedMuteDurationAsync(
                userId, 
                wordListEntry.MuteDurationHours.Value);
            
            muteExpiresAt = now.AddHours(escalatedDuration.Hours);
            isEscalation = escalatedDuration.IsEscalation;
        }

        var infraction = new UserInfraction
        {
            UserId = userId,
            ContentId = contentId,
            ContentType = contentType,
            MatchedPattern = wordListEntry.WordPattern,
            WordListEntryId = wordListEntry.Id,
            ActionTaken = BuildActionDescription(wordListEntry, muteExpiresAt),
            OccurredAt = now,
            MuteExpiresAt = muteExpiresAt,
            SessionFingerprint = sessionFingerprint,
            TorFingerprint = torFingerprint,
            OriginalContent = originalContent,
            ContentModified = contentModified,
            IsEscalation = isEscalation
        };

        _context.UserInfractions.Add(infraction);
        await _context.SaveChangesAsync();

        // Check if user should be auto-banned (default: 10 infractions in 7 days)
        await CheckAndApplyAutoBanAsync(userId);

        return infraction;
    }

    /// <summary>
    /// Check if user should be automatically banned based on infraction count
    /// </summary>
    private async Task CheckAndApplyAutoBanAsync(int userId, int threshold = 10, int daysWindow = 7)
    {
        var lookback = DateTime.UtcNow.AddDays(-daysWindow);
        var recentInfractionCount = await _context.UserInfractions
            .Where(i => i.UserId == userId && i.OccurredAt >= lookback)
            .CountAsync();

        if (recentInfractionCount >= threshold)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null && user.SuspendedUntil == null)
            {
                // Ban for 30 days
                user.SuspendedUntil = DateTime.UtcNow.AddDays(30);
                await _context.SaveChangesAsync();
            }
        }
    }

    /// <summary>
    /// Calculate escalated mute duration based on recent infractions
    /// </summary>
    private async Task<(int Hours, bool IsEscalation)> CalculateEscalatedMuteDurationAsync(
        int userId, 
        int baseDurationHours)
    {
        var now = DateTime.UtcNow;
        var lookbackWindow = now.AddHours(-168); // Look back 7 days

        // Check for active mutes
        var activeMutes = await _context.UserInfractions
            .Where(i => i.UserId == userId && 
                       i.MuteExpiresAt > now)
            .OrderByDescending(i => i.MuteExpiresAt)
            .ToListAsync();

        if (activeMutes.Any())
        {
            // Extend the longest active mute instead of stacking
            var longestMute = activeMutes.First();
            var currentHoursRemaining = (longestMute.MuteExpiresAt!.Value - now).TotalHours;
            var totalHours = (int)Math.Ceiling(currentHoursRemaining) + baseDurationHours;
            
            return (totalHours, IsEscalation: true);
        }

        // Check recent infractions for escalation pattern
        var recentInfractions = await _context.UserInfractions
            .Where(i => i.UserId == userId && 
                       i.OccurredAt >= lookbackWindow &&
                       i.MuteExpiresAt != null)
            .OrderByDescending(i => i.OccurredAt)
            .Take(5)
            .ToListAsync();

        if (recentInfractions.Count >= 2)
        {
            // Escalate: 12 → 24 → 72 hours
            var escalatedHours = baseDurationHours switch
            {
                12 => 24,
                24 => 72,
                72 => 72, // Max
                _ => baseDurationHours
            };

            if (escalatedHours > baseDurationHours)
            {
                return (escalatedHours, IsEscalation: true);
            }
        }

        return (baseDurationHours, IsEscalation: false);
    }

    /// <summary>
    /// Build human-readable action description
    /// </summary>
    private string BuildActionDescription(WordListEntry entry, DateTime? muteExpiresAt)
    {
        var parts = new List<string>();

        if (entry.ReplacementText != null)
        {
            parts.Add("Content filtered");
        }

        if (entry.ActionType == WordActionType.InfractionAndMute && muteExpiresAt.HasValue)
        {
            var hours = (muteExpiresAt.Value - DateTime.UtcNow).TotalHours;
            parts.Add($"Muted for {Math.Round(hours, 1)} hours");
        }

        return parts.Any() ? string.Join("; ", parts) : "Warning issued";
    }

    /// <summary>
    /// Get infraction statistics for a user
    /// </summary>
    public async Task<InfractionStats> GetUserStatsAsync(int userId, int days = 30)
    {
        var since = DateTime.UtcNow.AddDays(-days);
        
        var infractions = await _context.UserInfractions
            .Where(i => i.UserId == userId && i.OccurredAt >= since)
            .ToListAsync();

        var activeMute = infractions
            .Where(i => i.MuteExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.MuteExpiresAt)
            .FirstOrDefault();

        return new InfractionStats
        {
            TotalInfractions = infractions.Count,
            ContentModifications = infractions.Count(i => i.ContentModified),
            MutesIssued = infractions.Count(i => i.MuteExpiresAt != null),
            EscalatedMutes = infractions.Count(i => i.IsEscalation),
            CurrentlyMuted = activeMute != null,
            MuteExpiresAt = activeMute?.MuteExpiresAt
        };
    }

    /// <summary>
    /// Get recent infractions for admin review
    /// </summary>
    public async Task<List<UserInfraction>> GetRecentInfractionsAsync(
        int limit = 100, 
        int? userId = null,
        bool? onlyActive = null)
    {
        var query = _context.UserInfractions
            .Include(i => i.User)
            .Include(i => i.WordListEntry)
            .AsQueryable();

        if (userId.HasValue)
        {
            query = query.Where(i => i.UserId == userId.Value);
        }

        if (onlyActive == true)
        {
            var now = DateTime.UtcNow;
            query = query.Where(i => i.MuteExpiresAt > now);
        }

        return await query
            .OrderByDescending(i => i.OccurredAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Get infraction metrics for dashboard
    /// </summary>
    public async Task<InfractionMetrics> GetMetricsAsync(TimeSpan? window = null)
    {
        window ??= TimeSpan.FromHours(24);
        var since = DateTime.UtcNow.Subtract(window.Value);

        var infractions = await _context.UserInfractions
            .Where(i => i.OccurredAt >= since)
            .ToListAsync();

        var now = DateTime.UtcNow;
        var hourAgo = now.AddHours(-1);
        var minAgo = now.AddMinutes(-1);

        return new InfractionMetrics
        {
            TotalInWindow = infractions.Count,
            LastHour = infractions.Count(i => i.OccurredAt >= hourAgo),
            LastMinute = infractions.Count(i => i.OccurredAt >= minAgo),
            ActiveMutes = infractions.Count(i => i.MuteExpiresAt > now),
            UniqueUsers = infractions.Select(i => i.UserId).Distinct().Count(),
            PerMinute = infractions.Count / Math.Max(1, window.Value.TotalMinutes),
            PerHour = infractions.Count / Math.Max(1, window.Value.TotalHours)
        };
    }

    /// <summary>
    /// Clear expired mutes (cleanup task)
    /// </summary>
    public async Task<int> ClearExpiredMutesAsync()
    {
        var now = DateTime.UtcNow;
        var expired = await _context.UserInfractions
            .Where(i => i.MuteExpiresAt != null && i.MuteExpiresAt <= now)
            .ToListAsync();

        // Already expired, just for reporting
        return expired.Count;
    }

    /// <summary>
    /// Reset all infractions for a user (admin action)
    /// </summary>
    public async Task<int> ResetUserInfractionsAsync(int userId)
    {
        var infractions = await _context.UserInfractions
            .Where(i => i.UserId == userId)
            .ToListAsync();

        var count = infractions.Count;
        _context.UserInfractions.RemoveRange(infractions);
        await _context.SaveChangesAsync();

        return count;
    }

    /// <summary>
    /// Lift ban/suspension from a user (admin action)
    /// </summary>
    public async Task<bool> LiftBanAsync(int userId)
    {
        var user = await _context.Users.FindAsync(userId);
        if (user != null && user.SuspendedUntil != null)
        {
            user.SuspendedUntil = null;
            await _context.SaveChangesAsync();
            return true;
        }
        return false;
    }
}

public class InfractionStats
{
    public int TotalInfractions { get; set; }
    public int ContentModifications { get; set; }
    public int MutesIssued { get; set; }
    public int EscalatedMutes { get; set; }
    public bool CurrentlyMuted { get; set; }
    public DateTime? MuteExpiresAt { get; set; }
}

public class InfractionMetrics
{
    public int TotalInWindow { get; set; }
    public int LastHour { get; set; }
    public int LastMinute { get; set; }
    public int ActiveMutes { get; set; }
    public int UniqueUsers { get; set; }
    public double PerMinute { get; set; }
    public double PerHour { get; set; }
}
