using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MyFace.Data;

namespace MyFace.Web.Middleware;

public class UsernameRequiredMiddleware
{
    private readonly RequestDelegate _next;

    public UsernameRequiredMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
    {
        // Allow access to account-related pages and static files
        var path = context.Request.Path.Value?.ToLower() ?? "";
        
        // Paths that don't require a username
        var allowedPaths = new[]
        {
            "/account/setusername",
            "/account/login",
            "/account/register",
            "/account/logout",
            "/",
            "/home",
            "/thread/index",
            "/captcha"
        };

        var allowedPrefixes = new[]
        {
            "/css/",
            "/js/",
            "/lib/",
            "/images/",
            "/favicon"
        };

        // Check if path is allowed
        var isAllowed = allowedPaths.Any(ap => path == ap || path.StartsWith(ap + "/")) ||
                        allowedPrefixes.Any(prefix => path.StartsWith(prefix));

        if (isAllowed || !context.User.Identity?.IsAuthenticated == true)
        {
            await _next(context);
            return;
        }

        // User is authenticated, check if they have a username set
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userId))
        {
            var user = await db.Users.FindAsync(int.Parse(userId));
            if (user != null && string.IsNullOrEmpty(user.Username))
            {
                // Redirect to SetUsername page
                context.Response.Redirect("/account/setusername");
                return;
            }
        }

        await _next(context);
    }
}
