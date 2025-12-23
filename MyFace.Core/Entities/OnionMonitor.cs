namespace MyFace.Core.Entities;

public class OnionMonitor
{
    public int Id { get; set; }
    public string OnionUrl { get; set; } = string.Empty;
    public string? FriendlyName { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastChecked { get; set; }
    public DateTime? LastOnline { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? Notes { get; set; }
}
