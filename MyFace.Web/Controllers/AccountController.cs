using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
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
    private readonly RateLimitService _rateLimitService;

    public AccountController(
        UserService userService, 
        ApplicationDbContext db, 
        MyFace.Web.Services.CaptchaService captchaService,
        RateLimitService rateLimitService)
    {
        _userService = userService;
        _db = db;
        _captchaService = captchaService;
        _rateLimitService = rateLimitService;
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword()
    {
        return View(new ChangePasswordViewModel());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdString, out var userId))
        {
            return Unauthorized();
        }

        var success = await _userService.ChangePasswordAsync(userId, model.CurrentPassword, model.NewPassword);
        if (!success)
        {
            TempData["Error"] = "Current password is incorrect.";
            return View(model);
        }

        TempData["Success"] = "Password updated. Use your new password next time you log in.";
        return RedirectToAction("ChangePassword");
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

        var user = await _userService.RegisterAsync(model.Username, model.Password, null);
        
        if (user == null)
        {
            ModelState.AddModelError("", "Login name already exists. Please choose a different one.");
            return View(model);
        }

        await SignInUserAsync(user.Id, user.LoginName, string.Empty);
        return RedirectToAction("SetUsername");
    }

    [HttpGet]
    public IActionResult Login()
    {
        RefreshLoginCaptcha();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string captchaAnswer)
    {
        // Check rate limit FIRST (before captcha or authentication)
        var rateLimitDelay = await _rateLimitService.CheckLoginRateLimitAsync(model.Username);
        if (rateLimitDelay > 0)
        {
            var minutes = rateLimitDelay / 60;
            var seconds = rateLimitDelay % 60;
            var timeMessage = minutes > 0 
                ? $"{minutes} minute{(minutes > 1 ? "s" : "")} and {seconds} second{(seconds > 1 ? "s" : "")}"
                : $"{seconds} second{(seconds > 1 ? "s" : "")}";
            
            ModelState.AddModelError(nameof(LoginViewModel.Username), $"Too many failed login attempts. Please wait {timeMessage} before trying again.");
            
            RefreshLoginCaptcha();
            return View(model);
        }
        
        // Verify captcha
        var correctAnswer = HttpContext.Session.GetString("LoginCaptchaAnswer");
        var captchaIsValid = !string.IsNullOrEmpty(correctAnswer) &&
                             _captchaService.Validate(correctAnswer, captchaAnswer);
        if (!captchaIsValid)
        {
            ModelState.AddModelError("captchaAnswer", "Incorrect security check answer.");
            await _rateLimitService.RecordLoginAttemptAsync(model.Username, false);
            
            RefreshLoginCaptcha();
            return View(model);
        }

        if (!ModelState.IsValid)
        {
            RefreshLoginCaptcha();
            return View(model);
        }

        var user = await _userService.AuthenticateAsync(model.Username, model.Password);
        
        if (user == null)
        {
            // Record failed attempt
            await _rateLimitService.RecordLoginAttemptAsync(model.Username, false);
            
            // Generic error message to prevent account enumeration
            ModelState.AddModelError(nameof(LoginViewModel.Password), "Invalid login credentials. Please check your login name and password.");
            
            RefreshLoginCaptcha();
            return View(model);
        }
        
        // Record successful attempt
        await _rateLimitService.RecordLoginAttemptAsync(model.Username, true);

        // If user hasn't set a username yet, redirect to SetUsername
        if (string.IsNullOrEmpty(user.Username))
        {
            await SignInUserAsync(user.Id, user.LoginName, string.Empty);
            return RedirectToAction("SetUsername");
        }

        // Check if user has unnotified username change
        var usernameChangeLog = await _userService.GetUnnotifiedUsernameChangeAsync(user.Id);
        
        await SignInUserAsync(user.Id, user.LoginName, user.Username);
        
        // If username was changed by admin/mod, redirect to notification page
        if (usernameChangeLog != null || user.MustChangeUsername)
        {
            if (usernameChangeLog != null)
            {
                return RedirectToAction("UsernameChangeNotification", new { logId = usernameChangeLog.Id });
            }
            // If flag is set but no log, just redirect to SetUsername
            return RedirectToAction("SetUsername");
        }

        return RedirectToAction("Index", "Home");
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    private async Task SignInUserAsync(int userId, string loginName, string username)
    {
        var user = await _userService.GetUserByIdAsync(userId);
        var role = user?.Role ?? "User";

        // Use username for display if set, otherwise use LoginName temporarily
        var displayName = !string.IsNullOrEmpty(username) ? username : loginName;

        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Name, displayName),
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

    [Authorize]
    public async Task<IActionResult> UsernameChangeNotification(int logId)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!int.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized();
        }

        var changeLog = await _userService.GetUnnotifiedUsernameChangeAsync(userId);
        if (changeLog == null || changeLog.Id != logId)
        {
            return RedirectToAction("Index", "Home");
        }

        await _userService.MarkUsernameChangeNotifiedAsync(logId);
        return View(changeLog);
    }

    private void RefreshLoginCaptcha()
    {
        var challenge = _captchaService.GenerateChallenge();
        HttpContext.Session.SetString("LoginCaptchaAnswer", challenge.Answer);
        ViewBag.CaptchaContext = challenge.Context;
        ViewBag.CaptchaQuestion = challenge.Question;
    }

    [HttpGet]
    public IActionResult SetUsername()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login");
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUsername(string username, string action)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
        {
            return RedirectToAction("Login");
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            TempData["Error"] = "Username cannot be empty.";
            return View();
        }

        // Validate username format
        if (!System.Text.RegularExpressions.Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
        {
            TempData["Error"] = "Username can only contain letters, numbers, and underscores.";
            return View();
        }

        // Set the username
        var success = await _userService.SetUsernameAsync(int.Parse(userId), username);
        
        if (!success)
        {
            TempData["Error"] = "Username is already taken. Please choose a different one.";
            return View();
        }

        // Clear the MustChangeUsername flag if it was set
        await _userService.ClearMustChangeUsernameAsync(int.Parse(userId));

        // Update the claims with the new username
        var user = await _userService.GetByIdAsync(int.Parse(userId));
        if (user != null)
        {
            await SignInUserAsync(user.Id, user.LoginName, user.Username);
        }

        // Redirect based on action
        if (action == "save_and_pgp")
        {
            return RedirectToAction("PgpSetup");
        }

        TempData["Success"] = $"Welcome, {username}! Your username has been set.";
        return RedirectToAction("Index", "Thread");
    }

    [HttpGet]
    public async Task<IActionResult> PgpSetup()
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

        ViewBag.Username = user.Username;
        ViewBag.CurrentKey = user.PgpPublicKey;
        ViewBag.Step = TempData["Step"] ?? "1";
        ViewBag.Challenge = TempData["Challenge"];
        ViewBag.Fingerprint = TempData["Fingerprint"];
        ViewBag.Error = TempData["Error"];
        ViewBag.Message = TempData["Message"];
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PgpSetupStep1(string PgpPublicKey)
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

        if (string.IsNullOrWhiteSpace(PgpPublicKey))
        {
            TempData["Error"] = "Please provide a PGP public key.";
            TempData["Step"] = "1";
            return RedirectToAction("PgpSetup");
        }

        // Extract fingerprint from the public key
        if (!PgpVerifier.TryGetPrimaryPublicKey(PgpPublicKey, out var pubKey, out var fingerprint, out var error))
        {
            TempData["Error"] = $"Invalid PGP key: {error}";
            TempData["Step"] = "1";
            return RedirectToAction("PgpSetup");
        }

        // Save the key
        user.PgpPublicKey = PgpPublicKey;
        await _db.SaveChangesAsync();

        // Generate challenge
        var challenge = new PGPVerification
        {
            UserId = user.Id,
            Fingerprint = fingerprint,
            ChallengeText = Guid.NewGuid().ToString("N"),
            Verified = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.PGPVerifications.Add(challenge);
        await _db.SaveChangesAsync();

        TempData["Step"] = "2";
        TempData["Challenge"] = challenge.ChallengeText;
        TempData["Fingerprint"] = fingerprint;
        return RedirectToAction("PgpSetup");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PgpSetupStep2(string Response, string Fingerprint)
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

        if (string.IsNullOrWhiteSpace(Response))
        {
            TempData["Error"] = "Please provide a signature.";
            TempData["Step"] = "2";
            TempData["Fingerprint"] = Fingerprint;
            return RedirectToAction("PgpSetup");
        }

        var latest = _db.PGPVerifications
            .Where(v => v.UserId == user.Id)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefault();

        if (latest == null)
        {
            TempData["Error"] = "No challenge found. Please start over.";
            TempData["Step"] = "1";
            return RedirectToAction("PgpSetup");
        }

        if (string.IsNullOrWhiteSpace(user.PgpPublicKey))
        {
            TempData["Error"] = "No PGP key found. Please start over.";
            TempData["Step"] = "1";
            return RedirectToAction("PgpSetup");
        }

        // Verify signature
        if (!PgpVerifier.VerifySignature(user.PgpPublicKey, Response, latest.ChallengeText, out var error))
        {
            TempData["Error"] = $"Signature verification failed: {error}";
            TempData["Step"] = "2";
            TempData["Challenge"] = latest.ChallengeText;
            TempData["Fingerprint"] = Fingerprint;
            return RedirectToAction("PgpSetup");
        }

        // Mark as verified
        latest.Verified = true;
        await _db.SaveChangesAsync();

        TempData["Step"] = "3";
        TempData["Message"] = "PGP key verified successfully!";
        TempData["Fingerprint"] = Fingerprint;
        return RedirectToAction("PgpSetup");
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
