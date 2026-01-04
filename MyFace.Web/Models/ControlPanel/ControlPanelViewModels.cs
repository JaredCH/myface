using MyFace.Services;

namespace MyFace.Web.Models.ControlPanel;

public class ControlPanelPageViewModel
{
    public ControlPanelPageViewModel(string sectionKey, string sectionTitle, ControlPanelSnapshot snapshot, string? placeholderBody = null, int refreshIntervalSeconds = 60, bool isAdmin = false)
    {
        SectionKey = sectionKey;
        SectionTitle = sectionTitle;
        Snapshot = snapshot;
        PlaceholderBody = placeholderBody;
        RefreshIntervalSeconds = Math.Max(0, refreshIntervalSeconds);
        IsAdmin = isAdmin;
    }

    public string SectionKey { get; }
    public string SectionTitle { get; }
    public ControlPanelSnapshot Snapshot { get; }
    public string? PlaceholderBody { get; }
    public int RefreshIntervalSeconds { get; }
    public bool IsAdmin { get; }
}

public class ControlPanelNavLink
{
    public ControlPanelNavLink(string key, string label, string url, bool requiresAdmin = false)
    {
        Key = key;
        Label = label;
        Url = url;
        RequiresAdmin = requiresAdmin;
    }

    public string Key { get; }
    public string Label { get; }
    public string Url { get; }
    public bool RequiresAdmin { get; }
    public bool IsActive { get; set; }
}
