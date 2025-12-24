using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Services;
using MyFace.Web.Models;

namespace MyFace.Web.Controllers;

public class ChatController : Controller
{
    private readonly ChatService _chatService;
    private readonly ChatSnapshotService _snapshotService;
    private readonly MyFace.Data.ApplicationDbContext _db;

    public ChatController(ChatService chatService, ChatSnapshotService snapshotService, MyFace.Data.ApplicationDbContext db)
    {
        _chatService = chatService;
        _snapshotService = snapshotService;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();

        var model = new ChatIndexViewModel
        {
            CurrentUser = user,
            PublicCanPost = _chatService.CanPostToRoom(user, ChatService.RoomPublic),
            VerifiedCanPost = _chatService.CanPostToRoom(user, ChatService.RoomVerified),
            SigilShortsCanPost = _chatService.CanPostToRoom(user, ChatService.RoomSigilShorts),
            PublicPaused = _chatService.IsRoomPaused(ChatService.RoomPublic),
            VerifiedPaused = _chatService.IsRoomPaused(ChatService.RoomVerified),
            SigilShortsPaused = _chatService.IsRoomPaused(ChatService.RoomSigilShorts)
        };

        return View(model);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Messages(string room)
    {
        if (!_chatService.IsValidRoom(room)) return BadRequest("Unknown room");

        var user = await GetCurrentUserAsync();
        if (!_chatService.CanViewRoom(user, room)) return Forbid();

        await _chatService.EnsureSchemaAsync();
        var html = await _snapshotService.GetSnapshotAsync(room);
        return View("Messages", new ChatMessagesViewModel
        {
            Room = room,
            SnapshotHtml = html,
            Paused = _chatService.IsRoomPaused(room)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string room, string content)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            TempData["ChatError"] = "Login required to post.";
            return RedirectToAction("Index");
        }

        var result = await _chatService.PostMessageAsync(user, room, content);
        if (!result.Ok)
        {
            TempData[$"ChatError_{room}"] = result.Error;
        }
        else
        {
            TempData[$"ChatInfo_{room}"] = "Message sent.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var user = await GetCurrentUserAsync();
        if (user == null || !_chatService.IsModeratorOrAdmin(user)) return Forbid();

        var result = await _chatService.DeleteMessageAsync(id, user);
        if (!result.Ok)
        {
            TempData["ChatError_Admin"] = result.Error;
        }
        else if (!string.IsNullOrEmpty(result.Room))
        {
            TempData[$"ChatInfo_{result.Room}"] = "Message deleted.";
        }

        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Pause(string room)
    {
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (!IsModeratorOrAdmin(userRole)) return Forbid();
        if (!_chatService.IsValidRoom(room)) return BadRequest();
        _chatService.SetRoomPaused(room, true);
        TempData[$"ChatInfo_{room}"] = "Room paused.";
        _snapshotService.Invalidate(room);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Resume(string room)
    {
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (!IsModeratorOrAdmin(userRole)) return Forbid();
        if (!_chatService.IsValidRoom(room)) return BadRequest();
        _chatService.SetRoomPaused(room, false);
        TempData[$"ChatInfo_{room}"] = "Room resumed.";
        _snapshotService.Invalidate(room);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Mute(string username, int minutes)
    {
        var userRole = User.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
        if (!IsModeratorOrAdmin(userRole)) return Forbid();
        if (string.IsNullOrWhiteSpace(username) || minutes <= 0) return RedirectToAction("Index");

        var target = await _db.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.ToLower());
        if (target != null)
        {
            _chatService.MuteUser(target.Id, TimeSpan.FromMinutes(minutes));
            TempData["ChatInfo_Admin"] = $"Muted {target.Username} for {minutes} minutes.";
        }
        else
        {
            TempData["ChatError_Admin"] = "User not found.";
        }

        return RedirectToAction("Index");
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(idClaim)) return null;
        if (!int.TryParse(idClaim, out var id)) return null;
        return await _db.Users
            .Include(u => u.PGPVerifications)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    private static bool IsModeratorOrAdmin(string role)
    {
        var r = role.ToLowerInvariant();
        return r == "admin" || r == "moderator";
    }
}
