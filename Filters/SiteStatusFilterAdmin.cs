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
            bool IsDeveloper = _httpContextAccessor.HttpContext?.User.IsInRole("Developer") ?? false;
            bool IsAdmin = _httpContextAccessor.HttpContext?.User.IsInRole("Admin") ?? false;
            var status = (StatueSite?)context.HttpContext.Items["SiteStatus"];

            if ((status == StatueSite.Developer ||
                status == StatueSite.ColsePlane) && !IsDeveloper)
            {
                if (status == StatueSite.ColsePlane && IsAdmin)
                {
                    var data = await _httpClient.GetFromJsonAsync<List<SiteDtoAdmin>>("https://controlpanelsite-assil.onrender.com/SiteAPI/GetSitesAdmin");
                    var statueAdmin = data.Where(s => s.Siteid == 1).FirstOrDefault();
                    context.Result = new RedirectToActionResult(
                       "close",
                       "DirectiveDevClose", new { area = "", Url = statueAdmin.Url, SiteName = statueAdmin.Sitename, StartDate = statueAdmin.StartDate, EndDate = statueAdmin.EndDate, OriginalDuration = statueAdmin.DurationDay });


                }
                else
                {
                   context.Result = new RedirectToActionResult(
                   "Developer",
                   "DirectiveDevClose",
   new { area = "" },
                   null);
                }
            }

        }



        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}