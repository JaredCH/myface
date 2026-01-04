namespace MyFace.Core.Entities;

public class PrivateMessage
{
    public int Id { get; set; }
    public int? SenderId { get; set; }
    public int? RecipientId { get; set; }
    public string SenderUsernameSnapshot { get; set; } = string.Empty;
    public string RecipientUsernameSnapshot { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsDraft { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool SenderDeleted { get; set; }
    public bool RecipientDeleted { get; set; }

    public User? Sender { get; set; }
    public User? Recipient { get; set; }
}
