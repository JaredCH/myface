using System.Security.Claims;
using MyFace.Services;

namespace MyFace.Web.Middleware;

public class VisitTrackingMiddleware
{
    private readonly RequestDelegate _next;

    public VisitTrackingMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, VisitTrackingService visitTracking)
    {
        // Process the request first
        await _next(context);
        
        // Track the visit after the request is processed (fire and forget)
        var path = context.Request.Path.ToString();
        var userAgent = context.Request.Headers["User-Agent"].ToString();
        var sessionId = context.Session?.Id;
        var user = context.User;
        var username = user?.Identity?.IsAuthenticated == true ? user.Identity?.Name : null;
        int? userId = null;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var idValue = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(idValue, out var parsedId))
            {
                userId = parsedId;
            }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await visitTracking.TrackVisitAsync(path, userAgent, userId, username, sessionId);
            }
            catch
            {
                // Silently fail - don't break the request
            }
        });
    }
}
