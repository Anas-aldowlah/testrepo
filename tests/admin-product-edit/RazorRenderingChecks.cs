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
using Npgsql;
using Yagot.Areas.Admin.Controllers;
using YAGOT_2._0.Models;
using YAGOT_2._0.Models.Admin;
using YAGOT_2._0.Services;

internal static class RazorRenderingChecks
{
    private const string ConnectionVariable = "YAGOT_ADMIN_EDIT_RENDER_CONNECTION";
    private const string PersistenceConnectionVariable = "YAGOT_ADMIN_PRODUCT_PERSISTENCE_CONNECTION";
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

        var persistenceConnection = Environment.GetEnvironmentVariable(PersistenceConnectionVariable);
        if (string.IsNullOrWhiteSpace(persistenceConnection))
        {
            Console.WriteLine("SKIP: persistence action checks require the separately approved disposable loopback connection.");
            return;
        }

        AssertDisposablePersistenceBoundary(persistenceConnection);
        var persistenceOptions = new DbContextOptionsBuilder<NeondbContext>()
            .UseNpgsql(persistenceConnection)
            .Options;
        await RunPersistenceActionChecksAsync(services, persistenceOptions);
    }

    private static void AssertDisposablePersistenceBoundary(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var expectedPassfile = Path.GetFullPath(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "YAGOT",
            "PerformanceCredentials",
            "pgpass.conf"));
        var actualPassfile = string.IsNullOrWhiteSpace(builder.Passfile) ? string.Empty : Path.GetFullPath(builder.Passfile);
        var isLoopback = string.Equals(builder.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(builder.Host, "localhost", StringComparison.OrdinalIgnoreCase);

        Assert(isLoopback, "Persistence checks refused a non-loopback PostgreSQL host.");
        Assert(builder.Port == 5432, "Persistence checks refused an unexpected PostgreSQL port.");
        Assert(string.Equals(builder.Database, "yagot_performance_main", StringComparison.Ordinal),
            "Persistence checks refused a non-disposable database.");
        Assert(string.Equals(builder.Username, "yagot_performance_app", StringComparison.Ordinal),
            "Persistence checks refused an unexpected database role.");
        Assert(string.IsNullOrEmpty(builder.Password), "Persistence checks refuse inline database credentials.");
        Assert(string.Equals(actualPassfile, expectedPassfile, StringComparison.OrdinalIgnoreCase),
            "Persistence checks refused an unapproved passfile.");
    }

    private static async Task RunPersistenceActionChecksAsync(
        IServiceProvider services,
        DbContextOptions<NeondbContext> options)
    {
        await using var database = new NeondbContext(options);
        await using var transaction = await database.Database.BeginTransactionAsync();
        var categoryId = await database.Categories.AsNoTracking().Select(category => category.Id).FirstAsync();

        var emptyCreate = CreateInput(categoryId, retailEnabled: true);
        var emptyCreateController = BuildController(services, database, "Create");
        var emptyCreateResult = await emptyCreateController.Create(emptyCreate);
        AssertInvalidForm(emptyCreateController, emptyCreateResult, "Create", "RetailPrices", "Create retail ON with zero rows");
        Assert(!emptyCreateController.TempData.ContainsKey("Success"), "Invalid Create exposed success feedback.");

        var invalidCreate = CreateInput(
            categoryId,
            retailEnabled: true,
            new AdminProductCreateRetailPriceInput { SizeMl = 25, Price = 0 });
        var invalidCreateController = BuildController(services, database, "Create");
        var invalidCreateResult = await invalidCreateController.Create(invalidCreate);
        AssertInvalidForm(invalidCreateController, invalidCreateResult, "Create", "RetailPrices[0].Price", "Create invalid retail price");

        var retailOffName = $"QA retail off {Guid.NewGuid():N}";
        var retailOffCreate = CreateInput(categoryId, retailEnabled: false, name: retailOffName);
        var retailOffController = BuildController(services, database, "Create");
        var retailOffResult = await retailOffController.Create(retailOffCreate);
        AssertSuccessRedirect(retailOffController, retailOffResult, "تمت إضافة المنتج بنجاح.", "Create retail OFF", "Index");
        Assert(await database.Products.AnyAsync(product => product.Name == retailOffName && !product.IsRetailEnabled),
            "Create retail OFF did not persist inside the disposable transaction.");

        var validRetailName = $"QA retail valid {Guid.NewGuid():N}";
        var validRetailCreate = CreateInput(
            categoryId,
            retailEnabled: true,
            new AdminProductCreateRetailPriceInput { SizeMl = 25, Price = 5 },
            name: validRetailName);
        var validRetailController = BuildController(services, database, "Create");
        var validRetailResult = await validRetailController.Create(validRetailCreate);
        AssertSuccessRedirect(validRetailController, validRetailResult, "تمت إضافة المنتج بنجاح.", "Create valid retail row", "Index");
        Assert(await database.Products.AnyAsync(product =>
                product.Name == validRetailName && product.IsRetailEnabled && product.RetailPrices.Any(price => price.SizeMl == 25 && price.Price == 5)),
            "Create valid retail row did not persist inside the disposable transaction.");

        var retailProduct = await database.Products
            .Include(product => product.RetailPrices)
            .FirstAsync(product => product.Name == validRetailName);

        var emptyEdit = ProductInput(retailProduct);
        emptyEdit.RetailPrices = [];
        var emptyEditController = BuildController(services, database, "Edit");
        var emptyEditResult = await emptyEditController.Edit(emptyEdit);
        AssertInvalidForm(emptyEditController, emptyEditResult, "Edit", "Product.RetailPrices", "Edit retail ON with zero rows");
        Assert(!emptyEditController.TempData.ContainsKey("Success"), "Invalid Edit exposed success feedback.");
        Assert(((ViewResult)emptyEditResult).Model is AdminProductEditViewModel emptyEditModel && emptyEditModel.Product.RetailPrices.Count == 0,
            "Invalid Edit did not preserve the submitted empty retail row list.");

        var invalidEdit = ProductInput(retailProduct);
        invalidEdit.RetailPrices[0].SizeMl = 0;
        var invalidEditController = BuildController(services, database, "Edit");
        var invalidEditResult = await invalidEditController.Edit(invalidEdit);
        AssertInvalidForm(invalidEditController, invalidEditResult, "Edit", "Product.RetailPrices[0].SizeMl", "Edit invalid retail size");

        var originalPrice = retailProduct.RetailPrices.Single();
        var addNewPrice = ProductInput(retailProduct);
        addNewPrice.RetailPrices =
        [
            new ProductRetailPriceInput { SizeMl = originalPrice.SizeMl, Price = 7, IsActive = true },
            new ProductRetailPriceInput { Id = originalPrice.Id, SizeMl = 30, Price = 6, IsActive = true }
        ];
        var addNewPriceController = BuildController(services, database, "Edit");
        var addNewPriceResult = await addNewPriceController.Edit(addNewPrice);
        AssertSuccessRedirect(addNewPriceController, addNewPriceResult, "تم حفظ تعديلات المنتج بنجاح.",
            "Edit adds an ID-less row before its former-size existing row", "Edit", retailProduct.Id);

        database.ChangeTracker.Clear();
        retailProduct = await database.Products.Include(product => product.RetailPrices)
            .SingleAsync(product => product.Id == retailProduct.Id);
        var newlyAdded = retailProduct.RetailPrices.SingleOrDefault(price =>
            price.Id != originalPrice.Id && price.SizeMl == 25 && price.Price == 7 && price.IsActive);
        Assert(newlyAdded != null, "ID-authoritative sync did not persist the exact new X/Y/active values.");
        var newlyAddedId = newlyAdded?.Id
            ?? throw new InvalidOperationException("ID-authoritative sync did not return the new retail row identity.");
        Assert(retailProduct.RetailPrices.Any(price => price.Id == originalPrice.Id && price.SizeMl == 30 && price.Price == 6),
            "ID-authoritative sync did not preserve and update the existing row independently.");

        var editExisting = ProductInput(retailProduct);
        var editedInput = editExisting.RetailPrices.Single(price => price.Id == newlyAddedId);
        editedInput.SizeMl = 35;
        editedInput.Price = 8;
        editedInput.IsActive = false;
        var editExistingController = BuildController(services, database, "Edit");
        var editExistingResult = await editExistingController.Edit(editExisting);
        AssertSuccessRedirect(editExistingController, editExistingResult, "تم حفظ تعديلات المنتج بنجاح.",
            "Edit existing retail row", "Edit", retailProduct.Id);

        database.ChangeTracker.Clear();
        retailProduct = await database.Products.Include(product => product.RetailPrices)
            .SingleAsync(product => product.Id == retailProduct.Id);
        Assert(retailProduct.RetailPrices.Any(price => price.Id == newlyAddedId && price.SizeMl == 35 && price.Price == 8 && !price.IsActive),
            "Existing retail row did not reload with the exact edited X/Y/active values.");

        var deleteUnused = ProductInput(retailProduct);
        deleteUnused.RetailPrices.RemoveAll(price => price.Id == newlyAddedId);
        var deleteUnusedController = BuildController(services, database, "Edit");
        var deleteUnusedResult = await deleteUnusedController.Edit(deleteUnused);
        AssertSuccessRedirect(deleteUnusedController, deleteUnusedResult, "تم حفظ تعديلات المنتج بنجاح.",
            "Delete unused retail row", "Edit", retailProduct.Id);
        database.ChangeTracker.Clear();
        Assert(!await database.ProductRetailPrices.AnyAsync(price => price.Id == newlyAddedId),
            "Unused retail price was not physically deleted.");

        retailProduct = await database.Products.Include(product => product.RetailPrices)
            .SingleAsync(product => product.Id == retailProduct.Id);
        var addHistoricalTarget = ProductInput(retailProduct);
        addHistoricalTarget.RetailPrices.Add(new ProductRetailPriceInput { SizeMl = 40, Price = 9, IsActive = true });
        var addHistoricalController = BuildController(services, database, "Edit");
        var addHistoricalResult = await addHistoricalController.Edit(addHistoricalTarget);
        AssertSuccessRedirect(addHistoricalController, addHistoricalResult, "تم حفظ تعديلات المنتج بنجاح.",
            "Add historically used target", "Edit", retailProduct.Id);

        database.ChangeTracker.Clear();
        retailProduct = await database.Products.Include(product => product.RetailPrices)
            .SingleAsync(product => product.Id == retailProduct.Id);
        var historicalTarget = retailProduct.RetailPrices.Single(price => price.SizeMl == 40 && price.Price == 9);
        var localTimestamp = DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified);
        var salesDay = new SalesDay { Date = localTimestamp.Date, Status = "Closed", CreatedAt = localTimestamp };
        var completedSale = new Sale
        {
            SalesDay = salesDay,
            Status = "Completed",
            TotalAmount = historicalTarget.Price,
            FinalAmount = historicalTarget.Price,
            CreatedAt = localTimestamp,
            CompletedAt = localTimestamp
        };
        var historicalSaleItem = new SaleItem
        {
            Sale = completedSale,
            ProductId = retailProduct.Id,
            RetailPriceId = historicalTarget.Id,
            RetailSizeMl = historicalTarget.SizeMl,
            ProductName = retailProduct.Name,
            Quantity = 1,
            UnitPrice = historicalTarget.Price,
            Total = historicalTarget.Price
        };
        database.SaleItems.Add(historicalSaleItem);
        await database.SaveChangesAsync();

        var deleteHistorical = ProductInput(retailProduct);
        deleteHistorical.RetailPrices.RemoveAll(price => price.Id == historicalTarget.Id);
        var deleteHistoricalController = BuildController(services, database, "Edit");
        var deleteHistoricalResult = await deleteHistoricalController.Edit(deleteHistorical);
        AssertSuccessRedirect(deleteHistoricalController, deleteHistoricalResult, "تم حفظ تعديلات المنتج بنجاح.",
            "Delete historically used retail row", "Edit", retailProduct.Id);
        Assert(string.Equals(deleteHistoricalController.TempData["Warning"]?.ToString(),
                "هذا السعر مرتبط بعمليات بيع سابقة، لذلك تم إيقافه بدل حذفه للحفاظ على سجل المبيعات.",
                StringComparison.Ordinal),
            "Historically used deletion did not expose the exact Arabic warning.");

        database.ChangeTracker.Clear();
        var retainedHistorical = await database.ProductRetailPrices.SingleAsync(price => price.Id == historicalTarget.Id);
        Assert(!retainedHistorical.IsActive, "Historically used retail price was not retained as inactive.");
        Assert(await database.SaleItems.AnyAsync(item => item.Id == historicalSaleItem.Id && item.RetailPriceId == historicalTarget.Id),
            "Historically used retail relationship was not preserved.");

        retailProduct = await database.Products.Include(product => product.RetailPrices)
            .SingleAsync(product => product.Id == retailProduct.Id);

        var allInactive = ProductInput(retailProduct);
        allInactive.IsRetailEnabled = false;
        allInactive.RetailPrices.ForEach(price => price.IsActive = false);
        var allInactiveController = BuildController(services, database, "Edit");
        var allInactiveResult = await allInactiveController.Edit(allInactive);
        AssertSuccessRedirect(allInactiveController, allInactiveResult, "تم حفظ تعديلات المنتج بنجاح.",
            "Retail OFF with two inactive rows", "Edit", retailProduct.Id);
        database.ChangeTracker.Clear();
        retailProduct = await database.Products.Include(product => product.RetailPrices)
            .SingleAsync(product => product.Id == retailProduct.Id);
        Assert(!retailProduct.IsRetailEnabled && retailProduct.RetailPrices.All(price => !price.IsActive),
            "Retail OFF did not preserve two submitted inactive rows after reload.");

        var mixedActivity = ProductInput(retailProduct);
        mixedActivity.IsRetailEnabled = false;
        var orderedRows = mixedActivity.RetailPrices.OrderBy(price => price.Id).ToList();
        Assert(orderedRows.Count >= 2, "Retail OFF mixed-state check requires two persisted rows.");
        orderedRows[0].IsActive = false;
        orderedRows[1].IsActive = true;
        var expectedActivity = orderedRows.ToDictionary(price => price.Id!.Value, price => price.IsActive);
        var mixedActivityController = BuildController(services, database, "Edit");
        var mixedActivityResult = await mixedActivityController.Edit(mixedActivity);
        AssertSuccessRedirect(mixedActivityController, mixedActivityResult, "تم حفظ تعديلات المنتج بنجاح.",
            "Retail OFF with mixed row activity", "Edit", retailProduct.Id);
        database.ChangeTracker.Clear();
        retailProduct = await database.Products.Include(product => product.RetailPrices)
            .SingleAsync(product => product.Id == retailProduct.Id);
        Assert(!retailProduct.IsRetailEnabled &&
               retailProduct.RetailPrices.Count == expectedActivity.Count &&
               retailProduct.RetailPrices.All(price => expectedActivity[price.Id] == price.IsActive),
            "Retail OFF did not preserve the exact submitted mixed activity after reload.");

        await transaction.RollbackAsync();
        Console.WriteLine("PASS: disposable local DB ID-authoritative sync, Retail OFF exact activity reload, unused delete, historical deactivate, PRG feedback, and validation checks (rolled back).");
    }

    private static AdminProductCreateViewModel CreateInput(
        int categoryId,
        bool retailEnabled,
        AdminProductCreateRetailPriceInput? row = null,
        string? name = null) => new()
    {
        Categoryid = categoryId,
        Name = name ?? $"QA invalid {Guid.NewGuid():N}",
        Price = 10,
        Stockquantity = 1,
        StockUnit = "Ml",
        VolumeMl = 100,
        IsRetailEnabled = retailEnabled,
        RetailPrices = row == null ? [] : [row]
    };

    private static ProductVW ProductInput(Product product) => new()
    {
        Id = product.Id,
        Categoryid = product.Categoryid,
        Name = product.Name,
        Description = product.Description,
        Price = product.Price,
        Stockquantity = product.Stockquantity,
        StockUnit = product.StockUnit,
        VolumeMl = product.VolumeMl,
        IsRetailEnabled = product.IsRetailEnabled,
        Existingimage = product.Imageurl,
        Brand = product.Brand,
        RetailPrices = product.RetailPrices.Select(price => new ProductRetailPriceInput
        {
            Id = price.Id,
            SizeMl = price.SizeMl,
            Price = price.Price,
            IsActive = price.IsActive
        }).ToList()
    };

    private static ProductsController BuildController(IServiceProvider services, NeondbContext database, string action)
    {
        var routeData = new RouteData();
        routeData.Values["area"] = "Admin";
        routeData.Values["controller"] = "Products";
        routeData.Values["action"] = action;
        routeData.Routers.Add(new RenderingRouter());
        var actionContext = new ActionContext(
            new DefaultHttpContext { RequestServices = services },
            routeData,
            new ControllerActionDescriptor
            {
                ControllerName = "Products",
                ActionName = action,
                ControllerTypeInfo = typeof(ProductsController).GetTypeInfo()
            });
        var environment = services.GetRequiredService<IWebHostEnvironment>();
        return new ProductsController(
            new ProductService(database),
            database,
            environment,
            new Image(environment),
            new InventoryService(database),
            NullLogger<ProductsController>.Instance)
        {
            ControllerContext = new ControllerContext(actionContext)
        };
    }

    private static void AssertInvalidForm(
        ProductsController controller,
        IActionResult result,
        string expectedView,
        string expectedKey,
        string scenario)
    {
        Assert(result is ViewResult view && string.Equals(view.ViewName, expectedView, StringComparison.Ordinal),
            $"{scenario}: expected the same {expectedView} form.");
        Assert(controller.ModelState.TryGetValue(expectedKey, out var entry) && entry.Errors.Count > 0,
            $"{scenario}: expected ModelState error at {expectedKey}.");
    }

    private static void AssertSuccessRedirect(
        ProductsController controller,
        IActionResult result,
        string expectedMessage,
        string scenario,
        string expectedAction,
        int? expectedId = null)
    {
        var redirect = result as RedirectToActionResult;
        Assert(redirect != null && string.Equals(redirect.ActionName, expectedAction, StringComparison.Ordinal),
            $"{scenario}: expected {expectedAction} redirect after persistence.");
        if (expectedId.HasValue)
        {
            Assert(redirect!.RouteValues != null && Convert.ToInt32(redirect.RouteValues["id"]) == expectedId.Value,
                $"{scenario}: expected redirect to the same product ID.");
        }
        Assert(string.Equals(controller.TempData["Success"]?.ToString(), expectedMessage, StringComparison.Ordinal),
            $"{scenario}: expected exact success feedback.");
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
        var renderedRetailRows = Regex.Matches(
            html,
            "class=\"[^\"]*\\byq-retail-price-row\\b[^\"]*\"",
            RegexOptions.CultureInvariant).Count;
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
