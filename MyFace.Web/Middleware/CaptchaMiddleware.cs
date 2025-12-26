using Microsoft.AspNetCore.Http;
using MyFace.Web.Services;
using System.Security.Cryptography;
using System.Security.Claims;

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
             path.StartsWith("/chat") ||
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
            
            // Get or generate random captcha threshold (15-30 page loads)
            var threshold = context.Session.GetInt32("CaptchaThreshold");
            var (min, max) = CaptchaSettings.GetRangeForUser(context.User);
            if (threshold == null || threshold < min || threshold > max)
            {
                threshold = CaptchaSettings.NextThreshold(context.User);
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
