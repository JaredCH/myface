using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Models;

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

        ViewBag.User = user;
        ViewBag.CurrentPage = page;
        ViewBag.HasMorePages = posts.Count == pageSize;
        ViewBag.IsOwner = User.Identity?.IsAuthenticated == true && User.Identity.Name == username;
        
        // Check if viewer is admin or mod
        var viewerRole = User.FindFirstValue(ClaimTypes.Role);
        ViewBag.IsAdminOrMod = viewerRole == "Admin" || viewerRole == "Moderator";
        ViewBag.IsAdmin = viewerRole == "Admin";

        return View(posts);
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
        string vendorShopDescription, string vendorPolicies, string vendorPayments, string vendorExternalReferences)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var resolvedBackground = NormalizeHexOrFallback(backgroundColorHex, backgroundColor, "#0f172a");
        var resolvedFont = NormalizeHexOrFallback(fontColorHex, fontColor, "#e5e7eb");
        var resolvedAccent = NormalizeHexOrFallback(accentColorHex, accentColor, "#3b82f6");
        var resolvedBorder = NormalizeHexOrFallback(borderColorHex, borderColor, "#334155");
        var resolvedButtonBg = NormalizeHexOrFallback(buttonBackgroundColorHex, buttonBackgroundColor, "#0ea5e9");
        var resolvedButtonText = NormalizeHexOrFallback(buttonTextColorHex, buttonTextColor, "#ffffff");
        var resolvedButtonBorder = NormalizeHexOrFallback(buttonBorderColorHex, buttonBorderColor, resolvedButtonBg);

        await _userService.UpdateFullProfileAsync(userId, aboutMe, resolvedFont, fontFamily,
            resolvedBackground, resolvedAccent, resolvedBorder, profileLayout,
            resolvedButtonBg, resolvedButtonText, resolvedButtonBorder,
            vendorShopDescription, vendorPolicies, vendorPayments, vendorExternalReferences);
        
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
        string vendorShopDescription, string vendorPolicies, string vendorPayments, string vendorExternalReferences)
    {
        var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return NotFound();

        var resolvedBackground = NormalizeHexOrFallback(backgroundColorHex, backgroundColor, user.BackgroundColor ?? "#0f172a");
        var resolvedFont = NormalizeHexOrFallback(fontColorHex, fontColor, user.FontColor ?? "#e5e7eb");
        var resolvedAccent = NormalizeHexOrFallback(accentColorHex, accentColor, user.AccentColor ?? "#3b82f6");
        var resolvedBorder = NormalizeHexOrFallback(borderColorHex, borderColor, user.BorderColor ?? "#334155");
        var resolvedButtonBg = NormalizeHexOrFallback(buttonBackgroundColorHex, buttonBackgroundColor, user.ButtonBackgroundColor ?? "#0ea5e9");
        var resolvedButtonText = NormalizeHexOrFallback(buttonTextColorHex, buttonTextColor, user.ButtonTextColor ?? "#ffffff");
        var resolvedButtonBorder = NormalizeHexOrFallback(buttonBorderColorHex, buttonBorderColor, user.ButtonBorderColor ?? resolvedButtonBg);

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
            ProfileLayout = string.IsNullOrWhiteSpace(profileLayout) ? user.ProfileLayout : profileLayout,
            ButtonBackgroundColor = resolvedButtonBg,
            ButtonTextColor = resolvedButtonText,
            ButtonBorderColor = resolvedButtonBorder,
            VendorShopDescription = vendorShopDescription ?? string.Empty,
            VendorPolicies = vendorPolicies ?? string.Empty,
            VendorPayments = vendorPayments ?? string.Empty,
            VendorExternalReferences = vendorExternalReferences ?? string.Empty,
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
        return View("EditProfile");
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
        if (ModelState.IsValid)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _userService.AddNewsAsync(userId, model.Title, model.Content);
        }
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
