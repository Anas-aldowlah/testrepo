using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ModelBinding.Metadata;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;

await RazorRenderingChecks.RunFromEnvironmentAsync();
AssertArabicValidationConfiguration();

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
