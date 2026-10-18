# Story 15 — Platform Presentation & Complete CRM UI Modernization — Business Feature Intake

## Feature

- **Display name:** Platform Features — Complete CRM UI Modernization, English/Arabic Localization, RTL, Responsive UX & Organization Branding
- **Feature slug:** `platform-features`
- **Work item ID:** `PLAT-001`
- **Work item type:** `Business Feature Story`
- **Story number:** `15` (verify against actual Squad-Kit index)
- **Tracker:** `none` (local training/assessment CRM)
- **Status:** `UI direction + PL3–PL5 approved — ready for Squad-Kit planning`
- **Labels:** `platform, redesign, ux, angular, material, ng-matero, localization, arabic, english, rtl, theming, branding, responsive, charts, datepicker, accessibility, squad-kit, training`

## Title

Platform Features — Ng-Matero-Inspired Full CRM Frontend Redesign, English/Arabic RTL, Functional Calendars & Charts, and Organization Branding

## 1. Business objective and binding scope

The user finds the **existing CRM UI primitive**, with poor information hierarchy, large pages containing unrelated content, manual-entry-only date fields, and primitive static graphs. Replace the visual and interaction layer with a **modern, professional, workflow-focused CRM interface** informed by the publicly available, free **Ng-Matero / Angular Material** design system. Improve **every existing staff and Customer Portal view** while preserving the application already delivered in Stories 01–14.

Deliver all of the following as real working features, not mockups:

1. **Full UI/UX modernization** across existing CRM modules: consistent shell, typography, responsive navigation, polished cards/tables/forms, contextual tabs or nested child routes for complex screens, and clear user feedback.
2. **Functional calendar/date-range pickers** wherever the app currently depends on date entry: real calendar popup interaction, appropriate presets, locale display, proper validation and preserved backend date semantics.
3. **Interactive, responsive charts** for existing data-driven dashboards/reports; use actual current APIs and definitions, with tooltips, legends, accessible numeric data and accurate loading/empty/error states.
4. **Complete English + Arabic localization**, including RTL/LTR direction switching and persisted language preference across sessions; user-generated content remains exactly as saved.
5. **Basic organization branding**, reusing Story 14 Organization Settings display name: optional SuperAdmin-managed logo, a small accessible primary-accent preset palette, and user-selectable persistent light/dark themes, consistently applied on login, staff and Customer Portal screens.
6. **Full feature preservation**: same SQL Server data, views/information, core operations, valid CRUD outcomes, business rules, authentication, JWT/session behavior, role policies, assignment/ownership restrictions, SLA/automation, reporting calculations and AI opt-in. Restructuring Angular pages is allowed; useful API splitting is allowed only when behavior and existing consumers remain correct.

**User expressly approved** UI Direction **A**, PL3 **A**, PL4 **A**, and PL5 **A** (§3). **Multiple Departments and Multiple Branches are expressly deferred to a separate future story**; neither is part of `PLAT-001`. **Communication Channels remains the very last module**; do not implement conversations/email/WhatsApp/SMS/chat as part of this redesign.

This remains a **training/assessment CRM**: implement high-quality core UX at straightforward engineering depth, without replatforming to another SPA framework, enterprise theming/i18n infrastructure, a design-asset CMS, multi-tenancy, major CQRS/API redesign or expansive automated testing.

## 2. Existing system and baseline to inspect

- Work inside the existing repository; the user accepted Story 14 `ADMIN-001` with all six browser groups passing and authorized its local commit. **Check `git status`, `git log`, `.squad/plans/00-index.md`, and Story 14 commit state before beginning planning; do not assume that the commit was actually performed unless verified.** Do not commit/push during planning or implementation before independent acceptance.
- Backend: .NET 10, Clean Architecture (`Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`), Identity/JWT, CQRS, EF Core 10, SQL Server LocalDB `CrmDev`, `/api/v1`, existing validation and error conventions.
- Frontend: Angular 22 standalone components, Angular Material 22, role-based navigation, responsive existing shell, typed API clients, guards, authentication/session handling and Vitest unit tests.
- Reporting (`REPORT-001`) provides four existing API datasets for Ticket, SLA, Agent Performance and CSAT, with unified management UI. Its date-based metrics use **UTC day boundaries** and carefully labeled live snapshots versus filtered cohorts; preserve exact semantics. It currently has basic CSS-rendered charts that need better data visualization.
- Admin (`ADMIN-001`) provides SuperAdmin Users, Permissions, Audit Logs, Organization Settings and existing Agent provisioning route; settings include persisted organization `DisplayName`, support email, phone and information. Organization Settings APIs are SuperAdmin-only, including `GET`, so **public login branding must not read that privileged endpoint anonymously**; planner should choose a minimal, explicitly public, whitelisted branding-only endpoint or appropriate equivalent.
- Customer Portal uses own-ticket authorization, published/public Knowledge Base Help Center, CSAT feedback and profile linkage. Agent/Customer/SuperAdmin route access must not change due to new tabs, theming or language changes.
- SLA/automation background processing and business-hours calendar, TicketActivity, Knowledge Base statuses, AI-001 user-initiated actions and saved Customer Portal feedback all remain intact.
- Baseline after Story 14 reported **14 backend + 14 frontend passing unit tests**, with builds succeeding and an existing initial Angular bundle warning (~638 kB; previous baseline not formally measured). Verify actual baseline before using test counts or attributing any bundle increase.
- Study actual routes, screens, component hierarchy, CSS/SCSS, dependencies, Angular Material version, error presentation, forms, date controls, `docs/identity-and-access.md`, Story 14 theme/organization settings code and all tested feature flows. Do not assume names or specific API signatures from this intake.

## 3. Approved business decisions — binding

| ID | User-approved selection | Binding requirements |
| --- | --- | --- |
| **UI1 — Visual foundation** | **Direction A: Ng-Matero-inspired Angular Material foundation.** | Keep the existing Angular 22 standalone application and Angular Material. Reuse compatible Ng-Matero layout/theme/visual patterns selectively; **do not replace the app wholesale or force an incompatible template dependency**. Free/public code or assets must have verified redistribution licenses and preserved attribution where needed. Preserve the working CRM. |
| **PL3 — Language** | **Option A: Complete English/Arabic translation + RTL.** | Every existing CRM module, staff and Customer Portal, has English/Arabic UI labels, navigation, forms, tables, statuses, dialogs, messages, validation/errors and loading/empty/retry states; an accessible language switcher sets correct LTR/RTL and persists across sessions. Do **not** auto-translate customer text, ticket subjects, comments, notes, KB bodies, AI output or backend enum/database values. |
| **PL4 — Branding** | **Option A: Standard organization branding.** | Reuse `ADMIN-001` persisted organization display name; optional SuperAdmin-managed safe organization logo; accessible preset primary accent color; persistent individual light/dark appearance preference; apply branding consistently to login, staff UI and Customer Portal. No duplicate/conflicting organization setting, insecure image upload or change to business workflows. |
| **PL5 — UX organization** | **Option A: Workflow-focused modules + contextual tabs/child routes.** | Professional modern role-aware navigation; organize oversized pages into contextual tabs or nested routes, maintain all existing views/data/actions; functional Material date/date-range pickers and interactive freely licensed charts; modern tables/forms/dialogs, 400 px responsive screens, full English/Arabic RTL. Splitting APIs only when justified and backwards-safe. |
| **Deferred — Departments/Branches** | **Separate future story.** | **NO department or branch tables, filters, fields, assignment, isolation, hierarchy, policies, migrations or workflow changes in PLAT-001.** |

**Approval gate:** UI1, PL3, PL4, PL5 are approved; no more user business choices are needed to plan Story 15. Technical implementation proposals below are planner decisions, not additional user-approved requirements. Raise material conflicts rather than silently weakening the four decisions.

## 4. Guardrails: preserve application behavior and data

- Preserve **existing SQL records and relationships**. Do not reset the DB, drop/truncate existing tables, change Ticket/Customer/SLA/CSAT/Audit identities or invent dataset defaults. Additive schema change is allowed **only if needed** for branding/logo/theme persistence, documented and reviewed.
- **Keep all previously implemented roles, API contracts and record-level authorization.** SuperAdmin administrative data remains SuperAdmin-only; Agent staff-ticket access remains assignment-scoped; Customer Portal access remains linked-profile/ownership-scoped. No new role or permission editor.
- All existing ticket create/edit/status/assignee/priority/category, activities, attachments/notes, tasks/reminders/collaboration, SLA policy/calendar/rules/alerts, KB draft/publication/help center, explicitly invoked Gemini assistance, CSAT, reports and Admin workflows must remain available and correct after the redesign.
- Preserve API date coercion, enum/status/priority serialization, ETag/`If-Match` optimistic concurrency, HTTP validation/problem details, authentication refresh/revocation, and existing backend business logic. A visual redesign must not make invalid forms appear saved or hide errors.
- Avoid changes to backend merely for visual reasons. Nested routes, tabs and lazy-loaded components can typically share existing GET endpoints. If independent section loading truly benefits from splitting a large API, make the contract change additive/compatible and update every affected client; report it explicitly.
- Do not import incompatible Ng-Matero versions or its entire project. Check current Angular 22 compatibility **in the repository and the chosen upstream package's actual installed version**. Borrow MIT-licensed patterns/code only where appropriate; retain required license attribution. Do not import Pro/commercial assets, copied proprietary logos, unsupported CSS, or a package requiring a paid license for project use.
- The frontend must remain accessible by keyboard, screen-reader-friendly where practicable, with visible focus states, adequate contrast, touch-friendly controls, proper labels and reduced-motion respect where supported. Keep the implementation pragmatic rather than building a new accessibility framework.
- Do not add automatic AI calls to dashboards or on page load. **Communication Channels last**; no real outbound messages introduced in this story.

## 5. Full UI modernization: shell, layout, navigation and component standards

### 5.1 Shared application shell

- Create one coherent design language covering login/auth, staff shell and customer shell: typography, spacing scale, elevation, color tokens, icon usage, page header conventions, empty-state illustrations/icons, information banners, actionable feedback and responsive grid structure.
- Modern, collapsible, **role-filtered sidebar** with grouped modules, active route indicators, sensible icons/labels, mobile drawer behavior, top bar with language/theme switching and user/profile/logout actions. Keep existing route destinations reachable; redirects and deep links must survive browser reloads and localization changes.
- RTL: navigation side, drawer open direction, breadcrumbs, icons with directionality, text alignment and dialog placement must adapt correctly. Avoid naïvely flipping data charts, ticket IDs, email addresses, phone numbers, code blocks or numeric input reading order. Use directional CSS logical properties and Angular/CDK `Directionality` patterns where they fit.
- Prefer lazy route components for feature areas and keep initial bundle overhead controlled. No new duplicate frontends or route systems.

### 5.2 Oversized screens: contextual organization

- Choose **tabs** when areas represent one entity/detail context; choose **child routes** when areas are independent navigable pages. Preserve current URL semantics where practical; existing bookmarked routes should still work or have clear redirects.
- Examples for planning (not strict imposed tab names): Agent Dashboard [Overview/My Tickets/Tasks/Notifications], Ticket Details [Overview/Activity/Customer/SLA/Knowledge/AI], Customer Details [Profile/Tickets/Notes/Activity/Attachments], SLA [Policies/Business Calendar/Rules/Escalations/Alerts], KB [Articles/Categories/Management], Reports [Overview/Tickets/SLA/Agents/CSAT], Administration [Users/Permissions/Audit/Organization Settings], Portal [My Tickets/Create/Help Center/Feedback]. Existing sections that live elsewhere can remain there; **do not duplicate screens** just to match the examples.
- A deep-linkable URL should lead to the corresponding record/tab when useful. Keep meaningful primary actions visible, avoid 8+ densely packed tabs if splitting into nested navigation is clearer. Ensure forms do not silently lose unsaved edits when navigating tabs.
- Do not show privileged tab labels, links or data to a role without authorization. All backend checks remain authoritative.

### 5.3 Forms/tables/interaction

- Replace primitive controls with consistent Angular Material input/select/autocomplete/date-picker and accessible confirmation dialogs where suitable; retain existing reactive-form validation and server-side validation detail rendering.
- Tables: legible headers, useful filters/search, pagination and sorting **where supported by data/APIs**, safe text wrapping, relevant row-level actions, compact status/priority badges, responsive overflow or adaptive presentation (without hiding essential columns/actions).
- Loading spinners/skeletons, honest empty states, friendly errors, Retry and success feedback must work for each important independently loaded section. No stale data displayed as if it belongs to newly chosen filters/accounts.
- Preserve confirmations and ETag conflict/reload UX for current administration/settings/tasks/feedback; disable Save during pending request; never incorrectly report success after failed API call.

## 6. Functional date/time and charting overhaul

### 6.1 Date pickers

- Replace existing manual-only date/date-range inputs with **clickable Angular Material calendar popups**, with accessible keyboard selection and a clearly visible calendar affordance. Text input may remain as an additional supported entry method, **not the only method**.
- For Reports, preserve 7/30/90-day and custom period presets, default behavior, max/min/range rules, UTC inclusive UI days mapped to existing half-open UTC API windows, correct last-day inclusion and appropriate day/week buckets. Cairo timezone must not shift `YYYY-MM-DD` labels or date-only values.
- Support Arabic date-picker locale/labels and RTL direction, readable weekday/month navigation, date normalization and input errors. Do not assume a local-midnight `Date` string or JS `DatePipe` with timezone can always safely render date-only UTC values.
- Replace manual-only fields in other modules too (SLA business calendar/holidays, tasks/reminders, audit filters and all date-related pages identified by source audit), preserving the existing endpoint's expected date format and time semantics.
- For date-time inputs, retain valid time selection and local/UTC conversion expectations; do not silently change SLA deadlines, reminders, activity timestamps, CSAT submission dates or other persisted values.

### 6.2 Charts

- Upgrade primitive/static CSS graphs using an actively maintained, **freely licensed and Angular 22-compatible** chart solution; **Chart.js (MIT)** is a preferred option to validate. Avoid depending on licensed-only/proprietary components or chart libraries whose current licensing is unsuitable; verify exact dependency licenses rather than relying on marketing labels.
- Apply interactive charts to existing dashboard/report measures where graphs are already relevant: Ticket volume/trend, status/category/priority breakdown, SLA compliance/trends, Agent workload/actions, CSAT distribution and unified KPIs. Do not invent new business metrics or revise existing SQL/reporting calculations.
- Use suitable bar, line and doughnut charts, tooltips, legends, clear axes/units, visible numerical summaries/tables and empty/zero-data states. Respect RTL text while keeping chronological axis direction meaningful. Responsive at desktop and ~400 px.
- Only use data already returned from APIs unless additive endpoints are strictly necessary. Chart rendering must **not cause writes**, AI calls, extra notifications or inaccurate live-vs-period snapshots; preserve timestamp/cohort labeling and manual refresh behavior.
- Optimize bundle loading (lazy/chart library on relevant routes), report unavoidable bundle-size warnings without hiding them; compare baseline if available.

## 7. PL3 — Complete bilingual UI and RTL/LTR

- Language switcher visible to logged-in staff and Customer Portal users and appropriately accessible on login/registration. Choose language `en` or `ar` without requiring logout or a full data reset.
- **Every existing UI screen** must have translated, understandable English/Arabic interface text: sidebar/topbar, breadcrumbs, page headers, cards/KPIs, tables/headings, forms/labels/placeholders, action buttons, statuses/enum names, validation errors, dialogs, toasts, loading/empty/error/retry messages, calendar labels and other system-owned strings.
- Persist the chosen language for subsequent visits/sessions (at minimum per browser, not just component memory). Planner verifies whether account-level cross-device persistence exists or is needed; user approved persistent preference, not any particular database design. Defaults must be predictable; no forced language from IP or location.
- RTL direction switches globally and correctly integrates Angular Material/CDK overlays, popups, dialogs, menus, tooltips and forms. Prevent partial directionality in modals or date-picker overlays. Preserve proper mixed-direction display of email, URLs, ID values, numbers and code.
- Preserve database/storage enum values, API contracts and existing user-entered ticket titles/descriptions/comments/notes/KB article bodies/feedback, free-form settings and AI responses **verbatim**. UI labels may be translated; do not machine-translate persisted business content or call Gemini for localization.
- If backend validation/errors arrive in English, choose a maintainable **frontend localization mapping** for known messages/field codes; retain safe readable fallback for unexpected server error text. Do not rewrite every backend exception or localize internal API tokens.
- Avoid `instant` assumptions causing untranslated first render, unnecessary reloads or translation flashes; establish a reusable translation mechanism. Keep translation keys organized by shared + feature, with a completeness inventory so no module is silently left English-only.
- Date/time text should be locale-appropriate without changing actual UTC storage or backend-defined calculation windows. Language/RTL switching must leave active forms and URLs stable where feasible.

## 8. PL4 — Organization branding and theme

- Reuse **Story 14 persisted Organization Settings display name as the single authoritative organization-name source**. No second contradictory display-name field or hardcoded real-organization name. Show reasonable generic CRM fallback when unconfigured.
- Add **one optional SuperAdmin-managed organization logo**, with preview/replace/remove action and effective display across login, staff shell and Customer Portal. Use safe, simple existing storage conventions if available; validate file size/type/actual file signature, limit image formats to low-risk raster PNG/JPEG/WebP unless strong reason otherwise, enforce SuperAdmin on mutations, avoid path traversal or untrusted SVG/HTML, and serve only public **branding bytes** where needed for logged-out login. Do not expose private uploaded ticket attachments through branding routes.
- A small curated palette of accessible primary accent colors, not unrestricted arbitrary CSS or a custom theme builder. Apply it consistently to active nav elements, buttons, focus rings and charts where helpful while maintaining readable contrast in both themes.
- Individual users can switch **light/dark** appearance and keep it across sessions; a logged-out visitor can use a reasonable default/preference without exposing the account's secrets. Prefer existing preferences infrastructure or simple browser persistence unless repository evidence calls for something else; document exact scope of persistence (browser vs account) rather than claiming unsupported cross-device synchronization.
- Organization branding values (logo/color/organization name) should apply globally rather than per-user, and the SuperAdmin config controls belong to a consistent existing Administration/Settings UI, not a duplicate settings module. Reuse existing row/version/audit conventions when modifying organization branding fields. If new changes are considered administrative setting changes, ensure truthful audit handling or explicitly document their treatment in the plan instead of silently bypassing the audited settings route.
- Because the login is anonymous, **do not expose the existing SuperAdmin-only `GET /api/v1/admin/organization-settings` to anonymous callers**. Design the smallest allowlisted public branding response (`displayName`, safe logo URL and allowed accent identifier only) or equivalent public static assets; do not publish support settings, updated-by identity or secret internals unnecessarily.
- Brand changes must not send email, alter business policies, SLA, ticket creation or authorization. Show sensible image fallback on logo read errors.

## 9. Screen-by-screen preservation inventory — mandatory planning deliverable

**The planner must enumerate actual Angular routes/components and produce a before→after inventory**, with existing user actions, sensitive permissions, required data calls, intended UI transformation and regression check for **each** screen. The following is a minimum checklist to refine from actual repository source; do not assume that every example is currently a separate route.

| Existing CRM capability | Minimum preserved functions and expected UX modernization |
| --- | --- |
| Authentication/Profile | Login, Customer registration, JWT refresh/logout, profile/password actions; branded bilingual forms; error/validation states; no authorization regression. |
| Staff navigation/SuperAdmin navigation | Role-specific links, breadcrumbs, layout and sidebar; no inappropriate access; mobile drawer and directionality. |
| Agent Dashboard | Personal workload, ticket access, tasks/reminders/notifications as implemented; interactive data presentation and logical grouping; keep scoped data. |
| Tickets list/create/edit/detail | Every field, search/filter, create/edit, status transitions, assignee, activities, authorization, action UX; detail tabs/child routes and preserved deep links. |
| Tasks/Reminders/Collaboration | Existing task CRUD/status/reminders, member/collaboration and notifications; usable date/time pickers and dialogs; no behavior removal. |
| Customers and Customer Records | Profiles, search/list/create/edit, tickets, notes, history, attachments, related actions; contextual entity tabs; preserve authorization/data. |
| SLA & Automation | Policy settings, calendar/holidays, rule configuration, alerts/escalations, assignment/worker-driven displays; date pickers and segmented navigation; same real backend behavior. |
| Knowledge Base | Staff article/category management, drafts/publishing/linking where permitted, search and Help Center; polished text presentation and permissions. |
| AI Assistance | Staff-only, explicitly user-initiated summary/reply/category/solution/chat functions; advisory/apply-only semantics; never fire AI on tab mount or chart render. |
| Customer Portal | Own tickets/list/create/detail/status history, published/public Help Center, CSAT create/edit; branded bilingual responsive customer-specific shell. |
| Reports & Management | Four existing report datasets, unified dashboard, KPIs, period presets/date range, live vs cohort labels, charts, refresh, feedback table; numbers remain consistent with previous implementation. |
| Security & Administration | Users/accounts/status/create Agent, read-only Permissions, Audit Logs/filter/date, Organization Settings/ETags; optional logo and palette controls reuse settings UI; no bypass of audit. |
| Shared app states | Loading/empty/error/retry, error messages, dialogs, validation, tables, responsive styling, accessibility, language/theme switchers across the whole app. |

Also check every preexisting route not listed above and include it in the inventory. **No feature may be considered successfully redesigned solely because its menu item is present**; the data and actions must still work for the authorized role.

## 10. User workflows / acceptance scenarios

The final implementation must satisfy these operational examples:

1. **SuperAdmin desktop:** sign in, see branded modern staff shell, navigate all authorized modules, use a refined report page with interactive charts and visible numerical totals, select Last 30 Days or a custom range using the calendar (without typing), and open relevant report subsections.
2. **Agent desktop:** see only Agent-authorized modules, open an assigned ticket, reach every historical ticket detail function through intuitive tabs, and use tasks/reminders, KB and explicit AI actions without lost data or access expansion.
3. **Customer mobile:** at ~400 px, register/sign in if relevant, navigate branded portal, create/view own ticket, browse Help Center and submit/edit CSAT without horizontal page failure or touching staff-only screens.
4. **Language switching:** switch from English to Arabic while inside a complex screen, observe comprehensive Arabic UI and RTL overlays; switch back without modifying records or losing access. Return in a new session/browser reload and retain the chosen preference.
5. **Theming:** switch dark/light and reload; selected appearance remains and elements remain legible. SuperAdmin changes accent preset and organization logo; login and staff/portal show current branding, without exposing privileged Admin settings.
6. **Dates:** select a custom report range including the period endpoint using the popup; values match the current report API, with no Cairo timezone drift. Choose a calendar holiday/date or audit filter and see correct persisted/query behavior.
7. **Charts:** hover/focus chart elements to inspect values, legends and labels; printed numbers allow users to understand counts without charts; zero or missing data is not invented as 0% compliance/CSAT.
8. **Security:** Agent/Customer cannot navigate directly to SuperAdmin admin/reports screens or call APIs; all current ticket ownership/assignment restrictions still hold.
9. **Failures:** stop API and use Retry, display validation problems, preserve useful entered values, handle 409/428 ETag conflicts, and avoid showing stale data or a falsely successful save.
10. **No side effects:** navigation/localization/appearance/reporting never triggers Gemini requests, writes to tickets or new communications automatically.

## 11. Testing and verification policy

- Retain the established training-project test policy: **exactly ONE added backend happy-path automated unit test and ONE added frontend happy-path automated unit test** for this story (even though the majority of changes are Angular presentation). The planner should pick meaningful changed seams; e.g., backend branding/public response contract and frontend language/theme/date formatter. If no backend behavior truly changes, choose the smallest meaningful applicable backend case and **do not invent unrelated infrastructure merely to manufacture a test**; document any unavoidable conflict for independent approval. Existing tests must pass. Expected **15/15** each only if 14/14 is confirmed as the committed starting point.
- All other verification is hands-on browser/API/SQL checks—not a new Playwright/E2E suite, excessive test files or enterprise screenshot tooling. Full UI redesign requires an **explicit route-by-route manual preservation matrix**, rather than smoke-checking only the dashboard.
- Build .NET and Angular, run xUnit and Vitest, inspect dependency/license list and check lazy chunks/bundle budgets; report baseline and new warnings accurately.
- Inspect any EF migration/schema diff, `has-pending-model-changes`, and DB row counts before/after; avoid destructive migrations, duplicate Organization Settings, risky logo storage or old-data mutations.
- Check API authorization as SuperAdmin/Agent/Customer/anonymous, especially any new public branding read endpoint and logo upload/edit/remove endpoints. Public branding endpoints must be restricted to intended public fields only.
- Verify old routes/deep links, forms, permissions, old API consumers, dashboard cohort/date semantics, ETag conflict handling, ticket actions, CSAT edits, SLA calendar, Audit Logs, Agent tasks and Customer Portal ownership.
- Manual UI acceptance **in both languages and both themes** at desktop and ~400 px mobile. Test RTL in Material menus/date pickers/dialogs/sidebar; mixed English/Arabic text, email/ID direction, long labels, keyboard navigation, browser refresh/entry at deep links.
- Confirm every screen in §9 has all essential controls and data/actions, with no broken links, partial untranslated English interface strings, or unexpectedly empty/new records.
- SQL/HTTP checks should verify navigation, filters, charts and localization cause **no unauthorized data writes**, automatic Gemini calls or outgoing communication messages.
- Distinguish `PASS`, `FAIL`, and `UNVERIFIED` in Claude's implementation report. **Do not claim browser checks happened when only builds/API tests ran.** Human browser acceptance and independent review precede local Git commit; no push.

## 12. Explicitly out of scope

- **Multiple Departments and Multiple Branches entirely deferred** to a separate upcoming Organization Structure story. No department/branch CRUD, Agent/team membership, ticket routing or isolation changes.
- **Communication Channels remains last:** no inbound/outbound email/SMS/WhatsApp/chat/live ticket conversations, email invitation system or notification transport integration.
- Migration from Angular to React/Vue, rewriting backend services for template compatibility, replacing existing source with another CRM app, imported proprietary/paid dashboard packs, enterprise design system or runtime UI builder.
- Enterprise i18n translation management services, automatic machine translation of user content, translated KB article storage or Gemini calls for localization.
- Arbitrary CSS injection, custom JavaScript themes, unlimited logo library, user-uploaded SVG execution, external image-hosting/asset CDN dependency or complex per-tenant branding.
- Recalculation/redefinition of established SLA/report/CSAT metrics, changing customer-ticket visibility or Agent assignments, altering permission policies, changing roles, reworking AUDIT-001 history, extra analytics tables, or automatic message/AI generation.
- New export/report scheduled-delivery features, artificial graphs/metrics, or additional independent features not needed for parity.
- New large automated E2E/performance suite and extra backend/frontend unit test cases beyond exactly one new happy-path test each.

## 13. Squad-Kit planning handoff, milestones and acceptance gates

- **Story:** `PLAT-001` — Story **15**.
- **Feature slug:** `platform-features`.
- **Intake location:** `.squad/stories/platform-features/PLAT-001/intake.md`.
- **Suggested plan location:** `.squad/plans/platform-features/15-story-platform-features-ui-modernization.md`.
- **Index/overview:** `.squad/plans/platform-features/00-overview.md` and `.squad/plans/00-index.md`.
- **Baseline dependency:** all completed Stories 01–14, most directly AUTH-001, Ticket/Customer workflows, SLA-001, KB-001, AI-001, PORTAL-001, REPORT-001, ADMIN-001; **confirm ADMIN-001 was actually committed** before implementation.
- **Planning output must be implementation-ready**, not a vague aesthetic proposal: full route/component inventory, current UI limitations, target shell/layout/component system, precise dependencies+license/compatibility, localized string inventory/strategy, RTL + locale handling, theme/branding persistence/API/security, list of required UI tabs and component refactors, chart mapping to existing API fields, date-picker migration matrix, API/migration changes if necessary, and screen-by-screen regression plan.
- **Milestone 1: Verify baseline & inventory.** Inspect commits, frontend routes/components and user flows, charts/date controls, dependencies, existing Organization Settings and authorization. Produce before/after preservation matrix.
- **Milestone 2: Shared foundation.** Adapt compatible Ng-Matero-inspired shell/tokens/nav; establish translation/RTL and light/dark preference infrastructure; verify brand-safe read model and avatar/logo handling.
- **Milestone 3: Shared controls.** Functional Material calendars, responsive tables/forms, reusable dialog/feedback patterns, interactive chart primitives; preserve date semantics and numeric labels.
- **Milestone 4: Feature-by-feature redesign.** Cover all staff modules and Customer Portal. Break giant pages into contextual tabs/child routes; verify each major workflow and avoid unrelated backend changes.
- **Milestone 5: Polish & parity.** Branding from persisted organization settings, optional logo and accent presets; full en/ar translation coverage, dark/light contrast and 400 px layout; preserve all workflows.
- **Milestone 6: Verification & docs.** Both builds, 1 backend + 1 frontend added test, existing tests, API/SQL/security, screen-by-screen browser acceptance in en/ar and light/dark, link/feature parity checklist; record unverified items explicitly.
- **Planning stop-and-report triggers:** incompatible template/package requiring Angular downgrade or wholesale app replacement; proprietary/commercial asset license requirement; inability to preserve a current critical workflow or backend permission; need for destructive database changes; inadvertent automatic AI/communications; inability to keep sensitive Organization Settings secured while providing public login branding; material i18n feature-coverage gap that would violate PL3.
- **Planning stage only:** Claude/Squad-Kit may write/update `.squad` plan, overview and index. **Do not implement application code, install packages, migrate DB, commit or push before independent plan review.** After plan approval, implement by milestones with normal manual review and no commit until user acceptance.
- Because this story is much larger than an ordinary screen, make the plan **phased and checkpointed internally**, but keep PLAT-001 as the agreed story; no arbitrary deletion/deferral of existing views to make the story appear done.

## 14. Source and approval note

- Original *Customer Support CRM — Core Features* platform section: Arabic/English, mobile-friendly, multidepartment, multibranch and organization branding.
- Explicit changes from the user: **complete frontend modernization as part of Story 15**, Ng-Matero-inspired Angular Material 22 direction, fully functioning calendars/charts and workflow-focused navigation, full en/ar RTL and persisted preference, standard branding with optional logo/accent and light/dark preference. **Departments and branches explicitly moved out of Story 15**.
- The original source and approvals do not prescribe a specific npm package, chart implementation, i18n library, persistence location or UI route names. Planner must verify what is technically compatible with checked-out Angular 22 and current license terms, and identify necessary minimal backend additions without loosening requirements.
- No current local repository source was directly available to the author of this intake. Claims about current components, routes, packages or exact settings schema are **to be verified by Squad-Kit against the local checkout**, not treated as inspected code.
