# Yaqoot Backend Development Rules

This document is the authoritative backend development guide for the Yaqoot project.
It defines how controllers, services, models, ViewModels, persistence, filters,
configuration, integrations, and backend modules must be written.

Yaqoot is a reusable commercial e-commerce template. Backend code must preserve
template reuse, production readiness, maintainability, and clear separation of
responsibilities.

---

## 1. Purpose

The purpose of this document is to make backend implementation consistent and safe
across the storefront, customer experience, and administration area.

This document defines:

- Where backend responsibilities belong.
- How controllers, services, models, and ViewModels must be written.
- How business logic should be isolated and reused.
- How Entity Framework Core should be used.
- How configuration, dependency injection, file storage, filters, authentication,
  authorization, and external integrations should be handled.
- What backend patterns are not allowed for new work.
- What reviewers should check before accepting backend changes.

This document focuses on implementation rules. It does not restate the project
architecture, frontend rules, visual design system, or workflow process.

---

## 2. Backend Philosophy

Backend code should be simple, explicit, reusable, and production-oriented.

The current backend uses:

- ASP.NET Core MVC controllers.
- Public controllers under `Controllers`.
- Admin controllers under `Areas/Admin/Controllers`.
- Entity Framework Core with PostgreSQL through `NeondbContext`.
- Service classes under `Services`.
- Filters under `Filters`.
- Entity and form/view model types under `Models`.
- Cookie authentication with Google authentication support.
- Dependency injection configured in `Program.cs`.

New backend work must strengthen these boundaries. Controllers should orchestrate
requests. Services should own reusable business operations. Models should represent
clear data shapes. Configuration should provide environment and client-specific
values. External providers should be isolated behind services.

---

## 3. Controller Rules

Controllers coordinate HTTP requests and responses. They must not become the primary
home for reusable business logic.

Rules:

- Keep controllers thin and workflow-focused.
- Use constructor injection for dependencies.
- Delegate reusable business operations to services.
- Use `async` EF Core and service APIs consistently.
- Validate request intent before performing state changes.
- Use `[HttpGet]`, `[HttpPost]`, and `[ValidateAntiForgeryToken]` on form actions.
- Return `NotFound`, `Forbid`, redirects, or views intentionally.
- Use `TempData` only for short-lived user feedback across redirects.
- Use `ModelState` for validation feedback.
- Use area controllers only for admin or area-specific workflows.
- Avoid direct `DbContext` access in controllers when the operation belongs in a service.
- Avoid duplicated query logic across public and admin controllers.
- Avoid file-system, external API, or cryptography logic inside controllers.

Allowed controller responsibilities:

- Resolve the current route and request parameters.
- Load page-specific view data by calling services.
- Check ownership or authorization for the current request.
- Translate service results into MVC responses.
- Rebuild select lists or view data after validation failures.

---

## 4. Service Layer Rules

Services own reusable business operations and infrastructure-facing workflows.

Current services include:

- `CartService`.
- `OrderService`.
- `ProductService`.
- `CategoryServer`.
- `Image`.
- `DealingAPI`.
- `ShippingPolicy`.
- `VisitService`.

Rules:

- Put reusable business logic in services, not controllers.
- Keep each service cohesive around one business capability or integration.
- Use constructor injection for `DbContext`, `HttpClient`, configuration, and other
  dependencies.
- Prefer async methods when interacting with the database, files, or network.
- Do not expose mutable public fields for operation results.
- Return explicit results or throw meaningful domain exceptions for invalid workflows.
- Keep service methods reusable across storefront and admin when the behavior is shared.
- Avoid service methods that silently fail without a clear result.
- Avoid catch-all utility services with unrelated responsibilities.
- Do not place presentation formatting in services unless it is explicitly a shared
  view-facing formatter.

Service naming should describe responsibility. New services should use clear names
such as `ProductCatalogService`, `OrderWorkflowService`, `MediaStorageService`, or
`StoreSettingsService` when those boundaries become formal.

---

## 5. Business Logic Rules

Business logic belongs in services or dedicated domain helpers, not in views and not
primarily in controllers.

Business logic includes:

- Cart creation and mutation.
- Stock validation.
- Order creation.
- Order status transitions.
- Product lifecycle rules.
- Category lifecycle rules.
- User registration and profile rules.
- Password hashing and credential verification.
- Shipping policy calculation.
- File validation and storage decisions.
- Site status interpretation.
- Visit and security logging.

Rules:

- Do not duplicate business rules across controllers.
- Do not encode business rules in Razor Views.
- Do not rely on client-side JavaScript for business correctness.
- Keep business rules deterministic and testable where practical.
- Use constants or configuration for reusable status names, limits, and policy values.
- Avoid magic numbers and magic strings in repeated workflows.
- Use transactions when one business operation updates multiple related records.

---

## 6. Model Rules

Models must have clear responsibility.

Current model types include:

- EF Core entities such as `Product`, `Category`, `Cart`, `Cartitem`, `Order`,
  `Orderitem`, `User`, `Visit`, `Securitylog`, and `Deliveryorder`.
- View-facing or input models such as `ProductVW`, `CategoryVW`, `ProfileVM`,
  `RecoveryModel`, `ContactMessageVM`, and `ErrorViewModel`.
- Integration DTOs such as `SiteDto`, `SiteDtoAdmin`, `IpApiResponse`, and
  `IpWhoIsResponse`.

Rules:

- Entity models represent persisted database records.
- Input/ViewModel types represent form input or view-facing data.
- DTOs represent external API data.
- Do not mix unrelated responsibilities in one type.
- Do not add UI-only fields to EF entities when a ViewModel would be cleaner.
- Do not expose sensitive fields to views unless the view truly needs them.
- Use nullable annotations intentionally.
- Use validation attributes on input models where appropriate.
- Keep naming clear enough to distinguish entities, ViewModels, and DTOs.

Naming recommendation:

- Prefer `ProductViewModel`, `ProductInputModel`, or `ProductEditModel` over ambiguous
  suffixes for new model types.

---

## 7. ViewModel Rules

ViewModels are the contract between controllers and views.

Rules:

- Use ViewModels when a view needs data from multiple entities.
- Use ViewModels when a form should not bind directly to an entity.
- Use ViewModels for file upload inputs.
- Use ViewModels for select lists, summary counts, filters, and view-only state.
- Keep ViewModels free of database access and business methods.
- Validate ViewModels with data annotations or explicit service validation.
- Rebuild ViewModel supporting data after validation failures.
- Do not use `ViewBag` for complex view data when a ViewModel would be clearer.

Current opportunity:

- The existing `Services/ViewModels.cs` contains a generic `ViewModels` class for
  products and categories. Future work should prefer named, module-specific ViewModels
  with explicit responsibility.

---

## 8. Entity Framework Core Rules

Entity Framework Core is the database access boundary.

Rules:

- Use injected `NeondbContext`; do not instantiate it manually.
- Use async EF Core methods for request-path database operations.
- Use `AsNoTracking()` for read-only queries where entities will not be modified.
- Use `Include` only when related data is required by the workflow.
- Avoid N+1 query patterns by shaping queries intentionally.
- Do not call `AsEnumerable()` before filtering or materializing unless the operation
  truly must run in memory.
- Do not wrap synchronous queries in `Task.FromResult`.
- Do not call `SaveChanges` repeatedly inside loops unless required and justified.
- Use `SaveChangesAsync` consistently in async flows.
- Use transactions for multi-step writes such as order creation, stock changes, and
  cart clearing.
- Do not manually calculate primary keys in application code for production workflows.
- Let the database own identity generation where possible.

---

## 9. Database & Migration Rules

Database changes must be intentional and production-safe.

Rules:

- Schema changes must align with a module or reusable template capability.
- Do not change schema for one-client display preferences.
- Do not depend on seed/demo data for production behavior.
- Keep migrations small and reviewable when migrations are introduced.
- Name migrations according to the business change.
- Review generated migrations before applying them.
- Preserve existing data unless the task explicitly includes data migration.
- Add indexes for frequently queried fields when performance requires it.
- Use database constraints for durable invariants where appropriate.
- Do not store secrets in the database unless they are encrypted and necessary.

Current note:

- The project currently appears scaffolded from an existing PostgreSQL database and
  does not show a formal migrations folder. If migrations become the project standard,
  document and apply that workflow consistently.

---

## 10. Dependency Injection Rules

Dependency injection is the standard way to compose backend services.

Current DI is configured in `Program.cs` for MVC, EF Core, HTTP clients, filters,
authentication, and scoped services.

Rules:

- Register reusable services in `Program.cs` or a future dedicated registration
  extension method.
- Use scoped lifetime for services that depend on `NeondbContext`.
- Use typed `HttpClient` registrations for external API services.
- Avoid duplicate service registrations.
- Avoid injecting services that are not used.
- Avoid service locator patterns except at framework boundaries where no better option
  exists.
- Prefer interfaces for services that are likely to vary by provider, client, or test.
- Keep composition root configuration readable as the project grows.

Good candidates for future interfaces:

- Media storage.
- Payment provider.
- Shipping provider.
- Store settings.
- Notification provider.
- Authentication/account workflow service.

---

## 11. Validation Rules

Validation must happen server-side for all important workflows.

Rules:

- Use ViewModel validation attributes for basic input rules.
- Use service-level validation for business rules.
- Use `ModelState` to return validation errors to MVC views.
- Validate ownership before mutating user-owned data.
- Validate file uploads before storage.
- Validate status transitions.
- Validate quantities and stock before cart/order updates.
- Validate configuration values at startup or service initialization where practical.
- Do not rely on frontend validation for correctness.
- Do not continue state changes after validation failure.

Validation messages should be user-safe and should not expose internal implementation
details.

---

## 12. Authorization & Authentication Rules

Authentication and authorization must be enforced server-side.

Current project behavior:

- Cookie authentication is configured.
- Google authentication is supported.
- Admin area access is protected by custom middleware role checks.
- Some controllers/actions use `[Authorize]`.
- Claims include user id, name, phone, and role.

Rules:

- Use `[Authorize]` for authenticated workflows.
- Use role-based authorization for admin and developer workflows.
- Do not rely only on hidden navigation or frontend checks.
- Verify resource ownership for customer data such as orders, carts, and profiles.
- Use `ClaimTypes.NameIdentifier` for user id when resolving current user identity.
- Avoid resolving users by mutable display name for new code.
- Do not expose hashed or encrypted sensitive values in views.
- Use `[ValidateAntiForgeryToken]` on form POST actions.
- Keep redirect behavior safe and validate return URLs.

Future improvement:

- Move admin authorization into policies or attributes so access rules are explicit
  and testable instead of primarily path-based middleware checks.

---

## 13. Filters & Middleware Rules

Filters and middleware handle cross-cutting request concerns.

Current examples:

- `SiteStatusFilter`.
- `SiteStatusFilterAdmin`.
- Custom middleware in `Program.cs` for root redirects, admin access, and site status.

Rules:

- Use middleware for request-wide pipeline behavior.
- Use filters for MVC action-level behavior.
- Keep business decisions inside services and let filters/middleware call those services.
- Do not hardcode external API URLs directly inside filters.
- Do not create new `HttpContextAccessor` instances inside filters.
- Inject and use the registered dependencies.
- Keep filters deterministic and easy to reason about.
- Avoid duplicate checks in middleware and filters unless intentionally layered.
- Keep static assets excluded from request behavior that should only affect pages.

---

## 14. Error Handling Guidelines

Error handling must be safe for users and useful for maintainers.

Rules:

- Return `NotFound` for missing resources.
- Return `Forbid` for ownership or authorization failures.
- Use redirects with `TempData` for recoverable form/workflow errors.
- Use `ModelState` for validation errors.
- Do not expose raw exception messages to customers.
- Log unexpected exceptions instead of writing to console.
- Catch specific expected exceptions where recovery is possible.
- Let unexpected exceptions flow to the global exception handler.
- Avoid swallowing exceptions silently.
- Keep production error pages generic.

Service methods should communicate expected business failures through explicit result
types or domain-specific exceptions, not generic exception strings.

---

## 15. File Upload & Storage Rules

Cloudinary is the standard production media storage strategy for Yaqoot.

Current implementation includes a local `Image` service that validates file extension
and size, then stores images under `wwwroot/images/{subFolder}`. This is acceptable
for local development and legacy compatibility, but production modules should move
toward Cloudinary-backed media storage.

Rules:

- Validate file presence, size, extension, and content type before upload.
- Use a dedicated media storage service for upload, update, and delete behavior.
- Do not perform file-system operations directly in controllers.
- Do not store provider-specific details in views.
- Store resolved public URLs or provider identifiers consistently.
- Use Cloudinary for production product, category, logo, and content media.
- Keep local storage only as a development fallback or temporary compatibility path.
- Do not trust original file names.
- Generate safe unique file names or provider public IDs.
- Delete or replace old media only after the new media is successfully stored.
- Keep upload limits configuration-backed.

Recommended future boundary:

- Replace or wrap `Image` with an interface such as `IMediaStorageService` and provide
  a Cloudinary implementation as the production default.

---

## 16. Configuration & Options Pattern

Configuration must own environment-specific and client-specific values.

Rules:

- Do not hardcode connection strings, API URLs, credentials, encryption keys, upload
  limits, or provider settings in code.
- Use `appsettings.json`, environment variables, or secret management as appropriate.
- Bind related configuration into options classes for reusable services.
- Validate required configuration values at startup where practical.
- Use named options for providers when multiple implementations may exist.
- Keep default development values safe and non-secret.
- Do not commit production secrets.
- Keep client branding and store settings outside core code where practical.

Recommended option groups:

- `StorageOptions`.
- `CloudinaryOptions`.
- `AuthenticationOptions`.
- `StoreSettingsOptions`.
- `ExternalApiOptions`.
- `UploadOptions`.

---

## 17. API Integration Rules

External APIs must be isolated behind services.

Current external integration examples:

- `DealingAPI` for site status and phone decryption helper behavior.
- Google authentication.
- Planned/standard production media storage through Cloudinary.

Rules:

- Use typed `HttpClient` services for external APIs.
- Put base URLs and credentials in configuration.
- Do not hardcode external URLs in services, filters, or controllers.
- Use timeouts and failure handling appropriate to the workflow.
- Treat external API failures as expected operational events.
- Do not block normal commerce workflows on non-essential external APIs.
- Deserialize into DTOs, not entities.
- Keep DTOs separate from persistence models.
- Log failures with enough context to debug safely.
- Do not write integration errors to console in production code.

---

## 18. Logging Guidelines

Logging should support production diagnosis without exposing sensitive data.

Rules:

- Use `ILogger<T>` for backend logging.
- Do not use `Console.WriteLine` for production diagnostics.
- Log unexpected exceptions.
- Log important business events where useful, such as order creation failure, upload
  failure, external provider failure, and suspicious account activity.
- Do not log passwords, raw phone numbers, tokens, cookies, secrets, or full connection
  strings.
- Include identifiers such as order id, product id, user id, and provider name when
  safe and useful.
- Keep user-facing error messages separate from logs.

Future opportunity:

- Add structured logging conventions for commerce events and security events.

---

## 19. Performance Guidelines

Backend performance should be predictable and simple.

Rules:

- Use async database and network operations.
- Use `AsNoTracking()` for read-only list/detail pages.
- Avoid N+1 queries by using `Include`, projections, or targeted query shaping.
- Avoid loading entire tables when filters can run in the database.
- Avoid repeated `SaveChanges` inside loops.
- Avoid unnecessary dependency resolution inside request middleware.
- Cache only when there is a real performance need and invalidation is clear.
- Keep admin dashboard queries efficient and bounded.
- Keep external API calls out of hot paths unless necessary.
- Use pagination for large admin and storefront lists when data grows.
- Project to ViewModels when a view does not need full entities.

---

## 20. Security Guidelines

Security rules are mandatory for production backend work.

Rules:

- Keep secrets out of source control.
- Use secure password hashing.
- Use constant-time comparison for password verification.
- Protect form POST actions with anti-forgery tokens.
- Enforce authorization on the server.
- Validate resource ownership.
- Validate and sanitize file uploads.
- Do not expose internal exception details.
- Do not weaken cookie security settings for production.
- Use strict CORS policies for production.
- Avoid storing sensitive values in plaintext.
- Use HTTPS and forwarded headers correctly behind hosting proxies.
- Keep Google authentication configuration in secrets or environment configuration.
- Review all admin actions for role enforcement.

Current critical issue:

- `NeondbContext.OnConfiguring` contains a scaffolded hardcoded PostgreSQL connection
  string. This must be removed or neutralized in favor of configured connection strings.

---

## 21. Reusable Module Guidelines

Backend modules should be cohesive and reusable across future clients.

A module should define:

- Public or admin controllers.
- Service operations.
- Input/ViewModels.
- Persistence models or schema changes, if required.
- Validation rules.
- Authorization rules.
- Configuration values, if required.
- External provider boundaries, if required.

Rules:

- Build modules around commerce capabilities, not pages alone.
- Keep public and admin workflows separate when their responsibilities differ.
- Share services between public and admin workflows when business behavior is the same.
- Avoid copying query and mutation logic between controllers.
- Add configuration for client-specific behavior.
- Do not add one-client rules into reusable module services.
- Keep module dependencies explicit through constructor injection.
- Document new reusable services or provider assumptions when introduced.

---

## 22. Backend Anti-Patterns

The following patterns are not allowed for new backend work:

- Fat controllers.
- Business logic in Razor Views.
- Repeated business logic across controllers.
- Direct file-system operations in controllers.
- Hardcoded connection strings, credentials, API URLs, or provider secrets.
- Manual primary key generation in production workflows.
- Synchronous database calls inside async request paths.
- `Task.FromResult` wrappers around already executed synchronous EF queries.
- Repeated `SaveChanges` inside loops without a transaction.
- Public mutable fields on scoped services for operation results.
- Catching broad exceptions and ignoring them.
- Writing production diagnostics with `Console.WriteLine`.
- Returning raw exception messages to users.
- Using `ViewBag` as the primary contract for complex data.
- Binding admin/customer forms directly to entities when a ViewModel is safer.
- Storing production media only in local `wwwroot` folders.
- Relying on frontend checks for authorization or validation.

---

## 23. Client Customization Guidelines

Client customization must be configuration-backed and isolated from core backend logic.

Allowed customization areas:

- Store name and identity.
- Contact details.
- Social links.
- Logo and media assets.
- Theme settings.
- Shipping policy values.
- Payment provider settings.
- Cloudinary folder or transformation settings.
- Feature enablement for already-supported optional capabilities.

Rules:

- Prefer configuration or Store Settings over source-code changes.
- Do not add client-specific branches to core services.
- Do not fork controllers for branding differences.
- Do not change shared persistence models only for display preferences.
- Promote broadly reusable client needs into template modules.
- Isolate truly unique client behavior behind extension services or configuration.

---

## 24. Code Review Checklist

Before approving backend changes, verify:

- Controllers remain thin.
- Business logic lives in services or dedicated backend helpers.
- Existing services were reused where appropriate.
- New services are registered in dependency injection.
- Async database and network calls are used correctly.
- No hardcoded secrets, connection strings, API URLs, or upload limits were added.
- Configuration values are used through configuration or options.
- Entity changes are intentional and persistence-safe.
- ViewModels are used for forms and mixed view data.
- Model validation and service validation are both handled where needed.
- POST actions include anti-forgery validation.
- Authorization and ownership checks are server-side.
- File uploads are validated and routed through a storage service.
- Cloudinary remains the production media storage direction.
- External APIs are isolated behind services and typed clients.
- Error handling does not expose internal exception details.
- Logging uses `ILogger<T>` and avoids sensitive data.
- Queries avoid obvious N+1 and unnecessary full-table loading.
- No one-client behavior was added to reusable core modules.

---

## Existing Backend Strengths

- The project already uses ASP.NET Core MVC with clear public and admin controller
  surfaces.
- Dependency injection is used for core services, filters, EF Core, and HTTP clients.
- EF Core is configured for PostgreSQL with retry behavior through Npgsql.
- Cookie authentication and Google authentication are already integrated.
- Several reusable services exist for cart, orders, products, images, visits, shipping,
  and external site status.
- Admin workflows are isolated in an Admin area.
- Anti-forgery validation is present on several form POST actions.
- File upload validation already checks size and extension in the image service.
- Product/category/order/user modules have recognizable backend boundaries.
- Site status behavior is centralized through middleware and filters rather than
  scattered across every view.

---

## Existing Backend Inconsistencies

- Controllers mix service usage with direct `DbContext` access.
- Some business logic lives in controllers instead of services, especially account,
  product management, and user resolution workflows.
- Some services wrap synchronous EF Core queries in `Task.FromResult`.
- Some workflows use manual primary key generation.
- Some methods mix synchronous `SaveChanges` with async request flows.
- Product and category media handling mixes local storage paths with the intended
  Cloudinary production strategy.
- Some configuration values are hardcoded, including external API URLs and upload limits.
- Role checks are split between attributes, middleware, and custom filters.
- Model/ViewModel/DTO naming is inconsistent.
- Some controllers contain duplicate using directives and legacy/commented code.
- Current user lookup sometimes depends on display name instead of stable user id claims.

---

## Backend Technical Debt Discovered

- `NeondbContext.OnConfiguring` contains a hardcoded PostgreSQL connection string and
  scaffold warning.
- `Image` stores production-like media under `wwwroot` and should be replaced or wrapped
  by Cloudinary-backed storage.
- `ProductService`, `CartService`, and `OrderService` need stronger async EF usage and
  query shaping.
- Order creation performs multiple writes without an explicit transaction and saves
  repeatedly inside a loop.
- Services use magic strings for statuses and some user-facing messages.
- `CartService` exposes a mutable public `MESSAGE` field for operation feedback.
- External site status integration hardcodes remote URLs and uses console output on
  errors.
- Account workflows currently contain hashing, encryption, validation, persistence,
  sign-in, and Google response handling in one controller.
- Admin authorization is partly enforced through path-based middleware rather than
  a formal policy/attribute model.
- The project lacks clear interface boundaries for storage, external APIs, account
  workflows, settings, and provider-specific integrations.

---

## Recommendations for Improving Long-Term Backend Maintainability

- Remove the hardcoded connection string from `NeondbContext.OnConfiguring` and rely
  only on configured connection strings.
- Introduce an `IMediaStorageService` with Cloudinary as the production implementation
  and local storage only as a development fallback if needed.
- Move account registration, login, password hashing, phone encryption, and profile
  mutation into dedicated account/security services.
- Replace manual id generation with database-managed identity or sequence behavior.
- Refactor service methods to use async EF Core queries directly.
- Add explicit transactions for order creation and any multi-record write workflow.
- Introduce constants or enums for order statuses, roles, and reusable state values.
- Replace mutable service feedback fields with result objects.
- Move external API base URLs and upload limits into options classes.
- Add service interfaces for provider-varying areas such as media, payment, shipping,
  notifications, external status, and settings.
- Consolidate authorization into policies or attributes for admin and developer access.
- Add focused service-level tests for cart, checkout, order creation, media validation,
  and account workflows when testing infrastructure is introduced.
