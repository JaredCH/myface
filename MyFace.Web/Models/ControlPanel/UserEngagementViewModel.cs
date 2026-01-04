using System.Globalization;
using MyFace.Services;

namespace MyFace.Web.Models.ControlPanel;

public class UserEngagementViewModel
{
    public UserEngagementViewModel(UserEngagementResult data)
    {
        Data = data;
    }

    public UserEngagementResult Data { get; }

    public bool HasGrowthData => Data.GrowthSeries.Count > 0;

    public string FormatDate(DateTime? value)
    {
        return value.HasValue
            ? value.Value.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) + " UTC"
            : "—";
    }

    public string FormatSnippet(string snippet)
    {
        return string.IsNullOrWhiteSpace(snippet)
            ? "No preview available."
            : snippet;
    }
}
