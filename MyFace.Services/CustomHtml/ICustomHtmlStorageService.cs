using System.Threading;
using System.Threading.Tasks;

namespace MyFace.Services.CustomHtml;

public interface ICustomHtmlStorageService
{
    Task<CustomHtmlStorageResult> SaveAsync(CustomHtmlStorageRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int userId, string username, CancellationToken cancellationToken = default);
    Task<CustomHtmlFileInfo?> GetInfoAsync(int userId, string username, CancellationToken cancellationToken = default);
}
