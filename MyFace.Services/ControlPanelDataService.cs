using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class ControlPanelDataService
{
    private readonly ApplicationDbContext _context;
    private readonly ChatService _chatService;

    public ControlPanelDataService(ApplicationDbContext context, ChatService chatService)
    {
        _context = context;
        _chatService = chatService;
    }

    public async Task<ContentMetricsResult> GetContentMetricsAsync(bool includeAdmin, ContentMetricsRange range = ContentMetricsRange.Day, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var dayAgo = now.AddDays(-1);
        var weekAgo = now.AddDays(-7);
        var monthAgo = now.AddMonths(-1);
        var rangeStart = range.GetWindowStartUtc(now);

        var threadsDay = await _context.Threads.CountAsync(t => t.CreatedAt >= dayAgo, ct);
        var threadsWeek = await _context.Threads.CountAsync(t => t.CreatedAt >= weekAgo, ct);
        var threadsMonth = await _context.Threads.CountAsync(t => t.CreatedAt >= monthAgo, ct);
        var postsDay = await _context.Posts.CountAsync(p => p.CreatedAt >= dayAgo, ct);
        var postsWeek = await _context.Posts.CountAsync(p => p.CreatedAt >= weekAgo, ct);
        var postsMonth = await _context.Posts.CountAsync(p => p.CreatedAt >= monthAgo, ct);
        var pinnedThreads = await _context.Threads.CountAsync(t => t.IsPinned, ct);
        var stickyPosts = await _context.Posts.CountAsync(p => p.IsSticky, ct);
        var reportsPending = await _context.Posts.CountAsync(p => p.ReportCount > 0 && !p.IsReportHidden, ct);

        var topCategoryRows = await _context.Threads
            .Where(t => t.CreatedAt >= weekAgo)
            .GroupBy(t => t.Category)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync(ct);

        var topCategories = topCategoryRows
            .Select(row => new NamedMetric(row.Label ?? "Uncategorized", row.Count))
            .ToList();

        var anonStats = await _context.Posts
            .Where(p => p.CreatedAt >= weekAgo)
            .GroupBy(p => p.IsAnonymous)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var moderationActions = await _context.Posts
            .Where(p => p.WasModerated)
            .CountAsync(ct);

        var editedPosts = await _context.Posts.CountAsync(p => p.EditCount > 0, ct);
        var deletedPosts = await _context.Posts.CountAsync(p => p.IsDeleted, ct);

        var imagesUploadedDay = await _context.PostImages.CountAsync(img => img.CreatedAt >= dayAgo, ct);
        var uploadFailuresDay = await _context.UploadScanLogs.CountAsync(log => log.Blocked && log.CreatedAt >= dayAgo, ct);
        var malwareDetectionsDay = await _context.UploadScanLogs.CountAsync(log => log.ScanStatus == "Malicious" && log.CreatedAt >= dayAgo, ct);

        var threadsRange = await _context.Threads.CountAsync(t => t.CreatedAt >= rangeStart, ct);
        var postsRange = await _context.Posts.CountAsync(p => p.CreatedAt >= rangeStart, ct);
        var votesRange = await _context.Votes.CountAsync(v => v.CreatedAt >= rangeStart, ct);
        var activitiesRange = await _context.Activities.CountAsync(a => a.CreatedAt >= rangeStart, ct);
        var moderationRange = await _context.Posts.CountAsync(p => p.WasModerated && ((p.EditedAt ?? p.CreatedAt) >= rangeStart), ct);
        var visibleReportsRange = await _context.Posts.CountAsync(p => p.ReportCount > 0 && !p.IsReportHidden && p.CreatedAt >= rangeStart, ct);
        var hiddenReportsRange = await _context.Posts.CountAsync(p => p.ReportCount > 0 && p.IsReportHidden && ((p.EditedAt ?? p.CreatedAt) >= rangeStart), ct);
        var averagePostsPerThread = threadsRange == 0 ? 0 : (double)postsRange / threadsRange;

        var anonPosts = anonStats.FirstOrDefault(x => x.Key)?.Count ?? 0;
        var totalAnonWindow = anonStats.Sum(x => x.Count);
        var anonRatio = totalAnonWindow == 0 ? 0 : (double)anonPosts / totalAnonWindow;

        return new ContentMetricsResult
        {
            ThreadsDay = threadsDay,
            ThreadsWeek = threadsWeek,
            ThreadsMonth = threadsMonth,
            PostsDay = postsDay,
            PostsWeek = postsWeek,
            PostsMonth = postsMonth,
            StickyOrPinned = pinnedThreads + stickyPosts,
            ReportsPending = reportsPending,
            TopCategories = topCategories,
            AnonymousPostRatio = anonRatio,
            ModeratedPosts = moderationActions,
            EditedPosts = editedPosts,
            DeletedPosts = deletedPosts,
            ImagesUploadedDay = imagesUploadedDay,
            UploadFailuresDay = uploadFailuresDay,
            MalwareDetectionsDay = malwareDetectionsDay,
            IncludeAdmin = includeAdmin,
            SelectedRange = range,
            RangeStartUtc = rangeStart,
            ThreadsInRange = threadsRange,
            PostsInRange = postsRange,
            AveragePostsPerThread = averagePostsPerThread,
            VotesInRange = votesRange,
            ActivitiesInRange = activitiesRange,
            ModerationActionsInRange = moderationRange,
            VisibleReportsInRange = visibleReportsRange,
            HiddenReportsInRange = hiddenReportsRange
        };
    }

    public async Task<UserEngagementResult> GetUserEngagementAsync(bool includeAdmin, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-30);

        var totalUsers = await _context.Users.CountAsync(ct);
        var activeUsers = await _context.Users.CountAsync(u => u.LastSeenAt >= sevenDaysAgo && u.IsActive, ct);
        var newUsers = await _context.Users.CountAsync(u => u.CreatedAt >= thirtyDaysAgo, ct);
        var bannedUsers = await _context.Users.CountAsync(u => !u.IsActive, ct);
        var pgpVerified = await _context.Users.CountAsync(u => !string.IsNullOrWhiteSpace(u.PgpPublicKey), ct);

        var topContributors = await _context.Posts
            .AsNoTracking()
            .Where(p => p.UserId.HasValue && p.CreatedAt >= thirtyDaysAgo)
            .GroupBy(p => new
            {
                p.UserId,
                Username = p.User!.Username,
                p.User.Role,
                p.User.LastSeenAt
            })
            .Select(g => new UserContributionSummary(
                g.Key.UserId!.Value,
                g.Key.Username,
                g.Key.Role,
                g.Count(),
                g.Key.LastSeenAt))
            .OrderByDescending(x => x.PostsLast30Days)
            .ThenBy(x => x.Username)
            .Take(5)
            .ToListAsync(ct);

        var suspendedUsers = await _context.Users
            .AsNoTracking()
            .Where(u => u.SuspendedUntil != null && u.SuspendedUntil >= now)
            .OrderBy(u => u.SuspendedUntil)
            .Select(u => new SuspendedUserSummary(u.Id, u.Username, u.Role, u.SuspendedUntil, u.LastSeenAt))
            .Take(5)
            .ToListAsync(ct);

        var voteLeaders = await _context.Votes
            .AsNoTracking()
            .Where(v => v.PostId.HasValue)
            .GroupBy(v => v.PostId!.Value)
            .Select(g => new { PostId = g.Key, Score = g.Sum(v => v.Value) })
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.PostId)
            .Take(5)
            .ToListAsync(ct);

        var leaderIds = voteLeaders.Select(v => v.PostId).ToList();
        var postLookup = leaderIds.Count == 0
            ? new Dictionary<int, PostProjection>()
            : await _context.Posts
                .AsNoTracking()
                .Where(p => leaderIds.Contains(p.Id))
                .Select(p => new PostProjection(
                    p.Id,
                    p.Thread.Title,
                    p.Content,
                    p.User != null ? p.User.Username : "Anonymous"))
                .ToDictionaryAsync(p => p.Id, p => p, ct);

        var topPosts = voteLeaders
            .Select(v =>
            {
                if (postLookup.TryGetValue(v.PostId, out var post))
                {
                    return new PostHighlight(post.Id, post.ThreadTitle, TrimContent(post.Content), post.Author, v.Score);
                }

                return new PostHighlight(v.PostId, $"Post #{v.PostId}", "Unavailable", "Unknown", v.Score);
            })
            .ToList();

        var growthSeries = new List<TimeSeriesPoint>();
        var pmStats = new PrivateMessageStats(0, 0, 0);

        if (includeAdmin)
        {
            growthSeries = await _context.Users
                .AsNoTracking()
                .Where(u => u.CreatedAt >= thirtyDaysAgo)
                .GroupBy(u => u.CreatedAt.Date)
                .Select(g => new TimeSeriesPoint(g.Key, g.Count()))
                .OrderBy(p => p.Day)
                .ToListAsync(ct);

            var sentLastWeek = await _context.PrivateMessages
                .AsNoTracking()
                .CountAsync(m => !m.IsDraft && m.SentAt != null && m.SentAt >= sevenDaysAgo, ct);

            var draftsLastWeek = await _context.PrivateMessages
                .AsNoTracking()
                .CountAsync(m => m.IsDraft && m.CreatedAt >= sevenDaysAgo, ct);

            var totalDrafts = await _context.PrivateMessages
                .AsNoTracking()
                .CountAsync(m => m.IsDraft, ct);

            pmStats = new PrivateMessageStats(sentLastWeek, draftsLastWeek, totalDrafts);
        }

        return new UserEngagementResult
        {
            IncludeAdmin = includeAdmin,
            TotalUsers = totalUsers,
            ActiveUsers7Days = activeUsers,
            NewUsers30Days = newUsers,
            BannedUsers = bannedUsers,
            PgpVerifiedUsers = pgpVerified,
            TopContributors = topContributors,
            SuspendedUsers = suspendedUsers,
            TopPosts = topPosts,
            GrowthSeries = growthSeries,
            PrivateMessages = pmStats
        };
    }

    private static string TrimContent(string value, int maxLength = 140)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return trimmed[..maxLength] + "…";
    }

    private readonly record struct PostProjection(int Id, string ThreadTitle, string Content, string Author);
    private readonly record struct UserLite(int UserId, string Username, string Role);

    public async Task<ChatOversightResult> GetChatOversightAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var dayAgo = now.AddDays(-1);
        var hourAgo = now.AddHours(-1);
        var twelveHoursAgo = now.AddHours(-12);

        var messagesByRoomRows = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.CreatedAt >= dayAgo)
            .GroupBy(m => m.Room)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToListAsync(ct);

        var messagesByRoom = messagesByRoomRows
            .Select(row => new NamedMetric(row.Label ?? "(unknown)", row.Count))
            .ToList();

        var totalMessages = messagesByRoom.Sum(m => m.Count);
        var verifiedMessages = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.CreatedAt >= dayAgo && m.IsVerifiedSnapshot)
            .CountAsync(ct);

        var moderatorMessages = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.CreatedAt >= dayAgo && (m.RoleSnapshot == "Admin" || m.RoleSnapshot == "Moderator"))
            .CountAsync(ct);

        var messagesLastHour = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.CreatedAt >= hourAgo)
            .CountAsync(ct);

        var hourByRoom = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.CreatedAt >= hourAgo)
            .GroupBy(m => m.Room)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);

        var uniqueChatters = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.CreatedAt >= dayAgo && m.UserId != null)
            .Select(m => m.UserId!.Value)
            .Distinct()
            .CountAsync(ct);

        // Materialize the recent chat messages first so we can perform grouping in-memory; EF cannot translate the DateTime
        // projection with distinct user counting per hour across providers.
        var recentMessages = await _context.ChatMessages
            .AsNoTracking()
            .Where(m => m.CreatedAt >= twelveHoursAgo)
            .Select(m => new { m.CreatedAt, m.UserId, m.Id })
            .ToListAsync(ct);

        var hourlyActive = recentMessages
            .GroupBy(m => DateTime.SpecifyKind(
                new DateTime(m.CreatedAt.Year, m.CreatedAt.Month, m.CreatedAt.Day, m.CreatedAt.Hour, 0, 0),
                DateTimeKind.Utc))
            .Select(g => new ChatHourlyActive(g.Key, g.Select(msg => msg.UserId ?? -msg.Id).Distinct().Count()))
            .OrderBy(x => x.HourUtc)
            .ToList();

        var muteStates = _chatService.GetActiveMutes();
        var muteUserIds = muteStates.Select(m => m.UserId).Distinct().ToList();
        var muteUsers = muteUserIds.Count == 0
            ? new Dictionary<int, UserLite>()
            : await _context.Users
                .AsNoTracking()
                .Where(u => muteUserIds.Contains(u.Id))
                .Select(u => new UserLite(u.Id, u.Username, u.Role))
                .ToDictionaryAsync(u => u.UserId, u => u, ct);

        var muteSummaries = muteStates
            .Select(state =>
            {
                if (muteUsers.TryGetValue(state.UserId, out var user))
                {
                    return new ChatMuteSummary(user.UserId, user.Username, user.Role, state.ExpiresAt);
                }

                return new ChatMuteSummary(state.UserId, $"User #{state.UserId}", "User", state.ExpiresAt);
            })
            .OrderByDescending(m => m.ExpiresAt)
            .ToList();

        var roomStatuses = _chatService.GetRoomStatuses();
        var statusSummaries = roomStatuses
            .Select(status =>
            {
                hourByRoom.TryGetValue(status.Room, out var hourlyCount);
                return new ChatRoomStatus(status.Room, status.IsPaused, status.ExpiresAt, hourlyCount);
            })
            .ToList();

        var verifiedShare = totalMessages == 0 ? 0 : (double)verifiedMessages / totalMessages;
        var moderatorShare = totalMessages == 0 ? 0 : (double)moderatorMessages / totalMessages;

        return new ChatOversightResult
        {
            MessagesByRoom = messagesByRoom,
            HourlyActive = hourlyActive,
            VerifiedShare = verifiedShare,
            ModeratorShare = moderatorShare,
            MessagesLastHour = messagesLastHour,
            UniqueChattersLastDay = uniqueChatters,
            ActiveMutes = muteSummaries,
            RoomStatuses = statusSummaries
        };
    }

    public async Task<SecurityOverviewResult> GetSecurityOverviewAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var hourAgo = now.AddHours(-1);
        var dayAgo = now.AddDays(-1);

        var failedHour = await _context.LoginAttempts
            .AsNoTracking()
            .CountAsync(a => !a.Success && a.AttemptedAt >= hourAgo, ct);

        var failedDay = await _context.LoginAttempts
            .AsNoTracking()
            .CountAsync(a => !a.Success && a.AttemptedAt >= dayAgo, ct);

        var successDay = await _context.LoginAttempts
            .AsNoTracking()
            .CountAsync(a => a.Success && a.AttemptedAt >= dayAgo, ct);

        var topLoginTargetRows = await _context.LoginAttempts
            .AsNoTracking()
            .Where(a => a.AttemptedAt >= dayAgo)
            .GroupBy(a => a.LoginNameHash)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync(ct);

        var topTargets = topLoginTargetRows
            .Select(row => new NamedMetric(row.Label ?? "(anonymous)", row.Count))
            .ToList();

        var topIpHashRows = await _context.LoginAttempts
            .AsNoTracking()
            .Where(a => a.AttemptedAt >= dayAgo && !string.IsNullOrEmpty(a.IpAddressHash))
            .GroupBy(a => a.IpAddressHash)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(5)
            .ToListAsync(ct);

        var topIpHashes = topIpHashRows
            .Select(row => new NamedMetric(row.Label ?? "(unknown ip)", row.Count))
            .ToList();

        var captchaPressure = await _context.Activities
            .AsNoTracking()
            .Where(a => a.CreatedAt >= hourAgo && a.UserId != null)
            .GroupBy(a => a.UserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .Where(x => x.Count >= 10)
            .OrderByDescending(x => x.Count)
            .Take(10)
            .ToListAsync(ct);

        var captchaIds = captchaPressure.Select(x => x.UserId).ToList();
        var captchaUsers = captchaIds.Count == 0
            ? new Dictionary<int, UserLite>()
            : await _context.Users
                .AsNoTracking()
                .Where(u => captchaIds.Contains(u.Id))
                .Select(u => new UserLite(u.Id, u.Username, u.Role))
                .ToDictionaryAsync(u => u.UserId, u => u, ct);

        var captchaSummaries = captchaPressure
            .Select(item =>
            {
                if (captchaUsers.TryGetValue(item.UserId, out var user))
                {
                    return new CaptchaPressureUser(user.UserId, user.Username, item.Count);
                }

                return new CaptchaPressureUser(item.UserId, $"User #{item.UserId}", item.Count);
            })
            .ToList();

        var sessionAlertsRaw = await _context.PageVisits
            .AsNoTracking()
            .Where(v => v.UserId != null && v.VisitedAt >= hourAgo)
            .GroupBy(v => v.UserId!.Value)
            .Select(g => new { UserId = g.Key, Fingerprints = g.Select(v => v.SessionFingerprint ?? string.Empty).Distinct().Count() })
            .Where(x => x.Fingerprints >= 3)
            .ToListAsync(ct);

        var alertIds = sessionAlertsRaw.Select(x => x.UserId).Distinct().ToList();
        var alertUsers = alertIds.Count == 0
            ? new Dictionary<int, UserLite>()
            : await _context.Users
                .AsNoTracking()
                .Where(u => alertIds.Contains(u.Id))
                .Select(u => new UserLite(u.Id, u.Username, u.Role))
                .ToDictionaryAsync(u => u.UserId, u => u, ct);

        var alerts = new List<SecurityAlert>();
        if (failedHour > 250)
        {
            alerts.Add(new SecurityAlert("Failed login spike", $"{failedHour} failures detected in the last hour."));
        }

        foreach (var anomaly in sessionAlertsRaw)
        {
            var label = alertUsers.TryGetValue(anomaly.UserId, out var user)
                ? user.Username
                : $"User #{anomaly.UserId}";
            alerts.Add(new SecurityAlert("Session fingerprint churn", $"{label} used {anomaly.Fingerprints} fingerprints in the last hour."));
        }

        var passwordResets = await _context.ControlPanelAuditEntries
            .AsNoTracking()
            .CountAsync(e => e.Action == "password.reset" && e.CreatedAt >= dayAgo, ct);

        return new SecurityOverviewResult
        {
            FailedLoginsLastHour = failedHour,
            FailedLoginsLastDay = failedDay,
            SuccessfulLoginsLastDay = successDay,
            PasswordResetRequestsLastDay = passwordResets,
            TopLoginTargets = topTargets,
            TopIpHashes = topIpHashes,
            CaptchaPressureUsers = captchaSummaries,
            Alerts = alerts
        };
    }

    public async Task<UserManagementDetail?> GetUserManagementDetailAsync(int userId, CancellationToken ct = default)
    {
        var user = await _context.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new ControlPanelUserSummary(
                u.Id,
                u.Username,
                u.LoginName,
                u.Role,
                u.IsActive,
                u.SuspendedUntil,
                u.CreatedAt,
                u.LastSeenAt,
                u.PgpPublicKey))
            .FirstOrDefaultAsync(ct);

        if (user == null)
        {
            return null;
        }

        var loginHash = HashLoginName(user.LoginName);

        var loginAttempts = await _context.LoginAttempts
            .AsNoTracking()
            .Where(a => a.LoginNameHash == loginHash)
            .OrderByDescending(a => a.AttemptedAt)
            .Take(15)
            .Select(a => new LoginAttemptSummary(a.AttemptedAt, a.Success, a.LoginNameHash, a.IpAddressHash))
            .ToListAsync(ct);

        var sessionHistory = await _context.PageVisits
            .AsNoTracking()
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.VisitedAt)
            .Take(10)
            .Select(v => new SessionVisitSummary(v.VisitedAt, v.Path, v.IpHash, v.SessionFingerprint, v.UserAgent, v.Referrer))
            .ToListAsync(ct);

        var recentPosts = await _context.Posts
            .AsNoTracking()
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.CreatedAt)
            .Take(10)
            .Select(p => new PostModerationSummary(
                p.Id,
                p.ThreadId,
                p.Thread != null ? p.Thread.Title : $"Thread #{p.ThreadId}",
                p.CreatedAt,
                p.IsDeleted,
                p.IsReportHidden,
                p.ReportCount))
            .ToListAsync(ct);

        return new UserManagementDetail(user, loginAttempts, sessionHistory, recentPosts);
    }

    private static string HashLoginName(string loginName)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(loginName.ToLowerInvariant());
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }
}

public class ContentMetricsResult
{
    public int ThreadsDay { get; set; }
    public int ThreadsWeek { get; set; }
    public int ThreadsMonth { get; set; }
    public int PostsDay { get; set; }
    public int PostsWeek { get; set; }
    public int PostsMonth { get; set; }
    public int StickyOrPinned { get; set; }
    public int ReportsPending { get; set; }
    public IReadOnlyList<NamedMetric> TopCategories { get; set; } = Array.Empty<NamedMetric>();
    public double AnonymousPostRatio { get; set; }
    public int ModeratedPosts { get; set; }
    public int EditedPosts { get; set; }
    public int DeletedPosts { get; set; }
    public int ImagesUploadedDay { get; set; }
    public int UploadFailuresDay { get; set; }
    public int MalwareDetectionsDay { get; set; }
    public bool IncludeAdmin { get; set; }
    public ContentMetricsRange SelectedRange { get; set; }
    public DateTime RangeStartUtc { get; set; }
    public int ThreadsInRange { get; set; }
    public int PostsInRange { get; set; }
    public double AveragePostsPerThread { get; set; }
    public int VotesInRange { get; set; }
    public int ActivitiesInRange { get; set; }
    public int ModerationActionsInRange { get; set; }
    public int VisibleReportsInRange { get; set; }
    public int HiddenReportsInRange { get; set; }
}

public record NamedMetric(string Label, int Count);

public class UserEngagementResult
{
    public bool IncludeAdmin { get; set; }
    public int TotalUsers { get; set; }
    public int ActiveUsers7Days { get; set; }
    public int NewUsers30Days { get; set; }
    public int BannedUsers { get; set; }
    public int PgpVerifiedUsers { get; set; }
    public IReadOnlyList<UserContributionSummary> TopContributors { get; set; } = Array.Empty<UserContributionSummary>();
    public IReadOnlyList<SuspendedUserSummary> SuspendedUsers { get; set; } = Array.Empty<SuspendedUserSummary>();
    public IReadOnlyList<PostHighlight> TopPosts { get; set; } = Array.Empty<PostHighlight>();
    public IReadOnlyList<TimeSeriesPoint> GrowthSeries { get; set; } = Array.Empty<TimeSeriesPoint>();
    public PrivateMessageStats PrivateMessages { get; set; } = new PrivateMessageStats(0, 0, 0);
}

public record UserContributionSummary(int UserId, string Username, string Role, int PostsLast30Days, DateTime? LastSeenAt);

public record SuspendedUserSummary(int UserId, string Username, string Role, DateTime? SuspendedUntil, DateTime? LastSeenAt);

public record PostHighlight(int PostId, string Title, string Snippet, string Author, int Score);

public record TimeSeriesPoint(DateTime Day, int Count);

public record UserManagementDetail(
    ControlPanelUserSummary User,
    IReadOnlyList<LoginAttemptSummary> LoginAttempts,
    IReadOnlyList<SessionVisitSummary> RecentSessions,
    IReadOnlyList<PostModerationSummary> RecentPosts);

public record ControlPanelUserSummary(
    int UserId,
    string Username,
    string LoginName,
    string Role,
    bool IsActive,
    DateTime? SuspendedUntil,
    DateTime CreatedAt,
    DateTime? LastSeenAt,
    string? PgpPublicKey);

public record LoginAttemptSummary(DateTime AttemptedAt, bool Success, string LoginNameHash, string? IpAddressHash);

public record SessionVisitSummary(DateTime VisitedAt, string Path, string? IpHash, string? SessionFingerprint, string? UserAgent, string? Referrer);

public record PostModerationSummary(int PostId, int ThreadId, string ThreadTitle, DateTime CreatedAt, bool IsDeleted, bool IsReportHidden, int ReportCount);

public record PrivateMessageStats(int SentLast7Days, int DraftsLast7Days, int TotalDrafts);

public class ChatOversightResult
{
    public IReadOnlyList<NamedMetric> MessagesByRoom { get; set; } = Array.Empty<NamedMetric>();
    public IReadOnlyList<ChatHourlyActive> HourlyActive { get; set; } = Array.Empty<ChatHourlyActive>();
    public double VerifiedShare { get; set; }
    public double ModeratorShare { get; set; }
    public int MessagesLastHour { get; set; }
    public int UniqueChattersLastDay { get; set; }
    public IReadOnlyList<ChatMuteSummary> ActiveMutes { get; set; } = Array.Empty<ChatMuteSummary>();
    public IReadOnlyList<ChatRoomStatus> RoomStatuses { get; set; } = Array.Empty<ChatRoomStatus>();
}

public record ChatMuteSummary(int UserId, string Username, string Role, DateTime ExpiresAt);

public record ChatRoomStatus(string Room, bool IsPaused, DateTime? ExpiresAt, int MessagesLastHour);

public record ChatHourlyActive(DateTime HourUtc, int ParticipantCount);

public class SecurityOverviewResult
{
    public int FailedLoginsLastHour { get; set; }
    public int FailedLoginsLastDay { get; set; }
    public int SuccessfulLoginsLastDay { get; set; }
    public int PasswordResetRequestsLastDay { get; set; }
    public IReadOnlyList<NamedMetric> TopLoginTargets { get; set; } = Array.Empty<NamedMetric>();
    public IReadOnlyList<NamedMetric> TopIpHashes { get; set; } = Array.Empty<NamedMetric>();
    public IReadOnlyList<CaptchaPressureUser> CaptchaPressureUsers { get; set; } = Array.Empty<CaptchaPressureUser>();
    public IReadOnlyList<SecurityAlert> Alerts { get; set; } = Array.Empty<SecurityAlert>();
}

public record CaptchaPressureUser(int UserId, string Username, int ActivityCount);

public record SecurityAlert(string Title, string Details);

public enum ContentMetricsRange
{
    Day,
    Week,
    Month
}

public static class ContentMetricsRangeExtensions
{
    private static readonly IReadOnlyList<ContentMetricsRange> AllRanges = new[]
    {
        ContentMetricsRange.Day,
        ContentMetricsRange.Week,
        ContentMetricsRange.Month
    };

    public static IReadOnlyList<ContentMetricsRange> All => AllRanges;

    public static ContentMetricsRange Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return ContentMetricsRange.Day;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "week" or "7d" => ContentMetricsRange.Week,
            "month" or "30d" => ContentMetricsRange.Month,
            _ => ContentMetricsRange.Day
        };
    }

    public static string ToLabel(this ContentMetricsRange range) => range switch
    {
        ContentMetricsRange.Week => "7 days",
        ContentMetricsRange.Month => "30 days",
        _ => "24 hours"
    };

    public static string ToQueryValue(this ContentMetricsRange range) => range switch
    {
        ContentMetricsRange.Week => "7d",
        ContentMetricsRange.Month => "30d",
        _ => "24h"
    };

    public static DateTime GetWindowStartUtc(this ContentMetricsRange range, DateTime reference)
    {
        return range switch
        {
            ContentMetricsRange.Month => reference.AddMonths(-1),
            ContentMetricsRange.Week => reference.AddDays(-7),
            _ => reference.AddDays(-1)
        };
    }
}