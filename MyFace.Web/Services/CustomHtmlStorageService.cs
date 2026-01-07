using System;
using System.Buffers;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyFace.Services;
using MyFace.Services.CustomHtml;

namespace MyFace.Web.Services;

public class CustomHtmlStorageService : ICustomHtmlStorageService
{
    private readonly IWebHostEnvironment _environment;
    private readonly IOptions<CustomHtmlStorageOptions> _options;
    private readonly ILogger<CustomHtmlStorageService> _logger;
    private readonly MonitorLogService _monitorLog;

    public CustomHtmlStorageService(
        IWebHostEnvironment environment,
        IOptions<CustomHtmlStorageOptions> options,
        ILogger<CustomHtmlStorageService> logger,
        MonitorLogService monitorLog)
    {
        _environment = environment;
        _options = options;
        _logger = logger;
        _monitorLog = monitorLog;
    }

    public async Task<CustomHtmlStorageResult> SaveAsync(CustomHtmlStorageRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Username))
        {
            throw new ArgumentException("Username is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.SanitizedHtml))
        {
            throw new ArgumentException("Sanitized HTML cannot be empty.", nameof(request));
        }

        var bytesToWrite = request.OutputByteCount > 0
            ? request.OutputByteCount
            : Encoding.UTF8.GetByteCount(request.SanitizedHtml);
        var byteLimit = Math.Clamp(_options.Value.MaxWriteBytes, 50_000, 1_000_000);
        if (bytesToWrite > byteLimit)
        {
            throw new InvalidOperationException($"Sanitized HTML exceeds the {byteLimit:N0}-byte storage cap.");
        }

        var paths = ResolvePaths(request.Username, request.UserId);
        Directory.CreateDirectory(paths.UserRoot);

        await BackupExistingAsync(paths.LiveFilePath, paths.BackupDirectory, request.NewVersion, cancellationToken);

        var tempPath = Path.Combine(paths.UserRoot, $".tmp-{Guid.NewGuid():N}.html");
        await File.WriteAllTextAsync(tempPath, request.SanitizedHtml, Encoding.UTF8, cancellationToken);
        ReplaceFile(tempPath, paths.LiveFilePath);

        var backups = await PruneBackupsAsync(paths, cancellationToken);
        var fileInfo = new FileInfo(paths.LiveFilePath);

        _monitorLog.Append($"Custom HTML v{request.NewVersion} saved for user #{request.UserId} ({request.Username}).");
        if (!string.IsNullOrWhiteSpace(request.OriginalFileName))
        {
            _logger.LogInformation(
                "Stored custom HTML version {Version} for user {UserId} ({Username}) from {FileName}",
                request.NewVersion,
                request.UserId,
                request.Username,
                request.OriginalFileName);
        }
        else
        {
            _logger.LogInformation(
                "Stored custom HTML version {Version} for user {UserId} ({Username})",
                request.NewVersion,
                request.UserId,
                request.Username);
        }

        return new CustomHtmlStorageResult(
            paths.RelativeRequestPath,
            paths.LiveFilePath,
            fileInfo.Length,
            request.NewVersion,
            DateTimeOffset.UtcNow,
            backups);
    }

    public async Task DeleteAsync(int userId, string username, CancellationToken cancellationToken = default)
    {
        var paths = ResolvePaths(username, userId);
        await SecureDeleteIfExistsAsync(paths.LiveFilePath, cancellationToken);

        if (Directory.Exists(paths.BackupDirectory))
        {
            foreach (var file in Directory.GetFiles(paths.BackupDirectory, "*.html", SearchOption.TopDirectoryOnly))
            {
                await SecureDeleteIfExistsAsync(file, cancellationToken);
            }

            try
            {
                Directory.Delete(paths.BackupDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete backup folder {Folder}", paths.BackupDirectory);
            }
        }

        if (Directory.Exists(paths.UserRoot) && !Directory.EnumerateFileSystemEntries(paths.UserRoot).Any())
        {
            try
            {
                Directory.Delete(paths.UserRoot);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete custom HTML directory {Directory}", paths.UserRoot);
            }
        }

        _monitorLog.Append($"Custom HTML deleted for user #{userId} ({username}).");
        _logger.LogInformation("Deleted custom HTML for user {UserId}", userId);
    }

    public Task<CustomHtmlFileInfo?> GetInfoAsync(int userId, string username, CancellationToken cancellationToken = default)
    {
        var paths = ResolvePaths(username, userId);
        if (!File.Exists(paths.LiveFilePath))
        {
            var empty = new CustomHtmlFileInfo(false, paths.RelativeRequestPath, paths.LiveFilePath, null, null, Array.Empty<string>());
            return Task.FromResult<CustomHtmlFileInfo?>(empty);
        }

        var info = new FileInfo(paths.LiveFilePath);
        var backups = Directory.Exists(paths.BackupDirectory)
            ? Directory.GetFiles(paths.BackupDirectory, "*.html", SearchOption.TopDirectoryOnly)
                .Select(file => CombineRequestPath(paths.RelativeBackupDirectory, Path.GetFileName(file)!))
                .OrderByDescending(path => path)
                .ToArray()
            : Array.Empty<string>();

        var details = new CustomHtmlFileInfo(true, paths.RelativeRequestPath, paths.LiveFilePath, info.LastWriteTimeUtc, info.Length, backups);
        return Task.FromResult<CustomHtmlFileInfo?>(details);
    }

    private async Task BackupExistingAsync(string livePath, string backupDirectory, int version, CancellationToken cancellationToken)
    {
        if (!File.Exists(livePath))
        {
            return;
        }

        Directory.CreateDirectory(backupDirectory);
        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture);
        var backupName = $"profile_v{version:D4}_{timestamp}.html";
        var backupPath = Path.Combine(backupDirectory, backupName);
        await Task.Run(() => File.Copy(livePath, backupPath, overwrite: true), cancellationToken);
    }

    private async Task<IReadOnlyList<string>> PruneBackupsAsync(ResolvedUserPaths paths, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(paths.BackupDirectory))
        {
            return Array.Empty<string>();
        }

        var files = Directory.GetFiles(paths.BackupDirectory, "*.html", SearchOption.TopDirectoryOnly)
            .Select(path => new FileInfo(path))
            .OrderByDescending(info => info.LastWriteTimeUtc)
            .ToList();

        var maxVersions = Math.Max(1, _options.Value.MaxVersionsPerUser);
        var keep = files.Take(maxVersions).ToList();
        var remove = files.Skip(maxVersions).ToList();

        foreach (var stale in remove)
        {
            await SecureDeleteIfExistsAsync(stale.FullName, cancellationToken);
        }

        var relative = keep
            .Select(info => CombineRequestPath(paths.RelativeBackupDirectory, info.Name))
            .ToList();

        return relative;
    }

    private async Task SecureDeleteIfExistsAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return;
        }

        if (_options.Value.EnableSecureDelete)
        {
            try
            {
                var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
                try
                {
                    var length = new FileInfo(path).Length;
                    await using var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None, buffer.Length, useAsync: true);
                    long remaining = length;
                    while (remaining > 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var chunk = (int)Math.Min(buffer.Length, remaining);
                        await stream.WriteAsync(buffer.AsMemory(0, chunk), cancellationToken);
                        remaining -= chunk;
                    }
                    await stream.FlushAsync(cancellationToken);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to securely overwrite {Path}", path);
            }
        }

        try
        {
            File.Delete(path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete file {Path}", path);
        }
    }

    private ResolvedUserPaths ResolvePaths(string username, int userId)
    {
        var slug = NormalizeSegment(username, userId);
        var fileName = string.IsNullOrWhiteSpace(_options.Value.FileName)
            ? "profile.html"
            : _options.Value.FileName.Trim();

        var backupFolderName = string.IsNullOrWhiteSpace(_options.Value.BackupFolderName)
            ? "versions"
            : _options.Value.BackupFolderName.Trim();

        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(AppContext.BaseDirectory, "wwwroot");
        }

        var configuredRoot = _options.Value.RootDirectory;
        var physicalRoot = string.IsNullOrWhiteSpace(configuredRoot)
            ? Path.Combine(webRoot, "user-html")
            : (Path.IsPathRooted(configuredRoot)
                ? configuredRoot
                : Path.Combine(webRoot, configuredRoot));

        var userRoot = Path.Combine(physicalRoot, slug);
        var livePath = Path.Combine(userRoot, fileName);
        var backupDirectory = Path.Combine(userRoot, backupFolderName);

        var prefix = NormalizeRequestPrefix(_options.Value.RequestPathPrefix);
        var relativePath = CombineRequestPath(prefix, slug, fileName);
        var relativeBackupDirectory = CombineRequestPath(prefix, slug, backupFolderName);

        return new ResolvedUserPaths(userRoot, livePath, backupDirectory, relativePath, relativeBackupDirectory);
    }

    private static void ReplaceFile(string sourceTempPath, string destinationPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        File.Move(sourceTempPath, destinationPath, overwrite: true);
    }

    private static string NormalizeSegment(string username, int fallbackId)
    {
        var normalized = username?.Trim().ToLowerInvariant() ?? string.Empty;
        var builder = new StringBuilder(normalized.Length);

        foreach (var c in normalized)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
            }
            else if (c == '-' || c == '_')
            {
                builder.Append(c == '_' ? '-' : c);
            }
            else
            {
                builder.Append('-');
            }

            if (builder.Length >= 64)
            {
                break;
            }
        }

        var candidate = builder.ToString().Trim('-');
        if (string.IsNullOrWhiteSpace(candidate))
        {
            candidate = $"user-{fallbackId}";
        }

        return candidate;
    }

    private static string NormalizeRequestPrefix(string? prefix)
    {
        var value = string.IsNullOrWhiteSpace(prefix) ? "/u" : prefix.Trim();
        if (!value.StartsWith('/'))
        {
            value = "/" + value;
        }

        return value.TrimEnd('/');
    }

    private static string CombineRequestPath(params string[] segments)
    {
        var builder = new StringBuilder();
        foreach (var segment in segments)
        {
            if (string.IsNullOrWhiteSpace(segment))
            {
                continue;
            }

            var trimmed = segment.Trim('/');
            if (builder.Length == 0)
            {
                builder.Append('/');
            }
            else
            {
                builder.Append('/');
            }

            builder.Append(trimmed);
        }

        return builder.Length == 0 ? "/" : builder.ToString();
    }

    private sealed record ResolvedUserPaths(
        string UserRoot,
        string LiveFilePath,
        string BackupDirectory,
        string RelativeRequestPath,
        string RelativeBackupDirectory);
}
