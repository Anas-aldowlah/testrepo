# Yaqoot Frontend Development Rules

This document is the authoritative frontend development guide for the Yaqoot project.
It defines how Razor views, CSS, JavaScript, shared components, and frontend assets
must be written and organized across the project.

Yaqoot is a reusable commercial e-commerce template. Frontend code must therefore
favor consistency, reuse, maintainability, accessibility, and client customization
over one-off page solutions.

---

## 1. Purpose

The purpose of this document is to make frontend implementation predictable across
the storefront, customer experience, and administration area.

This document defines:

- How Razor views should be structured.
- Where CSS and JavaScript files belong.
- When shared assets should be reused.
- When page-specific assets are appropriate.
- How frontend components should be extended.
- What frontend patterns are not allowed.
- What reviewers should check before accepting frontend changes.

This document focuses on implementation rules. Visual language belongs in
`DESIGN_SYSTEM.md`. High-level application structure belongs in `ARCHITECTURE.md`.
Development workflow belongs in `WORKFLOW.md`.

---

## 2. Frontend Philosophy

Frontend code should be modular, reusable, and easy to audit.

The project currently uses:

- ASP.NET Core Razor Views.
- Shared storefront and admin layouts.
- Razor sections for page-specific styles and scripts.
- Shared CSS under `wwwroot/css`.
- Module CSS folders such as `home`, `products`, `orders`, `cart`, `categories`,
  `account`, and `admin`.
- Shared JavaScript under `wwwroot/js`.
- Module JavaScript folders matching frontend modules.
- Bootstrap RTL and Bootstrap Icons.
- Custom classes using `yaqut-*`, `yq-*`, and BEM-like element naming.
- Data attributes such as `data-yq-*` and `data-yaqut-*` for JavaScript hooks.

Frontend implementation should keep this model intact. New work should reuse existing
layouts, sections, component classes, validation patterns, image fallback behavior,
theme behavior, and page/module asset organization before creating new patterns.

---

## 3. Razor View Rules

Razor Views are responsible for presenting prepared data and composing UI.

Rules:

- Keep views focused on markup, component composition, and simple display decisions.
- Do not put business logic in Razor Views.
- Do not query persistence from views.
- Do not call services from views unless the pattern is explicitly approved for a
  shared view concern.
- Use strongly typed models when a view has a defined data shape.
- Use `ViewData` and `ViewBag` only for lightweight page metadata or existing module
  conventions.
- Keep conditional rendering simple and readable.
- Use Tag Helpers for links, forms, validation, and static assets.
- Use `asp-append-version="true"` for project-owned CSS and JavaScript assets.
- Do not use inline CSS or inline JavaScript for new work.
- Do not create hidden page behavior through large Razor expressions inside attributes.
- Prefer reusable partials or components when markup repeats across pages.

Allowed Razor responsibilities:

- Setting `ViewData["Title"]`.
- Loading page-specific CSS through `@section Styles`.
- Loading page-specific JavaScript through `@section Scripts`.
- Rendering shared partials.
- Rendering accessible state, labels, and `data-*` attributes from prepared model data.
- Showing validation summaries, alerts, empty states, and user-facing feedback.

---

## 4. Layout Rules

Layouts own shared page shells and global asset loading.

Current shared layouts include:

- `Views/Shared/_Layout.cshtml` for the storefront and customer-facing pages.
- `Views/Shared/_AuthLayout.cshtml` for authentication flows.
- `Areas/Admin/Views/Shared/_AdminLayout.cshtml` for admin workflows.

Rules:

- Layouts should load only shared CSS and shared JavaScript required by that shell.
- Storefront layout should own storefront shell concerns such as header, nav, search,
  account actions, theme toggle, container, footer, and shared script sections.
- Admin layout should own admin shell concerns such as sidebar, topbar, breadcrumb,
  user menu, mobile overlay, theme toggle, and admin script sections.
- Page-specific CSS and JavaScript must be loaded by the page, not hardcoded into the
  layout.
- Layouts must expose `Styles` and `Scripts` sections for page assets.
- Layout markup must avoid client-specific content when that content should eventually
  be configurable.
- Do not duplicate shared shell markup across pages.
- Avoid inline event handlers in layouts. Use JavaScript modules and event listeners.

Exception:

- Existing legacy inline handlers may remain until scheduled cleanup, but new layout
  work should use unobtrusive JavaScript.

---

## 5. Partial View Rules

Partials are the preferred Razor mechanism for reusable markup fragments.

Current reusable partials include:

- `Views/Shared/_ProductCard.cshtml`.
- `Views/Shared/_ValidationScriptsPartial.cshtml`.

Rules:

- Use partials when the same markup structure appears across multiple views.
- Keep partials data-driven and free from page-specific assumptions.
- Pass data through the model or explicit `ViewData` values.
- Do not put page-specific script blocks inside partials.
- Do not put page-specific style blocks inside partials.
- Keep partial markup accessible on its own.
- Use stable classes and `data-*` hooks so partials can be safely styled and enhanced.
- Do not duplicate the product card, category card, badge, alert, or empty-state markup
  when an existing pattern can be reused or promoted.

Partials are appropriate for:

- Product cards.
- Category cards.
- Repeated badges.
- Empty states.
- Form field groups.
- Admin table row identity cells.
- Upload preview blocks.
- Confirmation dialog shells.

---

## 6. View Component Guidelines

View Components should be used when a reusable UI component needs its own preparation
logic or is too complex for a simple partial.

Use a View Component when:

- A shared UI block needs data loading or aggregation.
- The same UI appears across multiple controllers or areas.
- Component rendering requires reusable server-side logic.
- The component should expose a stable contract for future modules.

Avoid a View Component when:

- A simple partial is enough.
- The component would hide page-specific behavior.
- The logic belongs in a controller or service.
- The component would duplicate an existing partial pattern.

Potential future View Component candidates:

- Storefront product card list sections.
- Category navigation blocks.
- Cart summary.
- Admin status badge renderer.
- Admin table actions.
- Shared empty states.

---

## 7. HTML Structure Standards

HTML must be semantic, accessible, and stable for styling and scripting.

Rules:

- Use semantic elements such as `main`, `section`, `article`, `nav`, `header`, `footer`,
  `aside`, `form`, `table`, and `button` where appropriate.
- Every major page section should have a clear heading or accessible label.
- Use `button` for actions and `a` for navigation.
- Use `type="button"` for buttons that do not submit forms.
- Icon-only controls must include `aria-label` and, when useful, `title`.
- Decorative icons should use `aria-hidden="true"`.
- Responsive table cells should use `data-label` when the table stacks on mobile.
- Use `data-yq-*` or `data-yaqut-*` attributes for JavaScript hooks instead of selecting
  by fragile text or visual-only classes.
- Avoid inline `style` attributes.
- Avoid inline event handlers such as `onclick`, `onchange`, and `oninput`.
- Keep markup nesting shallow enough to understand and maintain.

---

## 8. CSS Architecture

CSS must be modular and organized by responsibility.

Current CSS ownership:

- `wwwroot/css/yaqut-theme.css`: global theme tokens, base styles, shared utilities,
  and legacy/shared component styles.
- `wwwroot/css/layout.css`: shared storefront shell styles.
- `wwwroot/css/admin/layout.css`: shared admin shell styles and admin tokens.
- `wwwroot/css/admin/*.css`: admin module styles.
- `wwwroot/css/{module}/index.css`: module index page styles.
- `wwwroot/css/{module}/{page}.css`: module page styles.
- `Views/Shared/_Layout.cshtml.css`: Razor scoped layout CSS generated/used by ASP.NET
  conventions, currently present but not the main project CSS pattern.

Rules:

- Put shared tokens, resets, utilities, and shared components in shared CSS only.
- Put storefront shell styles in `wwwroot/css/layout.css`.
- Put admin shell styles in `wwwroot/css/admin/layout.css`.
- Put admin module styles in `wwwroot/css/admin/{module}.css`.
- Put storefront/customer page styles in `wwwroot/css/{controller-or-module}/{page}.css`.
- Do not put page-specific styles in `yaqut-theme.css`.
- Do not put admin page-specific styles in admin layout CSS.
- Do not duplicate shared component styles in page CSS.
- Use CSS custom properties for values that should be reusable or client-configurable.
- Keep CSS selectors class-based and predictable.
- Avoid styling by element names globally except for base reset and typography rules.

---

## 9. CSS Naming Conventions

The project currently uses two established class prefixes:

- `yaqut-*` for many shared storefront and global component patterns.
- `yq-*` for newer modules, utilities, and admin/page component patterns.

Rules:

- Continue using existing class names when extending existing components.
- Use BEM-like element naming for component internals: `block__element`.
- Use modifier naming for variants: `block--modifier`.
- Use state classes for UI state: `is-active`, `is-open`, `is-visible`, `is-collapsed`,
  `is-invalid`, `is-valid`.
- Use `yq-admin-*` for admin shell components.
- Use module prefixes for module-specific CSS, such as `yq-products-*`,
  `yq-orders-*`, `yq-dashboard-*`, or `yq-cart-*`.
- Do not create vague class names such as `.box`, `.item`, `.text`, `.button`, or
  `.container` for project components.
- Do not use IDs for styling unless there is a strong reason.
- IDs are acceptable as JavaScript targets when they identify a unique control.

Future cleanup should clarify the boundary between `yaqut-*` and `yq-*`, but new work
must remain consistent with nearby code.

---

## 10. Shared vs Page-specific CSS

Use shared CSS when a style supports a reusable component or cross-page behavior.

Shared CSS is appropriate for:

- Theme tokens.
- Base typography and reset rules.
- Layout shells.
- Buttons.
- Forms.
- Alerts.
- Badges.
- Empty states.
- Product cards.
- Admin panels.
- Admin table foundations.
- Image fallback classes.
- Accessibility utilities.
- Motion utilities.

Page-specific CSS is appropriate when:

- The style applies only to one page.
- The layout composition is unique to that workflow.
- The module has page-specific sections that are not reusable yet.
- The code is likely to remain isolated.

Promotion rule:

- If a page-specific pattern is used by three or more pages or modules, promote it to
  shared CSS and document the pattern.

File loading rule:

- Each page should load only the page-specific CSS it needs through `@section Styles`.
- Shared layout CSS should be loaded once by the layout.

---

## 11. JavaScript Architecture

JavaScript must be modular, unobtrusive, and safe to load on pages where the target
markup may not exist.

Current JavaScript patterns:

- Shared global behavior in `wwwroot/js/yaqut-main.js`.
- Storefront shell behavior in `wwwroot/js/layout.js`.
- Admin shell behavior in `wwwroot/js/admin/layout.js`.
- Page/module behavior in folders such as `products`, `orders`, `cart`, `home`,
  `categories`, and `admin`.
- IIFE wrappers with `"use strict"`.
- DOM-ready initialization.
- Defensive early returns when root elements are missing.
- Event listeners bound through selectors and `data-*` attributes.

Rules:

- Use IIFE modules or another locally established module pattern.
- Do not leak variables into the global scope.
- Avoid adding new global functions.
- Do not use inline event handlers for new work.
- JavaScript should enhance server-rendered HTML, not replace required server behavior.
- Always guard initialization with root-element checks.
- Keep page-specific behavior in page-specific files.
- Keep shared behavior in shared files only when it is used across pages.
- Use `data-yq-*` or `data-yaqut-*` for behavior hooks.
- Avoid selecting elements by visible text.
- Avoid storing sensitive data in localStorage.
- Catch and ignore storage failures for non-critical UI preferences.
- Keep client-side validation supplemental to server-side validation.

---

## 12. Shared vs Page-specific JavaScript

Shared JavaScript is appropriate for:

- Theme switching.
- Image fallback handling.
- Image preview handling.
- Shared validation enhancement.
- Shared confirm behavior.
- Shared reveal behavior.
- Storefront layout behavior.
- Admin layout behavior.
- Reusable dropdown behavior.
- Reusable component behavior used by multiple modules.

Page-specific JavaScript is appropriate for:

- Product list sorting and pagination.
- Product detail tabs and quantity controls.
- Order filtering.
- Checkout page enhancements.
- Cart interactions.
- Contact form enhancements.
- Admin category delete dialog behavior.
- Any behavior tied to one page's DOM structure.

File placement:

- Storefront shared: `wwwroot/js/yaqut-main.js` or `wwwroot/js/layout.js`.
- Storefront module page: `wwwroot/js/{module}/{page}.js`.
- Admin shared: `wwwroot/js/admin/layout.js`.
- Admin module page: `wwwroot/js/admin/{feature}.js` or a more specific admin module
  file when the behavior grows.

Do not place page-specific code in `yaqut-main.js` merely because it is convenient.

---

## 13. Forms Standards

Forms must be accessible, validated, and consistent with existing input patterns.

Rules:

- Use Tag Helpers for form actions and fields.
- Include server-side validation summaries where appropriate.
- Include field-level validation spans for editable forms.
- Use `asp-validation-for` and `asp-validation-summary` for model validation.
- Use `aria-describedby` to connect inputs with help text and validation messages.
- Use `autocomplete`, `inputmode`, `type`, `min`, `step`, and `required` where useful.
- Use `data-yq-validate` only for progressive client-side enhancement.
- File inputs must show accepted file intent and preview where practical.
- Form action buttons should be grouped consistently.
- Avoid default values in views unless they are safe UI defaults.
- Do not rely on JavaScript-only validation for correctness or security.

Frontend validation must improve usability but never replace server validation.

---

## 14. Tables Standards

Tables are primarily admin and operational UI.

Rules:

- Use real `table` markup for tabular data.
- Include meaningful column headers.
- Use `data-label` on cells when the table has mobile stacked behavior.
- Keep row actions grouped in the final column.
- Use badges for statuses and roles.
- Use object identity cells for product, category, user, or order identity.
- Use table wrappers for horizontal overflow.
- Do not replace tabular admin data with cards unless the mobile layout requires it.
- Keep empty table states outside the table or in a clearly accessible table row.
- Avoid duplicating responsive table CSS across modules; promote repeated behavior.

---

## 15. Cards Standards

Cards are reusable object or information containers.

Rules:

- Use existing card patterns before creating new ones.
- Product listings should use the shared product card partial.
- Admin panels should follow admin panel/card conventions.
- Cards should have stable media dimensions to prevent layout shift.
- Card headings should be semantic and not only visual.
- Card actions should be placed predictably.
- Do not nest decorative cards inside decorative cards.
- Avoid duplicating card markup across modules when a partial would work.
- Use `article` when the card represents a standalone object.
- Use `section` when the card groups part of a page workflow.

---

## 16. Buttons Standards

Buttons must communicate action type and behave consistently.

Rules:

- Use shared button classes for primary and secondary actions.
- Use `button` for actions and `a` for navigation.
- Use `type="submit"` only when submitting a form.
- Use `type="button"` for JavaScript controls.
- Icon-only buttons must have accessible labels.
- Destructive buttons must use a destructive visual treatment.
- Avoid creating one-off button classes.
- Keep button text short and action-oriented.
- Use disabled and loading states when an operation may take time.
- Do not make non-interactive elements behave like buttons.

---

## 17. Responsive Design Rules

Responsive behavior must be implemented intentionally, not patched after desktop work.

Rules:

- Preserve RTL layout behavior at every breakpoint.
- Use existing responsive patterns from nearby modules.
- Keep touch targets usable on mobile.
- Ensure text does not overflow buttons, cards, nav items, badges, or tables.
- Ensure sticky headers, sidebars, and panels do not cover content.
- Use stacked layouts for narrow screens.
- Use horizontal overflow wrappers only when the content genuinely needs table structure.
- Keep page-specific breakpoints limited and consistent with existing module breakpoints.
- Test admin sidebar and storefront nav behavior after layout changes.

---

## 18. Accessibility Rules

Accessibility is required for production frontend work.

Rules:

- Preserve skip links in shared layouts.
- Use semantic HTML and landmark elements.
- Use `aria-label` for controls without visible text.
- Use `aria-current` for active navigation where appropriate.
- Use `aria-expanded` and `aria-controls` for toggles.
- Use `aria-hidden="true"` for decorative icons.
- Ensure modals and dropdowns close with Escape when applicable.
- Ensure focus-visible states are present and visible.
- Use `role="alert"` for immediate error or success feedback.
- Do not rely on color alone to communicate state.
- Respect reduced-motion preferences for animated UI.
- Keep image `alt` text useful and specific when the image conveys content.

---

## 19. Validation UI Rules

Validation UI must be consistent across storefront, account, checkout, and admin forms.

Rules:

- Use validation summaries for form-level errors.
- Use field-level validation messages near the field.
- Do not hide validation behind hover or icons only.
- Use the existing `yaqut-alert` and field validation class patterns until shared
  variants are standardized.
- Connect validation messages with `aria-describedby`.
- Client-side validation may add `is-invalid` and `is-valid` classes as enhancement.
- Validation text must be clear and actionable.
- Do not show successful validation noise unless it helps the workflow.

Technical debt note:

- Alert variants and field validation classes are not fully standardized yet. New forms
  should follow the nearest current module pattern and avoid inventing new variants.

---

## 20. Error & Empty State Rules

Errors and empty states must help the user recover.

Rules:

- Use shared alert patterns for errors and success messages.
- Use shared empty state patterns where available.
- Empty states should include a clear message and, when useful, a next action.
- Admin empty states should be direct and operational.
- Storefront empty states should guide shopping or discovery.
- Use `role="status"` for non-urgent empty or result states when useful.
- Use `role="alert"` for urgent validation or failure messages.
- Avoid blank pages when data is missing.
- Avoid treating empty data as an exception if it is a normal state.

---

## 21. Loading & Feedback Patterns

Frontend feedback should make user actions feel reliable.

Rules:

- Use server-rendered fallback content whenever possible.
- Add loading or disabled states for actions that can be submitted more than once.
- Use clear confirmation for destructive actions.
- Use image fallback behavior for product and category media.
- Use lazy loading for repeated product/category images where appropriate.
- Do not block the whole page for small local interactions.
- Do not fake successful operations on the client.
- Keep feedback close to the action that caused it.
- Preserve form values after validation failures through server model binding or safe
  client enhancement.

Standardization opportunity:

- Add shared loading button and pending form-submit patterns.

---

## 22. Component Reuse Rules

Reuse is mandatory when a component already exists.

Rules:

- Use `_ProductCard.cshtml` for product cards.
- Use shared layouts for all standard pages.
- Use existing alert, badge, empty-state, button, form, table, and card patterns.
- Promote repeated markup into partials or View Components.
- Promote repeated CSS into shared component CSS.
- Promote repeated JavaScript into shared component JavaScript only when it is truly
  cross-page.
- Do not fork a shared component for cosmetic client branding.
- Do not duplicate admin table or form-section patterns across new modules.
- Document new reusable components when introduced.

Review question:

- If the same structure appears in more than two places, should it become a partial,
  shared CSS class, or View Component?

---

## 23. File & Folder Organization

Frontend files must follow the existing project structure.

Razor:

- Storefront/customer views: `Views/{Controller}/{Action}.cshtml`.
- Shared storefront/customer partials: `Views/Shared/_Name.cshtml`.
- Admin views: `Areas/Admin/Views/{Controller}/{Action}.cshtml`.
- Admin shared layout/partials: `Areas/Admin/Views/Shared/_Name.cshtml`.

CSS:

- Global theme and shared components: `wwwroot/css/yaqut-theme.css`.
- Storefront shell: `wwwroot/css/layout.css`.
- Storefront/customer page CSS: `wwwroot/css/{module}/{page}.css`.
- Admin shell: `wwwroot/css/admin/layout.css`.
- Admin module CSS: `wwwroot/css/admin/{module}.css`.

JavaScript:

- Global shared behavior: `wwwroot/js/yaqut-main.js`.
- Storefront shell behavior: `wwwroot/js/layout.js`.
- Storefront/customer page JS: `wwwroot/js/{module}/{page}.js`.
- Admin shell behavior: `wwwroot/js/admin/layout.js`.
- Admin module JS: `wwwroot/js/admin/{feature}.js`.

Loading:

- Layouts load shared assets.
- Views load page assets through `@section Styles` and `@section Scripts`.
- Validation scripts are loaded only on pages that need them.
- Avoid loading page-specific assets globally.

---

## 24. Performance Guidelines

Frontend performance should remain simple and predictable.

Rules:

- Load only the CSS and JavaScript needed by the current page.
- Use `asp-append-version="true"` for cache busting.
- Use lazy loading for repeated product/category images.
- Keep JavaScript initialization defensive and cheap.
- Use passive listeners for scroll events when possible.
- Use `requestAnimationFrame` for scroll-driven visual updates.
- Avoid unnecessary DOM queries inside loops.
- Avoid large inline scripts that cannot be cached.
- Avoid duplicating CSS rules across files.
- Avoid layout shifts by defining stable image and card dimensions.
- Do not move essential server-rendered content into client-only rendering without a
  clear reason.

---

## 25. Frontend Anti-Patterns

The following patterns are not allowed for new frontend work:

- Inline CSS in Razor markup.
- Inline JavaScript in Razor markup.
- Inline event handlers such as `onclick`.
- Business logic in Razor Views.
- Database or service access from views.
- Duplicating shared components instead of reusing or promoting them.
- Adding page-specific styles to global CSS.
- Adding page-specific JavaScript to shared JS files.
- Creating new naming conventions for one page.
- Selecting JavaScript targets by visible text.
- Hiding required information behind decorative icons.
- Using client-side validation as the only validation layer.
- Storing sensitive information in localStorage.
- Hardcoding client-specific branding in reusable components.
- Adding a new dependency for a small interaction already possible with existing tools.
- Breaking RTL behavior with left/right-only assumptions where logical properties fit.

---

## 26. Client Customization Guidelines

Client customization must not require rewriting frontend architecture.

Allowed client customization areas:

- Logo and brand assets.
- Theme tokens.
- Fonts.
- Hero images.
- Storefront nav labels and links.
- Footer content.
- Social links.
- Announcement text.
- Static page content.
- Product and category media.

Rules:

- Prefer token changes over component rewrites.
- Prefer content/configuration changes over markup forks.
- Keep reusable components brand-neutral where practical.
- Do not duplicate layouts for a single client brand.
- Do not hardcode one client's copy into shared components.
- If a client need is reusable, promote it into the core frontend system.
- If a client need is unique, isolate it in client-specific theme/content assets.

---

## 27. Code Review Checklist

Before approving frontend changes, verify:

- The view uses the correct layout.
- Page-specific CSS is loaded through `@section Styles`.
- Page-specific JavaScript is loaded through `@section Scripts`.
- No new inline CSS was added.
- No new inline JavaScript or inline event handlers were added.
- Razor logic is limited to display composition.
- Existing shared components were reused where possible.
- CSS is in the correct shared or page-specific file.
- JavaScript is in the correct shared or page-specific file.
- Classes follow existing naming conventions.
- JavaScript initialization safely exits when markup is absent.
- Forms include server-side validation UI.
- Icon-only controls have accessible labels.
- Tables have headers and mobile `data-label` values where needed.
- Images have appropriate `alt` text and fallback behavior where needed.
- Responsive behavior works on mobile and desktop.
- Focus states and keyboard interactions are preserved.
- Client-specific branding has not been hardcoded into reusable components.
- No unnecessary frontend dependency was introduced.

---

## Existing Frontend Strengths

- The project already separates shared layouts from page views.
- Storefront and admin shells are clearly separated.
- CSS and JavaScript are mostly organized by module.
- Most pages use Razor `Styles` and `Scripts` sections correctly.
- Shared theme tokens exist through `--yq-*` and `--yq-admin-*`.
- Product cards already use a shared Razor partial.
- JavaScript commonly uses IIFE wrappers and defensive initialization.
- Data attributes are already used for behavior hooks.
- Image fallback and image preview behavior are shared.
- Admin views use semantic sections, accessible labels, and responsive table labels.
- Admin layout includes skip links, sidebar state, dropdown behavior, and mobile overlay.
- Storefront layout includes skip link, search, theme toggle, and responsive navigation.

---

## Existing Frontend Inconsistencies

- Some Razor views still contain inline `<style>` blocks, inline `style` attributes,
  inline `<script>` blocks, or inline event handlers.
- Shared storefront shell styles are split and partly duplicated between
  `yaqut-theme.css` and `layout.css`.
- Some page-specific behavior appears in shared JavaScript, especially older product,
  auth, and filtering behavior inside `yaqut-main.js`.
- Auth tab behavior appears more than once in JavaScript.
- Button class naming uses both `btn-yaqut-*` and `yq-btn-*` aliases.
- Alert variants are not fully standardized across pages.
- Admin responsive table CSS is repeated across module stylesheets.
- Admin panel/card/form section styles are repeated across modules.
- Status badge patterns are implemented separately in multiple CSS files.
- Some client-facing brand content remains hardcoded in layouts.
- Some legacy views use older Bootstrap/CDN patterns rather than the current shared
  asset pipeline.

---

## Frontend Technical Debt Discovered

- `yaqut-theme.css` is very large and contains a mix of tokens, base rules, shared
  components, page-like sections, admin legacy styles, and legacy aliases.
- `yaqut-main.js` contains multiple responsibilities and some page-specific behavior
  that should be split into shared component files or page modules.
- Inline styles and scripts remain in legacy/directive and account views.
- Existing frontend naming boundaries between `yaqut-*` and `yq-*` are not formalized.
- Shared alert, badge, table, empty-state, and admin panel foundations are not yet
  extracted into dedicated reusable frontend components.
- Client branding is not yet fully configuration-driven in layouts.
- Reduced-motion handling is stronger in admin than across all storefront animation.

---

## Recommendations for Improving Frontend Maintainability

- Split `yaqut-theme.css` into smaller ownership files for tokens, base rules,
  utilities, shared components, and legacy cleanup.
- Move page-specific logic out of `yaqut-main.js` into page/module JavaScript files.
- Remove inline styles, inline scripts, and inline event handlers during dedicated
  frontend cleanup work.
- Standardize alert, badge, table, card, form field, upload, dialog, and empty-state
  component foundations.
- Create shared admin table and admin panel CSS to reduce repeated module styles.
- Clarify the naming convention boundary between `yaqut-*` and `yq-*`.
- Move client-facing layout content toward a configurable Store Settings layer.
- Add a lightweight frontend component inventory as reusable pieces are introduced.
- Expand reduced-motion support across storefront page scripts and CSS animations.
