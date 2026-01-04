using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using MyFace.Services;

namespace MyFace.Web.Middleware;

public class SessionTimeoutMiddleware
{
    private const string LastTouchKey = "__session:last-touch";
    private readonly RequestDelegate _next;

    public SessionTimeoutMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, ControlSettingsReader settings)
    {
        if (!context.Session.IsAvailable)
        {
            await _next(context);
            return;
        }

        var timeoutMinutes = await settings.GetIntAsync(ControlSettingKeys.SessionTimeoutMinutes, 120, context.RequestAborted);
        timeoutMinutes = Math.Clamp(timeoutMinutes, 5, 1440);
        var idleWindow = TimeSpan.FromMinutes(timeoutMinutes);
        var now = DateTime.UtcNow;

        var lastTouchString = context.Session.GetString(LastTouchKey);
        if (long.TryParse(lastTouchString, NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
        {
            var last = new DateTime(ticks, DateTimeKind.Utc);
            if (now - last >= idleWindow)
            {
                context.Session.Clear();
                if (context.User.Identity?.IsAuthenticated == true)
                {
                    await context.SignOutAsync();
                }
            }
        }

        context.Session.SetString(LastTouchKey, now.Ticks.ToString(CultureInfo.InvariantCulture));
        await _next(context);
    }
}
