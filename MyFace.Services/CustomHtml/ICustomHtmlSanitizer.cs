using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MyFace.Services.CustomHtml;

public interface ICustomHtmlSanitizer
{
    Task<HtmlSanitizationResult> SanitizeAsync(Stream htmlStream, CancellationToken cancellationToken = default);
    HtmlSanitizationResult Sanitize(string? htmlContent, int? inputBytes = null);
}
