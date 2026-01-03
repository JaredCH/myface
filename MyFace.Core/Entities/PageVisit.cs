namespace MyFace.Core.Entities;

public class PageVisit
{
    public int Id { get; set; }
    public DateTime VisitedAt { get; set; }
    public string? IpHash { get; set; } // Optional: hashed IP for basic tracking (privacy-focused)
    public string? UserAgent { get; set; }
    public string Path { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string? UsernameSnapshot { get; set; }
    public string? SessionFingerprint { get; set; }
    public string EventType { get; set; } = "page-load";
}
