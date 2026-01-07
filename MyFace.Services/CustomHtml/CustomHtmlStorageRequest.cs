namespace MyFace.Services.CustomHtml;

public sealed record CustomHtmlStorageRequest(
    int UserId,
    string Username,
    string SanitizedHtml,
    int OutputByteCount,
    int NewVersion,
    string? OriginalFileName,
    string? SourceAddress);
