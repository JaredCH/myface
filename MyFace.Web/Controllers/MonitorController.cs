using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Models;
using MyFace.Web.Services;
using System;
using System.Linq;

namespace MyFace.Web.Controllers;

public class MonitorController : Controller
{
    private readonly OnionStatusService _statusService;
    private readonly MonitorLogService _monitorLog;

    public MonitorController(OnionStatusService statusService, MonitorLogService monitorLog)
    {
        _statusService = statusService;
        _monitorLog = monitorLog;
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
}
