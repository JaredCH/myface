using System.Collections.Generic;
using MyFace.Services;

namespace MyFace.Web.Models.ControlPanel;

public class ContentMetricsViewModel
{
    public ContentMetricsViewModel(ContentMetricsResult data, ContentMetricsRange selectedRange)
    {
        Data = data;
        SelectedRange = selectedRange;
    }

    public ContentMetricsResult Data { get; }
    public ContentMetricsRange SelectedRange { get; }
    public IReadOnlyList<ContentMetricsRange> AvailableRanges => ContentMetricsRangeExtensions.All;
    public string SelectedRangeLabel => ContentMetricsRangeExtensions.ToLabel(SelectedRange);
    public string RangeWindowDescription => $"{SelectedRangeLabel} · since {Data.RangeStartUtc:yyyy-MM-dd HH:mm} UTC";
    public string AveragePostsPerThreadDisplay => Data.AveragePostsPerThread <= 0
        ? "0"
        : Data.AveragePostsPerThread.ToString("0.0");

    public string AnonymousRatioDisplay => Data.AnonymousPostRatio <= 0
        ? "0%"
        : $"{Data.AnonymousPostRatio:P1}";
}
