namespace MyFace.Core.Entities;

public class UserContact
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string ServiceName { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    
    public User User { get; set; } = null!;
}
