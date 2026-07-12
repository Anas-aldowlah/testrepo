# Yaqoot Architecture Reference

This document is the authoritative architectural reference for the Yaqoot project.
It defines the stable structure, responsibilities, and extension rules for a reusable
production-ready e-commerce template.

Yaqoot is not treated as a one-off client application. It is a commercial template
intended to support multiple future stores with minimal architectural change.

---

## 1. Purpose

The purpose of this document is to preserve a stable architecture that can be reused,
extended, and customized safely across future client deployments.

This document defines:

- The boundaries of the Core Architecture.
- The responsibilities of each architectural layer.
- The rules for adding or modifying modules.
- The allowed customization points for client-specific branding, content, and business settings.
- The architectural constraints that protect long-term maintainability.

This document does not define task workflow, development process, or historical
decision records. Those responsibilities belong to `WORKFLOW.md` and `DECISIONS.md`.

---

## 2. Architectural Vision

Yaqoot is a modular ASP.NET Core MVC e-commerce template for small-to-medium
commercial businesses. The architecture should provide a complete storefront,
customer experience, and administration experience while remaining simple enough
to maintain, adapt, and deploy repeatedly.

The system should be reusable by default. Features that are common to commerce
stores belong in the template. Features that are unique to one client should be
isolated behind configuration, content, theme assets, or dedicated extension modules.

The long-term architectural goal is:

- A stable core that rarely changes.
- Client customization that does not require rewriting core modules.
- Reusable modules for commerce capabilities.
- Predictable MVC boundaries.
- Clear separation between presentation, workflow orchestration, business services,
  persistence, and infrastructure concerns.

---

## 3. Core Principles

The architecture is governed by the following principles:

- Core commerce behavior must be reusable across client deployments.
- Client-specific behavior must be isolated from reusable template code.
- MVC boundaries must remain explicit and predictable.
- Controllers coordinate requests; they do not own business rules.
- Views present data; they do not query persistence or implement domain workflows.
- Services own reusable business operations and integration behavior.
- Models represent persisted data, request data, or view-facing shapes with clear intent.
- Shared UI and shared client-side behavior must be extracted into reusable components.
- Configuration must be preferred over source modification for deployment-specific values.
- Simplicity is preferred over enterprise complexity unless a real template need exists.
- New modules must integrate through existing routing, layout, service, validation,
  authorization, and persistence patterns.

---

## 4. System Architecture

Yaqoot follows a server-rendered MVC architecture with modular areas for major
application experiences.

The primary system surfaces are:

- Public Storefront: browsing, category discovery, product discovery, cart entry points,
  informational pages, and public brand experience.
- Customer Experience: authentication, profile, checkout, order placement, and customer
  order visibility.
- Administration Area: operational workflows for managing products, categories, orders,
  users, and future store settings.
- Shared Platform Services: persistence, file handling, external service integration,
  site status checks, authentication, authorization, and reusable UI assets.

The architecture is intentionally layered:

1. HTTP routing and middleware receive requests and enforce cross-cutting policies.
2. Controllers and area controllers select workflows and prepare responses.
3. Services perform reusable business operations and integration work.
4. Persistence models and the database context provide data access boundaries.
5. Razor views, layouts, partials, CSS, and JavaScript render the user experience.
6. External providers supply infrastructure such as database hosting, image storage,
   hosting, authentication, and deployment.

The Core Architecture is the reusable structure that supports these surfaces.
Client Customization is the set of allowed changes that adapt the template for a
specific store without changing core behavior.

---

## 5. Technology Stack

The current project is built on the following technology choices:

- Application framework: ASP.NET Core MVC.
- Runtime target: .NET 9.
- View rendering: Razor Views.
- Persistence access: Entity Framework Core.
- Database provider: PostgreSQL through Npgsql.
- Authentication: cookie authentication with external Google authentication support.
- Static assets: modular CSS and JavaScript under `wwwroot`.
- UI foundation: Bootstrap, jQuery, and project-specific CSS and JavaScript.
- Hosting target: Render.
- Database hosting target: Neon PostgreSQL.
- File and image storage target: Cloudinary, with local public image assets also present.
- Source control: GitHub.

Architecturally, these are implementation choices serving the MVC template. Future
changes to providers should preserve the same boundaries: controllers remain thin,
services own infrastructure interaction, configuration supplies environment values,
and modules do not depend directly on deployment details.

---

## 6. Project Structure

The project structure should communicate architectural responsibility.

Current top-level responsibilities:

- `Areas/Admin`: administration module surface, including admin controllers and views.
- `Controllers`: public storefront and customer-facing controllers.
- `Views`: public storefront, customer-facing views, shared layouts, and partials.
- `Models`: persisted entities, data transfer shapes, and view-facing models.
- `Services`: reusable business services, integration services, and workflow helpers.
- `Filters`: reusable request filters and cross-cutting MVC policies.
- `wwwroot/css`: global, shared, area-specific, and page-specific styles.
- `wwwroot/js`: global, shared, area-specific, and page-specific browser behavior.
- `wwwroot/images`: static template assets, placeholders, and uploaded public assets.
- `Program.cs`: application composition root for dependency registration, middleware,
  routing, authentication, authorization, and provider setup.
- `Properties`: local and deployment profile metadata.
- Documentation files: project status, workflow, decision records, and architecture.

The structure should remain recognizable as the template grows. New modules should
be added in locations that match their application surface and responsibility.

---

## 7. Layer Responsibilities

Each layer has a distinct responsibility.

Presentation layer:

- Razor views, layouts, and partials render user interfaces.
- Views consume prepared models and view data only.
- Views may compose reusable components but must not own business decisions.
- Page-specific CSS and JavaScript may enhance presentation and interaction.

Controller layer:

- Controllers receive requests, validate request intent, call services, and choose responses.
- Controllers enforce workflow-level access and response decisions.
- Controllers should not directly contain reusable commerce rules.
- Controllers should not duplicate logic already available in services.

Service layer:

- Services own reusable business behavior and integration workflows.
- Services provide stable operations for controllers and modules.
- Services isolate persistence, file handling, external APIs, and complex workflow logic.
- Services should be designed for reuse across public, customer, and admin surfaces.

Persistence layer:

- Entity Framework Core and the database context define the persistence boundary.
- Persistence models represent durable business data.
- Query behavior should remain predictable and should not leak into views.
- Database provider details must not shape presentation-layer code.

Infrastructure layer:

- Middleware, authentication, external providers, HTTP clients, file providers, and hosting
  configuration belong at the application boundary.
- Infrastructure concerns must be registered centrally and consumed through services.
- Environment-specific values must come from configuration, not hardcoded code paths.

Client customization layer:

- Client identity, branding, static content, store settings, contact information,
  social links, visual theme tokens, and provider credentials belong outside core logic.
- Customization should be reversible and upgrade-friendly.

---

## 8. MVC Rules

Yaqoot uses MVC as an architectural boundary, not only as a framework convention.

Controllers must:

- Stay focused on request orchestration.
- Delegate reusable business work to services.
- Return appropriate views, redirects, status codes, or files.
- Use dependency injection for services and infrastructure dependencies.
- Preserve route conventions unless a module explicitly requires a new route.

Views must:

- Render prepared data.
- Use shared layouts and partials where appropriate.
- Keep markup semantic and maintainable.
- Avoid persistence access, service calls, and business rule calculation.
- Avoid embedding client-specific values that should come from configuration or content.

Models must:

- Have clear responsibility as persisted entities, request/input models, view models,
  or integration DTOs.
- Avoid mixing unrelated responsibilities in one type.
- Protect persistence models from view-only formatting concerns where practical.

Services must:

- Provide cohesive operations with clear ownership.
- Avoid becoming catch-all utility containers.
- Hide infrastructure details from controllers and views.
- Be reusable by multiple modules when the behavior is shared.

---

## 9. Module Architecture

A module is a cohesive business capability with its own workflows, views, assets,
and service dependencies.

Examples of current and expected modules include:

- Products.
- Categories.
- Cart.
- Checkout and orders.
- Users and accounts.
- Dashboard.
- Store settings.
- Content pages.

Each module should define:

- Its user-facing or admin-facing surface.
- Its controller actions.
- Its view models or view-facing data.
- Its service dependencies.
- Its validation and authorization requirements.
- Its page-specific assets, if needed.
- Its reusable components, if introduced.

Public modules belong in the standard MVC surface unless they are part of a distinct
area. Administrative modules belong under the Admin area. Future large surfaces may
use additional areas only when they represent a real application boundary.

New modules must integrate with existing architecture by:

- Registering services through dependency injection.
- Reusing existing layouts and shared components.
- Following established routing conventions.
- Keeping CSS and JavaScript modular.
- Using existing persistence patterns.
- Preserving authentication and authorization rules.
- Avoiding direct coupling to client-specific branding or deployment details.

---

## 10. Shared Components

Shared components are reusable UI or behavior units that reduce duplication and
protect consistency across modules.

Shared components may include:

- Razor partials.
- Layouts.
- View components, where appropriate.
- Shared CSS modules.
- Shared JavaScript modules.
- Reusable view models.
- Reusable service operations.

Shared components should be introduced when:

- The same UI pattern appears in multiple modules.
- A behavior must remain consistent across surfaces.
- A template-level capability will be reused by future clients.
- Duplication would increase maintenance risk.

Shared components must remain generic enough for template reuse. They may accept
configuration, model data, and styling hooks, but they should not contain one-client
business assumptions.

Core shared components should not be modified for a single client unless the change
improves the reusable template. Client-specific variations should be implemented
through configuration, theme assets, content data, or isolated extension components.

---

## 11. Data & Persistence

The persistence architecture is responsible for durable commerce data and operational
state. It should remain independent of presentation concerns.

Persistence rules:

- The database context is the application boundary for database access.
- Entities should represent durable business concepts.
- Data access should be mediated through services when used by application workflows.
- Views must not directly query the database.
- Business workflows should not depend on database-provider-specific behavior unless
  there is no practical alternative.
- Schema changes must be intentional and aligned with module ownership.
- Seed, sample, or demo data must not be required for production behavior.

Commerce data should be modeled around stable concepts such as products, categories,
carts, orders, users, visits, messages, and security logs. Future entities should be
added only when they represent durable business state or integration requirements.

---

## 12. File Storage Strategy

File storage is an infrastructure concern and must not leak into views or unrelated
business logic.

The template supports static public assets and uploaded commerce media. The architecture
should treat uploaded files, product images, category images, logos, and brand assets
as replaceable resources.

Rules:

- File upload validation belongs in services or dedicated validation policies.
- File size, type, and storage-provider limits must be enforced consistently.
- Storage provider interaction must be isolated behind a service boundary.
- Views should consume resolved URLs or asset references, not storage-provider details.
- Static template assets may live under `wwwroot`.
- Client-specific brand assets should be replaceable without modifying core workflows.
- Production media should use a durable external storage provider when practical.
- Local development assets must not become required production dependencies unless
  explicitly part of the template.

The Core Architecture should define how files are accepted, validated, stored, and
referenced. Client Customization should define which images, logos, and media are used.

---

## 13. Configuration Strategy

Configuration is the primary mechanism for deployment-specific and client-specific
values.

Configuration should own:

- Database connection strings.
- External authentication credentials.
- Storage provider credentials.
- Hosting and proxy assumptions.
- Store identity.
- Store contact information.
- Social links.
- Feature flags where justified.
- Operational limits such as upload size, where appropriate.

Rules:

- Secrets must not be committed to source control.
- Environment-specific values must not be hardcoded.
- Client-specific business identity must be configurable whenever practical.
- Configuration names should be stable and documented when they become part of the template.
- Defaults may exist for local development, but production deployments must provide
  real values through the hosting environment or secret management.

Configuration should not become a substitute for architecture. Use configuration for
values and switches, not for encoding complex business workflows.

---

## 14. Client Customization Strategy

Client Customization is the controlled layer where each future store adapts the
template without destabilizing the reusable core.

Allowed customization areas:

- Logo and brand assets.
- Color palette and visual theme tokens.
- Store name and business identity.
- Contact information.
- Social links.
- Static page content.
- Product and category data.
- Shipping and store policy values.
- Payment and fulfillment provider settings, when those providers are supported.
- Optional feature enablement, when the feature is already architected for toggling.

Client customization should avoid:

- Editing core service contracts for one store.
- Changing shared layouts in ways that break other modules.
- Hardcoding business values into controllers, views, or services.
- Forking modules for superficial branding differences.
- Changing persistence models only to support display text.
- Coupling a reusable module to one client's operational process.

Core Architecture should remain stable across clients. Client-specific needs should
be implemented through data, configuration, theme overrides, or isolated extension
modules. When a client requirement is broadly reusable, it should be promoted into
the Core Architecture through a deliberate architectural change.

---

## 15. Extension Rules

Future extensions must protect the template's upgrade path.

Extension rules:

- Add new modules only around cohesive business capabilities.
- Prefer extending services over duplicating controller logic.
- Prefer new configuration values over hardcoded client branches.
- Prefer reusable components over copied markup.
- Keep public, customer, and admin surfaces separated.
- Keep module-specific assets in module-specific asset folders.
- Register dependencies through the composition root.
- Respect existing authentication, authorization, validation, and routing patterns.
- Add persistence changes only when required by durable business state.
- Avoid adding external dependencies unless they provide clear template-level value.

Core files that should not be modified for client-specific behavior:

- Application startup and middleware composition, except for provider registration
  or template-level platform changes.
- Shared service contracts and reusable business services.
- Core persistence entities and database context definitions.
- Shared layouts and shared components, unless improving the reusable template.
- Authentication and authorization policy structure.
- Global CSS and JavaScript foundations.
- Module routing conventions.

If an extension requires changing these core areas, it should be treated as a template
architecture change, not a client customization.

---

## 16. Performance Principles

Performance should be designed into module boundaries and data access patterns.

Principles:

- Server-rendered pages should load only the data required for the current workflow.
- Controllers should avoid unnecessary repeated service calls.
- Services should shape queries for the consuming workflow.
- Views should not perform expensive computation.
- Shared components should avoid hidden data access.
- Static assets should remain modular and scoped to the pages that need them.
- Images should be validated, resized, compressed, and served from appropriate storage.
- Database calls should be predictable and should avoid unnecessary loading of related data.
- External service calls should be isolated, timeout-aware, and resilient where practical.
- Admin pages should prioritize fast scanning and efficient workflows.

Performance improvements should preserve architectural clarity. Avoid caching,
background processing, or advanced infrastructure until the project has a real need.

---

## 17. Security Principles

Security is a core architectural concern for the storefront, customer experience,
and administration area.

Principles:

- Authentication must be centralized through the framework authentication system.
- Administration access must require authenticated users with appropriate roles.
- Authorization rules must be enforced server-side.
- Client-side checks may improve experience but must never be the only protection.
- User input must be validated before persistence or workflow execution.
- File uploads must validate size, type, and storage behavior.
- Secrets must come from secure configuration sources.
- Cookies must use appropriate security, lifetime, and same-site settings.
- External authentication must be configured through provider settings.
- Error handling must avoid exposing sensitive details in production.
- Administrative workflows must not rely on obscurity or hidden navigation.
- Cross-origin policies must be restrictive enough for production deployment needs.

Security decisions that affect all clients belong in the Core Architecture. Client
deployments may supply credentials and policy values, but should not weaken core
authorization or validation behavior.

---

## 18. Architectural Anti-Patterns

The following patterns are not allowed in the architecture:

- Hardcoding client business data in controllers, views, services, or JavaScript.
- Adding one-client behavior directly into reusable modules.
- Querying the database from views.
- Placing reusable business logic in controllers.
- Duplicating service logic across public and admin controllers.
- Creating large utility services with unrelated responsibilities.
- Mixing admin and public workflows in the same module without a clear boundary.
- Adding page-specific CSS or JavaScript to global files without reusable purpose.
- Copying shared UI markup instead of extracting a component.
- Storing secrets in source-controlled configuration files.
- Letting storage provider details leak into views.
- Adding external dependencies for narrow client-specific needs.
- Treating static demo assets as production data.
- Making technical debt changes while implementing unrelated module features.
- Introducing enterprise patterns that exceed the needs of small-to-medium businesses.

These anti-patterns reduce reuse, increase client upgrade cost, and weaken the
commercial value of the template.

---

## 19. Future Growth

Yaqoot should grow by adding cohesive modules and strengthening reusable platform
capabilities.

Expected growth areas:

- Customer account workflows.
- Store settings and business configuration.
- Payment integration.
- Shipping and delivery configuration.
- Inventory management.
- Discounts and promotions.
- Content management for public pages.
- Notification workflows.
- Reporting and lightweight analytics.
- Multi-client deployment guidance.
- Production observability and operational health.

Growth rules:

- Add features as modules, not scattered code changes.
- Promote broadly useful client needs into reusable template capabilities.
- Keep advanced features optional unless they are essential to normal commerce.
- Prefer incremental architecture evolution over large rewrites.
- Preserve existing module contracts when possible.
- Document new architectural decisions when they change project direction.

Future growth should not turn Yaqoot into an enterprise platform by default. The
template should remain practical, elegant, and maintainable for small-to-medium
commercial stores.

---

## 20. Architecture Stability

Architecture stability means future developers and AI coding agents can add features
without rediscovering the project's structure each time.

Stable architecture requires:

- Consistent MVC boundaries.
- Clear distinction between Core Architecture and Client Customization.
- Reusable services and components.
- Modular CSS and JavaScript.
- Centralized configuration.
- Intentional persistence changes.
- Controlled external dependencies.
- Documentation that reflects real architectural decisions.

Core Architecture should change only when:

- A reusable template capability requires it.
- A production-readiness issue cannot be solved inside an existing boundary.
- A current boundary creates repeated duplication or maintenance risk.
- A new provider or module requires a deliberate platform-level integration point.

Core Architecture should not change for:

- One client's branding.
- One client's copy or content.
- One client's temporary process.
- A single page's visual preference.
- A shortcut around service, validation, authorization, or persistence boundaries.

When architecture changes are necessary, update the relevant documentation and record
new decisions in the decision log instead of silently changing the template rules.

---

## Architecture Assumptions

- Yaqoot remains an ASP.NET Core MVC application using server-rendered Razor views.
- The template continues to target small-to-medium commercial businesses.
- Public storefront, customer workflows, and admin workflows remain separate surfaces.
- PostgreSQL remains the primary relational persistence provider for production.
- External image storage remains the preferred production strategy for uploaded media.
- Client customization is expected to happen through configuration, content, and
  theme assets before source-code changes.
- The Admin area remains the primary operational surface for store management.

---

## Architectural Inconsistencies Discovered

- The project uses both external image storage as the intended strategy and local
  product/category image folders under `wwwroot`, so the long-term media ownership
  boundary should be clarified.
- Business and deployment values are still represented in mixed locations, including
  configuration, static assets, and code-level defaults. The architecture should move
  toward configuration-backed store settings.
- Some model names suggest mixed responsibilities between persisted entities, view
  models, and DTOs. Clearer model naming and placement would strengthen boundaries.
- Cross-cutting request behavior currently appears in startup middleware and filters.
  The ownership between middleware, filters, and services should be documented as the
  platform matures.
- CORS policy appears broadly permissive. Production deployments should define a
  stricter policy aligned with the hosting and client access model.
- The project status identifies Admin Settings as not implemented, while the architecture
  expects store configuration to become a first-class customization mechanism.

---

## Recommendations for Improving Long-Term Maintainability

- Introduce a formal Store Settings module to move business identity, contact details,
  social links, theme values, and policy values out of code and static markup.
- Establish explicit folders or naming conventions for entities, view models, input
  models, and integration DTOs.
- Define a storage abstraction for product, category, logo, and content images so
  local and external storage follow the same architectural contract.
- Tighten production configuration for CORS, cookies, upload limits, and provider
  credentials through environment-specific settings.
- Add lightweight module documentation for each major module once it is completed.
- Keep shared UI components registered in project documentation so future modules reuse
  them instead of duplicating markup.
- Consider interface boundaries for services that are likely to vary by client or
  provider, such as file storage, payment, shipping, notifications, and settings.
- Add automated architecture checks where practical, such as build checks, formatting,
  and focused tests for service-level workflows.
