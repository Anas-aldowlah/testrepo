using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using YAGOT_2._0.Models;
using static YAGOT_2._0.Services.DealingAPI;

namespace YAGOT_2._0.Filters
{
    public class SiteStatusFilterAdmin : IAsyncActionFilter
    {
        private readonly HttpClient _httpClient;
        private readonly IHttpContextAccessor _httpContextAccessor;
        public SiteStatusFilterAdmin(HttpClient httpClient, IHttpContextAccessor httpContextAccessor)
        {
            _httpClient = httpClient;
            _httpContextAccessor = httpContextAccessor;
        }
        public async Task OnActionExecutionAsync(
     ActionExecutingContext context,
     ActionExecutionDelegate next)
        {
            bool isDeveloper = context.HttpContext.User.IsInRole("Developer");
            bool isAdmin = context.HttpContext.User.IsInRole("Admin");

            var status = (StatueSite?)context.HttpContext.Items["SiteStatus"];

            // المطور يدخل دائماً
            if (isDeveloper)
            {
                await next();
                return;
            }

            // الموقع مغلق للصيانة
            if (status == StatueSite.ColsePlane)
            {
                if (isAdmin)
                {
                    var data = await _httpClient.GetFromJsonAsync<List<SiteDtoAdmin>>(
                        "SiteAPI/GetSitesAdmin");

                    var site = data?.FirstOrDefault(s => s.Siteid == 1);

                    if (site != null)
                    {
                        context.Result = new RedirectToActionResult(
                            "close",
                            "DirectiveDevClose",
                            new
                            {
                                area = "",
                                Url = site.Url,
                                SiteName = site.Sitename,
                                StartDate = site.StartDate,
                                EndDate = site.EndDate,
                                OriginalDuration = site.DurationDay
                            });

                        return;
                    }
                }
                if (status == StatueSite.Developer)
                {
                    if (isAdmin)
                    {
                        await next();
                        return;
                    }
                    else
                    {
                        context.Result = new RedirectToActionResult(
                   "Developer",
                   "DirectiveDevClose",
                   new { area = "" });
                    }
                }
                   
                return;
            }

           

            // السماح بتنفيذ الـ Action
            await next();
        }



        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
