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
    [StringLength(200)]
    [RegularExpression(@"^https?://.+", ErrorMessage = "Must be a valid http(s) URL")]
    public string OnionUrl { get; set; } = string.Empty;
}
