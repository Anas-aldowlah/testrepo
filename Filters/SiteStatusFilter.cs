using Microsoft.AspNetCore.Mvc.Filters;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;

namespace YAGOT_2._0.Filters;

public sealed class SiteStatusFilter(
    ISiteAccessDecisionService decisionService) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var decision = await decisionService.DecideAsync(
            SiteAccessSurface.Storefront,
            context.HttpContext.User,
            context.HttpContext.RequestAborted);
        context.Result = SiteAccessFilterResponse.Create(context.HttpContext, decision);
        if (context.Result is null)
        {
            await next();
        }
    }
}
