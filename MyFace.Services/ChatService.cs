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

    private const int MessageMaxLength = 500;
    private static readonly TimeSpan RateLimitWindow = TimeSpan.FromSeconds(3);

    public ChatService(ApplicationDbContext db, IMemoryCache cache, ChatSnapshotService snapshotService)
    {
        _db = db;
        _cache = cache;
        _snapshotService = snapshotService;
    }

    public bool IsValidRoom(string room) => AllowedRooms.Contains(room);

    public bool IsRoomPaused(string room)
    {
        return _cache.TryGetValue(GetPauseKey(room), out bool paused) && paused;
    }

    public void SetRoomPaused(string room, bool paused)
    {
        _cache.Set(GetPauseKey(room), paused, TimeSpan.FromHours(12));
    }

    public bool IsUserMuted(int userId)
    {
        if (_cache.TryGetValue(GetMuteKey(userId), out DateTime until))
        {
            return until > DateTime.UtcNow;
        }
        return false;
    }

    public void MuteUser(int userId, TimeSpan duration)
    {
        _cache.Set(GetMuteKey(userId), DateTime.UtcNow.Add(duration), duration);
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        var sql = @"
        CREATE TABLE IF NOT EXISTS \"ChatMessages\" (
            \"Id\" SERIAL PRIMARY KEY,
            \"Room\" varchar(32) NOT NULL,
            \"UserId\" integer NULL,
            \"UsernameSnapshot\" varchar(50) NOT NULL,
            \"RoleSnapshot\" varchar(16) NOT NULL DEFAULT 'User',
            \"IsVerifiedSnapshot\" boolean NOT NULL DEFAULT false,
            \"Content\" text NOT NULL,
            \"CreatedAt\" timestamptz NOT NULL DEFAULT NOW()
        );
        CREATE INDEX IF NOT EXISTS \"IX_ChatMessages_Room_CreatedAt\" ON \"ChatMessages\" (\"Room\", \"CreatedAt\");
        ";

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
        if (_cache.TryGetValue(GetRateKey(sender.Id, room), out DateTime lastSent))
        {
            if (now - lastSent < RateLimitWindow)
            {
                return ChatPostResult.Failed("You are sending messages too quickly.");
            }
        }

        var content = rawContent.Trim();
        if (content.Length > MessageMaxLength)
        {
            content = content.Substring(0, MessageMaxLength);
        }

        var encoded = System.Web.HttpUtility.HtmlEncode(content);
        encoded = encoded.Replace("\r\n", "<br />").Replace("\n", "<br />");

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

        _cache.Set(GetRateKey(sender.Id, room), now, RateLimitWindow);
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
