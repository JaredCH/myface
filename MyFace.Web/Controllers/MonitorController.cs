using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Models;
using MyFace.Web.Models.ControlPanel;
using MyFace.Web.Services;
using MyFace.Data;
using MyFace.Core.Entities;
using System;
using System.Linq;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace MyFace.Web.Controllers;

public class MonitorController : Controller
{
    private readonly OnionStatusService _statusService;
    private readonly MonitorLogService _monitorLog;
    private readonly ApplicationDbContext _context;

    public MonitorController(OnionStatusService statusService, MonitorLogService monitorLog, ApplicationDbContext context)
    {
        _statusService = statusService;
        _monitorLog = monitorLog;
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        await _statusService.EnsureSeedDataAsync();
        var monitors = await _statusService.GetAllAsync();
        return View(monitors);
    }

    [HttpGet]
    public IActionResult Log()
    {
        var entries = _monitorLog
            .GetEntries()
            .OrderByDescending(e => e.Timestamp)
            .Take(1000)
            .ToList();
        return View(entries);
    }

    [HttpGet("/monitor/go/{id}")]
    public async Task<IActionResult> Go(int id)
    {
        var target = await _statusService.RegisterClickAsync(id);
        if (string.IsNullOrWhiteSpace(target))
        {
            return NotFound();
        }

        return Redirect(target);
    }

    [HttpGet]
    [MyFace.Web.Services.AdminAuthorization]
    public IActionResult Add(string? returnUrl)
    {
        ViewBag.ReturnUrl = GetSafeReturnUrl(returnUrl);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> Add(AddMonitorViewModel model, string? returnUrl)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ReturnUrl = GetSafeReturnUrl(returnUrl);
            return View(model);
        }

        static string NormalizeSignedUrlKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return string.Empty;
            }

            var trimmed = raw.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[7..];
            }
            else if (trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[8..];
            }

            return trimmed.TrimEnd('/').ToLowerInvariant();
        }

        if (!string.IsNullOrWhiteSpace(model.PgpSignedMessages))
        {
            var extracted = PgpSignedOnionExtractor.Extract(model.PgpSignedMessages);
            if (extracted.Count == 0)
            {
                ModelState.AddModelError(nameof(model.PgpSignedMessages), "No signed onion URLs detected.");
                ViewBag.ReturnUrl = GetSafeReturnUrl(returnUrl);
                return View(model);
            }

            foreach (var entry in extracted.DistinctBy(e => NormalizeSignedUrlKey(e.OnionUrl)))
            {
                await _statusService.AddAsync(model.Name, model.Description, entry.OnionUrl, entry.ProofContent);
            }
        }
        else if (!string.IsNullOrWhiteSpace(model.OnionUrl))
        {
            await _statusService.AddAsync(model.Name, model.Description, model.OnionUrl);
        }

        return RedirectBack(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> Check(int id, string? returnUrl)
    {
        await _statusService.CheckAsync(id, HttpContext.RequestAborted);
        return RedirectBack(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> CheckAll(string? returnUrl)
    {
        await _statusService.CheckAllAsync(HttpContext.RequestAborted);
        return RedirectBack(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> Remove(int id, string? returnUrl)
    {
        await _statusService.RemoveAsync(id);
        return RedirectBack(returnUrl);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> RemoveMultiple(int[] ids, string? returnUrl)
    {
        if (ids != null && ids.Length > 0)
        {
            foreach (var id in ids)
            {
                await _statusService.RemoveAsync(id);
            }
        }
        return RedirectBack(returnUrl);
    }

    [HttpGet]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> Edit(int id, string? returnUrl)
    {
        var monitor = await _statusService.GetByIdAsync(id);
        if (monitor == null) return NotFound();
        
        var model = new AddMonitorViewModel
        {
            Name = monitor.Name,
            Description = monitor.Description,
            OnionUrl = monitor.OnionUrl
        };
        ViewBag.Id = id;
        ViewBag.ReturnUrl = GetSafeReturnUrl(returnUrl);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> Edit(int id, AddMonitorViewModel model, string? returnUrl)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Id = id;
            ViewBag.ReturnUrl = GetSafeReturnUrl(returnUrl);
            return View(model);
        }

        await _statusService.UpdateAsync(id, model.Name, model.Description, model.OnionUrl ?? string.Empty);
        return RedirectBack(returnUrl);
    }

    [HttpGet("/monitor/proof/{proofId}")]
    public async Task<IActionResult> Proof(int proofId)
    {
        var proof = await _statusService.GetProofByIdAsync(proofId);
        if (proof == null)
        {
            return NotFound();
        }

        var model = new MonitorProofViewModel
        {
            ServiceName = proof.OnionStatus?.Name ?? $"Monitor #{proof.OnionStatusId}",
            OnionUrl = proof.OnionStatus?.OnionUrl ?? string.Empty,
            ProofContent = proof.Content,
            CreatedAtUtc = proof.CreatedAt
        };

        return View(model);
    }

    private IActionResult RedirectBack(string? returnUrl)
    {
        var target = GetSafeReturnUrl(returnUrl);
        if (!string.IsNullOrEmpty(target))
        {
            return Redirect(target);
        }

        return RedirectToAction("Index");
    }

    [HttpGet("~/SigilStaff/MonitorQueue")]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> Rollup()
    {
        await _statusService.EnsureSeedDataAsync();
        var allMonitors = await _statusService.GetAllAsync();
        
        // Group by normalized key to show consolidated services
        var grouped = allMonitors
            .Where(m => !string.IsNullOrEmpty(m.NormalizedKey))
            .GroupBy(m => m.NormalizedKey + "|" + m.Description)
            .Select(g => new
            {
                CanonicalName = g.FirstOrDefault(m => !m.IsMirror)?.CanonicalName ?? g.First().Name,
                Category = g.First().Description,
                PrimaryService = g.FirstOrDefault(m => !m.IsMirror),
                Mirrors = g.Where(m => m.IsMirror).ToList(),
                TotalClicks = g.Where(m => !m.IsMirror).Sum(m => m.ClickCount),
                ServiceCount = g.Count()
            })
            .OrderByDescending(g => g.TotalClicks)
            .ThenBy(g => g.CanonicalName)
            .ToList();

        ViewBag.TotalServices = allMonitors.Count;
        ViewBag.PrimaryServices = allMonitors.Count(m => !m.IsMirror);
        ViewBag.MirrorServices = allMonitors.Count(m => m.IsMirror);
        ViewBag.ServicesWithMirrors = grouped.Count(g => g.Mirrors.Any());
        
        // Get pending submissions
        var pendingSubmissions = await _context.OnionSubmissions
            .Where(s => s.Status == "Pending")
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();
        
        ViewBag.PendingSubmissions = pendingSubmissions;
        ViewBag.HideSidebars = true;
        ViewData["Title"] = "Sigil Staff · Monitor Queue";
        ViewData["ControlPanelActive"] = "monitor";
        ViewData["ControlPanelRole"] = "Administrator";
        ViewData["ControlPanelNav"] = BuildNavigation("monitor", true);
        
        return View(grouped);
    }

    [HttpGet]
    public IActionResult Submit()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(AddMonitorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // Sanitize inputs
        var sanitizedName = SanitizeInput(model.Name);
        if (string.IsNullOrWhiteSpace(sanitizedName) || sanitizedName.Length > 200)
        {
            ModelState.AddModelError(nameof(model.Name), "Invalid service name.");
            return View(model);
        }

        // Sanitize description (optional)
        var sanitizedDescription = string.IsNullOrWhiteSpace(model.Description) 
            ? "Uncategorized" 
            : SanitizeInput(model.Description);
        if (sanitizedDescription.Length > 500)
        {
            sanitizedDescription = sanitizedDescription.Substring(0, 500);
        }

        // Require PGP signed message
        if (string.IsNullOrWhiteSpace(model.PgpSignedMessages))
        {
            ModelState.AddModelError(nameof(model.PgpSignedMessages), "PGP signed message is required.");
            return View(model);
        }

        // Basic validation that it looks like a PGP signed message
        var pgpContent = model.PgpSignedMessages.Trim();
        if (!pgpContent.Contains("-----BEGIN PGP SIGNED MESSAGE-----") || 
            !pgpContent.Contains("-----BEGIN PGP SIGNATURE-----"))
        {
            ModelState.AddModelError(nameof(model.PgpSignedMessages), "Invalid PGP format. Must contain a PGP signed message with signature.");
            return View(model);
        }

        // Extract onion URL from signed message content
        var onionUrl = ExtractOnionUrlFromSignedMessage(pgpContent);
        if (string.IsNullOrWhiteSpace(onionUrl))
        {
            ModelState.AddModelError(nameof(model.PgpSignedMessages), "No onion URL found in signed message.");
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var username = User.Identity?.Name;

        // Store the signed submission for admin review
        var submission = new OnionSubmission
        {
            Name = sanitizedName,
            Description = sanitizedDescription,
            OnionUrl = onionUrl,
            PgpSignedMessage = pgpContent,
            SubmittedByUserId = int.TryParse(userId, out var uid) ? uid : null,
            SubmittedByUsername = username
        };
        
        _context.OnionSubmissions.Add(submission);
        await _context.SaveChangesAsync();

        TempData["Message"] = $"✓ Success! Your submission for '{sanitizedName}' has been received and is pending admin review. You will be notified once it's approved.";
        return RedirectToAction("Submit");
    }

    private static string SanitizeInput(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        // Remove potentially dangerous characters but keep basic punctuation
        var sanitized = input.Trim();
        sanitized = System.Text.RegularExpressions.Regex.Replace(sanitized, @"[<>""'`\\]", "");
        return sanitized;
    }

    private static string? ExtractOnionUrlFromSignedMessage(string pgpSignedMessage)
    {
        // Extract content between BEGIN PGP SIGNED MESSAGE and BEGIN PGP SIGNATURE
        var lines = pgpSignedMessage.Split('\n');
        var inContent = false;
        
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            
            if (trimmed.StartsWith("-----BEGIN PGP SIGNED MESSAGE-----"))
            {
                inContent = true;
                continue;
            }
            
            if (trimmed.StartsWith("-----BEGIN PGP SIGNATURE-----"))
            {
                break;
            }
            
            // Skip hash line and empty lines
            if (!inContent || string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith("Hash:"))
            {
                continue;
            }
            
            // Look for onion URL
            if (trimmed.EndsWith(".onion", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }
        }
        
        return null;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> ApproveSubmission(int id, string? name, string? category)
    {
        var submission = await _context.OnionSubmissions.FindAsync(id);
        if (submission == null || submission.Status != "Pending")
        {
            return NotFound();
        }

        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var reviewerName = User.Identity?.Name;

        // Use edited name and category if provided
        var finalName = !string.IsNullOrWhiteSpace(name) ? SanitizeInput(name) : submission.Name;
        var finalCategory = !string.IsNullOrWhiteSpace(category) ? SanitizeInput(category) : submission.Description;

        submission.Status = "Approved";
        submission.ReviewedAt = DateTime.UtcNow;
        submission.ReviewedByUserId = int.TryParse(reviewerId, out var rid) ? rid : null;
        submission.ReviewedByUsername = reviewerName;
        submission.Name = finalName;
        submission.Description = finalCategory;

        // Add to OnionStatus
        if (!string.IsNullOrWhiteSpace(submission.PgpSignedMessage))
        {
            await _statusService.AddAsync(finalName, finalCategory, submission.OnionUrl, submission.PgpSignedMessage);
        }
        else
        {
            await _statusService.AddAsync(finalName, finalCategory, submission.OnionUrl);
        }

        await _context.SaveChangesAsync();

        TempData["Message"] = $"Approved submission: {submission.Name}";
        return RedirectToAction("Rollup");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> DenySubmission(int id, string? reason)
    {
        var submission = await _context.OnionSubmissions.FindAsync(id);
        if (submission == null || submission.Status != "Pending")
        {
            return NotFound();
        }

        var reviewerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var reviewerName = User.Identity?.Name;

        submission.Status = "Denied";
        submission.ReviewedAt = DateTime.UtcNow;
        submission.ReviewedByUserId = int.TryParse(reviewerId, out var rid) ? rid : null;
        submission.ReviewedByUsername = reviewerName;
        submission.ReviewNotes = reason;

        await _context.SaveChangesAsync();

        TempData["Message"] = $"Denied submission: {submission.Name}";
        return RedirectToAction("Rollup");
    }

    private static string? ExtractFingerprint(string? pgpContent)
    {
        if (string.IsNullOrWhiteSpace(pgpContent))
        {
            return null;
        }

        var lines = pgpContent.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("fingerprint", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Key ID", StringComparison.OrdinalIgnoreCase))
            {
                var parts = line.Split(':', '=');
                if (parts.Length > 1)
                {
                    return parts[1].Trim();
                }
            }
        }
        return null;
    }

    private string? GetSafeReturnUrl(string? candidate = null)
    {
        if (!string.IsNullOrWhiteSpace(candidate) && Url.IsLocalUrl(candidate))
        {
            return candidate;
        }

        var referer = Request.Headers.Referer.ToString();
        if (!string.IsNullOrWhiteSpace(referer) && Uri.TryCreate(referer, UriKind.Absolute, out var refererUri))
        {
            var localPath = refererUri.PathAndQuery;
            if (Url.IsLocalUrl(localPath))
            {
                return localPath;
            }
        }

        return null;
    }

    private IReadOnlyList<ControlPanelNavLink> BuildNavigation(string activeKey, bool isAdmin)
    {
        // Moderator links (alphabetical)
        var moderatorLinks = new List<ControlPanelNavLink>
        {
            new("chat", "Chat", "/SigilStaff/Chat"),
            new("content", "Content", "/SigilStaff/Content"),
            new("dashboard", "Dashboard", "/SigilStaff"),
            new("users", "Users", "/SigilStaff/Users"),
            new("usercontrol", "UserControl", "/SigilStaff/UserControl")
        };

        // Admin-only links (alphabetical)
        var adminLinks = new List<ControlPanelNavLink>
        {
            new("infractions", "Infractions", "/SigilStaff/Infractions", requiresAdmin: true),
            new("monitor", "Monitor Queue", "/SigilStaff/MonitorQueue", requiresAdmin: true),
            new("overview", "Overview", "/SigilStaff/Overview", requiresAdmin: true),
            new("security", "Security", "/SigilStaff/Security", requiresAdmin: true),
            new("settings", "Settings", "/SigilStaff/Settings", requiresAdmin: true),
            new("traffic", "Traffic", "/SigilStaff/Traffic", requiresAdmin: true),
            new("wordlist", "Word Filters", "/SigilStaff/WordList", requiresAdmin: true)
        };

        // Combine lists: moderator first, then admin
        var items = new List<ControlPanelNavLink>();
        items.AddRange(moderatorLinks);
        if (isAdmin)
        {
            items.AddRange(adminLinks);
        }

        foreach (var link in items)
        {
            link.IsActive = string.Equals(link.Key, activeKey, StringComparison.OrdinalIgnoreCase);
        }

        return items;
    }
}
