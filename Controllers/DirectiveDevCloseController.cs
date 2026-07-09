using Microsoft.AspNetCore.Mvc;

namespace Directing.Controllers
{
    public class DirectiveDevCloseController : Controller
    {
        public IActionResult Developer()
        {
            return View();
        }
        public IActionResult close(string Url,DateOnly StartDate,DateOnly EndDate,int OriginalDuration,string SiteName)
        {
            ViewBag.Url = Url;
            ViewBag.StartDate = StartDate;
            ViewBag.EndDate = EndDate;
            ViewBag.OriginalDuration = OriginalDuration;
            ViewBag.SiteName = SiteName;
            return View();
        }
    }
}
