using System.Text.RegularExpressions;
using System.Web;
using Microsoft.AspNetCore.Html;

namespace MyFace.Web.Services;

public class BBCodeFormatter
{
    public IHtmlContent Format(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return new HtmlString(string.Empty);
        }

        // 1. Sanitize raw HTML first
        var encoded = HttpUtility.HtmlEncode(input);

        // 2. Apply BBCode replacements
        // Bold
        encoded = Regex.Replace(encoded, @"\[b\](.*?)\[/b\]", "<strong>$1</strong>", RegexOptions.IgnoreCase);
        // Italic
        encoded = Regex.Replace(encoded, @"\[i\](.*?)\[/i\]", "<em>$1</em>", RegexOptions.IgnoreCase);
        // Strikethrough
        encoded = Regex.Replace(encoded, @"\[s\](.*?)\[/s\]", "<del>$1</del>", RegexOptions.IgnoreCase);
        // Small
        encoded = Regex.Replace(encoded, @"\[small\](.*?)\[/small\]", "<small>$1</small>", RegexOptions.IgnoreCase);
        // Headers (map to h3/h4 for safety)
        encoded = Regex.Replace(encoded, @"\[h1\](.*?)\[/h1\]", "<h3>$1</h3>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, @"\[h2\](.*?)\[/h2\]", "<h4>$1</h4>", RegexOptions.IgnoreCase);
        
        // Color (simple validation)
        encoded = Regex.Replace(encoded, @"\[color=([#a-zA-Z0-9]+)\](.*?)\[/color\]", "<span style=\"color:$1\">$2</span>", RegexOptions.IgnoreCase);

        // Lists
        // Simple list replacement: [list]...[/list] -> <ul>...</ul>
        encoded = Regex.Replace(encoded, @"\[list\]", "<ul>", RegexOptions.IgnoreCase);
        encoded = Regex.Replace(encoded, @"\[/list\]", "</ul>", RegexOptions.IgnoreCase);
        // List items: [*]Item -> <li>Item</li>
        // We'll assume [*] starts a line or is preceded by newline
        encoded = Regex.Replace(encoded, @"\[\*\](.*?)(?=(\[\*\]|\[/list\]|$))", "<li>$1</li>", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // 3. Mentions: @username -> <a href="/user/username">@username</a>
        // Match @username where username is alphanumeric/underscore, preceded by whitespace or start of string
        encoded = Regex.Replace(encoded, @"(?<=^|\s)@([a-zA-Z0-9_]+)", "<a href=\"/user/$1\">@$1</a>");

        // 4. Newlines to <br>
        encoded = encoded.Replace("\n", "<br />");

        return new HtmlString(encoded);
    }
}
