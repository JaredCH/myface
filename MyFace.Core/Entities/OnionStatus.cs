using System.Collections.Generic;

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
    
    // Link Rollup fields
    public string? CanonicalName { get; set; } // Title Case display name
    public string? NormalizedKey { get; set; } // Lowercase comparison key
    public int? ParentId { get; set; } // FK to parent service (null = primary)
    public bool IsMirror { get; set; } = false; // True if this is a mirror URL
    public int MirrorPriority { get; set; } = 0; // Display order (0 = primary)

    public ICollection<OnionProof> Proofs { get; set; } = new List<OnionProof>();
    public OnionStatus? Parent { get; set; } // Navigation property to parent
    public ICollection<OnionStatus> Mirrors { get; set; } = new List<OnionStatus>(); // Child mirrors
}
