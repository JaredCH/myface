using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using MyFace.Services.CustomHtml;

namespace MyFace.Web.Models.CustomHtml;

public sealed record CustomHtmlStatusResponse(
    bool HasCustomHtml,
    bool TemplateActive,
    bool ValidationPassed,
    IReadOnlyList<string> ValidationErrors,
    string? RelativePath,
    DateTime? UploadedAt,
    int Version,
    bool FileAvailable,
    IReadOnlyList<string> BackupPaths);

public sealed record CustomHtmlUploadResponse(
    bool Success,
    IReadOnlyList<string> Errors,
    CustomHtmlStatusResponse Status,
    HtmlDiagnostics Diagnostics);

public sealed record CustomHtmlTextUploadRequest
{
    private const int MaxInlineBytes = 512_000;

    [Required]
    [MaxLength(MaxInlineBytes)]
    public string Html { get; init; } = string.Empty;
}

public sealed record HtmlDiagnostics(
    int InputBytes,
    int OutputBytes,
    int NodeCount,
    bool InputLimitExceeded,
    bool OutputLimitExceeded,
    bool NodeLimitExceeded)
{
    public static HtmlDiagnostics FromResult(HtmlSanitizationResult result)
    {
        return new HtmlDiagnostics(
            result.InputBytes,
            result.OutputBytes,
            result.NodeCount,
            result.InputLimitExceeded,
            result.OutputLimitExceeded,
            result.NodeLimitExceeded);
    }
}
