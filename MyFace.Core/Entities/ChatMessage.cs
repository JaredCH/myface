using System;

namespace MyFace.Core.Entities;

public class ChatMessage
{
    public int Id { get; set; }
    public string Room { get; set; } = string.Empty;
    public int? UserId { get; set; }
    public string UsernameSnapshot { get; set; } = string.Empty;
    public string RoleSnapshot { get; set; } = "User";
    public bool IsVerifiedSnapshot { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}
