namespace MyFace.Core.Entities;

public class ControlSettingHistory
{
    public int Id { get; set; }
    public int? ControlSettingId { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? UpdatedByUserId { get; set; }
    public string? UpdatedByUsername { get; set; }
}
