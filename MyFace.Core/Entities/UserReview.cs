namespace MyFace.Core.Entities;

public class UserReview
{
    public int Id { get; set; }
    public int TargetUserId { get; set; }
    public int ReviewerUserId { get; set; }
    public int CommunicationScore { get; set; }
    public int ShippingScore { get; set; }
    public int QualityScore { get; set; }
    public int OverallScore { get; set; }
    public string Comment { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    public User TargetUser { get; set; } = null!;
    public User ReviewerUser { get; set; } = null!;
}
