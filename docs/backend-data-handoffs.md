# YAGOT Backend, Database, Data, Media & Infrastructure Handoffs

**READ THIS FILE BEFORE IMPLEMENTING BACKEND/DATABASE REMEDIATION.**

## 1. Purpose and Ownership

This document records cross-boundary work discovered during frontend remediation. Frontend intentionally did not implement these items unless a critical cross-boundary exception was necessary to remove an immediate frontend path.

This file is **not proof that Backend has not already fixed an item**. It distinguishes confirmed current source state, historical concern, and required future verification. Backend, Database, Data, Media, QA, and Infrastructure teams must inspect current source and—where authorized and safe—current live-data reality before acting.

## 2. Handoff Summary

| ID | Owner | Risk | Current known state | Required next action | Frontend dependency |
|----|-------|------|---------------------|----------------------|---------------------|
| FE-006-BE | Backend | Duplicate sale, inventory deduction, or payment state under replay | Current Complete Sale source uses a serializable transaction, locks the sale row, and rejects a non-Draft sale; no explicit idempotency-key contract was found. | Verify retry/replay and multi-client behavior; add the smallest safe server guarantee only if current safeguards are insufficient. | Frontend has one owner and an in-flight guard. |
| FE-010-BE | Backend / Database | Cross-session draft overwrite or lost updates | Frontend has a serialized coordinator. Current Sale has `UpdatedAt`, but no row-version/revision-token/optimistic-concurrency contract was found for POS draft saves. | Verify multi-client races and design durable conflict semantics if absent. | Preserve coordinator payload/state contracts. |
| FE-012-MEDIA | Media pipeline / Backend | Oversized media and no real responsive candidates | Frontend delivery attributes and fallbacks exist; no compression/WebP/AVIF derivative pipeline was found. | Profile layouts and design original/derivative processing and URL contracts. | Frontend can consume variant URLs once available. |
| IMG-DATA-001 | Data / Media | Broken, missing, stale, external, or unowned image references | Repository images and defensive URL handling exist; database reference completeness was not verified. | Audit stored image references against files and ownership. | Do not remove defensive fallback behavior prematurely. |
| IMG-DATA-002 | Data / Media | Inconsistent stored URL/path forms | Frontend remains defensive about paths and fallbacks. | Normalize or repair stored values after a data inventory and backup plan. | Keep accepted URL shape compatible or document the change. |
| IMG-DATA-003 | Data / Media | Orphaned originals/derivatives or unsafe asset deletion | No authoritative original → derivative → database ownership map is documented. | Establish mapping, lifecycle, retention, and deletion ownership. | Required before aggressive frontend asset cleanup. |
| FE-021 | Backend / Auth | External redirect if return URL validation regresses | Historical audit concern; current controller and Auth view use `Url.IsLocalUrl`. Exploitability is not established. | Run redirect regression tests for local, absolute, protocol-relative, encoded, and malformed values. | Frontend does not own redirect authorization. |
| FE-022 | Backend / Data | Privacy exposure from public legacy receipts | Protected storage and an authorized Admin receipt action exist. `wwwroot/uploads/receipts/receipt_8003cedb.png` is still tracked under static web content. | Inventory and classify legacy files, migrate/remove safely, verify new storage/authorization, and define retention. | Frontend uses the protected Admin URL for current receipt preview. |
| FE-023 | Infrastructure / Backend + Frontend | CSP bypass or production breakage from premature enforcement | Current `Program.cs` emits CSP, but allows `'unsafe-inline'`; current Razor still contains inline script/style structures. Strict CSP is not ready. | Inventory origins, use Report-Only telemetry, reduce blockers or design nonce/hash policy, then validate enforcement. | Frontend must avoid adding inline code or unsafe HTML. |
| A11Y-FORM-BE-001 | Backend / Forms | Server validation can break accessible recovery or expose unsafe messages | Frontend associations and focus behavior exist on remediated forms; authenticated server-error flows were not comprehensively exercised. | Verify ModelState, field/error associations, summaries, focus recovery, and safe message encoding. | Preserve current IDs, `asp-for`, `asp-validation-for`, and ARIA links. |
| QA-DB-001 | QA / Backend / Infrastructure / Database | Unsafe tests, unverified flows, and inability to measure production-like behavior | Startup migrates configured PostgreSQL and POS/Admin/Checkout flows can persist changes; no disposable authenticated QA environment is documented. | Create isolated disposable PostgreSQL, controlled configuration/migrations, seed data/users, and repeatable reset. | Unlocks safe MVC, visual, accessibility, and performance validation. |
| PERF-BE-001 | Backend / Database | Incomplete/non-deterministic storefront results and browser-only pagination | Storefront filters category/brand/search server-side, then calls `Take(100)` without explicit ordering or server page parameters; JS filters/sorts/pages only that returned set. | Design deterministic server filtering/order/pagination if current business requirements exceed this cap. | FE-007 only unified frontend state ownership. |
| MAINT-BE-001 | Backend / Platform Maintenance | Unsupported, vulnerable, or incompatible dependencies | Current project targets `net9.0`; several Microsoft/EF/Npgsql packages are pinned at 9.0.0. No current advisory/compatibility audit was performed here. | Use current official lifecycle, compatibility, and security information; upgrade in scoped tested changes. | Coordinate any rendered/dependency behavior changes. |
| PERF-INFRA-001 | Backend / Infrastructure | Production delivery may differ from source configuration | Source registers and uses Brotli/Gzip response compression; production headers, caching, validators, protocol, CDN, and actual compression were not verified. | Test production-equivalent HTTP behavior and define cache/versioning policy. | Frontend asset changes alone cannot prove delivery quality. |
| PERF-QA-001 | QA / Infrastructure / Frontend cooperation | No trustworthy performance baseline or regression budget | No production-like Core Web Vitals/network baseline exists; QA-DB-001 weakens representative authenticated coverage. | Measure representative pages on mobile and desktop after safe QA exists; record repeatable before/after results. | Uses current frontend as baseline, not as proof of speed. |

### FE-006-BE — Complete Sale server-side idempotency

**Owner: Backend**

The frontend now prevents duplicate local execution through one Complete Sale event owner and an in-flight guard. That does not by itself protect against retries, multiple tabs, multiple clients, network replay, or duplicate submissions.

Current source has meaningful server safeguards: Complete Sale requires a sale ID, opens a serializable transaction, locks the matching sale row with `FOR UPDATE`, verifies that its status is `Draft`, and only then changes inventory/payment/sale state. Because these safeguards may already provide an equivalent transactional guarantee for the current route, do not label the endpoint vulnerable without concurrency and replay verification.

Required action: test the complete-sale contract under duplicate requests, retries, multiple clients, transaction failures, and ambiguous network outcomes. Confirm that invoice creation, inventory deduction, and payment/order state can happen at most once. Add an idempotency key or another minimal durable mechanism only if current safeguards do not satisfy that requirement.

### FE-010-BE — POS draft durable revision/concurrency ownership

**Owner: Backend / Database**

Frontend current state includes a single-client save coordinator, single-flight execution, revision ownership, and dirty/latest preservation. Current backend draft saves use a transaction and require Draft status, and the Sale model has `UpdatedAt`. Current inspection did not find a row-version column, revision token, concurrency attribute, or POS-specific optimistic conflict response.

Multiple browser sessions or clients can therefore still race unless an unobserved database rule or server contract provides durable conflict semantics. Verify concurrent saves against a disposable database. If protection is absent, design the smallest safe row-version, revision-token, optimistic-concurrency, or equivalent conflict-detection solution without breaking the existing coordinator contract.

### FE-012-MEDIA — Responsive image derivative pipeline

**Owner: Media pipeline / Backend**

Frontend has implemented loading, decoding, fallback, priority, safe URL normalization, and layout reservation where available. It has not implemented actual compression, WebP, AVIF, responsive derivative generation, or real variant-backed `srcset` delivery.

Design an image-processing strategy based on actual layout and source-image profiling. Outputs may conceptually include small, medium, and large variants, but do not hardcode exact sizes before measuring current cards, hero, details, cart, and Admin surfaces. Define storage keys, failure behavior, regeneration, deletion, and frontend URL contracts.

### IMG-DATA-001 — Stored image reference audit

**Owner: Data / Media**

Audit stored image references and classify missing files, broken paths, stale records, external URLs, duplicates, and unknown ownership. Compare database values with repository/static storage and any deployment media store. Do not delete files until stored references and retention requirements are understood.

### IMG-DATA-002 — Stored image URL/path normalization

**Owner: Data / Media**

Normalize or repair stored image URL/path values where appropriate, after inventory and backup. Frontend normalization remains defensive and must not be removed until canonical data and migration compatibility are verified.

### IMG-DATA-003 — Original/derivative/database ownership

**Owner: Data / Media**

Establish the mapping and lifecycle between original assets, generated derivatives, and database references. Define who creates, regenerates, retires, and deletes each asset. This ownership map is required before aggressive frontend asset deletion.

### FE-021 — Return URL redirect verification

**Owner: Backend / Auth**

The original concern was an external return URL/open redirect. Current source uses `Url.IsLocalUrl` in `AccountController` and while preparing the Auth view, so the historical report is not proof of a current exploitable vulnerability.

Perform backend/runtime regression tests for:

- A valid local URL.
- An absolute external URL.
- A protocol-relative URL.
- Encoded and double-encoded values as applicable.
- A malformed or empty return URL.

Verify every login/register/recovery redirect path and fallback. Frontend does not own this security control.

### FE-022 — Payment receipt privacy and legacy public files

**Owner: Backend / Data**

**Original severity: Critical**

Current source stages and promotes new receipts under `App_Data/ProtectedReceipts`, and Admin receipt delivery is inside an `[Authorize(Roles = "Admin,Developer")]` controller/action. Current Admin views use that protected action URL.

However, `wwwroot/uploads/receipts/receipt_8003cedb.png` is still a tracked file beneath static web content, and `UseStaticFiles()` serves that area without application authorization. The file's real/sensitive-data status was not determined in this documentation task.

Required next actions:

- Inventory every public legacy receipt in source, deployment storage, and retained data references.
- Determine whether each file contains real or sensitive data and who owns it.
- Migrate or remove files safely, including stored database references.
- Verify that all new uploads remain outside public static storage.
- Test authorization and object ownership expectations for the delivery endpoint.
- Define receipt retention, audit, and deletion policy.

Do not blindly delete receipts without determining data ownership and retention requirements.

### FE-023 — Strict Content Security Policy program

**Owner: Infrastructure / Backend + Frontend cooperation**

The original audit found no CSP. Current source now emits a CSP response header, so that historical statement is outdated. The current policy still permits `'unsafe-inline'` for scripts and styles, and inline executable/style structures remain. Strict CSP is therefore not considered ready.

Required path:

1. Inventory required script, style, font, image, connection, and frame origins.
2. Introduce or evaluate a stricter policy in CSP Report-Only mode.
3. Collect and review telemetry.
4. Reduce remaining inline blockers or design a nonce/hash strategy.
5. Establish an explicit asset-origin and third-party dependency policy.
6. Enforce only after representative authenticated and public validation.

Do not deploy a strict policy blindly.

### A11Y-FORM-BE-001 — Server validation accessibility contract

**Owner: Backend / Forms**

Server-side validation responses must preserve frontend accessibility associations and safe recovery. Verify ModelState errors, field associations, error summaries, focus/recovery behavior, and safe encoding of server messages across representative invalid submissions. A build or static scan does not prove these authenticated/runtime flows.

### QA-DB-001 — Safe disposable QA environment

**Owner: QA / Backend / Infrastructure / Database**

This is a high-value project enablement item. Current source confirms:

- Startup executes `Database.MigrateAsync()`.
- The configured PostgreSQL database cannot be assumed disposable.
- Opening POS without a usable draft can create and commit a draft.
- Admin, POS, Checkout, and order flows may mutate data.
- Authenticated production-like frontend testing is therefore restricted.

Create a safe QA environment with:

- Disposable/test PostgreSQL.
- Separate secrets and configuration.
- Controlled migrations.
- Seeded authenticated users and roles.
- Seeded POS/Admin/storefront data.
- Disposable transactional records.
- A repeatable reset strategy.

This enables real MVC browser testing, Lighthouse/performance testing, responsive and visual QA, accessibility verification, and complete Checkout/POS/Admin scenarios. Do not point automated tests at shared production or staging data as a substitute.

### PERF-BE-001 — Product Query / Pagination Verification

**Owner: Backend / Database**

FE-007 closed duplicate frontend state ownership; it did not close backend query performance. Current storefront `ProductsController.Index` applies category, brand, and search filters server-side, then executes `Take(100).ToListAsync()` without an explicit `OrderBy` or server page/page-size contract. `wwwroot/js/products/index.js` then filters, sorts, and reveals pages of 12 only within that returned set.

Required action:

- Define deterministic ordering.
- Verify which filters must be server-side.
- Add server-side pagination using `Skip`/`Take` or an equivalent stable strategy if required.
- Return only required data and preserve URL/filter behavior.
- Measure query plans and indexes with representative data before calling the work optimized.

Do not modify the implementation until the current business requirements and data volume are confirmed.

### MAINT-BE-001 — .NET / NuGet Dependency Audit

**Owner: Backend / Platform Maintenance**

The frontend remediation program did not perform a dependency upgrade program. Current source targets `net9.0`; the project file pins Microsoft.AspNetCore.Authentication.Google, EF Core packages, and Npgsql EF Core at 9.0.0, alongside MaxMind.GeoIP2 and UAParser packages.

Audit current TargetFramework support, direct versions, transitive dependencies, security advisories, deprecated packages, and provider compatibility using current official information. Do not bulk-upgrade without compatibility review, build and runtime tests, and migration impact review.

### PERF-INFRA-001 — HTTP Delivery Verification

**Owner: Backend / Infrastructure**

Current source registers Brotli and Gzip response compression and calls `UseResponseCompression()`. This confirms application configuration only; it does not prove production behavior through reverse proxies, hosting, or CDN layers.

Inspect production-equivalent behavior for Brotli, Gzip, `Cache-Control`, `ETag`/`Last-Modified` where appropriate, static-asset caching, versioned assets, CDN policy if used, and HTTP/2 or HTTP/3 where relevant. Record response headers and transfer sizes. Frontend improvements alone do not prove optimal HTTP delivery.

### PERF-QA-001 — Production-like Performance Baseline

**Owner: QA / Infrastructure / Frontend cooperation**

This work is blocked or weakened by QA-DB-001 today. Once a safe representative environment exists, measure at minimum LCP, CLS, INP where measurable, FCP, TTFB, transferred bytes, request count, largest resources, and image/font/JavaScript/CSS bytes.

Test representative Home, Products, Product Details, Cart, Checkout, Auth, Admin Orders, and POS routes with both mobile and desktop profiles. Record repeatable before/after values and environment details. Do not infer production metrics from build, source, or harness results.

## 3. Work Explicitly NOT Completed by Frontend Remediation

- Server-side idempotency verification across retries/replays.
- Durable database concurrency for POS drafts.
- Image derivative generation.
- Image data cleanup and ownership mapping.
- Legacy receipt storage migration/data classification.
- Strict CSP enforcement.
- A safe disposable authenticated QA environment.
- Backend storefront product-query optimization and server pagination.
- A .NET/NuGet package update program.
- Production-equivalent compression and cache/header verification.
- A true production-like performance baseline.

## 4. Recommended Backend Execution Order

Suggested risk-based order:

1. FE-022 receipt privacy and data verification.
2. QA-DB-001 safe QA environment.
3. FE-006-BE sale idempotency/transactional verification.
4. FE-010-BE draft concurrency.
5. PERF-BE-001 product query/pagination verification.
6. FE-012-MEDIA and IMG-DATA image pipeline/ownership work.
7. MAINT-BE-001 dependency and security audit.
8. PERF-INFRA-001 HTTP caching/compression verification.
9. FE-023 CSP Report-Only and enforcement program.
10. PERF-QA-001 production-like performance baseline.

Actual priority must be re-evaluated against current backend source, live-data reality, business impact, exploitability, and operational risk. This is not a massive implementation roadmap; each coherent change should remain independently reviewable.

## 5. Rules for Future Backend/Database Codex Chats

Future Codex sessions must:

- Inspect current source before implementation.
- Never assume an old report equals a current vulnerability.
- Distinguish historical concern, confirmed current state, and required runtime/data verification.
- Create one scoped branch per coherent change, after read-only analysis and only when authorized.
- Protect production and shared data.
- Avoid unsafe migrations and destructive verification.
- Build and test proportionately before recommending a commit.
- Report static, build, runtime, authenticated, database, security, visual, and performance evidence separately.
- Not modify frontend contracts without explicitly documenting the impact.

Related project documentation:

- `docs/frontend-remediation-history.md`
- `docs/backend-data-handoffs.md`
- `docs/frontend-architecture.md`
