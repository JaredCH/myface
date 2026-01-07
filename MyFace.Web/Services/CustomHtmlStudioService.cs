using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MyFace.Core.Entities;
using MyFace.Data;
using MyFace.Services.CustomHtml;
using MyFace.Services.ProfileTemplates;
using MyFace.Web.Models.CustomHtml;

namespace MyFace.Web.Services;

public class CustomHtmlStudioService
{
    private const string InlineHtmlFileName = "inline.html";
    private readonly ApplicationDbContext _context;
    private readonly IProfileTemplateService _templateService;
    private readonly ICustomHtmlSanitizer _sanitizer;
    private readonly ICustomHtmlStorageService _storageService;
    private readonly ILogger<CustomHtmlStudioService> _logger;

    public CustomHtmlStudioService(
        ApplicationDbContext context,
        IProfileTemplateService templateService,
        ICustomHtmlSanitizer sanitizer,
        ICustomHtmlStorageService storageService,
        ILogger<CustomHtmlStudioService> logger)
    {
        _context = context;
        _templateService = templateService;
        _sanitizer = sanitizer;
        _storageService = storageService;
        _logger = logger;
    }

    public async Task<CustomHtmlUploadResponse> UploadInlineAsync(User user, string html, int editorUserId, string? sourceAddress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(html))
        {
            var emptyStatus = await BuildStatusAsync(user, cancellationToken);
            return new CustomHtmlUploadResponse(false, new[] { "Provide HTML content before saving." }, emptyStatus, EmptyDiagnostics());
        }

        HtmlSanitizationResult result;
        await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(html)))
        {
            result = await _sanitizer.SanitizeAsync(stream, cancellationToken);
        }

        var diagnostics = HtmlDiagnostics.FromResult(result);
        var settings = await _templateService.GetOrCreateSettingsAsync(user.Id, editorUserId, cancellationToken);

        if (!result.IsSuccess)
        {
            ApplyFailureMetadata(settings, result.Errors, editorUserId);
            await _context.SaveChangesAsync(cancellationToken);
            var failedStatus = await BuildStatusAsync(user, settings, null, null, cancellationToken);
            return new CustomHtmlUploadResponse(false, result.Errors, failedStatus, diagnostics);
        }

        var username = NormalizeUsername(user.Username, user.Id);
        var nextVersion = settings.CustomHtmlVersion + 1;

        var storageResult = await _storageService.SaveAsync(
            new CustomHtmlStorageRequest(
                user.Id,
                username,
                result.SanitizedHtml,
                result.OutputBytes,
                nextVersion,
                InlineHtmlFileName,
                sourceAddress),
            cancellationToken);

        ApplySuccessMetadata(settings, storageResult, editorUserId);
        await _context.SaveChangesAsync(cancellationToken);

        var status = await BuildStatusAsync(user, settings, storageResult, null, cancellationToken);
        _logger.LogInformation("Custom HTML saved for user {UserId} version {Version}", user.Id, storageResult.Version);
        return new CustomHtmlUploadResponse(true, Array.Empty<string>(), status, diagnostics);
    }

    public async Task<CustomHtmlStatusResponse> GetStatusAsync(User user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return await BuildStatusAsync(user, cancellationToken);
    }

    public async Task<CustomHtmlStatusResponse> DeleteAsync(User user, int editorUserId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        var settings = await _templateService.GetOrCreateSettingsAsync(user.Id, editorUserId, cancellationToken);
        var username = NormalizeUsername(user.Username, user.Id);
        await _storageService.DeleteAsync(user.Id, username, cancellationToken);

        settings.CustomHtmlPath = null;
        settings.CustomHtmlValidated = false;
        settings.CustomHtmlValidationErrors = null;
        settings.CustomHtmlUploadDate = null;
        settings.IsCustomHtml = false;
        if (settings.TemplateType == ProfileTemplate.CustomHtml)
        {
            settings.TemplateType = ProfileTemplate.Minimal;
        }

        settings.LastEditedAt = DateTime.UtcNow;
        settings.LastEditedByUserId = editorUserId;

        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Custom HTML removed for user {UserId}", user.Id);
        return BuildStatus(settings, storageResult: null, fileInfo: null);
    }

    private async Task<CustomHtmlStatusResponse> BuildStatusAsync(User user, CancellationToken cancellationToken)
    {
        var settings = await _templateService.GetOrCreateSettingsAsync(user.Id, user.Id, cancellationToken);
        var username = NormalizeUsername(user.Username, user.Id);
        var fileInfo = await _storageService.GetInfoAsync(user.Id, username, cancellationToken);
        return BuildStatus(settings, null, fileInfo);
    }

    private async Task<CustomHtmlStatusResponse> BuildStatusAsync(
        User user,
        UserProfileSettings settings,
        CustomHtmlStorageResult? storageResult,
        CustomHtmlFileInfo? fileInfo,
        CancellationToken cancellationToken)
    {
        var username = NormalizeUsername(user.Username, user.Id);
        var resolvedFileInfo = fileInfo ?? await _storageService.GetInfoAsync(user.Id, username, cancellationToken);
        return BuildStatus(settings, storageResult, resolvedFileInfo);
    }

    private static void ApplyFailureMetadata(UserProfileSettings settings, IReadOnlyList<string> errors, int editorUserId)
    {
        settings.CustomHtmlValidated = false;
        settings.CustomHtmlValidationErrors = string.Join('\n', errors);
        settings.LastEditedAt = DateTime.UtcNow;
        settings.LastEditedByUserId = editorUserId;
    }

    private static void ApplySuccessMetadata(UserProfileSettings settings, CustomHtmlStorageResult storageResult, int editorUserId)
    {
        settings.IsCustomHtml = true;
        settings.CustomHtmlPath = storageResult.RelativePath;
        settings.CustomHtmlUploadDate = storageResult.SavedAt.UtcDateTime;
        settings.CustomHtmlValidated = true;
        settings.CustomHtmlValidationErrors = null;
        settings.CustomHtmlVersion = storageResult.Version;
        settings.LastEditedAt = DateTime.UtcNow;
        settings.LastEditedByUserId = editorUserId;
    }

    private static CustomHtmlStatusResponse BuildStatus(UserProfileSettings settings, CustomHtmlStorageResult? storageResult, CustomHtmlFileInfo? fileInfo)
    {
        var relativePath = storageResult?.RelativePath ?? settings.CustomHtmlPath ?? fileInfo?.RelativePath;
        var uploadedAt = storageResult?.SavedAt.UtcDateTime ?? settings.CustomHtmlUploadDate;
        var backups = storageResult?.BackupRelativePaths ?? fileInfo?.BackupRelativePaths ?? Array.Empty<string>();
        var fileAvailable = storageResult != null || (fileInfo?.Exists ?? false);

        return new CustomHtmlStatusResponse(
            HasCustomHtml: settings.IsCustomHtml && !string.IsNullOrWhiteSpace(relativePath),
            TemplateActive: settings.TemplateType == ProfileTemplate.CustomHtml,
            ValidationPassed: settings.CustomHtmlValidated,
            ValidationErrors: SplitErrors(settings.CustomHtmlValidationErrors),
            RelativePath: relativePath,
            UploadedAt: uploadedAt,
            Version: settings.CustomHtmlVersion,
            FileAvailable: fileAvailable,
            BackupPaths: backups);
    }

    private static IReadOnlyList<string> SplitErrors(string? errors)
    {
        if (string.IsNullOrWhiteSpace(errors))
        {
            return Array.Empty<string>();
        }

        return errors
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToArray();
    }

    private static string NormalizeUsername(string? username, int fallbackId)
    {
        return string.IsNullOrWhiteSpace(username) ? $"user-{fallbackId}" : username;
    }

    private static HtmlDiagnostics EmptyDiagnostics()
    {
        return new HtmlDiagnostics(0, 0, 0, false, false, false);
    }
}
