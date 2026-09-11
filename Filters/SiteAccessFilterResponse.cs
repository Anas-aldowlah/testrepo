using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Integration.SiteState;

namespace YAGOT_2._0.Filters;

internal static class SiteAccessFilterResponse
{
    public static IActionResult? Create(HttpContext context, SiteAccessDecision decision)
    {
        if (decision.IsAllowed)
        {
            return null;
        }

        context.Response.Headers.CacheControl = "no-store";
        if (IsJsonRequest(context.Request))
        {
            return new ObjectResult(new
            {
                success = false,
                code = decision.Code,
                message = Message(decision.Kind)
            })
            {
                StatusCode = StatusCodes.Status503ServiceUnavailable
            };
        }

        return decision.HtmlTarget switch
        {
            SiteAccessHtmlTarget.Developer => new RedirectToActionResult(
                "Developer", "DirectiveDevClose", new { area = "" }),
            SiteAccessHtmlTarget.Close => new RedirectToActionResult(
                "close", "DirectiveDevClose", new { area = "" }),
            _ => new ViewResult
            {
                ViewName = "~/Views/DirectiveDevClose/Unavailable.cshtml",
                StatusCode = StatusCodes.Status503ServiceUnavailable
            }
        };
    }

    public static bool IsJsonRequest(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Headers["X-Requested-With"].Any(value =>
                string.Equals(value, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (request.Headers.Accept.Any(value =>
                value?.Contains("application/json", StringComparison.OrdinalIgnoreCase) == true))
        {
            return true;
        }

        return request.ContentType?.StartsWith(
            "application/json", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static string Message(SiteAccessDecisionKind kind) => kind switch
    {
        SiteAccessDecisionKind.DevelopmentRestricted =>
            "الموقع قيد التطوير حالياً.",
        SiteAccessDecisionKind.OfflineRestricted =>
            "الموقع غير متاح حالياً.",
        SiteAccessDecisionKind.Missing =>
            "حالة الموقع غير متاحة حالياً.",
        _ => "تعذر التحقق من حالة الموقع حالياً. يرجى المحاولة لاحقاً."
    };
}
