using System;

namespace MyFace.Core.Entities;

public class ProfileChatMessage
{
    public int Id { get; set; }
    public int TargetUserId { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public string AuthorRole { get; set; } = "User";
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User? TargetUser { get; set; }
    public User? AuthorUser { get; set; }
}
