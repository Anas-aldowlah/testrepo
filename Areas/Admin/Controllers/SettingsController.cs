using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
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
        private readonly ILogger<SettingsController> _logger;

        public SettingsController(
            StoreSettingsService settingsService,
            NeondbContext context,
            ILogger<SettingsController> logger)
        {
            _settingsService = settingsService;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var settings = await _settingsService.GetSettingsAsync();
            
            // Prepare categories for dropdown
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(category => category.Name)
                .ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", settings.FeaturedCategoryId);
            
            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(StoreSettings settings)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    await _settingsService.SaveSettingsAsync(settings);
                    _logger.LogInformation("Store settings were updated successfully for settings row {SettingsId}.", settings.Id);
                    TempData["Success"] = "تم حفظ الإعدادات بنجاح. تم تحديث بيانات التواصل في الموقع.";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save store settings for settings row {SettingsId}.", settings.Id);
                    TempData["Error"] = "تعذر حفظ الإعدادات حاليا. تأكد من الاتصال بقاعدة البيانات ثم حاول مرة أخرى.";
                }
            }
            else
            {
                var modelErrors = ModelState
                    .Where(entry => entry.Value?.Errors.Count > 0)
                    .Select(entry => $"{entry.Key}: {string.Join(" | ", entry.Value!.Errors.Select(error => error.ErrorMessage))}")
                    .ToList();
                _logger.LogWarning("Store settings were not saved because ModelState is invalid: {ModelErrors}", string.Join("; ", modelErrors));
                TempData["Error"] = "لم يتم حفظ الإعدادات. راجع الحقول وحاول مرة أخرى.";
            }

            // On error, reload dropdown
            var categories = await _context.Categories
                .AsNoTracking()
                .OrderBy(category => category.Name)
                .ToListAsync();
            ViewBag.Categories = new SelectList(categories, "Id", "Name", settings.FeaturedCategoryId);
            return View(settings);
        }
    }
}
