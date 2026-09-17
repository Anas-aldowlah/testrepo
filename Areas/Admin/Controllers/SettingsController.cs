using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services;
using YAGOT_2._0.Core.Capabilities;

namespace YAGOT_2._0.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin,Developer")]
    [ServiceFilter(typeof(SiteStatusFilterAdmin))]
    public class SettingsController : Controller
    {
        private readonly StoreSettingsService _settingsService;
        private readonly NeondbContext _context;
        private readonly ILogger<SettingsController> _logger;
        private readonly ICapabilityEvaluator _capabilityEvaluator;

        public SettingsController(
            StoreSettingsService settingsService,
            NeondbContext context,
            ILogger<SettingsController> logger,
            ICapabilityEvaluator capabilityEvaluator)
        {
            _settingsService = settingsService;
            _context = context;
            _logger = logger;
            _capabilityEvaluator = capabilityEvaluator;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            if (!IsSettingsEnabled())
            {
                return Forbid();
            }

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
            if (!IsSettingsEnabled())
            {
                return Forbid();
            }

            var currentSettings = await _settingsService.GetSettingsAsync();

            // Developer-only check for CurrencyCode
            var isDeveloper = User.IsInRole("Developer");
            var requestedCurrencyChanged = !string.Equals(currentSettings.CurrencyCode, settings.CurrencyCode, StringComparison.OrdinalIgnoreCase);

            if (requestedCurrencyChanged)
            {
                if (!isDeveloper)
                {
                    ModelState.AddModelError(nameof(settings.CurrencyCode), "تعديل عملة النظام مقتصر على حساب المطور (Developer) فقط.");
                    settings.CurrencyCode = currentSettings.CurrencyCode;
                }
                else if (!CurrencyHelper.IsValid(settings.CurrencyCode))
                {
                    ModelState.AddModelError(nameof(settings.CurrencyCode), "العملة المحددة غير مدعومة. العملات المسموحة هي: YER, SAR, USD فقط.");
                }
            }
            else
            {
                settings.CurrencyCode = currentSettings.CurrencyCode;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    var oldCurrency = currentSettings.CurrencyCode;
                    await _settingsService.SaveSettingsAsync(settings);

                    if (isDeveloper && requestedCurrencyChanged)
                    {
                        _logger.LogWarning(
                            "CURRENCY CHANGED: Developer '{User}' changed system currency from '{OldCode}' to '{NewCode}' at {Timestamp} UTC.",
                            User.Identity?.Name ?? "Developer", oldCurrency, settings.CurrencyCode, DateTime.UtcNow);
                    }

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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Developer")]
        public async Task<IActionResult> UpdateCurrency(string currencyCode)
        {
            if (!IsSettingsEnabled())
            {
                return Forbid();
            }

            if (!CurrencyHelper.IsValid(currencyCode))
            {
                return BadRequest(new { success = false, message = "العملة المحددة غير مدعومة. العملات المسموحة هي: YER, SAR, USD فقط." });
            }

            var normalized = CurrencyHelper.Normalize(currencyCode);
            var current = await _settingsService.GetSettingsAsync();
            var oldCode = current.CurrencyCode;
            current.CurrencyCode = normalized;
            await _settingsService.SaveSettingsAsync(current);

            _logger.LogWarning(
                "CURRENCY CHANGED: Developer '{User}' changed system currency from '{OldCode}' to '{NewCode}' at {Timestamp} UTC.",
                User.Identity?.Name ?? "Developer", oldCode, normalized, DateTime.UtcNow);

            return Json(new
            {
                success = true,
                currencyCode = normalized,
                symbol = CurrencyHelper.GetSymbol(normalized),
                name = CurrencyHelper.GetNameAr(normalized),
                message = "تم تحديث عملة النظام بنجاح."
            });
        }

        private bool IsSettingsEnabled() =>
            _capabilityEvaluator.IsFeatureEnabled(CapabilityFeatureCodes.StoreSettings);
    }
}
