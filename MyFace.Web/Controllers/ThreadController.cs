using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Models;

namespace MyFace.Web.Controllers;

public class ThreadController : Controller
{
    private readonly ForumService _forumService;

    public ThreadController(ForumService forumService)
    {
        _forumService = forumService;
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
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateThreadViewModel model)
    {
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

        return View(thread);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reply(int threadId, string content, bool postAsAnonymous = false)
    {
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
