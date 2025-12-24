using MyFace.Core.Entities;

namespace MyFace.Web.Models;

public class MailIndexViewModel
{
    public User CurrentUser { get; set; } = null!;
    public bool IsVerified { get; set; }
    public bool IsModeratorOrAdmin { get; set; }
    public string ActiveTab { get; set; } = "inbox";
    public List<MailItemViewModel> Inbox { get; set; } = new();
    public List<MailItemViewModel> Outbox { get; set; } = new();
    public List<MailItemViewModel> Drafts { get; set; } = new();
    public MailComposeModel Compose { get; set; } = new();
}

public class MailItemViewModel
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool IsDraft { get; set; }
}

public class MailComposeModel
{
    public string To { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
}

public class MailViewMessageModel
{
    public int Id { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public bool CanSendDraft { get; set; }
    public bool IsDraft { get; set; }
}
