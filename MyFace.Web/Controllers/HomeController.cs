using Microsoft.AspNetCore.Mvc;

namespace MyFace.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Thread");
    }

    public IActionResult Privacy()
    {
        return View();
    }
}
