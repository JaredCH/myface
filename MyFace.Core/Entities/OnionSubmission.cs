namespace MyFace.Core.Entities;

public class OnionSubmission
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OnionUrl { get; set; } = string.Empty;
    public string? PgpSignedMessage { get; set; }
    public string? PgpFingerprint { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Denied
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string? SubmittedByUsername { get; set; }
    public int? SubmittedByUserId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedByUsername { get; set; }
    public int? ReviewedByUserId { get; set; }
    public string? ReviewNotes { get; set; }
}
