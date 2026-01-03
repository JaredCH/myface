using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyFace.Data;

namespace MyFace.Services;

public class PostLogService
{
    private readonly ApplicationDbContext _db;

    public PostLogService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<PostLogRecord>> GetRecentPostsAsync(int take = 60, CancellationToken ct = default)
    {
        var size = Math.Clamp(take, 10, 200);
        var rows = await _db.Posts
            .AsNoTracking()
            .Include(p => p.Thread)
            .Include(p => p.User)
            .OrderByDescending(p => p.CreatedAt)
            .Take(size)
            .Select(p => new PostLogRecord(
                p.Id,
                p.ThreadId,
                p.Thread.Title,
                p.CreatedAt,
                p.UserId,
                p.User != null ? (string.IsNullOrWhiteSpace(p.User.Username) ? p.User.LoginName : p.User.Username) : null,
                p.IsAnonymous,
                p.IsDeleted,
                p.ReportCount,
                p.WasModerated,
                BuildSnippet(p.Content),
                p.Images.Count))
            .ToListAsync(ct);

        return rows;
    }

    private static string BuildSnippet(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return "(empty)";
        }

        var sanitized = Regex.Replace(content, @"\s+", " ").Trim();
        if (sanitized.Length <= 140)
        {
            return sanitized;
        }

        return sanitized[..137] + "...";
    }
}

public record PostLogRecord(
    int PostId,
    int ThreadId,
    string ThreadTitle,
    DateTime CreatedAt,
    int? UserId,
    string? Username,
    bool IsAnonymous,
    bool IsDeleted,
    int ReportCount,
    bool WasModerated,
    string Snippet,
    int ImageCount);
