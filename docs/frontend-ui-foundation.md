# Frontend UI foundation

- **Library:** Angular Material 22 (accessible components, Material 3 tokens, bidi-ready). The only added UI dependency is `chart.js` 4.5.1 (MIT), loaded lazily by the report routes. Ng-Matero is a visual reference only (no package, no copied code).
- **Theme:** violet primary by default (six accent presets chosen by the SuperAdmin), cyan tertiary, Inter (Noto Sans Arabic for Arabic) typography; light/dark follows the OS until the user picks one (per browser). Direction: calm, professional support-desk look; grouped sidebar with a mini rail on desktop, overlay drawer on handset.
- **Tokens:** spacing (4/8 px scale), radius and layout sizes are CSS custom properties in `src/styles.scss` (`--crm-*`).
- **Folders:**
  - `core/` — app-wide singletons: API config, layout, interceptors, auth (session service, interceptor, guards).
  - `shared/ui/` — presentational building blocks (`PageHeader`, `StatusBadge`, `EmptyState`, `ErrorState`, `ConfirmDialog`/`Confirm`, `KpiCard`, `ChartCard`, `tabQuery`). `shared/date/` — date helpers and the date adapter; `shared/charts/` — the Chart.js wrapper.
  - `features/<name>/` — routes, pages, API service and models for that feature, lazy-loaded via `<name>.routes.ts`.
- **Lists:** server-side paging with `MatPaginator` (`pageIndex = page - 1`), debounced (300 ms) search field, `StatusBadge` for record type/status.
- **Pickers:** select existing records through server-side search (`CustomerPicker` in `features/tickets/` wraps `MatAutocomplete` over `GET /api/v1/customers`); fixed sets use `MatSelect`. `TicketPicker` (`features/tasks/`) lists only tickets currently assigned to the chosen task owner.
- **Role-aware actions:** pages compute available actions from the current user and record (`ticketActions` in `features/tickets/ticket-workflow.ts`) for display only; the API re-checks. After a 404 on an action the page drops the stale record and shows an access-lost message.
- **CSS:** use logical properties only (`padding-inline`, `margin-block`, `inline-size`, `text-align: start`); never `left`/`right`. English/Arabic and RTL are described in [platform-ui-and-localization.md](platform-ui-and-localization.md).
- **Auth:** access token in memory only (`AuthService`); guards are UX only; `NavItem.roles` filters navigation; public pages (`/sign-in`, `/sign-up`) render outside the shell.
- **Navigation:** entries are added to `NAV_ITEMS` only when a screen ships (DASH-002 added Tasks, Quick Replies, My Collaborations; the Agent notification bell lives in the shell toolbar).
- **Polling:** only `core/notifications/NotificationBell` polls (60 s, Agent only, `exhaustMap`); every other screen loads on visit / user action.
- **Future screens** are designed per feature intake from real data; no placeholder pages.
