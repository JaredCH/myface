using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Services;

namespace MyFace.Web.Controllers;

public class VoteController : Controller
{
    private readonly ForumService _forumService;
    private readonly CaptchaService _captchaService;

    public VoteController(ForumService forumService, CaptchaService captchaService)
    {
        _forumService = forumService;
        _captchaService = captchaService;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Up(int postId, string captchaAnswer)
    {
        // 25% chance to trigger captcha
        // If captchaAnswer is present, we are verifying
        // If not, we roll the dice
        
        if (string.IsNullOrEmpty(captchaAnswer))
        {
            var rnd = new Random();
            if (rnd.Next(0, 4) == 0) // 25% chance (0, 1, 2, 3)
            {
                // Trigger Captcha
                var challenge = _captchaService.GenerateChallenge();
                HttpContext.Session.SetString("VoteCaptchaAnswer", challenge.Answer);
                HttpContext.Session.SetInt32("VotePostId", postId);
                
                ViewBag.CaptchaContext = challenge.Context;
                ViewBag.CaptchaQuestion = challenge.Question;
                ViewBag.PostId = postId;
                ViewBag.IsUpvote = true;
                return View("VoteCaptcha");
            }
        }
        else
        {
            // Verify
            var expected = HttpContext.Session.GetString("VoteCaptchaAnswer");
            var storedPostId = HttpContext.Session.GetInt32("VotePostId");
            
            if (storedPostId != postId || !_captchaService.Validate(expected, captchaAnswer))
            {
                // Failed
                var challenge = _captchaService.GenerateChallenge();
                HttpContext.Session.SetString("VoteCaptchaAnswer", challenge.Answer);
                HttpContext.Session.SetInt32("VotePostId", postId);
                
                ViewBag.CaptchaContext = challenge.Context;
                ViewBag.CaptchaQuestion = challenge.Question;
                ViewBag.PostId = postId;
                ViewBag.IsUpvote = true;
                ModelState.AddModelError("", "Incorrect answer.");
                return View("VoteCaptcha");
            }
            
            // Success
            HttpContext.Session.Remove("VoteCaptchaAnswer");
            HttpContext.Session.Remove("VotePostId");
        }

        var userId = GetCurrentUserId();
        var sessionId = HttpContext.Session.Id;
        await _forumService.VoteAsync(postId, userId, sessionId, +1);
        var post = await _forumService.GetPostByIdAsync(postId);
        return RedirectToAction("View", "Thread", new { id = post?.ThreadId ?? 1 });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Down(int postId)
    {
        // Downvotes don't trigger captcha per requirements ("Upvotes trigger it 25% of the time")
        var userId = GetCurrentUserId();
        var sessionId = HttpContext.Session.Id;
        await _forumService.VoteAsync(postId, userId, sessionId, -1);
        var post = await _forumService.GetPostByIdAsync(postId);
        return RedirectToAction("View", "Thread", new { id = post?.ThreadId ?? 1 });
    }

    // Thread voting
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThreadUp(int threadId, string captchaAnswer)
    {
        // 25% chance to trigger captcha (same as post upvotes)
        if (string.IsNullOrEmpty(captchaAnswer))
        {
            var rnd = new Random();
            if (rnd.Next(0, 4) == 0) // 25% chance
            {
                var challenge = _captchaService.GenerateChallenge();
                HttpContext.Session.SetString("ThreadVoteCaptchaAnswer", challenge.Answer);
                HttpContext.Session.SetInt32("VoteThreadId", threadId);
                
                ViewBag.CaptchaContext = challenge.Context;
                ViewBag.CaptchaQuestion = challenge.Question;
                ViewBag.ThreadId = threadId;
                ViewBag.IsUpvote = true;
                ViewBag.IsThreadVote = true;
                return View("VoteCaptcha");
            }
        }
        else
        {
            var expected = HttpContext.Session.GetString("ThreadVoteCaptchaAnswer");
            var storedThreadId = HttpContext.Session.GetInt32("VoteThreadId");
            
            if (storedThreadId != threadId || !_captchaService.Validate(expected, captchaAnswer))
            {
                var challenge = _captchaService.GenerateChallenge();
                HttpContext.Session.SetString("ThreadVoteCaptchaAnswer", challenge.Answer);
                HttpContext.Session.SetInt32("VoteThreadId", threadId);
                
                ViewBag.CaptchaContext = challenge.Context;
                ViewBag.CaptchaQuestion = challenge.Question;
                ViewBag.ThreadId = threadId;
                ViewBag.IsUpvote = true;
                ViewBag.IsThreadVote = true;
                ModelState.AddModelError("", "Incorrect answer.");
                return View("VoteCaptcha");
            }
            
            HttpContext.Session.Remove("ThreadVoteCaptchaAnswer");
            HttpContext.Session.Remove("VoteThreadId");
        }

        var userId = GetCurrentUserId();
        var sessionId = HttpContext.Session.Id;
        await _forumService.VoteOnThreadAsync(threadId, userId, sessionId, +1);
        return RedirectToAction("View", "Thread", new { id = threadId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ThreadDown(int threadId)
    {
        var userId = GetCurrentUserId();
        var sessionId = HttpContext.Session.Id;
        await _forumService.VoteOnThreadAsync(threadId, userId, sessionId, -1);
        return RedirectToAction("View", "Thread", new { id = threadId });
    }

    private int? GetCurrentUserId()
    {
        var claim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        return int.TryParse(claim?.Value, out var id) ? id : (int?)null;
    }
}
