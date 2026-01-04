using System;
using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Services;

public class ControlPanelAuditService
{
    private readonly ApplicationDbContext _context;

    public ControlPanelAuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(int? userId, string? username, string role, string action, string? target = null, string? details = null, CancellationToken ct = default)
    {
        var entry = new ControlPanelAuditEntry
        {
            ActorUserId = userId,
            ActorUsername = string.IsNullOrWhiteSpace(username) ? null : username,
            ActorRole = string.IsNullOrWhiteSpace(role) ? "Unknown" : role,
            Action = action,
            Target = string.IsNullOrWhiteSpace(target) ? null : target,
            Details = string.IsNullOrWhiteSpace(details) ? null : details,
            CreatedAt = DateTime.UtcNow
        };

        _context.ControlPanelAuditEntries.Add(entry);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ControlPanelAuditEntry>> GetRecentAsync(int take = 250, CancellationToken ct = default)
    {
        var size = Math.Clamp(take, 25, 500);
        return await _context.ControlPanelAuditEntries
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .Take(size)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ControlPanelAuditEntry>> GetRecentForTargetAsync(string target, int take = 100, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return Array.Empty<ControlPanelAuditEntry>();
        }

        var size = Math.Clamp(take, 10, 250);
        return await _context.ControlPanelAuditEntries
            .AsNoTracking()
            .Where(e => e.Target == target)
            .OrderByDescending(e => e.CreatedAt)
            .Take(size)
            .ToListAsync(ct);
    }
}
