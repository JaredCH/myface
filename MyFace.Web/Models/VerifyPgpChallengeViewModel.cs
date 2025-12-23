using System.ComponentModel.DataAnnotations;

namespace MyFace.Web.Models;

public class VerifyPgpChallengeViewModel
{
    [Required]
    public string Response { get; set; } = string.Empty;
}
