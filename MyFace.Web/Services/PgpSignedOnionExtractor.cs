using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace MyFace.Web.Services;

public sealed record PgpSignedOnion(string OnionUrl, string ProofContent);

public static class PgpSignedOnionExtractor
{
    private static readonly Regex SignedBlockRegex = new(
        @"-----BEGIN PGP SIGNED MESSAGE-----.*?\r?\n\r?\n(?<body>.*?)-----BEGIN PGP SIGNATURE-----.*?-----END PGP SIGNATURE-----",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex OnionRegex = new(
        @"(?<url>(?:https?://)?[a-z0-9]{16,256}\.(?:onion|i2p)(?:/\S*)?)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static IReadOnlyList<PgpSignedOnion> Extract(string? input)
    {
        var results = new List<PgpSignedOnion>();
        if (string.IsNullOrWhiteSpace(input))
        {
            return results;
        }

        foreach (Match match in SignedBlockRegex.Matches(input))
        {
            if (!match.Success)
            {
                continue;
            }

            var rawBlock = match.Value.Trim();
            var body = match.Groups["body"].Value.Replace("\r", string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(body))
            {
                continue;
            }

            var onionMatch = OnionRegex.Match(body);
            if (!onionMatch.Success)
            {
                continue;
            }

            var onion = onionMatch.Groups["url"].Value.Trim();
            results.Add(new PgpSignedOnion(onion, rawBlock));
        }

        return results;
    }
}
