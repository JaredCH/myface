namespace MyFace.Services.CustomHtml;

public class CustomHtmlStorageOptions
{
    /// <summary>
    /// Physical directory (absolute or relative to WebRoot) where custom HTML files are stored.
    /// </summary>
    public string RootDirectory { get; set; } = "user-html";

    /// <summary>
    /// Public request path prefix (e.g. "/u") used when constructing iframe URLs.
    /// </summary>
    public string RequestPathPrefix { get; set; } = "/u";

    /// <summary>
    /// File name written for the active profile HTML.
    /// </summary>
    public string FileName { get; set; } = "profile.html";

    /// <summary>
    /// Folder (under the user directory) used to keep versioned backups.
    /// </summary>
    public string BackupFolderName { get; set; } = "versions";

    /// <summary>
    /// Maximum number of backup versions to retain per user.
    /// </summary>
    public int MaxVersionsPerUser { get; set; } = 5;

    /// <summary>
    /// Maximum number of bytes we are willing to write to disk for each HTML document.
    /// </summary>
    public int MaxWriteBytes { get; set; } = 512_000;

    /// <summary>
    /// When enabled, overwrite files with zeros before deletion.
    /// </summary>
    public bool EnableSecureDelete { get; set; } = true;
}
