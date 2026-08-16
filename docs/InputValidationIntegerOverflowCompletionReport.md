# Input Validation & Integer Overflow Completion Report

Date: 2026-08-16

## Outcome

Numeric input validation, checked arithmetic, overflow handling, and PostgreSQL check constraints were added across product administration, carts, checkout/orders, inventory aggregation, and POS sales. Invalid annotated inputs now return HTTP 400 validation problem responses from the affected POST actions. Overflow paths are logged and return safe client-facing errors without exposing internals.

## Modified files

### Controllers

- `Areas/Admin/Controllers/ProductsController.cs`
  - Explicit `ModelState.IsValid` gates for Create, Edit, and AddStock.
  - HTTP 400 `ValidationProblem` responses with field/global messages.
  - Checked bottle-to-volume conversion, initial stock, and stock adjustment addition.
  - Overflow logging and safe handling; resulting stock is capped at 1,000,000.
- `Areas/Admin/Controllers/QuickSalesController.cs`
  - Explicit validation gates for OpenDay, SaveDraft, and CompleteSale.
  - Checked price/quantity multiplication, discount/total aggregation, payment aggregation, stock aggregation, and sales-day aggregation.
  - Currency totals are constrained to 0..1,000,000.00 and overflow is logged and returned as HTTP 400.
- `Controllers/CartController.cs`
  - Uses annotated cart input models for Add and Update.
  - Explicit validation gates returning HTTP 400.
  - Checked quantity and subtotal/line-total calculations with overflow handling.
- `Controllers/OrdersController.cs`
  - Explicit checkout validation gate returning HTTP 400.
  - Specific logged `OverflowException` handling returning HTTP 400.

### DTOs and view models

- `Models/ProductVW.cs`
- `Models/CartItemInput.cs` (new)
- `Models/CheckoutVM.cs`
- `Models/Admin/QuickSalesViewModels.cs`

### Services

- `Services/CartService.cs`
- `Services/GuestCartService.cs`
- `Services/OrderService.cs`
  - Checked cart quantity merging, grouped quantities, order line totals, order totals, and stock deduction aggregation.
  - `InventoryService.CalculateDeductionAmount` was audited and already used a checked multiplication block.

### EF Core and PostgreSQL

- `Models/NeondbContext.cs`
- `Migrations/20260816135606_AddStockAndPriceCheckConstraints.cs` (new)
- `Migrations/20260816135606_AddStockAndPriceCheckConstraints.Designer.cs` (new)
- `Migrations/NeondbContextModelSnapshot.cs`
- `scripts/AddStockAndPriceCheckConstraints.sql` (new idempotent PostgreSQL deployment script)

## DataAnnotation inventory

### ProductVW and retail price input

| Field | Attributes |
|---|---|
| `ProductVW.Price` | `[Required]`, `[Range(0, 1000000.00)]` |
| `ProductVW.Stockquantity` | `[Required]`, `[Range(0, 1000000)]` |
| `ProductVW.VolumeMl` | `[Range(0, 1000000)]`; it remains nullable for piece-based products, while the existing controller validation requires a positive value for ml-based products |
| `ProductVW.InitialStockQuantity` | `[Required]`, `[Range(0, 1000000)]` |
| `ProductVW.InitialBottleCount` | `[Required]`, `[Range(0, 1000000)]` |
| `ProductVW.AddedStockQuantity` | `[Required]`, `[Range(0, 1000000)]` |
| `ProductVW.AddedBottleCount` | `[Required]`, `[Range(0, 1000000)]` |
| `ProductRetailPriceInput.SizeMl` | `[Required]`, `[Range(0, 1000000)]` |
| `ProductRetailPriceInput.Price` | `[Required]`, `[Range(0, 1000000.00)]` |

### Cart and checkout input

| Field | Attributes |
|---|---|
| `AddCartItemInput.ProductId` | `[Required]`, `[Range(1, int.MaxValue)]` |
| `AddCartItemInput.Quantity` | `[Required]`, `[Range(0, 1000000)]` |
| `AddCartItemInput.RetailPriceId` | `[Range(1, int.MaxValue)]` |
| `UpdateCartItemInput.CartItemId` | `[Range(0, int.MaxValue)]` |
| `UpdateCartItemInput.ExpectedQuantity` | `[Range(0, 1000000)]` |
| `CheckoutVM.SubmittedCartTotal` | `[Required]`, `[Range(0, 1000000.00)]` |
| `CheckoutCartItemSnapshot.RetailSizeMl` | `[Range(0, 1000000)]` |
| `CheckoutCartItemSnapshot.Quantity` | `[Required]`, `[Range(0, 1000000)]` |
| `CheckoutCartItemSnapshot.UnitPrice` | `[Required]`, `[Range(0, 1000000.00)]` |

### POS input

| Field | Attributes |
|---|---|
| `OpenSalesDayViewModel.OpeningBalance` | `[Required]`, `[Range(0, 1000000.00)]` |
| `SaveDraftRequestModel.DiscountTotal` | `[Required]`, `[Range(0, 1000000.00)]` |
| `SaveDraftItemModel.RetailSizeMl` | `[Range(0, 1000000)]` |
| `SaveDraftItemModel.Quantity` | `[Required]`, `[Range(0, 1000000)]` |
| `SaveDraftItemModel.UnitPrice` | `[Required]`, `[Range(0, 1000000.00)]` |
| `SaveDraftItemModel.Discount` | `[Required]`, `[Range(0, 1000000.00)]` |
| `CompleteSaleRequestModel.DiscountTotal` | `[Required]`, `[Range(0, 1000000.00)]` |
| `CompleteSalePaymentModel.Amount` | `[Required]`, `[Range(0, 1000000.00)]` |

## PostgreSQL constraints

The migration and SQL script add these constraints:

- `products`: nonnegative `price` and `stockquantity`.
- `orders`: nonnegative `totalamount`.
- `orderitems`: positive `quantity` and nonnegative `unitprice`.
- `cartitems`: nonnegative `quantity`.
- `sales`: nonnegative `total_amount`, `discount_total`, and `final_amount`.
- `sale_items`: nonnegative `unit_price`, `discount`, and `total` (the existing positive quantity constraint remains).
- `sales_days`: nonnegative opening balance and recorded totals.
- Existing retail-price and sale-payment positive constraints remain in force.

Deployment options:

- EF Core: `dotnet ef database update --context NeondbContext`
- PostgreSQL script: `scripts/AddStockAndPriceCheckConstraints.sql`

The live database was not mutated by this workspace task; the migration and idempotent SQL script are provided for controlled deployment.

## Verification

- `dotnet build YAGOT_2.0.sln -c Release --no-restore`: **succeeded with 0 errors** (8 pre-existing warnings).
- `dotnet ef migrations has-pending-model-changes --context NeondbContext --configuration Release --no-build`: **no pending model changes**.
- `git diff --check`: no whitespace errors.

