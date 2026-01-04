using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using MyFace.Data;
using MyFace.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MyFace.Services;

public class VisitTrackingService
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _memoryCache;
    private const string TrafficAnalyticsCacheKeyTemplate = "control-panel:traffic:{0}";
    private static readonly TimeSpan TrafficAnalyticsModeratorTtl = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan TrafficAnalyticsAdminTtl = TimeSpan.FromMinutes(2);

    public VisitTrackingService(ApplicationDbContext context, IMemoryCache memoryCache)
    {
        _context = context;
        _memoryCache = memoryCache;
    }

    public async Task TrackVisitAsync(
        string path,
        string? userAgent = null,
        int? userId = null,
        string? username = null,
        string? sessionId = null,
        string? eventType = null,
        string? referrer = null)
    {
        var visit = new PageVisit
        {
            VisitedAt = DateTime.UtcNow,
            Path = string.IsNullOrWhiteSpace(path) ? "/" : path,
            UserAgent = userAgent,
            UserId = userId,
            UsernameSnapshot = NormalizeUsername(username),
            SessionFingerprint = HashSessionId(sessionId),
            EventType = string.IsNullOrWhiteSpace(eventType) ? ClassifyEvent(path) : eventType.Trim().ToLowerInvariant(),
            Referrer = string.IsNullOrWhiteSpace(referrer) ? null : referrer.Trim()
        };

        _context.PageVisits.Add(visit);
        await _context.SaveChangesAsync();
    }

    public async Task<IReadOnlyList<VisitLogRecord>> GetRecentVisitsAsync(int take = 120, CancellationToken ct = default)
    {
        var size = Math.Clamp(take, 10, 400);
        var items = await _context.PageVisits
            .AsNoTracking()
            .OrderByDescending(v => v.VisitedAt)
            .Take(size)
            .ToListAsync(ct);

        return items
            .Select(v => new VisitLogRecord(
                v.Id,
                v.VisitedAt,
                v.Path,
                v.EventType,
                v.UserId,
                v.UsernameSnapshot,
                v.SessionFingerprint,
                v.UserAgent))
            .ToList();
    }

    public async Task<ControlPanelSnapshot> GetControlPanelSnapshotAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var fifteenMinutesAgo = now.AddMinutes(-15);
        var hourAgo = now.AddHours(-1);
        var fourHoursAgo = now.AddHours(-4);
        var dayAgo = now.AddDays(-1);
        var todayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);

        var liveUsers = await _context.Users.CountAsync(u => u.LastSeenAt >= fifteenMinutesAgo && u.IsActive, ct);
        var usersOnlineFourHours = await _context.Users.CountAsync(u => u.LastSeenAt >= fourHoursAgo && u.IsActive, ct);
        var newRegistrations = await _context.Users.CountAsync(u => u.CreatedAt >= dayAgo, ct);
        var pageViews = await _context.PageVisits.CountAsync(v => v.VisitedAt >= dayAgo, ct);
        var threads = await _context.Threads.CountAsync(t => t.CreatedAt >= dayAgo, ct);
        var posts = await _context.Posts.CountAsync(p => p.CreatedAt >= dayAgo, ct);
        var reportsPending = await _context.Posts.CountAsync(p => p.ReportCount > 0 && !p.IsReportHidden, ct);
        var failedLogins = await _context.LoginAttempts.CountAsync(a => !a.Success && a.AttemptedAt >= hourAgo, ct);

        var sessionSamples = await _context.PageVisits
            .Where(v => v.VisitedAt >= hourAgo)
            .Select(v => new SessionSample(v.SessionFingerprint, v.UserId))
            .ToListAsync(ct);

        var todayVisits = await _context.PageVisits
            .Where(v => v.VisitedAt >= todayStart)
            .Select(v => new SessionVisit(v.VisitedAt, v.SessionFingerprint, v.UserId))
            .ToListAsync(ct);

        var activeSessionsHour = sessionSamples
            .Select(sample => BuildSessionKey(sample.SessionFingerprint, sample.UserId))
            .Where(key => !string.IsNullOrEmpty(key))
            .Distinct(StringComparer.Ordinal)
            .Count();

        var peakToday = CalculatePeakConcurrentUsers(todayVisits);

        var infra = CaptureInfrastructureSnapshot();

        return new ControlPanelSnapshot
        {
            GeneratedAt = now,
            LiveUsers15Minutes = liveUsers,
            ActiveSessionsHour = activeSessionsHour,
            UsersOnlineFourHours = usersOnlineFourHours,
            PeakUsersToday = peakToday,
            PageViews24Hours = pageViews,
            NewRegistrations24Hours = newRegistrations,
            ActiveThreads24Hours = threads,
            Posts24Hours = posts,
            ReportsPending = reportsPending,
            FailedLoginsHour = failedLogins,
            DatabaseSizeBytes = infra.DatabaseSizeBytes,
            DiskFreeBytes = infra.DiskFreeBytes,
            WorkingSetBytes = infra.WorkingSetBytes,
            ManagedMemoryBytes = infra.ManagedMemoryBytes
        };
    }

    public Task<TrafficAnalyticsResult> GetTrafficAnalyticsAsync(bool includeSensitive, CancellationToken ct = default)
    {
        var cacheKey = string.Format(TrafficAnalyticsCacheKeyTemplate, includeSensitive ? "admin" : "moderator");
        var ttl = includeSensitive ? TrafficAnalyticsAdminTtl : TrafficAnalyticsModeratorTtl;

        return _memoryCache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ttl;
            return await BuildTrafficAnalyticsAsync(includeSensitive, ct);
        })!;
    }

    private async Task<TrafficAnalyticsResult> BuildTrafficAnalyticsAsync(bool includeSensitive, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var dayAgo = now.AddDays(-1);
        var weekAgo = now.AddDays(-7);

        var visits24 = await _context.PageVisits
            .AsNoTracking()
            .Where(v => v.VisitedAt >= dayAgo)
            .OrderBy(v => v.VisitedAt)
            .Take(20000)
            .ToListAsync(ct);

        var sessionGroups = visits24
            .GroupBy(v => BuildSessionKey(v.SessionFingerprint, v.UserId) ?? $"anon:{v.Id}")
            .Select(g => g.OrderBy(v => v.VisitedAt).ToList())
            .ToList();

        var entryCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var exitCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var totalDuration = TimeSpan.Zero;
        var sessionCount = 0;
        var bounceCount = 0;

        foreach (var session in sessionGroups)
        {
            if (session.Count == 0)
            {
                continue;
            }

            sessionCount++;
            var first = session[0];
            var last = session[^1];
            IncreaseCount(entryCounts, first.Path);
            IncreaseCount(exitCounts, last.Path);
            if (session.Count == 1)
            {
                bounceCount++;
            }
            totalDuration += (last.VisitedAt - first.VisitedAt);
        }

        var topPages = visits24
            .GroupBy(v => v.Path)
            .Select(g => new MetricCount(g.Key, g.Count()))
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToList();

        var anonVisits = visits24.Count(v => !v.UserId.HasValue);
        var authVisits = visits24.Count - anonVisits;

        var hourlyRaw = await _context.PageVisits
            .AsNoTracking()
            .Where(v => v.VisitedAt >= weekAgo)
            .GroupBy(v => new { v.VisitedAt.Year, v.VisitedAt.Month, v.VisitedAt.Day, v.VisitedAt.Hour })
            .Select(g => new
            {
                g.Key.Year,
                g.Key.Month,
                g.Key.Day,
                g.Key.Hour,
                Count = g.Count()
            })
            .OrderBy(item => item.Year)
            .ThenBy(item => item.Month)
            .ThenBy(item => item.Day)
            .ThenBy(item => item.Hour)
            .ToListAsync(ct);

        var hourly = hourlyRaw
            .Select(item =>
            {
                var hour = DateTime.SpecifyKind(new DateTime(item.Year, item.Month, item.Day, item.Hour, 0, 0), DateTimeKind.Utc);
                return new TrafficHourBucket(hour, item.Count);
            })
            .ToList();

        var averageDurationSeconds = sessionCount == 0
            ? 0
            : totalDuration.TotalSeconds / sessionCount;

        double bounceRate = sessionCount == 0 ? 0 : (double)bounceCount / sessionCount;

        var referrerList = includeSensitive
            ? visits24
                .Where(v => !string.IsNullOrWhiteSpace(v.Referrer))
                .GroupBy(v => v.Referrer!.Trim())
                .Select(g => new MetricCount(g.Key, g.Count()))
                .OrderByDescending(x => x.Count)
                .Take(10)
                .ToList()
            : new List<MetricCount>();

        var newUserVisits = 0;
        var returningUserVisits = 0;
        if (includeSensitive)
        {
            var userIds = visits24
                .Where(v => v.UserId.HasValue)
                .Select(v => v.UserId!.Value)
                .Distinct()
                .ToList();

            if (userIds.Count > 0)
            {
                var createdLookup = await _context.Users
                    .Where(u => userIds.Contains(u.Id))
                    .Select(u => new { u.Id, u.CreatedAt })
                    .ToDictionaryAsync(x => x.Id, x => x.CreatedAt, ct);

                var returningThreshold = now.AddDays(-30);
                foreach (var visit in visits24)
                {
                    if (!visit.UserId.HasValue)
                    {
                        continue;
                    }

                    if (!createdLookup.TryGetValue(visit.UserId.Value, out var createdAt))
                    {
                        continue;
                    }

                    if (createdAt >= returningThreshold)
                    {
                        newUserVisits++;
                    }
                    else
                    {
                        returningUserVisits++;
                    }
                }
            }
        }

        return new TrafficAnalyticsResult
        {
            GeneratedAtUtc = now,
            TopPages = topPages,
            EntryPages = entryCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .Select(kvp => new MetricCount(kvp.Key, kvp.Value))
                .ToList(),
            ExitPages = exitCounts
                .OrderByDescending(kvp => kvp.Value)
                .Take(10)
                .Select(kvp => new MetricCount(kvp.Key, kvp.Value))
                .ToList(),
            AverageSessionSeconds = averageDurationSeconds,
            BounceRate = includeSensitive ? bounceRate : 0,
            HourlyCounts = hourly,
            AnonymousVisits = anonVisits,
            AuthenticatedVisits = authVisits,
            Referrers = referrerList,
            NewUserVisits = newUserVisits,
            ReturningUserVisits = returningUserVisits
        };
    }

    public async Task<SiteStatistics> GetStatisticsAsync()
    {
        var now = DateTime.UtcNow;
        var todayStart = DateTime.SpecifyKind(now.Date, DateTimeKind.Utc);
        var weekStart = DateTime.SpecifyKind(now.Date.AddDays(-(int)now.DayOfWeek), DateTimeKind.Utc);
        var monthStart = DateTime.SpecifyKind(new DateTime(now.Year, now.Month, 1), DateTimeKind.Utc);
        var quarterStart = DateTime.SpecifyKind(new DateTime(now.Year, ((now.Month - 1) / 3) * 3 + 1, 1), DateTimeKind.Utc);
        var yearStart = DateTime.SpecifyKind(new DateTime(now.Year, 1, 1), DateTimeKind.Utc);
        var fourHoursAgo = now.AddHours(-4);
        var fifteenMinutesAgo = now.AddMinutes(-15); // Active users in last 15 minutes

        var stats = new SiteStatistics
        {
            // Current period counts
            TodayCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= todayStart),
            WeekCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= weekStart),
            MonthCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= monthStart),
            QuarterCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= quarterStart),
            YearCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= yearStart),
            FourHourCount = await _context.PageVisits.CountAsync(v => v.VisitedAt >= fourHoursAgo),
            
            // Live user count (active in last 15 minutes)
            LiveUserCount = await _context.Users.CountAsync(u => u.LastSeenAt >= fifteenMinutesAgo),
            
            // Total all-time
            TotalCount = await _context.PageVisits.CountAsync(),
            
            // Total users
            TotalUsers = await _context.Users.CountAsync()
        };

        // Calculate activity metrics
        await CalculateActivityMetricsAsync(stats, now);

        // Calculate peaks
        stats.PeakDay = await GetPeakDayCountAsync();
        stats.PeakWeek = await GetPeakWeekCountAsync();
        stats.PeakMonth = await GetPeakMonthCountAsync();
        stats.PeakQuarter = await GetPeakQuarterCountAsync();
        stats.PeakYear = await GetPeakYearCountAsync();
        stats.PeakFourHours = await GetPeakFourHourCountAsync();

        return stats;
    }

    private async Task CalculateActivityMetricsAsync(SiteStatistics stats, DateTime now)
    {
        try
        {
            var minuteAgo = now.AddMinutes(-1);
            var hourAgo = now.AddHours(-1);
            var dayAgo = now.AddDays(-1);
            var weekAgo = now.AddDays(-7);
            var monthAgo = now.AddMonths(-1);
            var yearAgo = now.AddYears(-1);

            // Count activities in each time period
            var lastMinute = await _context.Activities.CountAsync(a => a.CreatedAt >= minuteAgo);
            var lastHour = await _context.Activities.CountAsync(a => a.CreatedAt >= hourAgo);
            var lastDay = await _context.Activities.CountAsync(a => a.CreatedAt >= dayAgo);
            var lastWeek = await _context.Activities.CountAsync(a => a.CreatedAt >= weekAgo);
            var lastMonth = await _context.Activities.CountAsync(a => a.CreatedAt >= monthAgo);
            var lastYear = await _context.Activities.CountAsync(a => a.CreatedAt >= yearAgo);

            // Calculate rates
            stats.ActionsPerMinute = lastMinute;
            stats.ActionsPerHour = lastHour;
            stats.ActionsPerDay = lastDay;
            stats.ActionsPerWeek = lastWeek;
            stats.ActionsPerMonth = lastMonth;
            stats.ActionsPerYear = lastYear;
        }
        catch
        {
            // Silently fail if Activities table doesn't exist yet or has issues
            stats.ActionsPerMinute = 0;
            stats.ActionsPerHour = 0;
            stats.ActionsPerDay = 0;
            stats.ActionsPerWeek = 0;
            stats.ActionsPerMonth = 0;
            stats.ActionsPerYear = 0;
        }
    }

    private async Task<int> GetPeakDayCountAsync()
    {
        var result = await _context.PageVisits
            .GroupBy(v => v.VisitedAt.Date)
            .Select(g => g.Count())
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();
        return result;
    }

    private async Task<int> GetPeakWeekCountAsync()
    {
        var visits = await _context.PageVisits.ToListAsync();
        if (!visits.Any()) return 0;
        
        var weekGroups = visits
            .GroupBy(v => GetWeekStart(v.VisitedAt))
            .Select(g => g.Count())
            .OrderByDescending(c => c);
        
        return weekGroups.FirstOrDefault();
    }

    private async Task<int> GetPeakMonthCountAsync()
    {
        var result = await _context.PageVisits
            .GroupBy(v => new { v.VisitedAt.Year, v.VisitedAt.Month })
            .Select(g => g.Count())
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();
        return result;
    }

    private async Task<int> GetPeakQuarterCountAsync()
    {
        var visits = await _context.PageVisits.ToListAsync();
        if (!visits.Any()) return 0;
        
        var quarterGroups = visits
            .GroupBy(v => new { v.VisitedAt.Year, Quarter = ((v.VisitedAt.Month - 1) / 3) + 1 })
            .Select(g => g.Count())
            .OrderByDescending(c => c);
        
        return quarterGroups.FirstOrDefault();
    }

    private async Task<int> GetPeakYearCountAsync()
    {
        var result = await _context.PageVisits
            .GroupBy(v => v.VisitedAt.Year)
            .Select(g => g.Count())
            .OrderByDescending(c => c)
            .FirstOrDefaultAsync();
        return result;
    }

    private async Task<int> GetPeakFourHourCountAsync()
    {
        var visits = await _context.PageVisits
            .Where(v => v.VisitedAt >= DateTime.UtcNow.AddDays(-7)) // Last week for performance
            .ToListAsync();
        
        if (!visits.Any()) return 0;

        var maxCount = 0;
        foreach (var visit in visits)
        {
            var fourHoursAfter = visit.VisitedAt.AddHours(4);
            var count = visits.Count(v => v.VisitedAt >= visit.VisitedAt && v.VisitedAt < fourHoursAfter);
            if (count > maxCount) maxCount = count;
        }
        
        return maxCount;
    }

    private DateTime GetWeekStart(DateTime date)
    {
        return DateTime.SpecifyKind(date.Date.AddDays(-(int)date.DayOfWeek), DateTimeKind.Utc);
    }

    private static int CalculatePeakConcurrentUsers(IReadOnlyCollection<SessionVisit> visits)
    {
        if (visits.Count == 0)
        {
            return 0;
        }

        var bucketTicks = TimeSpan.FromMinutes(5).Ticks;

        return visits
            .GroupBy(v => new DateTime((v.VisitedAt.Ticks / bucketTicks) * bucketTicks, DateTimeKind.Utc))
            .Select(group => group
                .Select(item => BuildSessionKey(item.SessionFingerprint, item.UserId))
                .Where(key => !string.IsNullOrEmpty(key))
                .Distinct(StringComparer.Ordinal)
                .Count())
            .DefaultIfEmpty(0)
            .Max();
    }

    private static string? BuildSessionKey(string? sessionFingerprint, int? userId)
    {
        if (!string.IsNullOrWhiteSpace(sessionFingerprint))
        {
            return sessionFingerprint.Trim();
        }

        if (userId.HasValue)
        {
            return $"user:{userId.Value}";
        }

        return null;
    }

    private static void IncreaseCount(IDictionary<string, int> map, string key)
    {
        if (map.TryGetValue(key, out var count))
        {
            map[key] = count + 1;
        }
        else
        {
            map[key] = 1;
        }
    }

    private InfrastructureSnapshot CaptureInfrastructureSnapshot()
    {
        var dbSize = GetDatabaseSizeBytes();
        var diskFree = GetDiskFreeBytes();
        var workingSet = Process.GetCurrentProcess().WorkingSet64;
        var managedMemory = GC.GetTotalMemory(false);

        return new InfrastructureSnapshot(dbSize, diskFree, workingSet, managedMemory);
    }

    private long GetDatabaseSizeBytes()
    {
        try
        {
            var provider = _context.Database.ProviderName ?? string.Empty;
            if (!provider.Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
            {
                return 0;
            }

            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                connection.Open();
            }

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT pg_database_size(current_database())";
            var result = command.ExecuteScalar();
            if (result == null || result == DBNull.Value)
            {
                return 0;
            }

            return Convert.ToInt64(result);
        }
        catch
        {
            return 0;
        }
    }

    private static long GetDiskFreeBytes()
    {
        try
        {
            var basePath = AppContext.BaseDirectory;
            var root = Path.GetPathRoot(basePath);
            if (string.IsNullOrWhiteSpace(root))
            {
                return 0;
            }

            var drive = new DriveInfo(root);
            return drive.AvailableFreeSpace;
        }
        catch
        {
            return 0;
        }
    }

    private readonly record struct SessionSample(string? SessionFingerprint, int? UserId);
    private readonly record struct SessionVisit(DateTime VisitedAt, string? SessionFingerprint, int? UserId);
    private readonly record struct InfrastructureSnapshot(long DatabaseSizeBytes, long DiskFreeBytes, long WorkingSetBytes, long ManagedMemoryBytes);

    private static string? NormalizeUsername(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? HashSessionId(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return null;
        }

        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sessionId));
        var hex = Convert.ToHexString(bytes).ToLowerInvariant();
        return hex[..Math.Min(16, hex.Length)];
    }

    private static string ClassifyEvent(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "page-load";
        }

        var normalized = path.Trim().ToLowerInvariant();
        if (normalized.StartsWith("/monitor/go"))
        {
            return "link-click";
        }

        if (normalized.Contains("/go/") || normalized.Contains("redirect"))
        {
            return "link-click";
        }

        return "page-load";
    }
}

public record VisitLogRecord(
    int Id,
    DateTime VisitedAt,
    string Path,
    string EventType,
    int? UserId,
    string? UsernameSnapshot,
    string? SessionFingerprint,
    string? UserAgent);

public class SiteStatistics
{
    public int TodayCount { get; set; }
    public int WeekCount { get; set; }
    public int MonthCount { get; set; }
    public int QuarterCount { get; set; }
    public int YearCount { get; set; }
    public int FourHourCount { get; set; }
    public int LiveUserCount { get; set; }
    public int TotalCount { get; set; }
    
    public int PeakDay { get; set; }
    public int PeakWeek { get; set; }
    public int PeakMonth { get; set; }
    public int PeakQuarter { get; set; }
    public int PeakYear { get; set; }
    public int PeakFourHours { get; set; }
    
    // User and activity metrics
    public int TotalUsers { get; set; }
    public double ActionsPerMinute { get; set; }
    public double ActionsPerHour { get; set; }
    public double ActionsPerDay { get; set; }
    public double ActionsPerWeek { get; set; }
    public double ActionsPerMonth { get; set; }
    public double ActionsPerYear { get; set; }
}

public class ControlPanelSnapshot
{
    public DateTime GeneratedAt { get; set; }
    public int LiveUsers15Minutes { get; set; }
    public int ActiveSessionsHour { get; set; }
    public int UsersOnlineFourHours { get; set; }
    public int PeakUsersToday { get; set; }
    public int PageViews24Hours { get; set; }
    public int NewRegistrations24Hours { get; set; }
    public int ActiveThreads24Hours { get; set; }
    public int Posts24Hours { get; set; }
    public int ReportsPending { get; set; }
    public int FailedLoginsHour { get; set; }
    public long DatabaseSizeBytes { get; set; }
    public long DiskFreeBytes { get; set; }
    public long WorkingSetBytes { get; set; }
    public long ManagedMemoryBytes { get; set; }
}

public class TrafficAnalyticsResult
{
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<MetricCount> TopPages { get; init; } = Array.Empty<MetricCount>();
    public IReadOnlyList<MetricCount> EntryPages { get; init; } = Array.Empty<MetricCount>();
    public IReadOnlyList<MetricCount> ExitPages { get; init; } = Array.Empty<MetricCount>();
    public double AverageSessionSeconds { get; init; }
    public double BounceRate { get; init; }
    public IReadOnlyList<TrafficHourBucket> HourlyCounts { get; init; } = Array.Empty<TrafficHourBucket>();
    public int AnonymousVisits { get; init; }
    public int AuthenticatedVisits { get; init; }
    public IReadOnlyList<MetricCount> Referrers { get; init; } = Array.Empty<MetricCount>();
    public int NewUserVisits { get; init; }
    public int ReturningUserVisits { get; init; }
}

public record MetricCount(string Label, int Count);
public record TrafficHourBucket(DateTime HourUtc, int Count);
