using System;
using System.Collections.Generic;

namespace MyFace.Data.TempModels;

public partial class User
{
    public int Id { get; set; }

    public string Username { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? PgpPublicKey { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? LastSeenAt { get; set; }

    public bool IsActive { get; set; }

    public string AboutMe { get; set; } = null!;

    public string FontColor { get; set; } = null!;

    public string FontFamily { get; set; } = null!;

    public string Role { get; set; } = null!;

    public DateTime? SuspendedUntil { get; set; }

    public string? LoginName { get; set; }

    public string AccentColor { get; set; } = null!;

    public string BackgroundColor { get; set; } = null!;

    public string BorderColor { get; set; } = null!;

    public string CustomCss { get; set; } = null!;

    public int FontSize { get; set; }

    public string ProfileLayout { get; set; } = null!;

    public bool MustChangeUsername { get; set; }

    public int? UsernameChangedByAdminId { get; set; }

    public virtual ICollection<Pgpverification> Pgpverifications { get; set; } = new List<Pgpverification>();

    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();

    public virtual ICollection<Thread> Threads { get; set; } = new List<Thread>();

    public virtual ICollection<UserContact> UserContacts { get; set; } = new List<UserContact>();

    public virtual ICollection<UserNews> UserNews { get; set; } = new List<UserNews>();

    public virtual ICollection<UsernameChangeLog> UsernameChangeLogChangedByUsers { get; set; } = new List<UsernameChangeLog>();

    public virtual ICollection<UsernameChangeLog> UsernameChangeLogUsers { get; set; } = new List<UsernameChangeLog>();

    public virtual ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
