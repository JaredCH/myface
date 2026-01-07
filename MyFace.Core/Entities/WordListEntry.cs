namespace MyFace.Core.Entities;

public enum WordMatchType
{
    Exact,          // Match exact string
    WordBoundary,   // Match with word boundaries (\b)
    Regex           // Full regex pattern
}

public enum WordActionType
{
    InfractionAndMute,  // Log infraction and optionally mute user
    WordSwapOnly        // Replace word without penalty
}

[Flags]
public enum ContentScope
{
    None = 0,
    Threads = 1,
    Comments = 2,
    Chats = 4,
    All = Threads | Comments | Chats
}

public class WordListEntry
{
    public int Id { get; set; }
    
    /// <summary>
    /// The word or pattern to match (plain text or regex depending on MatchType)
    /// </summary>
    public string WordPattern { get; set; } = string.Empty;
    
    /// <summary>
    /// How to match this pattern
    /// </summary>
    public WordMatchType MatchType { get; set; } = WordMatchType.WordBoundary;
    
    /// <summary>
    /// What action to take when matched
    /// </summary>
    public WordActionType ActionType { get; set; } = WordActionType.InfractionAndMute;
    
    /// <summary>
    /// Duration of mute in hours (null for no mute, or 12, 24, 72)
    /// </summary>
    public int? MuteDurationHours { get; set; }
    
    /// <summary>
    /// Replacement text (null to block entirely, or replacement string)
    /// </summary>
    public string? ReplacementText { get; set; }
    
    /// <summary>
    /// Whether matching should be case sensitive
    /// </summary>
    public bool CaseSensitive { get; set; } = false;
    
    /// <summary>
    /// Where this filter applies (bitwise flags)
    /// </summary>
    public ContentScope AppliesTo { get; set; } = ContentScope.All;
    
    /// <summary>
    /// Who created this entry
    /// </summary>
    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
    
    /// <summary>
    /// When this entry was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// Whether this filter is currently enabled
    /// </summary>
    public bool Enabled { get; set; } = true;
    
    /// <summary>
    /// Optional description/notes
    /// </summary>
    public string? Notes { get; set; }
}
