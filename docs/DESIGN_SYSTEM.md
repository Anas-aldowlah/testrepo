# Yaqoot Design System Reference

This document is the authoritative design system reference for the Yaqoot project.
It defines the reusable visual language, component patterns, and customization rules
for a production-ready e-commerce template.

Yaqoot branding is the default theme. It is not the only supported brand expression.
The Core Design System must remain reusable for future client deployments, while
Client-specific Branding should adapt identity, content, and visual tokens without
rewriting the core UI structure.

---

## 1. Purpose

The purpose of this document is to preserve a consistent, maintainable, and reusable
design system across the storefront, customer experience, and administration area.

This document defines:

- The default Yaqoot visual language.
- The reusable design tokens currently present in the project.
- The expected behavior of shared UI components.
- The distinction between Core Design System and Client-specific Branding.
- Rules for extending UI patterns without creating page-specific drift.
- Known inconsistencies that should be standardized over time.

This document describes the design language and component strategy. It does not
replace architectural documentation, implementation workflow, or historical decisions.

---

## 2. Design Philosophy

Yaqoot uses a premium, elegant, Arabic-first design language that balances luxury
presentation with practical commerce workflows.

The storefront should feel refined, immersive, and brand-led. It uses elevated
surfaces, glass effects, gold accents, generous spacing, expressive display typography,
product imagery, and smooth micro-interactions.

The admin area should feel focused, dense, and operational. It uses the same gold-led
brand foundation, but applies it through restrained surfaces, 8px radii, compact
tables, clear controls, responsive management layouts, and strong hierarchy.

The design system should support:

- A premium default identity.
- Reusable commerce components.
- Fast administrative workflows.
- RTL-first layouts.
- Light and dark themes.
- Future client branding through tokens and assets.
- Consistency across modules without forcing every page to look identical.

---

## 3. Brand Identity

The default Yaqoot brand identity is luxurious, minimal, warm, and polished.

Current brand signals include:

- Gold as the primary accent.
- Soft off-white and deep black backgrounds.
- Glass-like elevated surfaces.
- Rounded storefront cards and pill controls.
- Dense, squared admin panels.
- Arabic RTL layout as the primary reading direction.
- Display typography for editorial storefront moments.
- Product imagery as a first-class visual element.

Brand identity must be treated as a theme layer. The Yaqoot logo, gold palette,
store name, slogans, footer content, social links, campaign text, and product imagery
are default branding choices that future clients should be able to replace.

Core component behavior, spacing logic, responsive behavior, focus states, and
interaction patterns belong to the Core Design System and should remain stable.

---

## 4. Design Principles

- Reuse shared components before creating new page-specific UI.
- Keep storefront UI expressive but still commerce-focused.
- Keep admin UI productive, scannable, and restrained.
- Use design tokens for colors, typography, radius, shadows, and motion.
- Preserve RTL-first layout behavior.
- Use product, category, and brand images intentionally.
- Keep contrast strong enough for both light and dark themes.
- Prefer semantic HTML and accessible controls.
- Keep page-specific CSS limited to page-specific layout needs.
- Promote repeated page-specific patterns into shared components.
- Treat Yaqoot styling as a default theme, not permanent hardcoded identity.

---

## 5. Color System

The current design system is built around CSS custom properties using the `--yq-*`
and `--yq-admin-*` naming families.

Core storefront tokens:

- `--yq-gold`: primary brand accent.
- `--yq-gold-bright`: stronger accent for emphasis and active states.
- `--yq-gold-soft`: softer highlight for luxury gradients and text effects.
- `--yq-gold-grad`: default gold gradient for primary actions and selected states.
- `--yq-bg`: page background.
- `--yq-bg-elevated`: elevated surface background.
- `--yq-bg-glass`: translucent glass surface.
- `--yq-bg-input`: input background.
- `--yq-text`: primary text.
- `--yq-text-soft`: secondary text.
- `--yq-text-on-dark`: text on dark image or dark brand surfaces.
- `--yq-border`: brand-tinted border.
- `--yq-border-sub`: subtle neutral border.
- `--yq-glass-border`: border for translucent surfaces.

Admin-specific tokens:

- `--yq-admin-bg`: admin workspace background.
- `--yq-admin-surface`: translucent admin panel surface.
- `--yq-admin-surface-solid`: solid admin panel surface.
- `--yq-admin-surface-soft`: nested admin surface.
- `--yq-admin-sidebar-bg`: sidebar base.
- `--yq-admin-sidebar-panel`: sidebar panel shade.
- `--yq-admin-text`: primary admin text.
- `--yq-admin-muted`: secondary admin text.
- `--yq-admin-border`: default admin border.
- `--yq-admin-border-strong`: emphasized admin border.
- `--yq-admin-danger`: destructive and error color.

Color rules:

- Gold is the default primary accent, not a hardcoded requirement for all future clients.
- Semantic states must not rely only on gold. Use distinct state colors for success,
  warning, danger, shipped/processed, cancelled, and neutral statuses.
- Storefront surfaces may use stronger brand emotion.
- Admin surfaces should use brand accents sparingly to preserve scan speed.
- Light and dark themes must both be supported by token overrides.
- New colors should be introduced as tokens before being used widely.

Configurable for clients:

- Primary accent.
- Accent gradient.
- Background colors.
- Surface colors.
- Text colors.
- Border colors.
- Semantic state colors, where brand requirements demand it.

---

## 6. Typography

The current storefront typography uses:

- `--yq-font-display`: Cormorant Garamond with Tajawal fallback.
- `--yq-font-body`: Tajawal.

The admin layout also imports Cairo and Tajawal, while the token system primarily
uses the shared Yaqoot font variables.

Typography rules:

- Body text should use the body font token.
- Storefront hero titles, section titles, product prices, and luxury editorial headings
  may use the display font token.
- Admin headings should prioritize clarity and density over decorative effect.
- Labels, table headers, badges, navigation labels, and metadata should use compact
  weights with clear hierarchy.
- Avoid introducing page-local font families unless they become design-system tokens.
- Do not encode brand identity directly into markup through font-specific assumptions.

Configurable for clients:

- Display font.
- Body font.
- Font weights if supported by the selected font family.

Core behavior that should remain stable:

- Hierarchy scale.
- Readability requirements.
- RTL alignment.
- Truncation behavior in dense admin controls.

---

## 7. Spacing System

Spacing currently uses a practical mix of rem values and responsive `clamp()` values.
The admin shell defines `--yq-admin-space` for the main workspace rhythm.

Spacing rules:

- Storefront pages may use generous vertical spacing for premium presentation.
- Admin pages should use compact, repeatable spacing that supports scanning.
- Repeated module sections should use consistent gaps instead of one-off margins.
- Use container constraints for main content rather than arbitrary full-width content.
- Page-specific spacing should not override shared shell spacing unless the page has
  a real layout requirement.
- Responsive spacing should reduce gracefully on mobile.

Recommended spacing rhythm:

- 0.25rem for tiny internal gaps.
- 0.5rem for compact control gaps.
- 0.75rem to 1rem for form, nav, and table item spacing.
- 1.25rem to 1.5rem for card and panel spacing.
- 2rem to 2.5rem for section-level spacing.
- 4rem or more only for storefront editorial sections.

---

## 8. Border Radius

The project uses two distinct radius languages:

- Storefront: large rounded cards and pill controls.
- Admin: compact 8px surfaces and controls.

Current tokens:

- `--yq-radius`: default large storefront radius, currently 24px.
- `--yq-radius-sm`: compact storefront radius, currently 14px.
- `--yq-radius-lg`: appears in the token set and should be reserved for large surfaces.
- Admin components commonly use 8px directly.

Radius rules:

- Storefront product cards, category cards, glass panels, and editorial surfaces may use
  larger radii.
- Storefront buttons, filter pills, search controls, theme toggles, and badges may use
  pill radii.
- Admin cards, tables, nav items, icons, upload panels, and dialogs should use 8px.
- Avoid mixing storefront pill-heavy styling into admin data workflows.
- Avoid using one-off radius values when a token or established admin radius exists.

Configurable for clients:

- Storefront radius scale.
- Admin radius scale, if the client brand requires a softer or sharper operational UI.

---

## 9. Elevation & Shadows

Current elevation tokens:

- `--yq-shadow`: default elevated surface shadow.
- `--yq-shadow-gold`: brand-accented hover or active shadow.

Elevation patterns:

- Storefront uses glass surfaces, soft shadows, and gold glow for premium emphasis.
- Admin uses subtler panel shadows and stronger separation through borders.
- Hover elevation is common for storefront cards and primary interactions.
- Admin hover states should communicate interactivity without distracting from data.

Rules:

- Use elevation to communicate hierarchy, not decoration alone.
- Prefer tokenized shadows for shared components.
- Gold shadows should be reserved for active, selected, primary, or premium emphasis.
- Avoid heavy shadow stacking on dense admin pages.
- Dark theme shadows should remain visible without muddying contrast.

---

## 10. Iconography

The current project uses Bootstrap Icons across storefront and admin UI.

Iconography rules:

- Use icons to support recognition, not to replace required labels in critical workflows.
- Icon-only buttons must include accessible labels and titles where useful.
- Admin navigation icons should remain consistent in size and alignment.
- Storefront icons may be more expressive, especially in feature cards, trust items,
  product actions, and empty states.
- Use semantic icons consistently for recurring actions such as view, edit, delete,
  restore, cart, profile, logout, search, and navigation.
- Do not mix icon libraries casually. A new icon library should be adopted only as a
  design-system-level decision.

Configurable for clients:

- Decorative brand icon choices.
- Social media icons.
- Optional feature icons.

Core behavior that should remain stable:

- Icon sizing rules inside buttons, nav links, tables, badges, and cards.
- Accessible labels for icon-only controls.

---

## 11. Buttons

Current shared button patterns include:

- `btn-yaqut-gold` and `yq-btn-gold`: primary gold gradient action.
- `btn-yaqut-outline` and `yq-btn-outline`: secondary outline action.
- `yaqut-icon-btn`: circular storefront icon button.
- Admin action buttons in module CSS, such as edit, delete, restore, clear filter,
  status action, and detail link patterns.

Button rules:

- Primary actions should use the primary button pattern.
- Secondary actions should use the outline pattern.
- Icon-only buttons must be used for compact tools and table actions where the icon is
  familiar and the accessible label is present.
- Destructive actions must use a danger treatment and should not look like a primary
  brand action.
- Admin table actions should remain compact and predictable.
- Storefront commerce actions may be more prominent and tactile.
- Do not create new button styles for single pages unless they represent a reusable
  component need.

Buttons should support:

- Hover state.
- Focus-visible state.
- Active state.
- Disabled state.
- Loading or pending state when the action can take time.

Standardization opportunity:

- Align Bootstrap `btn` usage with custom Yaqoot button classes so action sizing and
  focus behavior are consistent everywhere.

---

## 12. Forms

Current form patterns include:

- `yaqut-form-group`, `yaqut-form-label`, `yaqut-input-wrap`, and `yaqut-form-input`.
- Admin product and category form layouts with sticky media side panels.
- Upload controls with preview areas.
- Validation summaries using `yaqut-alert`.
- Field-level error and valid/invalid input states.
- Storefront checkout, contact, profile, auth, and search forms.

Form rules:

- Labels must be visible or otherwise clearly associated with controls.
- Inputs should use tokenized surfaces, borders, and focus states.
- Validation must be visible near the relevant control or at the form summary level.
- Required and optional fields should be communicated consistently.
- Upload controls should provide a preview and clear affordance.
- Admin forms should group fields into clear sections.
- Storefront forms should feel premium but remain fast to complete.
- Form actions should be grouped at the end of the form or in a stable action area.

Standardization opportunity:

- Promote repeated admin form sections, field layouts, upload panels, validation styles,
  and sticky summary panels into documented shared patterns.

---

## 13. Tables

Current table patterns include:

- Dashboard recent orders table.
- Admin products table.
- Admin categories table.
- Admin orders table.
- Admin users table.
- Order detail product tables.
- User order tables.

Table rules:

- Admin tables should prioritize scan speed and compact density.
- Table headers should use muted text and strong weight.
- Rows should use subtle dividers and hover states.
- Repeated object identity patterns should include thumbnail/avatar/icon, name, and
  secondary metadata.
- Status and role values should use badges.
- Actions should be grouped consistently at the end of the row.
- Responsive tables should either scroll horizontally or transform into labeled stacked
  rows with `data-label` content.
- Storefront should use card layouts when tabular comparison is not required.

Standardization opportunity:

- Define one shared admin table foundation for headers, cells, responsive behavior,
  empty rows, row actions, and object identity cells.

---

## 14. Cards

Current card patterns include:

- `yaqut-product-card`.
- Category cards and category tiles.
- Storefront glass panels.
- Cart, order, checkout, confirmation, contact, profile, and product detail cards.
- Admin dashboard panels and KPI cards.
- Admin detail cards for orders and users.
- Product and category form panels.

Card rules:

- Storefront cards may use larger radius, imagery, glass effect, and hover elevation.
- Admin cards should use 8px radius, clear borders, muted surfaces, and compact spacing.
- Cards should represent one coherent object, action group, or information group.
- Do not nest decorative cards inside other decorative cards.
- Repeated cards should share a common class pattern or be promoted into a reusable
  partial/component.
- Cards must preserve stable image dimensions to prevent layout shift.

Standardization opportunity:

- Create shared card foundations for storefront object cards, admin panels, KPI cards,
  detail cards, and form sections.

---

## 15. Badges

Current badge patterns include:

- `yaqut-badge` for storefront order statuses.
- `yaqut-product-card__badge` for availability.
- `yq-dashboard-status`.
- `yq-orders-badge`.
- `yq-products-badge`.
- `yq-users-badge`.
- `yq-user-order-status`.

Badge rules:

- Badges should be short, readable, and semantically colored.
- Status badges should use the same meaning across storefront and admin surfaces.
- Availability, order state, role, stock state, and lifecycle state should not share
  ambiguous visual treatments.
- Use rounded pill badges for storefront and compact pill badges for admin.
- Do not create new status color mappings per page.

Standardization opportunity:

- Define a shared status token map for pending, processed, shipped, delivered,
  cancelled, active, inactive, warning, success, danger, admin, developer, and customer.

---

## 16. Alerts & Notifications

Current alert patterns include:

- `yaqut-alert`.
- `yaqut-alert--danger`.
- Page-scoped success alerts in profile and admin views.
- TempData success and error messages in admin workflows.
- Validation summaries using alert styling.

Alert rules:

- Alerts must use clear semantic color and `role="alert"` where immediate announcement
  is appropriate.
- Error, success, warning, and info variants should be standardized.
- Alerts should appear near the relevant workflow context.
- Validation summaries should not visually conflict with general system alerts.
- Storefront alerts may have a softer premium treatment.
- Admin alerts should be concise and operational.

Standardization opportunity:

- Add shared `success`, `warning`, and `info` alert variants instead of defining them
  page by page.

---

## 17. Empty States

Current empty state patterns include:

- `yaqut-empty`.
- `yq-dashboard-empty`.
- Admin module empty states for products, categories, orders, and users.
- Storefront search, shop, cart, and orders empty states.

Empty state rules:

- Empty states should explain what happened and provide the next useful action when one
  exists.
- Use icons consistently and sparingly.
- Storefront empty states may be warmer and brand-led.
- Admin empty states should be direct and action-oriented.
- Empty states should use shared layout and typography patterns.
- Avoid creating unique empty state structures for every module.

Standardization opportunity:

- Create shared storefront and admin empty-state component patterns with optional icon,
  title, message, and action slot.

---

## 18. Modals & Dialogs

Current dialog implementation is most visible in the admin category delete confirmation
pattern.

Dialog rules:

- Use dialogs only for confirmation, focused editing, or interruptive decisions.
- Destructive dialogs must clearly identify the object and consequence.
- Dialogs must include accessible labels, close controls, focus management, and keyboard
  dismissal behavior.
- Dialog actions must distinguish primary, secondary, and destructive choices.
- Backdrops should be visually clear without hiding essential context.
- Admin dialogs should be compact and task-focused.
- Storefront dialogs should be used sparingly and should not block normal shopping flow
  without a strong reason.

Standardization opportunity:

- Define a shared modal/dialog component for destructive confirmation, generic
  confirmation, and simple information dialogs.

---

## 19. Navigation

Current navigation patterns include:

- Storefront announcement bar.
- Sticky glass storefront header.
- Storefront search.
- Storefront mobile menu.
- Storefront footer navigation.
- Account/profile/logout controls.
- Admin sidebar with grouped navigation.
- Admin topbar with breadcrumb, page title, theme toggle, and user menu.
- Admin mobile overlay and collapsible sidebar.

Navigation rules:

- Storefront navigation should support discovery and shopping intent.
- Admin navigation should support fast access to operational modules.
- Active state must be visible and semantic.
- Mobile menus must expose the same essential navigation as desktop.
- Search should remain visually integrated with the storefront header.
- Admin breadcrumbs should identify location without becoming visually dominant.
- Icons should support navigation labels, not replace them except in collapsed admin mode.
- Navigation content that is client-specific should become configurable.

Configurable for clients:

- Logo.
- Storefront nav labels and links.
- Footer columns.
- Social links.
- Announcement content.

Core behavior that should remain stable:

- Header structure.
- Admin sidebar behavior.
- Mobile menu behavior.
- Focus and active states.

---

## 20. Admin Design Language

The admin design language is productivity-first.

Admin UI characteristics:

- Dense but readable layouts.
- 8px radius panels and controls.
- Sidebar-first navigation.
- Sticky topbar with page context.
- Strong content hierarchy.
- Muted backgrounds with gold highlights.
- Compact tables and row actions.
- KPI cards and dashboard panels.
- Clear status badges.
- Responsive tables for smaller screens.
- Accessible focus states.
- Reduced visual decoration compared with storefront.

Admin rules:

- Use `yq-admin-*` shell and token patterns for admin layout and surfaces.
- Use module-specific CSS only for module-specific composition.
- Keep repeated admin panel, table, badge, action, and form patterns aligned.
- Favor scannability over decorative effects.
- Avoid large editorial hero compositions inside routine admin pages.
- Keep destructive actions visually distinct.
- Preserve mobile overlay and collapsible navigation behavior.

---

## 21. Storefront Design Language

The storefront design language is premium commerce.

Storefront UI characteristics:

- Luxurious gold-led theme.
- Light and dark theme support.
- Glass surfaces and elevated cards.
- Editorial hero sections.
- Product and category imagery.
- Large rounded cards.
- Pill controls.
- Smooth hover and reveal effects.
- RTL-first composition.
- Strong shopping calls to action.
- Trust, delivery, and security cues.

Storefront rules:

- Use shared storefront shell patterns for header, footer, container, search, and actions.
- Use `yaqut-product-card` for product listing contexts unless a new reusable card is
  intentionally defined.
- Use category card/tile patterns consistently across home and category views.
- Keep page-specific hero variants aligned with the same typography, spacing, and
  image treatment.
- Product imagery must remain central and stable in layout.
- Avoid hardcoded Yaqoot content in components that should support future clients.

---

## 22. Responsive Design Rules

The current system uses responsive breakpoints around 1180px, 992px, 768px, 760px,
680px, 520px, and similar module-specific thresholds.

Responsive rules:

- Design mobile-first behavior before adding desktop refinements.
- Storefront grids should collapse to fewer columns while preserving image ratios.
- Admin tables should either scroll or become stacked labeled rows.
- Admin sidebar should become an off-canvas panel on smaller screens.
- Storefront navigation should become a controlled mobile menu.
- Touch targets should remain large enough for mobile use.
- Text must not overflow buttons, badges, tables, cards, or nav items.
- Sticky elements should not cover primary content on small screens.
- Page-specific breakpoints should be consolidated when repeated across modules.

Standardization opportunity:

- Define shared breakpoint tokens or documented breakpoint tiers for phone, tablet,
  desktop, and wide desktop behavior.

---

## 23. Accessibility Guidelines

Existing accessibility patterns include skip links, ARIA labels, semantic navigation,
focus-visible states, table labels on responsive rows, and reduced-motion handling in
the admin shell.

Accessibility rules:

- Preserve RTL semantics with `dir="rtl"` where appropriate.
- Use semantic headings in order.
- Icon-only controls must include accessible names.
- Interactive controls must be keyboard reachable.
- Focus-visible states must be clear on light and dark themes.
- Color must not be the only way to communicate status.
- Alerts should use appropriate roles.
- Images must have useful alt text or be marked decorative by context.
- Tables must retain meaningful labels when responsive.
- Motion should respect reduced-motion preferences.
- Contrast must be checked for both light and dark themes.

Standardization opportunity:

- Extend reduced-motion handling beyond admin shell to storefront reveal animations,
  hero transitions, hover transforms, and page-specific animated effects.

---

## 24. Motion & Micro-interactions

Current motion patterns include:

- `--yq-ease` for storefront transitions.
- Header shrink and shadow changes.
- Theme transition handling.
- Hover lift on product/category/feature cards.
- Button hover lift and brightness.
- Scroll reveal with `yq-reveal`.
- Hero slide transitions and text shine.
- Admin sidebar collapse and mobile overlay transitions.
- Admin dropdown and navigation hover states.

Motion rules:

- Motion should clarify state or add premium polish.
- Storefront motion may be more expressive.
- Admin motion should be quick and functional.
- Use shared easing and duration tokens where possible.
- Avoid motion that shifts layout unexpectedly.
- Avoid repeating custom transition values when the shared token fits.
- Respect `prefers-reduced-motion`.

Configurable for clients:

- Motion intensity may be reduced for more conservative brands.
- Hero animation style may vary by brand, while preserving accessibility.

---

## 25. Component Reuse Strategy

Existing reusable components and patterns include:

- Shared storefront layout.
- Shared admin layout.
- Theme toggle pill.
- Logo wrapper.
- Storefront header, nav, search, and footer.
- Admin sidebar, topbar, breadcrumb, user menu, and overlay.
- Product card partial.
- Product badge.
- Category card and tile patterns.
- Glass surface utility.
- Primary and outline buttons.
- Icon button.
- Form group, label, input, input wrapper, and validation patterns.
- Alert base.
- Empty state base.
- Status badges.
- Admin KPI cards.
- Admin panels.
- Admin responsive table patterns.
- Upload preview patterns.
- Delete confirmation dialog pattern.

Reuse rules:

- Before adding a new component, check whether an existing pattern can be reused.
- If a pattern appears in three or more modules, promote it into the design system.
- Shared components should accept data and configuration rather than hardcoded content.
- Component classes should follow the existing `yq-*`, `yaqut-*`, and BEM-like naming
  conventions.
- Module-specific classes should remain inside module CSS files.
- Shared classes should live in shared CSS files only when they are genuinely reusable.

---

## 26. Client Branding Strategy

Client-specific Branding is the allowed customization layer for future deployments.

Configurable visual elements:

- Logo and logo container treatment.
- Store name and display identity.
- Primary accent color.
- Accent gradient.
- Light and dark theme palettes.
- Typography families.
- Hero images.
- Product and category imagery.
- Announcement copy.
- Footer content.
- Social links.
- Trust badge labels.
- Static content page imagery.
- Optional theme radius scale, when supported by tokens.

Core Design System elements that should remain stable:

- Component structure.
- Layout behavior.
- Responsive rules.
- Accessibility behavior.
- Admin productivity patterns.
- Form validation patterns.
- Table behavior.
- Navigation behavior.
- Status semantics.
- Shared component contracts.

Client branding should not require page-by-page CSS rewrites. When branding cannot
be achieved through tokens, assets, content, or configuration, the design system
should be improved rather than forked for one client.

---

## 27. Design Consistency Rules

- Use the existing token names before adding new ones.
- Keep `--yq-*` tokens for global/storefront design and `--yq-admin-*` tokens for admin.
- Use Bootstrap Icons consistently unless a formal icon-system change is approved.
- Keep storefront and admin radius systems distinct.
- Use shared button styles for common actions.
- Use shared badge semantics for common statuses.
- Use shared alert variants for feedback.
- Use shared table foundations for admin data lists.
- Use shared empty-state patterns for no-data conditions.
- Keep one page-specific CSS file per page or module when needed.
- Do not place one-page styles in global CSS unless the pattern is reusable.
- Do not create a visually new component for a repeated pattern without documenting it.
- Do not hardcode Yaqoot branding in reusable components when it should be configurable.
- Keep dark and light mode behavior aligned when adding styles.

---

## 28. Future Design Evolution

The design system should evolve by standardizing repeated patterns, not by rewriting
the visual language.

Expected evolution areas:

- Formal shared alert variants.
- Shared admin table foundation.
- Shared admin panel/card foundation.
- Shared form field and upload components.
- Shared empty state component.
- Shared modal/dialog component.
- Status badge token map.
- Documented breakpoint tiers.
- Theme configuration model for future clients.
- Store settings integration for logo, colors, social links, and footer content.
- Broader reduced-motion support.
- Accessibility review for contrast, focus order, labels, and keyboard workflows.

Future design changes should preserve the split between Core Design System and
Client-specific Branding. Broadly reusable improvements belong in the core design
system. One-client visual identity belongs in theme tokens, assets, and content.

---

## Design System Assumptions

- Yaqoot remains an RTL-first, server-rendered ASP.NET Core MVC application.
- Bootstrap RTL and Bootstrap Icons remain the current UI foundation.
- Yaqoot remains the default premium fragrance-store theme.
- Light and dark themes remain supported.
- Storefront and admin design languages remain intentionally different.
- Future clients should customize brand identity through tokens, assets, content, and
  configuration before changing core UI code.
- Admin workflows should continue prioritizing productivity over decoration.

---

## Existing Inconsistencies Discovered

- Shared storefront layout styles appear in both `yaqut-theme.css` and `layout.css`,
  creating duplicated header, footer, navigation, search, and utility definitions.
- The project uses both `yaqut-*` and `yq-*` naming patterns. Both are established,
  but their intended boundary is not fully documented.
- Button naming has aliases such as `btn-yaqut-gold` and `yq-btn-gold`, which can
  lead to inconsistent usage.
- Alerts are not fully standardized. The shared alert base includes danger, while
  success and other variants appear page-scoped or module-specific.
- Status badges are implemented separately across storefront orders, dashboard,
  admin orders, products, users, and user order history.
- Admin panels, detail cards, KPI cards, and form sections repeat similar surface
  treatments without a single shared foundation class.
- Admin tables repeat similar responsive table behavior across multiple module CSS files.
- Admin uses an imported Cairo font while the global token system uses Cormorant
  Garamond and Tajawal, so font ownership should be clarified.
- Storefront and admin both use theme toggles, but styling ownership is shared between
  global and admin CSS.
- Modal/dialog patterns are not yet generalized beyond specific confirmation workflows.
- Some legacy aliases remain in the shared theme file, which makes the current design
  language harder to distinguish from older styles.
- Client-facing brand content, footer social links, announcement text, and logo usage
  are still present directly in layout markup rather than a configurable branding layer.

---

## Recommendations for Improving the Design System Over Time

- Split global CSS into clear token, base, component, utility, storefront shell, and
  admin shell ownership areas.
- Document the naming boundary between `yaqut-*` and `yq-*`, or migrate toward one
  preferred convention for new components.
- Create shared component foundations for buttons, alerts, badges, tables, cards,
  empty states, forms, upload panels, and dialogs.
- Consolidate status colors into semantic design tokens used by storefront and admin.
- Move repeated admin table and responsive table behavior into a shared admin component
  stylesheet.
- Move repeated admin panel/card surfaces into shared admin component classes.
- Add success, warning, info, and neutral variants to the shared alert system.
- Define breakpoint tiers and reuse them across storefront and admin modules.
- Expand reduced-motion support to all storefront animations and page-specific effects.
- Introduce a client theme configuration strategy for logo, colors, fonts, footer
  content, social links, announcement text, and static brand content.
- Maintain a lightweight component inventory whenever a reusable pattern is introduced.
