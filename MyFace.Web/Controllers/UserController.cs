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
}
