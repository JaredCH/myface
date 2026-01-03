using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFace.Core.Entities;
using MyFace.Services;
using MyFace.Web.Models;
using MyFace.Web.Layout;

namespace MyFace.Web.Controllers;

public class UserController : Controller
{
    private readonly UserService _userService;
    private readonly ForumService _forumService;
    private readonly ProfileChatService _profileChatService;
    private static readonly JsonSerializerOptions LayoutSerializerOptions = new(JsonSerializerDefaults.Web);

    public UserController(UserService userService, ForumService forumService, ProfileChatService profileChatService)
    {
        _userService = userService;
        _forumService = forumService;
        _profileChatService = profileChatService;
    }

    [HttpGet("/user/{username}")]
    public async Task<IActionResult> Index(string username, int page = 1)
    {
        var user = await _userService.GetByUsernameAsync(username);
        
        if (user == null)
        {
            return NotFound();
        }

        const int pageSize = 25;
        var posts = await _forumService.GetUserPostsAsync(user.Id, (page - 1) * pageSize, pageSize);

        var voteStats = await _forumService.CalculateUserVoteStatsAsync(user.Id);
        user.PostUpvotes = voteStats.ThreadUpvotes;
        user.PostDownvotes = voteStats.ThreadDownvotes;
        user.CommentUpvotes = voteStats.CommentUpvotes;
        user.CommentDownvotes = voteStats.CommentDownvotes;

        var viewer = await GetViewerAsync();
        var isOwner = viewer != null && viewer.Username == username;
        var viewerRole = User.FindFirstValue(ClaimTypes.Role);
        var isAdminOrMod = viewerRole == "Admin" || viewerRole == "Moderator";
        var isAdmin = viewerRole == "Admin";

        var layoutState = ParseSectionLayout(user.ProfileLayout);
        var layoutSlots = ProfileLayoutEngine.ComposeLayout(layoutState);
        var latestNews = (user.News ?? new List<UserNews>())
            .OrderByDescending(n => n.CreatedAt)
            .Take(3)
            .ToList();

        var payments = ProfileStructuredFields.ParsePayments(user.VendorPayments);
        var references = ProfileStructuredFields.ParseReferences(user.VendorExternalReferences);
        var reviewSummaryResult = await _userService.GetUserReviewSummaryAsync(user.Id);
        var reviewSummary = MapReviewSummary(reviewSummaryResult);
        var recentReviews = reviewSummaryResult.RecentReviews
            .Select(r => ToReviewDisplay(r, viewer?.Id))
            .ToList();

        var viewerHasReview = false;
        if (viewer != null && viewer.Id != user.Id)
        {
            viewerHasReview = await _userService.GetExistingReviewAsync(user.Id, viewer.Id) != null;
        }

        var chatMessages = await _profileChatService.GetRecentMessagesAsync(user.Id, 20);
        var chatWindow = chatMessages
            .OrderBy(m => m.CreatedAt)
            .Select(ToChatWindowMessage)
            .ToList();

        var model = new UserProfileViewModel
        {
            User = user,
            Posts = posts,
            LatestNews = latestNews,
            Sections = layoutSlots,
            LayoutState = layoutState,
            IsOwner = isOwner,
            IsAdminOrMod = isAdminOrMod,
            IsAdmin = isAdmin,
            CurrentPage = page,
            HasMorePages = posts.Count == pageSize,
            Payments = payments,
            ExternalReferences = references,
            ReviewSummary = reviewSummary,
            RecentReviews = recentReviews,
            ChatMessages = chatWindow,
            CanLeaveReview = viewer != null && viewer.Id != user.Id,
            ViewerAuthenticated = viewer != null,
            ViewerHasReview = viewerHasReview
        };

        ViewBag.HideSidebars = true;

        return View(model);
    }

    private static Dictionary<string, SectionLayoutState> CloneSectionDefaults()
    {
        return ProfileLayoutEngine.CreateDefaultLayout();
    }

    private static Dictionary<string, SectionLayoutState> ParseSectionLayout(string? payload)
    {
        var layout = CloneSectionDefaults();
        if (string.IsNullOrWhiteSpace(payload))
        {
            return layout;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, SectionLayoutState>>(payload, LayoutSerializerOptions);
            if (parsed == null)
            {
                return layout;
            }

            foreach (var kvp in parsed)
            {
                if (!layout.ContainsKey(kvp.Key))
                {
                    continue;
                }

                var fallback = layout[kvp.Key];
                var source = kvp.Value ?? fallback;
                var placement = ProfileLayoutEngine.NormalizePlacementSelection(kvp.Key, source.Placement, fallback.Placement);
                var enabled = source.Enabled;
                layout[kvp.Key] = fallback with
                {
                    Enabled = enabled,
                    Placement = placement,
                    CustomTitle = NormalizeCustomTitle(source.CustomTitle),
                    TitleAlignment = NormalizeAlignment(source.TitleAlignment, fallback.TitleAlignment),
                    ContentAlignment = NormalizeAlignment(source.ContentAlignment, fallback.ContentAlignment),
                    PanelBackground = NormalizeHexOrNull(source.PanelBackground),
                    HeaderBackground = NormalizeHexOrNull(source.HeaderBackground),
                    HeaderTextColor = NormalizeHexOrNull(source.HeaderTextColor),
                    ContentTextColor = NormalizeHexOrNull(source.ContentTextColor)
                };
            }
        }
        catch
        {
            // Ignore malformed payloads and fall back to defaults.
        }

        return layout;
    }

    private static string SerializeSectionLayout(Dictionary<string, SectionLayoutState> layout)
    {
        return JsonSerializer.Serialize(layout, LayoutSerializerOptions);
    }

    private static void ApplyLayoutSelection(Dictionary<string, SectionLayoutState> layout, string key, bool enabled, string? placement)
    {
        if (!layout.ContainsKey(key))
        {
            return;
        }

        var fallback = layout[key];
        layout[key] = fallback with
        {
            Enabled = enabled,
            Placement = ProfileLayoutEngine.NormalizePlacementSelection(key, placement, fallback.Placement)
        };
    }

    private static void ApplySectionStyleOverrides(Dictionary<string, SectionLayoutState> layout, Dictionary<string, SectionStylePostModel>? overrides)
    {
        if (overrides == null || overrides.Count == 0)
        {
            return;
        }

        foreach (var entry in overrides)
        {
            if (!layout.TryGetValue(entry.Key, out var current) || entry.Value == null)
            {
                continue;
            }

            var style = entry.Value;
            var customTitle = NormalizeCustomTitle(style.CustomTitle);
            var titleAlignment = NormalizeAlignment(style.TitleAlignment, "left");
            var contentAlignment = NormalizeAlignment(style.ContentAlignment, "left");
            var panelColor = style.PanelColorEnabled ? NormalizeHexOrNull(style.PanelColor) : null;
            var headerColor = style.HeaderColorEnabled ? NormalizeHexOrNull(style.HeaderColor) : null;
            var headerTextColor = style.HeaderTextColorEnabled ? NormalizeHexOrNull(style.HeaderTextColor) : null;
            var contentTextColor = style.ContentTextColorEnabled ? NormalizeHexOrNull(style.ContentTextColor) : null;

            layout[entry.Key] = current with
            {
                CustomTitle = customTitle,
                TitleAlignment = titleAlignment,
                ContentAlignment = contentAlignment,
                PanelBackground = panelColor,
                HeaderBackground = headerColor,
                HeaderTextColor = headerTextColor,
                ContentTextColor = contentTextColor
            };
        }
    }

    private static Dictionary<string, SectionLayoutState> BuildLayoutSelections(
        string? layoutPresetMode,
        bool aboutEnabled,
        bool shopEnabled,
        bool policiesEnabled,
        bool paymentsEnabled,
        bool referencesEnabled,
        bool newsEnabled,
        bool chatEnabled,
        bool reviewsEnabled,
        bool summaryEnabled,
        bool pgpEnabled,
        string aboutPlacement,
        string extraPlacement,
        string policiesPlacement,
        string paymentsPlacement,
        string externalPlacement,
        string newsPlacement,
        string chatPlacement,
        string reviewsPlacement,
        string summaryPlacement,
        string pgpPlacement,
        Dictionary<string, SectionStylePostModel>? sectionStyles)
    {
        var presetKey = string.IsNullOrWhiteSpace(layoutPresetMode) || string.Equals(layoutPresetMode, "custom", StringComparison.OrdinalIgnoreCase)
            ? null
            : layoutPresetMode;

        Dictionary<string, SectionLayoutState> layout;
        if (!string.IsNullOrWhiteSpace(presetKey) && ProfileLayoutEngine.TryGetPresetLayout(presetKey, out var presetLayout))
        {
            layout = presetLayout;
        }
        else
        {
            layout = CloneSectionDefaults();
            ApplyLayoutSelection(layout, "about", aboutEnabled, aboutPlacement);
            ApplyLayoutSelection(layout, "extra", shopEnabled, extraPlacement);
            ApplyLayoutSelection(layout, "policies", policiesEnabled, policiesPlacement);
            ApplyLayoutSelection(layout, "payments", paymentsEnabled, paymentsPlacement);
            ApplyLayoutSelection(layout, "external", referencesEnabled, externalPlacement);
            ApplyLayoutSelection(layout, "news", newsEnabled, newsPlacement);
            ApplyLayoutSelection(layout, "chat", chatEnabled, chatPlacement);
            ApplyLayoutSelection(layout, "reviews", reviewsEnabled, reviewsPlacement);
            ApplyLayoutSelection(layout, "summary", summaryEnabled, summaryPlacement);
            ApplyLayoutSelection(layout, "pgp", pgpEnabled, pgpPlacement);
        }

        ApplySectionStyleOverrides(layout, sectionStyles);
        return layout;
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

    [Authorize]
    [HttpPost("/user/{username}/profile-chat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostProfileChat(string username, string chatMessage)
    {
        var user = await _userService.GetByUsernameAsync(username);
        if (user == null)
        {
            return NotFound();
        }

        var viewer = await GetViewerAsync();
        if (viewer == null)
        {
            return RedirectToAction("Index", new { username });
        }

        if (string.IsNullOrWhiteSpace(chatMessage))
        {
            TempData["ProfileChatError"] = "Message cannot be empty.";
            return RedirectToAction("Index", new { username });
        }

        try
        {
            await _profileChatService.AddMessageAsync(viewer, user, chatMessage);
            TempData["ProfileChatSuccess"] = "Message sent. Refresh this page to see the update.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["ProfileChatError"] = ex.Message;
        }

        return RedirectToAction("Index", new { username });
    }

    [HttpGet("/user/news/{id}")]
    public async Task<IActionResult> News(int id)
    {
        var news = await _userService.GetNewsByIdAsync(id);
        if (news == null) return NotFound();
        return View(news);
    }

    [Authorize]
    [HttpGet("/user/edit")]
    public async Task<IActionResult> Edit()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return NotFound();

        var model = new EditProfileViewModel
        {
            AboutMe = user.AboutMe,
            FontColor = user.FontColor,
            FontFamily = user.FontFamily
        };
        return View(model);
    }

    [Authorize]
    [HttpGet("/user/editprofile")]
    public async Task<IActionResult> EditProfile()
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return NotFound();

        ViewBag.User = user;
        ViewBag.HideSidebars = true;
        ViewBag.SectionLayout = ParseSectionLayout(user.ProfileLayout);
        return View();
    }

    [Authorize]
    [HttpPost("/user/editprofile")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditProfile(string aboutMe, string fontColor, string fontFamily,
        string backgroundColor, string accentColor, string borderColor, string profileLayout,
        string? backgroundColorHex, string? fontColorHex, string? accentColorHex, string? borderColorHex,
        string buttonBackgroundColor, string buttonTextColor, string buttonBorderColor,
        string? buttonBackgroundColorHex, string? buttonTextColorHex, string? buttonBorderColorHex,
        string vendorShopDescription, string vendorPolicies, string vendorPayments, string vendorExternalReferences,
        bool shopEnabled, bool policiesEnabled, bool paymentsEnabled, bool referencesEnabled,
        bool aboutEnabled, bool newsEnabled, bool chatEnabled, bool reviewsEnabled, bool summaryEnabled, bool pgpEnabled,
        string aboutPlacement, string extraPlacement, string policiesPlacement, string paymentsPlacement, string externalPlacement,
        string newsPlacement, string chatPlacement, string reviewsPlacement, string summaryPlacement, string pgpPlacement,
        string[]? paymentType, string[]? paymentLabel, string[]? paymentDetails,
        string[]? referenceLabel, string[]? referenceUrl, string[]? referenceNotes,
        string? themePath, string? pathChoice, string? themePreset, string? applyTheme, string? tonePreset, string? accentPreset,
        string? tonePresetOriginal, string? accentPresetOriginal, string? layoutPresetMode,
        Dictionary<string, SectionStylePostModel>? sectionStyles)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var preset = GetThemePreset(themePreset);
        var shouldApplyPreset = preset.HasValue && (applyTheme == "load" || !string.IsNullOrWhiteSpace(themePreset));
        if (shouldApplyPreset)
        {
            backgroundColor = preset.Value.Background;
            backgroundColorHex = preset.Value.Background;
            fontColor = preset.Value.Font;
            fontColorHex = preset.Value.Font;
            accentColor = preset.Value.Accent;
            accentColorHex = preset.Value.Accent;
            borderColor = preset.Value.Border;
            borderColorHex = preset.Value.Border;
            buttonBackgroundColor = preset.Value.ButtonBg;
            buttonBackgroundColorHex = preset.Value.ButtonBg;
            buttonTextColor = preset.Value.ButtonText;
            buttonTextColorHex = preset.Value.ButtonText;
            buttonBorderColor = preset.Value.ButtonBorder;
            buttonBorderColorHex = preset.Value.ButtonBorder;
        }

        var resolvedBackground = NormalizeHexOrFallback(backgroundColorHex, backgroundColor, "#0f172a");
        var resolvedFont = NormalizeHexOrFallback(fontColorHex, fontColor, "#e5e7eb");
        var resolvedAccent = NormalizeHexOrFallback(accentColorHex, accentColor, "#3b82f6");
        var resolvedBorder = NormalizeHexOrFallback(borderColorHex, borderColor, "#334155");
        var resolvedButtonBg = NormalizeHexOrFallback(buttonBackgroundColorHex, buttonBackgroundColor, "#0ea5e9");
        var resolvedButtonText = NormalizeHexOrFallback(buttonTextColorHex, buttonTextColor, "#ffffff");
        var resolvedButtonBorder = NormalizeHexOrFallback(buttonBorderColorHex, buttonBorderColor, resolvedButtonBg);

        var tonePalette = GetTonePalette(tonePreset);
        var toneSelectionChanged = tonePalette.HasValue && !string.Equals(tonePreset, tonePresetOriginal, StringComparison.OrdinalIgnoreCase);
        if (tonePalette.HasValue && (string.IsNullOrWhiteSpace(tonePresetOriginal) || toneSelectionChanged))
        {
            resolvedBackground = tonePalette.Value.Background;
            resolvedFont = tonePalette.Value.Font;
            resolvedBorder = tonePalette.Value.Border;
        }

        var accentPalette = GetAccentPalette(accentPreset);
        var accentSelectionChanged = accentPalette.HasValue && !string.Equals(accentPreset, accentPresetOriginal, StringComparison.OrdinalIgnoreCase);
        if (accentPalette.HasValue && (string.IsNullOrWhiteSpace(accentPresetOriginal) || accentSelectionChanged))
        {
            resolvedAccent = accentPalette.Value.Accent;
            resolvedButtonBg = accentPalette.Value.ButtonBg;
            resolvedButtonText = accentPalette.Value.ButtonText;
            resolvedButtonBorder = accentPalette.Value.ButtonBorder;
        }

        var resolvedShop = shopEnabled ? vendorShopDescription ?? string.Empty : string.Empty;
        var resolvedPolicies = policiesEnabled ? vendorPolicies ?? string.Empty : string.Empty;
        var paymentRows = paymentsEnabled
            ? ProfileStructuredFields.ComposePayments(paymentType, paymentLabel, paymentDetails, vendorPayments)
            : Array.Empty<PaymentRow>();
        var resolvedPayments = paymentsEnabled ? ProfileStructuredFields.SerializePayments(paymentRows) : string.Empty;

        var referenceRows = referencesEnabled
            ? ProfileStructuredFields.ComposeReferences(referenceLabel, referenceUrl, referenceNotes, vendorExternalReferences)
            : Array.Empty<ReferenceRow>();
        var resolvedReferences = referencesEnabled ? ProfileStructuredFields.SerializeReferences(referenceRows) : string.Empty;

        var layoutSelections = BuildLayoutSelections(
            layoutPresetMode,
            aboutEnabled,
            shopEnabled,
            policiesEnabled,
            paymentsEnabled,
            referencesEnabled,
            newsEnabled,
            chatEnabled,
            reviewsEnabled,
            summaryEnabled,
            pgpEnabled,
            aboutPlacement,
            extraPlacement,
            policiesPlacement,
            paymentsPlacement,
            externalPlacement,
            newsPlacement,
            chatPlacement,
            reviewsPlacement,
            summaryPlacement,
            pgpPlacement,
            sectionStyles);
        profileLayout = SerializeSectionLayout(layoutSelections);

        await _userService.UpdateFullProfileAsync(userId, aboutMe, resolvedFont, fontFamily,
            resolvedBackground, resolvedAccent, resolvedBorder, profileLayout,
            resolvedButtonBg, resolvedButtonText, resolvedButtonBorder,
            resolvedShop, resolvedPolicies, resolvedPayments, resolvedReferences);
        
        TempData["Success"] = "Your page has been updated!";
        return RedirectToAction("Index", new { username = User.Identity!.Name });
    }

    [Authorize]
    [HttpPost("/user/editprofile/preview")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreviewProfile(string aboutMe, string fontColor, string fontFamily,
        string backgroundColor, string accentColor, string borderColor, string profileLayout,
        string? backgroundColorHex, string? fontColorHex, string? accentColorHex, string? borderColorHex,
        string buttonBackgroundColor, string buttonTextColor, string buttonBorderColor,
        string? buttonBackgroundColorHex, string? buttonTextColorHex, string? buttonBorderColorHex,
        string vendorShopDescription, string vendorPolicies, string vendorPayments, string vendorExternalReferences,
        bool shopEnabled, bool policiesEnabled, bool paymentsEnabled, bool referencesEnabled,
        bool aboutEnabled, bool newsEnabled, bool chatEnabled, bool reviewsEnabled, bool summaryEnabled, bool pgpEnabled,
        string aboutPlacement, string extraPlacement, string policiesPlacement, string paymentsPlacement, string externalPlacement,
        string newsPlacement, string chatPlacement, string reviewsPlacement, string summaryPlacement, string pgpPlacement,
        string[]? paymentType, string[]? paymentLabel, string[]? paymentDetails,
        string[]? referenceLabel, string[]? referenceUrl, string[]? referenceNotes,
        string? themePath, string? pathChoice, string? themePreset, string? applyTheme, string? tonePreset, string? accentPreset,
        string? tonePresetOriginal, string? accentPresetOriginal, string? layoutPresetMode,
        Dictionary<string, SectionStylePostModel>? sectionStyles)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return NotFound();

        var preset = GetThemePreset(themePreset);
        var shouldApplyPreset = preset.HasValue && (applyTheme == "load" || !string.IsNullOrWhiteSpace(themePreset));
        if (shouldApplyPreset)
        {
            backgroundColor = preset.Value.Background;
            backgroundColorHex = preset.Value.Background;
            fontColor = preset.Value.Font;
            fontColorHex = preset.Value.Font;
            accentColor = preset.Value.Accent;
            accentColorHex = preset.Value.Accent;
            borderColor = preset.Value.Border;
            borderColorHex = preset.Value.Border;
            buttonBackgroundColor = preset.Value.ButtonBg;
            buttonBackgroundColorHex = preset.Value.ButtonBg;
            buttonTextColor = preset.Value.ButtonText;
            buttonTextColorHex = preset.Value.ButtonText;
            buttonBorderColor = preset.Value.ButtonBorder;
            buttonBorderColorHex = preset.Value.ButtonBorder;
        }

        var resolvedBackground = NormalizeHexOrFallback(backgroundColorHex, backgroundColor, user.BackgroundColor ?? "#0f172a");
        var resolvedFont = NormalizeHexOrFallback(fontColorHex, fontColor, user.FontColor ?? "#e5e7eb");
        var resolvedAccent = NormalizeHexOrFallback(accentColorHex, accentColor, user.AccentColor ?? "#3b82f6");
        var resolvedBorder = NormalizeHexOrFallback(borderColorHex, borderColor, user.BorderColor ?? "#334155");
        var resolvedButtonBg = NormalizeHexOrFallback(buttonBackgroundColorHex, buttonBackgroundColor, user.ButtonBackgroundColor ?? "#0ea5e9");
        var resolvedButtonText = NormalizeHexOrFallback(buttonTextColorHex, buttonTextColor, user.ButtonTextColor ?? "#ffffff");
        var resolvedButtonBorder = NormalizeHexOrFallback(buttonBorderColorHex, buttonBorderColor, user.ButtonBorderColor ?? resolvedButtonBg);

        var tonePalette = GetTonePalette(tonePreset);
        var toneSelectionChanged = tonePalette.HasValue && !string.Equals(tonePreset, tonePresetOriginal, StringComparison.OrdinalIgnoreCase);
        if (tonePalette.HasValue && (string.IsNullOrWhiteSpace(tonePresetOriginal) || toneSelectionChanged))
        {
            resolvedBackground = tonePalette.Value.Background;
            resolvedFont = tonePalette.Value.Font;
            resolvedBorder = tonePalette.Value.Border;
        }

        var accentPalette = GetAccentPalette(accentPreset);
        var accentSelectionChanged = accentPalette.HasValue && !string.Equals(accentPreset, accentPresetOriginal, StringComparison.OrdinalIgnoreCase);
        if (accentPalette.HasValue && (string.IsNullOrWhiteSpace(accentPresetOriginal) || accentSelectionChanged))
        {
            resolvedAccent = accentPalette.Value.Accent;
            resolvedButtonBg = accentPalette.Value.ButtonBg;
            resolvedButtonText = accentPalette.Value.ButtonText;
            resolvedButtonBorder = accentPalette.Value.ButtonBorder;
        }

        var resolvedShop = shopEnabled ? vendorShopDescription ?? string.Empty : string.Empty;
        var resolvedPolicies = policiesEnabled ? vendorPolicies ?? string.Empty : string.Empty;
        var paymentRows = paymentsEnabled
            ? ProfileStructuredFields.ComposePayments(paymentType, paymentLabel, paymentDetails, vendorPayments)
            : Array.Empty<PaymentRow>();
        var resolvedPayments = paymentsEnabled ? ProfileStructuredFields.SerializePayments(paymentRows) : string.Empty;

        var referenceRows = referencesEnabled
            ? ProfileStructuredFields.ComposeReferences(referenceLabel, referenceUrl, referenceNotes, vendorExternalReferences)
            : Array.Empty<ReferenceRow>();
        var resolvedReferences = referencesEnabled ? ProfileStructuredFields.SerializeReferences(referenceRows) : string.Empty;

        var layoutSelections = BuildLayoutSelections(
            layoutPresetMode,
            aboutEnabled,
            shopEnabled,
            policiesEnabled,
            paymentsEnabled,
            referencesEnabled,
            newsEnabled,
            chatEnabled,
            reviewsEnabled,
            summaryEnabled,
            pgpEnabled,
            aboutPlacement,
            extraPlacement,
            policiesPlacement,
            paymentsPlacement,
            externalPlacement,
            newsPlacement,
            chatPlacement,
            reviewsPlacement,
            summaryPlacement,
            pgpPlacement,
            sectionStyles);
        profileLayout = SerializeSectionLayout(layoutSelections);

        var previewUser = new MyFace.Core.Entities.User
        {
            Id = user.Id,
            Username = user.Username,
            CreatedAt = user.CreatedAt,
            AboutMe = aboutMe ?? string.Empty,
            FontColor = resolvedFont,
            FontFamily = string.IsNullOrWhiteSpace(fontFamily) ? "system-ui, -apple-system, sans-serif" : fontFamily,
            FontSize = 14,
            BackgroundColor = resolvedBackground,
            AccentColor = resolvedAccent,
            BorderColor = resolvedBorder,
            ProfileLayout = profileLayout,
            ButtonBackgroundColor = resolvedButtonBg,
            ButtonTextColor = resolvedButtonText,
            ButtonBorderColor = resolvedButtonBorder,
            VendorShopDescription = resolvedShop,
            VendorPolicies = resolvedPolicies,
            VendorPayments = resolvedPayments,
            VendorExternalReferences = resolvedReferences,
            PostUpvotes = user.PostUpvotes,
            PostDownvotes = user.PostDownvotes,
            CommentUpvotes = user.CommentUpvotes,
            CommentDownvotes = user.CommentDownvotes,
            Contacts = user.Contacts,
            News = user.News,
            PGPVerifications = user.PGPVerifications
        };

        ViewBag.User = previewUser;
        ViewBag.IsPreview = true;
        ViewBag.HideSidebars = true;
        ViewBag.SectionLayout = layoutSelections;
        return View("EditProfile");
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

    [Authorize]
    [HttpPost("/user/edit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditProfileViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _userService.UpdateProfileAsync(userId, model.AboutMe, model.FontColor, model.FontFamily);
        
        return RedirectToAction("Index", new { username = User.Identity!.Name });
    }

    // Admin/Mod edit user about me
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminEditAboutMe(int userId, string aboutMe)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return NotFound();

        user.AboutMe = aboutMe;
        await _userService.UpdateProfileAsync(userId, aboutMe, user.FontColor, user.FontFamily);
        
        return RedirectToAction("Index", new { username = user.Username });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminUpdateVendor(int userId, string vendorShopDescription, string vendorPolicies, string vendorPayments, string vendorExternalReferences)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return NotFound();

        await _userService.UpdateVendorAsync(userId, vendorShopDescription, vendorPolicies, vendorPayments, vendorExternalReferences);
        return RedirectToAction("Index", new { username = user.Username });
    }

    // Admin/Mod suspend user
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspendUser(int userId, string duration)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var currentUser = await _userService.GetByUsernameAsync(User.Identity?.Name ?? "");
        var targetUser = await _userService.GetUserByIdAsync(userId);

        if (currentUser == null || targetUser == null)
        {
            return NotFound();
        }

        // Prevent self-suspension
        if (currentUser.Id == targetUser.Id)
        {
            return Forbid();
        }

        if (currentUser.Role != "Admin" && (targetUser.Role == "Admin" || targetUser.Role == "Moderator"))
        {
            return Forbid();
        }

        DateTime? suspendedUntil = null;
        switch (duration)
        {
            case "24h":
                suspendedUntil = DateTime.UtcNow.AddHours(24);
                break;
            case "72h":
                suspendedUntil = DateTime.UtcNow.AddHours(72);
                break;
            case "1w":
                suspendedUntil = DateTime.UtcNow.AddDays(7);
                break;
            case "2w":
                suspendedUntil = DateTime.UtcNow.AddDays(14);
                break;
            case "1m":
                suspendedUntil = DateTime.UtcNow.AddDays(30);
                break;
            case "forever":
                suspendedUntil = DateTime.MaxValue;
                break;
            case "unsuspend":
                suspendedUntil = null;
                break;
        }

        await _userService.SuspendUserAsync(userId, suspendedUntil);
        return RedirectToAction("Index", new { username = targetUser.Username });
    }

    // Admin only: nuke user account
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(int userId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin")
        {
            return Forbid();
        }

        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return NotFound();

        await _userService.DeleteUserAsync(userId);
        return RedirectToAction("Index", "Moderator");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminRemoveContact(int id)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var contact = await _userService.GetContactByIdAsync(id);
        if (contact == null) return NotFound();

        var username = contact.User?.Username;
        await _userService.RemoveContactByAdminAsync(id);

        if (!string.IsNullOrWhiteSpace(username))
        {
            return RedirectToAction("Index", new { username });
        }

        return RedirectToAction("Index", "Moderator");
    }

    [Authorize]
    [HttpPost("/user/contact/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddContact(AddContactViewModel model)
    {
        if (ModelState.IsValid)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _userService.AddContactAsync(userId, model.ServiceName, model.AccountId);
        }
        return RedirectToAction("Index", new { username = User.Identity!.Name });
    }

    [Authorize]
    [HttpPost("/user/contact/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveContact(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _userService.RemoveContactAsync(userId, id);
        return RedirectToAction("Index", new { username = User.Identity!.Name });
    }

    [Authorize]
    [HttpPost("/user/news/add")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddNews(AddNewsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View("CreateNews", model);
        }

        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _userService.AddNewsAsync(userId, model.Title, model.Content, model.ApplyTheme);
        return RedirectToAction("Index", new { username = User.Identity!.Name });
    }

    [Authorize]
    [HttpPost("/user/news/remove")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveNews(int id)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _userService.RemoveNewsAsync(userId, id);
        return RedirectToAction("Index", new { username = User.Identity!.Name });
    }

    [Authorize]
    [HttpGet("/user/news/new")]
    public IActionResult NewNews()
    {
        return View("CreateNews", new AddNewsViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminRemoveNews(int id)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var news = await _userService.GetNewsByIdAsync(id);
        if (news == null) return NotFound();

        await _userService.RemoveNewsByAdminAsync(id);
        return RedirectToAction("Index", new { username = news.User?.Username ?? User.Identity?.Name });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminClearPgpKey(int userId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return NotFound();

        await _userService.ClearPgpKeyAsync(userId);
        return RedirectToAction("Index", new { username = user.Username });
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

    private static ChatWindowMessageViewModel ToChatWindowMessage(ProfileChatMessage message)
    {
        var username = string.IsNullOrWhiteSpace(message.AuthorUsername) ? "user" : message.AuthorUsername;
        var role = string.IsNullOrWhiteSpace(message.AuthorRole) ? "User" : message.AuthorRole;

        return new ChatWindowMessageViewModel
        {
            Username = username,
            Role = role,
            CreatedAt = message.CreatedAt,
            Content = message.Body ?? string.Empty
        };
    }

    private static string? NormalizeCustomTitle(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string NormalizeAlignment(string? value, string fallback)
    {
        var normalized = value?.Trim().ToLowerInvariant();
        return normalized switch
        {
            "center" => "center",
            "right" => "right",
            _ => fallback
        };
    }

    private static string? NormalizeHexOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length == 7 && trimmed[0] == '#' && trimmed.Skip(1).All(Uri.IsHexDigit))
        {
            return trimmed;
        }

        return null;
    }

    private static string NormalizeHexOrFallback(string? hexValue, string? colorInput, string fallback)
    {
        // Prefer the color input (color picker) when valid; fall back to hex textbox, then default
        var candidates = new[] { colorInput, hexValue, fallback };
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var trimmed = candidate.Trim();
            if (trimmed.Length == 7 && trimmed[0] == '#' && trimmed.Skip(1).All(c => Uri.IsHexDigit(c)))
            {
                return trimmed;
            }
        }
        return fallback;
    }
}
