using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using static YAGOT_2._0.Services.DealingAPI;

namespace YAGOT_2._0.Filters
{
    public class SiteStatusFilter : IActionFilter
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public SiteStatusFilter(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = new HttpContextAccessor();
        }
        public void OnActionExecuting(ActionExecutingContext context)
        {
            bool IsDeveloper = _httpContextAccessor.HttpContext?.User.IsInRole("Developer") ?? false;
            bool IsAdmin = _httpContextAccessor.HttpContext?.User.IsInRole("Admin") ?? false;
            var status = (StatueSite?)context.HttpContext.Items["SiteStatus"];

            if ((status == StatueSite.Developer ||
                status == StatueSite.ColsePlane) && !IsDeveloper)
            {
                if(status == StatueSite.Developer && !IsAdmin)
                {
                    context.Result = new RedirectToActionResult(
                    "Developer",
                    "DirectiveDevClose",
                    null);
                }
              


                   
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
