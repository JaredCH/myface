using System.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MyFace.Core.Entities;
using MyFace.Services;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace MyFace.Web.Services;

public class ThreadImageStorageService
{
    private static readonly Dictionary<string, AllowedImageFormat> AllowedImageFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        ["JPEG"] = new AllowedImageFormat("image/jpeg", ".jpg"),
        ["PNG"] = new AllowedImageFormat("image/png", ".png"),
        ["GIF"] = new AllowedImageFormat("image/gif", ".gif"),
        ["WEBP"] = new AllowedImageFormat("image/webp", ".webp")
    };

    private const long MaxFileSizeBytes = 6 * 1024 * 1024; // 6 MB cap per image
    private const int MaxDimension = 1920;
    private static readonly Size ThumbnailSize = new(420, 260);
    private const string OriginalFolder = "uploads/thread-images/full";
    private const string ThumbnailFolder = "uploads/thread-images/thumbs";

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ThreadImageStorageService> _logger;
    private readonly IMalwareScanner _malwareScanner;
    private readonly UploadScanLogService _uploadScanLogService;
    private readonly IOptions<MalwareScannerOptions> _scannerOptions;

    public ThreadImageStorageService(
        IWebHostEnvironment environment,
        ILogger<ThreadImageStorageService> logger,
        IMalwareScanner malwareScanner,
        UploadScanLogService uploadScanLogService,
        IOptions<MalwareScannerOptions> scannerOptions)
    {
        _environment = environment;
        _logger = logger;
        _malwareScanner = malwareScanner;
        _uploadScanLogService = uploadScanLogService;
        _scannerOptions = scannerOptions;
    }

    public async Task<IReadOnlyList<StoredImageResult>> SaveImagesAsync(
        IEnumerable<IFormFile>? files,
        ImageUploadContext uploadContext,
        CancellationToken cancellationToken = default)
    {
        if (files == null)
        {
            return Array.Empty<StoredImageResult>();
        }

        if (uploadContext == null)
        {
            throw new ArgumentNullException(nameof(uploadContext));
        }

        var selectedFiles = files.Where(f => f != null && f.Length > 0).Take(2).ToList();
        var results = new List<StoredImageResult>(selectedFiles.Count);

        foreach (var file in selectedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (file.Length > MaxFileSizeBytes)
            {
                throw new InvalidOperationException($"{file.FileName} exceeds the {MaxFileSizeBytes / (1024 * 1024)} MB limit.");
            }

            var processingTimer = Stopwatch.StartNew();
            var scanResult = await RunMalwareScanAsync(file, cancellationToken);
            var scanDurationMs = (long)Math.Round(scanResult.Duration.TotalMilliseconds);
            if (scanResult.ShouldBlock(_scannerOptions.Value.BlockOnError))
            {
                processingTimer.Stop();
                var failureReason = scanResult.Status == MalwareScanStatus.Malicious
                    ? $"Scanner flagged {file.FileName}"
                    : "Scanner unavailable";
                await LogUploadAsync(file, uploadContext, scanResult, true, 0, 0, processingTimer.ElapsedMilliseconds, scanDurationMs, failureReason, null, cancellationToken);

                var userFacing = scanResult.Status == MalwareScanStatus.Malicious
                    ? "Upload blocked: the file tripped our malware scanner."
                    : "Upload blocked: malware scanning is unavailable. Please try again later.";
                throw new InvalidOperationException(userFacing);
            }

            IImageFormat? format;
            await using (var detectionStream = file.OpenReadStream())
            {
                format = await Image.DetectFormatAsync(detectionStream, cancellationToken);
            }

            var allowedFormat = ValidateDetectedFormat(file.FileName, format);

            await using var stream = file.OpenReadStream();
            using var image = await Image.LoadAsync<Rgba32>(stream, cancellationToken);

            // Normalize extension by format to avoid spoofing
            var extension = allowedFormat.DefaultExtension;

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var originalPath = Path.Combine(_environment.WebRootPath, OriginalFolder);
            var thumbPath = Path.Combine(_environment.WebRootPath, ThumbnailFolder);
            Directory.CreateDirectory(originalPath);
            Directory.CreateDirectory(thumbPath);

            var originalFullPath = Path.Combine(originalPath, fileName);
            var thumbFullPath = Path.Combine(thumbPath, fileName);
            var encoder = ResolveEncoder(format);
            var needsResize = image.Width > MaxDimension || image.Height > MaxDimension;
            if (needsResize)
            {
                image.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxDimension, MaxDimension)
                }));
            }

            StripMetadata(image);
            await image.SaveAsync(originalFullPath, encoder, cancellationToken);
            var finalWidth = image.Width;
            var finalHeight = image.Height;

            using var thumbImage = image.Clone();
            thumbImage.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
                Size = ThumbnailSize
            }));
            StripMetadata(thumbImage);
            await thumbImage.SaveAsync(thumbFullPath, encoder, cancellationToken);

            var fileInfo = new FileInfo(originalFullPath);
            var storedResult = new StoredImageResult(
                OriginalUrl: $"/{OriginalFolder}/{fileName}",
                ThumbnailUrl: $"/{ThumbnailFolder}/{fileName}",
                ContentType: allowedFormat.ContentType,
                Width: finalWidth,
                Height: finalHeight,
                FileSize: fileInfo.Length
            );
            results.Add(storedResult);

            processingTimer.Stop();
            await LogUploadAsync(
                file,
                uploadContext,
                scanResult,
                blocked: false,
                width: finalWidth,
                height: finalHeight,
                processingDurationMs: processingTimer.ElapsedMilliseconds,
                scanDurationMs: scanDurationMs,
                failureReason: null,
                storedResult,
                cancellationToken);
        }

        return results;
    }

    private async Task<MalwareScanResult> RunMalwareScanAsync(IFormFile file, CancellationToken cancellationToken)
    {
        try
        {
            await using var scanStream = file.OpenReadStream();
            return await _malwareScanner.ScanAsync(scanStream, file.FileName, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled malware scan failure for {FileName}", file.FileName);
            return MalwareScanResult.Error(_scannerOptions.Value.EngineName, ex.Message, TimeSpan.Zero, false);
        }
    }

    private async Task LogUploadAsync(
        IFormFile file,
        ImageUploadContext context,
        MalwareScanResult scanResult,
        bool blocked,
        int width,
        int height,
        long processingDurationMs,
        long scanDurationMs,
        string? failureReason,
        StoredImageResult? storedResult,
        CancellationToken cancellationToken)
    {
        try
        {
            var log = new UploadScanLog
            {
                EventType = "ThreadImage",
                Source = string.IsNullOrWhiteSpace(context.Source) ? "Unknown" : context.Source,
                UserId = context.UserId,
                UsernameSnapshot = context.Username,
                IsAnonymous = context.IsAnonymous,
                SessionId = string.IsNullOrWhiteSpace(context.SessionId) ? "anonymous" : context.SessionId,
                IpAddressHash = context.IpAddressHash,
                OriginalFileName = file.FileName,
                ContentType = storedResult?.ContentType ?? file.ContentType ?? string.Empty,
                FileSize = file.Length,
                Width = width,
                Height = height,
                StoragePath = storedResult?.OriginalUrl,
                ScanEngine = scanResult.Engine,
                ScanStatus = scanResult.Status.ToString(),
                ThreatName = scanResult.ThreatName,
                ScannerMessage = scanResult.Message,
                Blocked = blocked,
                ProcessingDurationMs = processingDurationMs,
                ScanDurationMs = scanDurationMs,
                FailureReason = failureReason
            };

            await _uploadScanLogService.LogAsync(log, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist upload scan log for {FileName}", file.FileName);
        }
    }

    private static IImageEncoder ResolveEncoder(IImageFormat? format)
    {
        return format switch
        {
            PngFormat => new PngEncoder(),
            GifFormat => new GifEncoder(),
            WebpFormat => new WebpEncoder(),
            _ => new JpegEncoder()
        };
    }

    private static AllowedImageFormat ValidateDetectedFormat(string fileName, IImageFormat? detectedFormat)
    {
        if (detectedFormat == null || string.IsNullOrWhiteSpace(detectedFormat.Name))
        {
            throw new InvalidOperationException($"{fileName} could not be read as a supported image.");
        }

        if (!AllowedImageFormats.TryGetValue(detectedFormat.Name, out var allowedFormat))
        {
            throw new InvalidOperationException($"{fileName} is not an accepted image format.");
        }

        return allowedFormat;
    }

    private static void StripMetadata(Image image)
    {
        if (image.Metadata.ExifProfile != null)
        {
            image.Metadata.ExifProfile = null;
        }
        if (image.Metadata.IptcProfile != null)
        {
            image.Metadata.IptcProfile = null;
        }
        if (image.Metadata.XmpProfile != null)
        {
            image.Metadata.XmpProfile = null;
        }
    }
}

public record StoredImageResult(
    string OriginalUrl,
    string ThumbnailUrl,
    string ContentType,
    int Width,
    int Height,
    long FileSize);

internal sealed record AllowedImageFormat(string ContentType, string DefaultExtension);
