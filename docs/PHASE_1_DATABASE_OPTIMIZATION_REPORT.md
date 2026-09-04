# YAGOT 2.0 Phase 1 Database Optimization Report

## 1. Executive Summary

Phase 1 optimized the existing Store database and its EF Core access paths without introducing a catalog database or changing the Users database architecture. The highest-impact changes are:

- The homepage no longer loads the complete product table and complete product relationships. It now reads at most eight product-card rows, twelve lightweight hero rows, and eight best-seller card rows.
- Storefront product cards, product details, recently viewed products, and catalog pages use a common read projection that selects only the scalar and relationship fields rendered by those views.
- Categories and brand filter metadata, plus public store settings/footer settings, use bounded in-process caching with explicit mutation invalidation.
- The cart badge uses a quantity aggregate instead of loading the cart/product/category/retail-price graph. Empty guest carts require no database query, and cart pages reuse a cart already loaded in the request.
- Best-seller refreshes load compact current totals and write only products whose computed total changed.
- Repeated admin dashboard/product-summary scans were consolidated into conditional aggregate queries.
- A reviewed PostgreSQL migration adds indexes for observed search, filtering, ordering, retail metadata, and POS best-seller aggregation patterns.

Expected Neon impact is lower rows transferred, less EF materialization/tracking, fewer repeated reference-data queries, fewer full-product reads, fewer repeated aggregate scans, and far fewer product updates during stable best-seller refreshes. No runtime Neon metrics or production `EXPLAIN ANALYZE` data were available, so no percentage reduction is claimed.

## 2. Files Changed

- `Controllers/HomeController.cs` — bounded homepage queries and cached catalog metadata use.
- `Controllers/ProductsController.cs` — projected product details and recently viewed reads.
- `Views/Home/Index.cshtml` — consumes explicit new-arrival, hero, best-seller, and brand collections instead of deriving all sections from every product.
- `Services/ViewModels.cs` — explicit homepage collections.
- `Services/StorefrontProductQueries.cs` — shared read-only storefront projection and detached view-model-shaped materialization.
- `Services/ProductCatalogService.cs` — projected paged products and cached categories/brands; current-stock-dependent retail sizes remain live.
- `Services/StoreSettingsService.cs` — application-level settings/footer cache, clone-on-read protection, invalidation, and a no-tracking settings read.
- `Services/CartService.cs` — lightweight authenticated cart quantity query and same-request cart reuse.
- `Services/GuestCartService.cs` — lightweight guest badge validation, empty-cart short circuit, and same-request cart reuse.
- `ViewComponents/CartBadgeViewComponent.cs` — quantity-only badge path.
- `Services/BestSellerService.cs` — compact change detection and changed-row-only updates.
- `Areas/Admin/Controllers/DashboardController.cs` — one order summary aggregate replaces seven separate order aggregates.
- `Areas/Admin/Controllers/ProductsController.cs` — one inventory summary aggregate and catalog-cache invalidation after product mutations.
- `Areas/Admin/Controllers/CategoriesController.cs` — catalog-cache invalidation after category mutations.
- `Areas/Admin/Controllers/OrdersController.cs` — no-tracking read for external user display data.
- `Models/NeondbContext.cs` — model definitions for reviewed PostgreSQL indexes and `pg_trgm`.
- `Migrations/20260903164333_Phase1StoreDatabaseIndexes.cs` — safe Phase 1 index migration, including the POS effective-completion-date expression index.
- `Migrations/20260903164333_Phase1StoreDatabaseIndexes.Designer.cs` — generated migration model metadata.
- `Migrations/NeondbContextModelSnapshot.cs` — updated EF Core model snapshot.
- `docs/PHASE_1_DATABASE_OPTIMIZATION_REPORT.md` — this report.

## 3. Database Queries Optimized

### Homepage products

Before: one unbounded `products` query included categories and active retail prices for every product; another joined product query loaded best sellers.

After: separate bounded queries read eight new-arrival card projections, twelve hero-only projections, and eight best-seller card projections. The split adds a small bounded query but eliminates the unbounded product graph and avoids transferring retail/category data for hero slides.

Expected impact: substantially lower product and retail-price rows/columns transferred as the catalog grows; bounded materialization cost.

### Catalog page products

Before: pagination was applied before materialization, but each page loaded full `Product`, `Category`, and filtered `ProductRetailPrice` entities.

After: filtering, count, ordering, `Skip`, and `Take` remain server-side; the resulting eight rows use the storefront projection. Categories and brands are retrieved from the shared metadata cache after its first fill.

Expected impact: fewer selected columns and two fewer metadata queries on warm-cache requests.

### Product details and recently viewed

Before: full product entities plus category and active retail entities were materialized.

After: only view-required product/category/retail fields are selected. Recently viewed remains capped at six and preserves caller order in memory.

Expected impact: lower row width and no change tracking.

### Cart badge

Before: authenticated requests loaded a cart, cart items, products, categories, and retail prices. Guest requests loaded products/categories and retail prices even for a count.

After: authenticated requests execute a nullable `SUM(quantity)` over matching cart items. Guest requests validate only product IDs and sum the protected cookie quantities; an empty cookie issues no SQL. If the full cart was already loaded earlier in the request, the badge uses that in-memory result.

Expected impact: removes a frequently executed multi-join entity graph from most rendered pages and eliminates duplicate cart reads on cart-page rendering.

### Admin dashboard

Before: total orders, revenue, and five status counts were seven separate scans/aggregates.

After: one grouped conditional aggregate produces the same seven values. Recent orders remain a separate bounded query because combining those rows with aggregates would produce more expensive SQL.

Expected impact: six fewer order aggregate statements per dashboard request.

### Admin product summary

Before: active, archived, low-stock, and out-of-stock values were four separate product counts.

After: one conditional aggregate computes all four values. The paged/filtered result count remains separate because it serves different filtering/pagination semantics.

Expected impact: three fewer product aggregate statements per product-management request.

### Best-seller refresh

Before: after two server-side sales aggregates, every product was loaded and every product received `total_sold` and `sales_last_updated_at`, causing updates even when totals were unchanged.

After: a compact `id,total_sold` candidate query finds products with current or computed sales, only changed IDs are loaded as tracked entities, and `SaveChanges` is skipped when nothing changed.

Expected impact: stable six-hour refreshes produce no product-row updates; changed refreshes update only affected products.

## 4. Homepage Optimization

- New arrivals are limited in SQL to eight.
- Hero slides are limited in SQL to twelve and select only ID, name, image URL, and creation time.
- The no-image fallback continues to use the first six new arrivals.
- Best sellers retain the existing sellability predicate, `total_sold > 0`, ordering, limit of eight, and minimum display threshold of three.
- Categories and brands reuse catalog metadata; homepage settings populate the same shared settings cache later used by the footer.
- Product-card retail and availability behavior is preserved because the projection includes stock unit, stock quantity, volume, retail-enabled state, and usable retail-price rows.

## 5. Product Query Optimization

The repository-wide product query audit covered homepage, catalog/search/filtering, categories, details, recently viewed, carts, checkout, order history, best sellers, inventory, POS, and admin product management.

Read-only storefront query shape is centralized in `StorefrontProductQueries`. It deliberately creates detached `Product` objects for existing strongly typed Razor partials, avoiding a broad view rewrite while reducing SQL columns. Tracked product loads remain in product editing, cart mutation validation, inventory adjustment, checkout, stock restoration, and POS paths where updates or transactional validation require them.

The `ProductService.GetProductByIdAsync` query remains tracked because its admin delete caller modifies the returned product before `SaveChanges`. Checkout/POS `SELECT ... FOR UPDATE` product loads were deliberately not projected or cached.

## 6. Search and Filtering

- Search normalization and escaped `ILIKE '%term%'` behavior remain unchanged for name, description, and brand.
- GIN trigram indexes were added to those three actual search columns so PostgreSQL can accelerate contains-style patterns when selective.
- Filtering, count, ordering, and pagination remain database-side.
- Category/newest ordering is covered by `(categoryid, createdat DESC)`; unfiltered newest ordering is covered by `(createdat DESC)`.
- Brand and category metadata are cached for five minutes. Retail-size metadata remains live because its result depends on current stock and must not become a transactional/availability cache.

## 7. Cart / Header / Footer

The header cart badge no longer loads full entities. Authenticated cart quantity is an aggregate; guest quantity reads only IDs needed to preserve the previous behavior of excluding deleted products. Empty guest carts avoid Store access entirely. Scoped service fields reuse a full cart already read by the cart page, preventing a second query during layout rendering.

Footer settings are projected to four values and cached application-wide for five minutes. A homepage full-settings read also primes the footer cache. Settings writes invalidate both settings keys immediately in the current application instance.

## 8. Best Seller Optimization

Online and POS aggregation logic, the rolling period, qualifying online statuses, stock-deducted requirement, POS completed status, quantities, transaction, and result ordering are unchanged. The write phase now compares computed totals with current compact totals and updates only differences.

`sales_last_updated_at` now advances only for rows whose total changes. This matches the field's per-product update meaning and avoids turning an otherwise unchanged refresh into a table-wide write. `GetLastRefreshTimeAsync` still returns the latest changed-product timestamp.

The process-local semaphore and database execution strategy remain. This phase did not introduce a distributed scheduler lock; that is listed as a remaining multi-instance concern.

## 9. Index Changes

| Table | Index / columns | Purpose and benefiting query | Reason |
|---|---|---|---|
| `products` | `ix_products_createdat (createdat DESC)` | Homepage new arrivals/hero and unfiltered newest catalog/admin reads | Existing indexes did not begin with creation time. |
| `products` | `ix_products_categoryid_createdat (categoryid, createdat DESC)` | Category-filtered newest catalog reads and category FK lookup | Replaces `IX_products_categoryid`; the same leading category key preserves its lookup capability while adding sort support, avoiding redundant indexes. |
| `products` | GIN `name gin_trgm_ops` | Public/admin contains search | B-tree indexes cannot efficiently serve leading-wildcard contains search. |
| `products` | GIN `description gin_trgm_ops` | Public/admin contains search | Actual `ILIKE`/contains predicate on a potentially large text column. |
| `products` | GIN `brand gin_trgm_ops` | Public/admin contains search | Actual contains predicate; also useful for selective brand text searches. |
| `product_retail_prices` | partial `size_ml` where active/valid | Live retail-size filter metadata | Narrows the index to rows eligible for the metadata predicate and avoids indexing invalid/inactive rows. The existing unique `(product_id,size_ml)` index cannot lead with `size_ml`. |
| `sales` | `(status, COALESCE(completed_at,created_at))` | POS portion of rolling best-seller aggregation | Matches the exact completed-status and effective-date predicate; existing sales indexes lead with sales-day ID instead. |

Existing useful indexes retained include cart uniqueness/item keys, order status/date and user/date, payment-method settings FK, product retail `(product_id,size_ml)`, order-item FK indexes, sale-item sale/product indexes, and sales-day/status indexes.

The generated migration contains no table/column deletion. It uses `DROP INDEX IF EXISTS` for the absent legacy single-column product-category index, then adds the composite leading-key replacement. On 2026-09-03 it was applied only to the locally configured test/backup Store target (`neondb` on `ep-floral-forest-al2seqtv.c-3.eu-central-1.aws.neon.tech`); no production or Users database migration was applied.

## 10. Caching

### Cached data

- Store settings used by public pages: five-minute absolute expiration.
- Footer contact/social projection: five-minute absolute expiration.
- Catalog categories and distinct brands: five-minute absolute expiration.

### Invalidation

- Successful store-settings writes advance a cache generation and remove settings and footer keys.
- Successful product create/edit/archive operations advance a cache generation and remove catalog metadata.
- Successful category create/edit/delete operations advance a cache generation and remove catalog metadata.
- A load that overlaps invalidation may serve its current request but cannot repopulate a stale shared cache entry because generation checks suppress that write.
- Absolute expiration bounds staleness if another application instance performs a write; the cache is intentionally simple and in-process because the project already uses `IMemoryCache`.

Cached `StoreSettings` values are cloned on read so Razor/admin consumers cannot mutate the shared cached instance. Stock, prices used for cart/checkout/POS validation, carts, orders, payment state, and retail-size availability are not cached.

## 11. Transaction Safety

- Checkout transactions remain intact: **confirmed**.
- Checkout product `FOR UPDATE` locking remains intact: **confirmed**.
- POS serializable/read-committed transactions, row locks, and draft locks remain intact: **confirmed**.
- Inventory atomic `ExecuteUpdateAsync` operations remain intact: **confirmed**.
- Cart advisory locking and transaction boundaries remain intact: **confirmed**.
- Authoritative price and retail-price validation remain live: **confirmed**.
- Stock and product-existence validation remain live: **confirmed**.

No transactional query was replaced by a cached read. No Store/Users split, catalog database, replication, synchronization, or distributed transaction was introduced.

## 12. Verification

### Build

`dotnet build YAGOT_2.0.sln --no-restore` succeeded with 0 errors. It reported 17 pre-existing warnings in files/lines unrelated to these changes; no warning points to a changed Phase 1 implementation line.

### Tests

`dotnet test YAGOT_2.0.sln --no-build --logger "console;verbosity=normal"` exited successfully. No test projects/test cases are present in the repository, so this is build-level test-runner validation rather than behavioral coverage.

### Migration validation

- `dotnet ef migrations has-pending-model-changes --context NeondbContext --no-build` reported: `No changes have been made to the model since the last migration.`
- A SQL script was generated from `20260901122213_AddAdminNoteToOrders` to `20260903164333_Phase1StoreDatabaseIndexes` and manually reviewed.
- The script contains only the documented index/extension operations and migration-history insert.
- `20260903164333_Phase1StoreDatabaseIndexes` was applied successfully to the configured test/backup Store database and verified in `__EFMigrationsHistory`.
- Direct PostgreSQL catalog checks confirmed `pg_trgm` and all seven intended Phase 1 indexes.
- `EXPLAIN ANALYZE` was not run as part of the migration-application pass; this verification used migration history, PostgreSQL catalogs, generated SQL, and HTTP smoke results rather than a performance benchmark.

### Application startup and smoke test

The application started successfully on `http://127.0.0.1:5088` using the same test Store target. Homepage, catalog, search, product details, and guest cart returned HTTP 200. Checkout, orders, admin products, admin dashboard, and POS routes returned their expected unauthenticated redirects. No test credentials/session were available, so protected views and authenticated cart/checkout actions were not bypassed or mutated. The startup best-seller refresh completed successfully with zero changed product rows.

### Code and related-path review

All changed files were reviewed with `git diff`/`git diff --check`. The follow-up audit re-searched every product query, `Include`, raw SQL statement, transaction, `FOR UPDATE`, advisory lock, bulk update, and bulk delete. Remaining large includes were kept only where existing views need the data or tracked/locked entities are required for mutation.

## 13. Performance / Compute Impact

### Measured

- Build: success, 0 errors.
- Test command: success; zero test projects discovered.
- Migration model drift: none.
- Migration SQL generation: success.
- Homepage product row bounds after change: 8 new-arrival card rows, 12 hero rows, and 8 best-seller card rows before related retail rows, instead of an unbounded all-product graph plus best sellers.
- Admin dashboard order aggregate statements: reduced from 7 to 1, with the separate six-row recent-orders query retained.
- Admin product inventory-summary statements: reduced from 4 to 1.
- Stable best-seller refresh product updates: 0 when no computed total differs.

No database latency, buffer, CPU, Neon active-time, or execution-plan measurements were taken.

### Estimated

- Homepage cost should remain bounded as product count grows except for distinct brand metadata on cache refill.
- Warm catalog/home metadata requests should avoid repeated category and brand scans.
- Cart-badge workload should be materially smaller because it no longer materializes joined entity graphs.
- GIN trigram indexes should reduce selective contains-search scans; very short or nonselective terms may still choose sequential scans.
- Best-seller write amplification should fall sharply during periods when rankings/totals are stable.
- New indexes add storage and product-write maintenance overhead; this is the tradeoff for lower read compute on the observed high-frequency predicates.

## 14. Remaining Bottlenecks

- Public catalog product reads, count queries, live retail-size metadata, and contains search still use the Store database and remain a major Store workload at high traffic.
- A catalog request still needs a filtered `COUNT` plus its page query. Keyset pagination would change existing numbered-page behavior and was not introduced.
- The homepage intentionally issues separate bounded new-arrival, hero, and best-seller queries. They avoid an unbounded join but are still Store reads.
- Order-history/admin/POS detail pages retain relationship graphs required by their current views. Dedicated DTO conversions could further reduce row width but would be a larger behavior-risking change.
- Checkout and stock-restoration lock products one at a time in deterministic ID order. This is intentionally retained for correctness even though it creates multiple statements.
- Best-seller scheduling uses a process-local semaphore. Multiple application instances could still run the job concurrently; a database advisory scheduler lock may be appropriate in a later carefully tested phase.
- The application currently applies EF migrations during startup. The new GIN indexes should be scheduled with operational awareness because normal `CREATE INDEX` can consume compute and briefly contend with writes; Phase 1 did not alter deployment architecture.
- Production `EXPLAIN (ANALYZE, BUFFERS)` and Neon compute metrics are still needed to quantify impact and confirm planner choices with real cardinalities.

## 15. Phase 2 Recommendation

Phase 2 was not implemented. The current evidence shows that public product/catalog traffic is still structurally coupled to the authoritative Store database even after bounding, projecting, caching metadata, and indexing searches. A separate read-only Catalog Project may be worthwhile if post-deployment Neon metrics still show public reads dominating compute or repeatedly waking the Store compute.

That decision should be based on measured Store query volume, Neon active time, cache hit rate, search plans, replication freshness requirements, operational cost, and failure-mode design. The Store `products` table must remain authoritative for carts, checkout, POS, prices, and inventory.
