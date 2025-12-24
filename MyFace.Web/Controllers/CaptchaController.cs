using Microsoft.AspNetCore.Mvc;
using MyFace.Web.Services;

namespace MyFace.Web.Controllers;

public class CaptchaController : Controller
{
    private readonly CaptchaService _captchaService;

    public CaptchaController(CaptchaService captchaService)
    {
        _captchaService = captchaService;
    }

    [HttpGet]
    public IActionResult Index(string returnUrl)
    {
        var challenge = _captchaService.GenerateChallenge();
        HttpContext.Session.SetString("CaptchaAnswer", challenge.Answer);
        ViewBag.Context = challenge.Context;
        ViewBag.Question = challenge.Question;
        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Verify(string answer, string returnUrl)
    {
        var expected = HttpContext.Session.GetString("CaptchaAnswer");
        if (_captchaService.Validate(expected, answer))
        {
            // Reset page view count
            HttpContext.Session.SetInt32("PageViews", 0);
            HttpContext.Session.SetInt32("CaptchaThreshold", CaptchaSettings.NextThreshold());
            HttpContext.Session.Remove("CaptchaAnswer");
            
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return RedirectToAction("Index", "Home");
        }

        // Failed
        ModelState.AddModelError("", "Incorrect answer. Please try again.");
        
        // Regenerate to prevent brute force on same question
        var challenge = _captchaService.GenerateChallenge();
        HttpContext.Session.SetString("CaptchaAnswer", challenge.Answer);
        ViewBag.Context = challenge.Context;
        ViewBag.Question = challenge.Question;
        ViewBag.ReturnUrl = returnUrl;
        
        return View("Index");
    }
}
