using System.Collections.Generic;

namespace MyFace.Services.CustomHtml;

public class CustomHtmlSettings
{
    public int MaxFileSizeBytes { get; set; } = 512_000;
    public int MaxOutputBytes { get; set; } = 512_000;
    public int MaxNodeCount { get; set; } = 1_000;
    public bool AllowDataImages { get; set; } = true;
    public bool AllowExternalUrls { get; set; } = false;
    public bool AllowSvg { get; set; } = false;

    public IReadOnlyList<string> AllowedTags { get; set; } = new[]
    {
        "div", "span", "p", "br", "hr", "h1", "h2", "h3", "h4", "h5", "h6",
        "ul", "ol", "li", "strong", "b", "em", "i", "u", "blockquote",
        "pre", "code", "table", "thead", "tbody", "tr", "th", "td", "img", "a"
    };
}
