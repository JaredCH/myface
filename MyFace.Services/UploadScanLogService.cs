using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class UploadScanLogService
{
    private readonly ApplicationDbContext _db;

    public UploadScanLogService(ApplicationDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(UploadScanLog entry, CancellationToken ct = default)
    {
        entry.CreatedAt = DateTime.UtcNow;
        _db.UploadScanLogs.Add(entry);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<UploadScanLogPage> GetLogsAsync(UploadScanLogQuery query, int page, int pageSize, CancellationToken ct = default)
    {
        var normalizedPage = Math.Max(1, page);
        var normalizedPageSize = Math.Clamp(pageSize, 1, 200);

        var logsQuery = _db.UploadScanLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            logsQuery = logsQuery.Where(l => l.EventType == query.EventType);
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            logsQuery = logsQuery.Where(l => l.Source == query.Source);
        }

        if (!string.IsNullOrWhiteSpace(query.ScanStatus))
        {
            logsQuery = logsQuery.Where(l => l.ScanStatus == query.ScanStatus);
        }

        if (query.Blocked.HasValue)
        {
            logsQuery = logsQuery.Where(l => l.Blocked == query.Blocked.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Username))
        {
            var like = $"%{query.Username.Trim()}%";
            logsQuery = logsQuery.Where(l => l.UsernameSnapshot != null && EF.Functions.ILike(l.UsernameSnapshot, like));
        }

        if (!string.IsNullOrWhiteSpace(query.Threat))
        {
            var like = $"%{query.Threat.Trim()}%";
            logsQuery = logsQuery.Where(l => l.ThreatName != null && EF.Functions.ILike(l.ThreatName, like));
        }

        if (!string.IsNullOrWhiteSpace(query.SessionId))
        {
            logsQuery = logsQuery.Where(l => l.SessionId == query.SessionId);
        }

        if (query.FromDateUtc.HasValue)
        {
            logsQuery = logsQuery.Where(l => l.CreatedAt >= query.FromDateUtc.Value);
        }

        if (query.ToDateUtc.HasValue)
        {
            logsQuery = logsQuery.Where(l => l.CreatedAt <= query.ToDateUtc.Value);
        }

        var total = await logsQuery.CountAsync(ct);

        var items = await logsQuery
            .OrderByDescending(l => l.CreatedAt)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToListAsync(ct);

        var totalPages = (int)Math.Ceiling(total / (double)normalizedPageSize);

        return new UploadScanLogPage(items, total, normalizedPage, normalizedPageSize, totalPages);
    }
}

public record UploadScanLogQuery
{
    public string? EventType { get; init; }
    public string? Source { get; init; }
    public string? ScanStatus { get; init; }
    public bool? Blocked { get; init; }
    public string? Username { get; init; }
    public string? SessionId { get; init; }
    public string? Threat { get; init; }
    public DateTime? FromDateUtc { get; init; }
    public DateTime? ToDateUtc { get; init; }
}

public record UploadScanLogPage(
    IReadOnlyList<UploadScanLog> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
