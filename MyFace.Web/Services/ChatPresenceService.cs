using System.Collections.Concurrent;
using System.Linq;
using Microsoft.Extensions.Caching.Memory;
using MyFace.Core.Entities;

namespace MyFace.Web.Services;

public class ChatPresenceService
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan ViewerTimeout = TimeSpan.FromSeconds(25);
    private static readonly TimeSpan BucketLifetime = TimeSpan.FromMinutes(10);

    public ChatPresenceService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public IReadOnlyList<ChatViewerPresence> Touch(string room, User? user, string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(room))
        {
            return Array.Empty<ChatViewerPresence>();
        }

        sessionId = NormalizeSession(sessionId);
        var bucket = _cache.GetOrCreate(GetCacheKey(room), entry =>
        {
            entry.SlidingExpiration = BucketLifetime;
            return new ConcurrentDictionary<string, PresenceRecord>();
        })!;

        var key = BuildKey(user, sessionId);
        bucket[key] = new PresenceRecord
        {
            DisplayName = BuildDisplayName(user, sessionId),
            Role = (user?.Role ?? "User") ?? "User",
            Verified = user?.PGPVerifications.Any(v => v.Verified) ?? false,
            LastSeenUtc = DateTime.UtcNow
        };

        var cutoff = DateTime.UtcNow - ViewerTimeout;
        foreach (var kv in bucket)
        {
            if (kv.Value.LastSeenUtc < cutoff)
            {
                bucket.TryRemove(kv.Key, out _);
            }
        }

        return bucket
            .Select(kv => new ChatViewerPresence(
                kv.Value.DisplayName,
                kv.Value.Role,
                kv.Value.Verified,
                kv.Key == key,
                kv.Value.LastSeenUtc))
            .OrderBy(v => ViewerRank(v))
            .ThenBy(v => v.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetCacheKey(string room) => $"chat:viewers:{room}";

    private static string BuildKey(User? user, string sessionId)
    {
        if (user != null)
        {
            return $"user:{user.Id}";
        }
        return $"session:{sessionId}";
    }

    private static string NormalizeSession(string? sessionId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            return Guid.NewGuid().ToString("N");
        }
        return sessionId;
    }

    private static string BuildDisplayName(User? user, string sessionId)
    {
        if (user != null)
        {
            return user.Username;
        }

        var slug = sessionId.Length <= 6 ? sessionId : sessionId[^6..];
        return $"Guest-{slug.ToUpperInvariant()}";
    }

    private static int ViewerRank(ChatViewerPresence viewer)
    {
        var role = viewer.Role?.ToLowerInvariant();
        return role switch
        {
            "admin" => 0,
            "moderator" => 1,
            _ when viewer.Verified => 2,
            _ => viewer.IsSelf ? 3 : 4
        };
    }

    private sealed class PresenceRecord
    {
        public string DisplayName { get; set; } = string.Empty;
        public string Role { get; set; } = "User";
        public bool Verified { get; set; }
        public DateTime LastSeenUtc { get; set; }
    }
}

public record ChatViewerPresence(string DisplayName, string Role, bool Verified, bool IsSelf, DateTime LastSeenUtc);
