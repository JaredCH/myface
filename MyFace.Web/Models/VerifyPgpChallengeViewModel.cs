using System.ComponentModel.DataAnnotations;

namespace MyFace.Web.Models;

public class VerifyPgpChallengeViewModel
{
    [Required]
    [DataType(DataType.MultilineText)]
    public string Response { get; set; } = string.Empty; // ASCII-armored signature over the challenge text
}
