namespace MyFace.Core.Entities;

public class Vote
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public int UserId { get; set; }
    public bool IsUpvote { get; set; }
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Post Post { get; set; } = null!;
    public User User { get; set; } = null!;
}
