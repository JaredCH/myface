namespace MyFace.Core.Entities;

public class PGPVerification
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
    public string ChallengeText { get; set; } = string.Empty;
    public bool Verified { get; set; }
    public DateTime CreatedAt { get; set; }

    public User User { get; set; } = null!;
}
