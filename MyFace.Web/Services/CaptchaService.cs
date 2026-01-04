using System.Security.Cryptography;

namespace MyFace.Web.Services;

public class CaptchaChallenge
{
    public string Context { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public class CaptchaService
{
    private const int MathChallengeChancePercent = 5;

    // Single word pool (distinct words) to produce thousands of unique 3-word phrases (nPk3 combinations)
    private static readonly string[] WordPool = new[]
    {
        "amber", "anchor", "apricot", "arch", "ash", "azure", "baker", "bamboo", "basil", "bay",
        "beacon", "birch", "blade", "bluff", "brick", "bronze", "buck", "canyon", "cedar", "chess",
        "chisel", "cobalt", "cobweb", "comet", "coral", "cotton", "cricket", "crown", "crystal", "delta",
        "ember", "fir", "flint", "fog", "forest", "fox", "frost", "gale", "granite", "grove",
        "harbor", "hazel", "heather", "ink", "iron", "ivory", "jungle", "kayak", "keystone", "lagoon",
        "lantern", "linen", "lotus", "maple", "marble", "meadow", "mesa", "mint", "monsoon", "moose",
        "moss", "navy", "nebula", "oak", "onyx", "opal", "orchid", "oyster", "papaya", "pebble",
        "pine", "plume", "quartz", "quill", "raven", "reef", "ridge", "rose", "saffron", "sage",
        "sand", "shale", "sienna", "silk", "slate", "smoke", "spruce", "starlit", "stone", "storm",
        "sumac", "sunset", "thicket", "thistle", "tidal", "topaz", "umber", "velvet", "walnut", "willow"
    };

    public CaptchaChallenge GenerateChallenge()
    {
        // 5% chance for Math Captcha
        if (RandomNumberGenerator.GetInt32(100) < MathChallengeChancePercent)
        {
            return GenerateMathChallenge();
        }
        
        return GenerateWordOrderChallenge();
    }

    private CaptchaChallenge GenerateWordOrderChallenge()
    {
        // pick 3 distinct words to maximize combination count
        var words = PickDistinctWords(WordPool, 3);
        var phrase = string.Join(' ', words);

        var position = RandomNumberGenerator.GetInt32(1, 4); // 1, 2, or 3
        var ordinal = position switch
        {
            1 => "first",
            2 => "second",
            _ => "third"
        };

        return new CaptchaChallenge
        {
            Context = $"Phrase: {phrase}",
            Question = $"What is the {ordinal} word in the phrase above?",
            Answer = words[position - 1]
        };
    }

    private CaptchaChallenge GenerateMathChallenge()
    {
        var a = RandomNumberGenerator.GetInt32(1, 11); // 1 to 10
        var b = RandomNumberGenerator.GetInt32(1, 11); // 1 to 10
        var isAddition = RandomNumberGenerator.GetInt32(2) == 0;

        if (!isAddition && a < b)
        {
            (a, b) = (b, a);
        }
        
        var result = isAddition ? a + b : a - b;
        var op = isAddition ? "+" : "-";
        var expression = $"{a} {op} {b} = ?";
        
        var sb = new System.Text.StringBuilder();
        sb.Append("<div style='user-select: none;'>");
        
        var fonts = new[] { "Courier New", "Arial", "Verdana", "Times New Roman", "Georgia", "Impact", "Comic Sans MS" };
        
        foreach (var c in expression)
        {
            if (c == ' ') 
            {
                sb.Append("&nbsp;");
                continue;
            }
            
            var font = fonts[RandomNumberGenerator.GetInt32(fonts.Length)];
            var size = RandomNumberGenerator.GetInt32(14, 26);
            var offset = RandomNumberGenerator.GetInt32(-5, 6);
            var rotate = RandomNumberGenerator.GetInt32(-15, 16);
            
            sb.Append($"<span style='font-family: \"{font}\"; font-size: {size}px; display: inline-block; transform: translateY({offset}px) rotate({rotate}deg); margin: 0 2px;'>{c}</span>");
        }
        sb.Append("</div>");

        return new CaptchaChallenge
        {
            Context = sb.ToString(),
            Question = "Solve the math problem above.",
            Answer = result.ToString()
        };
    }

    private static string[] PickDistinctWords(string[] pool, int count)
    {
        // Fisher-Yates partial shuffle for distinct picks without allocations beyond span copy
        var temp = (string[])pool.Clone();
        var n = temp.Length;
        for (int i = 0; i < count; i++)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(i, n);
            (temp[i], temp[swapIndex]) = (temp[swapIndex], temp[i]);
        }

        var result = new string[count];
        Array.Copy(temp, result, count);
        return result;
    }

    public bool Validate(string expected, string actual)
    {
        if (string.IsNullOrWhiteSpace(expected) || string.IsNullOrWhiteSpace(actual))
            return false;

        var normalizedExpected = NormalizeAnswer(expected);
        var normalizedActual = NormalizeAnswer(actual);

        if (normalizedExpected == normalizedActual)
            return true;

        // Numeric fallback: tolerate formats like "12" vs "012" vs "12.0"
        if (int.TryParse(normalizedExpected, out var expectedNum) && int.TryParse(normalizedActual, out var actualNum))
            return expectedNum == actualNum;

        return false;
    }

    private static string NormalizeAnswer(string input)
    {
        // Lowercase, strip punctuation, collapse whitespace; keep letters/digits only.
        var sb = new System.Text.StringBuilder();
        var lastWasSpace = false;

        foreach (var ch in input.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(ch);
                lastWasSpace = false;
            }
            else if (char.IsWhiteSpace(ch))
            {
                if (!lastWasSpace && sb.Length > 0)
                {
                    sb.Append(' ');
                    lastWasSpace = true;
                }
            }
            // Punctuation and symbols are dropped to be forgiving.
        }

        return sb.ToString().Trim();
    }
}
