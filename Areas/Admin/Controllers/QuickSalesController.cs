using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YAGOT_2._0.Filters;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;

namespace YAGOT_2._0.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize]
[ServiceFilter(typeof(SiteStatusFilterAdmin))]
public class QuickSalesController : Controller
{
    private readonly NeondbContext _context;
    private readonly IInventoryService _inventoryService;

    public QuickSalesController(NeondbContext context, IInventoryService inventoryService)
    {
        _context = context;
        _inventoryService = inventoryService;
    }

    // 1. MAIN QUICK SALES DASHBOARD / STATE ROUTE
    public async Task<IActionResult> Index()
    {
        var openDay = await _context.SalesDays
            .AsNoTracking()
            .FirstOrDefaultAsync(sd => sd.Status == "Open");

        var activeDrafts = new List<Sale>();
        int todayCompletedCount = 0;
        decimal todayCompletedTotal = 0m;

        if (openDay != null)
        {
            activeDrafts = await _context.Sales
                .AsNoTracking()
                .Include(s => s.SaleItems)
                .Where(s => s.SalesDayId == openDay.Id && s.Status == "Draft")
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .ToListAsync();

            todayCompletedCount = await _context.Sales
                .AsNoTracking()
                .CountAsync(s => s.SalesDayId == openDay.Id && s.Status == "Completed");

            todayCompletedTotal = await _context.Sales
                .AsNoTracking()
                .Where(s => s.SalesDayId == openDay.Id && s.Status == "Completed")
                .SumAsync(s => (decimal?)s.FinalAmount) ?? 0m;
        }

        var viewModel = new QuickSalesIndexViewModel
        {
            CurrentOpenDay = openDay,
            ActiveDrafts = activeDrafts,
            TodayCompletedSalesCount = todayCompletedCount,
            TodayCompletedSalesTotal = todayCompletedTotal
        };

        return View(viewModel);
    }

    // 2. OPEN NEW SALES DAY (GET)
    [HttpGet]
    public async Task<IActionResult> OpenDay()
    {
        var existingOpenDay = await _context.SalesDays
            .AsNoTracking()
            .FirstOrDefaultAsync(sd => sd.Status == "Open");

        if (existingOpenDay != null)
        {
            TempData["WarningMessage"] = $"يوجد يوم بيع مفتوح بالفعل بتاريخ {existingOpenDay.Date:yyyy/MM/dd}. يرجى المتابعة عليه.";
            return RedirectToAction(nameof(Index));
        }

        var model = new OpenSalesDayViewModel
        {
            Date = DateTime.Today,
            OpeningBalance = 0m
        };

        return View(model);
    }

    // 2. OPEN NEW SALES DAY (POST)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenDay(OpenSalesDayViewModel model)
    {
        if (model == null)
        {
            return RedirectToAction(nameof(Index));
        }

        var maxAllowedDate = DateTime.Today.AddDays(1); // Today + 1 (Tomorrow)
        if (model.Date.Date > maxAllowedDate)
        {
            ModelState.AddModelError(nameof(model.Date), "لا يمكن اختيار تاريخ بعد يوم غد.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var existingOpenDay = await _context.SalesDays
            .FirstOrDefaultAsync(sd => sd.Status == "Open");

        if (existingOpenDay != null)
        {
            TempData["WarningMessage"] = $"يوجد يوم بيع مفتوح بالفعل بتاريخ {existingOpenDay.Date:yyyy/MM/dd}.";
            return RedirectToAction(nameof(Index));
        }

        var newSalesDay = new SalesDay
        {
            Date = model.Date,
            OpeningBalance = model.OpeningBalance,
            Notes = model.Notes?.Trim(),
            Status = "Open",
            CreatedBy = User.Identity?.Name ?? "المدير",
            CreatedAt = DateTime.Now
        };

        _context.SalesDays.Add(newSalesDay);
        await _context.SaveChangesAsync();

        TempData["SuccessMessage"] = $"تم فتح يوم بيع جديد بتاريخ {newSalesDay.Date:yyyy/MM/dd} بنجاح.";
        return RedirectToAction(nameof(Index));
    }

    // 3. NEW SALE / EDIT DRAFT INTERFACE
    [HttpGet]
    public async Task<IActionResult> NewSale(int? id)
    {
        var openDay = await _context.SalesDays
            .AsNoTracking()
            .FirstOrDefaultAsync(sd => sd.Status == "Open");

        if (openDay == null)
        {
            TempData["ErrorMessage"] = "يجب فتح يوم بيع أولاً قبل بدء عملية بيع جديدة.";
            return RedirectToAction(nameof(Index));
        }

        Sale? sale = null;
        if (id.HasValue && id.Value > 0)
        {
            sale = await _context.Sales
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                        .ThenInclude(p => p.RetailPrices)
                .Include(s => s.SalePayments)
                .FirstOrDefaultAsync(s => s.Id == id.Value && s.Status == "Draft");

            if (sale == null)
            {
                TempData["ErrorMessage"] = "المسودة المطلوبة غير موجودة أو تم إغلاقها.";
                return RedirectToAction(nameof(Index));
            }
        }
        else
        {
            // Auto-load latest active draft for current open sales day when opening/refreshing Quick Sales
            sale = await _context.Sales
                .Include(s => s.SaleItems)
                    .ThenInclude(si => si.Product)
                        .ThenInclude(p => p.RetailPrices)
                .Include(s => s.SalePayments)
                .Where(s => s.SalesDayId == openDay.Id && s.Status == "Draft")
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .FirstOrDefaultAsync();
        }

        ViewBag.OpenSalesDay = openDay;
        return View(sale);
    }

    // 4. SERVER-SIDE AUTOCOMPLETE PRODUCT SEARCH API
    [HttpGet]
    public async Task<IActionResult> SearchProducts(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Trim().Length < 1)
        {
            return Json(new List<ProductSearchResultDto>());
        }

        var query = q.Trim().ToLower();

        var products = await _context.Products
            .AsNoTracking()
            .Include(p => p.RetailPrices)
            .Where(p => p.Name.ToLower().Contains(query) || (p.Brand != null && p.Brand.ToLower().Contains(query)))
            .OrderBy(p => p.Name)
            .Take(12)
            .Select(p => new ProductSearchResultDto
            {
                Id = p.Id,
                Name = p.Name,
                Brand = p.Brand,
                Price = p.Price,
                Stock = p.Stockquantity,
                StockUnit = p.StockUnit,
                VolumeMl = p.VolumeMl,
                IsRetailEnabled = p.IsRetailEnabled,
                RetailPrices = p.RetailPrices
                    .OrderBy(price => price.SizeMl)
                    .Select(price => new ProductRetailPriceDto
                    {
                        Id = price.Id,
                        SizeMl = price.SizeMl,
                        Price = price.Price
                    })
                    .ToList(),
                ImageUrl = string.IsNullOrWhiteSpace(p.Imageurl) ? "/images/yaqut-logo.png" : p.Imageurl
            })
            .ToListAsync();

        return Json(products);
    }

    // 5. SAVE OR UPDATE DRAFT SALE (ATOMIC TRANSACTION)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft([FromBody] SaveDraftRequestModel model)
    {
        if (model == null)
        {
            return Json(new { success = false, message = "بيانات طلب المسودة غير صالحة." });
        }

        var openDay = await _context.SalesDays
            .FirstOrDefaultAsync(sd => sd.Id == model.SalesDayId && sd.Status == "Open");

        if (openDay == null)
        {
            return Json(new { success = false, message = "لا يوجد يوم بيع مفتوح لهذا الطلب." });
        }

        if (model.Items == null || !model.Items.Any())
        {
            return Json(new { success = false, message = "يجب إضافة منتج واحد على الأقل للمسودة." });
        }

        var productIds = model.Items.Select(i => i.ProductId).Distinct().ToList();
        var dbProducts = await _context.Products
            .Include(p => p.RetailPrices)
            .Where(p => productIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        foreach (var item in model.Items)
        {
            if (!dbProducts.ContainsKey(item.ProductId))
            {
                return Json(new { success = false, message = $"المنتج رقم {item.ProductId} غير موجود في الكتالوج." });
            }
            if (item.Quantity <= 0)
            {
                return Json(new { success = false, message = "كمية المنتج يجب أن تكون أكبر من 0." });
            }
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (!model.SaleId.HasValue || model.SaleId.Value <= 0)
                {
                    var existingDraft = await _context.Sales
                        .Where(s => s.SalesDayId == openDay.Id && s.Status == "Draft")
                        .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                        .FirstOrDefaultAsync();

                    if (existingDraft != null)
                    {
                        model.SaleId = existingDraft.Id;
                    }
                }

                Sale sale;
                if (model.SaleId.HasValue && model.SaleId.Value > 0)
                {
                    sale = await _context.Sales
                        .Include(s => s.SaleItems)
                        .FirstOrDefaultAsync(s => s.Id == model.SaleId.Value && s.Status == "Draft");

                    if (sale == null)
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = "المسودة المحددة غير موجودة أو ليست بحالة مسودة." });
                    }

                    if (sale.SaleItems != null && sale.SaleItems.Any())
                    {
                        _context.SaleItems.RemoveRange(sale.SaleItems);
                    }

                    sale.UpdatedAt = DateTime.Now;
                }
                else
                {
                    var invoiceNum = $"POS-{openDay.Id}-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
                    sale = new Sale
                    {
                        SalesDayId = openDay.Id,
                        InvoiceNumber = invoiceNum,
                        Status = "Draft",
                        CreatedBy = User.Identity?.Name ?? "المدير",
                        CreatedAt = DateTime.Now
                    };

                    _context.Sales.Add(sale);
                }

                sale.CustomerName = model.CustomerName?.Trim();
                sale.CustomerPhone = model.CustomerPhone?.Trim();
                sale.Notes = model.Notes?.Trim();

                var draftDeductionGroups = new Dictionary<int, int>();
                foreach (var item in model.Items)
                {
                    if (!dbProducts.TryGetValue(item.ProductId, out var product))
                    {
                        await transaction.RollbackAsync();
                        return Json(new { success = false, message = $"المنتج رقم {item.ProductId} غير موجود." });
                    }

                    var retailPrice = await _inventoryService.ValidateRetailPriceAsync(product, item.RetailPriceId);
                    var deductionAmount = _inventoryService.CalculateDeductionAmount(product, item.Quantity, retailPrice?.SizeMl);

                    if (draftDeductionGroups.ContainsKey(product.Id))
                        draftDeductionGroups[product.Id] += deductionAmount;
                    else
                        draftDeductionGroups[product.Id] = deductionAmount;
                }

                foreach (var kvp in draftDeductionGroups)
                {
                    var prodId = kvp.Key;
                    var totalDeduction = kvp.Value;
                    var product = dbProducts[prodId];
                    if (totalDeduction > product.Stockquantity)
                    {
                        await transaction.RollbackAsync();
                        string msg = string.Equals(product.StockUnit, "Ml", StringComparison.OrdinalIgnoreCase)
                            ? $"الكمية المطلوبة من ({product.Name}) تتجاوز المخزون المتوفر بالمليلتر."
                            : $"الكمية المطلوبة من ({product.Name}) أكبر من المخزون المتوفر.";
                        return Json(new { success = false, message = msg });
                    }
                }

                decimal grossSubtotal = 0m;
                decimal lineDiscountsTotal = 0m;
                var newItems = new List<SaleItem>();
                foreach (var item in model.Items)
                {
                    var product = dbProducts[item.ProductId];
                    var retailPrice = await _inventoryService.ValidateRetailPriceAsync(product, item.RetailPriceId);
                    var unitPrice = _inventoryService.GetUnitPrice(product, retailPrice);
                    var lineGross = unitPrice * item.Quantity;
                    var lineDiscount = Math.Max(0m, item.Discount);
                    if (lineDiscount > lineGross) lineDiscount = lineGross;
                    var lineTotal = lineGross - lineDiscount;

                    grossSubtotal += lineGross;
                    lineDiscountsTotal += lineDiscount;

                    newItems.Add(new SaleItem
                    {
                        ProductId = product.Id,
                        RetailPriceId = item.RetailPriceId,
                        RetailSizeMl = retailPrice?.SizeMl,
                        ProductName = product.Name,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice,
                        Discount = lineDiscount,
                        Total = lineTotal
                    });
                }

                var overallDiscount = Math.Max(0m, model.DiscountTotal);
                var totalDiscounts = lineDiscountsTotal + overallDiscount;

                sale.TotalAmount = grossSubtotal;
                sale.DiscountTotal = totalDiscounts;
                sale.FinalAmount = Math.Max(0m, grossSubtotal - totalDiscounts);

                if (sale.Id == 0)
                {
                    await _context.SaveChangesAsync();
                }

                foreach (var item in newItems)
                {
                    item.SaleId = sale.Id;
                    _context.SaleItems.Add(item);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    saleId = sale.Id,
                    invoiceNumber = sale.InvoiceNumber,
                    message = "تم حفظ المسودة في قاعدة البيانات بنجاح."
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[SaveDraft Error] {ex}");
                return Json(new { success = false, message = "حدث خطأ غير متوقع أثناء حفظ المسودة. يرجى المحاولة مرة أخرى." });
            }
        });
    }

    // 6. DRAFT SALES LIST PAGE
    public async Task<IActionResult> Drafts()
    {
        var openDay = await _context.SalesDays
            .AsNoTracking()
            .FirstOrDefaultAsync(sd => sd.Status == "Open");

        var drafts = new List<Sale>();
        if (openDay != null)
        {
            drafts = await _context.Sales
                .AsNoTracking()
                .Include(s => s.SaleItems)
                .ThenInclude(si => si.Product)
                .Where(s => s.SalesDayId == openDay.Id && s.Status == "Draft")
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .ToListAsync();
        }

        return View(drafts);
    }

    // 7. DELETE / CANCEL DRAFT (ATOMIC TRANSACTION)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDraft(int id)
    {
        if (id <= 0)
        {
            return Json(new { success = false, message = "معرف المسودة غير صالح." });
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var draft = await _context.Sales
                    .FirstOrDefaultAsync(s => s.Id == id && s.Status == "Draft");

                if (draft == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "المسودة غير موجودة أو تم معالجتها سابقاً." });
                }

                await _context.SaleItems
                    .Where(si => si.SaleId == draft.Id)
                    .ExecuteDeleteAsync();

                await _context.SalePayments
                    .Where(sp => sp.SaleId == draft.Id)
                    .ExecuteDeleteAsync();

                _context.Sales.Remove(draft);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new { success = true, message = "تم حذف المسودة بنجاح." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[DeleteDraft Error] {ex}");
                return Json(new { success = false, message = "حدث خطأ غير متوقع أثناء حذف المسودة. يرجى المحاولة مرة أخرى." });
            }
        });
    }

    // 8. GET ACTIVE PAYMENT METHODS LIST FOR POS CHECKOUT
    [HttpGet]
    public async Task<IActionResult> GetPaymentMethods()
    {
        var methods = await GetActivePaymentMethodsListAsync();
        var result = methods.Select(pm => new PaymentMethodOptionDto
        {
            Id = pm.Id,
            Name = pm.Name,
            Type = pm.Type,
            CardColor = pm.Cardcolor
        }).ToList();

        return Json(result);
    }

    private async Task<List<Paymentmethod>> GetActivePaymentMethodsListAsync()
    {
        var activeMethods = await _context.Paymentmethods
            .AsNoTracking()
            .Where(pm => pm.Isactive)
            .ToListAsync();

        var cashMethod = activeMethods.FirstOrDefault(pm =>
            (pm.Type != null && pm.Type.Equals("Cash", StringComparison.OrdinalIgnoreCase)) ||
            (pm.Name != null && (pm.Name.Contains("نقدي") || pm.Name.Contains("نقد") || pm.Name.Equals("Cash", StringComparison.OrdinalIgnoreCase))));

        if (cashMethod == null)
        {
            var existingCash = await _context.Paymentmethods
                .FirstOrDefaultAsync(pm =>
                    (pm.Type != null && pm.Type.ToLower() == "cash") ||
                    (pm.Name != null && (pm.Name.Contains("نقدي") || pm.Name.Contains("نقد") || pm.Name.ToLower() == "cash")));

            if (existingCash != null)
            {
                existingCash.Isactive = true;
                await _context.SaveChangesAsync();
                cashMethod = existingCash;
            }
            else
            {
                cashMethod = new Paymentmethod
                {
                    Name = "نقدي",
                    Type = "Cash",
                    Accountholdername = "المتجر",
                    Accountnumber = "CASH-001",
                    Isactive = true,
                    Storesettingsid = 1
                };
                _context.Paymentmethods.Add(cashMethod);
                await _context.SaveChangesAsync();
            }

            if (!activeMethods.Any(pm => pm.Id == cashMethod.Id))
            {
                activeMethods.Add(cashMethod);
            }
        }

        var resultList = new List<Paymentmethod> { cashMethod };
        resultList.AddRange(activeMethods
            .Where(pm => pm.Id != cashMethod.Id)
            .OrderBy(pm => pm.Name));

        return resultList;
    }

    // 9. CONFIRM SALE & DEDUCT INVENTORY
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteSale([FromBody] CompleteSaleRequestModel model)
    {
        if (model == null || model.Items == null || !model.Items.Any())
        {
            return Json(new CompleteSaleResponseDto { Success = false, Message = "يجب إدخال منتج واحد على الأقل لاعتماد عملية البيع." });
        }

        // 1. Validate Active Sales Day
        var openDay = await _context.SalesDays
            .FirstOrDefaultAsync(sd => sd.Id == model.SalesDayId && sd.Status == "Open");

        if (openDay == null)
        {
            return Json(new CompleteSaleResponseDto { Success = false, Message = "يوم البيع مغلق أو غير موجود. لا يمكن اعتماد المبيعات على يوم مغلق." });
        }

        // 2. Validate Active Payment Methods from DB
        var activePaymentMethods = await GetActivePaymentMethodsListAsync();
        var activePaymentMethodIds = activePaymentMethods.Select(pm => pm.Id).ToHashSet();

        // Aggregate duplicate payments if same method selected twice
        var aggregatedPayments = model.Payments
            .Where(p => p.PaymentMethodId > 0 && p.Amount > 0)
            .GroupBy(p => p.PaymentMethodId)
            .Select(g => new CompleteSalePaymentModel
            {
                PaymentMethodId = g.Key,
                Amount = g.Sum(x => x.Amount),
                TransactionReference = g.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x.TransactionReference))?.TransactionReference
            })
            .ToList();

        if (!aggregatedPayments.Any())
        {
            return Json(new CompleteSaleResponseDto { Success = false, Message = "يرجى إدخال مبلغ صحيح أكبر من 0 لطريقة الدفع." });
        }

        foreach (var p in aggregatedPayments)
        {
            if (!activePaymentMethodIds.Contains(p.PaymentMethodId))
            {
                return Json(new CompleteSaleResponseDto { Success = false, Message = "طريقة الدفع المحددة غير مفعلة أو غير موجودة بالنظام." });
            }
        }

        // 3. Begin EF Core Database Transaction for Atomic Operations under Retrying Execution Strategy
        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 4. Load or create Sale record
                Sale sale;
                if (model.SaleId.HasValue && model.SaleId.Value > 0)
                {
                    sale = await _context.Sales
                        .FirstOrDefaultAsync(s => s.Id == model.SaleId.Value && s.SalesDayId == openDay.Id);

                    if (sale == null)
                    {
                        await transaction.RollbackAsync();
                        return Json(new CompleteSaleResponseDto { Success = false, Message = "المسودة المطلوبة غير موجودة في يوم البيع الحالي أو تم حذفها." });
                    }

                    if (sale.Status == "Completed")
                    {
                        await transaction.RollbackAsync();
                        return Json(new CompleteSaleResponseDto
                        {
                            Success = true,
                            Message = "تم اعتماد عملية البيع هذه سابقاً ولا يمكن اعتمادها مرتين.",
                            SaleId = sale.Id,
                            InvoiceNumber = sale.InvoiceNumber,
                            FinalAmount = sale.FinalAmount,
                            CompletedAt = sale.CompletedAt?.ToString("yyyy/MM/dd HH:mm")
                        });
                    }

                    if (sale.Status != "Draft")
                    {
                        await transaction.RollbackAsync();
                        return Json(new CompleteSaleResponseDto { Success = false, Message = "حالة عملية البيع لا تسمح بالاعتماد." });
                    }

                    // Delete existing items & payments directly without EF entity tracking concurrency conflicts
                    await _context.SaleItems
                        .Where(si => si.SaleId == sale.Id)
                        .ExecuteDeleteAsync();

                    await _context.SalePayments
                        .Where(sp => sp.SaleId == sale.Id)
                        .ExecuteDeleteAsync();
                }
                else
                {
                    var invoiceNum = $"POS-{openDay.Id}-{DateTime.Now:yyyyMMddHHmmss}-{Random.Shared.Next(100, 999)}";
                    sale = new Sale
                    {
                        SalesDayId = openDay.Id,
                        InvoiceNumber = invoiceNum,
                        CreatedBy = User.Identity?.Name ?? "المدير",
                        CreatedAt = DateTime.Now
                    };
                    _context.Sales.Add(sale);
                }

                // 5. Product Stock Validation & Atomic Deductions
                var productIds = model.Items.Select(i => i.ProductId).Distinct().ToList();

                // Fetch products from DB
                var dbProducts = await _context.Products
                    .Include(p => p.RetailPrices)
                    .Where(p => productIds.Contains(p.Id))
                    .ToDictionaryAsync(p => p.Id);

                // Group deductions per ProductId to safely validate and deduct inventory even if product appears in multiple rows
                var deductionGroups = new Dictionary<int, int>();
                foreach (var item in model.Items)
                {
                    if (!dbProducts.TryGetValue(item.ProductId, out var product))
                    {
                        await transaction.RollbackAsync();
                        return Json(new CompleteSaleResponseDto { Success = false, Message = $"المنتج رقم {item.ProductId} غير موجود بالكتالوج." });
                    }

                    if (item.Quantity <= 0)
                    {
                        await transaction.RollbackAsync();
                        return Json(new CompleteSaleResponseDto { Success = false, Message = $"كمية المنتج ({product.Name}) يجب أن تكون أكبر من 0." });
                    }

                    var retailPrice = await _inventoryService.ValidateRetailPriceAsync(product, item.RetailPriceId);
                    var deductionAmount = _inventoryService.CalculateDeductionAmount(product, item.Quantity, retailPrice?.SizeMl);

                    if (deductionGroups.ContainsKey(product.Id))
                        deductionGroups[product.Id] += deductionAmount;
                    else
                        deductionGroups[product.Id] = deductionAmount;
                }

                // Deduct stock per product
                foreach (var kvp in deductionGroups)
                {
                    var prodId = kvp.Key;
                    var totalDeduction = kvp.Value;
                    var product = dbProducts[prodId];
                    try
                    {
                        await _inventoryService.DeductStockAsync(prodId, totalDeduction);
                    }
                    catch (InvalidOperationException)
                    {
                        await transaction.RollbackAsync();
                        return Json(new CompleteSaleResponseDto
                        {
                            Success = false,
                            Message = $"عذراً، الكمية المتوفرة غير كافية للمنتج ({product.Name}) أو تم تعديل المخزون بنفس الوقت."
                        });
                    }
                }

                // Compute Trusted Prices & Totals
                decimal grossSubtotal = 0m;
                decimal lineDiscountsTotal = 0m;
                var newSaleItems = new List<SaleItem>();

                foreach (var item in model.Items)
                {
                    var product = dbProducts[item.ProductId];
                    var retailPrice = await _inventoryService.ValidateRetailPriceAsync(product, item.RetailPriceId);
                    var unitPrice = _inventoryService.GetUnitPrice(product, retailPrice);
                    var lineGross = unitPrice * item.Quantity;
                    var lineDiscount = Math.Max(0m, item.Discount);
                    if (lineDiscount > lineGross) lineDiscount = lineGross;
                    var lineTotal = lineGross - lineDiscount;

                    grossSubtotal += lineGross;
                    lineDiscountsTotal += lineDiscount;

                    newSaleItems.Add(new SaleItem
                    {
                        ProductId = product.Id,
                        RetailPriceId = item.RetailPriceId,
                        RetailSizeMl = retailPrice?.SizeMl,
                        ProductName = product.Name,
                        Quantity = item.Quantity,
                        UnitPrice = unitPrice,
                        Discount = lineDiscount,
                        Total = lineTotal
                    });
                }

                var overallDiscount = Math.Max(0m, model.DiscountTotal);
                var totalDiscounts = lineDiscountsTotal + overallDiscount;
                var finalAmount = Math.Max(0m, grossSubtotal - totalDiscounts);
                var totalPaid = aggregatedPayments.Sum(p => p.Amount);

                // 6. Validate Payment Total Matches Final Amount Exactly
                if (Math.Abs(totalPaid - finalAmount) > 0.01m)
                {
                    await transaction.RollbackAsync();
                    return Json(new CompleteSaleResponseDto
                    {
                        Success = false,
                        Message = $"مجموع الدفعات المدخلة ({totalPaid:N2} ر.س) لا يساوي المبلغ الإجمالي النهائي للبيع ({finalAmount:N2} ر.س)."
                    });
                }

                var newSalePayments = new List<SalePayment>();
                foreach (var p in aggregatedPayments)
                {
                    newSalePayments.Add(new SalePayment
                    {
                        PaymentMethodId = p.PaymentMethodId,
                        Amount = p.Amount,
                        TransactionReference = p.TransactionReference?.Trim(),
                        CreatedAt = DateTime.Now
                    });
                }

                // Complete Sale Object Status
                sale.CustomerName = model.CustomerName?.Trim();
                sale.CustomerPhone = model.CustomerPhone?.Trim();
                sale.Notes = model.Notes?.Trim();
                sale.TotalAmount = grossSubtotal;
                sale.DiscountTotal = totalDiscounts;
                sale.FinalAmount = finalAmount;
                sale.Status = "Completed";
                sale.CompletedAt = DateTime.Now;

                // Save Sale Header First
                await _context.SaveChangesAsync();

                // Attach items and payments with valid sale.Id
                foreach (var item in newSaleItems)
                {
                    item.SaleId = sale.Id;
                    _context.SaleItems.Add(item);
                }

                foreach (var payment in newSalePayments)
                {
                    payment.SaleId = sale.Id;
                    _context.SalePayments.Add(payment);
                }

                // Save SaleItems and SalePayments to PostgreSQL
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new CompleteSaleResponseDto
                {
                    Success = true,
                    Message = "تم اعتماد عملية البيع وتحديث المخزون بنجاح!",
                    SaleId = sale.Id,
                    InvoiceNumber = sale.InvoiceNumber,
                    FinalAmount = sale.FinalAmount,
                    TotalPaid = totalPaid,
                    CompletedAt = sale.CompletedAt?.ToString("yyyy/MM/dd HH:mm")
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[CompleteSale Error] {ex}");
                return Json(new CompleteSaleResponseDto
                {
                    Success = false,
                    Message = "حدث خطأ غير متوقع أثناء معالجة اعتماد البيع. يرجى المحاولة مرة أخرى."
                });
            }
        });
    }

    // 10. DAILY LEDGER / ACCOUNTING SUMMARY VIEW
    [HttpGet]
    public async Task<IActionResult> Ledger(int? id)
    {
        SalesDay? salesDay = null;

        if (id.HasValue && id.Value > 0)
        {
            salesDay = await _context.SalesDays
                .AsNoTracking()
                .FirstOrDefaultAsync(sd => sd.Id == id.Value);
        }
        else
        {
            salesDay = await _context.SalesDays
                .AsNoTracking()
                .FirstOrDefaultAsync(sd => sd.Status == "Open")
                ?? await _context.SalesDays
                    .AsNoTracking()
                    .OrderByDescending(sd => sd.Date)
                    .FirstOrDefaultAsync();
        }

        if (salesDay == null)
        {
            TempData["ErrorMessage"] = "لا يوجد يوم بيع محدد أو دفتر مبيعات متاح.";
            return RedirectToAction(nameof(Index));
        }

        var completedCount = await _context.Sales
            .AsNoTracking()
            .CountAsync(s => s.SalesDayId == salesDay.Id && s.Status == "Completed");

        var draftsCount = await _context.Sales
            .AsNoTracking()
            .CountAsync(s => s.SalesDayId == salesDay.Id && s.Status == "Draft");

        var totalSales = await _context.Sales
            .AsNoTracking()
            .Where(s => s.SalesDayId == salesDay.Id && s.Status == "Completed")
            .SumAsync(s => (decimal?)s.FinalAmount) ?? 0m;

        var paymentTotals = await _context.SalePayments
            .AsNoTracking()
            .Where(sp => sp.Sale.SalesDayId == salesDay.Id && sp.Sale.Status == "Completed")
            .GroupBy(sp => new { sp.PaymentMethodId, sp.PaymentMethod.Name, sp.PaymentMethod.Type })
            .Select(g => new PaymentMethodTotalDto
            {
                PaymentMethodId = g.Key.PaymentMethodId,
                PaymentMethodName = g.Key.Name,
                PaymentMethodType = g.Key.Type,
                TotalAmount = g.Sum(x => x.Amount)
            })
            .OrderBy(p => p.PaymentMethodName)
            .ToListAsync();

        var completedSales = await _context.Sales
            .AsNoTracking()
            .Include(s => s.SaleItems)
            .Include(s => s.SalePayments)
            .ThenInclude(sp => sp.PaymentMethod)
            .Where(s => s.SalesDayId == salesDay.Id && s.Status == "Completed")
            .OrderByDescending(s => s.CompletedAt ?? s.CreatedAt)
            .ToListAsync();

        var openDay = await _context.SalesDays
            .AsNoTracking()
            .FirstOrDefaultAsync(sd => sd.Status == "Open");

        var viewModel = new SalesDayLedgerViewModel
        {
            SalesDay = salesDay,
            CompletedSalesCount = completedCount,
            DraftsCount = draftsCount,
            TotalSalesAmount = totalSales,
            NetTotalAmount = totalSales,
            PaymentTotals = paymentTotals,
            CompletedSales = completedSales,
            IsCurrentOpenDay = openDay != null && openDay.Id == salesDay.Id
        };

        return View(viewModel);
    }

    // 11. GET SALE DETAILS (JSON / MODAL DATA)
    [HttpGet]
    public async Task<IActionResult> SaleDetails(int id)
    {
        var sale = await _context.Sales
            .AsNoTracking()
            .Include(s => s.SaleItems)
            .ThenInclude(si => si.Product)
            .Include(s => s.SalePayments)
            .ThenInclude(sp => sp.PaymentMethod)
            .Include(s => s.SalesDay)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (sale == null)
        {
            return Json(new { success = false, message = "تفاصيل عملية البيع غير موجودة." });
        }

        var dto = new
        {
            success = true,
            id = sale.Id,
            invoiceNumber = sale.InvoiceNumber,
            date = sale.SalesDay?.Date.ToString("yyyy/MM/dd"),
            time = (sale.CompletedAt ?? sale.CreatedAt).ToString("yyyy/MM/dd HH:mm"),
            status = sale.Status,
            customerName = string.IsNullOrWhiteSpace(sale.CustomerName) ? "عميل عادي" : sale.CustomerName,
            customerPhone = string.IsNullOrWhiteSpace(sale.CustomerPhone) ? "-" : sale.CustomerPhone,
            notes = string.IsNullOrWhiteSpace(sale.Notes) ? "-" : sale.Notes,
            createdBy = sale.CreatedBy,
            totalAmount = sale.TotalAmount,
            discountTotal = sale.DiscountTotal,
            finalAmount = sale.FinalAmount,
            items = sale.SaleItems.Select(si => new
            {
                productId = si.ProductId,
                productName = si.ProductName,
                retailSizeMl = si.RetailSizeMl,
                stockUnit = si.Product != null ? si.Product.StockUnit : "Piece",
                quantity = si.Quantity,
                unitPrice = si.UnitPrice,
                discount = si.Discount,
                total = si.Total
            }),
            payments = sale.SalePayments.Select(sp => new
            {
                methodName = sp.PaymentMethod?.Name ?? "طريقة دفع",
                methodCode = sp.PaymentMethod?.Type ?? "code",
                amount = sp.Amount,
                reference = sp.TransactionReference ?? "-"
            })
        };

        return Json(dto);
    }

    // 12. CLOSE SALES DAY (POST / AJAX)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseDay([FromBody] CloseSalesDayRequestModel model)
    {
        if (model == null || model.SalesDayId <= 0)
        {
            return Json(new { success = false, message = "طلب إغلاق اليوم غير صالحة." });
        }

        var strategy = _context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var salesDay = await _context.SalesDays
                    .FirstOrDefaultAsync(sd => sd.Id == model.SalesDayId);

                if (salesDay == null)
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "يوم البيع المطلوب غير موجود." });
                }

                if (salesDay.Status == "Closed")
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "يوم البيع مغلق بالفعل." });
                }

                // Strict Rule: Disallow closing if active draft sales exist
                var activeDraftsCount = await _context.Sales
                    .CountAsync(s => s.SalesDayId == salesDay.Id && s.Status == "Draft");

                if (activeDraftsCount > 0)
                {
                    await transaction.RollbackAsync();
                    return Json(new
                    {
                        success = false,
                        message = $"لا يمكن إغلاق يوم البيع لوجود ({activeDraftsCount}) مسودة مبيعات غير مكتملة. يرجى فتحها واعتمادها أو حذفها أولاً قبل الإغلاق."
                    });
                }

                var completedSales = await _context.Sales
                    .Where(s => s.SalesDayId == salesDay.Id && s.Status == "Completed")
                    .ToListAsync();

                var totalSales = completedSales.Sum(s => s.FinalAmount);

                var paymentTotals = await _context.SalePayments
                    .Where(sp => sp.Sale.SalesDayId == salesDay.Id && sp.Sale.Status == "Completed")
                    .GroupBy(sp => new { sp.PaymentMethod.Type, sp.PaymentMethod.Name })
                    .Select(g => new { Type = g.Key.Type, Name = g.Key.Name, Total = g.Sum(x => x.Amount) })
                    .ToListAsync();

                salesDay.TotalSales = totalSales;
                salesDay.TotalCash = paymentTotals.Where(p => p.Type != null && (p.Type.Equals("Cash", StringComparison.OrdinalIgnoreCase) || p.Type.Contains("cash") || p.Type.Contains("نقدي"))).Sum(p => p.Total);
                salesDay.TotalWallet = paymentTotals.Where(p => p.Type != null && (p.Type.Equals("Wallet", StringComparison.OrdinalIgnoreCase) || p.Type.Contains("wallet") || p.Type.Contains("محفظة"))).Sum(p => p.Total);
                // TotalTransfer aggregates all remaining electronic/bank/custom payment methods (Al-Amqi, Al-Kuraimi, Transfer, Card, Custom, etc.) so NO payment is dropped
                salesDay.TotalTransfer = paymentTotals.Where(p => !(p.Type != null && (p.Type.Equals("Cash", StringComparison.OrdinalIgnoreCase) || p.Type.Contains("cash") || p.Type.Contains("نقدي"))) && !(p.Type != null && (p.Type.Equals("Wallet", StringComparison.OrdinalIgnoreCase) || p.Type.Contains("wallet") || p.Type.Contains("محفظة")))).Sum(p => p.Total);
                salesDay.NetTotal = totalSales;

                if (!string.IsNullOrWhiteSpace(model.Notes))
                {
                    salesDay.Notes = string.IsNullOrWhiteSpace(salesDay.Notes) ? model.Notes.Trim() : (salesDay.Notes + " | " + model.Notes.Trim());
                }

                salesDay.Status = "Closed";
                salesDay.ClosedAt = DateTime.Now;
                salesDay.ClosedBy = User.Identity?.Name ?? "المدير";

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = $"تم إغلاق يوم البيع بتاريخ {salesDay.Date:yyyy/MM/dd} وتوثيق السجل اليومي بنجاح.",
                    salesDayId = salesDay.Id
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"[CloseDay Error] {ex}");
                return Json(new { success = false, message = "حدث خطأ غير متوقع أثناء إغلاق يوم البيع. يرجى المحاولة مرة أخرى." });
            }
        });
    }

    // 13. SALES DAY HISTORY (HISTORICAL LEDGERS LIST)
    [HttpGet]
    public async Task<IActionResult> History()
    {
        var days = await _context.SalesDays
            .AsNoTracking()
            .OrderByDescending(sd => sd.Date)
            .ThenByDescending(sd => sd.CreatedAt)
            .ToListAsync();

        var dayIds = days.Select(d => d.Id).ToList();

        var completedCounts = await _context.Sales
            .AsNoTracking()
            .Where(s => dayIds.Contains(s.SalesDayId) && s.Status == "Completed")
            .GroupBy(s => s.SalesDayId)
            .Select(g => new { SalesDayId = g.Key, Count = g.Count(), Total = g.Sum(x => x.FinalAmount) })
            .ToDictionaryAsync(g => g.SalesDayId);

        var draftsCounts = await _context.Sales
            .AsNoTracking()
            .Where(s => dayIds.Contains(s.SalesDayId) && s.Status == "Draft")
            .GroupBy(s => s.SalesDayId)
            .Select(g => new { SalesDayId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.SalesDayId);

        var historyItems = days.Select(d => new SalesDayHistoryItemDto
        {
            Id = d.Id,
            Date = d.Date,
            Status = d.Status,
            OpeningBalance = d.OpeningBalance,
            CompletedSalesCount = completedCounts.ContainsKey(d.Id) ? completedCounts[d.Id].Count : 0,
            TotalSales = completedCounts.ContainsKey(d.Id) ? completedCounts[d.Id].Total : 0m,
            DraftsCount = draftsCounts.ContainsKey(d.Id) ? draftsCounts[d.Id].Count : 0,
            CreatedAt = d.CreatedAt,
            ClosedAt = d.ClosedAt,
            ClosedBy = d.ClosedBy
        }).ToList();

        return View(historyItems);
    }

    // 14. POS REPORTS DASHBOARD (PHASE 5)
    [HttpGet]
    public async Task<IActionResult> Reports(DateTime? startDate, DateTime? endDate)
    {
        var start = (startDate ?? DateTime.Today.AddDays(-7)).Date;
        var end = (endDate ?? DateTime.Today).Date;

        var completedSalesQuery = _context.Sales
            .AsNoTracking()
            .Where(s => s.Status == "Completed" && s.SalesDay.Date >= start && s.SalesDay.Date <= end);

        var totalOperations = await completedSalesQuery.CountAsync();
        var grossSales = await completedSalesQuery.SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;
        var totalDiscounts = await completedSalesQuery.SumAsync(s => (decimal?)s.DiscountTotal) ?? 0m;
        var netSales = await completedSalesQuery.SumAsync(s => (decimal?)s.FinalAmount) ?? 0m;

        var paymentTotals = await _context.SalePayments
            .AsNoTracking()
            .Where(sp => sp.Sale.Status == "Completed" && sp.Sale.SalesDay.Date >= start && sp.Sale.SalesDay.Date <= end)
            .GroupBy(sp => new { sp.PaymentMethodId, sp.PaymentMethod.Name, sp.PaymentMethod.Type })
            .Select(g => new PaymentMethodTotalDto
            {
                PaymentMethodId = g.Key.PaymentMethodId,
                PaymentMethodName = g.Key.Name,
                PaymentMethodType = g.Key.Type,
                TotalAmount = g.Sum(x => x.Amount)
            })
            .OrderByDescending(p => p.TotalAmount)
            .ToListAsync();

        var topProducts = await _context.SaleItems
            .AsNoTracking()
            .Where(si => si.Sale.Status == "Completed" && si.Sale.SalesDay.Date >= start && si.Sale.SalesDay.Date <= end)
            .GroupBy(si => new { si.ProductId, si.ProductName, si.RetailSizeMl })
            .Select(g => new ProductSalesSummaryDto
            {
                ProductId = g.Key.ProductId,
                ProductName = g.Key.ProductName,
                RetailSizeMl = g.Key.RetailSizeMl,
                TotalQuantitySold = g.Sum(x => x.Quantity),
                TotalRevenue = g.Sum(x => x.Total)
            })
            .OrderByDescending(p => p.TotalRevenue)
            .Take(15)
            .ToListAsync();

        var sampleSales = await completedSalesQuery
            .Include(s => s.SalePayments)
            .ThenInclude(sp => sp.PaymentMethod)
            .OrderByDescending(s => s.CompletedAt ?? s.CreatedAt)
            .Take(20)
            .ToListAsync();

        var viewModel = new QuickSalesReportsIndexViewModel
        {
            StartDate = start,
            EndDate = end,
            TotalOperations = totalOperations,
            GrossSales = grossSales,
            TotalDiscounts = totalDiscounts,
            NetSales = netSales,
            PaymentTotals = paymentTotals,
            TopProducts = topProducts,
            SampleCompletedSales = sampleSales
        };

        return View(viewModel);
    }
}
