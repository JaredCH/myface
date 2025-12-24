using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Services;

namespace MyFace.Web.Controllers;

[OnlyAdminAuthorization]
public class AdminController : Controller
{
    private readonly UserService _userService;

    public AdminController(UserService userService)
    {
        _userService = userService;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userService.GetAllUsersAsync();
        return View(users);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole(int userId, string role)
    {
        if (role != "User" && role != "Moderator" && role != "Admin")
        {
            return BadRequest("Invalid role");
        }
        await _userService.SetRoleAsync(userId, role);
        return RedirectToAction("Index");
    }
}
