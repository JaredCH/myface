using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;
using Npgsql;

namespace MyFace.Services;

public class ProfileChatService
{
    private const int MaxMessageLength = 500;
    private readonly ApplicationDbContext _context;

    public ProfileChatService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ProfileChatMessage>> GetRecentMessagesAsync(int targetUserId, int take, CancellationToken ct = default)
    {
        try
        {
            var effectiveTake = Math.Max(1, take);
            return await _context.ProfileChatMessages
                .Where(m => m.TargetUserId == targetUserId)
                .OrderByDescending(m => m.CreatedAt)
                .Take(effectiveTake)
                .AsNoTracking()
                .ToListAsync(ct);
        }
        catch (PostgresException ex) when (IsMissingRelation(ex))
        {
            // Database migrations have not been applied yet. Fail gracefully and hide the chat history.
            return new List<ProfileChatMessage>();
        }
    }

    public async Task<ProfileChatMessage> AddMessageAsync(User author, User targetUser, string body, CancellationToken ct = default)
    {
        if (author == null) throw new ArgumentNullException(nameof(author));
        if (targetUser == null) throw new ArgumentNullException(nameof(targetUser));

        var sanitized = Sanitize(body);
        if (string.IsNullOrEmpty(sanitized))
        {
            throw new InvalidOperationException("Message cannot be empty.");
        }

        var message = new ProfileChatMessage
        {
            TargetUserId = targetUser.Id,
            AuthorUserId = author.Id,
            AuthorUsername = string.IsNullOrWhiteSpace(author.Username) ? "user" : author.Username,
            AuthorRole = string.IsNullOrWhiteSpace(author.Role) ? "User" : author.Role,
            Body = sanitized,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _context.ProfileChatMessages.Add(message);
            await _context.SaveChangesAsync(ct);
            return message;
        }
        catch (PostgresException ex) when (IsMissingRelation(ex))
        {
            throw new InvalidOperationException("Profile chat is not available yet. Please contact support to run the latest database migrations.", ex);
        }
    }

    private static string Sanitize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var normalized = raw.Replace("\r", string.Empty).Trim();
        if (normalized.Length > MaxMessageLength)
        {
            normalized = normalized.Substring(0, MaxMessageLength).TrimEnd();
        }

        return normalized;
    }

    private static bool IsMissingRelation(PostgresException ex)
    {
        return ex.SqlState == PostgresErrorCodes.UndefinedTable;
    }
}
