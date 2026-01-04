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
}
