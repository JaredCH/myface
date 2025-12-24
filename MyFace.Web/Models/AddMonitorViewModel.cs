using System.ComponentModel.DataAnnotations;

namespace MyFace.Web.Models;

public class AddMonitorViewModel
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [RegularExpression(@"^https?://[a-z2-7]{16,56}\.onion.*$", ErrorMessage = "Must be a valid .onion URL")]
    public string OnionUrl { get; set; } = string.Empty;
}
