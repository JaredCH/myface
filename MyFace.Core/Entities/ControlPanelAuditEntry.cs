namespace MyFace.Core.Entities;

public class ControlPanelAuditEntry
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? ActorUserId { get; set; }
    public string? ActorUsername { get; set; }
    public string ActorRole { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Target { get; set; }
    public string? Details { get; set; }
}
