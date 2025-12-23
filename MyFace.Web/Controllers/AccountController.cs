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

    public AccountController(UserService userService, ApplicationDbContext db)
    {
        _userService = userService;
        _db = db;
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
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userService.AuthenticateAsync(model.Username, model.Password);
        
        if (user == null)
        {
            ModelState.AddModelError("", "Invalid username or password.");
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
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, username)
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

        var latest = _db.PGPVerifications
            .Where(v => v.UserId == user.Id)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefault();

        if (latest == null)
        {
            TempData["Error"] = "No challenge to verify.";
            return RedirectToAction("Index", "User", new { username = user.Username });
        }

        // MVP verification: compare provided response to challenge text
        if (model.Response?.Trim() == latest.ChallengeText)
        {
            latest.Verified = true;
            await _db.SaveChangesAsync();
            TempData["Message"] = "PGP verified.";
        }
        else
        {
            TempData["Error"] = "Verification failed.";
        }

        return RedirectToAction("Index", "User", new { username = user.Username });
    }
}
