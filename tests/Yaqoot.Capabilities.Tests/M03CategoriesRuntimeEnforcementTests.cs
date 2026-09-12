using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using YAGOT_2._0.Core.Capabilities;
using YAGOT_2._0.Integration.Capabilities;
using YAGOT_2._0.Models;
using YAGOT_2._0.Services.Integration;
using AdminCategoriesController = YAGOT_2._0.Areas.Admin.Controllers.CategoriesController;
using StorefrontCategoriesController = YAGOT_2._0.Controllers.CategoriesController;
using StorefrontProductsController = YAGOT_2._0.Controllers.ProductsController;
using ImagesController = YAGOT_2._0.Controllers.ImagesController;
using Xunit;

namespace Yaqoot.Capabilities.Tests;

public sealed class M03CategoriesRuntimeEnforcementTests
{
    private static readonly string[] M03Features =
    [
        CapabilityFeatureCodes.CategoryView,
        CapabilityFeatureCodes.CategoryCreate,
        CapabilityFeatureCodes.CategoryEdit,
        CapabilityFeatureCodes.CategoryDelete,
        CapabilityFeatureCodes.CategoryImages,
        CapabilityFeatureCodes.CategoryProductsView
    ];

    private readonly CapabilityCatalog _catalog = new();

    [Fact]
    public void EnabledModule_AllowsEachEnabledM03Feature()
    {
        var evaluator = Evaluator(Snapshot());

        Assert.True(evaluator.IsModuleEnabled(CapabilityModuleCodes.Categories));
        Assert.All(M03Features, code => Assert.True(evaluator.IsFeatureEnabled(code)));
    }

    [Fact]
    public void DisabledModule_DisablesAllSixM03Features()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));

        Assert.False(evaluator.IsModuleEnabled(CapabilityModuleCodes.Categories));
        Assert.All(M03Features, code =>
            Assert.Equal(CapabilityEvaluationReason.ModuleDisabled, evaluator.EvaluateFeature(code).Reason));
    }

    [Theory]
    [MemberData(nameof(IndependentFeatureCases))]
    public void DisabledFeature_DoesNotDisableOtherM03Features(string disabledFeature, string enabledFeature)
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: disabledFeature));

        Assert.Equal(CapabilityEvaluationReason.FeatureDisabled, evaluator.EvaluateFeature(disabledFeature).Reason);
        Assert.True(evaluator.IsFeatureEnabled(enabledFeature));
    }

    [Fact]
    public async Task ViewCategoriesOff_BlocksDirectStorefrontRequest()
    {
        var controller = new StorefrontCategoriesController(null!, Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CategoryView)));

        Assert.IsType<NotFoundResult>(await controller.Index());
    }

    [Fact]
    public void CreateCategoriesOff_BlocksDirectAdminGetRequest()
    {
        var controller = AdminController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CategoryCreate)));

        Assert.IsType<ForbidResult>(controller.Create());
    }

    [Fact]
    public async Task CreateCategoriesOff_BlocksDirectAdminPostRequest()
    {
        var controller = AdminController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CategoryCreate)));

        Assert.IsType<ForbidResult>(await controller.Create(new CategoryVW { Name = "blocked" }));
    }

    [Fact]
    public async Task UpdateCategoriesOff_BlocksDirectAdminRequests()
    {
        var controller = AdminController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CategoryEdit)));

        Assert.IsType<ForbidResult>(await controller.Edit(1));
        Assert.IsType<ForbidResult>(await controller.Edit(new CategoryVW { Id = 1, Name = "blocked" }));
    }

    [Fact]
    public async Task DeleteCategoriesOff_BlocksDirectAdminPostRequest()
    {
        var controller = AdminController(Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CategoryDelete)));

        Assert.IsType<ForbidResult>(await controller.Delete(1));
    }

    [Fact]
    public async Task CategoryImagesOff_BlocksCreateAndEditImageMutationsWithoutBlockingTheirFeatures()
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CategoryImages));
        var controller = AdminController(evaluator);
        var upload = new FormFile(Stream.Null, 0, 1, "ImageFile", "category.png");

        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CategoryCreate));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CategoryEdit));
        Assert.IsType<ForbidResult>(await controller.Create(new CategoryVW { Name = "category", ImageFile = upload }));
        Assert.IsType<ForbidResult>(await controller.Edit(new CategoryVW { Id = 1, Name = "category", ImageFile = upload }));
    }

    [Fact]
    public void CategoryImagesOff_BlocksDirectCategoryImageRequest()
    {
        var controller = new ImagesController(null!, Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CategoryImages)));

        Assert.IsType<NotFoundResult>(controller.Get("categories", "category.webp"));
    }

    [Fact]
    public async Task ProductsByCategoryOff_BlocksCategoryFilterButNotNormalProductCapability()
    {
        var evaluator = Evaluator(Snapshot(disabledFeature: CapabilityFeatureCodes.CategoryProductsView));
        var controller = new StorefrontProductsController(null!, null!, evaluator);

        var result = await controller.Index(new ProductsCatalogRequest { CategoryId = 12 }, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.CategoryView));
        Assert.True(evaluator.IsFeatureEnabled(CapabilityFeatureCodes.ProductView));
    }

    [Fact]
    public async Task DisabledModule_BlocksEachM03ControllerBoundary()
    {
        var evaluator = Evaluator(Snapshot(moduleEnabled: false));
        var admin = AdminController(evaluator);

        Assert.IsType<NotFoundResult>(await new StorefrontCategoriesController(null!, evaluator).Index());
        Assert.IsType<NotFoundResult>(await new StorefrontProductsController(null!, null!, evaluator)
            .Index(new ProductsCatalogRequest { CategoryId = 3 }, CancellationToken.None));
        Assert.IsType<ForbidResult>(admin.Create());
        Assert.IsType<ForbidResult>(await admin.Create(new CategoryVW { Name = "blocked" }));
        Assert.IsType<ForbidResult>(await admin.Edit(3));
        Assert.IsType<ForbidResult>(await admin.Edit(new CategoryVW { Id = 3, Name = "blocked" }));
        Assert.IsType<ForbidResult>(await admin.Delete(3));
    }

    [Fact]
    public async Task RuntimePublication_IsObservedWithoutRecreatingController()
    {
        var runtime = Runtime();
        runtime.Publish(Snapshot(revision: 1));
        var controller = new StorefrontCategoriesController(null!, new CapabilityEvaluator(_catalog, runtime));

        runtime.Publish(Snapshot(revision: 2, disabledFeature: CapabilityFeatureCodes.CategoryView));

        Assert.IsType<NotFoundResult>(await controller.Index());
    }

    [Fact]
    public void RazorUi_UsesM03CapabilitiesForRelevantControls()
    {
        var root = FindSolutionRoot();

        Assert.Contains("CategoryCreate", File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Categories", "Index.cshtml")));
        Assert.Contains("CategoryEdit", File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Categories", "Index.cshtml")));
        Assert.Contains("CategoryDelete", File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Categories", "Index.cshtml")));
        Assert.Contains("CategoryImages", File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Categories", "Create.cshtml")));
        Assert.Contains("CategoryImages", File.ReadAllText(Path.Combine(root, "Areas", "Admin", "Views", "Categories", "Edit.cshtml")));
        Assert.Contains("CategoryView", File.ReadAllText(Path.Combine(root, "Views", "Shared", "_StorefrontHeader.cshtml")));
        Assert.Contains("CategoryProductsView", File.ReadAllText(Path.Combine(root, "Views", "Products", "Index.cshtml")));
    }

    public static TheoryData<string, string> IndependentFeatureCases => new()
    {
        { CapabilityFeatureCodes.CategoryCreate, CapabilityFeatureCodes.CategoryView },
        { CapabilityFeatureCodes.CategoryEdit, CapabilityFeatureCodes.CategoryDelete },
        { CapabilityFeatureCodes.CategoryImages, CapabilityFeatureCodes.CategoryView }
    };

    private AdminCategoriesController AdminController(ICapabilityEvaluator evaluator) =>
        new(null!, null!, null!, null!, null!, evaluator);

    private CapabilityEvaluator Evaluator(CapabilitySnapshotV1 snapshot)
    {
        var runtime = Runtime();
        runtime.Publish(snapshot);
        return new CapabilityEvaluator(_catalog, runtime);
    }

    private LocalCapabilityRuntimeStateProvider Runtime() => new(_catalog, TimeProvider.System);

    private CapabilitySnapshotV1 Snapshot(
        long revision = 1,
        bool moduleEnabled = true,
        string? disabledFeature = null)
    {
        var now = new DateTimeOffset(2026, 9, 12, 0, 0, 0, TimeSpan.Zero);
        return new CapabilitySnapshotV1(
            CapabilityContractV1.ContractVersion,
            CapabilityContractV1.CatalogVersion,
            1,
            revision,
            now,
            now,
            _catalog.Modules.Select(module => new CapabilityModuleStateV1(
                module.Code,
                module.Code != CapabilityModuleCodes.Categories || moduleEnabled)).ToArray(),
            _catalog.Features.Select(feature => new CapabilityFeatureStateV1(
                feature.Code,
                feature.ModuleCode,
                !string.Equals(feature.Code, disabledFeature, StringComparison.Ordinal))).ToArray());
    }

    private static string FindSolutionRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "YAGOT_2.0.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new DirectoryNotFoundException("Could not locate the Yaqoot solution root.");
    }
}
