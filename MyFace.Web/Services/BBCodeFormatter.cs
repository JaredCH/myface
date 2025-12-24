using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Html;

namespace MyFace.Web.Services;

public class BBCodeFormatter
{
    private static bool IsInternalLink(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return false;
        var trimmed = url.Trim();
        // Internal if relative (/path or path without scheme)
        var lower = trimmed.ToLowerInvariant();
        if (lower.StartsWith("http://") || lower.StartsWith("https://"))
        {
            return false; // treat all absolute as external to stay safe
        }
        return trimmed.StartsWith("/") || (!trimmed.Contains("://") && !trimmed.StartsWith("//"));
    }

    private static string RenderLink(string href, string text)
    {
        var safeHref = HttpUtility.HtmlAttributeEncode(href);
        var safeText = text; // already encoded upstream
        return $"<a href=\"{safeHref}\" target=\"_blank\" rel=\"noopener\">{safeText}</a>";
    }

    public IHtmlContent Format(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new HtmlString(string.Empty);
        }

        // 1. Sanitize raw HTML first
        var encoded = HttpUtility.HtmlEncode(input);

        // 2. Apply BBCode replacements (order matters!)
        
        // Headers (map to h3/h4 for safety) - do these early before newlines become <br>
        encoded = Regex.Replace(encoded, @"\[h1\](.*?)\[/h1\]", "<h3>$1</h3>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        encoded = Regex.Replace(encoded, @"\[h2\](.*?)\[/h2\]", "<h4>$1</h4>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // HR (horizontal rule)
        encoded = Regex.Replace(encoded, @"\[hr\]", "<hr style=\"border:none;border-top:1px solid var(--border);margin:1rem 0\" />", RegexOptions.IgnoreCase);
        
        // Center text
        encoded = Regex.Replace(encoded, @"\[center\](.*?)\[/center\]", "<div style=\"text-align:center\">$1</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Quote
        encoded = Regex.Replace(encoded, @"\[quote\](.*?)\[/quote\]", "<blockquote style=\"border-left:3px solid var(--border);padding-left:1rem;margin:0.5rem 0;font-style:italic;color:var(--text-muted)\">$1</blockquote>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Lists
        encoded = Regex.Replace(encoded, @"\[list\]", "<ul>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, @"\[/list\]", "</ul>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, @"\[\*\](.*?)(?=(\[\*\]|\[/list\]|&lt;br /&gt;|\r?\n|$))", "<li>$1</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Bold
        encoded = Regex.Replace(encoded, @"\[b\](.*?)\[/b\]", "<strong>$1</strong>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        // Italic
        encoded = Regex.Replace(encoded, @"\[i\](.*?)\[/i\]", "<em>$1</em>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        // Underline
        encoded = Regex.Replace(encoded, @"\[u\](.*?)\[/u\]", "<u>$1</u>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        // Strikethrough
        encoded = Regex.Replace(encoded, @"\[s\](.*?)\[/s\]", "<del>$1</del>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        // Small
        encoded = Regex.Replace(encoded, @"\[small\](.*?)\[/small\]", "<small>$1</small>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        // Large
        encoded = Regex.Replace(encoded, @"\[big\](.*?)\[/big\]", "<span style=\"font-size:1.2em\">$1</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Color (simple validation)
        encoded = Regex.Replace(encoded, @"\[color=([#a-zA-Z0-9]+)\](.*?)\[/color\]", "<span style=\"color:$1\">$2</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Background color
        encoded = Regex.Replace(encoded, @"\[bg=([#a-zA-Z0-9]+)\](.*?)\[/bg\]", "<span style=\"background-color:$1;padding:2px 4px;border-radius:2px\">$2</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Code (inline)
        encoded = Regex.Replace(encoded, @"\[code\](.*?)\[/code\]", "<code style=\"background:var(--card-bg);padding:2px 6px;border-radius:3px;font-family:monospace;font-size:0.9em\">$1</code>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Spoiler (CSS-only hover with spoiler class)
        encoded = Regex.Replace(encoded, @"\[spoiler\](.*?)\[/spoiler\]", "<span class=\"spoiler\">$1</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Disallow images: render [img]...[/img] as plain text URL (already encoded)
        encoded = Regex.Replace(encoded, @"\[img\](.*?)\[/img\]", "$1", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // URL handling with security rules
        encoded = Regex.Replace(encoded, @"\[url=(.*?)\](.*?)\[/url\]", match =>
        {
            var href = match.Groups[1].Value;
            var text = match.Groups[2].Value;
            return IsInternalLink(href) ? RenderLink(href, text) : text;
        }, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        encoded = Regex.Replace(encoded, @"\[url\](.*?)\[/url\]", match =>
        {
            var href = match.Groups[1].Value;
            return IsInternalLink(href) ? RenderLink(href, href) : href;
        }, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 3. Mentions: @username -> <a href="/user/username">@username</a>
        encoded = Regex.Replace(encoded, @"(?<=^|\s|&lt;br /&gt;)@([a-zA-Z0-9_]+)", "<a href=\"/user/$1\">@$1</a>");

        // 4. Newlines to <br> (do this last!)
        encoded = encoded.Replace("\r\n", "<br />").Replace("\n", "<br />");

        return new HtmlString(encoded);
    }
}
