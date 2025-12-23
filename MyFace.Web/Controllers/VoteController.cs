using Microsoft.AspNetCore.Mvc;
using MyFace.Services;

namespace MyFace.Web.Controllers;

public class VoteController : Controller
{
    private readonly ForumService _forumService;

    public VoteController(ForumService forumService)
    {
        _forumService = forumService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Up(int postId)
    {
        var userId = GetCurrentUserId();
        var sessionId = HttpContext.Session.Id;
        await _forumService.VoteAsync(postId, userId, sessionId, +1);
        var post = await _forumService.GetPostByIdAsync(postId);
        return RedirectToAction("View", "Thread", new { id = post?.ThreadId ?? 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Down(int postId)
    {
        var userId = GetCurrentUserId();
        var sessionId = HttpContext.Session.Id;
        await _forumService.VoteAsync(postId, userId, sessionId, -1);
        var post = await _forumService.GetPostByIdAsync(postId);
        return RedirectToAction("View", "Thread", new { id = post?.ThreadId ?? 1 });
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return int.TryParse(claim?.Value, out var id) ? id : (int?)null;
    }
}
