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
        await _statusService.EnsureSeedDataAsync();
        var monitors = await _statusService.GetAllAsync();
        return View(monitors);
    }

    [HttpGet("/monitor/go/{id}")]
    public async Task<IActionResult> Go(int id)
    {
        var target = await _statusService.RegisterClickAsync(id);
        if (string.IsNullOrWhiteSpace(target))
        {
            return NotFound();
        }

        return Redirect(target);
    }

    [HttpGet]
    [MyFace.Web.Services.AdminAuthorization]
    public IActionResult Add()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> Add(AddMonitorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        await _statusService.AddAsync(model.Name, model.Description, model.OnionUrl);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> Check(int id)
    {
        await _statusService.CheckAsync(id, HttpContext.RequestAborted);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> CheckAll()
    {
        await _statusService.CheckAllAsync(HttpContext.RequestAborted);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> Remove(int id)
    {
        await _statusService.RemoveAsync(id);
        return RedirectToAction("Index");
    }

    [HttpGet]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> Edit(int id)
    {
        var monitor = await _statusService.GetByIdAsync(id);
        if (monitor == null) return NotFound();
        
        var model = new AddMonitorViewModel
        {
            Name = monitor.Name,
            Description = monitor.Description,
            OnionUrl = monitor.OnionUrl
        };
        ViewBag.Id = id;
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [MyFace.Web.Services.AdminAuthorization]
    public async Task<IActionResult> Edit(int id, AddMonitorViewModel model)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Id = id;
            return View(model);
        }

        await _statusService.UpdateAsync(id, model.Name, model.Description, model.OnionUrl);
        return RedirectToAction("Index");
    }
}
