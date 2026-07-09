using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Net.Http;
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
            _httpContextAccessor = new HttpContextAccessor();
            _httpClient = httpClient;
        }
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            bool IsDeveloper = _httpContextAccessor.HttpContext?.User.IsInRole("Developer") ?? false;
            bool IsAdmin = _httpContextAccessor.HttpContext?.User.IsInRole("Admin") ?? false;

            var status = (StatueSite?)context.HttpContext.Items["SiteStatus"];

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
                    var data = await _httpClient.GetFromJsonAsync<List<SiteDtoAdmin>>(
                        "https://controlpanelsite-assil.onrender.com/SiteAPI/GetSitesAdmin");

                    var statueAdmin = data?.FirstOrDefault(s => s.Siteid == 1);

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
