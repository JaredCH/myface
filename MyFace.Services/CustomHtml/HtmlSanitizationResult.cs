using System.Collections.Generic;

namespace MyFace.Services.CustomHtml;

public sealed class HtmlSanitizationResult
{
    private HtmlSanitizationResult(
        bool isSuccess,
        string sanitizedHtml,
        IReadOnlyList<string> errors,
        int inputBytes,
        int outputBytes,
        int nodeCount,
        bool inputLimitExceeded,
        bool outputLimitExceeded,
        bool nodeLimitExceeded)
    {
        IsSuccess = isSuccess;
        SanitizedHtml = sanitizedHtml;
        Errors = errors;
        InputBytes = inputBytes;
        OutputBytes = outputBytes;
        NodeCount = nodeCount;
        InputLimitExceeded = inputLimitExceeded;
        OutputLimitExceeded = outputLimitExceeded;
        NodeLimitExceeded = nodeLimitExceeded;
    }

    public bool IsSuccess { get; }
    public string SanitizedHtml { get; }
    public IReadOnlyList<string> Errors { get; }
    public int InputBytes { get; }
    public int OutputBytes { get; }
    public int NodeCount { get; }
    public bool InputLimitExceeded { get; }
    public bool OutputLimitExceeded { get; }
    public bool NodeLimitExceeded { get; }

    public static HtmlSanitizationResult Success(string sanitizedHtml, int nodeCount, int inputBytes, int outputBytes)
    {
        return new HtmlSanitizationResult(true, sanitizedHtml, Array.Empty<string>(), inputBytes, outputBytes, nodeCount, false, false, false);
    }

    public static HtmlSanitizationResult Failure(
        IReadOnlyList<string> errors,
        int inputBytes,
        int outputBytes,
        int nodeCount,
        bool inputLimitExceeded,
        bool outputLimitExceeded,
        bool nodeLimitExceeded)
    {
        return new HtmlSanitizationResult(false, string.Empty, errors, inputBytes, outputBytes, nodeCount, inputLimitExceeded, outputLimitExceeded, nodeLimitExceeded);
    }
}
