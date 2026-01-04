using System.Threading;
using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class MailService
{
    private readonly ApplicationDbContext _db;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaReady;

    private const int SubjectMaxLength = 200;
    private const int BodyMaxLength = 5000;

    public MailService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (_schemaReady) return;

        await SchemaLock.WaitAsync(ct);
        try
        {
            if (_schemaReady) return;

            var sql = """
            CREATE TABLE IF NOT EXISTS "PrivateMessages" (
                "Id" SERIAL PRIMARY KEY,
                "SenderId" integer NULL,
                "RecipientId" integer NULL,
                "SenderUsernameSnapshot" varchar(50) NOT NULL,
                "RecipientUsernameSnapshot" varchar(50) NOT NULL,
                "Subject" varchar(200) NOT NULL,
                "Body" text NOT NULL,
                "IsDraft" boolean NOT NULL DEFAULT false,
                "CreatedAt" timestamptz NOT NULL DEFAULT NOW(),
                "SentAt" timestamptz NULL,
                "ReadAt" timestamptz NULL,
                "SenderDeleted" boolean NOT NULL DEFAULT false,
                "RecipientDeleted" boolean NOT NULL DEFAULT false
            );
            CREATE INDEX IF NOT EXISTS "IX_PrivateMessages_RecipientId_CreatedAt" ON "PrivateMessages" ("RecipientId", "CreatedAt");
            CREATE INDEX IF NOT EXISTS "IX_PrivateMessages_SenderId_CreatedAt" ON "PrivateMessages" ("SenderId", "CreatedAt");
            ALTER TABLE "PrivateMessages" ADD COLUMN IF NOT EXISTS "SenderDeleted" boolean NOT NULL DEFAULT false;
            ALTER TABLE "PrivateMessages" ADD COLUMN IF NOT EXISTS "RecipientDeleted" boolean NOT NULL DEFAULT false;
            """;

            await _db.Database.ExecuteSqlRawAsync(sql, ct);
            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }

    public async Task<MailSendResult> SendAsync(User sender, IEnumerable<User> recipients, string subject, string body, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        var now = DateTime.UtcNow;
        var trimmedSubject = (subject ?? string.Empty).Trim();
        if (trimmedSubject.Length > SubjectMaxLength)
        {
            trimmedSubject = trimmedSubject.Substring(0, SubjectMaxLength);
        }

        var trimmedBody = (body ?? string.Empty).Trim();
        if (trimmedBody.Length > BodyMaxLength)
        {
            trimmedBody = trimmedBody.Substring(0, BodyMaxLength);
        }

        var recipientList = recipients.ToList();
        if (recipientList.Count == 0)
        {
            return MailSendResult.Failed("No recipients found.");
        }

        foreach (var r in recipientList)
        {
            var message = new PrivateMessage
            {
                SenderId = sender.Id,
                RecipientId = r.Id,
                SenderUsernameSnapshot = sender.Username,
                RecipientUsernameSnapshot = r.Username,
                Subject = trimmedSubject,
                Body = trimmedBody,
                IsDraft = false,
                CreatedAt = now,
                SentAt = now,
                SenderDeleted = false,
                RecipientDeleted = false
            };

            _db.PrivateMessages.Add(message);
        }

        await _db.SaveChangesAsync(ct);
        return MailSendResult.Success(recipientList.Count);
    }

    public async Task<MailSendResult> SaveDraftAsync(User sender, User? recipient, string subject, string body, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        var trimmedSubject = (subject ?? string.Empty).Trim();
        if (trimmedSubject.Length > SubjectMaxLength)
        {
            trimmedSubject = trimmedSubject.Substring(0, SubjectMaxLength);
        }

        var trimmedBody = (body ?? string.Empty).Trim();
        if (trimmedBody.Length > BodyMaxLength)
        {
            trimmedBody = trimmedBody.Substring(0, BodyMaxLength);
        }

        var message = new PrivateMessage
        {
            SenderId = sender.Id,
            RecipientId = recipient?.Id,
            SenderUsernameSnapshot = sender.Username,
            RecipientUsernameSnapshot = recipient?.Username ?? string.Empty,
            Subject = trimmedSubject,
            Body = trimmedBody,
            IsDraft = true,
            CreatedAt = DateTime.UtcNow,
            SentAt = null,
            SenderDeleted = false,
            RecipientDeleted = false
        };

        _db.PrivateMessages.Add(message);
        await _db.SaveChangesAsync(ct);
        return MailSendResult.Success(1);
    }

    public async Task<bool> SendDraftAsync(int draftId, int userId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);

        var draft = await _db.PrivateMessages.FirstOrDefaultAsync(m => m.Id == draftId && m.SenderId == userId && m.IsDraft && !m.SenderDeleted, ct);
        if (draft == null || draft.RecipientId == null)
        {
            return false;
        }

        draft.IsDraft = false;
        draft.SentAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<List<PrivateMessage>> GetInboxAsync(int userId, int take = 100, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        return await _db.PrivateMessages
            .Where(m => m.RecipientId == userId && !m.IsDraft && !m.RecipientDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<PrivateMessage>> GetOutboxAsync(int userId, int take = 100, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        return await _db.PrivateMessages
            .Where(m => m.SenderId == userId && !m.IsDraft && !m.SenderDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<List<PrivateMessage>> GetDraftsAsync(int userId, int take = 100, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        return await _db.PrivateMessages
            .Where(m => m.SenderId == userId && m.IsDraft && !m.SenderDeleted)
            .OrderByDescending(m => m.CreatedAt)
            .Take(take)
            .ToListAsync(ct);
    }

    public async Task<int> GetUnreadCountAsync(int userId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        return await _db.PrivateMessages
            .AsNoTracking()
            .Where(m => m.RecipientId == userId && !m.IsDraft && m.ReadAt == null && !m.RecipientDeleted)
            .CountAsync(ct);
    }

    public async Task<PrivateMessage?> GetMessageAsync(int id, int userId, CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        return await _db.PrivateMessages
            .Where(m => m.Id == id)
            .Where(m => (m.SenderId == userId && !m.SenderDeleted) || (m.RecipientId == userId && !m.RecipientDeleted))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<bool> MarkReadAsync(PrivateMessage message, int userId, CancellationToken ct = default)
    {
        if (message.RecipientId != userId) return false;
        if (message.ReadAt != null) return true;
        message.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(PrivateMessage message, int userId, CancellationToken ct = default)
    {
        if (message == null) return false;

        if (message.SenderId == userId)
        {
            if (message.SenderDeleted) return true;
            message.SenderDeleted = true;
        }
        else if (message.RecipientId == userId)
        {
            if (message.RecipientDeleted) return true;
            message.RecipientDeleted = true;
        }
        else
        {
            return false;
        }

        var senderDone = message.SenderId == null || message.SenderDeleted;
        var recipientDone = message.RecipientId == null || message.RecipientDeleted;
        if (senderDone && recipientDone)
        {
            _db.PrivateMessages.Remove(message);
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public record MailSendResult(bool Ok, string? Error, int Recipients)
{
    public static MailSendResult Success(int recipients) => new(true, null, recipients);
    public static MailSendResult Failed(string error) => new(false, error, 0);
}
