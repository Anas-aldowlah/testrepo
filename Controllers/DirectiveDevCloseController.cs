using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Integration.SiteState;
using YAGOT_2._0.Services.Integration;

namespace Directing.Controllers;

public sealed class DirectiveDevCloseController(
    ISiteAccessDecisionService decisionService) : Controller
{
    public IActionResult Developer() => View();

    public async Task<IActionResult> Close(CancellationToken cancellationToken)
    {
        var decision = await decisionService.DecideAsync(
            SiteAccessSurface.Storefront,
            User,
            cancellationToken);

        if (decision.EffectiveMode == SiteStateContractV1.Offline &&
            decision.Maintenance is not null)
        {
            Response.Headers.CacheControl = "no-store";
            return View("close", decision.Maintenance);
        }

        if (decision.EffectiveMode == SiteStateContractV1.Development)
        {
            return RedirectToAction(nameof(Developer));
        }

        if (decision.IsAllowed)
        {
            return RedirectToAction("Index", "Home");
        }

        Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        Response.Headers.CacheControl = "no-store";
        return View("Unavailable");
    }
}
