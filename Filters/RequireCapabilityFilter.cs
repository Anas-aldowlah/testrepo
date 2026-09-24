using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using YAGOT_2._0.Core.Capabilities;

namespace YAGOT_2._0.Filters;

/// <summary>
/// فلتر فحص القدرات اللحظي على مستوى كل طلب HTTP.
/// يضمن رفض الوصول الفوري (HTTP 403) مع أول نقرة أو انتقال في حال تعطيل الميزة في لوحة التحكم.
/// </summary>
public sealed class RequireCapabilityFilter(
    ICapabilityEvaluator capabilityEvaluator,
    ILogger<RequireCapabilityFilter> logger,
    string capabilityCode) : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var isModule = capabilityCode.StartsWith("M0", StringComparison.OrdinalIgnoreCase);
        var evaluation = isModule
            ? capabilityEvaluator.EvaluateModule(capabilityCode)
            : capabilityEvaluator.EvaluateFeature(capabilityCode);

        if (!evaluation.IsEnabled)
        {
            logger.LogWarning(
                "RequireCapabilityFilter: تم حظر الوصول إلى {Action} نظراً لتعطيل القدرة {CapabilityCode} (السبب: {Reason}).",
                context.ActionDescriptor.DisplayName,
                capabilityCode,
                evaluation.Reason);

            if (IsJsonRequest(context.HttpContext.Request))
            {
                context.Result = new ObjectResult(new
                {
                    success = false,
                    statusCode = StatusCodes.Status403Forbidden,
                    code = "CapabilityDisabled",
                    capability = capabilityCode,
                    reason = evaluation.Reason.ToString(),
                    message = "هذه الميزة معطلة حالياً في لوحة التحكم المركزية."
                })
                {
                    StatusCode = StatusCodes.Status403Forbidden
                };
                return;
            }

            context.Result = new RedirectResult("/Home/NotFoundPage?statusCode=403");
            return;
        }

        await next();
    }

    private static bool IsJsonRequest(HttpRequest request)
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

        return request.ContentType?.StartsWith("application/json", StringComparison.OrdinalIgnoreCase) == true;
    }
}
