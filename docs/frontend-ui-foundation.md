# Frontend UI foundation

- **Library:** Angular Material 22 (accessible components, Material 3 tokens, bidi-ready). No other UI dependencies.
- **Theme:** violet primary, cyan tertiary, Inter typography, follows the OS light/dark preference via `light-dark()` tokens. Direction: calm, professional support-desk look; side navigation on desktop, overlay drawer on handset.
- **Tokens:** spacing (4/8 px scale), radius and layout sizes are CSS custom properties in `src/styles.scss` (`--crm-*`).
- **Folders:**
  - `core/` — app-wide singletons: API config, layout, interceptors, auth (session service, interceptor, guards).
  - `shared/ui/` — presentational building blocks (`PageHeader`, `StatusBadge`).
  - `features/<name>/` — routes, pages, API service and models for that feature, lazy-loaded via `<name>.routes.ts`.
- **Lists:** server-side paging with `MatPaginator` (`pageIndex = page - 1`), debounced (300 ms) search field, `StatusBadge` for record type/status.
- **Pickers:** select existing records through server-side search (`CustomerPicker` in `features/tickets/` wraps `MatAutocomplete` over `GET /api/v1/customers`); fixed sets use `MatSelect`.
- **CSS:** use logical properties only (`padding-inline`, `margin-block`, `inline-size`, `text-align: start`); never `left`/`right`. No RTL or localization logic yet.
- **Auth:** access token in memory only (`AuthService`); guards are UX only; `NavItem.roles` filters navigation; public pages (`/sign-in`, `/sign-up`) render outside the shell.
- **Navigation:** entries are added to `NAV_ITEMS` only when a screen ships.
- **Future screens** are designed per feature intake from real data; no placeholder pages.
