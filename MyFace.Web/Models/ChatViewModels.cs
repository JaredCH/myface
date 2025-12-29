using System.Collections.Generic;
using MyFace.Core.Entities;
using MyFace.Web.Services;

namespace MyFace.Web.Models;

public class ChatIndexViewModel
{
    public User? CurrentUser { get; set; }
    public bool PublicCanPost { get; set; }
    public bool VerifiedCanPost { get; set; }
    public bool SigilShortsCanPost { get; set; }
    public bool PublicPaused { get; set; }
    public bool VerifiedPaused { get; set; }
    public bool SigilShortsPaused { get; set; }
}

public class ChatMessagesViewModel
{
    public string Room { get; set; } = string.Empty;
    public string SnapshotHtml { get; set; } = string.Empty;
    public bool Paused { get; set; }
    public bool ShowMessageIds { get; set; }
    public string ViewerUsername { get; set; } = string.Empty;
}

public class ChatViewersViewModel
{
    public IReadOnlyList<ChatViewerPresence> Viewers { get; set; } = System.Array.Empty<ChatViewerPresence>();
}

public class ChatSectionModel
{
    public string Title { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool CanPost { get; set; }
    public bool Paused { get; set; }
    public string FlashInfo { get; set; } = string.Empty;
    public string FlashError { get; set; } = string.Empty;
    public bool IsModerator { get; set; }
    public bool UserCanView { get; set; }
}

public class ChatCaptchaViewModel
{
    public string Room { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ContextHtml { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public int AttemptsRemaining { get; set; }
    public string? Error { get; set; }
}
