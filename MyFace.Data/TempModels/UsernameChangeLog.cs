using System;
using System.Collections.Generic;

namespace MyFace.Data.TempModels;

public partial class UsernameChangeLog
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string OldUsername { get; set; } = null!;

    public string NewUsername { get; set; } = null!;

    public int? ChangedByUserId { get; set; }

    public DateTime ChangedAt { get; set; }

    public bool UserNotified { get; set; }

    public string? AdminNote { get; set; }

    public virtual User? ChangedByUser { get; set; }

    public virtual User User { get; set; } = null!;
}
