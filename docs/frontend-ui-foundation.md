# Frontend UI foundation

- **Library:** Angular Material 22 (accessible components, Material 3 tokens, bidi-ready). No other UI dependencies.
- **Theme:** violet primary, cyan tertiary, Inter typography, follows the OS light/dark preference via `light-dark()` tokens. Direction: calm, professional support-desk look; side navigation on desktop, overlay drawer on handset.
- **Tokens:** spacing (4/8 px scale), radius and layout sizes are CSS custom properties in `src/styles.scss` (`--crm-*`).
- **Folders:**
  - `core/` — app-wide singletons: API config, layout, interceptors.
  - `shared/ui/` — presentational building blocks (`PageHeader`, `StatusBadge`).
  - `features/<name>/` — routes, pages, API service and models for that feature, lazy-loaded via `<name>.routes.ts`.
- **CSS:** use logical properties only (`padding-inline`, `margin-block`, `inline-size`, `text-align: start`); never `left`/`right`. No RTL or localization logic yet.
- **Navigation:** entries are added to `NAV_ITEMS` only when a screen ships.
- **Future screens** are designed per feature intake from real data; no placeholder pages.
