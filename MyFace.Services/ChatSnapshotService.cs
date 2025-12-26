using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class ChatSnapshotService
{
    private readonly ApplicationDbContext _db;
    private readonly IMemoryCache _cache;

    private const int DefaultMessageLimit = 80;

    public ChatSnapshotService(ApplicationDbContext db, IMemoryCache cache)
    {
        _db = db;
        _cache = cache;
    }

    public void Invalidate(string room)
    {
        var key = CacheKey(room);
        _cache.Remove(key);
    }

    public async Task<string> GetSnapshotAsync(string room, CancellationToken ct = default)
    {
        var key = CacheKey(room);
        if (_cache.TryGetValue(key, out string cached))
        {
            return cached;
        }

        var html = await BuildSnapshotAsync(room, ct);
        _cache.Set(key, html, TimeSpan.FromSeconds(30));
        return html;
    }

    private async Task<string> BuildSnapshotAsync(string room, CancellationToken ct)
    {
        var messages = await _db.ChatMessages
            .Where(m => m.Room == room)
            .OrderByDescending(m => m.CreatedAt)
            .Take(DefaultMessageLimit)
            .AsNoTracking()
            .ToListAsync(ct);

        messages.Reverse(); // oldest first for display

        var sb = new StringBuilder();
        sb.Append("<div class=\"chat-lines\">");

        foreach (var msg in messages)
        {
            var ts = msg.CreatedAt.ToUniversalTime().ToString("HH:mm");
            var role = (msg.RoleSnapshot ?? "User").ToLowerInvariant();
            var roleClass = role switch
            {
                "admin" => "role-admin",
                "moderator" => "role-mod",
                _ => msg.IsVerifiedSnapshot ? "role-verified" : "role-user"
            };

            var name = System.Web.HttpUtility.HtmlEncode(msg.UsernameSnapshot);
            var content = msg.Content;
            content = RenderMentions(content);

            sb.Append("<div class=\"line\">");
            sb.Append($"<span class=\"ts\">[{ts}]</span> ");
            sb.Append($"<span class=\"user {roleClass}\">@{name}</span> ");
            sb.Append($"<span class=\"msg\">{content}</span> ");
            sb.Append($"<span class=\"meta\">#");
            sb.Append(msg.Id);
            sb.Append("</span>");
            sb.Append("</div>");
        }

        if (messages.Count == 0)
        {
            sb.Append("<div class=\"line\"><span class=\"ts\">[--:--]</span> <span class=\"msg\">No messages yet.</span></div>");
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static string RenderMentions(string content)
    {
        // content is already HTML-encoded and may contain <br />
        return System.Text.RegularExpressions.Regex.Replace(
            content,
            @"@([a-zA-Z0-9_]+)",
            "<span class=\\\"mention\\\">@$1</span>");
    }

    private static string CacheKey(string room) => $"chat:snapshot:{room}";
}
