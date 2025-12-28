using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
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
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif"
    };

    private const long MaxFileSizeBytes = 6 * 1024 * 1024; // 6 MB cap per image
    private const int MaxDimension = 1920;
    private static readonly Size ThumbnailSize = new(420, 260);
    private const string OriginalFolder = "uploads/thread-images/full";
    private const string ThumbnailFolder = "uploads/thread-images/thumbs";

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ThreadImageStorageService> _logger;

    public ThreadImageStorageService(IWebHostEnvironment environment, ILogger<ThreadImageStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<IReadOnlyList<StoredImageResult>> SaveImagesAsync(IEnumerable<IFormFile>? files, CancellationToken cancellationToken = default)
    {
        if (files == null)
        {
            return Array.Empty<StoredImageResult>();
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

            var contentType = file.ContentType?.ToLowerInvariant() ?? string.Empty;
            if (!AllowedContentTypes.Contains(contentType))
            {
                throw new InvalidOperationException($"{file.FileName} is not an accepted image format.");
            }

            IImageFormat? format;
            await using (var detectionStream = file.OpenReadStream())
            {
                format = await Image.DetectFormatAsync(detectionStream, cancellationToken);
            }

            await using var stream = file.OpenReadStream();
            using var image = await Image.LoadAsync<Rgba32>(stream, cancellationToken);

            // Normalize extension by format to avoid spoofing
            var extension = GetFileExtension(format) ?? Path.GetExtension(file.FileName);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".img";
            }

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var originalPath = Path.Combine(_environment.WebRootPath, OriginalFolder);
            var thumbPath = Path.Combine(_environment.WebRootPath, ThumbnailFolder);
            Directory.CreateDirectory(originalPath);
            Directory.CreateDirectory(thumbPath);

            var originalFullPath = Path.Combine(originalPath, fileName);
            var thumbFullPath = Path.Combine(thumbPath, fileName);
            var encoder = ResolveEncoder(format);
            var needsResize = image.Width > MaxDimension || image.Height > MaxDimension;
            var finalWidth = image.Width;
            var finalHeight = image.Height;

            if (needsResize)
            {
                using var resizedImage = image.Clone();
                resizedImage.Mutate(ctx => ctx.Resize(new ResizeOptions
                {
                    Mode = ResizeMode.Max,
                    Size = new Size(MaxDimension, MaxDimension)
                }));

                await resizedImage.SaveAsync(originalFullPath, encoder, cancellationToken);
                finalWidth = resizedImage.Width;
                finalHeight = resizedImage.Height;
            }
            else
            {
                await using var destinationStream = File.Open(originalFullPath, FileMode.Create, FileAccess.Write, FileShare.None);
                await using var sourceStream = file.OpenReadStream();
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }

            using var thumbImage = image.Clone();
            thumbImage.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Crop,
                Position = AnchorPositionMode.Center,
                Size = ThumbnailSize
            }));
            await thumbImage.SaveAsync(thumbFullPath, encoder, cancellationToken);

            var fileInfo = new FileInfo(originalFullPath);
            results.Add(new StoredImageResult(
                OriginalUrl: $"/{OriginalFolder}/{fileName}",
                ThumbnailUrl: $"/{ThumbnailFolder}/{fileName}",
                ContentType: contentType,
                Width: finalWidth,
                Height: finalHeight,
                FileSize: fileInfo.Length
            ));
        }

        return results;
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

    private static string? GetFileExtension(IImageFormat? format)
    {
        if (format?.FileExtensions == null || !format.FileExtensions.Any())
        {
            return null;
        }

        return "." + format.FileExtensions.First();
    }
}

public record StoredImageResult(
    string OriginalUrl,
    string ThumbnailUrl,
    string ContentType,
    int Width,
    int Height,
    long FileSize);
