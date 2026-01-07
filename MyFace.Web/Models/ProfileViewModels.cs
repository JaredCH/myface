using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MyFace.Core.Entities;

namespace MyFace.Web.Models;

public class ReviewSummaryViewModel
{
    public static readonly ReviewSummaryViewModel Empty = new();

    public int TotalReviews { get; init; }
    public double AverageScore { get; init; }
    public double PositivePercent { get; init; }
    public int PositiveReviews { get; init; }
}

public class UserReviewDisplayModel
{
    public string ReviewerUsername { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public int CommunicationScore { get; init; }
    public int ShippingScore { get; init; }
    public int QualityScore { get; init; }
    public int OverallScore { get; init; }
    public string Comment { get; init; } = string.Empty;
    public bool IsViewer { get; init; }
}
public class ReviewFormModel
{
    [Range(1, 5)]
    public int CommunicationScore { get; set; } = 5;

    [Range(1, 5)]
    public int ShippingScore { get; set; } = 5;

    [Range(1, 5)]
    public int QualityScore { get; set; } = 5;

    [Range(1, 5)]
    public int OverallScore { get; set; } = 5;

    [Required]
    [MaxLength(2000)]
    public string Comment { get; set; } = string.Empty;
}

public class UserReviewsPageViewModel
{
    public required User TargetUser { get; init; }
    public ReviewSummaryViewModel Summary { get; init; } = ReviewSummaryViewModel.Empty;
    public IReadOnlyList<UserReviewDisplayModel> Reviews { get; init; } = Array.Empty<UserReviewDisplayModel>();
    public ReviewFormModel Form { get; init; } = new();
    public bool CanSubmit { get; init; }
    public bool HasExistingReview { get; init; }
    public bool ViewerIsOwner { get; init; }
    public int CurrentPage { get; init; }
    public bool HasMorePages { get; init; }
}
