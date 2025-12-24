namespace MyFace.Core.Entities;

public class UsernameChangeLog
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string OldUsername { get; set; } = string.Empty;
    public string NewUsername { get; set; } = string.Empty;
    public string? AdminNote { get; set; } // Reason from admin/mod
    public int? ChangedByUserId { get; set; } // Admin/Mod who made the change
    public DateTime ChangedAt { get; set; }
    public bool UserNotified { get; set; } = false;
    
    public User User { get; set; } = null!;
    public User? ChangedByUser { get; set; }
}
