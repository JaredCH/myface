using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Models;
using MyFace.Web.Services;

namespace MyFace.Web.Controllers;

public class ThreadController : Controller
{
    private readonly ForumService _forumService;
    private readonly CaptchaService _captchaService;
    private readonly ThreadImageStorageService _imageStorageService;
    private readonly ControlSettingsReader _settingsReader;

    public ThreadController(
        ForumService forumService,
        CaptchaService captchaService,
        ThreadImageStorageService imageStorageService,
        ControlSettingsReader settingsReader)
    {
        _forumService = forumService;
        _captchaService = captchaService;
        _imageStorageService = imageStorageService;
        _settingsReader = settingsReader;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 25;
        
        // Hot: Wilson Score + Time Decay ranking with failsafe
        ViewBag.HotThreads = await _forumService.GetHotThreadsAsync(pageSize);
        
        // New: Simple chronological order, newest first
        var allThreads = await _forumService.GetThreadsAsync(0, 1000);
        ViewBag.NewThreads = allThreads.OrderByDescending(t => t.CreatedAt).Take(pageSize).ToList();
        
        // News: Only threads with News category
        ViewBag.NewsThreads = allThreads.Where(t => t.Category == "News").OrderByDescending(t => t.CreatedAt).Take(pageSize).ToList();
        
        // Announcements: Only threads with Announcements category
        ViewBag.AnnouncementsThreads = allThreads.Where(t => t.Category == "Announcements").OrderByDescending(t => t.CreatedAt).Take(pageSize).ToList();

        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role == "Admin" || role == "Moderator")
        {
            ViewBag.ReportedPosts = await _forumService.GetReportedPostsAsync();
        }
        ViewBag.ReportHideThreshold = _forumService.GetReportHideThreshold();
        
        ViewBag.CurrentPage = page;
        ViewBag.HasMorePages = false;
        
        // Return hot as default
        return View(ViewBag.HotThreads as List<MyFace.Core.Entities.Thread>);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        PrepareCreateCaptcha();
        await SetAnonymousFlagAsync();
        return View(new CreateThreadViewModel());
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var thread = await _forumService.GetThreadWithUserAsync(id);
        if (thread == null)
        {
            return NotFound();
        }

        var userId = GetCurrentUserId();
        var role = User.FindFirstValue(ClaimTypes.Role);
        var isAdmin = role == "Admin";
        var isMod = role == "Moderator";
        var ownerRole = thread.User?.Role ?? "User";
        var canOverride = isAdmin || (isMod && ownerRole == "User");

        if (userId == null && !isAdmin)
        {
            return Forbid();
        }

        if (thread.UserId != userId && !canOverride)
        {
            return Forbid();
        }

        var mainPost = thread.Posts.OrderBy(p => p.CreatedAt).FirstOrDefault();
        var model = new EditThreadViewModel
        {
            Id = thread.Id,
            Title = thread.Title,
            Content = mainPost?.Content ?? string.Empty
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateThreadViewModel model, string captchaAnswer, CancellationToken cancellationToken)
    {
        var expected = HttpContext.Session.GetString("ThreadCaptchaAnswer");
        if (!_captchaService.Validate(expected, captchaAnswer))
        {
            ModelState.AddModelError("Captcha", "Incorrect security check answer.");
            PrepareCreateCaptcha();
            return View(model);
        }
        HttpContext.Session.Remove("ThreadCaptchaAnswer");

        var imageCount = model.Images?.Count ?? 0;
        if (imageCount > 2)
        {
            ModelState.AddModelError("Images", "You can attach up to two images.");
        }

        if (!ModelState.IsValid)
        {
            PrepareCreateCaptcha();
            await SetAnonymousFlagAsync();
            return View(model);
        }

        int? userId = GetCurrentUserId();
        var allowAnonymous = await _settingsReader.GetBoolAsync(ControlSettingKeys.PostAllowAnonymous, true, HttpContext.RequestAborted);
        if (model.PostAsAnonymous && !allowAnonymous)
        {
            TempData["ThreadCreateError"] = "Anonymous posting is currently disabled by administrators.";
        }

        bool isAnonymous = (model.PostAsAnonymous && allowAnonymous) || userId == null;

        List<StoredImageResult> uploads = new();
        if (model.Images?.Any() == true)
        {
            try
            {
                var uploadContext = BuildUploadContext("ThreadCreate", userId, isAnonymous);
                uploads = (await _imageStorageService.SaveImagesAsync(model.Images, uploadContext, cancellationToken)).ToList();
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError("Images", ex.Message);
                PrepareCreateCaptcha();
                await SetAnonymousFlagAsync();
                return View(model);
            }
        }

        var thread = await _forumService.CreateThreadAsync(model.Title, userId, isAnonymous);
        var post = await _forumService.CreatePostAsync(thread.Id, model.Content, userId, isAnonymous);

        var order = 0;
        foreach (var upload in uploads)
        {
            await _forumService.AddPostImageAsync(post.Id, upload.OriginalUrl, upload.ThumbnailUrl, upload.ContentType, upload.Width, upload.Height, upload.FileSize, order++);
        }

        return RedirectToAction("View", new { id = thread.Id });
    }

    private async Task SetAnonymousFlagAsync()
    {
        ViewBag.AllowAnonymousPosts = await _settingsReader.GetBoolAsync(ControlSettingKeys.PostAllowAnonymous, true, HttpContext.RequestAborted);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(EditThreadViewModel model)
    {
        var thread = await _forumService.GetThreadWithUserAsync(model.Id);
        if (thread == null)
        {
            return NotFound();
        }

        var userId = GetCurrentUserId();
        var role = User.FindFirstValue(ClaimTypes.Role);
        var isAdmin = role == "Admin";
        var isMod = role == "Moderator";
        var ownerRole = thread.User?.Role ?? "User";
        var canOverride = isAdmin || (isMod && ownerRole == "User");

        if (userId == null && !isAdmin)
        {
            return Forbid();
        }

        if (thread.UserId != userId && !canOverride)
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var actingUserId = userId ?? 0;
        await _forumService.UpdateThreadAsync(thread.Id, actingUserId, model.Title, canOverride);

        var mainPost = thread.Posts.OrderBy(p => p.CreatedAt).FirstOrDefault();
        if (mainPost != null)
        {
            await _forumService.UpdatePostAsync(mainPost.Id, actingUserId, model.Content, canOverride);
        }

        return RedirectToAction("View", new { id = thread.Id });
    }

    public async Task<IActionResult> View(int id)
    {
        var thread = await _forumService.GetThreadByIdAsync(id);
        
        if (thread == null)
        {
            return NotFound();
        }

        var currentUserId = GetCurrentUserId();
        ViewBag.CurrentUserId = currentUserId;

        // Calculate thread score
        ViewBag.ThreadScore = await _forumService.GetThreadScoreAsync(id);

        ViewBag.ReportHideThreshold = _forumService.GetReportHideThreshold();

        // Calculate scores for posts
        var postScores = new Dictionary<int, int>();
        foreach (var post in thread.Posts)
        {
            postScores[post.Id] = await _forumService.GetPostScoreAsync(post.Id);
        }
        ViewBag.PostScores = postScores;

        // Generate Captcha for Reply
        PrepareReplyCaptcha();
        await SetAnonymousFlagAsync();

        return View(thread);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(ThreadReplyViewModel model, string captchaAnswer, CancellationToken cancellationToken)
    {
        var expected = HttpContext.Session.GetString("ReplyCaptchaAnswer");
        if (!_captchaService.Validate(expected, captchaAnswer))
        {
            return RedirectToAction("View", new { id = model.ThreadId, error = "CaptchaFailed" });
        }
        HttpContext.Session.Remove("ReplyCaptchaAnswer");

        if (!ModelState.IsValid)
        {
            TempData["ThreadReplyError"] = "Reply content is required.";
            return RedirectToAction("View", new { id = model.ThreadId });
        }

        if ((model.Images?.Count ?? 0) > 2)
        {
            TempData["ThreadReplyError"] = "You can attach up to two images.";
            return RedirectToAction("View", new { id = model.ThreadId });
        }

        var allowAnonymous = await _settingsReader.GetBoolAsync(ControlSettingKeys.PostAllowAnonymous, true, HttpContext.RequestAborted);
        if (model.PostAsAnonymous && !allowAnonymous)
        {
            TempData["ThreadReplyError"] = "Anonymous posting is currently disabled by administrators.";
        }

        int? userId = GetCurrentUserId();
        bool isAnonymous = (model.PostAsAnonymous && allowAnonymous) || userId == null;

        List<StoredImageResult> uploads = new();
        try
        {
            if (model.Images?.Any() == true)
            {
                var uploadContext = BuildUploadContext("ThreadReply", userId, isAnonymous);
                uploads = (await _imageStorageService.SaveImagesAsync(model.Images, uploadContext, cancellationToken)).ToList();
            }
        }
        catch (InvalidOperationException ex)
        {
            TempData["ThreadReplyError"] = ex.Message;
            return RedirectToAction("View", new { id = model.ThreadId });
        }

        var post = await _forumService.CreatePostAsync(model.ThreadId, model.Content, userId, isAnonymous);

        var order = 0;
        foreach (var upload in uploads)
        {
            await _forumService.AddPostImageAsync(post.Id, upload.OriginalUrl, upload.ThumbnailUrl, upload.ContentType, upload.Width, upload.Height, upload.FileSize, order++);
        }

        return RedirectToAction("View", new { id = model.ThreadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(int postId, bool isUpvote)
    {
        var userId = GetCurrentUserId();
        var sessionId = HttpContext.Session.Id;
        if (userId == null)
        {
            // allow anonymous session-based voting
        }

        var value = isUpvote ? 1 : -1;
        await _forumService.VoteAsync(postId, userId, sessionId, value);

        // Redirect back to the thread containing this post
        var post = await _forumService.GetPostByIdAsync(postId);
        return RedirectToAction("View", new { id = post?.ThreadId ?? 1 });
    }

    // Admin/Mod thread management
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteThread(int id)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        await _forumService.DeleteThreadAsync(id);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LockThread(int id)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var thread = await _forumService.GetThreadByIdAsync(id);
        if (thread == null) return NotFound();

        await _forumService.LockThreadAsync(id, true);
        return RedirectToAction("View", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockThread(int id)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var thread = await _forumService.GetThreadByIdAsync(id);
        if (thread == null) return NotFound();

        await _forumService.LockThreadAsync(id, false);
        return RedirectToAction("View", new { id });
    }

    // Admin/Mod post management
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminDeletePost(int postId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var post = await _forumService.GetPostByIdAsync(postId);
        if (post == null) return NotFound();

        await _forumService.AdminDeletePostAsync(postId);
        var returnToModeration = Request.HasFormContentType && Request.Form.TryGetValue("returnToModeration", out var flag) && flag == "true";
        if (returnToModeration)
        {
            return RedirectToAction("Index", new { tab = "moderation" });
        }

        return RedirectToAction("View", new { id = post.ThreadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminEditPost(int postId, string content)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var post = await _forumService.GetPostWithUserAsync(postId);
        if (post == null) return NotFound();

        var ownerRole = post.User?.Role ?? "User";
        if (role == "Moderator" && ownerRole != "User")
        {
            return Forbid();
        }

        await _forumService.AdminEditPostAsync(postId, content, GetCurrentUserId());
        return RedirectToAction("View", new { id = post.ThreadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSticky(int postId, bool isSticky)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var post = await _forumService.GetPostByIdAsync(postId);
        if (post == null) return NotFound();

        await _forumService.SetStickyAsync(postId, isSticky);
        return RedirectToAction("View", new { id = post.ThreadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreReportedPost(int postId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var post = await _forumService.GetPostByIdAsync(postId);
        if (post == null)
        {
            return NotFound();
        }

        await _forumService.RestorePostFromReportsAsync(postId);
        var returnToModeration = Request.HasFormContentType && Request.Form.TryGetValue("returnToModeration", out var flag) && flag == "true";
        if (returnToModeration)
        {
            return RedirectToAction("Index", new { tab = "moderation" });
        }

        return RedirectToAction("View", new { id = post.ThreadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetCategory(int id, string category)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        
        // Only admins and mods can change categories
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }
        
        // Validate category
        var validCategories = new[] { "Hot", "New", "News", "Announcements" };
        if (!validCategories.Contains(category))
        {
            return BadRequest("Invalid category");
        }
        
        // Only admins can set Announcements
        if (category == "Announcements" && role != "Admin")
        {
            return Forbid();
        }
        
        var thread = await _forumService.GetThreadByIdAsync(id);
        if (thread == null) return NotFound();
        
        await _forumService.SetThreadCategoryAsync(id, category);
        return RedirectToAction("View", new { id });
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }
        return null;
    }

    private ImageUploadContext BuildUploadContext(string origin, int? userId, bool isAnonymous)
    {
        var sessionId = HttpContext.Session.Id;
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
        }

        var username = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.Name);
        var userAgent = Request.Headers.UserAgent.ToString();

        return new ImageUploadContext(
            userId,
            string.IsNullOrWhiteSpace(username) ? null : username,
            isAnonymous,
            sessionId,
            HashIpAddress(HttpContext.Connection.RemoteIpAddress?.ToString()),
            origin,
            string.IsNullOrWhiteSpace(userAgent) ? null : userAgent);
    }

    private static string? HashIpAddress(string? rawIp)
    {
        if (string.IsNullOrWhiteSpace(rawIp))
        {
            return null;
        }

        using var sha256 = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(rawIp);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToHexString(hash);
    }

    private void PrepareCreateCaptcha()
    {
        var challenge = _captchaService.GenerateChallenge();
        HttpContext.Session.SetString("ThreadCaptchaAnswer", challenge.Answer);
        ViewBag.CaptchaContext = challenge.Context;
        ViewBag.CaptchaQuestion = challenge.Question;
    }

    private void PrepareReplyCaptcha()
    {
        var challenge = _captchaService.GenerateChallenge();
        HttpContext.Session.SetString("ReplyCaptchaAnswer", challenge.Answer);
        ViewBag.CaptchaContext = challenge.Context;
        ViewBag.CaptchaQuestion = challenge.Question;
    }
}
