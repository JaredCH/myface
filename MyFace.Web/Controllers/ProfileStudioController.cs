using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MyFace.Core.Entities;
using MyFace.Services;
using MyFace.Services.ProfileTemplates;
using MyFace.Web.Models.ProfileTemplates;
using MyFace.Web.Services;

namespace MyFace.Web.Controllers;

[Authorize]
[Route("profile/studio")]
public class ProfileStudioController : Controller
{
    private readonly UserService _userService;
    private readonly IProfileTemplateService _templateService;
    private readonly CustomHtmlStudioService _customHtmlService;

    private static readonly IReadOnlyDictionary<ProfilePanelType, string> PanelTypeLabels = new Dictionary<ProfilePanelType, string>
    {
        [ProfilePanelType.Summary] = "Summary hero",
        [ProfilePanelType.About] = "About section",
        [ProfilePanelType.Contact] = "Contact channels",
        [ProfilePanelType.Activity] = "Activity feed",
        [ProfilePanelType.Projects] = "Project showcase",
        [ProfilePanelType.Skills] = "Skills grid",
        [ProfilePanelType.Testimonials] = "Testimonials",
        [ProfilePanelType.Shop] = "Shop inventory",
        [ProfilePanelType.Policies] = "Store policies",
        [ProfilePanelType.Payments] = "Payment options",
        [ProfilePanelType.References] = "References",
        [ProfilePanelType.CustomBlock1] = "Custom block A",
        [ProfilePanelType.CustomBlock2] = "Custom block B",
        [ProfilePanelType.CustomBlock3] = "Custom block C",
        [ProfilePanelType.CustomBlock4] = "Custom block D"
    };

    private static readonly IReadOnlyList<ThemePresetViewModel> ThemePresetOptions = new List<ThemePresetViewModel>
    {
        new("classic-light", "Classic light", "#f6f7fb", "#1f2933", "#2f6fe4"),
        new("midnight", "Midnight", "#0f172a", "#e5e7eb", "#8b5cf6"),
        new("forest", "Forest", "#0f2214", "#e3f4e3", "#34d399"),
        new("sunrise", "Sunrise", "#2b1b12", "#fef3c7", "#f59e0b"),
        new("ocean", "Ocean", "#0b1f2a", "#e0f2fe", "#06b6d4"),
        new("ember", "Ember", "#111827", "#e5e7eb", "#f97316"),
        new("pastel", "Pastel", "#f5f3ff", "#312e81", "#a5b4fc"),
        new("slate", "Slate", "#0f172a", "#e2e8f0", "#38bdf8"),
        new("mono", "Mono", "#0b0b0f", "#f8fafc", "#ffffff")
    };

    public ProfileStudioController(UserService userService, IProfileTemplateService templateService, CustomHtmlStudioService customHtmlService)
    {
        _userService = userService;
        _templateService = templateService;
        _customHtmlService = customHtmlService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var viewModel = await BuildViewModel(user, cancellationToken);

        ViewBag.HideSidebars = true;
        ViewData["Title"] = "Profile Studio";
        return View("Index", viewModel);
    }

    [HttpPost("template")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SelectTemplate(TemplateSelectionForm form, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            SetError("Choose a template before submitting.");
            return RedirectToAction(nameof(Index));
        }

        if (form.Template == ProfileTemplate.CustomHtml)
        {
            var status = await _customHtmlService.GetStatusAsync(user, cancellationToken);
            if (status == null || !status.HasCustomHtml)
            {
                SetError("Upload custom HTML before enabling that mode.");
                return RedirectToAction(nameof(Index));
            }
        }

        try
        {
            await _templateService.UpdateTemplateAsync(user.Id, form.Template, user.Id, cancellationToken);
            SetSuccess("Template updated.");
        }
        catch (InvalidOperationException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("panel/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePanel(PanelCreateForm form, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            SetError("Fill in content before creating a panel.");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _templateService.CreatePanelAsync(user.Id, form.PanelType, form.Content, form.ContentFormat, user.Id, cancellationToken);
            SetSuccess("Panel created.");
        }
        catch (InvalidOperationException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("panel/update")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePanel(PanelEditForm form, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            SetError("Review the panel content and try again.");
            return RedirectToAction(nameof(Index));
        }

        var panels = await _templateService.GetPanelsAsync(user.Id, null, includeHidden: true, cancellationToken);
        var panel = panels.FirstOrDefault(p => p.Id == form.PanelId);
        if (panel == null)
        {
            SetError("Panel not found.");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var updated = await _templateService.UpdatePanelAsync(user.Id, panel.Id, form.Content, form.ContentFormat, user.Id, cancellationToken);

            if (panel.IsVisible != form.IsVisible)
            {
                await _templateService.TogglePanelVisibilityAsync(user.Id, panel.Id, form.IsVisible, user.Id, cancellationToken);
            }

            if (panel.Position != form.Position && form.Position > 0)
            {
                var positions = new Dictionary<int, int> { [panel.Id] = form.Position };
                await _templateService.ReorderPanelsAsync(user.Id, positions, cancellationToken);
            }

            SetSuccess($"Panel \"{GetPanelLabel(updated.PanelType)}\" saved.");
        }
        catch (InvalidOperationException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("panel/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePanel(PanelDeleteForm form, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            SetError("Panel identifier missing.");
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _templateService.DeletePanelAsync(user.Id, form.PanelId, cancellationToken);
            SetSuccess("Panel deleted.");
        }
        catch (InvalidOperationException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("theme/apply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyTheme(ThemeForm form, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        var overrides = form.BuildOverrides();
        var preset = string.IsNullOrWhiteSpace(form.Preset) ? null : form.Preset.Trim();

        try
        {
            await _templateService.ApplyThemeAsync(user.Id, preset, overrides.Count > 0 ? overrides : null, user.Id, cancellationToken);
            SetSuccess("Theme updated.");
        }
        catch (InvalidOperationException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("custom-html/upload")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UploadCustomHtml(CustomHtmlForm form, CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        if (!ModelState.IsValid)
        {
            SetError("Paste HTML before saving.");
            return RedirectToAction(nameof(Index));
        }

        var result = await _customHtmlService.UploadInlineAsync(
            user,
            form.Html,
            user.Id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            cancellationToken);

        if (!result.Success)
        {
            SetError(result.Errors.FirstOrDefault() ?? "Unable to save custom HTML.");
        }
        else
        {
            SetSuccess("Custom HTML saved and sanitized.");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("custom-html/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCustomHtml(CancellationToken cancellationToken)
    {
        var user = await GetCurrentUserAsync();
        if (user == null)
        {
            return RedirectToAction("Login", "Account");
        }

        try
        {
            await _customHtmlService.DeleteAsync(user, user.Id, cancellationToken);
            SetSuccess("Custom HTML removed.");
        }
        catch (InvalidOperationException ex)
        {
            SetError(ex.Message);
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<User?> GetCurrentUserAsync()
    {
        var userId = GetCurrentUserId();
        return userId == null ? null : await _userService.GetByIdAsync(userId.Value);
    }

    private async Task<ProfileStudioViewModel> BuildViewModel(User user, CancellationToken cancellationToken)
    {
        var snapshot = await _templateService.GetProfileAsync(user.Id, cancellationToken);
        var dto = ProfileSnapshotResponse.FromSnapshot(snapshot);
        var fallbackTokens = TemplateThemeTokenHelper.FromUser(user);
        var allowedPanels = _templateService.GetPanelTypesForTemplate(dto.Settings.TemplateType);
        var usedPanels = dto.Panels.Select(p => p.PanelType).ToHashSet();
        var panelOptions = allowedPanels
            .Select(type => new PanelTypeOptionViewModel(type, GetPanelLabel(type), !usedPanels.Contains(type)))
            .ToList();

        var customHtmlStatus = await _customHtmlService.GetStatusAsync(user, cancellationToken);
        TemplateRenderViewModel? previewModel = null;
        if (!dto.Settings.IsCustomHtml)
        {
            var displayName = string.IsNullOrWhiteSpace(user.Username) ? $"user-{user.Id}" : user.Username;
            var payload = new PublicProfileResponse(
                displayName,
                displayName,
                false,
                "studio-preview",
                null,
                dto);
            previewModel = TemplateRenderViewModelFactory.Create(payload, fallbackTokens);
        }

        return new ProfileStudioViewModel
        {
            Username = user.Username ?? $"user-{user.Id}",
            Snapshot = dto,
            ThemeTokens = TemplateThemeTokens.Create(dto.Theme, fallbackTokens),
            PanelTypeOptions = panelOptions,
            PanelLabels = PanelTypeLabels,
            ThemePresets = ThemePresetOptions,
            CustomHtmlStatus = customHtmlStatus,
            PreviewProfile = previewModel,
            SuccessMessage = TempData["StudioSuccess"] as string,
            ErrorMessage = TempData["StudioError"] as string
        };
    }

    private static string GetPanelLabel(ProfilePanelType panelType)
    {
        return PanelTypeLabels.TryGetValue(panelType, out var label) ? label : panelType.ToString();
    }

    private void SetSuccess(string message) => TempData["StudioSuccess"] = message;

    private void SetError(string message) => TempData["StudioError"] = message;

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
