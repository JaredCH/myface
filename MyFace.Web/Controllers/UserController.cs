using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFace.Core.Entities;
using MyFace.Services;
using MyFace.Web.Models;
using MyFace.Web.Models.ProfileTemplates;

namespace MyFace.Web.Controllers;

public class UserController : Controller
{
    private readonly UserService _userService;
    private readonly ForumService _forumService;

    public UserController(UserService userService, ForumService forumService)
    {
        _userService = userService;
        _forumService = forumService;
    }

    [HttpGet("/user/templates")]
    public IActionResult TemplateGallery()
    {
        var model = TemplateShowcaseFactory.CreateDefaults();
        ViewBag.HideSidebars = true;
        return View("TemplateGallery", model);
    }

    [HttpGet("/user/{username}")]
    public IActionResult Index(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return RedirectToAction("ViewProfile", "ProfileDisplay");
        }

        var destination = Url.Action("ViewProfile", "ProfileDisplay", new { username }) ?? $"/u/{username}";
        return RedirectPermanent(destination);
    }

    [HttpGet("/user/{username}/activity")]
    public async Task<IActionResult> Activity(string username, string? q, DateTime? start, DateTime? end, string? sort)
    {
        var user = await _userService.GetByUsernameAsync(username);
        if (user == null)
        {
            return NotFound();
        }

        var activity = await _forumService.GetUserActivityAsync(user.Id, q, start, end, sort);
        var isSelf = User.Identity?.IsAuthenticated == true && User.Identity.Name == username;
        var normalizedSort = string.IsNullOrWhiteSpace(sort) ? "newest" : sort.Trim().ToLower();

        var model = new UserActivityViewModel
        {
            Username = user.Username,
            Items = activity.Select(a => new UserActivityItemViewModel
            {
                Type = a.Type,
                Title = a.Title,
                Content = a.Content,
                CreatedAt = a.CreatedAt,
                ThreadId = a.ThreadId,
                PostId = a.PostId,
                NewsId = a.NewsId
            }).ToList(),
            Query = q,
            Start = start,
            End = end,
            Sort = normalizedSort,
            IsSelf = isSelf
        };
        return View("Activity", model);
    }

    [HttpGet("/user/{username}/reviews")]
    public async Task<IActionResult> Reviews(string username, int page = 1)
    {
        var user = await _userService.GetByUsernameAsync(username);
        if (user == null)
        {
            return NotFound();
        }

        var viewer = await GetViewerAsync();
        var model = await BuildReviewsViewModel(user, page, viewer, null);
        ViewBag.HideSidebars = true;
        return View("Reviews", model);
    }

    [Authorize]
    [HttpPost("/user/{username}/reviews")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SubmitReview(string username, ReviewFormModel form, int page = 1)
    {
        var user = await _userService.GetByUsernameAsync(username);
        if (user == null)
        {
            return NotFound();
        }

        var viewer = await GetViewerAsync();
        if (viewer == null)
        {
            return RedirectToAction("Reviews", new { username, page });
        }

        if (viewer.Id == user.Id)
        {
            TempData["ReviewError"] = "You cannot review yourself.";
            return RedirectToAction("Reviews", new { username, page });
        }

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildReviewsViewModel(user, page, viewer, form);
            ViewBag.HideSidebars = true;
            return View("Reviews", invalidModel);
        }

        await _userService.UpsertReviewAsync(user.Id, viewer.Id,
            form.CommunicationScore, form.ShippingScore, form.QualityScore, form.OverallScore, form.Comment);

        TempData["ReviewSuccess"] = "Review saved.";
        return RedirectToAction("Reviews", new { username, page });
    }

    [HttpGet("/user/news/{id}")]
    public async Task<IActionResult> News(int id)
    {
        var news = await _userService.GetNewsByIdAsync(id);
        if (news == null) return NotFound();
        return View(news);
    }

    private static (string Background, string Font, string Accent, string Border, string ButtonBg, string ButtonText, string ButtonBorder)? GetThemePreset(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        if (string.Equals(key, "__current", StringComparison.OrdinalIgnoreCase)) return null;

        return key.ToLowerInvariant() switch
        {
            "classic-light" => ("#f6f7fb", "#1f2933", "#2f6fe4", "#d9dee7", "#2f6fe4", "#ffffff", "#2f6fe4"),
            "midnight" => ("#0f172a", "#e5e7eb", "#8b5cf6", "#1f2937", "#8b5cf6", "#ffffff", "#8b5cf6"),
            "forest" => ("#0f2214", "#e3f4e3", "#34d399", "#14532d", "#22c55e", "#0b1f12", "#22c55e"),
            "sunrise" => ("#2b1b12", "#fef3c7", "#f59e0b", "#7c2d12", "#f59e0b", "#2b1b12", "#f59e0b"),
            "ocean" => ("#0b1f2a", "#e0f2fe", "#06b6d4", "#0c4a6e", "#06b6d4", "#082f49", "#06b6d4"),
            "ember" => ("#111827", "#e5e7eb", "#f97316", "#1f2937", "#f97316", "#0b0f19", "#f97316"),
            "pastel" => ("#f5f3ff", "#312e81", "#a5b4fc", "#cbd5e1", "#a5b4fc", "#0b1220", "#a5b4fc"),
            "slate" => ("#0f172a", "#e2e8f0", "#38bdf8", "#1f2937", "#38bdf8", "#0b1220", "#38bdf8"),
            "mono" => ("#0b0b0f", "#f8fafc", "#ffffff", "#1f1f24", "#ffffff", "#0b0b0f", "#ffffff"),
            _ => null
        };
    }

    private static (string Background, string Font, string Border)? GetTonePalette(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        return key.ToLowerInvariant() switch
        {
            "bg-light" => ("#f1f5f9", "#0f172a", "#cbd5e1"),
            "bg-dim" => ("#e2e8f0", "#0f172a", "#94a3b8"),
            "bg-dark" => ("#0f172a", "#e5e7eb", "#1f2937"),
            _ => null
        };
    }

    private static (string Accent, string ButtonBg, string ButtonText, string ButtonBorder)? GetAccentPalette(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        return key.ToLowerInvariant() switch
        {
            "acc-blue" => ("#3b82f6", "#1d4ed8", "#e2e8f0", "#1d4ed8"),
            "acc-pink" => ("#ec4899", "#be185d", "#ffffff", "#be185d"),
            "acc-gold" => ("#f59e0b", "#c2410c", "#fff7ed", "#c2410c"),
            "acc-teal" => ("#14b8a6", "#0d9488", "#e0f2f1", "#0d9488"),
            _ => null
        };
    }

    private async Task<User?> GetViewerAsync()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(idValue) || !int.TryParse(idValue, out var viewerId))
        {
            return null;
        }

        return await _userService.GetByIdAsync(viewerId);
    }

    private async Task<UserReviewsPageViewModel> BuildReviewsViewModel(User targetUser, int page, User? viewer, ReviewFormModel? overrideForm)
    {
        const int pageSize = 10;
        var currentPage = page < 1 ? 1 : page;
        var skip = (currentPage - 1) * pageSize;

        var summaryResult = await _userService.GetUserReviewSummaryAsync(targetUser.Id, 0);
        var reviews = await _userService.GetUserReviewsAsync(targetUser.Id, skip, pageSize);

        UserReview? existingReview = null;
        if (viewer != null && viewer.Id != targetUser.Id)
        {
            existingReview = await _userService.GetExistingReviewAsync(targetUser.Id, viewer.Id);
        }

        var formModel = overrideForm ?? (existingReview != null
            ? new ReviewFormModel
            {
                CommunicationScore = existingReview.CommunicationScore,
                ShippingScore = existingReview.ShippingScore,
                QualityScore = existingReview.QualityScore,
                OverallScore = existingReview.OverallScore,
                Comment = existingReview.Comment
            }
            : new ReviewFormModel());

        return new UserReviewsPageViewModel
        {
            TargetUser = targetUser,
            Summary = MapReviewSummary(summaryResult),
            Reviews = reviews.Select(r => ToReviewDisplay(r, viewer?.Id)).ToList(),
            Form = formModel,
            CanSubmit = viewer != null && viewer.Id != targetUser.Id,
            HasExistingReview = existingReview != null,
            ViewerIsOwner = viewer?.Id == targetUser.Id,
            CurrentPage = currentPage,
            HasMorePages = reviews.Count == pageSize
        };
    }

    private static ReviewSummaryViewModel MapReviewSummary(UserReviewSummaryResult summary)
    {
        if (summary.TotalReviews == 0)
        {
            return ReviewSummaryViewModel.Empty;
        }

        var average = Math.Round(summary.AverageScore, 1);
        var positivePercent = summary.TotalReviews == 0
            ? 0
            : Math.Round(summary.PositiveReviews / (double)summary.TotalReviews * 100, 1);

        return new ReviewSummaryViewModel
        {
            TotalReviews = summary.TotalReviews,
            AverageScore = average,
            PositivePercent = positivePercent,
            PositiveReviews = summary.PositiveReviews
        };
    }

    private static UserReviewDisplayModel ToReviewDisplay(UserReview review, int? viewerId)
    {
        var reviewerUsername = review.ReviewerUser?.Username;
        var reviewerName = string.IsNullOrWhiteSpace(reviewerUsername)
            ? "anonymous"
            : reviewerUsername!;

        return new UserReviewDisplayModel
        {
            ReviewerUsername = reviewerName,
            CreatedAt = review.CreatedAt,
            CommunicationScore = review.CommunicationScore,
            ShippingScore = review.ShippingScore,
            QualityScore = review.QualityScore,
            OverallScore = review.OverallScore,
            Comment = review.Comment,
            IsViewer = viewerId.HasValue && review.ReviewerUserId == viewerId
        };
    }

}
