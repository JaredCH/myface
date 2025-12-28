using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace MyFace.Web.Models;

public class ThreadTestReplyViewModel
{
    [Required]
    public int ThreadId { get; set; }

    [Required]
    [StringLength(10000, MinimumLength = 1)]
    public string Content { get; set; } = string.Empty;

    public bool PostAsAnonymous { get; set; }

    public List<IFormFile> Images { get; set; } = new();
}
