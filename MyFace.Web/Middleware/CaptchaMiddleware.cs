using Microsoft.AspNetCore.Http;
using System.Security.Cryptography;

namespace MyFace.Web.Middleware;

public class CaptchaMiddleware
{
    private readonly RequestDelegate _next;

    public CaptchaMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip for static files, captcha controller itself, and login/register pages if needed
        var path = context.Request.Path.Value?.ToLower();
        if (path != null && 
            (path.StartsWith("/captcha") || 
             path.Contains(".") || // Static files usually have extensions
             path.StartsWith("/css") || 
             path.StartsWith("/js") || 
             path.StartsWith("/lib")))
        {
            await _next(context);
            return;
        }

        // Only count GET requests that return HTML (approximated by not being API/static)
        if (context.Request.Method == "GET")
        {
            var views = context.Session.GetInt32("PageViews") ?? 0;
            views++;
            
            // Get or generate random captcha threshold (5-15 page loads)
            var threshold = context.Session.GetInt32("CaptchaThreshold");
            if (threshold == null)
            {
                threshold = RandomNumberGenerator.GetInt32(5, 16); // 5 to 15 inclusive
                context.Session.SetInt32("CaptchaThreshold", threshold.Value);
            }
            
            if (views >= threshold)
            {
                // Redirect to Captcha
                // Don't increment here, wait until solved
                context.Response.Redirect($"/Captcha/Index?returnUrl={Uri.EscapeDataString(context.Request.Path + context.Request.QueryString)}");
                return;
            }
            
            context.Session.SetInt32("PageViews", views);
        }

        await _next(context);
    }
}
