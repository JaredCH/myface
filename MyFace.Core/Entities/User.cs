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

    // Profile Customization (MySpace-style)
    public string FontColor { get; set; } = "#e5e7eb"; // Default light gray
    public string FontFamily { get; set; } = "system-ui, -apple-system, sans-serif";
    public int FontSize { get; set; } = 14; // Base font size in pixels
    public string BackgroundColor { get; set; } = "#0f172a"; // Profile background color
    public string AccentColor { get; set; } = "#3b82f6"; // Links and accents
    public string BorderColor { get; set; } = "#334155"; // Card borders
    public string ButtonBackgroundColor { get; set; } = "#0ea5e9"; // Buttons
    public string ButtonTextColor { get; set; } = "#ffffff";
    public string ButtonBorderColor { get; set; } = "#0ea5e9";
    public string ProfileLayout { get; set; } = "default"; // Layout style: default, compact, expanded
    public string CustomCSS { get; set; } = string.Empty; // Advanced: custom CSS (sanitized)
    public string AboutMe { get; set; } = string.Empty;

    // Vendor-facing profile sections (optional)
    public string VendorShopDescription { get; set; } = string.Empty;
    public string VendorPolicies { get; set; } = string.Empty;
    public string VendorPayments { get; set; } = string.Empty;
    public string VendorExternalReferences { get; set; } = string.Empty;

    // Username change tracking
    public bool MustChangeUsername { get; set; } = false; // Set when admin/mod changes username
    public int? UsernameChangedByAdminId { get; set; } // Track who changed it

    // Vote statistics (calculated from Votes collection)
    public int CommentUpvotes { get; set; } = 0;
    public int CommentDownvotes { get; set; } = 0;
    public int PostUpvotes { get; set; } = 0;
    public int PostDownvotes { get; set; } = 0;

    // Navigation properties
    public ICollection<Thread> Threads { get; set; } = new List<Thread>();
    public ICollection<Post> Posts { get; set; } = new List<Post>();
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
    public ICollection<PGPVerification> PGPVerifications { get; set; } = new List<PGPVerification>();
    public ICollection<UserContact> Contacts { get; set; } = new List<UserContact>();
    public ICollection<UserNews> News { get; set; } = new List<UserNews>();
    public ICollection<UserReview> ReviewsAuthored { get; set; } = new List<UserReview>();
    public ICollection<UserReview> ReviewsReceived { get; set; } = new List<UserReview>();
    public ICollection<ProfileChatMessage> ProfileChatMessagesAuthored { get; set; } = new List<ProfileChatMessage>();
    public ICollection<ProfileChatMessage> ProfileChatWall { get; set; } = new List<ProfileChatMessage>();
}
