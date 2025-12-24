using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Models;
using MyFace.Web.Services;

namespace MyFace.Web.Controllers;

public class ThreadController : Controller
{
    private readonly ForumService _forumService;
    private readonly CaptchaService _captchaService;

    public ThreadController(ForumService forumService, CaptchaService captchaService)
    {
        _forumService = forumService;
        _captchaService = captchaService;
    }

    public async Task<IActionResult> Index(int page = 1)
    {
        const int pageSize = 25;
        var threads = await _forumService.GetThreadsAsync((page - 1) * pageSize, pageSize);
        
        ViewBag.CurrentPage = page;
        ViewBag.HasMorePages = threads.Count == pageSize;
        
        return View(threads);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var challenge = _captchaService.GenerateChallenge();
        HttpContext.Session.SetString("ThreadCaptchaAnswer", challenge.Answer);
        ViewBag.CaptchaContext = challenge.Context;
        ViewBag.CaptchaQuestion = challenge.Question;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateThreadViewModel model, string captchaAnswer)
    {
        var expected = HttpContext.Session.GetString("ThreadCaptchaAnswer");
        if (!_captchaService.Validate(expected, captchaAnswer))
        {
            ModelState.AddModelError("Captcha", "Incorrect security check answer.");
            var challenge = _captchaService.GenerateChallenge();
            HttpContext.Session.SetString("ThreadCaptchaAnswer", challenge.Answer);
            ViewBag.CaptchaContext = challenge.Context;
            ViewBag.CaptchaQuestion = challenge.Question;
            return View(model);
        }
        HttpContext.Session.Remove("ThreadCaptchaAnswer");

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        int? userId = GetCurrentUserId();
        bool isAnonymous = model.PostAsAnonymous || userId == null;

        var thread = await _forumService.CreateThreadAsync(model.Title, userId, isAnonymous);
        
        // Create first post
        await _forumService.CreatePostAsync(thread.Id, model.Content, userId, isAnonymous);

        return RedirectToAction("View", new { id = thread.Id });
    }

    public async Task<IActionResult> View(int id)
    {
        var thread = await _forumService.GetThreadByIdAsync(id);
        
        if (thread == null)
        {
            return NotFound();
        }

        var currentUserId = GetCurrentUserId();
        ViewBag.CurrentUserId = currentUserId;

        // Calculate scores for posts
        var postScores = new Dictionary<int, int>();
        foreach (var post in thread.Posts)
        {
            postScores[post.Id] = await _forumService.GetPostScoreAsync(post.Id);
        }
        ViewBag.PostScores = postScores;

        // Generate Captcha for Reply
        var challenge = _captchaService.GenerateChallenge();
        HttpContext.Session.SetString("ReplyCaptchaAnswer", challenge.Answer);
        ViewBag.CaptchaContext = challenge.Context;
        ViewBag.CaptchaQuestion = challenge.Question;

        return View(thread);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int threadId, string content, string captchaAnswer, bool postAsAnonymous = false)
    {
        var expected = HttpContext.Session.GetString("ReplyCaptchaAnswer");
        if (!_captchaService.Validate(expected, captchaAnswer))
        {
            // Since we are redirecting back to View, we can't easily show the error without TempData
            // But TempData might be lost if we regenerate the captcha in View()
            // For now, let's just fail silently or redirect with error param
            return RedirectToAction("View", new { id = threadId, error = "CaptchaFailed" });
        }
        HttpContext.Session.Remove("ReplyCaptchaAnswer");

        if (string.IsNullOrWhiteSpace(content))
        {
            return RedirectToAction("View", new { id = threadId });
        }

        int? userId = GetCurrentUserId();
        bool isAnonymous = postAsAnonymous || userId == null;

        await _forumService.CreatePostAsync(threadId, content, userId, isAnonymous);

        return RedirectToAction("View", new { id = threadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Vote(int postId, bool isUpvote)
    {
        var userId = GetCurrentUserId();
        var sessionId = HttpContext.Session.Id;
        if (userId == null)
        {
            // allow anonymous session-based voting
        }

        var value = isUpvote ? 1 : -1;
        await _forumService.VoteAsync(postId, userId, sessionId, value);

        // Redirect back to the thread containing this post
        var post = await _forumService.GetPostByIdAsync(postId);
        return RedirectToAction("View", new { id = post?.ThreadId ?? 1 });
    }

    // Admin/Mod thread management
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteThread(int id)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        await _forumService.DeleteThreadAsync(id);
        return RedirectToAction("Index");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LockThread(int id)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var thread = await _forumService.GetThreadByIdAsync(id);
        if (thread == null) return NotFound();

        await _forumService.LockThreadAsync(id, true);
        return RedirectToAction("View", new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UnlockThread(int id)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var thread = await _forumService.GetThreadByIdAsync(id);
        if (thread == null) return NotFound();

        await _forumService.LockThreadAsync(id, false);
        return RedirectToAction("View", new { id });
    }

    // Admin/Mod post management
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminDeletePost(int postId)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var post = await _forumService.GetPostByIdAsync(postId);
        if (post == null) return NotFound();

        await _forumService.AdminDeletePostAsync(postId);
        return RedirectToAction("View", new { id = post.ThreadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AdminEditPost(int postId, string content)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var post = await _forumService.GetPostByIdAsync(postId);
        if (post == null) return NotFound();

        await _forumService.AdminEditPostAsync(postId, content);
        return RedirectToAction("View", new { id = post.ThreadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetSticky(int postId, bool isSticky)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            return Forbid();
        }

        var post = await _forumService.GetPostByIdAsync(postId);
        if (post == null) return NotFound();

        await _forumService.SetStickyAsync(postId, isSticky);
        return RedirectToAction("View", new { id = post.ThreadId });
    }

    private int? GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
        {
            return userId;
        }
        return null;
    }
}
