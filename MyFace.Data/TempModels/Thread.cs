using System;
using System.Collections.Generic;

namespace MyFace.Data.TempModels;

public partial class Thread
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public int? UserId { get; set; }

    public bool IsAnonymous { get; set; }

    public bool IsLocked { get; set; }

    public bool IsPinned { get; set; }

    public string Category { get; set; } = null!;

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    public virtual User? User { get; set; }
}
