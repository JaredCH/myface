using System.ComponentModel.DataAnnotations;

namespace MyFace.Web.Models;

public class RequestPgpChallengeViewModel
{
    [Required]
    [StringLength(64)]
    public string Fingerprint { get; set; } = string.Empty;
}
