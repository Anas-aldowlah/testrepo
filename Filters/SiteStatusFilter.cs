using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YAGOT_2._0.Models;
using static YAGOT_2._0.Services.DealingAPI;

namespace YAGOT_2._0.Filters
{
    public class SiteStatusFilter : IAsyncActionFilter
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpClient _httpClient;
        public SiteStatusFilter(IHttpContextAccessor httpContextAccessor, HttpClient httpClient)
        {
            _httpContextAccessor = httpContextAccessor;
            _httpClient = httpClient;
        }
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            bool IsDeveloper = _httpContextAccessor.HttpContext?.User.IsInRole("Developer") ?? false;
            bool IsAdmin = _httpContextAccessor.HttpContext?.User.IsInRole("Admin") ?? false;

            var status = context.HttpContext.Items.TryGetValue("SiteStatus", out var value) && value is StatueSite siteStatus
                ? siteStatus
                : StatueSite.ColsePlane;

            if ((status == StatueSite.Developer ||
                 status == StatueSite.ColsePlane) && !IsDeveloper)
            {
                if (status == StatueSite.Developer && !IsAdmin)
                {
                    context.Result = new RedirectToActionResult(
                        "Developer",
                        "DirectiveDevClose",
                        null);

                    return;
                }

                if (status == StatueSite.ColsePlane && !IsAdmin)
                {
                    context.Result = new RedirectToActionResult(
                        "Developer",
                        "DirectiveDevClose",
                        null);

                    return;
                }

                if (status == StatueSite.ColsePlane && IsAdmin)
                {
                    SiteDtoAdmin? statueAdmin = null;
                    try
                    {
                        var data = await _httpClient.GetFromJsonAsync<List<SiteDtoAdmin>>(
                            "SiteAPI/GetSitesAdmin");
                        statueAdmin = data?.FirstOrDefault(s => s.Siteid == 1);
                    }
                    catch (Exception)
                    {
                        // Maintenance metadata is optional; access remains closed if the API is unavailable.
                    }

                    context.Result = new RedirectToActionResult(
                        "close",
                        "DirectiveDevClose",
                        new
                        {
                            area = "",
                            Url = statueAdmin?.Url,
                            SiteName = statueAdmin?.Sitename,
                            StartDate = statueAdmin?.StartDate,
                            EndDate = statueAdmin?.EndDate,
                            OriginalDuration = statueAdmin?.DurationDay
                        });

                    return;
                }


            }

            // إذا لم يحدث Redirect فانتقل إلى الـ Action
            await next();
        }
        public void OnActionExecuted(ActionExecutedContext context)
        {
        }

     
    }
}
