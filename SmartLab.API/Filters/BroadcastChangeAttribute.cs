using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using SmartLab.BLL.Interfaces.Notifications;

namespace SmartLab.API.Filters;

/// <summary>
/// After a successful POST / PUT / DELETE of the controller, tells the open screens of every user that
/// <paramref name="entity"/> changed, so they reload it. GETs and failed requests send nothing.
/// The service has committed by then, because it has returned.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class BroadcastChangeAttribute : ActionFilterAttribute
{
    private readonly string _entity;

    public BroadcastChangeAttribute(string entity)
    {
        _entity = entity;
    }

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var executed = await next();

        if (HttpMethods.IsGet(context.HttpContext.Request.Method) || executed.Exception != null)
            return;

        var status = executed.Result is IStatusCodeActionResult result ? result.StatusCode : null;
        if (status >= StatusCodes.Status400BadRequest)
            return;

        var changes = context.HttpContext.RequestServices.GetRequiredService<IChangeBroadcaster>();
        await changes.SharedDataChangedAsync(_entity, RouteId(context));
    }

    private static Guid RouteId(ActionExecutingContext context)
    {
        return Guid.TryParse(context.RouteData.Values["id"]?.ToString(), out var id) ? id : Guid.Empty;
    }
}
