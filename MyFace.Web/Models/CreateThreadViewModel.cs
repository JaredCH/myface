using System.ComponentModel.DataAnnotations;

namespace MyFace.Web.Models;

public class CreateThreadViewModel
{
    [Required]
    [StringLength(200, MinimumLength = 3)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(10000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    public bool PostAsAnonymous { get; set; }
}
