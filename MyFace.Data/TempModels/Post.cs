using System;
using System.Collections.Generic;

namespace MyFace.Data.TempModels;

public partial class Post
{
    public int Id { get; set; }

    public int ThreadId { get; set; }

    public int? UserId { get; set; }

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? EditedAt { get; set; }

    public bool IsAnonymous { get; set; }

    public bool IsDeleted { get; set; }

    public bool IsSticky { get; set; }

    public virtual Thread Thread { get; set; } = null!;

    public virtual User? User { get; set; }

    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
