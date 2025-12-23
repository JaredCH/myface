using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Models;

namespace MyFace.Web.Controllers;

public class MonitorController : Controller
{
    private readonly OnionStatusService _statusService;

    public MonitorController(OnionStatusService statusService)
    {
        _statusService = statusService;
    }

    public async Task<IActionResult> Index()
    {
        var monitors = await _statusService.GetAllAsync();
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

        await _statusService.AddAsync(model.OnionUrl);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Check(int id)
    {
        await _statusService.CheckAsync(id);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CheckAll()
    {
        await _statusService.CheckAllAsync();
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        await _statusService.RemoveAsync(id);
        return RedirectToAction("Index");
    }
}
