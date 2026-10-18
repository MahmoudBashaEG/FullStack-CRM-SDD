# Platform UI, localization and branding (PLAT-001)

## 1. Shell and navigation

- One shell for staff and Customer users: a grouped, role-filtered sidebar (Support Portal, Workspace, Knowledge & tools, Management, Account; a group with no visible item is not rendered), a mini icon rail on desktop (remembered in `crm.navCollapsed`) and an overlay drawer on handset, a top bar with breadcrumbs, language switcher, light/dark toggle, the notification bell (Agent, unchanged) and a user menu (Profile, Sign out). `/sign-in` and `/sign-up` use `crm-auth-layout` (logo, organization name, switchers).
- Roles, paths and guards of every route are unchanged; `NAV_ITEMS` only gained a `group` and a translation key. Breadcrumbs are derived from route `data.crumb` (section) and the route `title` key (page).
- Ng-Matero is a **visual reference only** (sidebar + brand header, mini rail, top bar, page header, outlined cards). No Ng-Matero package or source is used, so no third-party notice is required. The only added dependency is `chart.js` 4.5.1 (MIT).
- Oversized pages use `?tab=` deep links (`tabQuery` helper): Ticket detail (`overview`, `details`, `activity`, `collaboration`, `knowledge`, `ai`), Customer detail (`profile`, `tickets`, `notes`, `interactions`, `attachments`), Agent dashboard (`overview`, `tasks`) and SLA (`policy`, `calendar`, `rules`, `overview`). Forms live in the page class, so switching tabs never loses edits; non-default tabs load lazily on first open (GET only; the AI tab sends nothing). Reports, Administration and the Knowledge Base use tab navigation bars over their existing child routes (KB Categories is shown to SuperAdmin only).

## 2. Design tokens and themes

`frontend/src/styles.scss`: Material 3 tokens (violet primary by default, cyan tertiary), `--crm-*` spacing, radius, elevation, nav sizes, success/warning colors. Typography is Inter, with Noto Sans Arabic when the language is Arabic. Light/dark: `color-scheme: light dark` follows the OS until the user toggles (`html[data-theme]`, stored in `crm.theme`). Accent presets are compiled at build time as `html[data-accent='<id>']` blocks (the settings preview reuses them through `.accent-preview`). Focus rings are global (`:focus-visible`); `prefers-reduced-motion` disables animations (charts included).

## 3. English / Arabic

- `core/i18n/en/<namespace>.ts` is the **source dictionary** (values copy the former literals verbatim). `core/i18n/ar/<namespace>.ts` is typed `Translations[...]` (`DeepStrings<typeof EN>`), so **a missing or extra Arabic key is a compile error** (`ng build`). English is bundled; Arabic is a lazy chunk loaded by the app initializer before the first render, so there is no English flash. No i18n package is used (no per-locale build, no reload).
- Use `{{ 'ns.key' | t }}` in templates and `inject(I18n).t(...)` in code. Interpolation is `{name}`. Add a key to the English file **and** the Arabic file.
- Label and format helpers take an optional last parameter `t: Translate = englishT`, so a call without it returns the same English as before (existing specs are unchanged).
- Dates: `crmDate` pipe for server timestamps; date-only `yyyy-MM-dd` values use `formatUtcDay(day, locale)`. The locale is `en-US` or `ar-EG-u-nu-latn` (Arabic month and day names with Latin digits, so ids, counts, percentages and typed dates read the same in both languages).
- Backend messages are unchanged. `describeHttpError` / `describeSaveError` take the translator: status-based messages (0, 401, 403, 404, 409, 413, 428, 429, 5xx), a table of ~110 known backend messages and message templates (`core/i18n/server-messages.ts`, generated tables in `en|ar/serverMessages.ts`), and **any other server text is shown verbatim**. Field names inside generic FluentValidation messages stay as the server sends them.
- **Never translated:** user and API content (subjects, descriptions, names, notes, comments, KB titles and bodies, AI output, organization settings values, audit labels and old/new values), enum **values** sent to the API (only their display text is translated), brand-like tokens (SLA, CSAT, AI, Gemini).
- RTL: `<html lang dir>` and the root CDK `Directionality` (`valueSignal` + `change`) are set together, so overlays opened after the switch follow. CSS uses logical properties only. IDs, emails and codes use `.ltr-value`; directional icons use `.dir-icon` (mirrored in RTL). An overlay already open when the language changes keeps its direction until reopened.

## 4. Dates and time

- The app-wide `CrmDateAdapter` (`shared/date/crm-date-adapter.ts`) replaces `NativeDateAdapter` parsing, which delegates to `Date.parse` (engine-defined; `yyyy-MM-dd` is parsed as UTC midnight). Typed dates are **year-month-day only** (`yyyy-mm-dd`, `/` or `.` also accepted, Latin or Arabic-Indic digits), always produce a **local-midnight** `Date`, and ambiguous day-first/month-first text (`05/06/2026`) is rejected, never guessed. The input shows the same text it accepts; times are a 24-hour `HH:mm` clock. The Arabic week starts on Saturday.
- Date-only values (`yyyy-MM-dd`, UTC calendar days): `toDateOnly()` reads the **local** getters, so the day the user picked is the string sent. `toISOString()` is never used on a date-only value, so there is no Cairo shift. The REPORT-001 half-open windows and the ADMIN-001 audit filters receive byte-identical strings.
- Date-time fields use a date picker + time picker pair and `combineLocalDateTime` (local wall time ⇒ ISO UTC, the same instant the former `datetime-local` input produced). "Day set, time empty" is a validation error; both empty is `null` where the field is optional.
- Migrated controls: report period (date-range), audit-log filter (date-range), SLA holiday (date), SLA opens/closes (time, 15 min), SLA preview start (date + time), task due and reminder (date + time), customer interaction time (date + time). **No `max`/`min` rule was added**; the server stays authoritative.

## 5. Charts

`crm-chart` (`shared/charts`) wraps Chart.js (bar, horizontal bar, line, doughnut; parts registered explicitly) and is imported dynamically, so the library exists only in lazy report chunks. Charts are read-only. Colors are resolved from the theme tokens (a `light-dark()` value cannot be used by a canvas directly), and charts re-color on theme, accent and language change. The mapping of screens to charts is in [reports-and-management.md](reports-and-management.md). Accessibility: the canvas is `role="img"` with a summary label and every chart card shows its numbers in a data table (the keyboard- and screen-reader-accessible equivalent); individual chart points are not keyboard-focusable.

## 6. Branding

- Organization name: the ADMIN-001 settings `displayName` (single source; "Support CRM" when not configured). Accent: six presets (`violet`, `blue`, `azure`, `green`, `orange`, `rose`). Logo: optional PNG/JPEG/WebP ≤ 512 KB. See [security-administration.md](security-administration.md) section 5 for the API, validation, ETag and audit rules.
- `GET /api/v1/branding` is anonymous and returns only `displayName`, `accentColor`, `logoVersion`; the client loads it before the first render with a 5 s timeout and falls back to the defaults if it fails (sign-in is never blocked). `logoVersion` changes on every replacement and is used as `?v=` on the logo URL, so a new logo is never masked by the cache.
- The settings page offers the accent swatches (with names, a live preview), logo choose/upload/remove (confirm dialog) and keeps unsaved text edits when a logo change updates the version.

## 7. Persistence scope

Language, theme and the collapsed sidebar are stored **per browser** in `localStorage` (`crm.lang`, `crm.theme`, `crm.navCollapsed`); there is no account-level preference store, so they are not synchronized across devices. No secret is ever stored (the access token stays in memory). Blocked storage falls back to English, the OS theme and an expanded sidebar.

## 8. Bundle

`ng build` (production), initial total: **637.91 kB raw / 149.30 kB transfer** before PLAT-001, **774.15 kB raw / 178.16 kB transfer** after. The existing 500 kB budget warning is unchanged (not raised or hidden). Arabic (~91 kB) and Chart.js (~215 kB) are lazy chunks. The paginator and datepicker label services are provided on the lazy feature routes that use them (not in `app.config`), which kept ~215 kB of Material code out of the initial bundle. The English dictionary (~75 kB) is in the initial bundle.

## 9. Not included

Departments, branches and Communication Channels; account-level preference storage; translating user content; new metrics; arbitrary colors or SVG logos.
