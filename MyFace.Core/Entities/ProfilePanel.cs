using System;

namespace MyFace.Core.Entities;

public class ProfilePanel
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public ProfileTemplate TemplateType { get; set; } = ProfileTemplate.Minimal;
    public ProfilePanelType PanelType { get; set; }
    public string Content { get; set; } = string.Empty;
    public string ContentFormat { get; set; } = "markdown";
    public int Position { get; set; }
    public bool IsVisible { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? LastEditedByUserId { get; set; }
    public string? ValidationMessage { get; set; }
    public bool RequiresModeration { get; set; }

    public User? User { get; set; }
    public User? LastEditedByUser { get; set; }
}
