using System.ComponentModel.DataAnnotations;

namespace MyFace.Web.Models;

public class EditProfileViewModel
{
    public string AboutMe { get; set; } = string.Empty;
    public string FontColor { get; set; } = "#e5e7eb";
    public string FontFamily { get; set; } = "system-ui, -apple-system, sans-serif";
}

public class AddContactViewModel
{
    [Required]
    [MaxLength(50)]
    public string ServiceName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string AccountId { get; set; } = string.Empty;
}

public class AddNewsViewModel
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;
    
    [Required]
    public string Content { get; set; } = string.Empty;

    public bool ApplyTheme { get; set; }
}
