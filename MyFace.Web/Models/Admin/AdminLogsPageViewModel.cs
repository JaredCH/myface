using System;
using System.Collections.Generic;

namespace MyFace.Web.Models.Admin;

public class AdminLogsPageViewModel
{
    public UploadLogsViewModel Uploads { get; init; } = new();
    public IReadOnlyList<PostLogEntryModel> PostLog { get; init; } = Array.Empty<PostLogEntryModel>();
    public IReadOnlyList<VisitLogEntryModel> VisitLog { get; init; } = Array.Empty<VisitLogEntryModel>();
    public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;
    public int AutoRefreshSeconds { get; init; } = 30;
}

public record PostLogEntryModel(
    int PostId,
    int ThreadId,
    string ThreadTitle,
    DateTime CreatedAtUtc,
    string ActorLabel,
    bool IsAnonymous,
    bool IsDeleted,
    int ReportCount,
    bool WasModerated,
    string Snippet,
    int ImageCount);

public record VisitLogEntryModel(
    int Id,
    DateTime VisitedAtUtc,
    string Path,
    string EventLabel,
    string ActorLabel,
    string SessionLabel,
    string? UserAgent,
    bool IsRecent);
