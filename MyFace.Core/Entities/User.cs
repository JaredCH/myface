namespace MyFace.Core.Entities;

public class User
{
    public int Id { get; set; }
    public string LoginName { get; set; } = string.Empty; // Private - used for authentication only
    public string Username { get; set; } = string.Empty; // Public - displayed on site
    public string PasswordHash { get; set; } = string.Empty;
    public string? PgpPublicKey { get; set; }
    public string Role { get; set; } = "User"; // User, Moderator, Admin
    public DateTime? SuspendedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool IsActive { get; set; } = true;

    // Profile Customization
    public string FontColor { get; set; } = "#e5e7eb"; // Default light gray
    public string FontFamily { get; set; } = "system-ui, -apple-system, sans-serif";
    public string AboutMe { get; set; } = string.Empty;

    // Navigation properties
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<PGPVerification> PGPVerifications { get; set; } = new List<PGPVerification>();
    public ICollection<UserContact> Contacts { get; set; } = new List<UserContact>();
    public ICollection<UserNews> News { get; set; } = new List<UserNews>();
}
