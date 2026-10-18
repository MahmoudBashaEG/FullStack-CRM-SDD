# Story 15 — Platform Features — Ng-Matero-Inspired Full CRM Frontend Redesign, English/Arabic RTL, Functional Calendars & Charts, and Organization Branding

## Prerequisites

- **Story 14 completed and committed:** [../security-administration/14-story-security-administration.md](../security-administration/14-story-security-administration.md) (`ADMIN-001`).
  - At planning time `git log --oneline -3` showed `779f034 feat(admin): … (ADMIN-001)` on top of `master`.
  - `git status` showed only the untracked `.squad/stories/platform-features/` and `.squad/plans/platform-features/` folders.
  - That confirms intake §2: **the ADMIN-001 commit exists.**
  - **Before implementing, run `git status` and `git log --oneline -3`. Stop and report if any application file is modified or `ADMIN-001` is missing from `git log`.** Do not depend on the exact commit id.
- **Baseline measured during planning (not assumed):**
  - Backend: **14 `[Fact]`** in `backend/tests/Crm.UnitTests` (one per file, 14 files).
  - Frontend: **14 `it(`** across 14 `*.spec.ts` files.
  - `npx ng build` (production) at `779f034`: **initial total 637.91 kB raw / 149.30 kB transfer**. It prints the existing warning "bundle initial exceeded maximum budget. Budget 500.00 kB was not met by 137.91 kB". Largest lazy chunks: `ticket-detail-page` 51.70 kB, `sla-admin-page` 44.11 kB, `customer-detail-page` 37.30 kB. **This 637.91 kB is the comparison baseline** for this story.
- **Stories 01–14 must not regress.** All of them are UI-touched by this story. Backend changes are limited to organization branding (task 1–6). Every other existing backend file stays unchanged.
- Tooling verified in `frontend/node_modules`:
  - `@angular/core` and `@angular/material` **22.2.3**, `@angular/cdk` ~22.2. Material ships `datepicker` **and** `timepicker` (both used here).
  - `@angular/common/locales/ar` and `ar-EG` exist.
  - `Directionality` (`@angular/cdk/bidi`, `types/_bidi-module-chunk.d.ts` lines 9–22) exposes a public `valueSignal: WritableSignal<Direction>` and `change: EventEmitter<Direction>`. Task 9 drives it.
- **New npm dependency (the only one):** `chart.js@4.5.1` — **MIT** (`npm view chart.js license` ⇒ `MIT`, verified during planning). It is used directly through one wrapper component; **no `ng2-charts`**.
- **Not installed (decision D1):** `@ng-matero/extensions` (22.1.1, MIT, peer `@angular/material >=22`) and the Ng-Matero starter template. Ng-Matero is used as a **visual reference only** (layout, sidebar, topbar, page-header and card patterns). No Ng-Matero source is copied, so no attribution file is needed. **If the executor copies any snippet from the Ng-Matero repository, it must add its MIT notice to `frontend/THIRD-PARTY-NOTICES.md`** (create it then).
- **No new NuGet package.** One additive EF migration: `AddOrganizationBranding`.
- Intake: `.squad/stories/platform-features/PLAT-001/intake.md` (`attachments/` is empty). **UI1, PL3, PL4 and PL5 are approved (all Option A) and are not reopened.** Departments/Branches and Communication Channels are out of scope.
- Shared contracts touched (additive unless noted):
  - Domain: `OrganizationSettings` gains `AccentColor` and logo metadata; new `OrganizationLogo` entity.
  - Application: `UpdateOrganizationSettingsCommand` gains `AccentColor` (**changed signature**); new `SetOrganizationLogoCommand`, `RemoveOrganizationLogoCommand`, `GetPublicBrandingQuery`, `GetOrganizationLogoQuery`; new `BrandingRules`; `IOrganizationSettingsRepository` gains 4 members; `OrganizationSettingsDetails` gains 5 fields.
  - API: new anonymous `BrandingController` (`GET /api/v1/branding`, `GET /api/v1/branding/logo`); `AdministrationController` gains `PUT` / `DELETE /admin/organization-settings/logo`; `OrganizationSettingsResponse` gains 5 fields and `UpdateOrganizationSettingsRequest` gains `AccentColor`.
  - Angular: new `core/i18n/`, `core/preferences/`, `core/branding/`, `core/layout/` rewrite, `shared/ui/` additions, `shared/date/`, `shared/charts/`; every feature template is touched for translation.

---

## Story Goal

Replace the visual and interaction layer of the whole CRM with a modern, workflow-focused Angular Material interface. Add English/Arabic with RTL, functional calendar pickers, interactive charts and standard organization branding. **Every existing view, action, rule and permission keeps working.**

1. **Shell and design system (UI1, PL5):** one Ng-Matero-inspired shell for staff and Customer users:
   - a grouped, role-filtered, collapsible sidebar (mini icon rail on desktop, drawer on handset);
   - a top bar with breadcrumbs, language switcher, light/dark toggle, notification bell (staff, unchanged), and a user menu (Profile, Sign out);
   - a branded auth layout for `/sign-in` and `/sign-up`;
   - shared building blocks: page header, empty state, error + Retry state, confirm dialog, KPI card, chart card.
2. **Contextual organization (PL5):**
   - Ticket detail and Customer detail become deep-linkable tabbed pages (`?tab=`);
   - Agent Dashboard, SLA, Reports, Knowledge Base and Administration get tab bars;
   - every existing URL keeps working.
3. **Dates (§6.1):** every `type="date"`, `type="datetime-local"` and `type="time"` input becomes a Material datepicker, date-range picker or datepicker + timepicker. Text entry still works as a second method. Date-only values stay `yyyy-MM-dd` UTC calendar days; date-time values keep the existing local ⇒ ISO UTC conversion. **No Cairo shift.**
4. **Charts (§6.2):** the CSS bars of the five report screens become Chart.js bar, line and doughnut charts:
   - tooltips, legends and keyboard-reachable data tables;
   - `null` percentages are **gaps labelled N/A, never 0 %**;
   - Chart.js is loaded only in the reports chunks.
5. **English + Arabic (PL3):**
   - every system-owned string on every screen is translated, including enums, validation, dialogs, snackbars, paginator and datepicker labels, and loading/empty/error states;
   - `dir="rtl"` is applied globally, including CDK overlays;
   - the language is persisted per browser;
   - user content, AI output and backend enum values are never translated.
6. **Branding (PL4):**
   - the organization name comes from the existing ADMIN-001 `DisplayName` (single source);
   - an optional SuperAdmin-managed PNG/JPEG/WebP logo, stored in SQL Server and served by a public bytes-only endpoint;
   - one of six accessible accent presets;
   - a per-browser light/dark preference;
   - a minimal **anonymous** `GET /api/v1/branding` returning only `displayName`, `accentColor` and `logoVersion`;
   - logo and accent changes are audited through the existing `OrganizationSettingsUpdated` action.
7. Exactly **one** new backend unit test and **one** new frontend unit test (14 → 15 each), new `docs/platform-ui-and-localization.md`, updated docs, and a route-by-route manual preservation matrix in both languages and both themes.

**Not in scope** (do **not** build):
- Departments, branches, or any of their tables, filters or policies.
- Communication Channels, email, SMS, WhatsApp, chat, or any outbound message.
- Account-level (cross-device) preference storage. Language and theme are **per browser** (D6).
- Translating user content or KB bodies, or translating anything with Gemini.
- A new role or permission editor.
- New metrics, report endpoints or report calculations.
- Arbitrary colors or CSS, SVG logos, or more than one logo.
- A Playwright/E2E suite.
- Replatforming, or installing the Ng-Matero template.

### Conflicts and decisions (surface these in the implementation report)

| # | Intake point | Resolution in this plan |
|---|---|---|
| D1 | UI1 "Ng-Matero-inspired", "do not force an incompatible template dependency". | **Patterns only, no package.** Ng-Matero's starter is a whole application (its own layout, routing and theme), not a drop-in library. `@ng-matero/extensions` is a component pack that this story does not need: Material 22 already has datepicker, timepicker, tabs, menus and dialogs. Adopted patterns: a sidebar with brand header and grouped menu, a collapsible mini rail, a top bar with switchers and user menu, a page header with breadcrumbs, and an outlined card grid. They are written as our own SCSS/TS under `core/layout/` and `shared/ui/`. The app is not replaced. |
| D2 | §6.2 chart library: "Chart.js (MIT) preferred, verify license". | **`chart.js@4.5.1`, MIT** (verified with `npm view`). **No `ng2-charts`**: a 90-line wrapper (`shared/charts/crm-chart.ts`) avoids a second dependency and its peer-version coupling. The wrapper `import()`s `chart.js` lazily, so the library lands in a lazy chunk used only by the report routes and never in the initial bundle. Controllers are registered explicitly (tree-shaken): bar, line, doughnut, category/linear scales, tooltip, legend, filler. |
| D3 | PL3 translation mechanism: no enterprise i18n, no translation flash, completeness inventory. | **An in-house, signal-based `I18n` service plus a `t` pipe. No i18n package.**<br>• `core/i18n/en.ts` is the **source dictionary**, a nested object.<br>• `core/i18n/ar.ts` is typed `Translations` (`DeepStrings<typeof EN>`), so **a missing or extra Arabic key is a TypeScript compile error**. That is the completeness inventory, enforced by `ng build`.<br>• English is bundled. Arabic is a lazy chunk, loaded **in the app initializer before the first render** when the stored language is `ar`, so there is no English flash.<br>• Angular's `$localize` build-time i18n is rejected: it needs one build per locale and a page reload to switch, which violates "switch without reload".<br>• `@ngx-translate/core` is rejected: it is an extra dependency with async loading that causes first-render flashes. |
| D4 | §7 numbers, IDs and mixed direction. | Arabic dates use the locale **`ar-EG-u-nu-latn`**: Arabic month and day names with **Latin digits**. Ticket IDs, emails, phone numbers, counts, percentages and chart axes therefore stay identical in both languages, and typed dates parse the same way. IDs, emails, URLs, phones and code are wrapped in `<bdi>` or `dir="ltr"` (`.ltr-value` class, task 13). Numbers in KPI cards are not reformatted. |
| D5 | §6.1 date-only semantics and Cairo drift. | The datepicker uses `provideNativeDateAdapter()`, which produces **local-midnight `Date`** objects. **Never** convert those with `toISOString()`.<br>• `toDateOnly(date)` reads the **local** `getFullYear/getMonth/getDate`, so the calendar day the user clicked becomes the `yyyy-MM-dd` string unchanged.<br>• `fromDateOnly('2030-01-31')` builds `new Date(2030, 0, 31)` (local).<br>• The API still receives the same `yyyy-MM-dd` strings it gets today, so the report half-open `[from 00:00Z, to+1 00:00Z)` windows (REPORT-001) and the audit filters (ADMIN-001) are byte-identical.<br>• Date-time fields keep `fromLocalInput` semantics (local wall time ⇒ ISO UTC) through `combineLocalDateTime`.<br>• Display of date-only UTC values keeps the existing `formatUtcDay` (UTC-anchored `Intl`), now locale-aware. |
| D6 | PL3/PL4 persistence scope: "planner verifies whether account-level persistence exists". | **It does not exist.** There is no user-preferences table or endpoint (`grep -rn "Preference" backend/src` finds none; `AuthService` keeps only the session, `auth-service.ts` line 10). The approved requirement is "persistent preference", not a database design. Decision: **`localStorage` per browser** (`crm.lang`, `crm.theme`, `crm.navCollapsed`). These keys hold no secrets; the access token stays memory-only. The scope is documented in `docs/platform-ui-and-localization.md` as "per browser, not synchronized across devices". All reads and writes are wrapped in try/catch (private mode ⇒ defaults). |
| D7 | PL4 public branding without exposing `GET /admin/organization-settings`. | **A new anonymous `BrandingController`** (`[AllowAnonymous]`, the `AuthController` precedent, `AuthController.cs` line 17).<br>• `GET /api/v1/branding` ⇒ `{ displayName, accentColor, logoVersion }` **only**. No support email, phone, information, updater identity or version.<br>• `GET /api/v1/branding/logo` ⇒ the stored image bytes, with `Content-Type` from the server allow-list, `X-Content-Type-Options: nosniff`, `Content-Security-Policy: default-src 'none'; sandbox`, `Content-Disposition: inline` and cache headers. 404 when there is no logo.<br>• The SuperAdmin endpoints keep their `SuperAdmin` policy unchanged. |
| D8 | PL4 logo storage: "safe, simple existing storage conventions". | Follow **CUST-002 attachments (bytes in SQL Server)**, as in `CustomerAttachment.cs` lines 3–33 and `CustomerRecordRules.cs` lines 12–86:<br>• a new singleton table `OrganizationLogos` (Id 1 + check constraint, `Content varbinary(max)`, `ContentType nvarchar(20)`), **separate** from `OrganizationSettings` so the settings and branding reads never load the bytes;<br>• **PNG, JPEG and WebP only**, ≤ **512 KB**. The extension **and** the magic bytes must agree; the stored content type comes from the signature table, never from the client. SVG/HTML is impossible.<br>• No file name is stored, so there is no path anywhere.<br>• There is no shared folder with customer attachments, and the branding route can only ever return the `OrganizationLogos` row. |
| D9 | PL4 audit: "no bypass of the audited settings route". | Logo set/replace/remove and accent changes go through **the same settings aggregate and the same `OrganizationSettingsUpdated` audit action**. **No new audit action**, so the ADMIN-001 list of exactly 4 actions is kept.<br>• New audited fields: `accentColor` (old/new preset id) and `logo` (old/new description, e.g. `"image/png, 48 KB"` / `null`, never bytes or a file name).<br>• The settings `rowversion` ETag protects every branding mutation: `If-Match` is required, missing ⇒ 428, stale ⇒ 409.<br>• Each mutation is a single `SaveChanges` (settings UPDATE + logo INSERT/UPDATE/DELETE + audit INSERT), so it is atomic, like `UpdateOrganizationSettingsCommand.cs` line 42. |
| D10 | PL4 accent palette. | Six presets, all **Material 3 tonal palettes** shipped with Material 22: `violet` (the current default, `styles.scss` line 7), `blue`, `azure`, `green` (`mat.$green-palette`), `orange`, `rose`. M3 derives `on-primary` and `primary-container` tones with contrast in both light and dark schemes. **No free color input.**<br>• The stored value is the preset id; `null` means `violet`.<br>• Compiled at build time as `html[data-accent='<id>']` theme blocks (task 10).<br>• The backend validates against the same 6-id allow-list (`BrandingRules.AccentPresets`). |
| D11 | PL4 `UpdateOrganizationSettingsRequest` change. | `AccentColor` is added to the existing `PUT` body (nullable). **The only consumer is `OrganizationSettingsPage`**, which this story updates to always send it. A client that omits it would reset the accent to the default, which is acceptable and documented: there is no other consumer (`grep -rn "organization-settings" frontend/src` ⇒ `admin-api.ts` only). Logo upload is a separate multipart endpoint because it is binary. |
| D12 | §5.2 tabs vs child routes; "do not lose unsaved edits". | **Tabs with `?tab=` query param** for single-entity pages (Ticket, Customer, Agent Dashboard, SLA):<br>• the forms stay in the **parent** component class, so switching tabs never discards a `FormGroup` value;<br>• `matTabContent` lazy templates delay child loads until the tab is first opened (GET only);<br>• `preserveContent` is **on** for the tab group holding the edit form.<br>**Child routes** (already present) for independent pages (Reports, Administration, KB categories), shown with `mat-tab-nav-bar`.<br>No route is removed. `/tickets/:id` without `tab` opens Overview. |
| D13 | §7 backend English errors. | `describeHttpError` and `describeSaveError` become language-aware:<br>• status-based messages are translated (0, 401, 403, 404, 409, 428, 429, 500);<br>• known server messages go through a small exact-match map (`core/i18n/server-messages.ts`, initially the messages shown in the list below);<br>• any other server text is shown **verbatim** as a safe fallback, never dropped. Backend messages are **not** changed.<br>Initial map: `"Invalid email or password."`, `"Too many attempts…"`, `"The organization settings were changed by someone else. Reload them and try again."`, `"You cannot deactivate your own account."`, `"At least one other active SuperAdmin is required."`, `"Send If-Match with the version you last loaded."`. Grep `ConflictException("` in `backend/src/Crm.Application` and add the exact strings of conflicts reachable from the UI. |
| D14 | §11 exactly one test per layer. | **Backend:** `SetOrganizationLogoCommandHandlerTests` (signature-checked logo stored, settings updated, one audit entry, one save). **Frontend:** `shared/date/date-only.spec.ts` (Cairo-safe date-only and date-time conversion), the highest-risk changed seam. Arabic completeness is enforced by the compiler (D3), not by a test. |
| D15 | Existing specs assert English strings (`admin-format.spec.ts`, `portal-timeline.spec.ts`, `ticket-workflow.spec.ts`, `report-format.spec.ts`). | **Existing tests must pass unchanged.** Label and format helpers gain an **optional last parameter** `t: Translate = englishT` (`core/i18n/translate-fn.ts`), so a call without it returns exactly today's English. **The EN dictionary copies the current literals verbatim**, so default output is byte-identical. Templates pass the live translator. |
| D16 | Initial bundle budget. | The baseline already exceeds the 500 kB warning (637.91 kB). This story adds the shell, the i18n service, the EN dictionary and the theme CSS to the initial bundle; **Arabic and Chart.js stay lazy**. Target: initial ≤ 700 kB raw. **Do not raise the budget to hide the warning.** Report the new number next to the baseline. If it exceeds 1 MB (error budget), stop and report. |

### Recorded decisions

| Concern | Decision |
|---|---|
| Naming (frontend) | `core/i18n/{i18n.ts, en.ts, ar.ts, translate.pipe.ts, translate-fn.ts, localized-date.pipe.ts, material-intl.ts, server-messages.ts}`; `core/preferences/{preferences-store.ts, theme.ts}`; `core/branding/{branding-api.ts, branding.ts}`; `core/layout/{navigation.ts, shell/*, auth-layout/*, breadcrumbs.ts, topbar-controls.ts}`; `shared/ui/{page-header, status-badge, empty-state, error-state, confirm-dialog, kpi-card, chart-card}`; `shared/date/date-only.ts`; `shared/charts/crm-chart.ts`. |
| Naming (backend) | Domain `Crm.Domain.Administration.OrganizationLogo`. Application `Crm.Application.Administration.{SetOrganizationLogo, RemoveOrganizationLogo}` and `Crm.Application.Branding.{GetPublicBranding, GetOrganizationLogo}`. Rules `Crm.Application.Common.Validation.BrandingRules`. API `Crm.Api.Controllers.BrandingController` and `Crm.Api.Contracts.Branding.BrandingContracts`. |
| Routes (new) | `GET /api/v1/branding` (anonymous) · `GET /api/v1/branding/logo` (anonymous) · `PUT /api/v1/admin/organization-settings/logo` (SuperAdmin, multipart `file`, If-Match) · `DELETE /api/v1/admin/organization-settings/logo` (SuperAdmin, If-Match). |
| Schema | `OrganizationSettings` + `AccentColor nvarchar(20) NULL`, `LogoContentType nvarchar(20) NULL`, `LogoSizeBytes int NULL`, `LogoUpdatedAtUtc datetimeoffset NULL`. New `OrganizationLogos (Id int PK CHECK (Id = 1), Content varbinary(max) NOT NULL, ContentType nvarchar(20) NOT NULL)`. |
| Storage keys | `localStorage`: `crm.lang` (`en`/`ar`), `crm.theme` (`light`/`dark`; absent = follow OS), `crm.navCollapsed` (`1`/absent). |
| Direction | `<html lang dir>` + root `Directionality.valueSignal` and `change` (task 9). CSS uses logical properties only (existing rule, `docs/frontend-ui-foundation.md` line 13). Icons with direction (`arrow_back`, `chevron_right`, `navigate_next`, `send`, `logout`) get class `.dir-icon`, which is mirrored in RTL (`[dir='rtl'] .dir-icon { transform: scaleX(-1) }`). |
| Charts | Chronological X axes are **not mirrored**: time axes stay oldest-left ⇒ newest-right in both languages (analytics convention, intake §5.1 "avoid naïvely flipping data charts"). Legends and tooltips use `rtl: true` and `textDirection: 'rtl'` in Arabic. All six accents come from the M3 palettes exported by `@angular/material/_index.scss` lines 9–11. |
| Text safety | Unchanged: interpolation only. The only `[innerHTML]` stays in `markdown-view.ts` line 9 (sanitized KB). Translations are plain text; no translation value contains HTML. |

---

## Context — Read These Files First

1. `.squad/stories/platform-features/PLAT-001/intake.md` — the contract: §3 (UI1, PL3, PL4, PL5), §4 guardrails, §5 shell, §6 dates and charts, §7 bilingual, §8 branding, §9 preservation inventory, §10 the ten acceptance scenarios, §11 the 1+1 test policy, §12 out of scope, §13 milestones and stop triggers.
2. [../security-administration/14-story-security-administration.md](../security-administration/14-story-security-administration.md) — the structure and tone of this plan, decisions C9/C10 (settings singleton, ETag) and the Verification style.
3. `frontend/src/app/app.config.ts` lines 9–22 — the provider list. Add `provideNativeDateAdapter()`, the Material intl providers and three initializers (task 9) **after** `provideAppInitializer(() => inject(AuthService).restoreSession())` (line 20). Order matters: i18n first, then branding, then the session.
4. `frontend/src/app/app.routes.ts` lines 5–99 — every route and its `roleGuard`. **Guards and paths do not change.** Add `data: { crumb: '<key>' }` only.
5. `frontend/src/app/core/layout/navigation.ts` lines 3–32 — `NavItem` and the 18 `NAV_ITEMS` with their `roles`. The redesign **regroups** these same items; the roles of every item stay identical.
6. `frontend/src/app/core/layout/shell/shell.ts` lines 15–49, `shell.html` lines 1–51 and `shell.scss` lines 1–58 — the current shell: `BreakpointObserver(Handset)`, `navItems` computed filter (line 38), `NotificationBell` for staff (line 37–39), sign out (line 41–43). The rewrite keeps these behaviours.
7. `frontend/src/styles.scss` lines 3–13 (`mat.theme` violet/cyan, `color-scheme: light dark`) and 15–28 (`--crm-*` tokens). Task 10 extends this file.
8. `frontend/src/index.html` line 2 (`<html lang="en">`) and lines 10–11 (Inter and Material Icons fonts). Inter has no Arabic glyphs: add **Noto Sans Arabic** in the same Google Fonts link (task 10).
9. `frontend/src/app/shared/ui/page-header/page-header.ts` lines 1–25 and `status-badge/status-badge.ts` lines 1–47 — the existing shared UI, to extend, not replace.
10. `frontend/src/app/core/api/describe-http-error.ts` lines 5–14 and `features/sla/sla-errors.ts` lines 6–10 (`describeSaveError`) — the two error formatters used by every page (D13).
11. `frontend/src/app/core/auth/auth-interceptor.ts` lines 8–15 — `isBusinessApi` adds the bearer token to every `/api/v1` call. The anonymous branding GET passes through harmlessly: it never returns 401, so it never triggers a refresh.
12. **Reports** (`frontend/src/app/features/reports/`):
    - `report-bars.ts` lines 11–47: the CSS bars, to be replaced;
    - `report-format.ts`: line 6 `UTC_DAY` (en-US), lines 10–12 `formatPercent` (null ⇒ 'N/A'), lines 28–30 `formatUtcDay`, lines 33–36 `formatBucketLabel`, line 39 `parsePeriodParams`;
    - `report-period-filter.ts`: lines 30–45 (custom `type="date"` form) and lines 86–98 (`applyCustom`: both dates required, `from <= to` lexical);
    - `reports-models.ts` lines 3–12 (presets and `PRESET_LABELS`) and lines 21–148 (every chartable field);
    - `sla-report-page.ts` lines 38–51: **`value: b.responseCompliancePercent ?? 0` draws N/A as a 0-length bar.** The chart must render `null` as a gap (§10.7);
    - `ticket-report-page.ts` lines 38–49 and `ticket-report-page.html` lines 52–91;
    - `management-dashboard-page.html` lines 24–29 (report link buttons) and 157–194 (three chart cards with per-section error);
    - `agent-report-page.ts` lines 49–51 and `csat-report-page.ts` lines 50–52.
13. **Date inputs** (the complete list from `grep -rn 'type="date\|type="datetime-local\|type="time"'`):
    - `features/reports/report-period-filter.ts` lines 34 and 38 (date-only, UTC days);
    - `features/admin/audit-logs-page.html` lines 17 and 22 (date-only, UTC days);
    - `features/sla/business-calendar-form.html` lines 44 and 48 (`opensAt`/`closesAt`, `HH:mm` wall time in the calendar zone), line 56 (holiday, date-only) and line 101 (`previewStart`, local date-time ⇒ ISO, `business-calendar-form.ts` line 172);
    - `features/tasks/task-edit-page.html` lines 21, 39, 81 and 110 (`due`, `remindAt`, local ⇒ ISO via `fromLocalInput` / `toLocalInput`);
    - `features/tasks/task-models.ts` lines 80–92 (`fromLocalInput`, `toLocalInput`);
    - `features/customers/customer-interactions.html` line 14 and `customer-interactions.ts` lines 59 and 105–120 (`occurredAt`, future tolerance).
14. **Oversized pages:**
    - `features/tickets/ticket-detail-page.html` lines 1–240. Card order: Summary 11–47, SLA 49–61, Workflow 63–105, Assignment 107–144, Edit details 147–196, Activity 198–205, Internal discussion 207–212, Collaborators 214–219, Quick replies 221–224, Knowledge Base 226–231, AI assistance 233–238. `ticket-detail-page.ts` line 74 reads `id` from the snapshot.
    - `features/customers/customer-detail-page.html` lines 1–121: Summary at 13, Edit at 44, Notes 102–103, Interactions 107–108, Related tickets 112–113, Attachments 117–118. `customer-detail-page.ts` line 48.
    - `features/agent-dashboard/agent-dashboard-page.html`: toolbar with 7 links at lines 3–16, Workload 28–60, Needs attention from 62, My tasks from 103.
    - `features/sla/sla-admin-page.html` lines 6–19: already a `mat-tab-group` (Policy, Calendar, Rules, Overview), not deep-linkable.
15. `frontend/src/app/features/ai/ticket-ai-assistant.ts` lines 103–105 — "No hook ever calls the AI". The AI tab must keep this: **mounting the tab sends nothing.**
16. `window.confirm` call sites to replace with the shared dialog: `features/admin/admin-user-detail-page.ts` line 69 and `features/collaboration/ticket-collaborators.ts` line 52.
17. **Label helpers to make language-aware (D15):** `grep -rn "function [a-zA-Z]*Label\|_LABELS" frontend/src/app --include=*.ts`. Today they are `admin-format.ts` lines 3–24, `customer-records-models.ts` line 8, `knowledge-base-models.ts` lines 10–19, `reports-models.ts` line 7, `automation-rules.ts` lines 32–43 and 72–73, `sla-models.ts` lines 155 and 179, `ticket-models.ts` line 132, plus `portal-timeline.ts` and `ticket-workflow.ts` (labels inside the returned objects).
18. **Backend organization settings (ADMIN-001):**
    - `backend/src/Crm.Domain/Administration/OrganizationSettings.cs` lines 9–60 (`Update` 26–51, `Track` 53–59);
    - `backend/src/Crm.Application/Administration/UpdateOrganizationSettings/UpdateOrganizationSettingsCommand.cs`: record lines 12–14, version check line 25, update lines 31–37, audit + single save lines 39–43, validator lines 49–61;
    - `GetOrganizationSettings/GetOrganizationSettingsQuery.cs` lines 16–26 (`OrganizationSettingsMapper`);
    - `Administration/AdministrationDetails.cs` lines 12–14;
    - `Abstractions/Persistence/IOrganizationSettingsRepository.cs` lines 6–15;
    - `Crm.Infrastructure/Persistence/Configurations/OrganizationSettingsConfiguration.cs` lines 8–22;
    - `Repositories/OrganizationSettingsRepository.cs` lines 7–20;
    - `CrmDbContext.cs` lines 44–45;
    - `Infrastructure/DependencyInjection.cs` lines 62–63;
    - `Crm.Api/Controllers/AdministrationController.cs` lines 59–76;
    - `Contracts/Administration/AdministrationContracts.cs` lines 34–44;
    - `Http/EntityTags.cs` lines 18–44;
    - `Domain/Administration/AdminAuditEntry.cs` lines 33–42.
19. **Upload precedent:** `backend/src/Crm.Application/Common/Validation/CustomerRecordRules.cs` lines 12–86 (`CustomerAttachmentRules`: size, sanitize, extension allow-list, magic bytes) and `Crm.Api/Controllers/CustomerRecordsController.cs` lines 76–96 (multipart upload, `RequestSizeLimit`, `CopyToAsync`) and 98–105 (`nosniff`, cache headers, `File(...)`).
20. **Test patterns:** `backend/tests/Crm.UnitTests/Customers/UploadCustomerAttachmentCommandHandlerTests.cs` lines 1–25 (fakes, `FixedTimeProvider`, `"%PDF-…"u8` content) and `Administration/SetAccountStatusCommandHandlerTests.cs` lines 11–47 (audit assertions). Frontend: `features/reports/report-format.spec.ts` (one `describe`, one `it`, many `expect`s).
21. `frontend/src/app/features/admin/`:
    - `organization-settings-page.ts` lines 31–36 (form), 72–114 (`save` with 409/400 handling) and 116–125 (`apply`);
    - `organization-settings-page.html` lines 22–62;
    - `admin-api.ts` lines 63–72;
    - `admin-models.ts` lines 44–60;
    - `admin-format.ts` lines 10–17 (`FIELD_LABELS`: add `accentColor`, `logo`);
    - `admin-section-nav.ts` (button nav ⇒ tab nav bar).
22. `docs/frontend-ui-foundation.md` lines 3–17 — the foundation rules. Line 13 ("No RTL or localization logic yet") is replaced by this story.

---

## Product rules (from story)

### Screen-by-screen preservation inventory (intake §9 — mandatory)

Every row lists the existing route, its guard (unchanged), the essential actions and data calls that must still work, the intended transformation and the regression check. "Tr" = all system strings translated (task 15). Guard column = `app.routes.ts` / feature routes.

| # | Route (component) | Guard | Must keep (actions / data) | Transformation | Regression check |
|---|---|---|---|---|---|
| 1 | `/sign-in` (`SignInPage`) | `guestGuard` | Login, error 401/429, link to sign-up, redirect to the role home | `AuthLayout` with logo, org name, language + theme switch. Tr | Login as each role; wrong password ⇒ translated message |
| 2 | `/sign-up` (`SignUpPage`) | `guestGuard` | Customer registration, validation, server 400/409 | Same layout. Tr | Register a new Customer; duplicate email ⇒ 409 message |
| 3 | `/` (`HomePage`) | `authGuard` | Role cards, system status | KPI/card grid. Tr | All roles see their cards |
| 4 | `/profile` (`ProfilePage`) | `authGuard` | Edit details, change password | Two cards in a 2-column grid (1 column at 400 px). Tr | Save details; change password; wrong current ⇒ error |
| 5 | `/agent-dashboard` (`AgentDashboardPage`) | Agent | Workload counts, attention queue, my tasks, Refresh, 7 quick links | Tabs `overview` / `tasks` (`?tab=`); quick links ⇒ an action chip row. Tr | Counts equal the API; links work; Refresh = 1 GET |
| 6 | `/tickets` (`TicketListPage`) | SuperAdmin, Agent | Search, filters, `mat-table`, paginator, New ticket | Card table; filters in an expansion row at 400 px; translated paginator. Tr | Filters and paging give the same rows; Agent sees only assigned |
| 7 | `/tickets/new` (`TicketCreatePage`) | SuperAdmin, Agent | Customer picker, fields, create | Form card, sticky actions. Tr | Create ⇒ 201 and navigate |
| 8 | `/tickets/:id` (`TicketDetailPage`) | SuperAdmin, Agent | Summary, SLA + mark responded, transitions, escalate/clear, assignment, edit, activity, discussion, collaborators, quick replies, KB links, AI | Header (subject, badges, primary actions) + tabs `overview` (Summary, SLA, Workflow, Assignment), `details` (Edit), `activity`, `collaboration` (Discussion + Collaborators), `knowledge` (KB + Quick replies), `ai` | Every action works from its tab; `?tab=ai` sends **no** AI request; unsaved Edit survives tab switches; Agent 404 ⇒ access-lost |
| 9 | `/customers` (`CustomerListPage`) | SuperAdmin, Agent | Search, table, paginator, New | Same as #6. Tr | Same rows |
| 10 | `/customers/new` | SuperAdmin, Agent | Create | Form card. Tr | Create works |
| 11 | `/customers/:id` (`CustomerDetailPage`) | SuperAdmin, Agent | Summary, edit, notes, interactions (date-time), related tickets, attachments | Tabs `profile`, `tickets`, `notes`, `interactions`, `attachments`; interaction date via date + time pickers | Each sub-component loads/saves as before; upload/download attachment |
| 12 | `/agents`, `/agents/new`, `/agents/:id` | SuperAdmin | List, create Agent, edit, status, password | Restyle. Status change uses the confirm dialog where a confirm exists. Tr | Create Agent ⇒ audit row still written |
| 13 | `/tasks`, `/tasks/new`, `/tasks/:id` | SuperAdmin, Agent | List filters, create/edit, due, reminder, status, link ticket, owner | Datepicker + timepicker for `due` / `remindAt`. Tr | Saved `dueAtUtc` / `remindAtUtc` identical to the old input for the same wall time; ETag 409 still shown |
| 14 | `/notifications` (`NotificationsPage` + `SlaAlertList`) | SuperAdmin, Agent | List, mark read, SLA alerts | Restyle. Tr | Mark read works; the bell count updates |
| 15 | `/quick-replies`, `/new`, `/:id` | SuperAdmin, Agent | CRUD, copy | Restyle. Tr | CRUD works |
| 16 | `/collaboration`, `/collaboration/tickets/:id` | Agent | My collaborations, discussion | Restyle; remove-collaborator confirm ⇒ dialog. Tr | Post a message; remove a collaborator |
| 17 | `/sla` (`SlaAdminPage`) | SuperAdmin | Policy, calendar (days, hours, holidays, preview), rules, overview | Tabs deep-linkable `?tab=policy|calendar|rules|overview`; holiday datepicker; open/close timepicker; preview date + time pickers. Tr | Save calendar ⇒ same JSON as before (`HH:mm`, `yyyy-MM-dd`); preview result identical |
| 18 | `/knowledge-base`, `/new`, `/categories`, `/:id`, `/:id/edit` | SuperAdmin, Agent (categories SuperAdmin) | Search, list, create/edit, publish workflow, categories | Tab nav bar Articles / Categories (Categories link only for SuperAdmin). Tr | Agent cannot see Categories; publish rules unchanged |
| 19 | `/ai-assistant` (`AiAssistantPage`) | SuperAdmin, Agent | Explicit chat | Restyle. Tr (AI output untranslated) | Opening the page sends no request |
| 20 | `/portal` + `tickets`, `new-ticket`, `tickets/:id`, `help-center`, `help-center/:id` | Customer | Home, own list, create, detail + history, CSAT create/edit, Help Center | Branded customer navigation group; cards; 400 px layouts. Tr | Own tickets only; CSAT edit keeps the ETag flow |
| 21 | `/reports` + `tickets`, `sla`, `agents`, `customer-satisfaction` | SuperAdmin | Period presets + custom, refresh, per-section errors, live vs period labels, tables | Tab nav bar; date-range picker; Chart.js charts + data tables. Tr | Same numbers as before for the same period; N/A gaps |
| 22 | `/admin` + `users`, `users/:id`, `permissions`, `audit-logs`, `settings` | SuperAdmin | Users list/detail/status, matrix, audit filters, settings ETag | Tab nav bar; audit date pickers; settings gains Branding card (accent, logo). Tr | Audit filter returns the same rows; settings 409 flow; logo audited |
| 23 | `**` | — | Redirect to `/` | Unchanged | — |

### Role visibility (unchanged, display only — the API enforces)

| Nav group (new) | Items (existing `NAV_ITEMS` entries, same `roles`) |
|---|---|
| `nav.group.portal` | Support Portal, My Tickets, New Ticket, Help Center — Customer |
| `nav.group.workspace` | Home (all), My Dashboard (Agent), Tickets, Customers, Tasks (SuperAdmin, Agent), My Collaborations (Agent) |
| `nav.group.knowledge` | Knowledge Base, Quick Replies, AI Assistant — SuperAdmin, Agent |
| `nav.group.management` | Agents, SLA & Automation, Reports, Administration — SuperAdmin |
| `nav.group.account` | Profile (all) |

A group whose items are all filtered out is not rendered.

---

## Backend Tasks

### 1 — Domain: accent and logo on the settings aggregate

**File: `backend/src/Crm.Domain/Administration/OrganizationSettings.cs`**

- Add properties after `SupportInformation` (line 19):

```csharp
public string? AccentColor { get; private set; }           // BrandingRules.AccentPresets id; null = default (violet)
public string? LogoContentType { get; private set; }       // image/png | image/jpeg | image/webp; null = no logo
public int? LogoSizeBytes { get; private set; }
public DateTimeOffset? LogoUpdatedAtUtc { get; private set; } // also the public cache-busting version

public bool HasLogo => LogoContentType is not null;
```

- Change `Update` (lines 26–51) to take `string? accentColor` after `supportInformation`. Add `Track(changes, "accentColor", AccentColor, accentColor);` and assign `AccentColor = accentColor;`. Keep everything else, including the early return for no changes.
- Add:

```csharp
/// <summary>Logo set or replaced. Bytes live in OrganizationLogo; this row keeps the metadata + audit trail.</summary>
public IReadOnlyList<AdminAuditChange> SetLogo(string contentType, int sizeBytes, Guid actorUserId, DateTimeOffset now)
{
    var changes = new List<AdminAuditChange> { AdminAuditChange.Of("logo", DescribeLogo(), Describe(contentType, sizeBytes)) };
    LogoContentType = contentType;
    LogoSizeBytes = sizeBytes;
    LogoUpdatedAtUtc = now;
    UpdatedAtUtc = now;
    UpdatedByUserId = actorUserId;
    return changes; // always a change: a replace with identical bytes is still an explicit admin action
}

/// <summary>Empty when there was no logo (no-op, no audit).</summary>
public IReadOnlyList<AdminAuditChange> RemoveLogo(Guid actorUserId, DateTimeOffset now)
{
    if (!HasLogo) return [];
    var changes = new List<AdminAuditChange> { AdminAuditChange.Of("logo", DescribeLogo(), null) };
    LogoContentType = null;
    LogoSizeBytes = null;
    LogoUpdatedAtUtc = null;
    UpdatedAtUtc = now;
    UpdatedByUserId = actorUserId;
    return changes;
}

private string? DescribeLogo() => HasLogo ? Describe(LogoContentType!, LogoSizeBytes ?? 0) : null;
private static string Describe(string contentType, int sizeBytes) => $"{contentType}, {Math.Max(1, sizeBytes / 1024)} KB";
```

**Create file: `backend/src/Crm.Domain/Administration/OrganizationLogo.cs`**

```csharp
namespace Crm.Domain.Administration;

/// <summary>PLAT-001 D8: the one organization logo (Id 1). Bytes only; metadata and audit are on OrganizationSettings.
/// ContentType comes from the server signature allow-list, never from the client. Never shares storage with customer attachments.</summary>
public sealed class OrganizationLogo
{
    public const int SingletonId = 1;

    private OrganizationLogo() { } // EF Core

    public int Id { get; private set; }
    public byte[] Content { get; private set; } = [];
    public string ContentType { get; private set; } = string.Empty;

    public static OrganizationLogo Create(byte[] content, string contentType) =>
        new() { Id = SingletonId, Content = content, ContentType = contentType };

    public void Replace(byte[] content, string contentType)
    {
        Content = content;
        ContentType = contentType;
    }
}
```

### 2 — Application: rules, details, repository port

**Create file: `backend/src/Crm.Application/Common/Validation/BrandingRules.cs`** (pattern `CustomerAttachmentRules`, `CustomerRecordRules.cs` lines 12–86):

```csharp
namespace Crm.Application.Common.Validation;

/// <summary>PLAT-001 D8/D10. Enforced on the server; the Angular checks are a usability aid only.</summary>
public static class BrandingRules
{
    public static readonly IReadOnlyList<string> AccentPresets = ["violet", "blue", "azure", "green", "orange", "rose"];
    public const int LogoMaxBytes = 512 * 1024;
    public const long LogoRequestSizeLimitBytes = 1024 * 1024; // above LogoMaxBytes so slightly larger files get the friendly message
    public const string LogoAllowedTypesText = "PNG, JPEG or WebP";

    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] Riff = "RIFF"u8.ToArray();
    private static readonly byte[] Webp = "WEBP"u8.ToArray();

    public static bool IsAccentPreset(string? value) => value is null || AccentPresets.Contains(value, StringComparer.Ordinal);

    /// <summary>Content type from the magic bytes; null when not PNG/JPEG/WebP.</summary>
    public static string? DetectImageType(ReadOnlySpan<byte> content) =>
        content.StartsWith(Png) ? "image/png"
        : content.StartsWith(Jpeg) ? "image/jpeg"
        : content.Length >= 12 && content.StartsWith(Riff) && content[8..12].SequenceEqual(Webp) ? "image/webp"
        : null;

    /// <summary>First problem with the upload, or null. The extension must agree with the signature.</summary>
    public static string? CheckLogo(string? fileName, byte[] content) { /* see below */ }
}
```

`CheckLogo` order (each returns one message, key `file`):
1. empty ⇒ "Select an image to upload.";
2. `> LogoMaxBytes` ⇒ "The logo is larger than 512 KB.";
3. extension (`CustomerAttachmentRules.SanitizeFileName`, then `Path.GetExtension`, lower-case) not in `.png .jpg .jpeg .webp` ⇒ $"Upload a {LogoAllowedTypesText} image.";
4. `DetectImageType` null, or not matching the extension's type ⇒ "The file content is not a valid PNG, JPEG or WebP image.".

**File: `backend/src/Crm.Application/Administration/AdministrationDetails.cs`** — extend `OrganizationSettingsDetails` (lines 12–14) by appending `string? AccentColor, bool HasLogo, string? LogoContentType, int? LogoSizeBytes, DateTimeOffset? LogoUpdatedAtUtc` **before** `byte[] Version`. Add:

```csharp
public sealed record PublicBrandingDetails(string? DisplayName, string? AccentColor, DateTimeOffset? LogoUpdatedAtUtc);
public sealed record OrganizationLogoFile(byte[] Content, string ContentType);
```

**File: `backend/src/Crm.Application/Abstractions/Persistence/IOrganizationSettingsRepository.cs`** — add after line 14:

```csharp
/// <summary>Tracked logo row; null = no logo.</summary>
Task<OrganizationLogo?> FindLogoAsync(CancellationToken cancellationToken = default);
void AddLogo(OrganizationLogo logo);
void RemoveLogo(OrganizationLogo logo);

/// <summary>AsNoTracking projection of the public fields only (never support data or the updater). null = migration missing.</summary>
Task<PublicBrandingDetails?> GetPublicBrandingAsync(CancellationToken cancellationToken = default);

/// <summary>AsNoTracking bytes for the public logo route; null = no logo.</summary>
Task<OrganizationLogoFile?> GetLogoFileAsync(CancellationToken cancellationToken = default);
```

(The `using Crm.Application.Administration;` import is needed for the two detail records.)

**File: `GetOrganizationSettings/GetOrganizationSettingsQuery.cs`** — `ToDetailsAsync` (lines 22–26) passes the 5 new values: `s.AccentColor, s.HasLogo, s.LogoContentType, s.LogoSizeBytes, s.LogoUpdatedAtUtc`.

### 3 — Application: accent on the existing update command

**File: `UpdateOrganizationSettings/UpdateOrganizationSettingsCommand.cs`**

- Record (lines 12–14): append `string? AccentColor` after `SupportInformation`.
- Handler (lines 31–37): pass `AdministrationRules.Normalize(command.AccentColor)` to `current.Update` after the support information.
- Validator (lines 49–61): add `RuleFor(c => c.AccentColor).Must(v => BrandingRules.IsAccentPreset(AdministrationRules.Normalize(v))).WithMessage("Choose one of the available accent colors.");`.
- No other change. A no-op save (including the same accent) still writes nothing (`changes.Count == 0`).

### 4 — Application: logo commands

**Create file: `backend/src/Crm.Application/Administration/SetOrganizationLogo/SetOrganizationLogoCommand.cs`**

```csharp
/// <summary>Single save: settings metadata UPDATE (rowversion-checked) + logo INSERT/UPDATE + audit INSERT (D9).</summary>
public sealed record SetOrganizationLogoCommand(Guid ActorUserId, byte[] ExpectedVersion, string FileName, byte[] Content)
    : ICommand<OrganizationSettingsDetails>;

public sealed class SetOrganizationLogoCommandHandler(
    IOrganizationSettingsRepository settings,
    IAdminAuditRepository audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetOrganizationLogoCommand, OrganizationSettingsDetails>
{
    public async ValueTask<OrganizationSettingsDetails> Handle(SetOrganizationLogoCommand command, CancellationToken cancellationToken)
    {
        var current = await settings.RequireAsync(cancellationToken);
        if (!settings.GetVersion(current).AsSpan().SequenceEqual(command.ExpectedVersion))
        {
            throw new ConflictException("The organization settings were changed by someone else. Reload them and try again.");
        }

        var contentType = BrandingRules.DetectImageType(command.Content)!; // validator guarantees a supported signature
        var now = timeProvider.GetUtcNow();
        var changes = current.SetLogo(contentType, command.Content.Length, command.ActorUserId, now);

        var logo = await settings.FindLogoAsync(cancellationToken);
        if (logo is null) settings.AddLogo(OrganizationLogo.Create(command.Content, contentType));
        else logo.Replace(command.Content, contentType);

        audit.Add(AdminAuditEntry.OrganizationSettingsUpdated(command.ActorUserId, changes, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await OrganizationSettingsMapper.ToDetailsAsync(current, settings, cancellationToken);
    }
}

public sealed class SetOrganizationLogoValidator : AbstractValidator<SetOrganizationLogoCommand>
{
    public SetOrganizationLogoValidator()
    {
        RuleFor(c => c.ExpectedVersion).NotEmpty();
        RuleFor(c => c).Custom((c, context) =>
        {
            if (BrandingRules.CheckLogo(c.FileName, c.Content) is { } problem) context.AddFailure("file", problem);
        });
    }
}
```

`OrganizationSettingsMapper` is `internal static` in the same assembly (`GetOrganizationSettingsQuery.cs` line 16), so it is reachable. Use the same `using` set as `UpdateOrganizationSettingsCommand.cs` lines 1–7.

**Create file: `RemoveOrganizationLogo/RemoveOrganizationLogoCommand.cs`** — `RemoveOrganizationLogoCommand(Guid ActorUserId, byte[] ExpectedVersion)`, same version check. `changes = current.RemoveLogo(...)`. When `changes.Count > 0`: `FindLogoAsync` ⇒ `RemoveLogo(logo)` if not null, add the audit entry, one `SaveChangesAsync`. Otherwise do not save (no-op, no audit, same version). Validator: `ExpectedVersion` not empty.

### 5 — Application: public branding queries

**Create file: `backend/src/Crm.Application/Branding/GetPublicBranding/GetPublicBrandingQuery.cs`**

```csharp
public sealed record GetPublicBrandingQuery : IQuery<PublicBrandingDetails>;

public sealed class GetPublicBrandingQueryHandler(IOrganizationSettingsRepository settings)
    : IQueryHandler<GetPublicBrandingQuery, PublicBrandingDetails>
{
    public async ValueTask<PublicBrandingDetails> Handle(GetPublicBrandingQuery query, CancellationToken cancellationToken) =>
        await settings.GetPublicBrandingAsync(cancellationToken)
            ?? throw new InvalidOperationException("Organization settings row is missing; apply the migrations (dotnet ef database update).");
}
```

**Create file: `Branding/GetOrganizationLogo/GetOrganizationLogoQuery.cs`** — returns `OrganizationLogoFile`. `null` ⇒ `NotFoundException("No organization logo is configured.")`.

### 6 — Infrastructure, migration and API

**File: `OrganizationSettingsConfiguration.cs`** (lines 8–22) — add:

```csharp
builder.Property(s => s.AccentColor).HasMaxLength(20);
builder.Property(s => s.LogoContentType).HasMaxLength(20);
builder.Ignore(s => s.HasLogo);
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/OrganizationLogoConfiguration.cs`**

```csharp
internal sealed class OrganizationLogoConfiguration : IEntityTypeConfiguration<OrganizationLogo>
{
    public void Configure(EntityTypeBuilder<OrganizationLogo> builder)
    {
        builder.ToTable("OrganizationLogos", t => t.HasCheckConstraint("CK_OrganizationLogos_Singleton", "[Id] = 1"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Content).IsRequired();          // varbinary(max)
        builder.Property(l => l.ContentType).HasMaxLength(20).IsRequired();
    }
}
```

**File: `CrmDbContext.cs`** — after line 45 add `public DbSet<OrganizationLogo> OrganizationLogos => Set<OrganizationLogo>();`.

**File: `OrganizationSettingsRepository.cs`** (lines 7–20) — implement the 5 new members:
- `FindLogoAsync`: `dbContext.OrganizationLogos.SingleOrDefaultAsync(l => l.Id == OrganizationLogo.SingletonId, ct)`;
- `AddLogo` / `RemoveLogo`: `dbContext.OrganizationLogos.Add/Remove`;
- `GetPublicBrandingAsync`: `dbContext.OrganizationSettings.AsNoTracking().Where(s => s.Id == OrganizationSettings.SingletonId).Select(s => new PublicBrandingDetails(s.DisplayName, s.AccentColor, s.LogoUpdatedAtUtc)).SingleOrDefaultAsync(ct)`. **Only these 3 columns are selected.**
- `GetLogoFileAsync`: `dbContext.OrganizationLogos.AsNoTracking().Where(...).Select(l => new OrganizationLogoFile(l.Content, l.ContentType)).SingleOrDefaultAsync(ct)`.

No DI change: the repository is already registered (`DependencyInjection.cs` line 63), and Mediator source-generates the new handlers and validators.

**Migration:** in `backend/`:

```powershell
dotnet ef migrations add AddOrganizationBranding --project src/Crm.Infrastructure --startup-project src/Crm.Api
```

Review the generated file. It must contain only:
- 4 `AddColumn` calls on `OrganizationSettings`, all `nullable: true`;
- 1 `CreateTable("OrganizationLogos")` with the check constraint;
- a `Down` that drops exactly those.

**No `AlterColumn`, no data change, no `HasData`.** The existing row Id 1 keeps its values; the new columns are NULL, meaning default accent and no logo.

**Create file: `backend/src/Crm.Api/Contracts/Branding/BrandingContracts.cs`**

```csharp
namespace Crm.Api.Contracts.Branding;

/// <summary>Public, anonymous: ONLY these three fields (D7). logoVersion null = no logo.</summary>
public sealed record PublicBrandingResponse(string? DisplayName, string? AccentColor, string? LogoVersion)
{
    public static PublicBrandingResponse From(PublicBrandingDetails d) =>
        new(d.DisplayName, d.AccentColor, d.LogoUpdatedAtUtc?.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture));
}
```

**Create file: `backend/src/Crm.Api/Controllers/BrandingController.cs`**

```csharp
[ApiController]
[AllowAnonymous]
[Route(ApiRoutes.V1 + "/branding")]
public sealed class BrandingController(ISender sender) : ControllerBase
{
    [EndpointDescription("Anonymous. Public branding for the sign-in page and shells: displayName (null = not configured), accentColor preset id (null = default), logoVersion (null = no logo). No support or account data.")]
    [HttpGet]
    public async Task<ActionResult<PublicBrandingResponse>> Get(CancellationToken cancellationToken)
    {
        Response.Headers.CacheControl = "no-cache";
        return Ok(PublicBrandingResponse.From(await sender.Send(new GetPublicBrandingQuery(), cancellationToken)));
    }

    [EndpointDescription("Anonymous. The organization logo bytes (PNG, JPEG or WebP only). 404 when none. Use ?v=<logoVersion> for cache busting.")]
    [HttpGet("logo")]
    public async Task<IActionResult> Logo(CancellationToken cancellationToken)
    {
        var file = await sender.Send(new GetOrganizationLogoQuery(), cancellationToken);
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        Response.Headers.CacheControl = "public, max-age=86400";
        Response.Headers.ContentDisposition = "inline";
        return File(file.Content, file.ContentType); // no file name ⇒ no attachment disposition
    }
}
```

**File: `AdministrationController.cs`** — after `UpdateSettings` (line 70) add:

```csharp
[EndpointDescription("Requires role: SuperAdmin. multipart/form-data, field 'file'. PNG, JPEG or WebP, ≤ 512 KB (extension and signature checked). Sets or replaces the organization logo; audited as OrganizationSettingsUpdated (field 'logo')." + Concurrency)]
[HttpPut("organization-settings/logo")]
[Consumes("multipart/form-data")]
[RequestSizeLimit(BrandingRules.LogoRequestSizeLimitBytes)]
[RequestFormLimits(MultipartBodyLengthLimit = BrandingRules.LogoRequestSizeLimitBytes)]
public async Task<ActionResult<OrganizationSettingsResponse>> SetLogo(IFormFile? file, CancellationToken cancellationToken)
{
    var version = EntityTags.RequireIfMatch(Request);
    byte[] content = [];
    if (file is not null)
    {
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, cancellationToken);
        content = buffer.ToArray();
    }
    return SettingsResult(await sender.Send(
        new SetOrganizationLogoCommand(User.GetUserId(), version, file?.FileName ?? string.Empty, content), cancellationToken));
}

[EndpointDescription("Requires role: SuperAdmin. Removes the organization logo. No logo ⇒ 200 unchanged (no audit)." + Concurrency)]
[HttpDelete("organization-settings/logo")]
public async Task<ActionResult<OrganizationSettingsResponse>> RemoveLogo(CancellationToken cancellationToken) =>
    SettingsResult(await sender.Send(new RemoveOrganizationLogoCommand(User.GetUserId(), EntityTags.RequireIfMatch(Request)), cancellationToken));
```

- `UpdateSettings` (lines 66–70): pass `request.AccentColor`.

**File: `AdministrationContracts.cs`**:
- `OrganizationSettingsResponse` (lines 35–42): add `string? AccentColor, bool HasLogo, string? LogoContentType, int? LogoSizeBytes, DateTimeOffset? LogoUpdatedAtUtc` before `string Version`, and map them in `From`.
- `UpdateOrganizationSettingsRequest` (line 44): append `string? AccentColor`.

**Docs:** `docs/identity-and-access.md` — add the two anonymous rows next to line 100 (`/api/health`): `GET /api/v1/branding` and `GET /api/v1/branding/logo`, both "anonymous (public branding only)". Add the two SuperAdmin logo routes to the admin table.

### 7 — Backend unit test (exactly one)

See Test Plan item 1.

---

## Frontend Tasks

Execute tasks 8–20 in order. They follow intake milestones M2 (8–12), M3 (13–14), M4 (15–18) and M5 (19). Run `npx ng build` after tasks 12, 14 and 18 as checkpoints.

### 8 — Dependency

In `frontend/`: `npm install chart.js@4.5.1 --save-exact`. **Install nothing else.** Record in the report: `npm view chart.js@4.5.1 license` ⇒ MIT, and `npm ls chart.js` ⇒ one copy.

### 9 — i18n core, direction, Material intl, preferences

**Create file: `frontend/src/app/core/preferences/preferences-store.ts`**

```ts
/** PLAT-001 D6: per-browser preferences. Never secrets; the access token is never stored. Private mode / blocked storage ⇒ defaults. */
export type PreferenceKey = 'crm.lang' | 'crm.theme' | 'crm.navCollapsed';

export function readPreference(key: PreferenceKey): string | null {
  try { return globalThis.localStorage?.getItem(key) ?? null; } catch { return null; }
}

export function writePreference(key: PreferenceKey, value: string | null): void {
  try {
    if (value === null) globalThis.localStorage?.removeItem(key);
    else globalThis.localStorage?.setItem(key, value);
  } catch { /* storage unavailable: preference lasts for this page only */ }
}
```

**Create file: `frontend/src/app/core/i18n/en.ts`** — `export const EN = { … }`. A nested object, namespaces in this order:

`app, common, nav, shell, auth, home, profile, dashboard, tickets, customers, agents, tasks, notifications, quickReplies, collaboration, sla, kb, ai, portal, reports, admin, branding, errors, validation, enums, dates, charts, paginator, datepicker`

Rules:
- **Values copy today's English literals verbatim** (D15). Interpolation uses `{name}` placeholders.
- Enum labels live under `enums.<enumName>.<ApiValue>`, keyed by the **API value**, for example `enums.ticketStatus.InProgress: 'In Progress'`.
- `app.name: 'Support CRM'` is the fallback organization name, used when `displayName` is null.

**Create file: `frontend/src/app/core/i18n/ar.ts`**

```ts
import type { EN } from './en';

type DeepStrings<T> = { readonly [K in keyof T]: T[K] extends string ? string : DeepStrings<T[K]> };
export type Translations = DeepStrings<typeof EN>;

/** Must mirror EN exactly: a missing or extra key fails `ng build` (PLAT-001 D3, completeness inventory). */
export const AR: Translations = { /* … */ };
```

Arabic is professional Modern Standard Arabic. Keep product terms consistent, for example: تذكرة (ticket), عميل (customer), وكيل (agent), اتفاقية مستوى الخدمة (SLA), قاعدة المعرفة (Knowledge Base), لوحة التحكم (dashboard), التقارير (reports), الإدارة (administration), مساعد الذكاء الاصطناعي (AI assistant). **Brand-like tokens stay as-is:** "SLA" may appear in parentheses, and "CSAT" and "Gemini" stay in Latin script.

**Create file: `frontend/src/app/core/i18n/translate-fn.ts`**

```ts
import { EN } from './en';

export type Translate = (key: string, params?: Record<string, string | number>) => string;

export function lookup(dictionary: object, key: string, params?: Record<string, string | number>): string | null {
  const value = key.split('.').reduce<unknown>((node, part) => (node as Record<string, unknown> | undefined)?.[part], dictionary);
  if (typeof value !== 'string') return null;
  return params ? value.replace(/\{(\w+)\}/g, (m, name: string) => (name in params ? String(params[name]) : m)) : value;
}

/** Default for label helpers (D15): today's English, so existing specs stay byte-identical. */
export const englishT: Translate = (key, params) => lookup(EN, key, params) ?? key;
```

**Create file: `frontend/src/app/core/i18n/i18n.ts`**

```ts
export type Language = 'en' | 'ar';

@Injectable({ providedIn: 'root' })
export class I18n {
  private readonly dir = inject(Directionality);
  private readonly dateAdapter = inject(DateAdapter);
  private readonly document = inject(DOCUMENT);
  private readonly dictionary = signal<object>(EN);

  readonly language = signal<Language>('en');
  readonly direction = computed(() => (this.language() === 'ar' ? 'rtl' : 'ltr'));
  /** Intl locale: Arabic names with Latin digits (D4). */
  readonly locale = computed(() => (this.language() === 'ar' ? 'ar-EG-u-nu-latn' : 'en-US'));

  /** App initializer: stored language (default 'en', never inferred from IP/location). Awaited before first render. */
  init(): Promise<void> {
    return this.use(readPreference('crm.lang') === 'ar' ? 'ar' : 'en', false);
  }

  async use(language: Language, persist = true): Promise<void> {
    const dictionary = language === 'ar' ? (await import('./ar')).AR : EN;
    this.dictionary.set(dictionary);
    this.language.set(language);
    const dir = language === 'ar' ? 'rtl' : 'ltr';
    this.document.documentElement.lang = language;
    this.document.documentElement.dir = dir;
    this.dir.valueSignal.set(dir);   // CDK root Directionality: overlays (dialogs, menus, datepicker, snackbars) follow
    this.dir.change.emit(dir);
    this.dateAdapter.setLocale(this.locale());
    if (persist) writePreference('crm.lang', language);
  }

  readonly t: Translate = (key, params) =>
    lookup(this.dictionary(), key, params) ?? lookup(EN, key, params) ?? key; // missing ⇒ English, then the key (visible in review)
}
```

- `t` reads the `dictionary` signal, so templates and `computed`s re-evaluate on switch.
- If the Arabic chunk fails to load (offline), catch the error, keep English and show the snackbar `errors.languageLoad`.

**Create file: `frontend/src/app/core/i18n/translate.pipe.ts`** — `@Pipe({ name: 't', pure: false })`. `transform(key: string, params?)` returns `this.i18n.t(key, params)`. It is impure because the result depends on the language signal; the lookup is a small object walk.

**Create file: `frontend/src/app/core/i18n/localized-date.pipe.ts`** — `@Pipe({ name: 'crmDate', pure: false })`.
- `transform(value: string | Date | null | undefined, format: 'short' | 'medium' | 'long' | 'full' | 'date' | 'time' = 'medium', timeZone?: 'UTC')` returns `''` for null.
- Use a cached `Intl.DateTimeFormat(i18n.locale(), options)` per `locale|format|timeZone`, with options mirroring Angular's `DatePipe` presets: `medium` = `{ dateStyle: 'medium', timeStyle: 'short' }`, `short` = `{ dateStyle: 'short', timeStyle: 'short' }`, `full` = `{ dateStyle: 'full', timeStyle: 'short' }`, `date` = `{ dateStyle: 'medium' }`, `time` = `{ timeStyle: 'short' }`.
- **Replace every `| date` in templates** (58 occurrences, `grep -rn "| date" frontend/src/app --include=*.html`), keeping the same timezone argument. `| date: 'medium' : 'UTC'` becomes `| crmDate: 'medium' : 'UTC'`. Remove `DatePipe` from component `imports` when unused.
- **Never use `crmDate` for date-only `yyyy-MM-dd` values.** Those keep `formatUtcDay` (task 14).

**Create file: `frontend/src/app/core/i18n/material-intl.ts`**:
- `CrmPaginatorIntl extends MatPaginatorIntl`: labels from `paginator.*`; `getRangeLabel` uses `paginator.range` (`{start} – {end} of {length}`). An `effect` on `i18n.language()` sets the labels and calls `this.changes.next()`.
- `CrmDatepickerIntl extends MatDatepickerIntl`: `calendarLabel`, `openCalendarLabel`, `prevMonthLabel`, `nextMonthLabel`, `prevYearLabel`, `nextYearLabel`, `prevMultiYearLabel`, `nextMultiYearLabel`, `switchToMonthViewLabel`, `switchToMultiYearViewLabel`, `startDateLabel`, `endDateLabel`, `closeCalendarLabel` from `datepicker.*`, plus `changes.next()`.
- Provide both in `app.config.ts`: `{ provide: MatPaginatorIntl, useClass: CrmPaginatorIntl }` and `{ provide: MatDatepickerIntl, useClass: CrmDatepickerIntl }`.

**Create file: `frontend/src/app/core/i18n/server-messages.ts`** — `SERVER_MESSAGE_KEYS: Record<string, string>`, mapping exact English server text to a translation key (D13).

**File: `frontend/src/app/core/api/describe-http-error.ts`** (lines 5–14) — new signature `describeHttpError(error, fallback, t: Translate = englishT)`:
- status 0 ⇒ `t('errors.unreachable')`;
- status 429 ⇒ `t('errors.tooMany')`;
- `problem.detail` / `title` mapped through `SERVER_MESSAGE_KEYS` ⇒ `t(key)`, otherwise the server text verbatim;
- otherwise `fallback`.

Callers pass `i18n.t` and a **translated** fallback (`this.i18n.t('tickets.errors.load')`). `describeSaveError` (`sla-errors.ts` lines 6–10) gets the same optional `t` and maps each message through `SERVER_MESSAGE_KEYS`.

**File: `frontend/src/app/app.config.ts`** — add to `providers` (keep the existing ones):

```ts
provideNativeDateAdapter(),
{ provide: MatPaginatorIntl, useClass: CrmPaginatorIntl },
{ provide: MatDatepickerIntl, useClass: CrmDatepickerIntl },
provideAppInitializer(() => inject(I18n).init()),
provideAppInitializer(() => inject(Theme).init()),
provideAppInitializer(() => inject(Branding).load()),   // never rejects (task 11)
provideAppInitializer(() => inject(AuthService).restoreSession()), // existing, keep last
```

### 10 — Theme, accent presets, fonts, global tokens

**File: `frontend/src/styles.scss`**

- Keep the `html { color-scheme: light dark; @include mat.theme(...) }` block (lines 3–13) as the default (violet).
- Add the light/dark override. Tokens are `light-dark()`, so `color-scheme` alone switches the whole theme:

```scss
html[data-theme='light'] { color-scheme: light; }
html[data-theme='dark'] { color-scheme: dark; }
```

- Add the accent presets (D10). Each emits color tokens only:

```scss
$crm-accents: (
  blue: mat.$blue-palette,
  azure: mat.$azure-palette,
  green: mat.$green-palette,
  orange: mat.$orange-palette,
  rose: mat.$rose-palette,
);
@each $name, $palette in $crm-accents {
  html[data-accent='#{$name}'] {
    @include mat.theme((color: (primary: $palette, tertiary: mat.$cyan-palette)));
  }
}
```

- Typography: `font-family: var(--mat-sys-body-large-font)` stays. Add `html[lang='ar'] body { font-family: 'Noto Sans Arabic', var(--mat-sys-body-large-font); }`, and set `--mat-sys-*-font` under `html[lang='ar']` the same way, so Material components switch font too.
- Extend `:root` tokens (lines 15–28):
  - `--crm-nav-rail-inline-size: 72px`;
  - `--crm-topbar-block-size: 64px`;
  - `--crm-radius-sm: 8px`;
  - `--crm-elevation-1: 0 1px 2px rgb(0 0 0 / 8%), 0 1px 3px rgb(0 0 0 / 6%)`;
  - `--crm-warning` / `--crm-warning-container` via `light-dark()`.
- Add global helpers:
  - `.ltr-value { direction: ltr; unicode-bidi: isolate; }` for IDs, emails, phones, URLs and code;
  - `[dir='rtl'] .dir-icon { transform: scaleX(-1); }`;
  - `:focus-visible { outline: 2px solid var(--mat-sys-primary); outline-offset: 2px; }`;
  - `@media (prefers-reduced-motion: reduce) { *, *::before, *::after { animation-duration: 0.01ms !important; transition-duration: 0.01ms !important; } }`.

**File: `frontend/src/index.html`** — the line 10 font link becomes `family=Inter:wght@400;500;600;700&family=Noto+Sans+Arabic:wght@400;500;600;700&display=swap`. Line 2 stays `lang="en"` (the initializer sets it).

**Create file: `frontend/src/app/core/preferences/theme.ts`**

```ts
export type ThemeMode = 'light' | 'dark';

@Injectable({ providedIn: 'root' })
export class Theme {
  private readonly root = inject(DOCUMENT).documentElement;
  /** null = follow the OS (prefers-color-scheme) until the user chooses. */
  readonly mode = signal<ThemeMode | null>(null);

  init(): void {
    const stored = readPreference('crm.theme');
    this.apply(stored === 'light' || stored === 'dark' ? stored : null, false);
  }

  /** The effective mode for the toggle icon. */
  readonly effective = computed<ThemeMode>(() => this.mode() ?? (matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light'));

  toggle(): void { this.apply(this.effective() === 'dark' ? 'light' : 'dark', true); }

  private apply(mode: ThemeMode | null, persist: boolean): void {
    this.mode.set(mode);
    if (mode) this.root.dataset['theme'] = mode; else delete this.root.dataset['theme'];
    if (persist) writePreference('crm.theme', mode);
  }
}
```

Also expose a `version` signal that increments on theme changes; charts read it to re-color (task 14).

### 11 — Branding client

**Create file: `frontend/src/app/core/branding/branding-api.ts`**

```ts
/** Matches Crm.Api PublicBrandingResponse (anonymous). */
export interface PublicBranding { displayName: string | null; accentColor: AccentPreset | null; logoVersion: string | null; }
export const ACCENT_PRESETS = ['violet', 'blue', 'azure', 'green', 'orange', 'rose'] as const;
export type AccentPreset = (typeof ACCENT_PRESETS)[number];
```

`BrandingApi.get()` calls `GET apiUrl.versioned('branding')`. `logoUrl(version)` returns `apiUrl.versioned('branding/logo') + '?v=' + encodeURIComponent(version)`.

**Create file: `frontend/src/app/core/branding/branding.ts`**:
- `Branding` exposes the signals `displayName` (null = use `app.name`), `accent`, `logoUrl` and `logoFailed`.
- `load(): Promise<void>` runs `get()` with `timeout(5000)`. **On any error, keep the defaults and resolve.** The sign-in page must never be blocked by branding.
- It applies `document.documentElement.dataset['accent'] = accent ?? 'violet'`; `violet` uses the base theme, so no attribute rule is needed.
- `reload()` is called by the Organization Settings page after a successful save or logo change.
- `orgName = computed(() => displayName() ?? i18n.t('app.name'))`.
- Also set `document.title` via a `TitleStrategy` (task 12).

### 12 — Shell, navigation, auth layout, breadcrumbs, titles

**File: `frontend/src/app/core/layout/navigation.ts`** — replace `label` with `labelKey`, add `group`. **Keep every `path`, `exact` and `roles` value from lines 14–31 exactly.**

```ts
export type NavGroup = 'portal' | 'workspace' | 'knowledge' | 'management' | 'account';
export interface NavItem {
  readonly labelKey: string;   // en.ts nav.*
  readonly icon: string;
  readonly path: string;
  readonly exact: boolean;
  readonly group: NavGroup;
  readonly roles?: readonly UserRole[];
}
export const NAV_GROUP_ORDER: readonly NavGroup[] = ['portal', 'workspace', 'knowledge', 'management', 'account'];
```

Group assignment: see the Role visibility table in Product rules.

**Rewrite: `frontend/src/app/core/layout/shell/shell.ts|html|scss`** (Ng-Matero pattern, D1):
- **Sidebar** (`mat-sidenav`, `position="start"`, so it is on the right side automatically in RTL):
  - brand header: logo `<img [src]="logoUrl()" alt="" (error)="branding.logoFailed.set(true)">` when present and not failed, otherwise the `support_agent` icon; then the org name;
  - `@for` over `NAV_GROUP_ORDER`, rendering the group heading (`nav.group.*`, hidden in rail mode) and the role-filtered items (the same filter as `shell.ts` line 38);
  - the active item uses `routerLinkActive` + `[activated]` + `ariaCurrentWhenActive="page"`;
  - in rail mode the label is visually hidden but kept for screen readers, with `matTooltip` showing the label.
- **Modes:**
  - Handset (`Breakpoints.Handset`, as today): `mode="over"`, closed; it closes after navigation.
  - Desktop: `mode="side"`, opened. The collapse button toggles rail mode (`crm.navCollapsed`), and the sidenav width animates between `--crm-nav-inline-size` and `--crm-nav-rail-inline-size`. Respect reduced motion.
- **Top bar** (`mat-toolbar`, sticky, as `shell.scss` lines 21–28):
  - menu/collapse button (`aria-label` translated, `aria-expanded`);
  - `<crm-breadcrumbs />`;
  - spacer;
  - `<crm-topbar-controls />` (language and theme);
  - `NotificationBell` for staff (unchanged, `shell.html` lines 37–39);
  - a user menu button (avatar initials + name hidden < 600 px) opening `mat-menu` with Profile (`/profile`) and Sign out (`auth.logout()`, as `shell.ts` lines 41–43).
- **Content:** `<main id="main" tabindex="-1">` with a "Skip to content" link as the first focusable element. Keep `max-inline-size` and padding (`shell.scss` lines 43–57).

**Create file: `frontend/src/app/core/layout/topbar-controls.ts`** (`crm-topbar-controls`):
- language `mat-menu`, button `aria-label` = `shell.language`, items "English" and "العربية" (always in their own script, `lang` attribute on each item) ⇒ `i18n.use(...)`;
- theme icon button (`dark_mode` / `light_mode`), `aria-label` = `shell.themeToDark` / `shell.themeToLight`, `aria-pressed`.

**Create file: `frontend/src/app/core/layout/breadcrumbs.ts`** (`crm-breadcrumbs`):
- On `NavigationEnd`, walk `router.routerState.snapshot.root` and collect `data['crumb']` keys with their accumulated URLs;
- render `nav aria-label=shell.breadcrumbs` > `ol` with links, the last item `aria-current="page"`, separator `chevron_right.dir-icon`;
- hidden below 600 px (the page header shows the title).

**Files: `app.routes.ts` and each `features/*/*.routes.ts`** — add `data: { crumb: '<nav key>' }` to each lazy parent and child, and replace each `title: '… | Support CRM'` with a translation key `title: 'titles.tickets'`.

**Create file: `frontend/src/app/core/layout/crm-title-strategy.ts`** — `CrmTitleStrategy extends TitleStrategy`. `updateTitle` sets `document.title = t(key) + ' | ' + branding.orgName()`, and re-runs on language change (an `effect`). Provide it in `app.config.ts` with `{ provide: TitleStrategy, useClass: CrmTitleStrategy }`.

**Create file: `frontend/src/app/core/layout/auth-layout/auth-layout.ts`** (`crm-auth-layout`, content projection):
- full-height centered card; brand (logo or icon + `orgName`); `<crm-topbar-controls />` in the card's top end corner;
- used by `sign-in-page.html` and `sign-up-page.html`, replacing their `.auth-brand` block (line 2).
- No route change.

### 13 — Shared UI building blocks

All are standalone, `OnPush`, logical CSS only, and every text comes through `t`.

- **`shared/ui/page-header/page-header.ts`** (extend lines 1–25): add an optional `icon` input and an `<ng-content select="[actions]">` slot right-aligned at the inline end, which wraps below at 600 px. `title`/`subtitle` stay strings; callers pass translated text.
- **Create `shared/ui/empty-state.ts`** (`crm-empty-state`): inputs `icon`, `title`, `message`; optional projected action. `role="status"`.
- **Create `shared/ui/error-state.ts`** (`crm-error-state`): inputs `message`, output `retry`. Renders `role="alert"` text and a "Retry" stroked button (`common.retry`). Replaces the repeated `<p class="error" role="alert">…</p><button>Retry</button>` pairs (for example `organization-settings-page.html` lines 8–11 and `agent-dashboard-page.html` lines 22–25).
- **Create `shared/ui/confirm-dialog.ts`**: `ConfirmDialog` component (`mat-dialog`; title, message, confirm/cancel labels, `destructive` flag ⇒ error color) plus a `Confirm` service with `ask(options): Observable<boolean>`. The dialog direction comes from the root `Directionality` (task 9). Replace `window.confirm` at `admin-user-detail-page.ts` line 69 and `ticket-collaborators.ts` line 52 (`grep -rn "confirm(" frontend/src/app` ⇒ only these two).
- **Create `shared/ui/kpi-card.ts`** (`crm-kpi-card`): inputs `label`, `value` (string), `scope` (optional "Live"/"Period" chip), `icon`. Replaces the `summary-card` markup on Reports, Agent Dashboard and Home. Same numbers, larger type.
- **`shared/ui/status-badge/status-badge.ts`**: keep the API; add a `warning` tone (`--crm-warning*`). Reduce padding at 400 px.
- **Tables:** keep `mat-table` where used (`ticket-list-page.html` line 86, `customer-list-page.html` line 28, `agent-list-page.html` line 22). Wrap each in `.table-scroll` (`overflow-x: auto` **inside the card**, not the page). Mark the primary column `sticky` and set `text-align: start`. Plain `<table>`s (`agent-report-page.html` line 87, `permissions-page.html` line 6) get the same wrapper and header style.

**Create file: `frontend/src/app/shared/date/date-only.ts`** (D5):

```ts
const DATE_ONLY = /^(\d{4})-(\d{2})-(\d{2})$/;
const pad = (n: number) => String(n).padStart(2, '0');

/** Calendar day the user picked (local-midnight Date from the native adapter) ⇒ 'yyyy-MM-dd'. Never via toISOString (Cairo would shift). */
export function toDateOnly(date: Date | null): string | null {
  return date ? `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}` : null;
}

/** 'yyyy-MM-dd' ⇒ local-midnight Date for the picker; invalid ⇒ null. */
export function fromDateOnly(value: string | null | undefined): Date | null {
  const m = value ? DATE_ONLY.exec(value) : null;
  if (!m) return null;
  const d = new Date(Number(m[1]), Number(m[2]) - 1, Number(m[3]));
  return d.getMonth() === Number(m[2]) - 1 ? d : null; // rejects 2030-02-31
}

/** Picked day + picked time (local wall clock) ⇒ ISO UTC; same semantics as task-models fromLocalInput. Either missing ⇒ null. */
export function combineLocalDateTime(day: Date | null, time: Date | null): string | null {
  if (!day || !time) return null;
  return new Date(day.getFullYear(), day.getMonth(), day.getDate(), time.getHours(), time.getMinutes()).toISOString();
}

/** ISO UTC ⇒ { day, time } local Dates for the pickers (inverse of combineLocalDateTime); null ⇒ nulls. */
export function splitIsoToLocal(iso: string | null): { day: Date | null; time: Date | null } {
  if (!iso) return { day: null, time: null };
  const d = new Date(iso);
  return { day: new Date(d.getFullYear(), d.getMonth(), d.getDate()), time: d };
}

/** 'HH:mm' wall-clock text (SLA calendar) ⇔ picker Date on an arbitrary day. */
export function toTimeOfDay(time: Date | null): string | null { return time ? `${pad(time.getHours())}:${pad(time.getMinutes())}` : null; }
export function fromTimeOfDay(value: string | null | undefined): Date | null { /* parse HH:mm ⇒ new Date(2000, 0, 1, h, m); invalid ⇒ null */ }
```

`fromLocalInput` and `toLocalInput` in `task-models.ts` (lines 80–92) stay unchanged: they are still exported and used by nothing after task 16. Leave them in place to avoid touching unrelated code; mark them `/** @deprecated PLAT-001: use shared/date/date-only.ts */`.

### 14 — Charts and report date range

**Create file: `frontend/src/app/shared/charts/crm-chart.ts`** (`crm-chart`, D2):

```ts
export type CrmChartType = 'bar' | 'horizontalBar' | 'line' | 'doughnut';
export interface CrmChartSeries { label: string; data: (number | null)[]; }

@Component({
  selector: 'crm-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `<div class="frame" [style.block-size.px]="height()"><canvas #canvas role="img" [attr.aria-label]="ariaLabel()"></canvas></div>`,
  styles: `:host { display: block; } .frame { position: relative; inline-size: 100%; }`,
})
export class CrmChart {
  readonly type = input.required<CrmChartType>();
  readonly labels = input.required<string[]>();
  readonly series = input.required<CrmChartSeries[]>();
  readonly ariaLabel = input.required<string>();      // short summary; the numbers are in the adjacent data table
  readonly percent = input(false);                     // y axis 0–100 with '%' ticks; null ⇒ gap, tooltip 'N/A'
  readonly height = input(260);
  /* afterNextRender: const { Chart, ...parts } = await import('chart.js'); Chart.register(BarController, LineController,
     DoughnutController, BarElement, LineElement, PointElement, ArcElement, CategoryScale, LinearScale, Tooltip, Legend, Filler)
     once (module-level flag). Build the config from inputs; colors from getComputedStyle(document.documentElement):
     --mat-sys-primary, --mat-sys-tertiary, --mat-sys-error, --crm-success, --mat-sys-outline-variant (grid),
     --mat-sys-on-surface-variant (ticks). An effect on (inputs, i18n.language(), theme.version(), branding.accent())
     updates data/colors/rtl and calls chart.update(); DestroyRef ⇒ chart.destroy(). */
}
```

Configuration rules:
- `animation: false` when `matchMedia('(prefers-reduced-motion: reduce)')` matches;
- `responsive: true`, `maintainAspectRatio: false`;
- `plugins.legend.rtl` and `plugins.tooltip.rtl` = `i18n.direction() === 'rtl'`, with `textDirection` to match;
- the font family follows the current `body` font, so Arabic labels render;
- `horizontalBar` ⇒ `indexAxis: 'y'`; in RTL also set `scales.x.reverse = true`, so bars grow from the inline start;
- line and vertical bar time axes are **not** reversed (Recorded decisions, Charts);
- `percent` ⇒ `scales.y.min = 0`, `max = 100`, ticks `v + '%'`, `spanGaps: false`, and a tooltip `label` callback that prints `t('reports.na')` for `null`;
- integer counts ⇒ `ticks.precision = 0`;
- **No click handlers that navigate or write.** Charts are read-only.

**Create file: `frontend/src/app/shared/ui/chart-card.ts`** (`crm-chart-card`):
- inputs `title`, `summary` (for example "Total: 42"), `emptyText`, `empty` (boolean), `error` (string | null) and `rows: { label: string; value: string }[]`;
- content projection holds the `<crm-chart>`;
- below it, a **visible** compact data table (`<table>` with `th scope="col"`) when `rows.length <= 8`, or `<details><summary>{{ 'charts.showData' | t }}</summary>…</details>` when there are more (trend buckets);
- `empty` ⇒ `crm-empty-state` instead of the chart (no zero chart); `error` ⇒ `crm-error-state`.

**Delete file: `frontend/src/app/features/reports/report-bars.ts`** after the last usage is replaced (`grep -rn "crm-report-bars\|ReportBars\|ReportBar\b"` ⇒ nothing). Keep `barPercent` in `report-format.ts`: `report-format.spec.ts` asserts it (D15).

**Chart mapping (exact API fields, no new metrics):**

| Screen (file) | Chart | Labels | Series (field) | Type | Empty when |
|---|---|---|---|---|---|
| Ticket report (`ticket-report-page`) | Current status | `t('enums.ticketStatus.'+value)` | `byStatus[].count` | doughnut | `createdTickets === 0` |
| | Category | `enums.ticketCategory.*` | `byCategory[].count` | horizontalBar | same |
| | Priority | `enums.ticketPriority.*` | `byPriority[].count` | horizontalBar | same |
| | Creation trend | `formatBucketLabel(start,end,locale)` | `trend[].count` | bar (vertical) | `trend.length === 0` |
| SLA report (`sla-report-page`) | First response vs Resolution compliance trend | bucket labels | `responseCompliancePercent`, `resolutionCompliancePercent` (**null kept**) | line, `percent` | every value null ⇒ empty "N/A — nothing decided yet." |
| | Outcome split (per target) | Met / Breached / Pending / N/A | `firstResponse.{met,breached,pending,notApplicable}` and the same for `resolution` | two doughnuts | `cohortTickets === 0` |
| Agent report (`agent-report-page`) | Active workload (live) | `agents[].name` (user text, `<bdi>` in the table) | `agents[].open`, `agents[].inProgress` (stacked) | horizontalBar stacked | every `activeAssigned === 0` |
| CSAT report (`csat-report-page`) | Rating distribution | `${value} ★` | `distribution[].count` | bar | `feedbackCount === 0` |
| Management dashboard | Tickets created | buckets | `tickets.data.trend[].count` | bar | as above |
| | Active per Agent (live) | names | `agents.data.agents[].activeAssigned` | horizontalBar | as above |
| | Rating distribution | `n ★` | `csat.data.distribution[].count` | bar | as above |

- The KPI cards stay and keep their current values and "Live" / "Period" scope labels (`reports-page.scss` line 6 `.scope`).
- Per-section errors on the dashboard stay per card (`management-dashboard-page.html` lines 162–188, the `@if (o.tickets.data) … @else error` pattern).
- The SLA trend hint text (`sla-report-page.html` lines 92–95) stays, translated.

**File: `frontend/src/app/features/reports/report-format.ts`** — add an optional `locale = 'en-US'` parameter to `formatUtcDay` and `formatBucketLabel`, building a cached `Intl.DateTimeFormat(locale, { …, timeZone: 'UTC' })`. Pages pass `i18n.locale()`. The existing spec, which calls without `locale`, still gets 'Jan 1, 2030'. **The UTC anchoring is unchanged.** `formatPercent` and `formatAverage` gain an optional `t` (D15); 'N/A' comes from `reports.na`.

**Rewrite the template of `frontend/src/app/features/reports/report-period-filter.ts`** (lines 15–46). The logic in lines 54–98 stays the same except for the date conversion:
- presets: keep the `mat-button-toggle-group` (labels `reports.preset.*`), which scrolls horizontally inside its own container at 400 px;
- custom: replace the two `type="date"` inputs (lines 32–39) with

```html
<mat-form-field appearance="outline">
  <mat-label>{{ 'reports.customRange' | t }}</mat-label>
  <mat-date-range-input [formGroup]="form" [rangePicker]="picker" [max]="today">
    <input matStartDate formControlName="from" [placeholder]="'reports.from' | t" />
    <input matEndDate formControlName="to" [placeholder]="'reports.to' | t" />
  </mat-date-range-input>
  <mat-hint>{{ 'reports.utcDaysHint' | t }}</mat-hint>
  <mat-datepicker-toggle matIconSuffix [for]="picker" />
  <mat-date-range-picker #picker />
</mat-form-field>
```

- The `form` controls become `Date | null` (`group({ from: [null as Date | null], to: [null as Date | null] })`). The sync `effect` (lines 67–75) uses `fromDateOnly(v.from)` and `fromDateOnly(v.to)`. `applyCustom` (lines 86–98) converts with `toDateOnly` and then runs the **same** checks: both required, `from <= to` lexical, with the same messages translated. The emitted `ReportPeriodParams` therefore keeps the same `yyyy-MM-dd` strings, so the URL and API are unchanged.
- `today` = local today, matching today's behaviour where the API validates the range. **Do not add new min/max business rules**: the server remains authoritative and its 400 is shown through `describeSaveError`.
- Typing still works: `mat-date-range-input` accepts text, which the native adapter parses in the current locale.

**Create file: `frontend/src/app/features/reports/reports-nav.ts`** (`crm-reports-nav`): `nav mat-tab-nav-bar` with links Overview `/reports`, Tickets, SLA, Agents, CSAT; `queryParamsHandling="preserve"` (as `management-dashboard-page.html` lines 24–29, which it replaces). Add it under the page header of all five report pages.

### 15 — Translate every screen (PL3 coverage)

For **each** file below:
1. replace every system-owned literal in the template with `{{ 'ns.key' | t }}`, or `[attr]="'ns.key' | t"` for attributes (`aria-label`, `placeholder`, `matTooltip`, `title`);
2. replace TS literals (snackbar texts, fallbacks, `window.confirm` texts, page titles passed to `crm-page-header`) with `this.i18n.t(...)`;
3. pass `i18n.t` to label helpers;
4. wrap IDs, emails, phones and URLs in `.ltr-value` (or `<bdi>` inside sentences).

**Do not translate** values that come from the API: subjects, descriptions, names, notes, comments, KB titles and bodies, AI text, feedback comments, organization settings values, audit `targetLabel` and old/new values. Enum **values** are never sent translated: `mat-option [value]` stays the API value, and only the displayed text is translated.

| Namespace | Files (all under `frontend/src/app/`) |
|---|---|
| `shell`, `nav`, `titles` | `core/layout/**`, all `*.routes.ts`, `core/notifications/notification-bell.ts` |
| `auth` | `features/auth/sign-in-page.*`, `sign-up-page.*` |
| `home`, `profile` | `features/home/*`, `features/profile/*` |
| `dashboard` | `features/agent-dashboard/*` |
| `tickets` | `features/tickets/*` (`ticket-models.ts` `statusLabel` line 132 ⇒ optional `t`; `ticket-workflow.ts` transition labels ⇒ optional `t`; `ticket-sla.ts`; `ticket-activity-list.*`; `ticket-sla-card.*`; `customer-picker.ts`) |
| `customers` | `features/customers/*` (`customer-records-models.ts` line 8) |
| `agents` | `features/agents/*` |
| `tasks` | `features/tasks/*` (`ticket-picker.ts`) |
| `notifications` | `features/notifications/*`, `core/notifications/*` |
| `quickReplies` | `features/quick-replies/*` (`copy-quick-reply.ts` snackbar) |
| `collaboration` | `features/collaboration/*` |
| `sla` | `features/sla/*` (`sla-models.ts` lines 155 and 179, `automation-rules.ts` lines 32–43) |
| `kb` | `features/knowledge-base/*` (`knowledge-base-models.ts` lines 10–19). **Not** `markdown.ts` output |
| `ai` | `features/ai/*` (`ai-errors.ts`). AI responses untouched |
| `portal` | `features/portal/*` (`portal-timeline.ts` ⇒ optional `t`) |
| `reports` | `features/reports/*` (`reports-models.ts` line 7 `PRESET_LABELS` ⇒ keys) |
| `admin`, `branding` | `features/admin/*` (`admin-format.ts` lines 3–24 ⇒ optional `t`; `permission-matrix.ts` row/scope texts ⇒ keys) |
| `errors`, `validation`, `common` | `core/api/*`, `features/sla/sla-errors.ts`, every `mat-error` |

**Completeness check (run before task 20):**

```powershell
# in frontend/ — every remaining hard-coded word between tags or in common attributes is a candidate miss
git grep -nE ">[^<{}@]*[A-Za-z]{3,}[^<{}]*<" -- "src/app/**/*.html"
git grep -nE "(aria-label|placeholder|matTooltip|title|label)=\"[A-Za-z]" -- "src/app/**/*.html" "src/app/**/*.ts"
git grep -nE "snackBar\.open\('|confirm\(`|'Could not|\"Could not" -- "src/app/**/*.ts"
```

Every hit must be a non-translatable exception (for example `CSAT`, `★`, an icon ligature such as `<mat-icon>refresh</mat-icon>`, `English` / `العربية` in the language menu). Record the residual list in the implementation report.

### 16 — Dates on every screen (date-picker migration matrix)

All pickers use `<mat-datepicker-toggle matIconSuffix>`, so the calendar affordance is visible, the input stays typeable, and `mat-error` shows invalid dates. Add `MatDatepickerModule` and `MatTimepickerModule` to component `imports`.

| # | Location | Today | New control | Form value | Sent to API (unchanged) |
|---|---|---|---|---|---|
| 1 | `report-period-filter.ts` lines 34, 38 | 2× `type="date"` | `mat-date-range-input` (task 14) | `Date \| null` | `toDateOnly` ⇒ `yyyy-MM-dd` query `from`/`to` |
| 2 | `audit-logs-page.html` lines 17, 22 | 2× `type="date"` | `mat-date-range-input` (`from`/`to`) | `Date \| null` | `toDateOnly` in the filter ⇒ `params` (find the existing `from`/`to` param build in `audit-logs-page.ts` and `admin-api.ts`) |
| 3 | `business-calendar-form.html` line 56 | `type="date"` holiday | `matDatepicker` | `Date \| null` | `toDateOnly` ⇒ `holidays[].date` `yyyy-MM-dd` |
| 4 | `business-calendar-form.html` lines 44, 48 | `type="time"` | `matTimepicker` (`interval="15min"`) | `Date \| null` | `toTimeOfDay` ⇒ `HH:mm` (`opensAt`/`closesAt`); load via `fromTimeOfDay` |
| 5 | `business-calendar-form.html` line 101 | `datetime-local` preview start | `matDatepicker` + `matTimepicker` | two controls | `combineLocalDateTime` ⇒ ISO (replaces `business-calendar-form.ts` line 172); both empty ⇒ null (start now, as today) |
| 6 | `task-edit-page.html` lines 21 and 81 (`due`) | `datetime-local` | date + time pair | `dueDay`, `dueTime` | `combineLocalDateTime` ⇒ `dueAtUtc`; load `splitIsoToLocal(task.dueAtUtc)` (replaces `toLocalInput`, `task-edit-page.ts` line 269) |
| 7 | `task-edit-page.html` lines 39 and 110 (`remindAt`) | `datetime-local` | date + time pair | `remindDay`, `remindTime` | same ⇒ `remindAtUtc` (line 274) |
| 8 | `customer-interactions.html` line 14 | `datetime-local` | date + time pair, default now | `occurredDay`, `occurredTime` | `combineLocalDateTime` ⇒ `occurredAtUtc`; the future-tolerance check (`customer-interactions.ts` line 111) unchanged |

Rules:
- In a pair, "day set + time empty" is a validation error (`validation.timeRequired`); both empty means `null` wherever the old field was optional.
- **Never** call `toISOString()` on a date-only value.
- Display of server timestamps uses `crmDate`. Display of date-only values (holiday list `business-calendar-form.html` line 69, report periods) uses `formatUtcDay(day, i18n.locale())`.
- Holiday list sorting stays on the string (`sortedHolidays`).

### 17 — Restructure oversized screens (tabs / child routes, D12)

Shared tab ⇄ URL helper. **Create file: `frontend/src/app/shared/ui/tab-query.ts`**

```ts
/** Binds a mat-tab-group selectedIndex to ?tab=<id> (replaceUrl, other params kept). Unknown/absent ⇒ first tab. */
export function tabQuery(ids: readonly string[]) {
  const route = inject(ActivatedRoute);
  const router = inject(Router);
  const param = toSignal(route.queryParamMap.pipe(map(p => p.get('tab'))), { initialValue: null });
  const index = computed(() => Math.max(0, ids.indexOf(param() ?? '')));
  const select = (i: number) =>
    void router.navigate([], { relativeTo: route, queryParams: { tab: i === 0 ? null : ids[i] }, queryParamsHandling: 'merge', replaceUrl: true });
  return { index, select };
}
```

- **Ticket detail** (`ticket-detail-page.html` lines 1–240, `ticket-detail-page.ts`):
  - **Header:** `crm-page-header` with title = ticket subject (user text), subtitle = `<span class="ltr-value">{{ id }}</span>`, status/priority/escalated badges, and the actions slot holding "Back to tickets" (`arrow_back.dir-icon`).
  - **Tabs:** ids `['overview','details','activity','collaboration','knowledge','ai']`, `mat-tab-group [selectedIndex]="tabs.index()" (selectedIndexChange)="tabs.select($event)" [preserveContent]="true" animationDuration="0ms"`.
    - `overview`: Summary (lines 11–47), SLA (49–61), Workflow (63–105), Assignment (107–144) in a 2-column grid ≥ 960 px;
    - `details`: Edit details (147–196). The `form` lives in the class, so values survive switches. If `form.dirty`, the tab label shows a "•" plus `tickets.unsaved` sr-text;
    - `activity`: 198–205;
    - `collaboration`: discussion 207–212 + collaborators 214–219;
    - `knowledge`: KB 226–231 + quick replies 221–224;
    - `ai`: 233–238.
  - `overview` content is eager; the other tabs use `<ng-template matTabContent>`, so they load on first open.
  - **No component logic changes.** All handlers (`transition`, `escalate`, `saveAssignee`, `save`, `markResponded`, `onAccessLost`, `onTicketUpdated`) stay as they are.
- **Customer detail** (`customer-detail-page.html`): ids `['profile','tickets','notes','interactions','attachments']`. `profile` = Summary + Edit (the form stays in the class); the others wrap the existing child components (lines 102–118) in `matTabContent`.
- **Agent Dashboard** (`agent-dashboard-page.html`):
  - ids `['overview','tasks']`: `overview` = Workload KPI cards + Needs attention; `tasks` = My tasks;
  - the 7 toolbar links (lines 3–16) become an action row: primary "Create ticket" + a `mat-chip-set` of links;
  - Refresh stays; the `generatedAtUtc` hint uses `crmDate`.
- **SLA** (`sla-admin-page.html` lines 6–19): add `tabQuery(['policy','calendar','rules','overview'])` bound to the existing group. The `matTabContent` children are unchanged.
- **Reports:** `crm-reports-nav` (task 14).
- **Administration** (`admin-section-nav.ts`): replace the button nav with `nav mat-tab-nav-bar [tabPanel]` + `mat-tab-link` items (same 4 routes, `routerLinkActive`), and translate them.
- **Knowledge Base:** create `features/knowledge-base/kb-section-nav.ts` with a `mat-tab-nav-bar`: Articles `/knowledge-base` (exact) and Categories `/knowledge-base/categories`. **Categories is rendered only when `auth.role() === 'SuperAdmin'`** (the route keeps `roleGuard('SuperAdmin')`, `knowledge-base.routes.ts` line 10). Use it on `knowledge-base-page.html` and `knowledge-categories-page.html`.
- **Customer Portal:**
  - `portal-home-page.html`: a welcome header with `orgName`, quick-action cards (New request, My requests, Help Center), and Recent requests.
  - Detail page: Request card + History timeline + Feedback card in one column on handset and two columns ≥ 960 px.
  - No new routes.

### 18 — Responsive and accessibility pass (400 px)

For every route in the inventory table, check at 400 px CSS width in both directions:
- no horizontal **page** scroll (tables scroll inside `.table-scroll` only);
- touch targets ≥ 40 px (Material default density 0 stays, `styles.scss` line 11);
- forms in one column below 600 px (`.form-grid` / `.grid` in the feature SCSS: `grid-template-columns: 1fr`);
- dialogs at `maxWidth: '95vw'`;
- the toolbar user name hidden < 600 px (as `shell.scss` lines 50–53).

Visible focus everywhere (`:focus-visible`, task 10). Each page has exactly one `h1` (the page header). Component styles must stay ≤ 4 kB (`anyComponentStyle` budget); move shared rules to `styles.scss` or `shared/ui/productivity-page.scss` rather than raising the budget.

### 19 — Branding UI in Organization Settings (PL4)

**File: `features/admin/admin-models.ts`** (lines 44–60):
- `OrganizationSettings` gains `accentColor: AccentPreset | null; hasLogo: boolean; logoContentType: string | null; logoSizeBytes: number | null; logoUpdatedAtUtc: string | null;`;
- `UpdateOrganizationSettingsRequest` gains `accentColor: AccentPreset | null`.

**File: `features/admin/admin-api.ts`** (after line 72):

```ts
setLogo(version: string, file: File): Observable<OrganizationSettings> {
  const body = new FormData();
  body.append('file', file, file.name);
  return this.http.put<OrganizationSettings>(this.apiUrl.versioned('admin/organization-settings/logo'), body, {
    headers: { 'If-Match': `"${version}"` },
  });
}

removeLogo(version: string): Observable<OrganizationSettings> {
  return this.http.delete<OrganizationSettings>(this.apiUrl.versioned('admin/organization-settings/logo'), {
    headers: { 'If-Match': `"${version}"` },
  });
}
```

**File: `features/admin/organization-settings-page.ts|html`**:
- **Form:** add an `accentColor` control (`AccentPreset`, default `'violet'` when null). Send `accentColor: v.accentColor === 'violet' ? null : v.accentColor`. Keep the existing save, 409/400 handling and snackbar (lines 72–114). After a successful save call `branding.reload()`.
- **Template:** a new card "Branding" (`branding.*`) below the existing fields:
  - **Accent:** a `mat-radio-group` of 6 swatches (`role="radiogroup"`). Each option shows a 24 px circle using the preset's primary color plus its translated name, so it is not color-only. A live preview (`<div class="accent-preview" [attr.data-accent]="form.controls.accentColor.value">` holding a flat button and a chip) shows the chosen accent before saving. To make that work, extend the task 10 `@each` selector to `html[data-accent='#{$name}'], .accent-preview[data-accent='#{$name}']`, and add `.accent-preview[data-accent='violet']` with the base `mat.theme((color: (primary: mat.$violet-palette, tertiary: mat.$cyan-palette)))`.
  - **Logo:**
    - current preview `<img [src]="branding.logoUrl()" alt="{{ 'branding.logoAlt' | t }}" (error)="…">`, max 160×64, `object-fit: contain`;
    - a "Choose image" button opening a hidden `<input type="file" accept="image/png,image/jpeg,image/webp">`;
    - a client check (type in the 3 MIME types, size ≤ 512 KB) gives an immediate translated message, while **the server stays authoritative**;
    - "Upload" ⇒ `api.setLogo(current.version, file)`;
    - "Remove" ⇒ `Confirm.ask(...)`, then `api.removeLogo(current.version)`.
  - Both actions share the page's `saving` flag, disable Save during the request, handle 409 with the existing conflict UI (`conflict` signal + Reload), show 400 through `describeSaveError` (key `file`), and on success call `apply(saved)`, `branding.reload()` and a snackbar.
- **Important:** a logo change bumps the settings version. Apply the returned settings (`apply`, lines 116–125) **without resetting unsaved text edits**: if `form.dirty`, update only `settings` (and therefore `version`) and leave the form values.
- Replace the hint at line 61 ("Informational only…") with `admin.settings.brandingHint`: "The display name, accent and logo appear on the sign-in page, staff screens and the Customer Portal. Support details are informational."

**File: `features/admin/admin-format.ts`** — add to `FIELD_LABELS` (lines 10–17): `accentColor: 'Accent color'`, `logo: 'Logo'` (and their keys).

### 20 — Docs

- **Create `docs/platform-ui-and-localization.md`:**
  - shell and navigation groups;
  - design tokens;
  - the i18n mechanism (EN source, typed AR, `t` pipe, `crmDate`, optional `t` on helpers, server message map, how to add a key);
  - RTL rules (`.ltr-value`, `.dir-icon`, logical CSS, `Directionality`);
  - date rules (D5, the matrix from task 16);
  - charts (wrapper, mapping table, N/A gaps, lazy loading);
  - branding (public fields, logo rules, presets, audit fields);
  - persistence scope (D6, per browser);
  - the bundle numbers (baseline vs new);
  - the D1 attribution note.
- `docs/frontend-ui-foundation.md`: line 3 lists `chart.js` (MIT) as the only added UI dependency; line 4 covers the theme presets and dark mode; replace line 13 with a pointer to the new doc; line 8 extends the `shared/ui` list.
- `docs/security-administration.md`: the settings section gets the branding fields and the 2 logo endpoints. The audited fields list adds `accentColor` and `logo`.
- `docs/reports-and-management.md`: charts replace the CSS bars; the semantics are unchanged.
- `README.md`: one line on the language and theme switchers.

---

## Edge Cases & Failure Modes

- **Anonymous branding read exposes only 3 fields:** `GetPublicBrandingAsync` projects `DisplayName`, `AccentColor` and `LogoUpdatedAtUtc` only (task 6). A test GET as anonymous must show no `supportEmail`, `version`, `updatedByName` or other field. The SuperAdmin settings `GET` stays `[Authorize(Policy = SuperAdmin)]` (`AdministrationController.cs` line 22): anonymous ⇒ 401, Agent/Customer ⇒ 403.
- **Logo route can only return the logo row:** the `GetLogoFileAsync` query targets `OrganizationLogos` Id 1. It has no id parameter, so there is no IDOR path to `CustomerAttachments`.
- **Malicious logo:** an SVG, HTML or polyglot renamed `.png` ⇒ the signature check fails ⇒ 400 `file`. A JPEG renamed `.png` ⇒ type mismatch ⇒ 400. Over 512 KB ⇒ 400; over 1 MB ⇒ the Kestrel limit 413 (Angular shows the translated `errors.tooLarge`). The stored type comes from `DetectImageType`. The response carries `nosniff` and a `sandbox` CSP.
- **Logo upload without or with a stale If-Match:** 428 / 409 (`EntityTags.cs` lines 18–44, handler version check). The 409 shows the existing conflict banner and Reload.
- **Logo upload while the settings form has unsaved edits:** the version changes. The page keeps the edited values (task 19), and the next Save uses the new version, so there is no false 409.
- **Remove with no logo:** 200, no audit, same version (`RemoveLogo` returns empty).
- **Branding API down or slow at startup:** `Branding.load` times out after 5 s and resolves with defaults ("Support CRM", violet, no logo). Sign-in still renders. The other initializers are independent.
- **Logo image 404 or broken:** `(error)` sets `logoFailed`, and the shell, auth layout and settings preview fall back to the icon. `alt=""` in the shell (decorative next to the name) and a descriptive alt in settings.
- **`localStorage` blocked / private mode:** `readPreference` returns null ⇒ English, OS theme, expanded nav. `writePreference` swallows errors, so the choice lasts for the page only.
- **Arabic chunk fails to load:** `I18n.use('ar')` catches, keeps English and shows `errors.languageLoad`. The stored preference is left as-is, so the next reload retries.
- **Missing Arabic key:** impossible at build (D3 type). A key typo in a template ⇒ the English fallback; if absent from EN too, the raw key is shown and caught by the task 15 grep and the review.
- **Language switch inside a dirty form:** forms are class-level `FormGroup`s and the switch only re-renders text, so values and URLs are kept (§10.4). Validation messages re-render in the new language.
- **Language switch with an open dialog, menu or datepicker:** CDK overlays read the root `Directionality` at open time. An already-open overlay keeps its direction until reopened. Acceptable; documented. The switcher menu itself closes on selection.
- **Typed dates in Arabic UI:** with Latin digits (D4), the native adapter's `parse` uses `Date.parse` and accepts `2030-01-31` or locale formats. An unparseable input ⇒ `matDatepickerParse` error ⇒ `validation.invalidDate`.
- **Date-only in Cairo (UTC+2/+3):** picking 31 Jan gives a local midnight `Date`. `toDateOnly` uses local getters ⇒ `2030-01-31`. The old `toISOString()` path would give `2030-01-30T22:00Z`, and it is never used. Covered by the frontend test.
- **DST gaps for date-time pairs:** `combineLocalDateTime` builds a local `Date` through the constructor, the same behaviour as today's `new Date('yyyy-MM-ddTHH:mm')` in `fromLocalInput`. Saved instants are identical for the same wall time.
- **Report custom range `from > to`:** the client message is the same as today (lines 92–94); otherwise the server 400 is shown. A missing endpoint ⇒ "Choose both a start and an end date." (translated).
- **SLA compliance all-null:** the chart card is empty with "N/A — nothing decided yet.". A partially null series ⇒ line gaps and tooltip "N/A". **Never 0 %.**
- **Zero-feedback CSAT:** `averageRating null` ⇒ KPI "N/A", distribution empty state. No doughnut of zeros.
- **Chart.js chunk load failure:** `crm-chart` catches the import error and renders nothing. The data table below still shows every number.
- **Theme or accent change while charts are visible:** the chart effect re-reads the CSS variables and calls `update()`.
- **Breadcrumb on an unknown route:** `**` redirects to `/`, and crumbs are recomputed.
- **Role-hidden tabs:** the Ticket detail tabs show no additional data; Assignment is still gated by `a.canAssign`. KB Categories link: SuperAdmin only. **The backend remains authoritative** (all existing policies unchanged).
- **Agent deep-links to `/tickets/<unassigned id>?tab=ai`:** the existing GET ⇒ 404 ⇒ the existing `loadError` / access-lost path. The AI tab never mounts without a ticket.
- **AI tab mount:** `TicketAiAssistant` sends nothing in `ngOnInit` (`ticket-ai-assistant.ts` lines 103–105), so opening `?tab=ai` sends 0 requests to `/ai/*`.
- **Notification bell polling:** unchanged (60 s, Agent only). Language and theme switches trigger **no** API calls except the one-time `ar` chunk fetch.
- **Bundle:** if `initial` grows above 1 MB (the error budget), the build fails. Stop and report (D16).

---

## Test Plan

1. **Backend unit (the only new backend test) — create `backend/tests/Crm.UnitTests/Administration/SetOrganizationLogoCommandHandlerTests.cs`:**
   - `[Fact] Handle_ValidPngWithCurrentVersion_StoresLogoUpdatesSettingsMetadataAndWritesOneAuditEntryInOneSave`.
   - Arrange:
     - a `FakeOrganizationSettingsRepository` holding an `OrganizationSettings` created through the private constructor via `Activator.CreateInstance(typeof(OrganizationSettings), nonPublic: true)`, with `Id = 1`, then `Update("Acme Support", null, null, null, null, actor, earlier)`;
     - `GetVersion` returns `[1,2,3,4,5,6,7,8]`;
     - `FindLogoAsync` returns null and records `AddLogo`;
     - `FindUserNameAsync` returns "Admin User";
     - the other members throw `NotSupportedException`;
     - a `FakeAdminAuditRepository` recording `Add` (pattern `SetAccountStatusCommandHandlerTests.cs`);
     - a `FakeUnitOfWork` counting saves;
     - a nested `FixedTimeProvider` (lines 44–47 of that file);
     - content = the 8 PNG signature bytes plus 2040 zero bytes (2048 total).
   - Act: `Handle(new SetOrganizationLogoCommand(actorId, [1,2,3,4,5,6,7,8], @"C:\logos\brand.PNG", content), CancellationToken.None)`.
   - Assert:
     - the added logo has `ContentType == "image/png"` and `Content` equal to the input;
     - the result has `HasLogo == true`, `LogoContentType == "image/png"`, `LogoSizeBytes == 2048`, `LogoUpdatedAtUtc == now`, `DisplayName == "Acme Support"`;
     - exactly one audit entry: `Action == OrganizationSettingsUpdated`, `TargetType == OrganizationSettings`, single change `("logo", null, "image/png, 2 KB")`;
     - `SaveCount == 1`.
   - No other new backend test. 14 existing ⇒ **15 total**.
2. **Frontend unit (the only new frontend test) — create `frontend/src/app/shared/date/date-only.spec.ts`:**
   - one `describe('date-only and local date-time conversion')` with one `it('keeps picked calendar days and wall-clock times exact in any timezone (no Cairo shift)', …)` (pattern `report-format.spec.ts`). It asserts:
     - `toDateOnly(new Date(2030, 0, 31))` = `'2030-01-31'`;
     - `toDateOnly(new Date(2030, 11, 31, 23, 59))` = `'2030-12-31'`;
     - `toDateOnly(null)` = `null`;
     - `fromDateOnly('2030-01-31')` has `getFullYear() === 2030`, `getMonth() === 0` and `getDate() === 31`;
     - `fromDateOnly('2030-02-31')` = null; `fromDateOnly('31/01/2030')` = null;
     - round trip: `toDateOnly(fromDateOnly('2030-07-01'))` = `'2030-07-01'`;
     - `combineLocalDateTime(new Date(2030, 0, 31), new Date(2000, 0, 1, 9, 30))` equals `new Date(2030, 0, 31, 9, 30).toISOString()` (the same instant the old `fromLocalInput('2030-01-31T09:30')` produced: also assert equality with that call);
     - `combineLocalDateTime(new Date(2030, 0, 31), null)` = null;
     - `splitIsoToLocal(combineLocalDateTime(...))` returns the same day and `getHours() === 9`, `getMinutes() === 30`;
     - `toTimeOfDay(fromTimeOfDay('08:05'))` = `'08:05'`; `fromTimeOfDay('25:00')` = null.
   - **15 total.**
3. **Existing suites:** all 14 backend + 14 frontend tests pass **unchanged** (D15). Do not edit any existing spec. If one fails, the EN dictionary differs from the old literal: fix the dictionary, not the test.
4. **Build-time checks (not tests):** `ng build` type-checks `AR: Translations` (completeness); the task 15 grep commands; `has-pending-model-changes`.
5. **Manual (no new automated suites):** Verification steps 6–17 cover the route-by-route preservation matrix in en/ar × light/dark × desktop/400 px, plus API/SQL security. This follows intake §11.

---

## Migration / Rollback

- **Forward:** `AddOrganizationBranding` adds 4 nullable columns to `OrganizationSettings` and creates `OrganizationLogos`. Purely additive: no existing column or row changes, and the settings row keeps its values.
- **Before applying:** record `SELECT COUNT(*)` for `AspNetUsers`, `Tickets`, `TicketActivities`, `TicketFeedback`, `Customers`, `CustomerAttachments`, `AdminAuditLogs`, `KnowledgeArticles`, `AgentTasks`, and `SELECT * FROM OrganizationSettings`. Re-check after applying ⇒ identical, plus 4 new NULL columns.
- **Rollback:** `dotnet ef database update AddAdministrationAuditAndSettings --project src/Crm.Infrastructure --startup-project src/Crm.Api` drops the logo table and the 4 columns (loses only accent and logo). Then `dotnet ef migrations remove` and revert the code. Audit rows already written with `accentColor` / `logo` fields stay (append-only) and remain readable: `auditFieldLabel` falls back to the raw field name (`admin-format.ts` line 24).
- **Half-applied state:** one EF migration transaction, so on failure nothing changes; re-run after fixing. With the migration pending, `/api/v1/branding` returns 500. The Angular `Branding.load` swallows it, so the defaults show and sign-in still works.
- **Frontend-only rollback:** reverting the Angular code keeps working against the new API, because every new response field is additive.

---

## Verification Steps

1. **Baseline:** in the repo root, `git status` ⇒ only the untracked platform-features intake/plan folders; `git log --oneline -3` ⇒ `ADMIN-001` present. In `backend/`: `dotnet test` ⇒ 14 passed. In `frontend/`: `npm test -- --watch=false` ⇒ 14 passed. `npx ng build` ⇒ initial 637.91 kB and the existing budget warning (record any difference).
2. **Backend builds and tests:** in `backend/`: `dotnet build` (0 errors; list pre-existing warnings separately), then `dotnet test` ⇒ **15 passed**.
3. **Migration:** in `backend/`:
   - review `AddOrganizationBranding` (task 6 checklist), then `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api`;
   - `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api` ⇒ no changes;
   - row counts from Migration / Rollback unchanged; `OrganizationSettings` row 1 keeps its `DisplayName`.
4. **Frontend builds and tests:** in `frontend/`:
   - `npx ng build` ⇒ no errors. Record initial raw/transfer vs 637.91 / 149.30 kB, and confirm `chart.js` is **not** in an initial chunk: `npx ng build --stats-json`, then check that `dist/crm-frontend/stats.json` lists chart.js only under lazy chunks, or grep the initial `main-*.js` and `chunk-*` files from the table for `Chart.register`;
   - every component style ≤ 4 kB;
   - `npm test -- --watch=false` ⇒ **15 passed**;
   - `npm ls chart.js` ⇒ 4.5.1; `npm view chart.js@4.5.1 license` ⇒ MIT; `npm ls @ng-matero/extensions` ⇒ empty.
5. **Setup:** `.\run-dev.ps1`. Log in via `POST /api/v1/auth/login` as `$admin` (SuperAdmin), an Agent `$agent` and a Customer `$cust`. Never paste token values into the report.
6. **Branding API security (§11):**
   - `GET /api/v1/branding` with no token ⇒ 200 with **exactly** the keys `displayName`, `accentColor`, `logoVersion`;
   - `GET /api/v1/branding/logo` with no logo ⇒ 404;
   - `GET /api/v1/admin/organization-settings` with no token ⇒ 401, `$agent` / `$cust` ⇒ 403;
   - `PUT` / `DELETE /admin/organization-settings/logo` with no token ⇒ 401, `$agent` / `$cust` ⇒ 403, `$admin` without `If-Match` ⇒ 428, stale ⇒ 409;
   - as `$admin` with the current ETag: upload a 30 KB PNG ⇒ 200, `hasLogo:true`, new ETag;
   - `GET /branding` ⇒ `logoVersion` set; `GET /branding/logo` ⇒ 200 `image/png` with `X-Content-Type-Options: nosniff`, the CSP `sandbox` header and inline disposition;
   - upload `evil.png` containing `<svg …>` text ⇒ 400 `file`; a 700 KB JPEG ⇒ 400; a `.gif` ⇒ 400;
   - `DELETE` ⇒ 200 `hasLogo:false`; a second `DELETE` ⇒ 200, same ETag;
   - SQL: `SELECT Action, Changes FROM AdminAuditLogs ORDER BY Id DESC` ⇒ one `OrganizationSettingsUpdated` row for the upload, one for the remove, none for the no-op or failures. `SELECT COUNT(*) FROM OrganizationLogos` ⇒ 0 after remove.
7. **Accent:** `PUT /admin/organization-settings` with `accentColor:"teal"` ⇒ 400 `accentColor`; with `"green"` ⇒ 200 and one audit row with `accentColor: null→green`; the identical PUT again ⇒ no new audit row.
8. **Existing API regression (spot-check, unchanged contracts):** `GET /api/v1/tickets` as `$agent` (assigned only), `GET /api/v1/portal/tickets` as `$cust` (own only), `GET /api/v1/reports/tickets?preset=Custom&from=2026-10-01&to=2026-10-18` as `$admin` (save the JSON), the Agent dashboard, and KB articles ⇒ the same shapes and status codes as before the story.
9. **Browser — SuperAdmin desktop (§10.1, §10.5–10.7)** in Chrome at 1440 px, English, light:
   - the branded shell shows the org name and the uploaded logo; the nav groups are Workspace, Knowledge & tools, Management and Account;
   - collapse to rail and reload ⇒ still collapsed;
   - open every SuperAdmin route of the inventory (#3, 4, 6–15, 17–19, 21, 22) and perform its "Must keep" action;
   - Reports: choose Last 30 days, then Custom by clicking 1 Oct – 18 Oct in the calendar popup (no typing) ⇒ URL `from=2026-10-01&to=2026-10-18`;
   - compare the Ticket report numbers with the step 8 JSON ⇒ identical;
   - hover and keyboard-focus the chart points ⇒ tooltips; data tables show the same numbers;
   - SLA trend with N/A buckets ⇒ gaps, no 0 %;
   - Admin → Settings: pick the "Green" accent ⇒ the buttons and active nav change after Save; upload and then remove a logo ⇒ the shell updates without reload.
10. **Browser — Agent desktop (§10.2, §10.8):**
    - only Agent groups and items are visible;
    - type `/reports`, `/admin/users`, `/sla` and `/knowledge-base/categories` ⇒ redirected to `/`;
    - open an assigned ticket; use every tab: transition, edit (switch tabs with unsaved edits ⇒ values kept, "•" marker), activity, discussion post, KB link, quick reply copy;
    - open the AI tab ⇒ DevTools Network shows **no** `/ai/` request until an AI button is clicked;
    - tasks: create a task with due 31 Oct 09:30 via the pickers. SQL `SELECT DueAtUtc FROM AgentTasks WHERE Id=…` ⇒ `2026-10-31 07:30:00 +00:00` when the PC is in Africa/Cairo (UTC+2 on that date), or the matching UTC for the local zone; record the zone;
    - log an interaction with the date + time pickers ⇒ correct `OccurredAtUtc`.
11. **Browser — Customer at 400 px (§10.3):** DevTools device width 400:
    - sign-up (or sign-in) on the branded auth layout;
    - the portal drawer navigation works;
    - create a request, view it, browse the Help Center and an article, submit and then edit CSAT (ETag flow);
    - no horizontal page scroll on any portal route;
    - staff URLs (`/tickets`) ⇒ redirected.
12. **Language (§10.4, PL3):**
    - on `/tickets/<id>?tab=details` with an unsaved edit, switch to العربية ⇒ `<html dir="rtl" lang="ar">`, the sidebar is on the right, the whole UI is Arabic, the subject, description and customer name are unchanged, the edit is kept and the URL is unchanged;
    - open the status select, a datepicker, the confirm dialog (remove collaborator), the user menu, a tooltip and a snackbar ⇒ all RTL and Arabic;
    - IDs and emails are LTR-isolated, and dates use Arabic month names with Latin digits;
    - reload ⇒ Arabic restored with no English flash; a new tab ⇒ Arabic;
    - switch back to English ⇒ nothing modified (SQL: `Tickets.UpdatedAtUtc` unchanged).
    - Repeat the step 9 route walk in Arabic for **every** inventory route. Record in a table: route / Arabic complete (yes/no) / residual English (list) / RTL issues.
13. **Theme (§10.5):** toggle dark and reload ⇒ dark kept; check contrast of the badges, charts, tables and disabled buttons in dark for all 6 accents (spot-check the settings preview); log out ⇒ the sign-in page keeps the theme and language.
14. **Dates (§10.6):**
    - with the OS timezone set to Africa/Cairo: custom report range including the end day ⇒ the API `period.to` equals the chosen day;
    - add holiday 25 Dec via the picker ⇒ SQL / JSON `"2026-12-25"`;
    - the audit filter From/To 18 Oct ⇒ today's entries shown;
    - calendar open/close times via the timepicker ⇒ JSON `"09:00"` / `"17:00"`;
    - the preview with date + time ⇒ same result as before for the same wall time.
15. **Failures (§10.9):**
    - stop the API ⇒ each page shows a translated error with Retry; restart and Retry ⇒ loads;
    - a 400 on ticket create ⇒ server field messages shown; the form values are kept;
    - 409: edit the org settings in two tabs ⇒ the second Save shows the conflict + Reload;
    - Save is disabled during requests; no success snackbar after a failure.
16. **No side effects (§10.10):** with SQL Profiler or the API logs, or simply `SELECT MAX(UpdatedAtUtc)` on `Tickets`, `AgentTasks` and `CustomerNotes` before and after a full browse + language/theme switching + report charts ⇒ no writes. AI request count during browsing = 0. No outbound messages (none exist).
17. **Keyboard and accessibility:**
    - Tab from the top ⇒ "Skip to content" first; nav items show visible focus; tabs are operable with arrows; the datepicker is keyboard-selectable; chart data is reachable as a table; dialogs trap focus and restore it.
    - Reduced motion on (OS setting) ⇒ no sidenav or chart animation.

## Done Criteria

- [ ] One Ng-Matero-inspired shell (grouped role-filtered sidebar with rail, top bar with breadcrumbs, language/theme switchers and user menu, branded auth layout) covers staff, Customer and auth screens. No Ng-Matero package or copied code; `chart.js@4.5.1` (MIT) is the only new dependency (D1, D2).
- [ ] Every route in the preservation inventory still works for its authorized role with all listed actions, verified in the browser. No guard, route path or API policy changed; old URLs and deep links work, and `?tab=` deep links open the right tab.
- [ ] Ticket and Customer detail are tabbed without losing unsaved edits; Agent Dashboard, SLA, Reports, KB and Administration have tab navigation; KB Categories is visible to SuperAdmin only.
- [ ] All 8 date controls in the migration matrix use Material date/date-range/time pickers with a visible calendar affordance. API date formats and saved instants are identical to before. No Cairo shift (frontend test + Verification 14).
- [ ] Report charts (bar/line/doughnut) use only existing API fields, show tooltips, legends and data tables, render null as N/A gaps (never 0 %), and Chart.js is lazy (not in the initial bundle). Report numbers are unchanged.
- [ ] English/Arabic: every system string on every screen is translated (`AR: Translations` compiles; grep residuals listed). RTL applies to the layout and all CDK overlays. The language persists per browser with no first-render flash. User/API content and enum values are never translated.
- [ ] Branding: org name from ADMIN-001 `DisplayName` (single source, "Support CRM" fallback); optional PNG/JPEG/WebP logo ≤ 512 KB, signature-checked and stored in SQL; 6 accent presets; light/dark per browser. Anonymous `GET /api/v1/branding` returns exactly 3 fields; the SuperAdmin settings stay protected. Logo/accent changes are ETag-protected and audited as `OrganizationSettingsUpdated`.
- [ ] One additive migration `AddOrganizationBranding`; `has-pending-model-changes` reports none; existing row counts unchanged.
- [ ] Exactly one new backend test and one new frontend test (15/15). All existing tests pass unchanged. Both builds succeed. Bundle size reported vs the 637.91 kB baseline, with no budget raised.
- [ ] Responsive at 400 px (no page-level horizontal scroll), keyboard accessible, visible focus, reduced motion respected.
- [ ] No automatic AI calls, ticket writes or communications caused by navigation, tabs, charts, language or theme.
- [ ] `docs/platform-ui-and-localization.md` created. The UI foundation, security-administration, reports and identity docs and the README are updated.
- [ ] The implementation report marks each verification step `PASS` / `FAIL` / `UNVERIFIED` honestly, including the per-route Arabic table. Nothing is committed or pushed until the user reviews and accepts PLAT-001.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
