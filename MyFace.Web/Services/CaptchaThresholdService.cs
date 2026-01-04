using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using MyFace.Services;

namespace MyFace.Web.Services;

public class CaptchaThresholdService
{
    private readonly ControlSettingsReader _settings;

    public CaptchaThresholdService(ControlSettingsReader settings)
    {
        _settings = settings;
    }

    public async Task<(int Min, int Max)> GetRangeAsync(ClaimsPrincipal? user, CancellationToken ct = default)
    {
        var isAuthenticated = user?.Identity?.IsAuthenticated == true;
        var role = user?.FindFirstValue(ClaimTypes.Role)?.ToLowerInvariant();
        string minKey;
        string maxKey;

        if (!isAuthenticated)
        {
            minKey = ControlSettingKeys.CaptchaAnonMin;
            maxKey = ControlSettingKeys.CaptchaAnonMax;
        }
        else if (string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(role, "moderator", StringComparison.OrdinalIgnoreCase))
        {
            minKey = ControlSettingKeys.CaptchaStaffMin;
            maxKey = ControlSettingKeys.CaptchaStaffMax;
        }
        else
        {
            minKey = ControlSettingKeys.CaptchaUserMin;
            maxKey = ControlSettingKeys.CaptchaUserMax;
        }

        var min = await _settings.GetIntAsync(minKey, 10, ct);
        var max = await _settings.GetIntAsync(maxKey, 20, ct);
        if (max < min)
        {
            max = min;
        }

        return (min, max);
    }

    public async Task<int> NextThresholdAsync(ClaimsPrincipal? user, CancellationToken ct = default)
    {
        var (min, max) = await GetRangeAsync(user, ct);
        return RandomNumberGenerator.GetInt32(min, max + 1);
    }
}
