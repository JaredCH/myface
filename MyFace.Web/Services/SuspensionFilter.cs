using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using MyFace.Services;
using System.Security.Claims;

namespace MyFace.Web.Services;

public class SuspensionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated == true)
        {
            // Only check on POST requests or specific actions to avoid DB hit on every GET?
            // Requirement: "suspend users". Usually implies blocking posting/commenting.
            // Let's block POST requests for now, or all requests if "forever".
            // User asked for "suspend users", usually means read-only or no access.
            // Let's check DB.
            
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdStr, out var userId))
            {
                var userService = context.HttpContext.RequestServices.GetRequiredService<UserService>();
                var dbUser = await userService.GetUserByIdAsync(userId);
                
                if (dbUser?.SuspendedUntil != null && dbUser.SuspendedUntil > DateTime.UtcNow)
                {
                    if (context.HttpContext.Request.Method == "POST")
                    {
                        context.Result = new ContentResult 
                        { 
                            Content = $"You are suspended until {dbUser.SuspendedUntil}. You cannot perform this action.",
                            StatusCode = 403
                        };
                        return;
                    }
                }
            }
        }
        
        await next();
    }
}
