using System.Collections.Concurrent;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class ChatService
{
    public const string RoomPublic = "Public";
    public const string RoomVerified = "Verified";
    public const string RoomSigilShorts = "SigilShorts";

    private static readonly string[] AllowedRooms = { RoomPublic, RoomVerified, RoomSigilShorts };

    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;
    private readonly ChatSnapshotService _snapshotService;
    private readonly ControlSettingsReader _settings;
    private readonly ConcurrentDictionary<int, DateTime> _muteExpirations = new();
    private readonly ConcurrentDictionary<string, DateTime> _pauseExpirations = new(StringComparer.OrdinalIgnoreCase);

    public ChatService(ApplicationDbContext db, IMemoryCache cache, ChatSnapshotService snapshotService, ControlSettingsReader settings)
    {
        _db = db;
        _cache = cache;
        _snapshotService = snapshotService;
        _settings = settings;
    }

    public bool IsValidRoom(string room) => AllowedRooms.Contains(room);

    public bool IsRoomPaused(string room)
    {
        if (_cache.TryGetValue(GetPauseKey(room), out bool paused) && paused)
        {
            return true;
        }

        if (_pauseExpirations.TryGetValue(room, out var expiresAt))
        {
            if (expiresAt <= DateTime.UtcNow)
            {
                _pauseExpirations.TryRemove(room, out _);
                _cache.Remove(GetPauseKey(room));
                return false;
            }

            _cache.Set(GetPauseKey(room), true, expiresAt - DateTime.UtcNow);
            return true;
        }

        return false;
    }

    public void SetRoomPaused(string room, bool paused, TimeSpan? duration = null)
    {
        if (!IsValidRoom(room))
        {
            return;
        }

        if (paused)
        {
            var ttl = duration ?? TimeSpan.FromHours(12);
            var expires = DateTime.UtcNow.Add(ttl);
            _pauseExpirations[room] = expires;
            _cache.Set(GetPauseKey(room), true, ttl);
        }
        else
        {
            _pauseExpirations.TryRemove(room, out _);
            _cache.Remove(GetPauseKey(room));
        }
    }

    public bool IsUserMuted(int userId)
    {
        if (_muteExpirations.TryGetValue(userId, out var expires) && expires > DateTime.UtcNow)
        {
            return true;
        }

        if (_cache.TryGetValue(GetMuteKey(userId), out DateTime until) && until > DateTime.UtcNow)
        {
            _muteExpirations[userId] = until;
            return true;
        }

        _muteExpirations.TryRemove(userId, out _);
        _cache.Remove(GetMuteKey(userId));
        return false;
    }

    public void MuteUser(int userId, TimeSpan duration)
    {
        var expires = DateTime.UtcNow.Add(duration);
        _muteExpirations[userId] = expires;
        _cache.Set(GetMuteKey(userId), expires, duration);
    }

    public bool TryUnmuteUser(int userId)
    {
        var removed = _muteExpirations.TryRemove(userId, out _);
        _cache.Remove(GetMuteKey(userId));
        return removed;
    }

    public bool TryPauseRoom(string room, TimeSpan duration)
    {
        if (!IsValidRoom(room))
        {
            return false;
        }

        SetRoomPaused(room, true, duration);
        return true;
    }

    public bool TryResumeRoom(string room)
    {
        if (!IsValidRoom(room))
        {
            return false;
        }

        SetRoomPaused(room, false);
        return true;
    }

    public IReadOnlyList<ChatMuteState> GetActiveMutes()
    {
        var now = DateTime.UtcNow;
        var active = new List<ChatMuteState>();
        foreach (var entry in _muteExpirations.ToArray())
        {
            if (entry.Value <= now)
            {
                _muteExpirations.TryRemove(entry.Key, out _);
                _cache.Remove(GetMuteKey(entry.Key));
                continue;
            }

            active.Add(new ChatMuteState(entry.Key, entry.Value));
        }

        return active
            .OrderByDescending(m => m.ExpiresAt)
            .ToList();
    }

    public IReadOnlyList<ChatPauseState> GetRoomStatuses()
    {
        var statuses = new List<ChatPauseState>();
        foreach (var room in AllowedRooms)
        {
            var isPaused = IsRoomPaused(room);
            _pauseExpirations.TryGetValue(room, out var expires);
            statuses.Add(new ChatPauseState(room, isPaused, isPaused ? expires : null));
        }

        return statuses;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        var sql = """
        CREATE TABLE IF NOT EXISTS "ChatMessages" (
            "Id" SERIAL PRIMARY KEY,
            "Room" varchar(32) NOT NULL,
            "UserId" integer NULL,
            "UsernameSnapshot" varchar(50) NOT NULL,
            "RoleSnapshot" varchar(16) NOT NULL DEFAULT 'User',
            "IsVerifiedSnapshot" boolean NOT NULL DEFAULT false,
            "Content" text NOT NULL,
            "CreatedAt" timestamptz NOT NULL DEFAULT NOW()
        );
        CREATE INDEX IF NOT EXISTS "IX_ChatMessages_Room_CreatedAt" ON "ChatMessages" ("Room", "CreatedAt");
        """;

        await _db.Database.ExecuteSqlRawAsync(sql, ct);
    }

    public async Task<ChatPostResult> PostMessageAsync(User sender, string room, string rawContent, CancellationToken ct = default)
    {
        if (!IsValidRoom(room))
        {
            return ChatPostResult.Failed("Unknown room.");
        }

        await EnsureSchemaAsync(ct);

        if (IsRoomPaused(room))
        {
            return ChatPostResult.Failed("Chat is paused for this room.");
        }

        if (IsUserMuted(sender.Id))
        {
            return ChatPostResult.Failed("You are muted in chat.");
        }

        if (!CanViewRoom(sender, room) || !CanPostToRoom(sender, room))
        {
            return ChatPostResult.Failed("You are not allowed to post here.");
        }

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            return ChatPostResult.Failed("Message cannot be empty.");
        }

        var now = DateTime.UtcNow;
        var maxLength = await _settings.GetIntAsync(ControlSettingKeys.ChatMessageMaxLength, 199, ct);
        var cooldownSeconds = await _settings.GetIntAsync(ControlSettingKeys.ChatRateWindowSeconds, 7, ct);
        var rateLimitWindow = TimeSpan.FromSeconds(Math.Clamp(cooldownSeconds, 1, 600));

        if (_cache.TryGetValue(GetRateKey(sender.Id, room), out DateTime lastSent))
        {
            if (now - lastSent < rateLimitWindow)
            {
                return ChatPostResult.Failed("You are sending messages too quickly.");
            }
        }

        var content = rawContent.Replace("\r", " ").Replace("\n", " ").Trim();
        if (content.Length > maxLength)
        {
            content = content[..maxLength];
        }

        var encoded = System.Web.HttpUtility.HtmlEncode(content);

        var isVerified = sender.PGPVerifications.Any(v => v.Verified);
        var entity = new ChatMessage
        {
            Room = room,
            UserId = sender.Id,
            UsernameSnapshot = sender.Username,
            RoleSnapshot = sender.Role,
            IsVerifiedSnapshot = isVerified,
            Content = encoded,
            CreatedAt = now
        };

        _db.ChatMessages.Add(entity);
        await _db.SaveChangesAsync(ct);

        _cache.Set(GetRateKey(sender.Id, room), now, rateLimitWindow);
        _snapshotService.Invalidate(room);

        return ChatPostResult.Success();
    }

    public async Task<ChatDeleteResult> DeleteMessageAsync(int messageId, User moderator, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        if (!IsModeratorOrAdmin(moderator))
        {
            return ChatDeleteResult.Failed("Not authorized.");
        }

        var msg = await _db.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg == null)
        {
            return ChatDeleteResult.Failed("Message not found.");
        }

        _db.ChatMessages.Remove(msg);
        await _db.SaveChangesAsync(ct);
        _snapshotService.Invalidate(msg.Room);
        return ChatDeleteResult.Success(msg.Room);
    }

    public async Task<List<ChatMessage>> GetRecentMessagesForUserAsync(int targetUserId, int limit = 5, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        return await _db.ChatMessages
            .Where(m => m.UserId == targetUserId)
            .OrderByDescending(m => m.CreatedAt)
            .Take(Math.Max(1, limit))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public bool CanViewRoom(User? user, string room)
    {
        return room switch
        {
            RoomPublic => true,
            RoomVerified => user != null && (IsVerified(user) || IsModeratorOrAdmin(user)),
            RoomSigilShorts => true,
            _ => false
        };
    }

    public bool CanPostToRoom(User? user, string room)
    {
        if (user == null) return false;

        return room switch
        {
            RoomPublic => true,
            RoomVerified => IsVerified(user) || IsModeratorOrAdmin(user),
            RoomSigilShorts => IsModeratorOrAdmin(user),
            _ => false
        };
    }

    public bool IsModeratorOrAdmin(User? user)
    {
        if (user == null) return false;
        var role = user.Role?.ToLowerInvariant();
        return role == "admin" || role == "moderator";
    }

    public bool IsVerified(User user)
    {
        return user.PGPVerifications.Any(v => v.Verified);
    }

    private static string GetRateKey(int userId, string room) => $"chat:rate:{room}:{userId}";
    private static string GetPauseKey(string room) => $"chat:paused:{room}";
    private static string GetMuteKey(int userId) => $"chat:muted:{userId}";
}

public record ChatPostResult(bool Ok, string? Error)
{
    public static ChatPostResult Success() => new(true, null);
    public static ChatPostResult Failed(string error) => new(false, error);
}

public record ChatDeleteResult(bool Ok, string? Error, string? Room)
{
    public static ChatDeleteResult Success(string room) => new(true, null, room);
    public static ChatDeleteResult Failed(string error) => new(false, error, null);
}

public record ChatMuteState(int UserId, DateTime ExpiresAt);

public record ChatPauseState(string Room, bool IsPaused, DateTime? ExpiresAt);
