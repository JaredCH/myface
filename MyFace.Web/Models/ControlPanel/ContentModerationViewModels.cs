using MyFace.Core.Entities;
using MyFace.Services;

namespace MyFace.Web.Models.ControlPanel;

public class AddWordListEntryViewModel
{
    public string WordPattern { get; set; } = string.Empty;
    public WordMatchType MatchType { get; set; } = WordMatchType.WordBoundary;
    public WordActionType ActionType { get; set; } = WordActionType.InfractionAndMute;
    public int? MuteDurationHours { get; set; }
    public string? ReplacementText { get; set; }
    public bool CaseSensitive { get; set; }
    public ContentScope AppliesTo { get; set; } = ContentScope.All;
    public string? Notes { get; set; }
}

public class InfractionsViewModel
{
    public List<UserInfraction> Infractions { get; set; } = new();
    public InfractionMetrics Metrics { get; set; } = new();
    public int? FilterUserId { get; set; }
    public bool? FilterOnlyActive { get; set; }
}
