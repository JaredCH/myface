using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace MyFace.Services;

public enum ControlSettingDataType
{
    Integer,
    Boolean
}

public sealed record ControlSettingDefinition(
    string Key,
    string Label,
    string Category,
    string Description,
    ControlSettingDataType DataType,
    string DefaultValue,
    double? Min = null,
    double? Max = null)
{
    public bool IsBoolean => DataType == ControlSettingDataType.Boolean;
}

public static class ControlSettingKeys
{
    public const string CaptchaUserMin = "captcha.threshold.user.min";
    public const string CaptchaUserMax = "captcha.threshold.user.max";
    public const string CaptchaAnonMin = "captcha.threshold.anon.min";
    public const string CaptchaAnonMax = "captcha.threshold.anon.max";
    public const string CaptchaStaffMin = "captcha.threshold.staff.min";
    public const string CaptchaStaffMax = "captcha.threshold.staff.max";

    public const string RateLoginInitialAttempts = "rate.login.initialAttempts";
    public const string RateLoginMaxDelaySeconds = "rate.login.maxDelaySeconds";
    public const string RateActivityPerHour = "rate.activity.perHour";

    public const string RefreshDashboard = "controlpanel.refresh.dashboard";
    public const string RefreshTraffic = "controlpanel.refresh.traffic";
    public const string RefreshContent = "controlpanel.refresh.content";
    public const string RefreshUsers = "controlpanel.refresh.users";
    public const string RefreshChat = "controlpanel.refresh.chat";
    public const string RefreshSecurity = "controlpanel.refresh.security";
    public const string RefreshSettings = "controlpanel.refresh.settings";

    public const string UploadsThreadMaxBytes = "uploads.thread.maxBytes";
    public const string SessionTimeoutMinutes = "session.timeout.minutes";

    public const string ChatMessageMaxLength = "chat.message.maxLength";
    public const string ChatRateWindowSeconds = "chat.rate.windowSeconds";

    public const string PostMaxLength = "posts.maxLength";
    public const string PostAllowAnonymous = "posts.allowAnonymous";
}

public static class ControlSettingsCatalog
{
    private static readonly ImmutableArray<ControlSettingDefinition> Definitions = new[]
    {
        new ControlSettingDefinition(
            ControlSettingKeys.CaptchaUserMin,
            "Captcha threshold (authenticated min)",
            "Captcha & Anti-Abuse",
            "Minimum page views before requiring a captcha for signed-in users.",
            ControlSettingDataType.Integer,
            "20",
            1,
            200),
        new ControlSettingDefinition(
            ControlSettingKeys.CaptchaUserMax,
            "Captcha threshold (authenticated max)",
            "Captcha & Anti-Abuse",
            "Maximum page views before requiring a captcha for signed-in users.",
            ControlSettingDataType.Integer,
            "40",
            1,
            300),
        new ControlSettingDefinition(
            ControlSettingKeys.CaptchaAnonMin,
            "Captcha threshold (anonymous min)",
            "Captcha & Anti-Abuse",
            "Minimum page views before forcing captcha for anonymous sessions.",
            ControlSettingDataType.Integer,
            "10",
            1,
            200),
        new ControlSettingDefinition(
            ControlSettingKeys.CaptchaAnonMax,
            "Captcha threshold (anonymous max)",
            "Captcha & Anti-Abuse",
            "Maximum page views before forcing captcha for anonymous sessions.",
            ControlSettingDataType.Integer,
            "20",
            1,
            300),
        new ControlSettingDefinition(
            ControlSettingKeys.CaptchaStaffMin,
            "Captcha threshold (staff min)",
            "Captcha & Anti-Abuse",
            "Minimum page views before captcha for moderators and admins.",
            ControlSettingDataType.Integer,
            "50",
            1,
            500),
        new ControlSettingDefinition(
            ControlSettingKeys.CaptchaStaffMax,
            "Captcha threshold (staff max)",
            "Captcha & Anti-Abuse",
            "Maximum page views before captcha for moderators and admins.",
            ControlSettingDataType.Integer,
            "75",
            1,
            600),
        new ControlSettingDefinition(
            ControlSettingKeys.RateLoginInitialAttempts,
            "Rate limit grace logins",
            "Rate Limiting",
            "Number of failed logins before backoff starts.",
            ControlSettingDataType.Integer,
            "5",
            1,
            20),
        new ControlSettingDefinition(
            ControlSettingKeys.RateLoginMaxDelaySeconds,
            "Rate limit max delay (s)",
            "Rate Limiting",
            "Maximum enforced delay between repeated login failures.",
            ControlSettingDataType.Integer,
            "900",
            30,
            3600),
        new ControlSettingDefinition(
            ControlSettingKeys.RateActivityPerHour,
            "Captcha activity threshold/hour",
            "Rate Limiting",
            "Activities allowed per hour before captcha is required for a user.",
            ControlSettingDataType.Integer,
            "10",
            1,
            200),
        new ControlSettingDefinition(
            ControlSettingKeys.RefreshDashboard,
            "Dashboard auto-refresh (s)",
            "Control Panel UX",
            "Meta refresh interval for the dashboard shell.",
            ControlSettingDataType.Integer,
            "30",
            15,
            600),
        new ControlSettingDefinition(
            ControlSettingKeys.RefreshTraffic,
            "Traffic analytics refresh (s)",
            "Control Panel UX",
            "Meta refresh interval for /control-panel/traffic.",
            ControlSettingDataType.Integer,
            "120",
            30,
            900),
        new ControlSettingDefinition(
            ControlSettingKeys.RefreshContent,
            "Content metrics refresh (s)",
            "Control Panel UX",
            "Meta refresh interval for /control-panel/content.",
            ControlSettingDataType.Integer,
            "180",
            30,
            900),
        new ControlSettingDefinition(
            ControlSettingKeys.RefreshUsers,
            "User analytics refresh (s)",
            "Control Panel UX",
            "Meta refresh interval for /control-panel/users.",
            ControlSettingDataType.Integer,
            "300",
            60,
            1800),
        new ControlSettingDefinition(
            ControlSettingKeys.RefreshChat,
            "Chat oversight refresh (s)",
            "Control Panel UX",
            "Meta refresh interval for /control-panel/chat.",
            ControlSettingDataType.Integer,
            "120",
            30,
            600),
        new ControlSettingDefinition(
            ControlSettingKeys.RefreshSecurity,
            "Security dashboard refresh (s)",
            "Control Panel UX",
            "Meta refresh interval for /control-panel/security.",
            ControlSettingDataType.Integer,
            "180",
            60,
            900),
        new ControlSettingDefinition(
            ControlSettingKeys.RefreshSettings,
            "Settings refresh (s)",
            "Control Panel UX",
            "Meta refresh interval for /control-panel/settings (0 disables auto refresh).",
            ControlSettingDataType.Integer,
            "0",
            0,
            900),
        new ControlSettingDefinition(
            ControlSettingKeys.UploadsThreadMaxBytes,
            "Thread image max size (bytes)",
            "Media & Uploads",
            "Maximum per-image size for thread uploads (bytes).",
            ControlSettingDataType.Integer,
            (6 * 1024 * 1024).ToString(),
            262144,
            10485760),
        new ControlSettingDefinition(
            ControlSettingKeys.SessionTimeoutMinutes,
            "Session idle timeout (minutes)",
            "Auth & Sessions",
            "Idle time before authenticated sessions are expired by middleware.",
            ControlSettingDataType.Integer,
            "120",
            15,
            720),
        new ControlSettingDefinition(
            ControlSettingKeys.ChatMessageMaxLength,
            "Chat message max length",
            "Chat",
            "Maximum characters allowed per chat message.",
            ControlSettingDataType.Integer,
            "199",
            50,
            500),
        new ControlSettingDefinition(
            ControlSettingKeys.ChatRateWindowSeconds,
            "Chat post cooldown (s)",
            "Chat",
            "Per-user rate limit window between chat messages (seconds).",
            ControlSettingDataType.Integer,
            "7",
            2,
            120),
        new ControlSettingDefinition(
            ControlSettingKeys.PostMaxLength,
            "Post max length",
            "Posts & Threads",
            "Maximum characters allowed per forum post.",
            ControlSettingDataType.Integer,
            "4000",
            500,
            20000),
        new ControlSettingDefinition(
            ControlSettingKeys.PostAllowAnonymous,
            "Allow anonymous posting",
            "Posts & Threads",
            "Whether users may submit posts/threads anonymously.",
            ControlSettingDataType.Boolean,
            "true")
    }.ToImmutableArray();

    public static IReadOnlyList<ControlSettingDefinition> All => Definitions;

    public static bool TryGetDefinition(string key, [NotNullWhen(true)] out ControlSettingDefinition? definition)
    {
        definition = Definitions.FirstOrDefault(d => string.Equals(d.Key, key, StringComparison.OrdinalIgnoreCase));
        return definition != null;
    }

    public static ControlSettingDefinition GetDefinition(string key)
    {
        if (TryGetDefinition(key, out var definition))
        {
            return definition;
        }

        throw new InvalidOperationException($"Unknown control setting '{key}'.");
    }

    public static string GetDefaultValue(string key)
    {
        return TryGetDefinition(key, out var definition)
            ? definition.DefaultValue
            : string.Empty;
    }
}
