using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Models;

namespace MyFace.Web.Controllers;

public class MonitorController : Controller
{
    private readonly OnionMonitorService _monitorService;

    public MonitorController(OnionMonitorService monitorService)
    {
        _monitorService = monitorService;
    }

    public async Task<IActionResult> Index()
    {
        var monitors = await _monitorService.GetAllMonitorsAsync();
        return View(monitors);
    }

    [HttpGet]
    public IActionResult Add()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(AddMonitorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _monitorService.AddMonitorAsync(model.OnionUrl, model.FriendlyName, model.Notes);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Check(int id)
    {
        await _monitorService.CheckMonitorAsync(id);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckAll()
    {
        await _monitorService.CheckAllMonitorsAsync();
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        await _monitorService.RemoveMonitorAsync(id);
        return RedirectToAction("Index");
    }
}
