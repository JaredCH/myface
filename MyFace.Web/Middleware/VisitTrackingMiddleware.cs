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
        _ = Task.Run(async () =>
        {
            try
            {
                var path = context.Request.Path.ToString();
                var userAgent = context.Request.Headers["User-Agent"].ToString();
                await visitTracking.TrackVisitAsync(path, userAgent);
            }
            catch
            {
                // Silently fail - don't break the request
            }
        });
    }
}
