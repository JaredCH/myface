using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using MyFace.Services;
using MyFace.Web.Models;
using MyFace.Core.Entities;
using MyFace.Data;

namespace MyFace.Web.Controllers;

public class AccountController : Controller
{
    private readonly UserService _userService;
    private readonly ApplicationDbContext _db;
    private readonly MyFace.Web.Services.CaptchaService _captchaService;

    public AccountController(UserService userService, ApplicationDbContext db, MyFace.Web.Services.CaptchaService captchaService)
    {
        _userService = userService;
        _db = db;
        _captchaService = captchaService;
    }

    [HttpGet]
    public IActionResult Register()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userService.RegisterAsync(model.Username, model.Password, model.PgpPublicKey);
        
        if (user == null)
        {
            ModelState.AddModelError("", "Username already exists.");
            return View(model);
        }

        await SignInUserAsync(user.Id, user.Username);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Login()
    {
        var challenge = _captchaService.GenerateChallenge();
        HttpContext.Session.SetString("LoginCaptchaAnswer", challenge.Answer);
        ViewBag.CaptchaContext = challenge.Context;
        ViewBag.CaptchaQuestion = challenge.Question;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string captchaAnswer)
    {
        // Verify captcha first
        var correctAnswer = HttpContext.Session.GetString("LoginCaptchaAnswer");
        if (string.IsNullOrEmpty(captchaAnswer) || captchaAnswer != correctAnswer)
        {
            ModelState.AddModelError("", "Incorrect security check answer.");
            // Regenerate captcha
            var challenge = _captchaService.GenerateChallenge();
            HttpContext.Session.SetString("LoginCaptchaAnswer", challenge.Answer);
            ViewBag.CaptchaContext = challenge.Context;
            ViewBag.CaptchaQuestion = challenge.Question;
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userService.AuthenticateAsync(model.Username, model.Password);
        
        if (user == null)
        {
            ModelState.AddModelError("", "Invalid username or password.");
            // Regenerate captcha
            var challenge = _captchaService.GenerateChallenge();
            HttpContext.Session.SetString("LoginCaptchaAnswer", challenge.Answer);
            ViewBag.CaptchaContext = challenge.Context;
            ViewBag.CaptchaQuestion = challenge.Question;
            return View(model);
        }

        await SignInUserAsync(user.Id, user.Username);
        return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private async Task SignInUserAsync(int userId, string username)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        var role = user?.Role ?? "User";

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username),
            new Claim(ClaimTypes.Role, role)
        };

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var authProperties = new AuthenticationProperties
        {
            IsPersistent = true,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30)
        };

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity),
            authProperties);
    }

    [HttpGet]
    public IActionResult AttachPgp()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AttachPgp(AttachPgpViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login");
        }

        var user = await _userService.GetByIdAsync(int.Parse(userId));
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        user.PgpPublicKey = model.PgpPublicKey;
        await _db.SaveChangesAsync();
        return RedirectToAction("Index", "User", new { username = user.Username });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPgpChallenge(RequestPgpChallengeViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login");
        }

        var user = await _userService.GetByIdAsync(int.Parse(userId));
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid fingerprint provided.";
            return RedirectToAction("Index", "User", new { username = user.Username });
        }

        var challenge = new PGPVerification
        {
            UserId = user.Id,
            Fingerprint = model.Fingerprint,
            ChallengeText = Guid.NewGuid().ToString("N"),
            Verified = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.PGPVerifications.Add(challenge);
        await _db.SaveChangesAsync();
        TempData["Challenge"] = challenge.ChallengeText;
        return RedirectToAction("Index", "User", new { username = user.Username });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyPgpChallenge(VerifyPgpChallengeViewModel model)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login");
        }

        var user = await _userService.GetByIdAsync(int.Parse(userId));
        if (user == null)
        {
            return RedirectToAction("Login");
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Invalid verification response.";
            return RedirectToAction("Index", "User", new { username = user.Username });
        }

        var latest = _db.PGPVerifications
            .Where(v => v.UserId == user.Id)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefault();

        if (latest == null)
        {
            TempData["Error"] = "No challenge to verify.";
            return RedirectToAction("Index", "User", new { username = user.Username });
        }

        if (string.IsNullOrWhiteSpace(user.PgpPublicKey))
        {
            TempData["Error"] = "No PGP key attached.";
            return RedirectToAction("Index", "User", new { username = user.Username });
        }

        // Validate fingerprint consistency (best-effort)
        if (PgpVerifier.TryGetPrimaryPublicKey(user.PgpPublicKey!, out var pubKey, out var fpHex, out var fpErr))
        {
            if (!string.IsNullOrEmpty(latest.Fingerprint) && !string.IsNullOrEmpty(fpHex) && !string.Equals(latest.Fingerprint, fpHex, StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = $"Fingerprint mismatch (expected {latest.Fingerprint}).";
                return RedirectToAction("Index", "User", new { username = user.Username });
            }
        }

        // Verify ASCII-armored signature against the challenge
        if (PgpVerifier.VerifySignature(user.PgpPublicKey!, model.Response, latest.ChallengeText, out var err))
        {
            latest.Verified = true;
            await _db.SaveChangesAsync();
            TempData["Message"] = "PGP verified.";
        }
        else
        {
            TempData["Error"] = err ?? "Verification failed.";
        }

        return RedirectToAction("Index", "User", new { username = user.Username });
    }
}
