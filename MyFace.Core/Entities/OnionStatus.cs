namespace MyFace.Core.Entities;

public class OnionStatus
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OnionUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty; // e.g., Online, Offline, Error
    public DateTime? LastChecked { get; set; }
    public double? ResponseTime { get; set; } // milliseconds
    public int ReachableAttempts { get; set; }
    public int TotalAttempts { get; set; }
    public double? AverageLatency { get; set; }
    public int ClickCount { get; set; }
}
