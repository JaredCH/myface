namespace MyFace.Core.Entities;

public class UserNews
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    
    public User User { get; set; } = null!;
}
