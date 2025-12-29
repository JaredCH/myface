namespace MyFace.Web.Services;

public sealed record ImageUploadContext(
    int? UserId,
    string? Username,
    bool IsAnonymous,
    string SessionId,
    string? IpAddressHash,
    string Source,
    string? UserAgent);
