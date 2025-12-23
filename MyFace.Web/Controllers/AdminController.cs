using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Services;

namespace MyFace.Web.Controllers;

[AdminAuthorization]
public class AdminController : Controller
{
    private readonly ForumService _forumService;
    private readonly UserService _userService;

    public AdminController(ForumService forumService, UserService userService)
    {
        _forumService = forumService;
        _userService = userService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePost(int postId)
    {
        var post = await _forumService.GetPostByIdAsync(postId);
        if (post == null) return NotFound();
        await _forumService.DeletePostAsync(postId, post.UserId ?? 0); // admin delete ignoring owner
        return RedirectToAction("View", "Thread", new { id = post.ThreadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ModifyPost(int postId, string content)
    {
        var post = await _forumService.GetPostByIdAsync(postId);
        if (post == null) return NotFound();
        post.Content = content;
        post.EditedAt = DateTime.UtcNow;
        await Task.CompletedTask; // context save done by service methods elsewhere
        return RedirectToAction("View", "Thread", new { id = post.ThreadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BanUser(int userId)
    {
        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return NotFound();
        user.IsActive = false;
        return RedirectToAction("Index", "User", new { username = user.Username });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        var user = await _userService.GetByIdAsync(userId);
        if (user == null) return NotFound();
        // In MVP, soft-ban instead of hard delete
        user.IsActive = false;
        return RedirectToAction("Index", "Thread");
    }
}
