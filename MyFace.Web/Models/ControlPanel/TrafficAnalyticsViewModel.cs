using MyFace.Services;

namespace MyFace.Web.Models.ControlPanel;

public class TrafficAnalyticsViewModel
{
    public TrafficAnalyticsViewModel(TrafficAnalyticsResult analytics, bool includeSensitive)
    {
        Analytics = analytics;
        IncludeSensitive = includeSensitive;
    }

    public TrafficAnalyticsResult Analytics { get; }
    public bool IncludeSensitive { get; }

    public string AverageSessionDisplay
    {
        get
        {
            var seconds = Analytics.AverageSessionSeconds;
            if (seconds <= 0)
            {
                return "0s";
            }

            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalMinutes < 1)
            {
                return $"{ts.Seconds}s";
            }

            if (ts.TotalHours < 1)
            {
                return $"{ts.Minutes}m {ts.Seconds}s";
            }

            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        }
    }

    public string BounceRateDisplay => IncludeSensitive ? $"{Analytics.BounceRate:P1}" : "Hidden";
}
