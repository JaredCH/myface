using System;
using System.Collections.Generic;

namespace MyFace.Data.TempModels;

public partial class PageVisit
{
    public int Id { get; set; }

    public DateTime VisitedAt { get; set; }

    public string? IpHash { get; set; }

    public string? UserAgent { get; set; }

    public string Path { get; set; } = null!;
}
