# Story 13 — Reports & Management — Business Feature Intake

## Feature

- **Display name:** Reports & Management — Ticket, SLA, Agent Performance, Customer Satisfaction & Management Dashboards
- **Feature slug:** `reports-management`
- **Work item ID:** `REPORT-001`
- **Work item type:** `Business Feature Story`
- **Story number:** `13` (verify against the current Squad-Kit index)
- **Tracker:** `none` (local training/assessment CRM)
- **Status:** `Business decisions RM1–RM6 approved — ready for Squad-Kit planning`
- **Labels:** `reporting, management, dashboard, tickets, sla, agent-performance, csat, analytics, aspnet-core, angular, squad-kit, training`

## Title

Reports & Management — SuperAdmin Ticket Reports, SLA Performance, Agent Performance, Customer Satisfaction and Unified Management Dashboard

## 1. Business goal and authoritative scope

Deliver **all five** areas listed under **Reports & Management** in the original two-page *Customer Support CRM — Core Features* document:

1. **Ticket reports** — volume, current status/category/priority distributions, creation trends and date filtering.
2. **SLA performance** — recorded first-response and resolution compliance, breaches, active at-risk targets and trends.
3. **Agent performance** — current assigned workload, status/priority breakdown, overdue workload, correctly attributed resolution actions and Agent comparisons.
4. **Customer satisfaction** — current saved CSAT scores, distribution, response count/participation and recent feedback comments.
5. **Management dashboards** — a consolidated SuperAdmin overview using the same reporting calculations and navigation to the individual reports.

Implement real, readable SQL-backed results and usable Angular pages, not static sample charts or placeholder endpoints. **RM1–RM6 in §3 are expressly approved and binding.** This is a **training/assessment project**: deliver all five core workflows at functional basic depth without an analytics warehouse, ETL pipeline, scheduled materialization, configurable widget platform or enterprise BI architecture.

**Report correctness is more important than invented coverage.** A historical metric must be calculated from data the CRM actually records. A live snapshot cannot be presented as historical performance. Clearly label data cohorts, excluded/pending targets and period semantics.

## 2. Existing application and non-regression

- Extend the **already implemented** CRM monorepo. Stories **01–12** have been accepted; the user has confirmed **Story 12 PORTAL-001 was committed**. Before implementation, independently verify `git status`, `git log`, the current Squad-Kit plan index and the actual source; do not assume a commit hash or exact repository file names.
- Backend: **.NET 10**, Clean Architecture (`Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`), Mediator/CQRS, FluentValidation, EF Core 10, SQL Server LocalDB (`CrmDev`), ASP.NET Identity/JWT and existing `/api/v1` conventions.
- Frontend: **Angular 22** standalone features, Angular Material, typed API clients, existing authentication/role guards, shared navigation, responsive layouts and existing tests.
- Reuse actual Ticket creation metadata, current status/category/priority, assigned Agent, TicketActivity status-change actors/timestamps, SLA snapshots/status/deadlines/recorded first-response fields, and the Story 12 `TicketFeedback` data. **Inspect actual names and semantics before choosing report queries**. Do not assume activity histories or timestamps that do not exist.
- Preserve all staff Ticket and Knowledge Base operations, customer portal ownership/safety, existing SLA automation and hosted workers, and AI-001 (staff-only, user-initiated). Reporting is **read-only**: viewing or refreshing a report must not mutate tickets, SLA flags, feedback or activities, call Gemini, trigger automatic assignment or send notifications.
- Existing Agent Dashboard is for operational use. This story does **not** add Agent-facing management reports.
- **Communication Channels stays the very last module.** An internal `Mark as Responded`/recorded first-response action is not proof of a customer message being transmitted.
- Prefer on-demand LINQ/SQL aggregation and projection over loading all CRM tickets into Angular or computing aggregates in memory. Reuse existing date-handling, paging, enum serialization, HTTP errors, authorization policy, frontend component patterns and charting if actually installed; avoid introducing packages unless necessary.

## 3. Approved business decisions — RM1–RM6

The following were **explicitly selected and approved by the user**. Do not reopen them without a material incompatibility discovered in the checked-out repository.

| ID | Approved option | Binding business requirements |
| --- | --- | --- |
| **RM1 — Access** | **A: SuperAdmin only** | SuperAdmin can access every management report and dashboard. Agent keeps the existing Agent Dashboard; Customer and anonymous callers get no management access. Enforce the role on backend APIs, not only Angular navigation. |
| **RM2 — Ticket Reports** | **A: Standard** | Created-ticket volume; distributions of *current* Open/InProgress/Resolved/Closed status, General/Technical/Billing/Other category, and Low/Medium/High/Urgent priority; ticket-creation trend; last 7/30/90 days and custom date range; real database aggregation. Date window is by **ticket creation date**, while grouped status/category/priority values are **current**. |
| **RM3 — SLA Reports** | **A: Standard** | Recorded first-response and resolution SLA compliance, distinct response/resolution breach counts, active at-risk tickets and a compliance trend. Same reporting date filters as RM2. Distinguish recorded response actions from actual messages; accurately separate applicable, non-applicable, pending and breached targets. |
| **RM4 — Agent Performance** | **A: Standard** | Current Agent workload, current assigned-ticket status/priority breakdown, overdue assigned workload, historical *recorded resolution actions correctly attributed to the acting Agent*, and Agent-comparison table/charts. Distinguish live workload from selected-period actions; never credit a current assignee for actions by another Agent or SuperAdmin. |
| **RM5 — Customer Satisfaction** | **A: Standard** | Average current saved 1–5 CSAT, total submitted feedback, positive-rating percentage (4–5 stars), rating distribution, clearly defined feedback participation, recent feedback list with ticket reference/rating/comment/date. Filter feedback by **original submission (CreatedAtUtc)** and display **current saved** rating/comment. Missing feedback never counts as zero stars. |
| **RM6 — Management Dashboard** | **A: Unified** | SuperAdmin KPI cards for ticket volumes/workload, SLA, Agent workload and CSAT; simple charts; shared preset/custom date filter; links to all four reports; manual refresh; responsive design. Reuse report calculations, identify live-snapshot vs period-based measures, and do not implement saved/customizable widgets or separate analytics infrastructure. |

**Approval gate:** RM1–RM6 are complete. Operational definitions in the following sections are **pragmatic implementation proposals**, not additional user approvals. The planner must verify the actual schema and explicitly surface any calculation the database cannot faithfully support.

## 4. Roles, security and reporting data scope (RM1)

| Operation | SuperAdmin | Agent | Customer | Anonymous |
| --- | --- | --- | --- | --- |
| Read ticket reports and trends | Yes, organization-wide | No | No | No |
| Read SLA reports and trends | Yes | No | No | No |
| Read Agent comparisons and resolution activity | Yes | No | No | No |
| Read CSAT aggregates and feedback comments | Yes | No | No | No |
| Read unified management dashboard | Yes | No | No | No |
| Change CRM data through reports | **No new mutations** | No | No | No |

- Protect **every** new report API with the existing **SuperAdmin authorization policy/role**, including data lookups, date presets, trend and feedback endpoints. Anonymous ⇒ existing 401 behavior; authenticated Agent/Customer ⇒ 403. Avoid attaching generic staff (`Agent or SuperAdmin`) permissions by mistake.
- Add SuperAdmin-only navigation, route guard and links to reports. Agents retain their existing operational dashboard and do not receive report navigation or API access; Customers retain only authorized customer views.
- Backend decides report dimensions and valid filters; caller cannot supply an arbitrary SQL expression, role impersonation, or unrestricted user data projection. Do not expose unnecessary customer profile data or sensitive internal notes in the feedback list.
- Return aggregate counts/percentages and limited report rows. For the feedback table, expose only ticket reference, rating, optional comment, created timestamp and minimal context actually necessary to read the report. Display customer-supplied text safely (never untrusted `innerHTML`).
- Recheck permissions on every request; no public report endpoints, report sharing, scheduled email delivery or export/download features required.

## 5. Shared date selection, normalization and truthful labels

- Provide the **same** date-selection controls across ticket reports, SLA, Agent activity, CSAT and management overview: **Last 7 days**, **Last 30 days**, **Last 90 days**, **Custom From/To**. Use a consistent default (suggested: last 30 days).
- Validate custom ranges on the server (valid dates, From ≤ To, and reasonable bounds). Use one well-defined calendar/timezone rule and predictable UTC range boundaries, ideally half-open `[startUtc, endExclusiveUtc)` to avoid double-counting midnight; clearly label the UI dates/timezone. Follow the app's established conventions rather than inventing a complex timezone system.
- **Different report facts have different date anchors:** Ticket/SLA creation cohorts use Ticket.CreatedAtUtc; historical resolution actions use activity `OccurredAtUtc`; submitted CSAT uses `TicketFeedback.CreatedAtUtc` even after edits. **Current** Agent workload and current active at-risk counts are point-in-time snapshots, not historical counts for a past date range.
- Show a label/description wherever the date selector does not affect a live-snapshot KPI. The dashboard cannot imply its entire content is filtered identically. No fabricated historical-as-of views.
- Dates with no data produce zero counts where appropriate and **N/A** rather than `0%` for an undefined rate; trend buckets with no records can show zero. Use integer counts, appropriately rounded percentages and sensible defaults/empty/error states.
- If the current SLA or activity model only supports accurate as-of-now cohort reporting, label it **current outcome for tickets created during the selected period**, not a historical value measured at the end of that past period.

## 6. Ticket Reports (RM2)

- **Volume:** count tickets created in the selected period (`CreatedAtUtc`).
- **Status counts:** current Open, In Progress, Resolved and Closed for that same creation cohort, including zeros for unused enum values; totals should reconcile with volume.
- **Category and priority counts:** current category and priority for that same cohort; totals should also reconcile with volume. A post-creation category/priority change affects current distribution, not the original at-creation category.
- **Trend:** count creation events by date/week bucket over the selected period; choose a simple consistent granularity (daily for short ranges, weekly/monthly for long periods if useful) and label buckets. It is **ticket creation trend**, not status-transition trend.
- **UI:** one Ticket Reports section/page with summary cards, clear charts (bar/donut/line or accessible simple alternatives), date picker/presets, loading/no-data/failure/retry states and responsive layouts. Do not build paginated ticket drill-down or recreate the staff Ticket List (RM2 Option B rejected).
- Query results should not require reading every ticket or entire activity history into memory. Reports should reflect newly created tickets on manual refresh.

## 7. SLA Performance Reports (RM3)

- Use the **existing SLA snapshot and state** and any actually recorded first-response/resolution timestamps or breach fields; do not infer a real customer response from AI drafts, notes, ticket views, internal comments, or automated notifications.
- Provide separately for **response** and **resolution**: applicable target count, met count, breached count, pending count, and non-applicable count (or at least a clear excluded/not-applicable indicator); distinguish active/pending targets from completed outcomes.
- **Recommended explicit rate definition:** `met / (met + breached)` for targets whose outcome can be classified at the time of report; show eligible/outcome denominators and exclude still-pending/unclassified and non-applicable targets. If existing SLA state supports a different accurate definition, the planner must document it and use the same calculation everywhere.
- **Breach counts** must include existing confirmed breaches and/or past-deadline missed targets **as genuinely supported by existing recorded state**. A late first response must not become compliant merely because it was eventually recorded. Avoid double-counting the same target through repeated worker evaluations or repeated transitions.
- **At risk:** count **active, applicable, not-yet-breached** SLA targets approaching their deadlines. Prefer any existing app at-risk semantics; if none exist, a modest documented fixed time window (e.g. due in the next hour) is an acceptable planner-selected default. Do not present already-breached targets as merely at risk.
- **Compliance trend:** simple date-bucketed compliance by *ticket-created cohort*, not an invented historical point-in-time compliance reconstruction. Include or clearly explain each bucket's evaluated denominator, and show N/A for empty/unevaluable buckets.
- The selected period filters by **ticket creation date** for cohort-based SLA reporting. An **active at-risk count** may need a live all-active scope so management can act on current risk; if so, label it **live across all active tickets, not affected by historical date filters**. Do not silently mix scopes.
- Reopened/re-resolved tickets and multiple SLA targets must not generate contradictory counting. Let the actual source-of-truth schema dictate how existing resolution records or snapshots represent the current SLA cycle. If reliable response/completion or breach timing is unavailable, do not synthesize it.
- UI includes response/resolution compliance, breach counts, active-at-risk, non-applicable/pending explanations and trend; no separate SLA policy editor, notification engine or comprehensive breached-ticket table.

## 8. Agent Performance Reports (RM4)

- **Current assigned workload:** count Open/InProgress tickets whose **current** assigned Agent User ID equals each Agent. Clearly label this **live snapshot**, independent of selected historical date filters. Report may show current status breakdown including Resolved/Closed as supplementary workload information if useful, but the headline active workload must remain Open/InProgress only.
- **Priority/status distribution:** grouped counts over currently assigned tickets, labeled as a **current assignment snapshot** (not tickets handled historically by the Agent).
- **Overdue workload:** count currently assigned tickets with **actually breached** applicable SLA target(s), using the same authoritative SLA semantics as RM3 and avoiding duplicate counts per ticket. Clearly state whether counting active Open/InProgress tickets only.
- **Recorded resolution actions:** count `StatusChanged` activity rows transitioning **to Resolved**, with event timestamp in the selected period, where actor corresponds to a real Agent. Attribute the action to the **recorded actor at the time of the transition**, not current assignee. SuperAdmin-performed actions and system events must **not** be credited to an Agent. A ticket resolved twice after reopening has two resolution actions, **not necessarily two uniquely resolved tickets**.
- Do not derive historical action attribution from `Tickets.AssignedAgentUserId` or guess actors from current state. If older activities lack a dependable actor and transition representation, exclude them from attributed action counts and document the limitation. Display unassigned tickets separately if needed; never allocate them to an arbitrary Agent.
- **Comparison table and simple bars:** Agent identity/display name, current active assigned workload, current overdue workload, period's recorded resolution actions; optionally simple current status/priority drill within report components. No Agent score/ranking formula, performance grading or Agent-level personal dashboards.
- The report may include all Agents with 0 counts where reasonable. Keep actual table row count/manageability proportional to training scope, and reuse user/Agent records rather than storing new statistics.

## 9. Customer Satisfaction Reports (RM5)

- Source is Story 12's **one current `TicketFeedback` row per ticket**. Feedback edits update rating/comment and UpdatedAtUtc but retain original `CreatedAtUtc`; **filter the period by original `TicketFeedback.CreatedAtUtc`**, not by the last edit time or Ticket.CreatedAtUtc.
- **Submitted-feedback count**: number of feedback rows first submitted during the selected period.
- **Average CSAT:** arithmetic mean of the **current saved rating** among the in-period feedback rows (1–5); show N/A if no ratings, not 0 stars.
- **Positive-rating percentage:** `count(current rating 4 or 5) / count(submitted feedback rows)` for the selected feedback-date cohort; no feedback does **not** enter the denominator.
- **Rating distribution:** five buckets (1, 2, 3, 4, 5), counts of the **current saved rating** for rows originally submitted in the period; buckets should total the feedback count.
- **Participation — suggested truthful simple cohort definition:** Of **tickets created during the selected period and currently Resolved/Closed**, what percentage have a feedback record? `eligible-created-cohort tickets with feedback / all eligible-created-cohort tickets`. This metric uses a **ticket-created cohort with current eligibility**, intentionally different from the rating metrics anchored to feedback submission date. Label it plainly (e.g. **"Feedback participation among tickets created in period and currently eligible"**) and show N/A if denominator is zero. A reopened ticket with existing feedback is preserved but is *not currently eligible* until it is Resolved/Closed again. The planner may choose an equally simple supportable definition, but must explicitly give its numerator/denominator and avoid mixing incompatible cohorts under an unlabeled percentage.
- **Recent comments table:** limited recent in-period feedback, latest original feedback submission first, showing ticket reference, rating, optional comment and submission timestamp (and an optional link to existing authorized staff Ticket Details). Empty comments should display gracefully. The comment and current rating reflect the most recent saved edit, while the displayed report date remains the first submission date.
- CSAT **does not become Agent-specific** in this story. The current assigned Agent may not be the Agent who served the ticket; do not attribute customer ratings by assignee. No detailed satisfaction breakdown by priority/category/Agent or trend required (RM5 Option B rejected).
- Customer feedback is user-supplied input and must be escaped/used via Angular text rendering, not unsafe raw HTML. No reporting mutation of customer feedback and no automatic customer follow-up.

## 10. Unified Management Dashboard (RM6)

- Provide a distinct **SuperAdmin Management Dashboard**/overview route and navigation entry, with links to **Ticket Reports**, **SLA Performance**, **Agent Performance** and **Customer Satisfaction** report sections.
- Consolidated summary KPI cards should represent the **same** values/calculations as the underlying reports: created ticket volume (period cohort), current open/in-progress and Agent-assigned active workload (live), recorded response/resolution SLA outcomes (defined cohorts), actionable breach/at-risk counts (clearly scoped), average CSAT/feedback count (feedback-submission cohort).
- Add simple, readable charts: ticket creation trend and a small selection of SLA/CSAT or Agent aggregate visualizations **reusing existing report data**. Keep labels, units, `N/A` states and empty charts legible. No hardcoded demonstration numbers in actual report UI.
- Shared last-7/30/90/custom date selection, **manual Refresh**, loading/empty/failure/retry states and desktop/tablet/400px-mobile behavior. Avoid auto-polling, background refresh jobs or caching systems.
- Reuse report query/aggregation logic on the backend rather than copy/paste SQL formulas into dashboard-specific handlers. A small aggregation/orchestration query or frontend composition of existing APIs is acceptable, chosen based on actual code organization and minimal complexity. Avoid generating four separate inconsistent numbers for the same metric.
- Clearly label **Period-based** versus **Current snapshot** cards and the date anchors for SLA/CSAT; one selected time window does not retroactively change current workload. Dashboard must not imply historical snapshots are available.
- Do **not** add rearrangeable widgets, persisted widget settings, per-widget date periods, exports, notifications or personalized report subscriptions.

## 11. Backend/API expectations

- Implement read-only reporting use cases using existing layers and conventions. Proposed logical endpoints (illustrative, inspect actual routing before selecting):
  - `GET /api/v1/reports/tickets`
  - `GET /api/v1/reports/sla`
  - `GET /api/v1/reports/agents`
  - `GET /api/v1/reports/customer-satisfaction`
  - `GET /api/v1/reports/management-overview` (optional if existing four report calls suffice)
- Every reporting action has the existing SuperAdmin server authorization and validated date input. Use typed DTOs with explicit meaning, not raw EF entities or raw activity payloads. Return aggregate report projections, limited recent feedback, and documented metric definitions for client display.
- Group/count in SQL using current EF mapping. Avoid `AsEnumerable`/`ToList` of every Ticket/SLA/Activity/Feedback before aggregating. A small bounded client-side formatting step on already-aggregated rows is fine. No new data tables, reporting migrations, materialized views, queues or event sourcing should be necessary. If inspection proves a small schema addition essential, report the reason for separate review before adding it.
- For chart period buckets, do the simplest EF-translatable aggregation and handle missing/zero buckets clearly. Avoid turning UTC/date conversion into an elaborate new service.
- For historical Agent actions, read the existing activity actor and actual status transition; evaluate whether a reliable `Agent` role can be determined for the actor based on current Identity records/available activity metadata. If an actor could have changed roles since the action and there is no historical role snapshot, do **not** claim exact historical role attribution without qualification. Prefer a clearly stated available-data approximation or surface the limitation if material.
- Keep database reads efficient enough for the existing small training dataset and support manual refresh. No snapshot-write side effects, no call to external AI providers.

## 12. Angular UI and navigation

- Add a **SuperAdmin-only** Reports & Management entry and separate management overview plus four report screens/sections, compatible with existing router/guards/nav layout. Agents/Customers must not see the entry; API protection remains definitive.
- Implement shared date preset/custom range controls with clear labels of reporting-period interpretation and current-snapshot exemptions. Show input validation for malformed/inverted custom date ranges, server error messages where safe and Retry/Refresh actions.
- Charts + summary cards + simple Agent and recent-feedback tables. Use existing Angular Material and, if available, existing charts. A light chart implementation is acceptable; avoid a heavy dependency for basic bars/lines.
- Support both nonempty and empty data; zero vs N/A; readable large counts/percentages, legends, tooltip/labels as appropriate, keyboard/contrast basics and responsive layout around **400px**. Graphs alone should not be the sole readable presentation of critical numbers.
- Dates displayed in a clearly identified convention/timezone; backend handles UTC intervals consistently. Preserve staff operational dashboard and ticket page behavior.

## 13. Testing policy and manual acceptance

- Exactly **one new backend happy-path unit test** and **one new frontend happy-path unit test** following existing xUnit/Vitest conventions. Choose representative high-value metrics/UI logic; do not add an E2E framework, large negative-test suite, integration-test project or automated SQL/performance test suite. Run all existing tests and report final counts.
- Build both backend and frontend. No migration is expected; inspect `has-pending-model-changes` only if helpful. Existing frontend bundle-budget warning may be compared with the Story 12 baseline but is not, alone, a reason to redesign the UI.
- Perform practical **manual API, SQL and browser verification**, including representative known-data aggregates and access controls. Keep manual checks proportionate to five workflows; record unverified steps honestly rather than claiming browser or SQL results were run when they were not.

### Suggested acceptance matrix

1. **Preflight:** PORTAL-001 commit exists, no unexpected application changes; the new reporting pages remain hidden from Agent and Customer.
2. **Authorization:** anonymous reporting API ⇒ 401; Agent/Customer ⇒ 403; SuperAdmin succeeds on each reporting API. Direct URL/forged role cannot bypass.
3. **Ticket volume:** known set of tickets created in the selected period matches API/SQL; outside-window tickets excluded.
4. **Ticket grouping:** status/category/priority totals each reconcile to created-ticket volume; enum buckets including zeros are displayed.
5. **Ticket trends:** date grouping matches known creation timestamps; changing last-7/30/90/custom filters updates values; inverted/invalid dates rejected.
6. **Current versus original fields:** changing a ticket's status/category/priority affects current grouping of its creation cohort, without falsely representing old values.
7. **SLA:** response/resolution denominators clearly classify met, breached, pending and non-applicable; no division by zero; a late recorded response remains a breach; internal recorded response is not called a sent customer message.
8. **SLA at risk/trend:** active overdue targets count as breaches, not at-risk; not-yet-due active targets near deadline are counted according to a documented rule; buckets are creation cohorts, not retroactive historical point-in-time values.
9. **Agent workloads:** current Open/InProgress assignments and current per-status/priority counts match SQL; reassignment changes current workload on refresh, not historical resolution actor.
10. **Agent attribution:** an Agent's recorded transition to Resolved is attributed to that event actor and event timestamp; SuperAdmin actions are not miscredited; repeat resolutions after reopen are labeled actions, not unique permanently resolved tickets.
11. **Agent overdue:** one currently assigned ticket is counted at most once for overdue workload, even if both SLA targets are breached; unassigned tickets are not attributed to an Agent.
12. **CSAT:** feedback count, current mean, positive percentage and 1–5 distribution agree with persisted feedback originally submitted in selected window; absent ratings do not count as zeros.
13. **CSAT edits and participation:** updating a saved rating changes mean/distribution but not original submission-date cohort; participation numerator/denominator are documented and match sampled SQL; zero eligible tickets ⇒ N/A.
14. **Feedback table:** recent rows show correct ticket refs, safe escaped comments, original submission dates and current rating/comment; no raw script executes or staff-private ticket notes leak.
15. **Dashboard consistency:** cards/charts match corresponding report values for same filters; current-snapshot versus period-cohort KPIs visibly distinguished; Refresh issues new reads but no writes.
16. **Browser UI:** all five areas work with loading/empty/error/retry flows, date selection and narrow viewport ~400px; no misleading percentage, missing legend or unreadable chart.
17. **Existing-module smoke:** Agent operational dashboard, staff ticket details/workflow, SLA automation, Customer Portal CSAT, KB access and staff-only/on-demand AI remain functional. Reporting navigation must make zero AI requests.
18. **Non-regression and hygiene:** both builds and existing/new tests pass; no unexpected migration/data mutation, no secrets, no package-heavy analytics platform, no unrelated edits; implementation remains **uncommitted** until independent acceptance.

## 14. Explicitly out of scope

- Agent personal management reports or Customer reporting access; public/anonymous reports.
- Ticket list drill-down/paginated report ticket detail views; report-defined edits, assignments or workflow actions.
- Detailed SLA priority/category/policy breakdowns, historical policy reconstruction, mean handling durations unsupported by data or breached-ticket drill-down.
- Agent scoring, rankings, forecasts, detailed assignment-to-resolution durations, Agent-level SLA comparisons or CSAT attribution to Agents.
- Detailed CSAT breakdowns by Agent/priority/category, newly collected surveys, separate satisfaction questionnaire, CSAT trend as a mandatory feature.
- Customizable/persisted dashboard widgets, personal report layouts, self-service BI, export CSV/PDF/Excel, scheduled reports, report emails, subscriptions or automatic polling.
- New analytics/ETL data stores, warehouse, data mart, materialized aggregation jobs, queue infrastructure, new migrations unless a demonstrated necessity is reviewed.
- Customer conversations, inbound/outbound email, WhatsApp, SMS and live chat (**Communication Channels LAST**); automatic Gemini/report narration or AI calls.
- New Playwright/E2E/performance/integration test suites or extra automated test cases beyond exactly one backend and one frontend happy-path test.

## 15. Planning handoff and delivery gates

- **Story:** `REPORT-001` — Story 13.
- **Feature slug:** `reports-management`.
- **Depends on:** committed Stories **01–12**, most specifically Ticket Management (TICK-001/TICK-002), SLA & Automation (SLA-001), Identity/roles (AUTH-001) and Customer Portal CSAT (PORTAL-001).
- **Intake location:** `.squad/stories/reports-management/REPORT-001/intake.md`.
- **Suggested plan location:** `.squad/plans/reports-management/13-story-reports-management.md`; update `.squad/plans/reports-management/00-overview.md` and `.squad/plans/00-index.md`.
- **Planning sequence:** verify committed baseline → inspect Ticket/SLA/activity/feedback schema, actors, timestamps and SQL conventions → record reliable KPI definitions/date cohorts and gaps → choose shared read-only aggregate queries → define authorized API contracts → implement four reports and reusable dashboard composition → Angular routes/cards/charts/date controls → docs and exactly 1+1 unit tests → manual API/SQL/browser/security checks.
- The planner must give particular attention to **(a) response/resolution denominator correctness, (b) current snapshot versus historical cohort dates, (c) recorded-actor rather than current-assignee resolution counts, (d) CSAT original submission versus current edits, (e) the participation denominator, (f) consistent dashboard reuse.** Resolve implementation details with minimal code, and report material source-data limitations instead of silently inventing historical facts.
- **Plan first:** Squad-Kit planning can create/update `.squad` plan/index files only. No application code changes, database migrations, commits or pushes during planning. Independent plan review precedes implementation.
- **Implementation acceptance:** after approved plan, verify both builds, exactly one added xUnit happy-path and one added Vitest happy-path test, existing tests, representative manual API/SQL/backend authorization and browser checks, then independent acceptance. **No commit/push until accepted.**

## Source and approval note

- Original source: *Customer Support CRM — Core Features* (two pages), Section 9: **Ticket reports; SLA performance; Agent performance; Customer satisfaction; Management dashboards**. The source does not prescribe user roles, metric formulas, date cohorts or charts.
- **Explicit user choices:** RM1–RM6 above. Separate implementation suggestions (e.g. 30-day default, participation cohort definition, at-risk cutoff, proposed endpoints, UTC boundary handling) are **not additional user-approved requirements**. Inspect code before turning them into fixed contracts; surface material ambiguities.
- No current repository code was provided for direct inspection during intake drafting. Treat specific names, available timestamps and tracked activity semantics as **hypotheses to verify**, not as inspected source facts.
