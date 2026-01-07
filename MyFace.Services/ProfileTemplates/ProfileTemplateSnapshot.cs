using System.Collections.Generic;
using MyFace.Core.Entities;

namespace MyFace.Services.ProfileTemplates;

public sealed record ProfileTemplateSnapshot(
    UserProfileSettings Settings,
    IReadOnlyList<ProfilePanel> Panels,
    ProfileTheme Theme);
