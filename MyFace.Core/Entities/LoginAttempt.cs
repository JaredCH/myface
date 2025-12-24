namespace MyFace.Core.Entities;

public class LoginAttempt
{
    public int Id { get; set; }
    public string LoginNameHash { get; set; } = string.Empty; // Hash of login name being attempted
    public DateTime AttemptedAt { get; set; }
    public bool Success { get; set; }
    public string IpAddressHash { get; set; } = string.Empty; // Optional: Hash of IP for additional tracking
}
