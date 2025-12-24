using System;
using System.Collections.Generic;

namespace MyFace.Data.TempModels;

public partial class OnionStatus
{
    public int Id { get; set; }

    public string OnionUrl { get; set; } = null!;

    public string Status { get; set; } = null!;

    public DateTime? LastChecked { get; set; }

    public double? ResponseTime { get; set; }

    public string Name { get; set; } = null!;

    public string Description { get; set; } = null!;

    public int ReachableAttempts { get; set; }

    public int TotalAttempts { get; set; }

    public double? AverageLatency { get; set; }
}
