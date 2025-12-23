using Microsoft.AspNetCore.Mvc;
using MyFace.Services;

namespace MyFace.Web.Controllers;

public class RssController : Controller
{
    private readonly RssService _rssService;

    public RssController(RssService rssService)
    {
        _rssService = rssService;
    }

    [HttpGet("/rss/threads")]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> Threads()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var rss = await _rssService.GenerateThreadsFeedAsync(baseUrl);
        return Content(rss, "application/rss+xml");
    }

    [HttpGet("/rss/user/{username}")]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> UserFeed(string username)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var rss = await _rssService.GenerateUserFeedAsync(username, baseUrl);
        
        if (string.IsNullOrEmpty(rss))
        {
            return NotFound();
        }

        return Content(rss, "application/rss+xml");
    }

    [HttpGet("/rss/monitor")]
    [ResponseCache(Duration = 300)]
    public async Task<IActionResult> Monitor()
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}";
        var rss = await _rssService.GenerateMonitorFeedAsync(baseUrl);
        return Content(rss, "application/rss+xml");
    }
}
