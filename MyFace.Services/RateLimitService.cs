using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class RateLimitService
{
    private readonly ApplicationDbContext _context;
    private readonly ControlSettingsReader _settings;
    
    // Configuration
    private const int InitialAttempts = 5; // First 5 attempts no delay
    private const int MaxDelaySeconds = 900; // Max 15 minutes
    
    // Posting rate limits
    private const int UnverifiedThreadsPerHour = 1;
    private const int UnverifiedCommentsPerHour = 6;
    private const int VerifiedThreadsPerHour = 2;
    private const int VerifiedCommentsPerHour = 15;
    
    public RateLimitService(ApplicationDbContext context, ControlSettingsReader settings)
    {
        _context = context;
        _settings = settings;
    }
    
    /// <summary>
    /// Check if login attempt is allowed. Returns delay in seconds if rate limited, 0 if allowed.
    /// </summary>
    public async Task<int> CheckLoginRateLimitAsync(string loginName)
    {
        var loginNameHash = HashString(loginName.ToLowerInvariant());
        var now = DateTime.UtcNow;
        var lookbackWindow = now.AddHours(-24); // Look at last 24 hours
        
        // Get recent failed attempts for this login name
        var recentFailedAttempts = await _context.LoginAttempts
            .Where(a => a.LoginNameHash == loginNameHash && 
                       !a.Success && 
                       a.AttemptedAt >= lookbackWindow)
            .OrderByDescending(a => a.AttemptedAt)
            .ToListAsync();
        
        var graceAttempts = await _settings.GetIntAsync(ControlSettingKeys.RateLoginInitialAttempts, InitialAttempts);
        var failedCount = recentFailedAttempts.Count;
        
        // First grace attempts: no delay
        if (failedCount < graceAttempts)
        {
            return 0;
        }
        
        // Get the most recent failed attempt
        var lastAttempt = recentFailedAttempts.FirstOrDefault();
        if (lastAttempt == null)
        {
            return 0;
        }
        
        // Calculate exponential backoff: 2^(attempts - InitialAttempts) seconds
        // Example: 6th attempt = 2^1 = 2s, 7th = 4s, 8th = 8s, 9th = 16s, 10th = 32s
        var maxDelaySeconds = await _settings.GetIntAsync(ControlSettingKeys.RateLoginMaxDelaySeconds, MaxDelaySeconds);
        var attemptsOverLimit = failedCount - graceAttempts + 1;
        var requiredDelay = Math.Min((int)Math.Pow(2, attemptsOverLimit), maxDelaySeconds);
        
        var timeSinceLastAttempt = (now - lastAttempt.AttemptedAt).TotalSeconds;
        var remainingDelay = requiredDelay - (int)timeSinceLastAttempt;
        
        return Math.Max(0, remainingDelay);
    }
    
    /// <summary>
    /// Record a login attempt (success or failure)
    /// </summary>
    public async Task RecordLoginAttemptAsync(string loginName, bool success)
    {
        var loginNameHash = HashString(loginName.ToLowerInvariant());
        
        var attempt = new LoginAttempt
        {
            LoginNameHash = loginNameHash,
            AttemptedAt = DateTime.UtcNow,
            Success = success,
            IpAddressHash = string.Empty // Could add IP hashing here if needed
        };
        
        _context.LoginAttempts.Add(attempt);
        await _context.SaveChangesAsync();
        
        // If successful, we could optionally clear old failed attempts for this user
        if (success)
        {
            // Clean up old failed attempts (older than 24 hours)
            var cutoff = DateTime.UtcNow.AddHours(-24);
            var oldAttempts = await _context.LoginAttempts
                .Where(a => a.LoginNameHash == loginNameHash && a.AttemptedAt < cutoff)
                .ToListAsync();
            
            _context.LoginAttempts.RemoveRange(oldAttempts);
            await _context.SaveChangesAsync();
        }
    }
    
    /// <summary>
    /// Check captcha rate limit for posting/voting. Returns true if captcha required.
    /// </summary>
    public async Task<bool> IsCaptchaRequiredForActivityAsync(int? userId, string activityType)
    {
        if (userId == null) return false; // Anonymous users handled by session-based captcha
        
        var now = DateTime.UtcNow;
        var oneHourAgo = now.AddHours(-1);
        
        // Count activities in last hour
        var recentActivityCount = await _context.Activities
            .Where(a => a.UserId == userId && 
                       a.CreatedAt >= oneHourAgo)
            .CountAsync();
        
        // Thresholds: Allow 10 posts/votes per hour before requiring captcha
        var activityThreshold = await _settings.GetIntAsync(ControlSettingKeys.RateActivityPerHour, 10);
        
        return recentActivityCount >= activityThreshold;
    }
    
    private static string HashString(string input)
    {
        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    /// <summary>
    /// Check posting rate limit (threads/comments). Returns delay in seconds if limited, 0 if allowed.
    /// </summary>
    public async Task<PostingRateLimitResult> CheckPostingRateLimitAsync(
        int userId,
        string contentType, // "Thread" or "Comment"
        string? sessionFingerprint = null,
        string? torFingerprint = null)
    {
        var user = await _context.Users
            .Include(u => u.PGPVerifications)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return new PostingRateLimitResult { IsAllowed = false, DelaySeconds = 60 };
        }

        // Check if user is PGP verified
        var isVerified = user.PGPVerifications.Any(v => v.Verified);

        // Determine rate limits based on content type and verification
        var (limit, windowHours) = contentType.ToLower() switch
        {
            "thread" => isVerified 
                ? (await _settings.GetIntAsync("RateLimit.VerifiedThreadsPerHour", VerifiedThreadsPerHour), 1)
                : (await _settings.GetIntAsync("RateLimit.UnverifiedThreadsPerHour", UnverifiedThreadsPerHour), 1),
            "comment" => isVerified 
                ? (await _settings.GetIntAsync("RateLimit.VerifiedCommentsPerHour", VerifiedCommentsPerHour), 1)
                : (await _settings.GetIntAsync("RateLimit.UnverifiedCommentsPerHour", UnverifiedCommentsPerHour), 1),
            _ => (10, 1)
        };

        var now = DateTime.UtcNow;
        var windowStart = now.AddHours(-windowHours);

        // Count posts by account
        var postsByAccount = contentType.ToLower() == "thread"
            ? await _context.Threads.CountAsync(t => t.UserId == userId && t.CreatedAt >= windowStart)
            : await _context.Posts.CountAsync(p => p.UserId == userId && p.CreatedAt >= windowStart);

        if (postsByAccount >= limit)
        {
            // Find the oldest post in the window to determine when rate limit resets
            var oldestPost = contentType.ToLower() == "thread"
                ? await _context.Threads
                    .Where(t => t.UserId == userId && t.CreatedAt >= windowStart)
                    .OrderBy(t => t.CreatedAt)
                    .Select(t => t.CreatedAt)
                    .FirstOrDefaultAsync()
                : await _context.Posts
                    .Where(p => p.UserId == userId && p.CreatedAt >= windowStart)
                    .OrderBy(p => p.CreatedAt)
                    .Select(p => p.CreatedAt)
                    .FirstOrDefaultAsync();

            if (oldestPost != default)
            {
                var resetTime = oldestPost.AddHours(windowHours);
                var delaySeconds = (int)(resetTime - now).TotalSeconds;
                
                return new PostingRateLimitResult
                {
                    IsAllowed = false,
                    DelaySeconds = Math.Max(1, delaySeconds),
                    Reason = $"Rate limit: {limit} {contentType}s per {windowHours} hour(s)",
                    LimitType = "Account",
                    SilentFail = true // Use silent failure strategy
                };
            }
        }

        // Additional checks for session-based limits (Tor resilient)
        if (!string.IsNullOrEmpty(sessionFingerprint))
        {
            // Check session-based posting rate
            var postsBySession = await CountPostsBySessionAsync(sessionFingerprint, contentType, windowStart);
            var sessionLimit = limit * 2; // More lenient for session-based

            if (postsBySession >= sessionLimit)
            {
                return new PostingRateLimitResult
                {
                    IsAllowed = false,
                    DelaySeconds = 60,
                    Reason = "Session rate limit exceeded",
                    LimitType = "Session",
                    SilentFail = true
                };
            }
        }

        return new PostingRateLimitResult { IsAllowed = true, DelaySeconds = 0 };
    }

    /// <summary>
    /// Generate Tor circuit fingerprint from request characteristics
    /// </summary>
    public string GenerateTorFingerprint(string userAgent, string acceptHeaders, string? acceptLanguage = null)
    {
        var combined = $"{userAgent}|{acceptHeaders}|{acceptLanguage ?? ""}";
        return HashString(combined).Substring(0, 32); // Shorter hash for fingerprint
    }

    /// <summary>
    /// Count posts by session (for Tor users who share accounts but different sessions)
    /// </summary>
    private async Task<int> CountPostsBySessionAsync(string sessionFingerprint, string contentType, DateTime since)
    {
        // This would require adding SessionFingerprint to Thread/Post entities
        // For now, approximate using infractions table which has session tracking
        var sessionActivity = await _context.UserInfractions
            .Where(i => i.SessionFingerprint == sessionFingerprint && i.OccurredAt >= since)
            .CountAsync();

        return sessionActivity;
    }

    /// <summary>
    /// Apply silent rate limit failure (accept request, delay, return generic error)
    /// </summary>
    public async Task ApplySilentRateLimitAsync(int delaySeconds)
    {
        // Add random jitter to make timing attacks harder
        var random = new Random();
        var jitter = random.Next(1000, 3000); // 1-3 seconds
        var totalDelay = Math.Min(delaySeconds * 1000, 10000) + jitter; // Cap at 10s + jitter

        await Task.Delay(totalDelay);
    }
}

public class PostingRateLimitResult
{
    public bool IsAllowed { get; set; }
    public int DelaySeconds { get; set; }
    public string? Reason { get; set; }
    public string? LimitType { get; set; } // "Account", "Session", "TorFingerprint"
    public bool SilentFail { get; set; }
}
