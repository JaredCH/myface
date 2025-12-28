using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MyFace.Core.Entities;
using MyFace.Services;
using MyFace.Web.Models;
using MyFace.Web.Services;
using ThreadEntity = MyFace.Core.Entities.Thread;

namespace MyFace.Web.Controllers;

public class ThreadTestController : Controller
{
    private readonly ForumService _forumService;
    private readonly CaptchaService _captchaService;
    private readonly ThreadImageStorageService _imageStorageService;

    public ThreadTestController(ForumService forumService, CaptchaService captchaService, ThreadImageStorageService imageStorageService)
    {
        _forumService = forumService;
        _captchaService = captchaService;
        _imageStorageService = imageStorageService;
    }

    [HttpGet("/threadtest")]
    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 25;
        ViewBag.HotThreads = await _forumService.GetHotThreadsAsync(pageSize);

        var allThreads = await _forumService.GetThreadsAsync(0, 1000);
        ViewBag.NewThreads = allThreads.OrderByDescending(t => t.CreatedAt).Take(pageSize).ToList();
        ViewBag.NewsThreads = allThreads.Where(t => t.Category == "News").OrderByDescending(t => t.CreatedAt).Take(pageSize).ToList();
        ViewBag.AnnouncementsThreads = allThreads.Where(t => t.Category == "Announcements").OrderByDescending(t => t.CreatedAt).Take(pageSize).ToList();

        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role == "Admin" || role == "Moderator")
        {
            ViewBag.ReportedPosts = await _forumService.GetReportedPostsAsync();
        }
        else
        {
            ViewBag.ReportedPosts = new List<Post>();
        }
        ViewBag.ReportHideThreshold = _forumService.GetReportHideThreshold();
        ViewBag.CurrentPage = page;
        ViewBag.HasMorePages = false;

        return View("Index", ViewBag.HotThreads as List<ThreadEntity> ?? new List<ThreadEntity>());
    }

    [HttpGet("/threadtest/create")]
    public IActionResult Create()
    {
        PrepareCreateCaptcha();
        return View("Create", new ThreadTestCreateViewModel());
    }

    [HttpPost("/threadtest/create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ThreadTestCreateViewModel model, string captchaAnswer, CancellationToken cancellationToken)
    {
        if (!ValidateCreateCaptcha(captchaAnswer))
        {
            ModelState.AddModelError("Captcha", "Incorrect security check answer.");
            PrepareCreateCaptcha();
            return View("Create", model);
        }

        var imageCount = model.Images?.Count ?? 0;
        if (imageCount > 2)
        {
            ModelState.AddModelError("Images", "You can attach up to two images.");
        }

        if (!ModelState.IsValid)
        {
            PrepareCreateCaptcha();
            return View("Create", model);
        }

        List<StoredImageResult> uploads = new();
        try
        {
            uploads = (await _imageStorageService.SaveImagesAsync(model.Images, cancellationToken)).ToList();
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError("Images", ex.Message);
            PrepareCreateCaptcha();
            return View("Create", model);
        }

        var userId = GetCurrentUserId();
        var isAnonymous = model.PostAsAnonymous || userId == null;

        var thread = await _forumService.CreateThreadAsync(model.Title, userId, isAnonymous);
        var post = await _forumService.CreatePostAsync(thread.Id, model.Content, userId, isAnonymous);

        var order = 0;
        foreach (var upload in uploads)
        {
            await _forumService.AddPostImageAsync(post.Id, upload.OriginalUrl, upload.ThumbnailUrl, upload.ContentType, upload.Width, upload.Height, upload.FileSize, order++);
        }

        return RedirectToAction("ViewTest", new { id = thread.Id });
    }

    [HttpGet("/thread/viewtest/{id:int}")]
    public async Task<IActionResult> ViewTest(int id)
    {
        var thread = await _forumService.GetThreadByIdAsync(id);
        if (thread == null)
        {
            return NotFound();
        }

        var currentUserId = GetCurrentUserId();
        ViewBag.CurrentUserId = currentUserId;
        ViewBag.ThreadScore = await _forumService.GetThreadScoreAsync(id);
        ViewBag.ReportHideThreshold = _forumService.GetReportHideThreshold();

        var postScores = new Dictionary<int, int>();
        foreach (var post in thread.Posts)
        {
            postScores[post.Id] = await _forumService.GetPostScoreAsync(post.Id);
        }
        ViewBag.PostScores = postScores;

        PrepareReplyCaptcha();
        return View("ViewTest", thread);
    }

    [HttpPost("/thread/viewtest/reply")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReplyTest(ThreadTestReplyViewModel model, string captchaAnswer, CancellationToken cancellationToken)
    {
        if (!ValidateReplyCaptcha(captchaAnswer))
        {
            return RedirectToAction("ViewTest", new { id = model.ThreadId, error = "CaptchaFailed" });
        }

        if (!ModelState.IsValid)
        {
            return RedirectToAction("ViewTest", new { id = model.ThreadId });
        }

        if ((model.Images?.Count ?? 0) > 2)
        {
            TempData["ThreadTestReplyError"] = "You can attach up to two images.";
            return RedirectToAction("ViewTest", new { id = model.ThreadId });
        }

        List<StoredImageResult> uploads = new();
        try
        {
            uploads = (await _imageStorageService.SaveImagesAsync(model.Images, cancellationToken)).ToList();
        }
        catch (InvalidOperationException ex)
        {
            TempData["ThreadTestReplyError"] = ex.Message;
            return RedirectToAction("ViewTest", new { id = model.ThreadId });
        }

        var userId = GetCurrentUserId();
        var isAnonymous = model.PostAsAnonymous || userId == null;
        var post = await _forumService.CreatePostAsync(model.ThreadId, model.Content, userId, isAnonymous);

        var order = 0;
        foreach (var upload in uploads)
        {
            await _forumService.AddPostImageAsync(post.Id, upload.OriginalUrl, upload.ThumbnailUrl, upload.ContentType, upload.Width, upload.Height, upload.FileSize, order++);
        }

        return RedirectToAction("ViewTest", new { id = model.ThreadId });
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        return userIdClaim != null && int.TryParse(userIdClaim.Value, out var userId)
            ? userId
            : null;
    }

    private void PrepareCreateCaptcha()
    {
        var challenge = _captchaService.GenerateChallenge();
        HttpContext.Session.SetString("ThreadTestCaptchaAnswer", challenge.Answer);
        ViewBag.CaptchaContext = challenge.Context;
        ViewBag.CaptchaQuestion = challenge.Question;
    }

    private void PrepareReplyCaptcha()
    {
        var challenge = _captchaService.GenerateChallenge();
        HttpContext.Session.SetString("ReplyTestCaptchaAnswer", challenge.Answer);
        ViewBag.CaptchaContext = challenge.Context;
        ViewBag.CaptchaQuestion = challenge.Question;
    }

    private bool ValidateCreateCaptcha(string captchaAnswer)
    {
        var expected = HttpContext.Session.GetString("ThreadTestCaptchaAnswer");
        var isValid = _captchaService.Validate(expected, captchaAnswer);
        if (isValid)
        {
            HttpContext.Session.Remove("ThreadTestCaptchaAnswer");
        }
        return isValid;
    }

    private bool ValidateReplyCaptcha(string captchaAnswer)
    {
        var expected = HttpContext.Session.GetString("ReplyTestCaptchaAnswer");
        var isValid = _captchaService.Validate(expected, captchaAnswer);
        if (isValid)
        {
            HttpContext.Session.Remove("ReplyTestCaptchaAnswer");
        }
        return isValid;
    }
}
