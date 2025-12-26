using System;
using System.Collections.Generic;

namespace MyFace.Data.TempModels;

public partial class UserNews
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public bool ApplyTheme { get; set; }

    public virtual User User { get; set; } = null!;
}
