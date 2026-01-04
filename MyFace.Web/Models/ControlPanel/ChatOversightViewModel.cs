using MyFace.Services;

namespace MyFace.Web.Models.ControlPanel;

public class ChatOversightViewModel
{
    public ChatOversightViewModel(ChatOversightResult data, bool isAdmin)
    {
        Data = data;
        IsAdmin = isAdmin;
    }

    public ChatOversightResult Data { get; }
    public bool IsAdmin { get; }

    public string VerifiedShareDisplay => Data.VerifiedShare <= 0 ? "0%" : Data.VerifiedShare.ToString("P1");
    public string ModeratorShareDisplay => Data.ModeratorShare <= 0 ? "0%" : Data.ModeratorShare.ToString("P1");
    public bool HasMutes => Data.ActiveMutes.Count > 0;
}
