namespace MyFace.Core.Entities;

public class Thread
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int? UserId { get; set; }
    public bool IsAnonymous { get; set; }
    public bool IsLocked { get; set; }
    public bool IsPinned { get; set; }
    
    // Navigation properties
    public User? User { get; set; }
    public ICollection<Post> Posts { get; set; } = new List<Post>();
}
