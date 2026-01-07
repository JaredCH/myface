using System.Collections.Generic;
using MyFace.Core.Entities;

namespace MyFace.Web.Models;

public class ModeratorDashboardViewModel
{
    public List<MyFace.Core.Entities.User> Users { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public string? SearchQuery { get; set; }
    public string SortBy { get; set; } = "username";
}
