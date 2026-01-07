using System.Collections.Generic;

namespace MyFace.Services.ProfileTemplates;

public sealed record ProfileTheme(string? Preset, IReadOnlyDictionary<string, string> Overrides);
