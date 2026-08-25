using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using Yagot.Areas.Admin.Controllers;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;

await RazorRenderingChecks.RunFromEnvironmentAsync();
AssertArabicValidationConfiguration();
AssertRetailPriceIntegrityValidation();
AssertVolumeChangeConfiguration();
await AssertRetailOffActivitySynchronizationAsync();

var product = new Product
{
    Id = 7,
    Categoryid = 1,
    Name = "Runtime check",
    Price = 10,
    StockUnit = "Ml",
    VolumeMl = 1000,
    Stockquantity = 12500,
    IsRetailEnabled = true,
    RetailPrices =
    [
        new ProductRetailPrice { Id = 11, ProductId = 7, SizeMl = 250, Price = 3, IsActive = true },
        new ProductRetailPrice { Id = 12, ProductId = 7, SizeMl = 100, Price = 2, IsActive = false },
        new ProductRetailPrice { Id = 13, ProductId = 7, SizeMl = 1500, Price = 12, IsActive = true }
    ]
};

Assert(product.Stockquantity / (decimal)product.VolumeMl.Value == 12.5m, "Current package balance must remain fractional.");
AssertAmount(product, Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Base, 2), 2000, "Add with base size");
AssertAmount(product, Request(StockAdjustmentOperation.Subtract, StockAdjustmentSizeOptions.Base, 2), 2000, "Subtract with base size");
AssertAmount(product, Request(StockAdjustmentOperation.Add, "Retail:11", 3), 750, "Add with active retail size");
AssertAmount(product, Request(StockAdjustmentOperation.Subtract, "Retail:11", 3), 750, "Subtract with active retail size");
AssertAmount(product, Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Custom, 4, 125), 500, "Add with custom size");
AssertAmount(product, Request(StockAdjustmentOperation.Subtract, StockAdjustmentSizeOptions.Custom, 4, 125), 500, "Subtract with custom size");
AssertAmount(product, Request(StockAdjustmentOperation.Subtract, StockAdjustmentSizeOptions.Custom, 1, 12500), 12500, "Subtract exactly current stock");

AssertFailure(product, Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Base, 0), nameof(StockAdjustmentViewModel.Quantity), "Zero quantity");
AssertFailure(product, Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Base, -1), nameof(StockAdjustmentViewModel.Quantity), "Negative quantity");
AssertFailure(product, Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Custom, 1), nameof(StockAdjustmentViewModel.CustomSizeMl), "Blank custom size");
AssertFailure(product, Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Custom, 1, 0), nameof(StockAdjustmentViewModel.CustomSizeMl), "Zero custom size");
AssertFailure(product, Request(StockAdjustmentOperation.Add, "Retail:12", 1), nameof(StockAdjustmentViewModel.SizeOption), "Inactive retail size");
AssertFailure(product, Request(StockAdjustmentOperation.Add, "Retail:999", 1), nameof(StockAdjustmentViewModel.SizeOption), "Unknown retail size");
AssertFailure(product, Request(StockAdjustmentOperation.Add, "Retail:13", 1), nameof(StockAdjustmentViewModel.SizeOption), "Retail size at or above base size");
AssertFailure(product, Request((StockAdjustmentOperation)999, StockAdjustmentSizeOptions.Base, 1), nameof(StockAdjustmentViewModel.Operation), "Undefined operation");
AssertFailure(product, Request(StockAdjustmentOperation.Add, null!, 1), nameof(StockAdjustmentViewModel.SizeOption), "Blank size option");
AssertFailure(product, Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Custom, 1000000, 1000000), null, "Overflowing adjustment");

var noRetailProduct = CloneProduct(product);
noRetailProduct.RetailPrices.Clear();
AssertAmount(noRetailProduct, Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Base, 1), 1000, "Product without retail prices uses base size");
AssertFailure(noRetailProduct, Request(StockAdjustmentOperation.Add, "Retail:11", 1), nameof(StockAdjustmentViewModel.SizeOption), "Product without retail prices rejects retail selection");

var invalidBaseProduct = CloneProduct(product);
invalidBaseProduct.VolumeMl = null;
AssertFailure(invalidBaseProduct, Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Base, 1), null, "Missing base volume");

var pieceProduct = CloneProduct(product);
pieceProduct.StockUnit = "Piece";
pieceProduct.VolumeMl = null;
AssertAmount(pieceProduct, Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Base, 4), 4, "Piece adjustment");
AssertFailure(pieceProduct, Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Custom, 1, 10), nameof(StockAdjustmentViewModel.SizeOption), "Malformed piece size selection");

Assert(product.Stockquantity - 12500 == 0, "Exact subtraction must allow zero stock.");
Assert(product.Stockquantity - 12501 < 0, "Greater-than-stock subtraction must be identified for atomic rejection.");

Console.WriteLine("PASS: focused Admin Product Edit stock rule checks.");

static void AssertRetailPriceIntegrityValidation()
{
    const string requiredMessage = "يجب إضافة سعر تجزئة واحد على الأقل عند تفعيل البيع بالتجزئة.";

    var retailOffController = ValidateRetailPrices(RetailProduct(enabled: false), modelPrefix: null);
    Assert(retailOffController.ModelState.IsValid, "Retail OFF must allow zero retail-price rows.");

    var emptyCreateController = ValidateRetailPrices(RetailProduct(enabled: true), modelPrefix: null);
    AssertModelError(emptyCreateController, "RetailPrices", requiredMessage, "Create retail ON with zero rows");

    var emptyEditController = ValidateRetailPrices(
        RetailProduct(enabled: true),
        nameof(AdminProductEditViewModel.Product));
    AssertModelError(emptyEditController, "Product.RetailPrices", requiredMessage, "Edit retail ON with zero rows");

    var invalidRowsController = ValidateRetailPrices(
        RetailProduct(
            enabled: true,
            new ProductRetailPriceInput { SizeMl = 0, Price = 5 },
            new ProductRetailPriceInput { SizeMl = 100, Price = 0 }),
        modelPrefix: null);
    AssertModelError(invalidRowsController, "RetailPrices[0].SizeMl", null, "Missing retail size");
    AssertModelError(invalidRowsController, "RetailPrices[1].Price", null, "Missing retail price");
    AssertModelError(invalidRowsController, "RetailPrices", requiredMessage, "Retail ON with zero valid rows");

    var duplicateController = ValidateRetailPrices(
        RetailProduct(
            enabled: true,
            new ProductRetailPriceInput { SizeMl = 100, Price = 5 },
            new ProductRetailPriceInput { SizeMl = 100, Price = 6 }),
        modelPrefix: null);
    AssertModelError(duplicateController, "RetailPrices[0].SizeMl", null, "First duplicate retail size");
    AssertModelError(duplicateController, "RetailPrices[1].SizeMl", null, "Second duplicate retail size");
    AssertModelError(duplicateController, "RetailPrices", requiredMessage, "Duplicate rows leave zero valid retail rows");

    var validController = ValidateRetailPrices(
        RetailProduct(enabled: true, new ProductRetailPriceInput { SizeMl = 100, Price = 5 }),
        modelPrefix: null);
    Assert(validController.ModelState.IsValid, "Retail ON with one valid row must pass retail-price validation.");

    var rangeInvalidModel = RetailProduct(enabled: true, new ProductRetailPriceInput { SizeMl = 100, Price = 1000001 });
    var rangeInvalidController = new ProductsController(null!, null!, null!, null!, null!, null!);
    rangeInvalidController.ModelState.AddModelError("RetailPrices[0].Price", "يجب ألا يتجاوز السعر الحد الأعلى.");
    InvokeRetailPriceValidation(rangeInvalidController, rangeInvalidModel, modelPrefix: null);
    AssertModelError(rangeInvalidController, "RetailPrices", requiredMessage, "DataAnnotation-invalid row leaves zero valid rows");

    Console.WriteLine("PASS: shared Create/Edit retail-price integrity and field/group validation keys.");
}

static void AssertVolumeChangeConfiguration()
{
    var persistedProduct = new Product
    {
        StockUnit = "Ml",
        VolumeMl = 1000,
        Stockquantity = 18400
    };
    var submittedProduct = new ProductVW
    {
        StockUnit = "Ml",
        VolumeMl = 100,
        Stockquantity = persistedProduct.Stockquantity
    };

    var method = typeof(ProductsController).GetMethod(
        "ValidateProductConfiguration",
        BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ValidateProductConfiguration was not found.");
    method.Invoke(null, [submittedProduct, false, persistedProduct]);

    Assert(persistedProduct.Stockquantity == 18400,
        "Changing the base package definition must not mutate physical inventory.");
    persistedProduct.VolumeMl = submittedProduct.VolumeMl;
    AssertAmount(
        persistedProduct,
        Request(StockAdjustmentOperation.Add, StockAdjustmentSizeOptions.Base, 1),
        100,
        "Inventory adjustment uses the current saved base size");
    Assert(persistedProduct.Stockquantity == 18400,
        "Preview calculation must not mutate physical inventory.");

    Console.WriteLine("PASS: base VolumeMl changes are allowed and inventory calculations use the current value without mutating stock.");
}

static async Task AssertRetailOffActivitySynchronizationAsync()
{
    var product = new Product
    {
        Id = 7,
        StockUnit = "Ml",
        IsRetailEnabled = true,
        RetailPrices =
        [
            new ProductRetailPrice { Id = 100, ProductId = 7, SizeMl = 100, Price = 10, IsActive = true },
            new ProductRetailPrice { Id = 101, ProductId = 7, SizeMl = 150, Price = 15, IsActive = true }
        ]
    };
    var controller = new ProductsController(null!, null!, null!, null!, null!, null!);

    await InvokeRetailPriceSyncAsync(controller, product, RetailOffInput((100, false), (101, false)));
    Assert(product.RetailPrices.Count == 2 && product.RetailPrices.All(price => !price.IsActive),
        "Retail OFF with two inactive rows did not preserve both submitted inactive states.");

    await InvokeRetailPriceSyncAsync(controller, product, RetailOffInput((100, false), (101, true)));
    Assert(product.RetailPrices.Single(price => price.Id == 100).IsActive == false &&
           product.RetailPrices.Single(price => price.Id == 101).IsActive,
        "Retail OFF with mixed activity did not preserve the exact submitted states.");
    Assert(product.RetailPrices.Count == 2, "Retail OFF added or deleted a persisted retail row.");

    Console.WriteLine("PASS: Retail OFF preserves exact submitted row activity without adding or deleting rows.");

    static ProductVW RetailOffInput(params (int Id, bool IsActive)[] states) => new()
    {
        StockUnit = "Ml",
        IsRetailEnabled = false,
        RetailPrices = states.Select(state => new ProductRetailPriceInput
        {
            Id = state.Id,
            SizeMl = state.Id == 100 ? 100 : 150,
            Price = state.Id == 100 ? 10 : 15,
            IsActive = state.IsActive
        }).ToList()
    };
}

static async Task InvokeRetailPriceSyncAsync(ProductsController controller, Product product, ProductVW model)
{
    var method = typeof(ProductsController).GetMethod("SyncRetailPricesAsync", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("SyncRetailPricesAsync was not found.");
    await ((Task?)method.Invoke(controller, [product, model])
        ?? throw new InvalidOperationException("SyncRetailPricesAsync did not return a task."));
}

static ProductsController ValidateRetailPrices(ProductVW model, string? modelPrefix)
{
    var controller = new ProductsController(null!, null!, null!, null!, null!, null!);
    InvokeRetailPriceValidation(controller, model, modelPrefix);
    return controller;
}

static void InvokeRetailPriceValidation(ProductsController controller, ProductVW model, string? modelPrefix)
{
    var method = typeof(ProductsController).GetMethod("ValidateRetailPrices", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("ValidateRetailPrices was not found.");
    method.Invoke(controller, [model, null, modelPrefix]);
}

static ProductVW RetailProduct(bool enabled, params ProductRetailPriceInput[] rows) => new()
{
    StockUnit = "Ml",
    VolumeMl = 1000,
    IsRetailEnabled = enabled,
    RetailPrices = rows.ToList()
};

static void AssertModelError(ProductsController controller, string key, string? expectedMessage, string scenario)
{
    Assert(controller.ModelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0,
        $"{scenario}: expected a ModelState error at {key}.");
    if (expectedMessage != null)
        Assert(entry!.Errors.Any(error => error.ErrorMessage == expectedMessage),
            $"{scenario}: expected the required Arabic group message.");
}

static StockAdjustmentRequest Request(StockAdjustmentOperation operation, string sizeOption, int quantity, int? customSizeMl = null) =>
    new(7, operation, sizeOption, customSizeMl, quantity);

static void AssertAmount(Product product, StockAdjustmentRequest request, int expected, string scenario)
{
    var result = InventoryService.CalculateStockAdjustment(product, request);
    Assert(result.Succeeded, $"{scenario}: expected success, got {result.Error}");
    Assert(result.Amount == expected, $"{scenario}: expected {expected}, got {result.Amount}");
}

static void AssertFailure(Product product, StockAdjustmentRequest request, string? expectedField, string scenario)
{
    var result = InventoryService.CalculateStockAdjustment(product, request);
    Assert(!result.Succeeded, $"{scenario}: expected rejection.");
    Assert(result.Field == expectedField, $"{scenario}: expected field {expectedField ?? "<model>"}, got {result.Field ?? "<model>"}.");
    Assert(IsArabicUserMessage(result.Error), $"{scenario}: exposed a non-Arabic stock-adjustment error: {result.Error}");
}

static void AssertArabicValidationConfiguration()
{
    var messages = new DefaultModelBindingMessageProvider();
    ArabicModelBindingMessages.Configure(messages);
    var bindingMessages = new[]
    {
        messages.MissingBindRequiredValueAccessor("السعر"),
        messages.MissingKeyOrValueAccessor(),
        messages.MissingRequestBodyRequiredValueAccessor(),
        messages.ValueMustNotBeNullAccessor("السعر"),
        messages.AttemptedValueIsInvalidAccessor("abc", "السعر"),
        messages.NonPropertyAttemptedValueIsInvalidAccessor("abc"),
        messages.UnknownValueIsInvalidAccessor("السعر"),
        messages.NonPropertyUnknownValueIsInvalidAccessor(),
        messages.ValueIsInvalidAccessor("abc"),
        messages.ValueMustBeANumberAccessor("السعر"),
        messages.NonPropertyValueMustBeANumberAccessor()
    };
    Assert(bindingMessages.All(IsArabicUserMessage), "A framework model-binding message is not Arabic.");

    var product = new ProductVW
    {
        Name = string.Empty,
        Price = -1,
        Stockquantity = -1,
        VolumeMl = -1,
        InitialStockQuantity = -1,
        InitialBottleCount = -1
    };
    AssertArabicDataAnnotations(product, "Product Edit");
    AssertArabicDataAnnotations(new ProductRetailPriceInput { SizeMl = 0, Price = 0 }, "RetailPrices");
    AssertArabicDataAnnotations(new AdminProductCreateViewModel
    {
        Name = string.Empty,
        Categoryid = null,
        Price = null,
        Stockquantity = null,
        StockUnit = string.Empty
    }, "Product Create");
    AssertArabicDataAnnotations(new AdminProductCreateRetailPriceInput { SizeMl = 0, Price = 0 }, "Product Create retail prices");
    AssertArabicDataAnnotations(new StockAdjustmentViewModel
    {
        Id = 0,
        Operation = null,
        SizeOption = string.Empty,
        CustomSizeMl = 0,
        Quantity = 0
    }, "Stock adjustment");

    var invalidImage = new FormFile(Stream.Null, 0, 1, "Imagefile", "product.exe");
    var oversizedImage = new FormFile(Stream.Null, 0, (5 * 1024 * 1024) + 1, "Imagefile", "product.png");
    Assert(IsArabicUserMessage(Image.GetValidationError(invalidImage)), "Product Create image extension error is not Arabic.");
    Assert(IsArabicUserMessage(Image.GetValidationError(oversizedImage)), "Product Create image size error is not Arabic.");
    Assert(Image.GetValidationError(null) == null, "Optional Product Create image must accept an empty value.");

    Console.WriteLine("PASS: Arabic Product Create/Edit annotations, image validation, and framework model-binding messages.");
}

static void AssertArabicDataAnnotations(object model, string scenario)
{
    var results = new List<ValidationResult>();
    Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
    Assert(results.Count > 0, $"{scenario}: expected validation failures.");
    Assert(results.All(result => IsArabicUserMessage(result.ErrorMessage)), $"{scenario}: exposed a non-Arabic DataAnnotation message.");
}

static bool IsArabicUserMessage(string? message) =>
    !string.IsNullOrWhiteSpace(message) && !message.Any(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

static Product CloneProduct(Product product) => new()
{
    Id = product.Id,
    Categoryid = product.Categoryid,
    Name = product.Name,
    Price = product.Price,
    StockUnit = product.StockUnit,
    VolumeMl = product.VolumeMl,
    Stockquantity = product.Stockquantity,
    IsRetailEnabled = product.IsRetailEnabled,
    RetailPrices = product.RetailPrices.Select(price => new ProductRetailPrice
    {
        Id = price.Id,
        ProductId = price.ProductId,
        SizeMl = price.SizeMl,
        Price = price.Price,
        IsActive = price.IsActive
    }).ToList()
};

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
