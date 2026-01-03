using System;

namespace MyFace.Core.Entities;

public class OnionProof
{
    public int Id { get; set; }
    public int OnionStatusId { get; set; }
    public string ProofType { get; set; } = "pgp-signed";
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public OnionStatus? OnionStatus { get; set; }
}
