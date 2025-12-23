using Microsoft.AspNetCore.Mvc;
using MyFace.Services;

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

        return View(posts);
    }
}
