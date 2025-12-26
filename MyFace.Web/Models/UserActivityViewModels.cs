using System;
using System.Collections.Generic;

namespace MyFace.Web.Models;

public class UserActivityItemViewModel
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Content { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? ThreadId { get; set; }
    public int? PostId { get; set; }
    public int? NewsId { get; set; }
}

public class UserActivityViewModel
{
    public string Username { get; set; } = string.Empty;
    public List<UserActivityItemViewModel> Items { get; set; } = new();
    public string? Query { get; set; }
    public DateTime? Start { get; set; }
    public DateTime? End { get; set; }
    public string Sort { get; set; } = "newest";
    public bool IsSelf { get; set; }
}
