using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyFace.Core.Entities;
using MyFace.Services;
using MyFace.Services.CustomHtml;
using MyFace.Services.ProfileTemplates;
using MyFace.Web.Models.ProfileTemplates;

namespace MyFace.Web.Controllers;

[AllowAnonymous]
[Route("u")]
public class ProfileDisplayController : Controller
{
    private readonly UserService _userService;
    private readonly IProfileTemplateService _templateService;
    private readonly ICustomHtmlStorageService _storageService;
    private readonly ILogger<ProfileDisplayController> _logger;

    public ProfileDisplayController(
        UserService userService,
        IProfileTemplateService templateService,
        ICustomHtmlStorageService storageService,
        ILogger<ProfileDisplayController> logger)
    {
        _userService = userService;
        _templateService = templateService;
        _storageService = storageService;
        _logger = logger;
    }

    [HttpGet("{username}")]
    public async Task<IActionResult> ViewProfile(string username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return BadRequest(new { error = "Username is required." });
        }

        var user = await _userService.GetByUsernameAsync(username);
        if (user == null)
        {
            return NotFound();
        }

        var snapshot = await _templateService.GetProfileAsync(user.Id, cancellationToken);
        var slug = NormalizeUsername(user.Username, user.Id);
        var fileInfo = await _storageService.GetInfoAsync(user.Id, slug, cancellationToken);

        var usesCustomHtml = snapshot.Settings.IsCustomHtml
            && !string.IsNullOrWhiteSpace(snapshot.Settings.CustomHtmlPath)
            && fileInfo?.Exists == true;

        var mode = usesCustomHtml ? "customHtml" : "template";
        var payload = new PublicProfileResponse(
            user.Username ?? slug,
            user.Username ?? slug,
            usesCustomHtml,
            mode,
            usesCustomHtml ? fileInfo?.RelativePath : null,
            ProfileSnapshotResponse.FromSnapshot(snapshot));

        if (WantsJson())
        {
            return Ok(payload);
        }

        ViewBag.HideSidebars = true;
        ViewData["Title"] = $"{payload.DisplayName} · Profile";

        if (usesCustomHtml)
        {
            var iframeUrl = Url.Action(nameof(RenderCustomHtml), new { username = slug }) ?? $"/u/{slug}/profile.html";
            var warningText = "⚠️ This profile contains user-supplied HTML and is not endorsed. Be suspicious of any phishing attempts, bold claims, or exaggerated promises.";
            var subtitle = "Custom HTML runs inside a locked-down iframe with scripts removed, network access blocked, and styles sanitized.";
            var customViewModel = new CustomHtmlProfileViewModel
            {
                Username = payload.Username,
                DisplayName = payload.DisplayName,
                ProfileUrl = iframeUrl,
                WarningText = warningText,
                Subtitle = subtitle
            };
            return View("CustomHtmlProfile", customViewModel);
        }

        var fallback = TemplateThemeTokenHelper.FromUser(user);
        var viewModel = TemplateRenderViewModelFactory.Create(payload, fallback);
        return View("TemplateProfile", viewModel);
    }

    [HttpGet("{username}/profile.html")]
    public async Task<IActionResult> RenderCustomHtml(string username, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return NotFound();
        }

        var user = await _userService.GetByUsernameAsync(username);
        if (user == null)
        {
            return NotFound();
        }

        var snapshot = await _templateService.GetProfileAsync(user.Id, cancellationToken);
        if (!snapshot.Settings.IsCustomHtml || string.IsNullOrWhiteSpace(snapshot.Settings.CustomHtmlPath))
        {
            return NotFound();
        }

        var slug = NormalizeUsername(user.Username, user.Id);
        var fileInfo = await _storageService.GetInfoAsync(user.Id, slug, cancellationToken);
        if (fileInfo == null || !fileInfo.Exists || string.IsNullOrWhiteSpace(fileInfo.AbsolutePath) || !System.IO.File.Exists(fileInfo.AbsolutePath))
        {
            return NotFound();
        }

        try
        {
            var html = await System.IO.File.ReadAllTextAsync(fileInfo.AbsolutePath, cancellationToken);
            ApplySandboxHeaders();
            return Content(html, "text/html", Encoding.UTF8);
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to read custom HTML for user {UserId}", user.Id);
            return StatusCode(500, new { error = "Unable to load profile HTML at this time." });
        }
    }

    private static string NormalizeUsername(string? username, int fallbackId)
    {
        return string.IsNullOrWhiteSpace(username) ? $"user-{fallbackId}" : username;
    }

    private void ApplySandboxHeaders()
    {
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; img-src 'self' data:; style-src 'self'; font-src 'none'; media-src 'none'; frame-src 'none'; object-src 'none'; base-uri 'none'; form-action 'self'";
        Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Referrer-Policy"] = "no-referrer";
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
        Response.Headers["Pragma"] = "no-cache";
    }

    private bool WantsJson()
    {
        if (Request.Query.TryGetValue("format", out var format) && string.Equals(format, "json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var accept = Request.Headers["Accept"].ToString();
        return !string.IsNullOrWhiteSpace(accept) && accept.IndexOf("application/json", StringComparison.OrdinalIgnoreCase) >= 0;
    }

}
