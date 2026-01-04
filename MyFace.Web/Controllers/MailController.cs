using System.Security.Claims;
using System.Threading;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyFace.Core.Entities;
using MyFace.Data;
using MyFace.Services;
using MyFace.Web.Models;

namespace MyFace.Web.Controllers;

[Authorize]
public class MailController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly MailService _mailService;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);
    private static bool _schemaReady;

    public MailController(ApplicationDbContext db, MailService mailService)
    {
        _db = db;
        _mailService = mailService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string tab = "inbox", int? messageId = null, string to = "", string composeSubject = "", string composeBody = "")
    {
        await EnsureSchemaAsync();
        ViewBag.HideSidebars = true;
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var normalizedTab = NormalizeTab(tab);
        var isVerified = user.PGPVerifications.Any(v => v.Verified);
        var isMod = IsModeratorOrAdmin(user.Role);

        // Run sequentially to avoid parallel DbContext usage
        var inbox = await _mailService.GetInboxAsync(user.Id, 100);
        var outbox = await _mailService.GetOutboxAsync(user.Id, 100);
        var drafts = await _mailService.GetDraftsAsync(user.Id, 50);

        var model = new MailIndexViewModel
        {
            CurrentUser = user,
            IsVerified = isVerified,
            IsModeratorOrAdmin = isMod,
            ActiveTab = normalizedTab,
            Inbox = inbox.Select(ToItemView).ToList(),
            Outbox = outbox.Select(ToItemView).ToList(),
            Drafts = drafts.Select(ToItemView).ToList(),
            Compose = new MailComposeModel
            {
                To = to ?? string.Empty,
                Subject = composeSubject ?? string.Empty,
                Body = composeBody ?? string.Empty
            }
        };

        if (!string.Equals(normalizedTab, "new", StringComparison.OrdinalIgnoreCase))
        {
            var activeList = normalizedTab switch
            {
                "outbox" => model.Outbox,
                "drafts" => model.Drafts,
                _ => model.Inbox
            };

            var resolvedId = messageId ?? activeList.FirstOrDefault()?.Id;
            if (resolvedId.HasValue)
            {
                var detail = await _mailService.GetMessageAsync(resolvedId.Value, user.Id);
                if (detail != null)
                {
                    if (!detail.IsDraft && detail.RecipientId == user.Id)
                    {
                        await _mailService.MarkReadAsync(detail, user.Id);
                    }

                    model.SelectedMessage = ToViewMessageModel(detail, user);
                }
            }
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(string to, string subject, string body)
    {
        await EnsureSchemaAsync();
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var recipients = await ParseRecipientsAsync(user, to);
        if (!recipients.Ok)
        {
            TempData["MailError"] = recipients.Error;
            return RedirectToAction("Index", new { tab = "new", to });
        }

        var result = await _mailService.SendAsync(user, recipients.Users!, subject, body);
        if (!result.Ok)
        {
            TempData["MailError"] = result.Error;
        }
        else
        {
            TempData["MailInfo"] = $"Message sent to {result.Recipients} recipient(s).";
        }

        return RedirectToAction("Index", new { tab = "outbox" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft(string to, string subject, string body)
    {
        await EnsureSchemaAsync();
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var limitResult = await ParseRecipientsAsync(user, to, allowEmpty: true, allowMultiple: true);
        if (!limitResult.Ok)
        {
            TempData["MailError"] = limitResult.Error;
            return RedirectToAction("Index", new { tab = "new", to });
        }

        var recipient = limitResult.Users?.FirstOrDefault();
        var result = await _mailService.SaveDraftAsync(user, recipient, subject, body);
        if (!result.Ok)
        {
            TempData["MailError"] = result.Error;
        }
        else
        {
            TempData["MailInfo"] = "Draft saved.";
        }

        return RedirectToAction("Index", new { tab = "drafts" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendDraft(int id)
    {
        await EnsureSchemaAsync();
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var ok = await _mailService.SendDraftAsync(id, user.Id);
        TempData[ok ? "MailInfo" : "MailError"] = ok ? "Draft sent." : "Unable to send draft.";
        return RedirectToAction("Index", new { tab = "outbox" });
    }

    [HttpGet]
    public async Task<IActionResult> View(int id)
    {
        await EnsureSchemaAsync();
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var msg = await _mailService.GetMessageAsync(id, user.Id);
        if (msg == null) return NotFound();

        if (!msg.IsDraft)
        {
            await _mailService.MarkReadAsync(msg, user.Id);
        }

        var targetTab = ResolveTabForMessage(msg, user.Id);
        return RedirectToAction("Index", new { tab = targetTab, messageId = msg.Id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, string tab = "inbox")
    {
        await EnsureSchemaAsync();
        var user = await GetCurrentUserAsync();
        if (user == null) return RedirectToAction("Login", "Account");

        var message = await _mailService.GetMessageAsync(id, user.Id);
        if (message == null)
        {
            TempData["MailError"] = "Message not found.";
            return RedirectToAction("Index", new { tab });
        }

        var deleted = await _mailService.DeleteAsync(message, user.Id);
        TempData[deleted ? "MailInfo" : "MailError"] = deleted ? "Message deleted." : "Unable to delete message.";
        return RedirectToAction("Index", new { tab });
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var idClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(idClaim)) return null;
        if (!int.TryParse(idClaim, out var id)) return null;
        return await _db.Users.Include(u => u.PGPVerifications).FirstOrDefaultAsync(u => u.Id == id);
    }

    private static bool IsModeratorOrAdmin(string role)
    {
        var r = role?.ToLowerInvariant() ?? string.Empty;
        return r == "admin" || r == "moderator";
    }

    private async Task<(bool Ok, string? Error, List<User>? Users)> ParseRecipientsAsync(User sender, string to, bool allowEmpty = false, bool allowMultiple = true)
    {
        var raw = (to ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (raw.Count == 0)
        {
            return allowEmpty ? (true, null, new List<User>()) : (false, "Recipient required.", null);
        }

        var limit = sender.PGPVerifications.Any(v => v.Verified) ? 5 : 1;
        if (!allowMultiple && raw.Count > 1)
        {
            return (false, "Only one recipient allowed for drafts.", null);
        }
        if (raw.Count > limit)
        {
            return (false, $"Recipient limit is {limit} for your account.", null);
        }

        var users = await _db.Users.Where(u => raw.Contains(u.Username)).ToListAsync();
        if (users.Count != raw.Count)
        {
            return (false, "One or more recipients were not found.", null);
        }

        return (true, null, users);
    }

    private static MailItemViewModel ToItemView(PrivateMessage msg)
    {
        return new MailItemViewModel
        {
            Id = msg.Id,
            Subject = msg.Subject,
            From = msg.SenderUsernameSnapshot,
            To = msg.RecipientUsernameSnapshot,
            SentAt = msg.SentAt,
            ReadAt = msg.ReadAt,
            IsDraft = msg.IsDraft
        };
    }

    private static MailViewMessageModel ToViewMessageModel(PrivateMessage msg, User currentUser)
    {
        return new MailViewMessageModel
        {
            Id = msg.Id,
            Subject = msg.Subject,
            Body = msg.Body,
            From = msg.SenderUsernameSnapshot,
            To = msg.RecipientUsernameSnapshot,
            SentAt = msg.SentAt,
            ReadAt = msg.ReadAt,
            IsDraft = msg.IsDraft,
            CanSendDraft = msg.IsDraft && msg.SenderId == currentUser.Id && msg.RecipientId != null
        };
    }

    private static string NormalizeTab(string tab)
    {
        return string.IsNullOrWhiteSpace(tab) ? "inbox" : tab.Trim().ToLowerInvariant();
    }

    private static string ResolveTabForMessage(PrivateMessage msg, int currentUserId)
    {
        if (msg.IsDraft) return "drafts";
        return msg.SenderId == currentUserId ? "outbox" : "inbox";
    }

    private async Task EnsureSchemaAsync()
    {
        if (_schemaReady) return;
        await SchemaLock.WaitAsync();
        try
        {
            if (_schemaReady) return;
            await _mailService.EnsureSchemaAsync();
            _schemaReady = true;
        }
        finally
        {
            SchemaLock.Release();
        }
    }
}
