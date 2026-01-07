using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using MyFace.Core.Entities;
using MyFace.Services.ProfileTemplates;
using MyFace.Web.Models.ProfileTemplates;

namespace MyFace.Web.Controllers;

[Authorize]
[Route("profile")]
public class ProfileTemplateController : Controller
{
    private readonly IProfileTemplateService _templateService;
    private readonly ILogger<ProfileTemplateController> _logger;

    public ProfileTemplateController(IProfileTemplateService templateService, ILogger<ProfileTemplateController> logger)
    {
        _templateService = templateService;
        _logger = logger;
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var snapshot = await _templateService.GetProfileAsync(userId.Value, cancellationToken);
        return Ok(ProfileSnapshotResponse.FromSnapshot(snapshot));
    }

    [HttpPost("template/select")]
    public async Task<IActionResult> SelectTemplate([FromBody] SelectTemplateRequest? request, CancellationToken cancellationToken)
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

        try
        {
            await _templateService.UpdateTemplateAsync(userId.Value, request.Template, userId, cancellationToken);
            var snapshot = await _templateService.GetProfileAsync(userId.Value, cancellationToken);
            return Ok(ProfileSnapshotResponse.FromSnapshot(snapshot));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Template selection failed for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("theme/apply")]
    public async Task<IActionResult> ApplyTheme([FromBody] ApplyThemeRequest? request, CancellationToken cancellationToken)
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

        await _templateService.ApplyThemeAsync(userId.Value, request.Preset, request.Overrides, userId, cancellationToken);
        var settings = await _templateService.GetProfileAsync(userId.Value, cancellationToken);
        return Ok(ProfileSnapshotResponse.FromSnapshot(settings));
    }

    [HttpGet("panels")]
    public async Task<IActionResult> GetPanels([FromQuery] ProfileTemplate? template, [FromQuery] bool includeHidden = false, CancellationToken cancellationToken = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        var panels = await _templateService.GetPanelsAsync(userId.Value, template, includeHidden, cancellationToken);
        return Ok(panels.Select(ProfilePanelDto.FromEntity).ToList());
    }

    [HttpPost("panel/create")]
    public async Task<IActionResult> CreatePanel([FromBody] CreatePanelRequest? request, CancellationToken cancellationToken)
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

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var panel = await _templateService.CreatePanelAsync(userId.Value, request.PanelType, request.Content, request.ContentFormat, userId, cancellationToken);
            return Ok(ProfilePanelDto.FromEntity(panel));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Create panel failed for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("panel/{panelId:int}")]
    public async Task<IActionResult> UpdatePanel(int panelId, [FromBody] UpdatePanelRequest? request, CancellationToken cancellationToken)
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

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var panel = await _templateService.UpdatePanelAsync(userId.Value, panelId, request.Content, request.ContentFormat, userId, cancellationToken);
            return Ok(ProfilePanelDto.FromEntity(panel));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Update panel failed for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("panel/{panelId:int}")]
    public async Task<IActionResult> DeletePanel(int panelId, CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId == null)
        {
            return Unauthorized();
        }

        try
        {
            await _templateService.DeletePanelAsync(userId.Value, panelId, cancellationToken);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Delete panel failed for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("panel/{panelId:int}/toggle")]
    public async Task<IActionResult> TogglePanel(int panelId, [FromBody] TogglePanelVisibilityRequest? request, CancellationToken cancellationToken)
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

        try
        {
            await _templateService.TogglePanelVisibilityAsync(userId.Value, panelId, request.IsVisible, userId, cancellationToken);
            var panels = await _templateService.GetPanelsAsync(userId.Value, null, true, cancellationToken);
            var updated = panels.FirstOrDefault(p => p.Id == panelId);
            if (updated == null)
            {
                return NotFound();
            }

            return Ok(ProfilePanelDto.FromEntity(updated));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Toggle panel failed for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("panel/{panelId:int}/reorder")]
    public async Task<IActionResult> ReorderPanel(int panelId, [FromBody] ReorderPanelRequest? request, CancellationToken cancellationToken)
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

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var positions = new Dictionary<int, int>
        {
            [panelId] = request.Position
        };

        try
        {
            await _templateService.ReorderPanelsAsync(userId.Value, positions, cancellationToken);
            var panels = await _templateService.GetPanelsAsync(userId.Value, null, true, cancellationToken);
            return Ok(panels.Select(ProfilePanelDto.FromEntity).ToList());
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Reorder panel failed for user {UserId}", userId);
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("template/{template}/panels")]
    public IActionResult GetPanelTypes(ProfileTemplate template)
    {
        var types = _templateService.GetPanelTypesForTemplate(template);
        return Ok(types);
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
        {
            return null;
        }

        return int.TryParse(userIdClaim.Value, out var userId) ? userId : null;
    }
}
