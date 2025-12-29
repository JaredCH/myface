namespace MyFace.Core.Entities;

public class UploadScanLog
{
    public long Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? UsernameSnapshot { get; set; }
    public bool IsAnonymous { get; set; }
    public string SessionId { get; set; } = string.Empty;
    public string? IpAddressHash { get; set; }
    public string OriginalFileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? StoragePath { get; set; }
    public string ScanEngine { get; set; } = string.Empty;
    public string ScanStatus { get; set; } = string.Empty;
    public string? ThreatName { get; set; }
    public string? ScannerMessage { get; set; }
    public bool Blocked { get; set; }
    public long ProcessingDurationMs { get; set; }
    public long ScanDurationMs { get; set; }
    public string? FailureReason { get; set; }
    public DateTime CreatedAt { get; set; }
}
