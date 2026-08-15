# YAGOT frontend architecture contract

This contract describes the current ASP.NET Core MVC, Razor, Bootstrap 5 RTL, vanilla JavaScript frontend. It is a change checklist, not a framework migration plan.

## 1. Asset ownership

- Global JavaScript is limited to behavior shared by primary routes: `wwwroot/js/yaqut-main.js`, `wwwroot/js/layout.js`, and `wwwroot/js/cart-drawer.js`.
- Admin-shell behavior belongs to `wwwroot/js/admin/layout.js`. A POS or Orders feature must not become an implicit Admin-global dependency.
- Route behavior belongs in a page path such as `wwwroot/js/products/details.js` or `wwwroot/js/admin/orders/index.js` and is referenced from that Razor view's `Scripts` section.
- Global CSS owns tokens, base components, and shells: `wwwroot/css/yaqut-theme.css`, `wwwroot/css/layout.css`, and `wwwroot/css/admin/layout.css`.
- Route/component CSS remains page-scoped, for example `wwwroot/css/admin/orders.css`. Preserve the layout, global-theme, then page-style source order.

## 2. Page initialization and DOM ownership

- A page module starts from one unique `data-yq-*` root and returns without side effects when that root is absent.
- One behavior owns each interaction. Do not combine an inline `onclick` with an external listener or create multiple save/count/visibility owners.
- Stable delegation may be used for dynamic content. Bind once from the page/component owner; do not bind again after every render.
- FE-006, FE-007, and FE-010 established the project rule: one Product filter controller, one cart refresh/request owner, and one POS completion owner.
- Avoid new `window` globals. If legacy markup still invokes one, remove the markup-to-global dependency in the same verified change before removing the global.

## 3. Server-to-client configuration

- Use Razor-encoded `data-*` attributes for small scalar values such as URLs, IDs, flags, and short labels.
- Use a non-executable `application/json` element for structured configuration, with normal Razor encoding and an explicit parse failure path.
- Never construct executable JavaScript through untrusted Razor interpolation. Do not add `Html.Raw` around attacker-controlled JSON.
- Admin Orders demonstrates the scalar contract: `Areas/Admin/Views/Orders/Index.cshtml` supplies the status URL on `[data-yq-orders-page]`; `wwwroot/js/admin/orders/index.js` reads it.

## 4. Safe DOM construction

- Render untrusted or server-originated text with `textContent`, `createTextNode`, `createElement`, `new Option`, and explicit safe attribute/property assignment.
- `innerHTML` and `insertAdjacentHTML` require a proven static/trusted source and a local review explaining that trust boundary. They are not acceptable for raw server errors, storage values, or customer/order data.
- Do not restore Bootstrap `sanitize: false`. Popover content containing customer data must be constructed as DOM nodes.

## 5. Network and async ownership

- One event owner issues a request. It must set and clear loading state deterministically, check `response.ok`, handle parse failures, and restore usable controls on failure.
- Read-only searches may use cancellation/latest-request ownership and may be retried deliberately.
- Transactional sale, payment, checkout, order-completion, and status-update requests must not be retried automatically. Client guards do not replace server idempotency/concurrency controls.
- Preserve endpoint, method, antiforgery, header, payload, and response-shape contracts during extraction.

## 6. State ownership

- One coordinator is authoritative for each workflow. Current examples are Product filtering, cart refresh/latest quantity, and `wwwroot/js/quick-sales/draft-save-coordinator.js` for POS draft saves.
- Do not introduce a second pending flag, abort controller, timer, or storage model for the same workflow.
- Server-selected authentication/validation state remains authoritative; browser storage must not override it.

## 7. Responsive and RTL conventions

- Use Bootstrap's canonical vocabulary: `sm` 576px, `md` 768px, `lg` 992px, `xl` 1200px, and `xxl` 1400px.
- For max-width boundaries use the repository's corrected non-overlapping forms: 575.98px, 767.98px, 991.98px, and 1199.98px. Retain feature-specific breakpoints only where current component geometry requires them.
- Start mobile-first, keep responsive rules with the owning component, and use logical properties so `dir="rtl"` remains correct.
- POS retains its current contract: card/mobile presentation below 992px, desktop table from 992px, and the 1200px split. Do not alter it from unrelated page work.

## 8. Accessibility

- Prefer native buttons, links, inputs, and labels. Preserve keyboard activation and visible `:focus-visible` treatment.
- Modal, drawer, and popover owners manage open/close focus and focus return. Escape behavior must remain available where currently supported.
- Async user-visible status uses `role="status"`, `role="alert"`, or an appropriate `aria-live` region.
- Validation messages remain associated through `aria-describedby` and server/unobtrusive validation contracts.
- Preserve Arabic text, RTL direction, accessible names, and existing ARIA relationships during extraction.

## 9. Images and media

- Frontend owns safe URL normalization, useful intrinsic dimensions when known, appropriate `loading`/`decoding`, fallback behavior, and priority only for genuinely critical media.
- FE-012-MEDIA and IMG-DATA derivative generation/normalization remain media/data responsibilities; frontend code must not claim to solve them.

## 10. Dependencies

- The repository-owned Bootstrap CSS/JS version is 5.3.3 and uses the RTL build. Keep the bundle before page modules that call Bootstrap APIs.
- jQuery is used only where an existing dependency requires it. Validation order is fixed in `Views/Shared/_ValidationScriptsPartial.cshtml`: jQuery, Validate, then Unobtrusive.
- Fonts use the current preconnect and stylesheet policy in the shared layouts. Do not add a second font-loading path from a page.
- Bootstrap Icons 1.11.3 is still loaded from jsDelivr without a repository-owned copy or SRI. FE-026 remains open; do not invent integrity metadata or download a dependency solely to close the finding.

## 11. Security and CSP readiness

- Add no inline event attributes. Prefer external page scripts for new functionality and extractions when behavior can be preserved.
- Do not add sanitizer bypasses, untrusted HTML construction, `javascript:` URLs, or raw exception/server HTML.
- Existing inline theme bootstraps and legacy page blocks mean the frontend is not strict-CSP ready. FE-023 header/enforcement policy remains Backend/Infrastructure-owned.

## 12. Required verification for shared or extracted behavior

1. Map the page root, selectors, server values, events, network contract, state owner, ARIA, and breakpoint behavior before editing.
2. Run `scripts/verify-frontend-contracts.ps1` and syntax-check each changed external JavaScript file.
3. Exercise the relevant repository harness where available, including missing-root/optional content, success, failure, malformed response, and rapid activation cases that apply.
4. Check the selected component at 320, 390, 576, 768, 992, 1200, and 1440 CSS px when rendered evidence is safely available.
5. Run `git diff --check`, inspect the complete diff, and run an isolated non-incremental Release build.
6. Report static, harness, build, actual MVC, authenticated, database, accessibility, and visual evidence separately. QA-DB-001 prohibits unsafe POS/MVC mutation checks against the shared database.
