using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyFace.Core.Entities;

namespace MyFace.Services.ProfileTemplates;

public interface IProfileTemplateService
{
    Task<UserProfileSettings> GetOrCreateSettingsAsync(int userId, int? editorUserId = null, CancellationToken cancellationToken = default);
    Task<UserProfileSettings> UpdateTemplateAsync(int userId, ProfileTemplate template, int? editorUserId = null, CancellationToken cancellationToken = default);
    Task<UserProfileSettings> ApplyThemeAsync(int userId, string? preset, IDictionary<string, string>? overrides, int? editorUserId = null, CancellationToken cancellationToken = default);
    Task<ProfileTemplateSnapshot> GetProfileAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProfilePanel>> GetPanelsAsync(int userId, ProfileTemplate? template = null, bool includeHidden = false, CancellationToken cancellationToken = default);
    Task<ProfilePanel> CreatePanelAsync(int userId, ProfilePanelType panelType, string content, string contentFormat, int? editorUserId = null, CancellationToken cancellationToken = default);
    Task<ProfilePanel> UpdatePanelAsync(int userId, int panelId, string content, string contentFormat, int? editorUserId = null, CancellationToken cancellationToken = default);
    Task DeletePanelAsync(int userId, int panelId, CancellationToken cancellationToken = default);
    Task TogglePanelVisibilityAsync(int userId, int panelId, bool isVisible, int? editorUserId = null, CancellationToken cancellationToken = default);
    Task ReorderPanelsAsync(int userId, IReadOnlyDictionary<int, int> positions, CancellationToken cancellationToken = default);
    IReadOnlyList<ProfilePanelType> GetPanelTypesForTemplate(ProfileTemplate template);
}
