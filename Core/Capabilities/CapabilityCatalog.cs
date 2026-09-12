using System.Collections.ObjectModel;

namespace YAGOT_2._0.Core.Capabilities;

public sealed class CapabilityCatalog : ICapabilityCatalog
{
    private readonly IReadOnlyDictionary<string, CapabilityModule> _modulesByCode;
    private readonly IReadOnlyDictionary<string, CapabilityFeature> _featuresByCode;

    public CapabilityCatalog()
    {
        Features = new ReadOnlyCollection<CapabilityFeature>(CreateFeatures());
        Modules = new ReadOnlyCollection<CapabilityModule>(CreateModules(Features));
        _modulesByCode = new ReadOnlyDictionary<string, CapabilityModule>(Modules.ToDictionary(module => module.Code, StringComparer.Ordinal));
        _featuresByCode = new ReadOnlyDictionary<string, CapabilityFeature>(Features.ToDictionary(feature => feature.Code, StringComparer.Ordinal));
        ValidateCatalog();
    }

    public IReadOnlyList<CapabilityModule> Modules { get; }
    public IReadOnlyList<CapabilityFeature> Features { get; }

    public bool TryGetModule(string moduleCode, out CapabilityModule module) =>
        _modulesByCode.TryGetValue(moduleCode, out module!);

    public bool TryGetFeature(string featureCode, out CapabilityFeature feature) =>
        _featuresByCode.TryGetValue(featureCode, out feature!);

    private void ValidateCatalog()
    {
        if (Modules.Count != 8 || Features.Count != 51)
        {
            throw new InvalidOperationException("The capability catalog must contain exactly 8 modules and 51 features.");
        }

        foreach (var module in Modules)
        {
            if (module.FeatureCodes.Count == 0 || module.FeatureCodes.Distinct(StringComparer.Ordinal).Count() != module.FeatureCodes.Count)
            {
                throw new InvalidOperationException($"Module '{module.Code}' has invalid feature references.");
            }

            foreach (var featureCode in module.FeatureCodes)
            {
                if (!_featuresByCode.TryGetValue(featureCode, out var feature) || feature.ModuleCode != module.Code)
                {
                    throw new InvalidOperationException($"Feature '{featureCode}' is not consistently assigned to module '{module.Code}'.");
                }
            }
        }

        foreach (var feature in Features)
        {
            if (!_modulesByCode.TryGetValue(feature.ModuleCode, out var module) || !module.FeatureCodes.Contains(feature.Code, StringComparer.Ordinal))
            {
                throw new InvalidOperationException($"Feature '{feature.Code}' has no valid module assignment.");
            }
        }
    }

    private static List<CapabilityModule> CreateModules(IReadOnlyList<CapabilityFeature> features)
    {
        CapabilityModule Module(string code, string name, CapabilityClassification classification, CapabilityImplementationStatus status) =>
            new(code, name, classification, status,
                Array.AsReadOnly(features.Where(feature => feature.ModuleCode == code).Select(feature => feature.Code).ToArray()));

        return
        [
            Module(CapabilityModuleCodes.Core, "Core", CapabilityClassification.Core, CapabilityImplementationStatus.Core),
            Module(CapabilityModuleCodes.StoreManagement, "Store Management", CapabilityClassification.Business, CapabilityImplementationStatus.Implemented),
            Module(CapabilityModuleCodes.Categories, "Categories", CapabilityClassification.Business, CapabilityImplementationStatus.Implemented),
            Module(CapabilityModuleCodes.ProductsInventory, "Products & Inventory", CapabilityClassification.Business, CapabilityImplementationStatus.Implemented),
            Module(CapabilityModuleCodes.Offers, "Offers", CapabilityClassification.Business, CapabilityImplementationStatus.NotImplemented),
            Module(CapabilityModuleCodes.CustomerAccounts, "Customer Accounts", CapabilityClassification.Business, CapabilityImplementationStatus.Implemented),
            Module(CapabilityModuleCodes.MarketingOrders, "Marketing & Orders", CapabilityClassification.Business, CapabilityImplementationStatus.Implemented),
            Module(CapabilityModuleCodes.SalesPos, "Sales & POS", CapabilityClassification.Business, CapabilityImplementationStatus.Implemented)
        ];
    }

    private static List<CapabilityFeature> CreateFeatures()
    {
        var features = new List<CapabilityFeature>(51);
        void Core(string code, string name) => features.Add(new(code, CapabilityModuleCodes.Core, name,
            CapabilityClassification.Core, CapabilityImplementationStatus.Core, SupportsIndependentRuntimeDisablement: false));
        void Implemented(string code, string moduleCode, string name) => features.Add(new(code, moduleCode, name,
            CapabilityClassification.Business, CapabilityImplementationStatus.Implemented, SupportsIndependentRuntimeDisablement: false));
        void NotImplemented(string code, string moduleCode, string name) => features.Add(new(code, moduleCode, name,
            CapabilityClassification.Business, CapabilityImplementationStatus.NotImplemented, SupportsIndependentRuntimeDisablement: false));

        Core(CapabilityFeatureCodes.CoreAuthentication, "Authentication");
        Core(CapabilityFeatureCodes.CoreFileMedia, "File & Media Infrastructure");
        Core(CapabilityFeatureCodes.CoreDatabaseData, "Database & Data Infrastructure");
        Core(CapabilityFeatureCodes.CoreSharedServices, "Shared Services");
        Core(CapabilityFeatureCodes.CoreSystemConfiguration, "System Configuration");
        Core(CapabilityFeatureCodes.CoreSystemSecurity, "System Security");
        Core(CapabilityFeatureCodes.CoreErrorHandling, "Error Handling");
        Core(CapabilityFeatureCodes.CoreSystemFoundation, "System Foundation");

        Implemented(CapabilityFeatureCodes.StoreDashboard, CapabilityModuleCodes.StoreManagement, "Store Dashboard");
        Implemented(CapabilityFeatureCodes.StoreUserEmployeeManagement, CapabilityModuleCodes.StoreManagement, "User & Employee Management");
        Implemented(CapabilityFeatureCodes.StoreRolesPermissions, CapabilityModuleCodes.StoreManagement, "Roles & Permissions");
        Implemented(CapabilityFeatureCodes.StoreSettings, CapabilityModuleCodes.StoreManagement, "Store Settings");
        Implemented(CapabilityFeatureCodes.CategoryView, CapabilityModuleCodes.Categories, "View Categories");
        Implemented(CapabilityFeatureCodes.CategoryCreate, CapabilityModuleCodes.Categories, "Create Categories");
        Implemented(CapabilityFeatureCodes.CategoryEdit, CapabilityModuleCodes.Categories, "Edit Categories");
        Implemented(CapabilityFeatureCodes.CategoryDelete, CapabilityModuleCodes.Categories, "Delete Categories");
        Implemented(CapabilityFeatureCodes.CategoryImages, CapabilityModuleCodes.Categories, "Category Images");
        Implemented(CapabilityFeatureCodes.CategoryProductsView, CapabilityModuleCodes.Categories, "View Category Products");
        Implemented(CapabilityFeatureCodes.ProductCreate, CapabilityModuleCodes.ProductsInventory, "Create Products");
        Implemented(CapabilityFeatureCodes.ProductView, CapabilityModuleCodes.ProductsInventory, "View Products");
        Implemented(CapabilityFeatureCodes.ProductEdit, CapabilityModuleCodes.ProductsInventory, "Edit Products");
        Implemented(CapabilityFeatureCodes.ProductDelete, CapabilityModuleCodes.ProductsInventory, "Delete Products");
        Implemented(CapabilityFeatureCodes.ProductBrands, CapabilityModuleCodes.ProductsInventory, "Product Brands");
        Implemented(CapabilityFeatureCodes.ProductSearchFilter, CapabilityModuleCodes.ProductsInventory, "Product Search & Filter");
        Implemented(CapabilityFeatureCodes.InventoryManagement, CapabilityModuleCodes.ProductsInventory, "Inventory Management");
        Implemented(CapabilityFeatureCodes.RetailSelling, CapabilityModuleCodes.ProductsInventory, "Retail Selling");

        NotImplemented(CapabilityFeatureCodes.OfferDuration, CapabilityModuleCodes.Offers, "Offer Duration");
        NotImplemented(CapabilityFeatureCodes.OfferManagement, CapabilityModuleCodes.Offers, "Offer Management");
        NotImplemented(CapabilityFeatureCodes.OfferStatus, CapabilityModuleCodes.Offers, "Offer Status");
        NotImplemented(CapabilityFeatureCodes.OfferProducts, CapabilityModuleCodes.Offers, "Offer Products");
        NotImplemented(CapabilityFeatureCodes.OfferCustomerDisplay, CapabilityModuleCodes.Offers, "Customer Offer Display");
        NotImplemented(CapabilityFeatureCodes.OfferDiscountPricing, CapabilityModuleCodes.Offers, "Offer Discount Pricing");

        Implemented(CapabilityFeatureCodes.CustomerAccountCreate, CapabilityModuleCodes.CustomerAccounts, "Create Customer Account");
        Implemented(CapabilityFeatureCodes.CustomerLogin, CapabilityModuleCodes.CustomerAccounts, "Customer Login");
        Implemented(CapabilityFeatureCodes.CustomerGoogleLogin, CapabilityModuleCodes.CustomerAccounts, "Customer Google Login");
        Implemented(CapabilityFeatureCodes.CustomerProfile, CapabilityModuleCodes.CustomerAccounts, "Customer Profile");
        Implemented(CapabilityFeatureCodes.CustomerAccountRecovery, CapabilityModuleCodes.CustomerAccounts, "Customer Account Recovery");
        Implemented(CapabilityFeatureCodes.CartProductManagement, CapabilityModuleCodes.MarketingOrders, "Cart Product Management");
        Implemented(CapabilityFeatureCodes.CartView, CapabilityModuleCodes.MarketingOrders, "View Cart");
        Implemented(CapabilityFeatureCodes.OrderCreate, CapabilityModuleCodes.MarketingOrders, "Create Orders");
        Implemented(CapabilityFeatureCodes.Checkout, CapabilityModuleCodes.MarketingOrders, "Checkout");
        Implemented(CapabilityFeatureCodes.OrderManagement, CapabilityModuleCodes.MarketingOrders, "Order Management");
        Implemented(CapabilityFeatureCodes.OrderPaymentProof, CapabilityModuleCodes.MarketingOrders, "Order Payment Proof");
        Implemented(CapabilityFeatureCodes.PosCashier, CapabilityModuleCodes.SalesPos, "POS Cashier");
        Implemented(CapabilityFeatureCodes.PosSalesDay, CapabilityModuleCodes.SalesPos, "POS Sales Day");
        Implemented(CapabilityFeatureCodes.PosInvoices, CapabilityModuleCodes.SalesPos, "POS Invoices");
        Implemented(CapabilityFeatureCodes.PosDrafts, CapabilityModuleCodes.SalesPos, "POS Drafts");
        Implemented(CapabilityFeatureCodes.PosDiscounts, CapabilityModuleCodes.SalesPos, "POS Discounts");
        Implemented(CapabilityFeatureCodes.PosMultiplePayments, CapabilityModuleCodes.SalesPos, "POS Multiple Payments");
        Implemented(CapabilityFeatureCodes.PosSalesLedger, CapabilityModuleCodes.SalesPos, "POS Sales Ledger");
        Implemented(CapabilityFeatureCodes.PosReports, CapabilityModuleCodes.SalesPos, "POS Reports");
        return features;
    }
}
