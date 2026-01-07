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

        // 1a. Extract [code] blocks first so their contents are not re-parsed by later replacements
        var codeBlocks = new List<string>();
        encoded = Regex.Replace(encoded, @"\[code\](.*?)\[/code\]", match =>
        {
            var idx = codeBlocks.Count;
            var content = match.Groups[1].Value;
            var rendered = $"<pre class=\"bbcode-code\"><code>{content}</code></pre>";
            codeBlocks.Add(rendered);
            return $"__BBCODE_CODEBLOCK_{idx}__";
        }, RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 2. Apply BBCode replacements (order matters!)
        
        // Headers (map to h3/h4 for safety) - do these early before newlines become <br>
        encoded = Regex.Replace(encoded, @"\[h1\](.*?)\[/h1\]", "<h3 style=\"margin:0.29rem 0 0.23rem;line-height:1.2\">$1</h3>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        encoded = Regex.Replace(encoded, @"\[h2\](.*?)\[/h2\]", "<h4 style=\"margin:0.23rem 0 0.18rem;line-height:1.18\">$1</h4>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // HR (horizontal rule)
        encoded = Regex.Replace(encoded, @"\[hr\]", "<hr style=\"border:none;border-top:1px solid var(--border);margin:0.35rem 0\" />", RegexOptions.IgnoreCase);
        
        // Center text
        encoded = Regex.Replace(encoded, @"\[center\](.*?)\[/center\]", "<div style=\"text-align:center\">$1</div>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Quote
        encoded = Regex.Replace(encoded, @"\[quote\](.*?)\[/quote\]", "<blockquote style=\"border-left:3px solid var(--border);padding-left:0.6rem;margin:0.22rem 0;font-style:italic;color:var(--text-muted);line-height:1.08\">$1</blockquote>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        // Lists
        encoded = Regex.Replace(encoded, @"\[list\]", "<ul style=\"margin:0.2rem 0 0.2rem 1rem;padding-left:1rem;list-style-position:outside\">", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, @"\[/list\]", "</ul>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, @"\[\*\](.*?)(?=(\[\*\]|\[/list\]|&lt;br /&gt;|\r?\n|$))", "<li style=\"margin:0.1rem 0;line-height:1.2\">$1</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
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
        
        // Spoiler (CSS-only hover with spoiler class)
        encoded = Regex.Replace(encoded, @"\[spoiler\](.*?)\[/spoiler\]", "<span class=\"spoiler\">$1</span>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Line breaks: [br] or [brN] where N=1-10; each represents a blank line (two <br />)
        encoded = Regex.Replace(encoded, @"\[br(\d{0,2})\]", match =>
        {
            var numPart = match.Groups[1].Value;
            var blankLines = 1;
            if (!string.IsNullOrEmpty(numPart) && int.TryParse(numPart, out var parsed))
            {
                blankLines = Math.Clamp(parsed, 1, 10);
            }
            var brCount = blankLines * 2; // blank line = two <br />
            return string.Concat(Enumerable.Repeat("<br />", brCount));
        }, RegexOptions.IgnoreCase);

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

        // 3. Mentions: @username -> <a href="/u/username">@username</a>
        encoded = Regex.Replace(encoded, @"(?<=^|\s|&lt;br /&gt;)@([a-zA-Z0-9_]+)", "<a href=\"/u/$1\">@$1</a>");

        // 4. Newlines to <br> (do this last!)
        encoded = encoded.Replace("\r\n", "<br />").Replace("\n", "<br />");

        // 5. Tidy spacing: aggressively trim breaks around block elements and collapse multiples
        encoded = Regex.Replace(encoded, "(<br \\s*/?>\\s*){2,}", "<br />", RegexOptions.IgnoreCase); // collapse runs of <br>
        encoded = Regex.Replace(encoded, "</(div|p|h3|h4|blockquote)>\\s*<br \\s*/?>", "</$1>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "<br \\s*/?>\\s*<(div|p|h3|h4|blockquote)", "<$1", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "<ul[^>]*>\\s*<br \\s*/?>", string.Empty, RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "<ol[^>]*>\\s*<br \\s*/?>", string.Empty, RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "<br \\s*/?>\\s*</ul>", "</ul>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "<br \\s*/?>\\s*</ol>", "</ol>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "</li>\\s*<br \\s*/?>", "</li>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "<br \\s*/?>\\s*<li", "<li", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "<blockquote([^>]*)>\\s*<br \\s*/?>", "<blockquote$1>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "<br \\s*/?>\\s*</blockquote>", "</blockquote>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "</h3>\\s*<br \\s*/?>", "</h3>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "</h4>\\s*<br \\s*/?>", "</h4>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "<hr([^>]*)>\\s*<br \\s*/?>", "<hr$1>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, "<br \\s*/?>\\s*<hr", "<hr", RegexOptions.IgnoreCase);

        // 6. Restore code blocks (kept untouched)
        for (int i = 0; i < codeBlocks.Count; i++)
        {
            encoded = encoded.Replace($"__BBCODE_CODEBLOCK_{i}__", codeBlocks[i]);
        }

        return new HtmlString(encoded);
    }
}
