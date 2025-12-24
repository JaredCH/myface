namespace MyFace.Core.Entities;

public class Activity
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string ActivityType { get; set; } = string.Empty; // "post", "comment", "upvote", "downvote", "thread_upvote", "thread_downvote"
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public User? User { get; set; }
}
