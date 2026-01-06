using System.Text;
using System.Text.RegularExpressions;

namespace MyFace.Services;

/// <summary>
/// Service for normalizing monitor link names and generating comparison keys
/// </summary>
public static class LinkNormalizationService
{
    /// <summary>
    /// Normalize a service name to Title Case with proper spacing
    /// Example: "dark matter" -> "Dark Matter", "MarsMarket" -> "Mars Market"
    /// </summary>
    public static string NormalizeToCanonical(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        // Remove mirror markers like "(mirror 1)" or "(mirror)"
        var cleaned = RemoveMirrorMarkers(name);
        
        // Remove trailing numbers that indicate duplicates (e.g., "Pitch 1" -> "Pitch")
        cleaned = RemoveTrailingNumbers(cleaned);
        
        // Insert spaces before capitals in PascalCase (e.g., MarsMarket -> Mars Market)
        cleaned = InsertSpacesBeforeCapitals(cleaned);
        
        // Normalize to Title Case
        cleaned = ToTitleCase(cleaned);
        
        // Collapse multiple spaces
        cleaned = Regex.Replace(cleaned, @"\s+", " ").Trim();
        
        return cleaned;
    }

    /// <summary>
    /// Generate a normalized comparison key (lowercase, no spaces/punctuation)
    /// Example: "Dark Matter" -> "darkmatter"
    /// </summary>
    public static string GenerateNormalizedKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var canonical = NormalizeToCanonical(name);
        
        // Remove all non-alphanumeric characters and convert to lowercase
        var key = new StringBuilder();
        foreach (var c in canonical)
        {
            if (char.IsLetterOrDigit(c))
                key.Append(char.ToLowerInvariant(c));
        }
        
        return key.ToString();
    }

    /// <summary>
    /// Remove mirror indicators from the name
    /// </summary>
    private static string RemoveMirrorMarkers(string name)
    {
        // Remove patterns like "(mirror 1)", "(mirror)", "[mirror]", etc.
        var pattern = @"\s*[\(\[]mirror[^\)\]]*[\)\]]\s*";
        return Regex.Replace(name, pattern, " ", RegexOptions.IgnoreCase).Trim();
    }

    /// <summary>
    /// Remove trailing numbers that indicate duplicate entries
    /// Example: "Pitch 1" -> "Pitch"
    /// </summary>
    private static string RemoveTrailingNumbers(string name)
    {
        // Only remove if it's a standalone number at the end
        var match = Regex.Match(name, @"^(.+?)\s+\d+$");
        if (match.Success)
            return match.Groups[1].Value.Trim();
        
        return name;
    }

    /// <summary>
    /// Insert spaces before capital letters in PascalCase strings
    /// Example: "MarsMarket" -> "Mars Market"
    /// </summary>
    private static string InsertSpacesBeforeCapitals(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var result = new StringBuilder();
        result.Append(name[0]);

        for (int i = 1; i < name.Length; i++)
        {
            var current = name[i];
            var previous = name[i - 1];

            // Insert space if:
            // 1. Current is uppercase and previous is lowercase (e.g., "marsMarket" -> "mars Market")
            // 2. Current is uppercase, previous is uppercase, and next is lowercase (e.g., "HTTPServer" -> "HTTP Server")
            if (char.IsUpper(current) && char.IsLower(previous))
            {
                result.Append(' ');
            }
            else if (i < name.Length - 1 && 
                     char.IsUpper(current) && 
                     char.IsUpper(previous) && 
                     char.IsLower(name[i + 1]))
            {
                result.Append(' ');
            }

            result.Append(current);
        }

        return result.ToString();
    }

    /// <summary>
    /// Convert string to Title Case (each word capitalized)
    /// </summary>
    private static string ToTitleCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return name;

        var words = name.Split(new[] { ' ', '\t', '-', '_' }, StringSplitOptions.RemoveEmptyEntries);
        var titleCased = new StringBuilder();

        foreach (var word in words)
        {
            if (titleCased.Length > 0)
                titleCased.Append(' ');

            if (word.Length == 0)
                continue;

            // Special handling for all-caps acronyms (keep as-is if 2-4 chars and all uppercase)
            if (word.Length >= 2 && word.Length <= 4 && word.All(char.IsUpper))
            {
                titleCased.Append(word);
            }
            else
            {
                titleCased.Append(char.ToUpper(word[0]));
                if (word.Length > 1)
                    titleCased.Append(word.Substring(1).ToLower());
            }
        }

        return titleCased.ToString();
    }

    /// <summary>
    /// Check if two service names are likely the same service
    /// </summary>
    public static bool AreSimilar(string name1, string name2)
    {
        if (string.IsNullOrWhiteSpace(name1) || string.IsNullOrWhiteSpace(name2))
            return false;

        var key1 = GenerateNormalizedKey(name1);
        var key2 = GenerateNormalizedKey(name2);

        return string.Equals(key1, key2, StringComparison.OrdinalIgnoreCase);
    }
}
