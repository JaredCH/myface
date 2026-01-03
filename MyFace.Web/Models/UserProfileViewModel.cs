using System;
using System.Collections.Generic;
using MyFace.Core.Entities;
using MyFace.Web.Layout;

namespace MyFace.Web.Models;

public class UserProfileViewModel
{
    public required User User { get; init; }
    public IReadOnlyList<Post> Posts { get; init; } = Array.Empty<Post>();
    public IReadOnlyList<UserNews> LatestNews { get; init; } = Array.Empty<UserNews>();
    public IReadOnlyList<SectionRenderSlot> Sections { get; init; } = Array.Empty<SectionRenderSlot>();
    public Dictionary<string, SectionLayoutState> LayoutState { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public bool IsOwner { get; init; }
    public bool IsAdminOrMod { get; init; }
    public bool IsAdmin { get; init; }
    public int CurrentPage { get; init; }
    public bool HasMorePages { get; init; }
    public IReadOnlyList<PaymentRow> Payments { get; init; } = Array.Empty<PaymentRow>();
    public IReadOnlyList<ReferenceRow> ExternalReferences { get; init; } = Array.Empty<ReferenceRow>();
    public ReviewSummaryViewModel ReviewSummary { get; init; } = ReviewSummaryViewModel.Empty;
    public IReadOnlyList<UserReviewDisplayModel> RecentReviews { get; init; } = Array.Empty<UserReviewDisplayModel>();
    public IReadOnlyList<ChatWindowMessageViewModel> ChatMessages { get; init; } = Array.Empty<ChatWindowMessageViewModel>();
    public bool CanLeaveReview { get; init; }
    public bool ViewerAuthenticated { get; init; }
    public bool ViewerHasReview { get; init; }
}
