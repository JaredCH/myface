using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MyFace.Services;

namespace MyFace.Web.Controllers;

public class PostController : Controller
{
    private readonly ForumService _forumService;

    public PostController(ForumService forumService)
    {
        _forumService = forumService;
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
        bool isAnonymous = postAsAnonymous || userId == null;

        await _forumService.CreatePostAsync(threadId, content, userId, isAnonymous);
        return RedirectToAction("View", "Thread", new { id = threadId });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _forumService.GetPostByIdAsync(id);
        if (post == null)
        {
            return NotFound();
        }

        var userId = GetCurrentUserId();
        var role = User.FindFirstValue(ClaimTypes.Role);
        var isAdminOrMod = role == "Admin" || role == "Moderator";
        if (userId == null || (post.UserId != userId && !isAdminOrMod))
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
        var isAdminOrMod = role == "Admin" || role == "Moderator";
        if (userId == null && !isAdminOrMod)
        {
            return Forbid();
        }

        var actingUserId = userId ?? 0;
        var success = await _forumService.UpdatePostAsync(id, actingUserId, content, isAdminOrMod);
        if (!success)
        {
            return NotFound();
        }

        var post = await _forumService.GetPostByIdAsync(id);
        return RedirectToAction("View", "Thread", new { id = post?.ThreadId });
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
