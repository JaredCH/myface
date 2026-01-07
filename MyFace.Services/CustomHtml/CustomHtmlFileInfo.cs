using System;
using System.Collections.Generic;

namespace MyFace.Services.CustomHtml;

public sealed record CustomHtmlFileInfo(
    bool Exists,
    string RelativePath,
    string AbsolutePath,
    DateTimeOffset? LastModified,
    long? FileSize,
    IReadOnlyList<string> BackupRelativePaths);
