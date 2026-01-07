using Microsoft.AspNetCore.Http;

namespace MyFace.Web.Services;

public static class FingerprintExtensions
{
    /// <summary>
    /// Get session fingerprint from cookie-based session
    /// </summary>
    public static string? GetSessionFingerprint(this HttpContext context)
    {
        // Use ASP.NET session ID as session fingerprint
        var sessionId = context.Session.Id;
        return string.IsNullOrEmpty(sessionId) ? null : sessionId;
    }

    /// <summary>
    /// Generate Tor circuit fingerprint from request characteristics
    /// </summary>
    public static string? GetTorFingerprint(this HttpContext context)
    {
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        var accept = context.Request.Headers["Accept"].ToString();
        var acceptLanguage = context.Request.Headers["Accept-Language"].ToString();

        if (string.IsNullOrEmpty(userAgent))
        {
            return null;
        }

        // Use the RateLimitService hash function
        var combined = $"{userAgent}|{accept}|{acceptLanguage}";
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(combined);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash).Substring(0, 32);
    }
}
