using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MyFace.Services;

namespace MyFace.Web.Controllers;

public class PostController : Controller
{
    private readonly ForumService _forumService;
    private readonly ControlSettingsReader _settingsReader;

    public PostController(ForumService forumService, ControlSettingsReader settingsReader)
    {
        _forumService = forumService;
        _settingsReader = settingsReader;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Report(int postId)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Forbid();
        }

        await _forumService.ReportPostAsync(postId);
        var post = await _forumService.GetPostByIdAsync(postId);
        return RedirectToAction("View", "Thread", new { id = post?.ThreadId ?? 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int threadId, string content, bool postAsAnonymous = false)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return RedirectToAction("View", "Thread", new { id = threadId });
        }

        int? userId = GetCurrentUserId();
        var allowAnonymous = await _settingsReader.GetBoolAsync(ControlSettingKeys.PostAllowAnonymous, true, HttpContext.RequestAborted);
        if (postAsAnonymous && !allowAnonymous)
        {
            TempData["Error"] = "Anonymous posting is currently disabled by administrators.";
        }

        bool isAnonymous = (postAsAnonymous && allowAnonymous) || userId == null;

        await _forumService.CreatePostAsync(threadId, content, userId, isAnonymous);
        return RedirectToAction("View", "Thread", new { id = threadId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _forumService.GetPostWithUserAsync(id);
        if (post == null)
        {
            return NotFound();
        }

        var userId = GetCurrentUserId();
        var role = User.FindFirstValue(ClaimTypes.Role);
        var isAdmin = role == "Admin";
        var isMod = role == "Moderator";
        var postOwnerRole = post.User?.Role ?? "User";
        var canOverride = isAdmin || (isMod && postOwnerRole == "User");

        if (userId == null && !isAdmin)
        {
            return Forbid();
        }

        if (post.UserId != userId && !canOverride)
        {
            return Forbid();
        }

        return View(post);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, string content)
    {
        var userId = GetCurrentUserId();
        var role = User.FindFirstValue(ClaimTypes.Role);
        var isAdmin = role == "Admin";
        var isMod = role == "Moderator";

        if (userId == null && !isAdmin)
        {
            return Forbid();
        }

        var post = await _forumService.GetPostWithUserAsync(id);
        if (post == null)
        {
            return NotFound();
        }

        var postOwnerRole = post.User?.Role ?? "User";
        var canOverride = isAdmin || (isMod && postOwnerRole == "User");
        var actingUserId = userId ?? 0;

        if (post.UserId != actingUserId && !canOverride)
        {
            return Forbid();
        }

        var success = await _forumService.UpdatePostAsync(id, actingUserId, content, canOverride);
        if (!success)
        {
            return NotFound();
        }

        var threadId = post.ThreadId;
        return RedirectToAction("View", "Thread", new { id = threadId });
    }

    [HttpGet]
    public async Task<IActionResult> List(int threadId)
    {
        var thread = await _forumService.GetThreadByIdAsync(threadId);
        if (thread == null) return NotFound();
        return View(thread.Posts.OrderBy(p => p.CreatedAt).ToList());
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
}
