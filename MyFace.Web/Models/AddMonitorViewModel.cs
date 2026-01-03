using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace MyFace.Web.Models;

public class AddMonitorViewModel : IValidatableObject
{
    [Required]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(200)]
    public string? OnionUrl { get; set; }

    [Display(Name = "PGP Signed Messages")]
    public string? PgpSignedMessages { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (string.IsNullOrWhiteSpace(OnionUrl) && string.IsNullOrWhiteSpace(PgpSignedMessages))
        {
            yield return new ValidationResult(
                "Provide either a direct onion URL or at least one signed message.",
                new[] { nameof(OnionUrl), nameof(PgpSignedMessages) });
        }

        if (!string.IsNullOrWhiteSpace(OnionUrl) && !OnionUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !OnionUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            yield return new ValidationResult("Direct onion links must start with http:// or https://.", new[] { nameof(OnionUrl) });
        }
    }
}
