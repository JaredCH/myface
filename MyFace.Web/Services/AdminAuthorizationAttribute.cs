using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Security.Claims;

namespace MyFace.Web.Services;

public class AdminAuthorizationAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var role = user.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin" && role != "Moderator")
        {
            // Fallback to config for backward compatibility or initial setup
            var config = context.HttpContext.RequestServices.GetService<IConfiguration>();
            var admins = config?.GetSection("Admins").Get<string[]>() ?? Array.Empty<string>();
            var mods = config?.GetSection("Moderators").Get<string[]>() ?? Array.Empty<string>();
            var name = user.Identity?.Name;
            
            if (string.IsNullOrEmpty(name) || (!admins.Contains(name) && !mods.Contains(name)))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}

public class OnlyAdminAuthorizationAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var role = user.FindFirstValue(ClaimTypes.Role);
        if (role != "Admin")
        {
            // Fallback
            var config = context.HttpContext.RequestServices.GetService<IConfiguration>();
            var admins = config?.GetSection("Admins").Get<string[]>() ?? Array.Empty<string>();
            var name = user.Identity?.Name;
            
            if (string.IsNullOrEmpty(name) || !admins.Contains(name))
            {
                context.Result = new ForbidResult();
            }
        }
    }
}
