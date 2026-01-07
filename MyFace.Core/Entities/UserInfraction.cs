namespace MyFace.Core.Entities;

public class UserInfraction
{
    public int Id { get; set; }
    
    /// <summary>
    /// User who triggered the infraction
    /// </summary>
    public int UserId { get; set; }
    public User? User { get; set; }
    
    /// <summary>
    /// The content ID (Thread, Post, or ChatMessage ID)
    /// </summary>
    public int? ContentId { get; set; }
    
    /// <summary>
    /// Type of content (Thread, Comment, Chat)
    /// </summary>
    public string ContentType { get; set; } = string.Empty;
    
    /// <summary>
    /// The word/pattern that was matched
    /// </summary>
    public string MatchedPattern { get; set; } = string.Empty;
    
    /// <summary>
    /// Reference to the word list entry that triggered this
    /// </summary>
    public int? WordListEntryId { get; set; }
    public WordListEntry? WordListEntry { get; set; }
    
    /// <summary>
    /// Action taken (mute, replace, etc.)
    /// </summary>
    public string ActionTaken { get; set; } = string.Empty;
    
    /// <summary>
    /// When this infraction occurred
    /// </summary>
    public DateTime OccurredAt { get; set; }
    
    /// <summary>
    /// When the mute expires (null if no mute or already expired)
    /// </summary>
    public DateTime? MuteExpiresAt { get; set; }
    
    /// <summary>
    /// Session fingerprint (cookie-based session ID)
    /// </summary>
    public string? SessionFingerprint { get; set; }
    
    /// <summary>
    /// Tor circuit fingerprint heuristic (UA + headers hash)
    /// </summary>
    public string? TorFingerprint { get; set; }
    
    /// <summary>
    /// Original content before filtering (encrypted/admin-only)
    /// </summary>
    public string? OriginalContent { get; set; }
    
    /// <summary>
    /// Whether this infraction resulted in content being modified
    /// </summary>
    public bool ContentModified { get; set; }
    
    /// <summary>
    /// Whether this infraction is part of an escalation
    /// </summary>
    public bool IsEscalation { get; set; }
    
    /// <summary>
    /// Admin notes (optional, for review)
    /// </summary>
    public string? AdminNotes { get; set; }
}
