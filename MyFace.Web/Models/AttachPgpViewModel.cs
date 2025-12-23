using System.ComponentModel.DataAnnotations;

namespace MyFace.Web.Models;

public class AttachPgpViewModel
{
    [Required]
    [DataType(DataType.MultilineText)]
    public string PgpPublicKey { get; set; } = string.Empty;
}
