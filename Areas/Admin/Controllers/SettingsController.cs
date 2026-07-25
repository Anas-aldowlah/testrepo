using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.Areas.Admin.Controllers
{
    [Area("Admin")]
    [ServiceFilter(typeof(SiteStatusFilterAdmin))]
    public class SettingsController : Controller
    {
        private readonly StoreSettingsService _settingsService;
        private readonly NeondbContext _context;

        public SettingsController(StoreSettingsService settingsService, NeondbContext context)
        {
            _settingsService = settingsService;
            _context = context;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var settings = _settingsService.GetSettings();
            
            // Prepare categories for dropdown
            var categories = _context.Categories.ToList();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", settings.FeaturedCategoryId);
            
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Index(StoreSettings settings)
        {
            if (ModelState.IsValid)
            {
                _settingsService.SaveSettings(settings);
                TempData["Success"] = "تم حفظ الإعدادات بنجاح.";
                return RedirectToAction(nameof(Index));
            }

            // On error, reload dropdown
            var categories = _context.Categories.ToList();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", settings.FeaturedCategoryId);
            return View(settings);
        }
    }
}
