using System;
using System.IO;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Services.CustomHtml;
using MyFace.Web.Models.CustomHtml;
using MyFace.Web.Services;

namespace MyFace.Web.Controllers;

[Authorize]
[Route("profile/custom-html")]
public class CustomHtmlController : Controller
{
    private readonly UserService _userService;
    private readonly ICustomHtmlStorageService _storageService;
    private readonly CustomHtmlStudioService _studioService;

    public CustomHtmlController(
        UserService userService,
        ICustomHtmlStorageService storageService,
        CustomHtmlStudioService studioService)
    {
        _userService = userService;
        _storageService = storageService;
        _studioService = studioService;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload([FromBody] CustomHtmlTextUploadRequest? request, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        if (request == null)
        {
            return BadRequest(new { error = "Request payload is required." });
        }

        var user = await _userService.GetByIdAsync(userId.Value);
        if (user == null)
        {
            return NotFound();
        }

        var result = await _studioService.UploadInlineAsync(
            user,
            request.Html ?? string.Empty,
            userId.Value,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);
        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }

    [HttpGet("status")]
    public async Task<IActionResult> Status(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _userService.GetByIdAsync(userId.Value);
        if (user == null)
        {
            return NotFound();
        }

        var status = await _studioService.GetStatusAsync(user, cancellationToken);
        return Ok(status);
    }

    [HttpGet("preview")]
    public async Task<IActionResult> Preview(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _userService.GetByIdAsync(userId.Value);
        if (user == null)
        {
            return NotFound();
        }

        var username = NormalizeUsername(user.Username, user.Id);
        var fileInfo = await _storageService.GetInfoAsync(user.Id, username, cancellationToken);
        if (fileInfo == null || !fileInfo.Exists || string.IsNullOrEmpty(fileInfo.AbsolutePath) || !System.IO.File.Exists(fileInfo.AbsolutePath))
        {
            return NotFound();
        }

        var html = await System.IO.File.ReadAllTextAsync(fileInfo.AbsolutePath, cancellationToken);
        ApplySandboxHeaders();
        return Content(html, "text/html", Encoding.UTF8);
    }

    [HttpDelete]
    public async Task<IActionResult> Delete(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var user = await _userService.GetByIdAsync(userId.Value);
        if (user == null)
        {
            return NotFound();
        }

        var status = await _studioService.DeleteAsync(user, userId.Value, cancellationToken);
        return Ok(status);
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

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return null;
        }

        return int.TryParse(userIdClaim.Value, out var value) ? value : null;
    }
}
