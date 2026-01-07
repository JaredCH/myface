using System;
using System.Collections.Generic;

namespace MyFace.Services.CustomHtml;

public sealed record CustomHtmlStorageResult(
    string RelativePath,
    string AbsolutePath,
    long FileSize,
    int Version,
    DateTimeOffset SavedAt,
    IReadOnlyList<string> BackupRelativePaths);
