# Yaqoot Product Roadmap

This document is the official long-term product roadmap for the Yaqoot e-commerce
template. It describes how the template should evolve across phases while preserving
reuse, stability, and maintainability for future client deployments.

Yaqoot is not a one-client project. It is a reusable production-ready commerce
template for small-to-medium businesses.

---

## 1. Purpose

The purpose of this document is to define the long-term evolution of the Yaqoot
template.

This roadmap exists to:

- Keep product growth intentional.
- Separate completed, in-progress, planned, and deferred work.
- Prevent client-specific requests from distorting the reusable template.
- Guide future modules without breaking existing deployments.
- Preserve upgrade paths for future client stores.
- Keep development focused on production-ready commerce capabilities.

`PROJECT_STATUS.md` remains the short current-state tracker. This document is the
durable product roadmap.

---

## 2. Roadmap Philosophy

Yaqoot should grow through controlled, reusable phases.

Roadmap principles:

- Build template capabilities, not one-client features.
- Complete existing workflows before adding unrelated systems.
- Favor small-to-medium business needs over enterprise complexity.
- Add modules only when they support normal commerce, administration, customization,
  or production readiness.
- Preserve backward compatibility for existing client deployments whenever practical.
- Keep client customization isolated through configuration, content, branding, and
  extension points.
- Treat technical debt as roadmap work when it affects template reuse, stability,
  security, or maintainability.

Each phase should leave the template more reusable than before.

---

## 3. Phase Structure

Each phase represents a major product maturity milestone.

Phase states:

- Completed: the phase has passed audit and requires no essential remaining work.
- In Progress: the phase is actively being developed or polished.
- Planned: the phase is expected but not yet active.
- Deferred: the work is intentionally postponed to a later phase.

Phase types:

- Product capability phases add or complete customer-facing or admin-facing workflows.
- Production readiness phases improve security, performance, accessibility, reliability,
  deployment, and maintainability.
- Template evolution phases improve reuse, customization, documentation, and upgrade
  paths for future clients.
- Optional growth phases add advanced commerce capabilities only after the core template
  is stable.

---

## 4. Completed Phases

### Phase 3: Admin Dashboard

Status: Completed.

Phase 3 closed after verification of the existing Admin area structure, including
controllers, views, shared layout, module styles, module scripts, and completed
administrative workflows for dashboard, products, categories, orders, and users.

Completed module-level work is summarized in `PROJECT_STATUS.md`.

Deferred from Phase 3:

- Admin Settings.
- Technical Debt Sprint.

Deferred work is carried into Phase 4 and must not be treated as completed Phase 3
scope.

---

## 5. Current Phase Status

Current version:

- `v1.0.0-alpha.1`.

Current active phase:

- Phase 4: Production Readiness.

Current state:

- Phase 1: Public Storefront is In Progress.
- Phase 2: Customer Experience is Planned / Not Started.
- Phase 3: Admin Dashboard is Completed.
- Phase 4: Production Readiness is Planned / Next.

Deferred into Phase 4:

- Admin Settings is deferred to Phase 4 and must not be marked complete until a real
  Settings module exists.
- Technical Debt Sprint is deferred to Phase 4 and should be handled as dedicated
  production-readiness work.

Important note:

- The current roadmap order reflects active project history, not strict future
  sequencing. Future work should still complete modules deliberately and avoid starting
  broad new systems before essential existing workflows are stable.

---

## 6. Future Planned Phases

The following roadmap is the recommended long-term direction for the reusable
template.

### Phase 1: Public Storefront

Status: In Progress.

Goal:

- Complete the customer-facing browsing and shopping surface.

Scope:

- Home.
- Shared storefront layout.
- Product listing.
- Product details.
- Category browsing.
- Search.
- Cart.
- Checkout entry.
- Order confirmation.
- About.
- Contact.

Completion outcome:

- A guest can browse real catalog data and move naturally from discovery to cart and
  checkout.

### Phase 2: Customer Experience

Status: Planned.

Goal:

- Complete account, identity, and self-service customer workflows.

Scope:

- Login.
- Register.
- Profile.
- My Orders.
- Order Details.
- Password recovery.
- Customer account validation and security review.

Completion outcome:

- A customer can authenticate, manage basic profile data, and review their own orders
  without admin assistance.

### Phase 3: Admin Dashboard

Status: Completed.

Goal:

- Provide a productive operational dashboard for store administrators.

Completed scope:

- Admin layout.
- Dashboard.
- Products.
- Categories.
- Orders.
- Users index and details.
- Admin UI polish.
- Admin production readiness review.

Deferred from this phase:

- Admin Settings.
- Technical Debt Sprint.

Completion outcome:

- Administrators can manage the core store catalog, orders, users, and operational
  views through a polished admin area.

### Phase 4: Production Readiness

Status: Planned.

Goal:

- Prepare the template for reliable production deployment and future client reuse.

Scope:

- Admin Settings module.
- Technical Debt Sprint.
- Responsive review.
- UI and UX review.
- Accessibility review.
- Security review.
- Performance review.
- Architecture review.
- Backend maintainability review.
- Frontend maintainability review.
- Configuration hardening.
- Cloudinary production media storage.
- Store settings and business identity configuration.
- Final polish.

Completion outcome:

- The template can be deployed confidently and reused for future clients with minimal
  source-code changes.

### Phase 5: Template Customization Foundation

Status: Suggested / Planned.

Goal:

- Make client setup safer, faster, and more configuration-driven.

Scope:

- Store Settings expansion.
- Theme token customization.
- Logo and brand asset management.
- Contact details.
- Social links.
- Footer content.
- Announcement content.
- Static content customization.
- Client setup checklist.
- Upgrade-safe customization guidance.

Completion outcome:

- A new client deployment can be branded and configured without modifying core modules.

### Phase 6: Commerce Operations

Status: Suggested / Planned.

Goal:

- Add practical commerce management capabilities needed by small-to-medium stores.

Scope:

- Inventory rules.
- Stock history or stock adjustment workflow.
- Discounts and promotions.
- Shipping policy configuration.
- Order status workflow improvements.
- Lightweight reporting.
- Export-ready admin data, if required.

Completion outcome:

- Store operators can manage common commerce operations beyond basic catalog and orders.

### Phase 7: Integrations

Status: Suggested / Planned.

Goal:

- Introduce provider-backed integrations through stable service boundaries.

Scope:

- Payment provider integration.
- Shipping or delivery provider integration.
- Notification provider integration.
- Email or SMS transactional messages.
- Cloudinary completion and media transformations.
- Provider configuration and failure handling.

Completion outcome:

- The template can connect to real production commerce providers while preserving
  provider-replaceable service boundaries.

### Phase 8: Content & Marketing

Status: Suggested / Planned.

Goal:

- Support reusable content and merchandising workflows.

Scope:

- Public page content management.
- Featured products.
- Featured categories.
- Campaign sections.
- SEO metadata management.
- Simple banner or announcement management.
- Reusable content blocks.

Completion outcome:

- Clients can update marketing and storefront content without developer intervention.

### Phase 9: Observability & Maintenance

Status: Suggested / Planned.

Goal:

- Improve production visibility and long-term maintainability.

Scope:

- Structured logging.
- Health checks.
- Operational diagnostics.
- Security event review.
- Backup and recovery guidance.
- Error tracking readiness.
- Maintenance checklist.
- Upgrade checklist.

Completion outcome:

- Production deployments are easier to monitor, diagnose, maintain, and upgrade.

---

## 7. Feature Classification

Every roadmap item should be classified before implementation.

Essential features:

- Required for normal store operation.
- Complete an existing workflow.
- Protect security, correctness, or production readiness.
- Examples: checkout correctness, order details, user details, file upload validation,
  admin settings, configuration hardening.

Template features:

- Reusable across multiple client deployments.
- Improve the commercial value of the template.
- Examples: theme configuration, store settings, provider interfaces, reusable content
  blocks, shared reporting.

Client-specific features:

- Needed by one client but not broadly reusable.
- Should be isolated through configuration, content, branding, or extension modules.
- Should not be added directly to core modules unless promoted into a reusable feature.

Optional enhancements:

- Useful but not required for core operation.
- Should be deferred when they increase complexity before the template is stable.
- Examples: export, print, bulk actions, advanced analytics, promotional campaigns.

Technical debt:

- Internal maintainability, security, or scalability work.
- Should be scheduled deliberately and not mixed into unrelated feature tasks.

---

## 8. Release Strategy

Yaqoot should use controlled releases that communicate template maturity.

Release stages:

- Alpha: active development, incomplete phases, structural changes expected.
- Beta: core workflows are present, production-readiness work is underway.
- Release candidate: production behavior is stable and audit issues are limited.
- Stable: suitable as a reusable client template baseline.

Release rules:

- Patch releases should fix bugs, security issues, and small maintainability problems.
- Minor releases may add backward-compatible template capabilities.
- Major releases may change core contracts, module boundaries, or upgrade requirements.
- Version changes should be reflected in `PROJECT_STATUS.md`.
- Breaking changes require explicit documentation and migration guidance.

Release readiness requires:

- Build success.
- Module audit completion.
- No known critical workflow gaps.
- Security and configuration review.
- Frontend and backend rules compliance.
- Documentation updated for changed capabilities.

---

## 9. Module Completion Rules

A module is not complete simply because it has an index, create, or edit page.

Module completion requires:

- Controller actions reviewed.
- All view-returning actions identified.
- Required views present.
- Partial views and shared components reviewed.
- Validation behavior reviewed.
- Authorization behavior reviewed.
- Empty and error states reviewed.
- Frontend assets reviewed.
- Backend service usage reviewed.
- Essential workflows verified.
- Remaining work classified as essential, optional, deferred, or technical debt.

Only after module completion audit should a module be marked complete in
`PROJECT_STATUS.md` or summarized as complete in this roadmap.

---

## 10. Backward Compatibility Policy

Future phases should extend the template without breaking existing client deployments
whenever practical.

Compatibility rules:

- Preserve public route behavior unless a change is intentional and documented.
- Preserve service contracts when possible.
- Preserve database data when schema evolves.
- Preserve theme and layout customization paths.
- Avoid removing reusable components without replacement guidance.
- Avoid changing configuration keys without migration notes.
- Avoid breaking client branding assets.
- Avoid changing authentication or authorization behavior silently.

Breaking changes are allowed only when:

- They resolve a production-readiness issue.
- They remove unsafe behavior.
- They simplify a core contract that would otherwise block long-term reuse.
- They are documented with upgrade steps.

---

## 11. Upgrade Strategy

The template should support future client upgrades with minimal manual merge work.

Upgrade principles:

- Keep client customization outside core modules where practical.
- Use configuration, theme tokens, content records, and media assets for client identity.
- Keep reusable modules stable and documented.
- Avoid client-specific source edits in shared layouts, services, and persistence models.
- Document breaking changes.
- Provide migration notes for schema, configuration, provider, and asset changes.
- Prefer additive changes over destructive changes.

Upgrade documentation should include:

- Changed files or modules.
- Required configuration changes.
- Database migration requirements.
- Provider setup changes.
- Client customization impact.
- Manual verification checklist.

---

## 12. Template Evolution Principles

Yaqoot should evolve as a reusable commercial product.

Principles:

- Grow by modules, not scattered changes.
- Standardize repeated patterns.
- Promote broadly useful client needs into core template capabilities.
- Keep optional advanced capabilities modular.
- Avoid enterprise complexity until it has clear template value.
- Keep design, frontend, backend, and architecture rules aligned.
- Keep documentation current when product direction changes.
- Keep deployment and client setup practical.

New capabilities should answer:

- Is this useful to multiple future clients?
- Does this preserve the core architecture?
- Can this be configured instead of hardcoded?
- Does this reduce or increase upgrade friction?
- Is this essential now or optional later?

---

## 13. Client-Specific Features Policy

Client-specific features must not destabilize the reusable template.

Allowed client-specific customization:

- Branding.
- Theme values.
- Logo and media.
- Contact details.
- Social links.
- Store policies.
- Static content.
- Provider credentials.
- Product and category data.

Client-specific features should be implemented through:

- Configuration.
- Store Settings.
- Content records.
- Theme tokens.
- Media assets.
- Isolated extension modules.

Client-specific features should not:

- Modify core services for one client.
- Fork shared layouts.
- Add one-client branches inside reusable modules.
- Change database schema for display-only preferences.
- Override authorization, validation, or security rules.
- Break future template upgrades.

If a client feature is broadly reusable, promote it into the planned roadmap.

---

## 14. Technical Debt Strategy

Technical debt should be tracked and addressed deliberately.

Technical debt is roadmap-worthy when it affects:

- Security.
- Production readiness.
- Upgradeability.
- Client customization.
- Reusable module boundaries.
- Performance.
- Accessibility.
- Maintainability.
- Developer or AI-agent reliability.

Current high-value technical debt themes:

- Configuration hardening.
- Removing hardcoded secrets and provider URLs.
- Cloudinary media storage standardization.
- Backend service boundary cleanup.
- Frontend shared component extraction.
- Alert, badge, table, card, and form standardization.
- Admin settings and store settings foundation.
- Authorization policy consolidation.
- Documentation synchronization.

Technical debt should not be mixed into unrelated UI or feature work unless it is
necessary to complete the task safely.

---

## 15. Documentation Maintenance

Documentation is part of the product.

Documentation responsibilities:

- `PROJECT_STATUS.md`: current version, phase, module status, milestones, and concise
  current state.
- `DECISIONS.md`: accepted architectural, workflow, design, and product decisions.
- `WORKFLOW.md`: implementation workflow rules.
- `ARCHITECTURE.md`: stable architecture reference.
- `DESIGN_SYSTEM.md`: visual language and reusable design system reference.
- `FRONTEND_RULES.md`: frontend implementation rules.
- `BACKEND_RULES.md`: backend implementation rules.
- `PHASES.md`: long-term product roadmap.

Documentation should be updated when:

- A phase starts or finishes.
- A significant module is completed.
- A future phase is added, removed, or redefined.
- A feature is promoted from client-specific to template-level.
- A breaking change is introduced.
- A major technical debt item becomes roadmap work.
- A major architecture or design direction changes.

---

## 16. Roadmap Maintenance Rules

Roadmap changes must be intentional.

Rules:

- Do not rewrite the roadmap for minor UI changes.
- Do not mark phases complete without a phase audit.
- Do not mark Admin Settings complete until a Settings module exists.
- Do not add client-specific work to core phases unless it has template value.
- Keep phase statuses clear: Completed, In Progress, Planned, Deferred.
- Keep future phases broad enough to remain useful for years.
- Keep `PROJECT_STATUS.md` synchronized when current phase or module status changes.
- Record major direction changes in `DECISIONS.md`.
- Keep roadmap entries focused on product outcomes, not implementation details.

When uncertainty exists, classify the item as Suggested or Deferred instead of forcing
it into the active roadmap.

---

## Suggested Future Phases

- Phase 5: Template Customization Foundation.
- Phase 6: Commerce Operations.
- Phase 7: Integrations.
- Phase 8: Content & Marketing.
- Phase 9: Observability & Maintenance.

These phases are suggested because they improve reuse across future clients without
turning the template into an enterprise platform by default.

---

## Missing Roadmap Items

- Formal Store Settings module.
- Cloudinary-backed production media storage.
- Payment provider integration.
- Shipping or delivery configuration.
- Inventory adjustment workflow.
- Discounts and promotions.
- Content management for public pages.
- Notification workflows.
- SEO metadata management.
- Structured logging and health checks.
- Upgrade and client setup checklist.
- Accessibility and security audit milestones.
- Shared component standardization milestones.

---

## Recommendations for Long-Term Template Evolution

- Complete essential storefront and customer workflows before expanding optional
  commerce features.
- Treat Admin Settings and Store Settings as foundational for client reuse.
- Move client identity and business content toward configuration-backed settings.
- Standardize media storage around Cloudinary before broader production deployment.
- Add provider interfaces before introducing payment, shipping, notification, or media
  integrations.
- Schedule technical debt phases deliberately instead of mixing them into feature work.
- Keep future phases modular, reviewable, and backward-compatible.
- Maintain documentation as part of every roadmap-level change.
