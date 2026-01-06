using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using MyFace.Core.Entities;
using MyFace.Services;
using MyFace.Web.Models.ControlPanel;
using MyFace.Web.Services;

namespace MyFace.Web.Controllers;

[Route("control-panel")]
[Route("ControlPanel")]
[AdminAuthorization]
public class ControlPanelController : Controller
{
    private readonly VisitTrackingService _visitTrackingService;
    private readonly ControlPanelAuditService _auditService;
    private readonly IMemoryCache _memoryCache;
    private readonly ControlPanelDataService _dataService;
    private readonly ChatService _chatService;
    private readonly UserService _userService;
    private readonly ForumService _forumService;
    private readonly ControlSettingsReader _settingsReader;
    private readonly ControlSettingsService _controlSettingsService;

    public ControlPanelController(
        VisitTrackingService visitTrackingService,
        ControlPanelAuditService auditService,
        IMemoryCache memoryCache,
        ControlPanelDataService dataService,
        ChatService chatService,
        UserService userService,
        ForumService forumService,
        ControlSettingsReader settingsReader,
        ControlSettingsService controlSettingsService)
    {
        _visitTrackingService = visitTrackingService;
        _auditService = auditService;
        _memoryCache = memoryCache;
        _dataService = dataService;
        _chatService = chatService;
        _userService = userService;
        _forumService = forumService;
        _settingsReader = settingsReader;
        _controlSettingsService = controlSettingsService;
    }

    [HttpGet("")]
    [HttpGet("index")]
    [HttpGet("dashboard")]
    public Task<IActionResult> Index() => RenderSectionAsync(
        "dashboard",
        "Dashboard",
        viewName: "Index",
        refreshSettingKey: ControlSettingKeys.RefreshDashboard,
        fallbackSeconds: 30);

    [HttpGet("traffic")]
    public async Task<IActionResult> Traffic()
    {
        var isAdmin = IsAdmin(User);
        var analytics = await _visitTrackingService.GetTrafficAnalyticsAsync(isAdmin);
        var nav = BuildNavigation("traffic", isAdmin);
        ViewBag.HideSidebars = true;
        ViewData["Title"] = "Control Panel · Traffic";
        ViewData["ControlPanelNav"] = nav;
        ViewData["ControlPanelActive"] = "traffic";
        ViewData["ControlPanelRole"] = isAdmin ? "Administrator" : "Moderator";
        ViewData["MetaRefreshSeconds"] = await GetRefreshAsync(ControlSettingKeys.RefreshTraffic, 120);
        var vm = new TrafficAnalyticsViewModel(analytics, isAdmin);
        return View("Traffic", vm);
    }

    [HttpGet("content")]
    public async Task<IActionResult> Content([FromQuery] string? range)
    {
        var isAdmin = IsAdmin(User);
        var selectedRange = ContentMetricsRangeExtensions.Normalize(range);
        var metrics = await _dataService.GetContentMetricsAsync(isAdmin, selectedRange);
        var nav = BuildNavigation("content", isAdmin);
        ViewBag.HideSidebars = true;
        ViewData["Title"] = "Control Panel · Content";
        ViewData["ControlPanelNav"] = nav;
        ViewData["ControlPanelActive"] = "content";
        ViewData["ControlPanelRole"] = isAdmin ? "Administrator" : "Moderator";
        ViewData["MetaRefreshSeconds"] = await GetRefreshAsync(ControlSettingKeys.RefreshContent, 180);
        var vm = new ContentMetricsViewModel(metrics, selectedRange);
        return View("Content", vm);
    }

    [HttpGet("users")]
    public async Task<IActionResult> Users()
    {
        var isAdmin = IsAdmin(User);
        var data = await _dataService.GetUserEngagementAsync(isAdmin);
        var nav = BuildNavigation("users", isAdmin);
        ViewBag.HideSidebars = true;
        ViewData["Title"] = "Control Panel · Users";
        ViewData["ControlPanelNav"] = nav;
        ViewData["ControlPanelActive"] = "users";
        ViewData["ControlPanelRole"] = isAdmin ? "Administrator" : "Moderator";
        ViewData["MetaRefreshSeconds"] = await GetRefreshAsync(ControlSettingKeys.RefreshUsers, 300);
        var vm = new UserEngagementViewModel(data);
        return View("Users", vm);
    }

    [HttpGet("users/manage")]
    public async Task<IActionResult> ManageUser([FromQuery] int? userId, [FromQuery] string? username)
    {
        var isAdmin = IsAdmin(User);
        var resolvedId = userId;
        if (!resolvedId.HasValue && !string.IsNullOrWhiteSpace(username))
        {
            var lookup = await _userService.GetByUsernameAsync(username.Trim());
            resolvedId = lookup?.Id;
            ViewData["LookupUsername"] = username.Trim();
            if (!resolvedId.HasValue)
            {
                TempData["Error"] = "User not found.";
            }
        }

        UserManagementDetail? detail = null;
        IReadOnlyList<ControlPanelAuditEntry> auditEntries = Array.Empty<ControlPanelAuditEntry>();
        if (resolvedId.HasValue)
        {
            detail = await _dataService.GetUserManagementDetailAsync(resolvedId.Value, HttpContext.RequestAborted);
            if (detail == null)
            {
                TempData["Error"] = "Unable to load the requested user.";
            }
            else
            {
                var targetKey = $"user:{detail.User.UserId}";
                auditEntries = await _auditService.GetRecentForTargetAsync(targetKey, 50, HttpContext.RequestAborted);
            }
        }

        var nav = BuildNavigation("users", isAdmin);
        ViewBag.HideSidebars = true;
        ViewData["Title"] = "Control Panel · Manage User";
        ViewData["ControlPanelNav"] = nav;
        ViewData["ControlPanelActive"] = "users";
        ViewData["ControlPanelRole"] = isAdmin ? "Administrator" : "Moderator";
        ViewData["MetaRefreshSeconds"] = 0;

        var vm = new UserManagementViewModel(detail, auditEntries, isAdmin);
        return View("ManageUser", vm);
    }

    [HttpPost("users/manage/suspend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SuspendUser([FromForm] UserSuspendInput input)
    {
        if (input == null || input.UserId <= 0)
        {
            TempData["Error"] = "Invalid user.";
            return RedirectToAction(nameof(ManageUser));
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            TempData["Error"] = "A reason is required.";
            return RedirectToManage(input.UserId);
        }

        var isAdmin = IsAdmin(User);
        var maxMinutes = isAdmin ? 43200 : 10080; // 30 days vs 7 days
        var minutes = Math.Clamp(input.Minutes, 5, maxMinutes);
        var until = DateTime.UtcNow.AddMinutes(minutes);
        await _userService.SuspendUserAsync(input.UserId, until);
        await LogAuditAsync("user.suspend", $"userId={input.UserId};minutes={minutes};reason={input.Reason}", $"user:{input.UserId}");
        TempData["Success"] = $"Suspended user for {minutes} minutes.";
        return RedirectToManage(input.UserId);
    }

    [HttpPost("users/manage/unsuspend")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnsuspendUser([FromForm] UserReasonInput input)
    {
        if (input == null || input.UserId <= 0)
        {
            TempData["Error"] = "Invalid user.";
            return RedirectToAction(nameof(ManageUser));
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            TempData["Error"] = "A reason is required.";
            return RedirectToManage(input.UserId);
        }

        await _userService.SuspendUserAsync(input.UserId, null);
        await LogAuditAsync("user.unsuspend", $"userId={input.UserId};reason={input.Reason}", $"user:{input.UserId}");
        TempData["Success"] = "Suspension cleared.";
        return RedirectToManage(input.UserId);
    }

    [OnlyAdminAuthorization]
    [HttpPost("users/manage/activation")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetActivation([FromForm] UserActivationInput input)
    {
        if (input == null || input.UserId <= 0)
        {
            TempData["Error"] = "Invalid user.";
            return RedirectToAction(nameof(ManageUser));
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            TempData["Error"] = "A reason is required.";
            return RedirectToManage(input.UserId);
        }

        var updated = await _userService.SetActiveStateAsync(input.UserId, input.IsActive);
        if (!updated)
        {
            TempData["Error"] = "Unable to update user.";
            return RedirectToManage(input.UserId);
        }

        var verb = input.IsActive ? "user.activate" : "user.deactivate";
        await LogAuditAsync(verb, $"userId={input.UserId};reason={input.Reason}", $"user:{input.UserId}");
        TempData["Success"] = input.IsActive ? "User reactivated." : "User deactivated.";
        return RedirectToManage(input.UserId);
    }

    [OnlyAdminAuthorization]
    [HttpPost("users/manage/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword([FromForm] UserPasswordResetInput input)
    {
        if (input == null || input.UserId <= 0)
        {
            TempData["Error"] = "Invalid user.";
            return RedirectToAction(nameof(ManageUser));
        }

        if (string.IsNullOrWhiteSpace(input.NewPassword) || input.NewPassword.Length < 10)
        {
            TempData["Error"] = "Password must be at least 10 characters.";
            return RedirectToManage(input.UserId);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            TempData["Error"] = "A reason is required.";
            return RedirectToManage(input.UserId);
        }

        var updated = await _userService.AdminSetPasswordAsync(input.UserId, input.NewPassword);
        if (!updated)
        {
            TempData["Error"] = "Unable to reset password.";
            return RedirectToManage(input.UserId);
        }

        await LogAuditAsync("user.password.reset", $"userId={input.UserId};reason={input.Reason}", $"user:{input.UserId}");
        TempData["Success"] = "Password reset.";
        return RedirectToManage(input.UserId);
    }

    [OnlyAdminAuthorization]
    [HttpPost("users/manage/force-username")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForceUsername([FromForm] UserUsernameInput input)
    {
        if (input == null || input.UserId <= 0)
        {
            TempData["Error"] = "Invalid user.";
            return RedirectToAction(nameof(ManageUser));
        }

        if (string.IsNullOrWhiteSpace(input.NewUsername) || input.NewUsername.Length < 3)
        {
            TempData["Error"] = "Provide the replacement username.";
            return RedirectToManage(input.UserId);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            TempData["Error"] = "A reason is required.";
            return RedirectToManage(input.UserId);
        }

        var (actorId, _, _) = GetActorContext();
        var success = await _userService.ChangeUsernameByAdminAsync(input.UserId, input.NewUsername.Trim(), actorId ?? 0, input.Reason);
        if (!success)
        {
            TempData["Error"] = "Unable to queue username change.";
            return RedirectToManage(input.UserId);
        }

        await LogAuditAsync("user.force-username", $"userId={input.UserId};new={input.NewUsername.Trim()};reason={input.Reason}", $"user:{input.UserId}");
        TempData["Success"] = "User must change username on next login.";
        return RedirectToManage(input.UserId);
    }

    [OnlyAdminAuthorization]
    [HttpPost("users/manage/set-role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetRole([FromForm] UserRoleInput input)
    {
        if (input == null || input.UserId <= 0)
        {
            TempData["Error"] = "Invalid user.";
            return RedirectToAction(nameof(ManageUser));
        }

        if (string.IsNullOrWhiteSpace(input.Role))
        {
            TempData["Error"] = "Select a role.";
            return RedirectToManage(input.UserId);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            TempData["Error"] = "A reason is required.";
            return RedirectToManage(input.UserId);
        }

        var normalized = input.Role.Trim();
        if (!string.Equals(normalized, "User", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalized, "Moderator", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(normalized, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Unsupported role.";
            return RedirectToManage(input.UserId);
        }

        var (_, _, actorRole) = GetActorContext();
        if (string.Equals(normalized, "Admin", StringComparison.OrdinalIgnoreCase) && !string.Equals(actorRole, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Only administrators may promote to admin.";
            return RedirectToManage(input.UserId);
        }

        await _userService.SetRoleAsync(input.UserId, normalized);
        await LogAuditAsync("user.role", $"userId={input.UserId};role={normalized};reason={input.Reason}", $"user:{input.UserId}");
        TempData["Success"] = "Role updated.";
        return RedirectToManage(input.UserId);
    }

    [OnlyAdminAuthorization]
    [HttpPost("users/manage/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser([FromForm] UserReasonInput input)
    {
        if (input == null || input.UserId <= 0)
        {
            TempData["Error"] = "Invalid user.";
            return RedirectToAction(nameof(ManageUser));
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            TempData["Error"] = "A reason is required.";
            return RedirectToManage(input.UserId);
        }

        var success = await _userService.DeleteUserAsync(input.UserId);
        if (!success)
        {
            TempData["Error"] = "Unable to delete user.";
            return RedirectToManage(input.UserId);
        }

        await LogAuditAsync("user.delete", $"userId={input.UserId};reason={input.Reason}", $"user:{input.UserId}");
        TempData["Success"] = "User deleted.";
        return RedirectToAction(nameof(Users));
    }

    [HttpPost("users/manage/thread-lock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetThreadLock([FromForm] ThreadLockInput input)
    {
        if (input == null || input.ThreadId <= 0)
        {
            TempData["Error"] = "Invalid thread.";
            return RedirectToAction(nameof(ManageUser), new { userId = input?.ReturnUserId });
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            TempData["Error"] = "A reason is required.";
            return RedirectToAction(nameof(ManageUser), new { userId = input.ReturnUserId });
        }

        var success = await _forumService.LockThreadAsync(input.ThreadId, input.Lock);
        if (!success)
        {
            TempData["Error"] = "Unable to update thread.";
        }
        else
        {
            var verb = input.Lock ? "thread.lock" : "thread.unlock";
            await LogAuditAsync(verb, $"threadId={input.ThreadId};reason={input.Reason}");
            TempData["Success"] = input.Lock ? "Thread locked." : "Thread unlocked.";
        }

        return RedirectToAction(nameof(ManageUser), new { userId = input.ReturnUserId });
    }

    [HttpPost("users/manage/post-delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePost([FromForm] PostModerationInput input)
    {
        if (input == null || input.PostId <= 0)
        {
            TempData["Error"] = "Invalid post.";
            return RedirectToAction(nameof(ManageUser), new { userId = input?.ReturnUserId });
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            TempData["Error"] = "A reason is required.";
            return RedirectToAction(nameof(ManageUser), new { userId = input.ReturnUserId });
        }

        var success = await _forumService.AdminDeletePostAsync(input.PostId);
        if (!success)
        {
            TempData["Error"] = "Unable to delete the post.";
        }
        else
        {
            await LogAuditAsync("post.delete", $"postId={input.PostId};reason={input.Reason}");
            TempData["Success"] = "Post removed.";
        }

        return RedirectToAction(nameof(ManageUser), new { userId = input.ReturnUserId });
    }

    [HttpPost("users/manage/post-hide")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HidePost([FromForm] PostModerationInput input)
    {
        if (input == null || input.PostId <= 0)
        {
            TempData["Error"] = "Invalid post.";
            return RedirectToAction(nameof(ManageUser), new { userId = input?.ReturnUserId });
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            TempData["Error"] = "A reason is required.";
            return RedirectToAction(nameof(ManageUser), new { userId = input.ReturnUserId });
        }

        var success = await _forumService.AdminHidePostAsync(input.PostId);
        if (!success)
        {
            TempData["Error"] = "Unable to hide the post.";
        }
        else
        {
            await LogAuditAsync("post.hide", $"postId={input.PostId};reason={input.Reason}");
            TempData["Success"] = "Post hidden from public feeds.";
        }

        return RedirectToAction(nameof(ManageUser), new { userId = input.ReturnUserId });
    }

    [HttpGet("chat")]
    public async Task<IActionResult> Chat()
    {
        var isAdmin = IsAdmin(User);
        var data = await _dataService.GetChatOversightAsync();
        var nav = BuildNavigation("chat", isAdmin);
        ViewBag.HideSidebars = true;
        ViewData["Title"] = "Control Panel · Chat";
        ViewData["ControlPanelNav"] = nav;
        ViewData["ControlPanelActive"] = "chat";
        ViewData["ControlPanelRole"] = isAdmin ? "Administrator" : "Moderator";
        ViewData["MetaRefreshSeconds"] = await GetRefreshAsync(ControlSettingKeys.RefreshChat, 120);
        var vm = new ChatOversightViewModel(data, isAdmin);
        return View("Chat", vm);
    }

    [OnlyAdminAuthorization]
    [HttpGet("security")]
    public async Task<IActionResult> Security()
    {
        var data = await _dataService.GetSecurityOverviewAsync();
        var nav = BuildNavigation("security", isAdmin: true);
        ViewBag.HideSidebars = true;
        ViewData["Title"] = "Control Panel · Security";
        ViewData["ControlPanelNav"] = nav;
        ViewData["ControlPanelActive"] = "security";
        ViewData["ControlPanelRole"] = "Administrator";
        ViewData["MetaRefreshSeconds"] = await GetRefreshAsync(ControlSettingKeys.RefreshSecurity, 180);
        var vm = new SecurityOverviewViewModel(data);
        return View("Security", vm);
    }

    [OnlyAdminAuthorization]
    [HttpGet("settings")]
    public async Task<IActionResult> Settings([FromQuery] string? highlight)
    {
        var details = await _controlSettingsService.GetAllAsync(HttpContext.RequestAborted);
        var grouped = ControlSettingsViewModel.From(details, highlight);
        var nav = BuildNavigation("settings", isAdmin: true);
        ViewBag.HideSidebars = true;
        ViewData["Title"] = "Control Panel · Settings";
        ViewData["ControlPanelNav"] = nav;
        ViewData["ControlPanelActive"] = "settings";
        ViewData["ControlPanelRole"] = "Administrator";
        ViewData["MetaRefreshSeconds"] = await GetRefreshAsync(ControlSettingKeys.RefreshSettings, 0);
        return View("Settings", grouped);
    }

    [HttpGet]
    [Route("/ControlPanel/ControlPanel")]
    public IActionResult LegacyControlPanel() => RedirectToAction(nameof(Index));

    [HttpPost("clear-cache")]
    [ValidateAntiForgeryToken]
    [OnlyAdminAuthorization]
    public async Task<IActionResult> ClearCache()
    {
        if (_memoryCache is MemoryCache concreteCache)
        {
            concreteCache.Compact(1.0);
        }
        await LogAuditAsync("cache.clear", null);
        TempData["Success"] = "Memory cache cleared.";
        return RedirectToAction("Index");
    }

    [HttpPost("export-metrics")]
    [ValidateAntiForgeryToken]
    [OnlyAdminAuthorization]
    public async Task<IActionResult> ExportMetrics()
    {
        var snapshot = await _visitTrackingService.GetControlPanelSnapshotAsync();
        var csv = BuildSnapshotCsv(snapshot);
        var fileName = $"control-panel-metrics-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        await LogAuditAsync("metrics.export", null);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    [HttpPost("traffic/export")]
    [ValidateAntiForgeryToken]
    [OnlyAdminAuthorization]
    public async Task<IActionResult> ExportTrafficAnalytics()
    {
        var analytics = await _visitTrackingService.GetTrafficAnalyticsAsync(includeSensitive: true);
        var csv = BuildTrafficCsv(analytics);
        var fileName = $"traffic-analytics-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        await LogAuditAsync("traffic.export", null);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    [HttpPost("content/export")]
    [ValidateAntiForgeryToken]
    [OnlyAdminAuthorization]
    public async Task<IActionResult> ExportContentMetrics([FromForm] string? range)
    {
        var selectedRange = ContentMetricsRangeExtensions.Normalize(range);
        var metrics = await _dataService.GetContentMetricsAsync(includeAdmin: true, selectedRange);
        var csv = BuildContentMetricsCsv(metrics);
        var fileName = $"content-metrics-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";
        await LogAuditAsync("content.export", null);
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }

    [HttpPost("chat/mute-user")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MuteChatUser([FromForm] string username, [FromForm] int minutes)
    {
        if (string.IsNullOrWhiteSpace(username) || minutes <= 0)
        {
            TempData["Error"] = "Username and duration are required.";
            return RedirectToAction(nameof(Chat));
        }

        var target = await _userService.GetByUsernameAsync(username.Trim());
        if (target == null)
        {
            TempData["Error"] = "User not found.";
            return RedirectToAction(nameof(Chat));
        }

        var isAdmin = IsAdmin(User);
        var maxMinutes = isAdmin ? 7 * 24 * 60 : 24 * 60;
        var allowedMinutes = Math.Clamp(minutes, 1, maxMinutes);
        _chatService.MuteUser(target.Id, TimeSpan.FromMinutes(allowedMinutes));
        await LogAuditAsync("chat.mute", $"userId={target.Id};minutes={allowedMinutes}");
        TempData["Success"] = $"Muted {target.Username} for {allowedMinutes} minutes.";
        return RedirectToAction(nameof(Chat));
    }

    [HttpPost("chat/unmute-user")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnmuteChatUser([FromForm] int userId)
    {
        if (userId <= 0)
        {
            TempData["Error"] = "Invalid user id.";
            return RedirectToAction(nameof(Chat));
        }

        var user = await _userService.GetByIdAsync(userId);
        if (!_chatService.TryUnmuteUser(userId))
        {
            TempData["Error"] = "User was not muted.";
            return RedirectToAction(nameof(Chat));
        }

        await LogAuditAsync("chat.unmute", $"userId={userId}");
        TempData["Success"] = user != null
            ? $"Unmuted {user.Username}."
            : "Mute cleared.";
        return RedirectToAction(nameof(Chat));
    }

    [HttpPost("chat/pause-room")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PauseChatRoom([FromForm] string room, [FromForm] int minutes)
    {
        if (string.IsNullOrWhiteSpace(room) || !_chatService.IsValidRoom(room))
        {
            TempData["Error"] = "Unknown room.";
            return RedirectToAction(nameof(Chat));
        }

        if (minutes <= 0)
        {
            TempData["Error"] = "Duration must be positive.";
            return RedirectToAction(nameof(Chat));
        }

        var isAdmin = IsAdmin(User);
        var maxMinutes = isAdmin ? 12 * 60 : 120;
        var allowedMinutes = Math.Clamp(minutes, 1, maxMinutes);
        _chatService.TryPauseRoom(room, TimeSpan.FromMinutes(allowedMinutes));
        await LogAuditAsync("chat.pause", $"room={room};minutes={allowedMinutes}");
        TempData["Success"] = $"Paused {room} for {allowedMinutes} minutes.";
        return RedirectToAction(nameof(Chat));
    }

    [HttpPost("chat/resume-room")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResumeChatRoom([FromForm] string room)
    {
        if (string.IsNullOrWhiteSpace(room) || !_chatService.IsValidRoom(room))
        {
            TempData["Error"] = "Unknown room.";
            return RedirectToAction(nameof(Chat));
        }

        if (!_chatService.TryResumeRoom(room))
        {
            TempData["Error"] = "Room was not paused.";
            return RedirectToAction(nameof(Chat));
        }

        await LogAuditAsync("chat.resume", $"room={room}");
        TempData["Success"] = $"Resumed {room}.";
        return RedirectToAction(nameof(Chat));
    }

    [HttpGet("audit-log")]
    [OnlyAdminAuthorization]
    public async Task<IActionResult> AuditLog()
    {
        var entries = await _auditService.GetRecentAsync();
        ViewBag.HideSidebars = true;
        ViewData["Title"] = "Control Panel · Audit Log";
        ViewData["ControlPanelNav"] = BuildNavigation("dashboard", true);
        ViewData["ControlPanelRole"] = "Administrator";
        ViewData["MetaRefreshSeconds"] = 0;
        return View("AuditLog", entries);
    }

    [OnlyAdminAuthorization]
    [HttpPost("settings/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateSetting([FromForm] ControlSettingUpdateInput input)
    {
        if (input == null || string.IsNullOrWhiteSpace(input.Key))
        {
            TempData["Error"] = "Setting key is required.";
            return RedirectToAction(nameof(Settings));
        }

        var trimmedKey = input.Key.Trim();
        var (actorId, actorUsername, _) = GetActorContext();

        try
        {
            await _controlSettingsService.UpdateAsync(trimmedKey, input.Value ?? string.Empty, actorId, actorUsername, input.Reason, HttpContext.RequestAborted);
            await LogAuditAsync("settings.update", $"key={trimmedKey}");
            TempData["Success"] = $"Updated {trimmedKey}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Settings), new { highlight = trimmedKey });
    }

    [OnlyAdminAuthorization]
    [HttpPost("settings/restore")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RestoreSetting([FromForm] ControlSettingRestoreInput input)
    {
        if (input == null || input.HistoryId <= 0)
        {
            TempData["Error"] = "A valid history entry is required.";
            return RedirectToAction(nameof(Settings));
        }

        ControlSettingDetail? restored = null;
        var (actorId, actorUsername, _) = GetActorContext();
        try
        {
            restored = await _controlSettingsService.RestoreFromHistoryAsync(input.HistoryId, actorId, actorUsername, input.Reason, HttpContext.RequestAborted);
            await LogAuditAsync("settings.restore", $"history={input.HistoryId};key={restored.Definition.Key}");
            TempData["Success"] = $"Restored {restored.Definition.Label} from history.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        var highlightKey = restored?.Definition.Key ?? string.Empty;
        return RedirectToAction(nameof(Settings), new { highlight = highlightKey });
    }

    private async Task<IActionResult> RenderSectionAsync(string key, string title, string viewName, string? placeholder = null, string? refreshSettingKey = null, int fallbackSeconds = 90)
    {
        var snapshot = await _visitTrackingService.GetControlPanelSnapshotAsync();
        var isAdmin = IsAdmin(User);
        var nav = BuildNavigation(key, isAdmin);

        ViewBag.HideSidebars = true;
        ViewData["Title"] = $"Control Panel · {title}";
        ViewData["ControlPanelNav"] = nav;
        ViewData["ControlPanelActive"] = key;
        ViewData["ControlPanelRole"] = isAdmin ? "Administrator" : "Moderator";

        var refreshInterval = await GetRefreshAsync(refreshSettingKey ?? ControlSettingKeys.RefreshDashboard, fallbackSeconds);
        var vm = new ControlPanelPageViewModel(key, title, snapshot, placeholder, refreshInterval, isAdmin);
        ViewData["MetaRefreshSeconds"] = refreshInterval;
        return View(viewName, vm);
    }

    private IActionResult RedirectToManage(int userId)
    {
        return RedirectToAction(nameof(ManageUser), new { userId });
    }

    private async Task<int> GetRefreshAsync(string settingKey, int fallback)
    {
        var value = await _settingsReader.GetIntAsync(settingKey, fallback, HttpContext.RequestAborted);
        return Math.Max(0, value);
    }

    private IReadOnlyList<ControlPanelNavLink> BuildNavigation(string activeKey, bool isAdmin)
    {
        var items = new List<ControlPanelNavLink>
        {
            new("dashboard", "Dashboard", Url.Action("Index", "ControlPanel") ?? "#"),
            new("traffic", "Traffic", Url.Action("Traffic", "ControlPanel") ?? "#"),
            new("content", "Content", Url.Action("Content", "ControlPanel") ?? "#"),
            new("users", "Users", Url.Action("Users", "ControlPanel") ?? "#"),
            new("chat", "Chat", Url.Action("Chat", "ControlPanel") ?? "#"),
            new("security", "Security", Url.Action("Security", "ControlPanel") ?? "#", requiresAdmin: true),
            new("monitor", "Monitor Queue", Url.Action("Rollup", "Monitor") ?? "#", requiresAdmin: true),
            new("moderator", "Moderation", Url.Action("Index", "Moderator") ?? "#"),
            new("admin", "Admin Panel", Url.Action("Index", "Admin") ?? "#", requiresAdmin: true),
            new("settings", "Settings", Url.Action("Settings", "ControlPanel") ?? "#", requiresAdmin: true)
        };

        var filtered = items
            .Where(link => isAdmin || !link.RequiresAdmin)
            .ToList();

        foreach (var link in filtered)
        {
            link.IsActive = string.Equals(link.Key, activeKey, StringComparison.OrdinalIgnoreCase);
        }

        return filtered;
    }

    private static bool IsAdmin(ClaimsPrincipal user)
    {
        var role = user.FindFirstValue(ClaimTypes.Role);
        return string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase);
    }

    private async Task LogAuditAsync(string action, string? details, string? target = null)
    {
        var (userId, username, role) = GetActorContext();
        await _auditService.LogAsync(userId, username, role, action, target, details);
    }

    private (int? UserId, string? Username, string Role) GetActorContext()
    {
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int? userId = null;
        if (int.TryParse(actorId, out var parsed))
        {
            userId = parsed;
        }

        var username = User.Identity?.Name;
        var role = User.FindFirstValue(ClaimTypes.Role) ?? "Unknown";
        return (userId, username, role);
    }

    private static string BuildSnapshotCsv(ControlPanelSnapshot snapshot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Metric,Value");
        sb.AppendLine($"GeneratedAtUTC,{snapshot.GeneratedAt:O}");
        sb.AppendLine($"LiveUsers15Minutes,{snapshot.LiveUsers15Minutes}");
        sb.AppendLine($"ActiveSessionsHour,{snapshot.ActiveSessionsHour}");
        sb.AppendLine($"UsersOnlineFourHours,{snapshot.UsersOnlineFourHours}");
        sb.AppendLine($"PeakUsersToday,{snapshot.PeakUsersToday}");
        sb.AppendLine($"PageViews24Hours,{snapshot.PageViews24Hours}");
        sb.AppendLine($"NewRegistrations24Hours,{snapshot.NewRegistrations24Hours}");
        sb.AppendLine($"ActiveThreads24Hours,{snapshot.ActiveThreads24Hours}");
        sb.AppendLine($"Posts24Hours,{snapshot.Posts24Hours}");
        sb.AppendLine($"ReportsPending,{snapshot.ReportsPending}");
        sb.AppendLine($"FailedLoginsHour,{snapshot.FailedLoginsHour}");
        sb.AppendLine($"DatabaseSizeBytes,{snapshot.DatabaseSizeBytes}");
        sb.AppendLine($"DiskFreeBytes,{snapshot.DiskFreeBytes}");
        sb.AppendLine($"WorkingSetBytes,{snapshot.WorkingSetBytes}");
        sb.AppendLine($"ManagedMemoryBytes,{snapshot.ManagedMemoryBytes}");
        return sb.ToString();
    }

    private static string BuildTrafficCsv(TrafficAnalyticsResult analytics)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Metric,Value");
        sb.AppendLine($"GeneratedAtUTC,{analytics.GeneratedAtUtc:O}");
        sb.AppendLine($"AverageSessionSeconds,{analytics.AverageSessionSeconds:0.##}");
        sb.AppendLine($"BounceRate,{analytics.BounceRate:P2}");
        sb.AppendLine($"AnonymousVisits,{analytics.AnonymousVisits}");
        sb.AppendLine($"AuthenticatedVisits,{analytics.AuthenticatedVisits}");
        sb.AppendLine($"NewUserVisits,{analytics.NewUserVisits}");
        sb.AppendLine($"ReturningUserVisits,{analytics.ReturningUserVisits}");

        AppendMetricSection(sb, "TopPages", analytics.TopPages);
        AppendMetricSection(sb, "EntryPages", analytics.EntryPages);
        AppendMetricSection(sb, "ExitPages", analytics.ExitPages);
        AppendMetricSection(sb, "Referrers", analytics.Referrers);
        AppendHourlySection(sb, analytics.HourlyCounts);

        return sb.ToString();
    }

    private static string BuildContentMetricsCsv(ContentMetricsResult metrics)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Metric,Value");
        sb.AppendLine($"Range,{ContentMetricsRangeExtensions.ToLabel(metrics.SelectedRange)}");
        sb.AppendLine($"RangeStartUTC,{metrics.RangeStartUtc:O}");
        sb.AppendLine($"ThreadsDay,{metrics.ThreadsDay}");
        sb.AppendLine($"ThreadsWeek,{metrics.ThreadsWeek}");
        sb.AppendLine($"ThreadsMonth,{metrics.ThreadsMonth}");
        sb.AppendLine($"PostsDay,{metrics.PostsDay}");
        sb.AppendLine($"PostsWeek,{metrics.PostsWeek}");
        sb.AppendLine($"PostsMonth,{metrics.PostsMonth}");
        sb.AppendLine($"ThreadsInRange,{metrics.ThreadsInRange}");
        sb.AppendLine($"PostsInRange,{metrics.PostsInRange}");
        sb.AppendLine($"AveragePostsPerThread,{metrics.AveragePostsPerThread:0.##}");
        sb.AppendLine($"VotesInRange,{metrics.VotesInRange}");
        sb.AppendLine($"ActivitiesInRange,{metrics.ActivitiesInRange}");
        sb.AppendLine($"ModerationActionsInRange,{metrics.ModerationActionsInRange}");
        sb.AppendLine($"VisibleReportsInRange,{metrics.VisibleReportsInRange}");
        sb.AppendLine($"HiddenReportsInRange,{metrics.HiddenReportsInRange}");
        sb.AppendLine($"ReportsPending,{metrics.ReportsPending}");
        sb.AppendLine($"AnonymousPostRatio,{metrics.AnonymousPostRatio:P2}");
        sb.AppendLine($"EditedPosts,{metrics.EditedPosts}");
        sb.AppendLine($"DeletedPosts,{metrics.DeletedPosts}");
        sb.AppendLine($"ModeratedPostsTotal,{metrics.ModeratedPosts}");
        sb.AppendLine($"ImagesUploadedDay,{metrics.ImagesUploadedDay}");
        sb.AppendLine($"UploadFailuresDay,{metrics.UploadFailuresDay}");
        sb.AppendLine($"MalwareDetectionsDay,{metrics.MalwareDetectionsDay}");

        var categories = metrics.TopCategories
            .Select(c => new MetricCount(c.Label, c.Count))
            .ToList();
        AppendMetricSection(sb, "TopCategories", categories);

        return sb.ToString();
    }

    private static void AppendMetricSection(StringBuilder sb, string title, IReadOnlyList<MetricCount> rows)
    {
        if (rows.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine(title);
        sb.AppendLine("Label,Count");
        foreach (var row in rows)
        {
            sb.AppendLine($"{EscapeCsv(row.Label)},{row.Count}");
        }
    }

    private static void AppendHourlySection(StringBuilder sb, IReadOnlyList<TrafficHourBucket> buckets)
    {
        if (buckets.Count == 0)
        {
            return;
        }

        sb.AppendLine();
        sb.AppendLine("HourlyCounts");
        sb.AppendLine("HourUtc,Count");
        foreach (var bucket in buckets)
        {
            sb.AppendLine($"{bucket.HourUtc:O},{bucket.Count}");
        }
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}

public class ControlSettingUpdateInput
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class ControlSettingRestoreInput
{
    public int HistoryId { get; set; }
    public string? Reason { get; set; }
}

public class UserSuspendInput
{
    public int UserId { get; set; }
    public int Minutes { get; set; }
    public string? Reason { get; set; }
}

public class UserReasonInput
{
    public int UserId { get; set; }
    public string? Reason { get; set; }
}

public class UserActivationInput
{
    public int UserId { get; set; }
    public bool IsActive { get; set; }
    public string? Reason { get; set; }
}

public class UserPasswordResetInput
{
    public int UserId { get; set; }
    public string NewPassword { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class UserUsernameInput
{
    public int UserId { get; set; }
    public string NewUsername { get; set; } = string.Empty;
    public string? Reason { get; set; }
}

public class UserRoleInput
{
    public int UserId { get; set; }
    public string Role { get; set; } = "User";
    public string? Reason { get; set; }
}

public class ThreadLockInput
{
    public int ThreadId { get; set; }
    public bool Lock { get; set; }
    public string? Reason { get; set; }
    public int? ReturnUserId { get; set; }
}

public class PostModerationInput
{
    public int PostId { get; set; }
    public string? Reason { get; set; }
    public int? ReturnUserId { get; set; }
}
