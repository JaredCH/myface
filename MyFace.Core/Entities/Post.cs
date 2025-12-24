namespace MyFace.Core.Entities;

public class Post
{
    public int Id { get; set; }
    public int ThreadId { get; set; }
    public int? UserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? EditedAt { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsDeleted { get; set; }
    public bool IsSticky { get; set; } = false;
    
    // Navigation properties
    public Thread Thread { get; set; } = null!;
    public User? User { get; set; }
    public ICollection<Vote> Votes { get; set; } = new List<Vote>();
}
