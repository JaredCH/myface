using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Services;
using System.Security.Claims;

namespace MyFace.Web.Controllers;

[OnlyAdminAuthorization]
public class AdminController : Controller
{
    private readonly UserService _userService;
    private readonly VisitTrackingService _visitTracking;

    public AdminController(UserService userService, VisitTrackingService visitTracking)
    {
        _userService = userService;
        _visitTracking = visitTracking;
    }

    public async Task<IActionResult> Index()
    {
        var users = await _userService.GetAllUsersAsync();
        var stats = await _visitTracking.GetStatisticsAsync();
        
        ViewBag.Statistics = stats;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeUsername(int userId, string newUsername, string? adminNote)
    {
        if (string.IsNullOrWhiteSpace(newUsername))
        {
            TempData["Error"] = "Username cannot be empty";
            return RedirectToAction("Index");
        }

        var adminIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(adminIdString, out var adminId))
        {
            return Unauthorized();
        }

        var success = await _userService.ChangeUsernameByAdminAsync(userId, newUsername, adminId, adminNote);
        
        if (success)
        {
            TempData["Success"] = "Username has been reset. User will be prompted to choose a new username on next login.";
        }
        else
        {
            TempData["Error"] = "Failed to reset username.";
        }

        return RedirectToAction("Index");
    }
}
