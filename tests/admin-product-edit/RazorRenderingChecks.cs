using System.Diagnostics;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Yagot.Areas.Admin.Controllers;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;

internal static class RazorRenderingChecks
{
    private const string ConnectionVariable = "YAGOT_ADMIN_EDIT_RENDER_CONNECTION";
    private const string ProductIdsVariable = "YAGOT_ADMIN_EDIT_RENDER_PRODUCT_IDS";
    private const string MissingVolumeVariable = "YAGOT_ADMIN_EDIT_RENDER_MISSING_VOLUME";
    private const string RepositoryRootVariable = "YAGOT_REPOSITORY_ROOT";

    public static async Task RunFromEnvironmentAsync()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Console.WriteLine("SKIP: authenticated-data Razor checks require the approved read-only Performance connection.");
            return;
        }

        var options = new DbContextOptionsBuilder<NeondbContext>()
            .UseNpgsql(connectionString)
            .Options;
        var productIds = (Environment.GetEnvironmentVariable(ProductIdsVariable) ?? "1,2")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();
        await using var services = BuildViewServices();
        foreach (var productId in productIds)
            await RenderControllerResultAndAssertAsync(services, options, productId);
        Console.WriteLine($"PASS: current Product Edit controller GET and compiled Razor rendered real records: {string.Join(", ", productIds)}.");
    }

    private static ServiceProvider BuildViewServices()
    {
        var repositoryRoot = Environment.GetEnvironmentVariable(RepositoryRootVariable)
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var environment = new TestWebHostEnvironment
        {
            ApplicationName = typeof(Yagot.Areas.Admin.Controllers.ProductsController).Assembly.GetName().Name!,
            ContentRootPath = repositoryRoot,
            ContentRootFileProvider = new PhysicalFileProvider(repositoryRoot),
            WebRootPath = Path.Combine(repositoryRoot, "wwwroot"),
            WebRootFileProvider = new PhysicalFileProvider(Path.Combine(repositoryRoot, "wwwroot"))
        };
        var diagnosticListener = new DiagnosticListener("YAGOT.AdminProductEdit.RazorChecks");
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IWebHostEnvironment>(environment);
        services.AddSingleton<IHostEnvironment>(environment);
        services.AddSingleton<DiagnosticListener>(diagnosticListener);
        services.AddSingleton<DiagnosticSource>(diagnosticListener);
        services.AddSingleton<ITempDataProvider, EmptyTempDataProvider>();
        services.AddControllersWithViews(options =>
            ArabicModelBindingMessages.Configure(options.ModelBindingMessageProvider))
            .AddApplicationPart(typeof(Yagot.Areas.Admin.Controllers.ProductsController).Assembly);
        return services.BuildServiceProvider();
    }

    private static async Task RenderControllerResultAndAssertAsync(
        IServiceProvider services,
        DbContextOptions<NeondbContext> options,
        int productId)
    {
        await using var database = new NeondbContext(options);
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var routeData = new RouteData();
        routeData.Values["area"] = "Admin";
        routeData.Values["controller"] = "Products";
        routeData.Values["action"] = "Edit";
        routeData.Routers.Add(new RenderingRouter());
        var actionContext = new ActionContext(httpContext, routeData, new ControllerActionDescriptor
        {
            ControllerName = "Products",
            ActionName = "Edit",
            ControllerTypeInfo = typeof(ProductsController).GetTypeInfo()
        });
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        var controller = new ProductsController(
            new ProductService(database),
            database,
            environment,
            new Image(environment),
            new InventoryService(database),
            NullLogger<ProductsController>.Instance)
        {
            ControllerContext = new ControllerContext(actionContext)
        };
        var actionResult = await controller.Edit(productId);
        var controllerView = actionResult as ViewResult
            ?? throw new InvalidOperationException($"Product {productId} controller GET returned {actionResult.GetType().Name}.");
        var model = controllerView.Model as AdminProductEditViewModel
            ?? throw new InvalidOperationException($"Product {productId} controller GET returned an unexpected model.");
        var simulateMissingVolume = string.Equals(
            Environment.GetEnvironmentVariable(MissingVolumeVariable),
            "1",
            StringComparison.Ordinal);
        if (simulateMissingVolume)
        {
            model.Product.VolumeMl = null;
            model.StockSizes = [];
        }

        var viewEngine = services.GetRequiredService<IRazorViewEngine>();
        var viewResult = viewEngine.GetView(null, "/Areas/Admin/Views/Products/Edit.cshtml", isMainPage: true);
        if (!viewResult.Success)
            throw new InvalidOperationException($"Product Edit view was not found: {string.Join(", ", viewResult.SearchedLocations)}");

        var viewData = new ViewDataDictionary<AdminProductEditViewModel>(controllerView.ViewData) { Model = model };
        var tempData = new TempDataDictionary(httpContext, services.GetRequiredService<ITempDataProvider>());
        await using var writer = new StringWriter();
        var viewContext = new ViewContext(actionContext, viewResult.View, viewData, tempData, writer, new HtmlHelperOptions());
        await viewResult.View.RenderAsync(viewContext);
        var html = writer.ToString();

        var clientValidationMessages = Regex.Matches(
                html,
                "data-val-(?:required|range|number)=\"([^\"]*)\"",
                RegexOptions.CultureInvariant)
            .Select(match => WebUtility.HtmlDecode(match.Groups[1].Value))
            .ToList();
        Assert(clientValidationMessages.Count > 0,
            $"Product {productId} did not render client validation messages.");
        var nonArabicClientMessage = clientValidationMessages.FirstOrDefault(message => !IsArabicUserMessage(message));
        Assert(nonArabicClientMessage == null,
            $"Product {productId} rendered a non-Arabic client validation message: {nonArabicClientMessage}");

        Assert(html.Contains("تسوية المخزون", StringComparison.Ordinal), $"Product {productId} did not render the stock card.");
        Assert(html.Contains($"value=\"{productId}\"", StringComparison.Ordinal), $"Product {productId} identity was not rendered.");
        var renderedRetailRows = CountOccurrences(html, "class=\"yq-retail-price-row\"");
        var expectRetailRows = model.Product.RetailPrices.Count;
        Assert(renderedRetailRows == expectRetailRows,
            $"Product {productId} rendered {renderedRetailRows} retail rows; expected {expectRetailRows}.");
        if (simulateMissingVolume)
        {
            Assert(html.Contains("لا يمكن إجراء التسوية حتى يتم حفظ حجم عبوة أساسي صالح.", StringComparison.Ordinal),
                $"Product {productId} missing-volume edge did not render the graceful stock warning.");
            Console.WriteLine($"PASS product {productId}: in-memory missing-volume edge rendered a warning instead of a blank page.");
        }
        Console.WriteLine(
            $"PASS product {productId}: retail={model.Product.IsRetailEnabled}, volume={model.Product.VolumeMl?.ToString() ?? "null"}, rows={expectRetailRows}, activeRows={model.Product.RetailPrices.Count(row => row.IsActive)}.");

        if (!simulateMissingVolume && model.Product.IsRetailEnabled && model.Product.RetailPrices.Count > 0)
        {
            var checksStoredSizeConflict = model.Product.RetailPrices.Count > 1;
            if (checksStoredSizeConflict)
            {
                model.Product.RetailPrices[0].SizeMl = model.Product.RetailPrices[1].SizeMl;
                model.Product.RetailPrices.RemoveAt(1);
            }
            else
            {
                model.Product.RetailPrices[0].Price = 0;
            }
            var submittedRowCount = model.Product.RetailPrices.Count;
            var submittedSize = model.Product.RetailPrices[0].SizeMl;
            var submittedPrice = model.Product.RetailPrices[0].Price;
            var bindingMessages = services.GetRequiredService<IOptions<MvcOptions>>()
                .Value.ModelBindingMessageProvider;
            var numericBindingError = bindingMessages.ValueMustBeANumberAccessor("السعر");
            controller.ModelState.AddModelError("Product.Price", numericBindingError);
            var invalidPost = await controller.Edit(model.Product);
            var invalidView = invalidPost as ViewResult
                ?? throw new InvalidOperationException($"Product {productId} invalid Edit POST did not remain in-page.");
            var invalidModel = invalidView.Model as AdminProductEditViewModel
                ?? throw new InvalidOperationException($"Product {productId} invalid Edit POST returned an unexpected model.");
            Assert(string.Equals(invalidView.ViewName, "Edit", StringComparison.Ordinal),
                $"Product {productId} invalid Edit POST did not explicitly return the Edit view.");
            Assert(invalidModel.Product.RetailPrices.Count == submittedRowCount &&
                   invalidModel.Product.RetailPrices[0].SizeMl == submittedSize &&
                   invalidModel.Product.RetailPrices[0].Price == submittedPrice,
                $"Product {productId} invalid Edit POST did not preserve the submitted retail values.");
            Assert(!controller.ModelState.IsValid,
                $"Product {productId} invalid Edit POST lost its validation errors.");
            var serverErrors = controller.ModelState.Values
                .SelectMany(entry => entry.Errors)
                .Select(error => error.ErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToList();
            Assert(serverErrors.Count > 0 && serverErrors.All(IsArabicUserMessage),
                $"Product {productId} exposed a non-Arabic server validation message.");
            Assert(serverErrors.Contains(numericBindingError, StringComparer.Ordinal),
                $"Product {productId} lost the Arabic numeric model-binding error.");
            if (checksStoredSizeConflict)
            {
                Assert(controller.ModelState.ContainsKey("Product.RetailPrices[0].SizeMl"),
                    $"Product {productId} stored-size conflict was not attached to the submitted size field.");
            }
            Console.WriteLine($"PASS product {productId}: invalid retail Edit POST remained in-page and preserved submitted values/errors.");
        }
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count += 1;
            index += value.Length;
        }
        return count;
    }

    private static bool IsArabicUserMessage(string? message) =>
        !string.IsNullOrWhiteSpace(message) &&
        !message.Any(character => character is >= 'A' and <= 'Z' or >= 'a' and <= 'z');

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class EmptyTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }

    private sealed class RenderingRouter : IRouter
    {
        public VirtualPathData GetVirtualPath(VirtualPathContext context) => new(this, "/test-route");
        public Task RouteAsync(RouteContext context) => Task.CompletedTask;
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = string.Empty;
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
