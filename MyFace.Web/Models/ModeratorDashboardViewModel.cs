using System.Collections.Generic;
using MyFace.Core.Entities;

namespace MyFace.Web.Models;

public class ModeratorDashboardViewModel
{
    public List<MyFace.Core.Entities.User> Users { get; set; } = new();
    public List<OnionStatus> Monitors { get; set; } = new();
}
