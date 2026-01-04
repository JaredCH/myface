using MyFace.Services;

namespace MyFace.Web.Models.ControlPanel;

public class SecurityOverviewViewModel
{
    public SecurityOverviewViewModel(SecurityOverviewResult data)
    {
        Data = data;
    }

    public SecurityOverviewResult Data { get; }
    public bool HasAlerts => Data.Alerts.Count > 0;
}
