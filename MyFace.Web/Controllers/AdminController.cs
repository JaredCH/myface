using System;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using MyFace.Services;
using MyFace.Web.Models.Admin;
using MyFace.Web.Services;

namespace MyFace.Web.Controllers;

[OnlyAdminAuthorization]
public class AdminController : Controller
{
    private readonly UserService _userService;
    private readonly VisitTrackingService _visitTracking;
    private readonly UploadScanLogService _uploadScanLogService;
    private readonly PostLogService _postLogService;

    public AdminController(
        UserService userService,
        VisitTrackingService visitTracking,
        UploadScanLogService uploadScanLogService,
        PostLogService postLogService)
    {
        _userService = userService;
        _visitTracking = visitTracking;
        _uploadScanLogService = uploadScanLogService;
        _postLogService = postLogService;
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPasswordTemp(int userId, string tempPassword)
    {
        if (string.IsNullOrWhiteSpace(tempPassword))
        {
            TempData["Error"] = "Temporary password is required.";
            return RedirectToAction("Index");
        }

        var success = await _userService.AdminSetPasswordAsync(userId, tempPassword);
        TempData[success ? "Success" : "Error"] = success
            ? "Temporary password set. Ask the user to change it after logging in."
            : "User not found.";

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(int userId)
    {
        var currentIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(currentIdString, out var currentId) && currentId == userId)
        {
            TempData["Error"] = "You cannot delete your own account.";
            return RedirectToAction("Index");
        }

        var user = await _userService.GetUserByIdAsync(userId);
        if (user == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction("Index");
        }

        await _userService.DeleteUserAsync(userId);
        TempData["Success"] = $"User {(string.IsNullOrWhiteSpace(user.Username) ? user.LoginName : user.Username)} deleted.";
        return RedirectToAction("Index");
    }

    [HttpGet]
    public async Task<IActionResult> Logs([FromQuery] UploadLogFilterModel? filter, int page = 1)
    {
        filter ??= new UploadLogFilterModel();

        var query = new UploadScanLogQuery
        {
            EventType = "ThreadImage",
            Source = filter.Origin,
            ScanStatus = filter.Status,
            Blocked = filter.Blocked,
            Username = filter.Username,
            SessionId = filter.SessionId,
            Threat = filter.Threat,
            FromDateUtc = filter.From?.ToUniversalTime(),
            ToDateUtc = filter.To?.ToUniversalTime()
        };

        var result = await _uploadScanLogService.GetLogsAsync(query, page, 40);
        var uploads = new UploadLogsViewModel
        {
            Entries = result.Items,
            Filter = filter,
            Page = result.Page,
            TotalPages = result.TotalPages,
            TotalCount = result.TotalCount
        };

        var postLog = await _postLogService.GetRecentPostsAsync(60);
        var visitLog = await _visitTracking.GetRecentVisitsAsync(160);
        var now = DateTime.UtcNow;

        var postModels = postLog
            .Select(p => new PostLogEntryModel(
                p.PostId,
                p.ThreadId,
                p.ThreadTitle,
                p.CreatedAt,
                FormatPostActor(p),
                p.IsAnonymous,
                p.IsDeleted,
                p.ReportCount,
                p.WasModerated,
                p.Snippet,
                p.ImageCount))
            .ToList();

        var visitModels = visitLog
            .Select(v => new VisitLogEntryModel(
                v.Id,
                v.VisitedAt,
                v.Path,
                FormatEventLabel(v.EventType),
                FormatVisitActor(v),
                BuildSessionLabel(v),
                v.UserAgent,
                now.Subtract(v.VisitedAt) <= TimeSpan.FromMinutes(1)))
            .ToList();

        var viewModel = new AdminLogsPageViewModel
        {
            Uploads = uploads,
            PostLog = postModels,
            VisitLog = visitModels,
            GeneratedAtUtc = now,
            AutoRefreshSeconds = 30
        };

        ViewBag.PageSize = result.PageSize;
        return View(viewModel);
    }
        string FormatPostActor(PostLogRecord entry)
        {
            if (!string.IsNullOrWhiteSpace(entry.Username))
            {
                return entry.UserId.HasValue
                    ? $"{entry.Username} (#{entry.UserId})"
                    : entry.Username;
            }

            return entry.IsAnonymous ? "Anonymous" : "Unknown";
        }

        static string FormatEventLabel(string eventType)
        {
            return eventType?.Equals("link-click", StringComparison.OrdinalIgnoreCase) == true
                ? "Link Click"
                : "Page Load";
        }

        static string BuildSessionLabel(VisitLogRecord visit)
        {
            if (!string.IsNullOrWhiteSpace(visit.SessionFingerprint))
            {
                return $"visitor-{visit.SessionFingerprint}";
            }

            return $"visitor-{visit.Id:X}";
        }

        static string FormatVisitActor(VisitLogRecord visit)
        {
            if (!string.IsNullOrWhiteSpace(visit.UsernameSnapshot))
            {
                return visit.UserId.HasValue
                    ? $"{visit.UsernameSnapshot} (#{visit.UserId})"
                    : visit.UsernameSnapshot;
            }

            if (visit.UserId.HasValue)
            {
                return $"user #{visit.UserId}";
            }

            if (!string.IsNullOrWhiteSpace(visit.SessionFingerprint))
            {
                return $"visitor-{visit.SessionFingerprint}";
            }

            return $"visitor-{visit.Id:X}";
        }
}
