using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AngleSharp.Dom;
using AngleSharp.Html.Dom;
using Ganss.Xss;
using Microsoft.Extensions.Options;

namespace MyFace.Services.CustomHtml;

public class CustomHtmlSanitizer : ICustomHtmlSanitizer
{
    private static readonly string[] GlobalAttributes = { "id", "class", "title", "alt" };
    private static readonly string[] AnchorAttributes = { "href", "rel" };
    private static readonly string[] ImageAttributes = { "src", "alt", "width", "height" };
    private const string AnchorRelValue = "nofollow noopener noreferrer";

    private readonly CustomHtmlSettings _settings;

    public CustomHtmlSanitizer(IOptions<CustomHtmlSettings> options)
    {
        _settings = options?.Value ?? new CustomHtmlSettings();
        if (_settings.MaxOutputBytes <= 0)
        {
            _settings.MaxOutputBytes = _settings.MaxFileSizeBytes;
        }
    }

    public async Task<HtmlSanitizationResult> SanitizeAsync(Stream htmlStream, CancellationToken cancellationToken = default)
    {
        if (htmlStream == null) throw new ArgumentNullException(nameof(htmlStream));

        using var memoryStream = new MemoryStream();
        var buffer = ArrayPool<byte>.Shared.Rent(16 * 1024);
        var totalBytes = 0;

        try
        {
            int read;
            while ((read = await htmlStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
            {
                totalBytes += read;
                if (totalBytes > _settings.MaxFileSizeBytes)
                {
                    return HtmlSanitizationResult.Failure(
                        new[] { $"HTML upload exceeds {_settings.MaxFileSizeBytes:N0} bytes." },
                        totalBytes,
                        outputBytes: 0,
                        nodeCount: 0,
                        inputLimitExceeded: true,
                        outputLimitExceeded: false,
                        nodeLimitExceeded: false);
                }

                memoryStream.Write(buffer, 0, read);
            }

            var content = Encoding.UTF8.GetString(memoryStream.ToArray());
            var measuredInput = totalBytes == 0 ? Encoding.UTF8.GetByteCount(content) : totalBytes;
            return Sanitize(content, measuredInput);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    public HtmlSanitizationResult Sanitize(string? htmlContent, int? inputBytes = null)
    {
        var content = htmlContent ?? string.Empty;
        var measuredInput = inputBytes ?? Encoding.UTF8.GetByteCount(content);

        if (measuredInput > _settings.MaxFileSizeBytes)
        {
            return HtmlSanitizationResult.Failure(
                new[] { $"HTML input exceeds {_settings.MaxFileSizeBytes:N0} bytes." },
                measuredInput,
                outputBytes: 0,
                nodeCount: 0,
                inputLimitExceeded: true,
                outputLimitExceeded: false,
                nodeLimitExceeded: false);
        }

        var errors = new List<string>();
        var sanitizer = CreateSanitizer(errors);
        var document = sanitizer.SanitizeDom(content ?? string.Empty);

        NormalizeAnchorRel(document);
        RemoveDisallowedImageAttributes(document, errors);

        var sanitizedHtml = document.Body?.InnerHtml?.Trim() ?? string.Empty;
        var outputBytes = Encoding.UTF8.GetByteCount(sanitizedHtml);
        var nodeCount = document.All.Length;

        var nodeLimitExceeded = nodeCount > _settings.MaxNodeCount;
        if (nodeLimitExceeded)
        {
            errors.Add($"HTML contains {nodeCount:N0} nodes which exceeds the limit of {_settings.MaxNodeCount:N0}.");
        }

        var outputLimitExceeded = outputBytes > _settings.MaxOutputBytes;
        if (outputLimitExceeded)
        {
            errors.Add($"Sanitized HTML exceeds the {_settings.MaxOutputBytes:N0}-byte output limit.");
        }

        if (errors.Count > 0)
        {
            return HtmlSanitizationResult.Failure(errors, measuredInput, outputBytes, nodeCount, false, outputLimitExceeded, nodeLimitExceeded);
        }

        return HtmlSanitizationResult.Success(sanitizedHtml, nodeCount, measuredInput, outputBytes);
    }

    private HtmlSanitizer CreateSanitizer(ICollection<string> errors)
    {
        var sanitizer = new HtmlSanitizer
        {
            AllowDataAttributes = false
        };

        sanitizer.AllowedTags.Clear();
        foreach (var tag in _settings.AllowedTags)
        {
            if (string.Equals(tag, "svg", StringComparison.OrdinalIgnoreCase) && !_settings.AllowSvg)
            {
                continue;
            }
            sanitizer.AllowedTags.Add(tag);
        }

        sanitizer.AllowedAttributes.Clear();
        foreach (var attribute in GlobalAttributes.Concat(AnchorAttributes).Concat(ImageAttributes).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add(Uri.UriSchemeHttp);
        sanitizer.AllowedSchemes.Add(Uri.UriSchemeHttps);
        if (_settings.AllowDataImages)
        {
            sanitizer.AllowedSchemes.Add("data");
        }

        sanitizer.FilterUrl += (_, args) =>
        {
            var context = args.Tag != null && string.Equals(args.Tag.TagName, "img", StringComparison.OrdinalIgnoreCase)
                ? UrlContext.Image
                : UrlContext.Anchor;

            if (IsAllowedUrl(args.OriginalUrl, context))
            {
                return;
            }

            args.SanitizedUrl = null;
            if (!string.IsNullOrWhiteSpace(args.OriginalUrl))
            {
                var message = context == UrlContext.Image
                    ? $"Image source '{args.OriginalUrl}' is not allowed. Use relative paths or data:image URIs."
                    : $"Link target '{args.OriginalUrl}' is not allowed. Use relative paths only.";
                errors.Add(message);
            }
        };

        return sanitizer;
    }

    private static void NormalizeAnchorRel(IHtmlDocument document)
    {
        foreach (var anchor in document.QuerySelectorAll("a"))
        {
            if (anchor is IHtmlAnchorElement htmlAnchor)
            {
                htmlAnchor.Relation = AnchorRelValue;
                htmlAnchor.SetAttribute("rel", AnchorRelValue);
            }
        }
    }

    private void RemoveDisallowedImageAttributes(IHtmlDocument document, ICollection<string> errors)
    {
        foreach (var node in document.QuerySelectorAll("img"))
        {
            if (node is not IHtmlImageElement image)
            {
                continue;
            }

            if (!IsAllowedUrl(image.Source, UrlContext.Image))
            {
                node.Remove();
                if (!string.IsNullOrWhiteSpace(image.Source))
                {
                    errors.Add($"Removed image with forbidden source '{image.Source}'. Only relative paths or data:image URIs are permitted.");
                }
                continue;
            }

            if (!string.IsNullOrWhiteSpace(image.Source) && image.Source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                if (!_settings.AllowDataImages)
                {
                    node.Remove();
                    errors.Add("Data URI images are disabled for this installation.");
                }
                else if (!image.Source.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
                {
                    node.Remove();
                    errors.Add("Only data:image/* URIs are permitted for inline images.");
                }
            }
        }
    }

    private bool IsAllowedUrl(string? url, UrlContext context)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        if (url.StartsWith("#", StringComparison.Ordinal))
        {
            return true;
        }

        if (Uri.TryCreate(url, UriKind.Relative, out _))
        {
            return true;
        }

        if (url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            if (context != UrlContext.Image)
            {
                return false;
            }

            if (!_settings.AllowDataImages)
            {
                return false;
            }

            return url.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            return false;
        }

        if (_settings.AllowExternalUrls &&
            (absolute.Scheme == Uri.UriSchemeHttp || absolute.Scheme == Uri.UriSchemeHttps))
        {
            return true;
        }

        return false;
    }

    private enum UrlContext
    {
        Anchor,
        Image
    }
}
