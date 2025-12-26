using System.Security.Cryptography;
using System.Security.Claims;

namespace MyFace.Web.Services;

public class CaptchaChallenge
{
    public string Context { get; set; } = string.Empty;
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public static class CaptchaSettings
{
    public const int MinPageViewsBeforeCaptcha = 15;
    public const int MaxPageViewsBeforeCaptcha = 30;

    public const int AnonymousMinPageViews = 3;
    public const int AnonymousMaxPageViews = 7;

    public const int AdminModMinPageViews = 30;
    public const int AdminModMaxPageViews = 60;

    public static int NextThreshold(System.Security.Claims.ClaimsPrincipal user)
    {
        var (min, max) = GetRangeForUser(user);
        return RandomNumberGenerator.GetInt32(min, max + 1);
    }

    public static (int Min, int Max) GetRangeForUser(System.Security.Claims.ClaimsPrincipal user)
    {
        if (user?.Identity?.IsAuthenticated != true)
        {
            return (AnonymousMinPageViews, AnonymousMaxPageViews);
        }

        var role = user.FindFirstValue(System.Security.Claims.ClaimTypes.Role)?.ToLowerInvariant();
        if (role == "admin" || role == "moderator")
        {
            return (AdminModMinPageViews, AdminModMaxPageViews);
        }

        return (MinPageViewsBeforeCaptcha, MaxPageViewsBeforeCaptcha);
    }
}

public class CaptchaService
{
    private const int MathChallengeChancePercent = 5;

    private static readonly string[] Adjectives = new[]
    {
        "Red", "Blue", "Green", "Dark", "Bright", "Silent", "Hidden", "Lost", "Found", "Broken",
        "Quantum", "Neural", "Cyber", "Analog", "Digital", "Solar", "Lunar", "Cosmic", 
        "Static", "Dynamic", "Kinetic", "Sonic", "Optic", "Thermal", "Magnetic", "Electric"
    };

    private static readonly string[] Nouns = new[]
    {
        "lanterns", "keys", "doors", "wires", "clocks",
        "panels", "tunnels", "mirrors", "signals", "ladders",
        "folders", "screens", "valves", "circuits", "handles",
        "paths", "markers", "windows", "rails", "switches",
        "nodes", "buffers", "frames", "channels", "ports",
        "alpha gates", "beta locks", "delta paths", "gamma nodes",
        "echo panels", "foxtrot keys", "lambda rails",
        "omega switches", "sigma buffers", "vector clocks",
        "Orion markers", "Atlas doors", "Nova signals",
        "Helix tunnels", "Cipher panels", "Apex valves",
        "Vertex mirrors", "Pulse channels", "Flux nodes"
    };

    private static readonly string[] Templates = new[]
    {
        "Note: replies referencing {PHRASE} may be delayed.",
        "Reminder: posts mentioning {PHRASE} are filtered.",
        "Threads involving {PHRASE} may load more slowly.",
        "Mentions of {PHRASE} are subject to review.",
        "Posts discussing {PHRASE} may not appear immediately.",
        "Some replies that reference {PHRASE} can be delayed.",
        "Posts containing {PHRASE} may be temporarily held.",
        "Threads that include {PHRASE} are rate-limited.",
        "Replies mentioning {PHRASE} may require approval.",
        "Discussions involving {PHRASE} may experience delays.",
        "Please note that {PHRASE} references can slow replies.",
        "Replies that include {PHRASE} may be queued.",
        "Posts referencing {PHRASE} are monitored.",
        "Threads mentioning {PHRASE} may be processed later.",
        "Mentions of {PHRASE} may affect post timing.",
        "Replies involving {PHRASE} are occasionally delayed.",
        "Posts that reference {PHRASE} may be reviewed.",
        "Threads containing {PHRASE} may load inconsistently.",
        "Mentions of {PHRASE} can trigger moderation checks.",
        "Replies mentioning {PHRASE} may not post instantly.",
        "Posts involving {PHRASE} are sometimes delayed.",
        "Threads referencing {PHRASE} may appear slower.",
        "Replies that include {PHRASE} can be deferred.",
        "Mentions of {PHRASE} may affect visibility.",
        "Posts mentioning {PHRASE} may be queued briefly.",
        "Replies referencing {PHRASE} are processed carefully.",
        "Threads involving {PHRASE} may have slower updates.",
        "Posts containing {PHRASE} are occasionally filtered.",
        "Mentions of {PHRASE} may result in short delays.",
        "Replies that reference {PHRASE} may be staged.",
        "Threads mentioning {PHRASE} may update slowly.",
        "Posts involving {PHRASE} can experience lag.",
        "Replies containing {PHRASE} may post later.",
        "Mentions of {PHRASE} may pause submissions.",
        "Threads referencing {PHRASE} may load gradually."
    };

    public CaptchaChallenge GenerateChallenge()
    {
        // 5% chance for Math Captcha
        if (RandomNumberGenerator.GetInt32(100) < MathChallengeChancePercent)
        {
            return GenerateMathChallenge();
        }
        
        return GenerateTextChallenge();
    }

    private CaptchaChallenge GenerateTextChallenge()
    {
        var adjective = Adjectives[RandomNumberGenerator.GetInt32(Adjectives.Length)];
        var noun = Nouns[RandomNumberGenerator.GetInt32(Nouns.Length)];
        var phrase = $"{adjective} {noun}";
        var template = Templates[RandomNumberGenerator.GetInt32(Templates.Length)];
        var sentence = template.Replace("{PHRASE}", phrase);

        return new CaptchaChallenge
        {
            Context = sentence,
            Question = "What was the subject matter of the above sentence?",
            Answer = phrase
        };
    }

    private CaptchaChallenge GenerateMathChallenge()
    {
        var a = RandomNumberGenerator.GetInt32(10, 45); // 10 to 44
        var b = RandomNumberGenerator.GetInt32(10, 45); // 10 to 44
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
