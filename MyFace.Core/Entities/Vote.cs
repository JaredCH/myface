namespace MyFace.Core.Entities;

public class Vote
{
    public int Id { get; set; }
    public int PostId { get; set; }
    public int? UserId { get; set; }
    public string? SessionId { get; set; }
    public int Value { get; set; } // -1 or +1
    public DateTime CreatedAt { get; set; }
    
    // Navigation properties
    public Post Post { get; set; } = null!;
    public User? User { get; set; }
}
