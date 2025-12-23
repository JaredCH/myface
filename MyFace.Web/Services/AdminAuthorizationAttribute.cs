using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MyFace.Web.Services;

public class AdminAuthorizationAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;
        var config = context.HttpContext.RequestServices.GetService<IConfiguration>();
        var admins = config?.GetSection("Admins").Get<string[]>() ?? Array.Empty<string>();
        var name = user.Identity?.Name;
        if (string.IsNullOrEmpty(name) || !admins.Contains(name))
        {
            context.Result = new ForbidResult();
        }
    }
}
