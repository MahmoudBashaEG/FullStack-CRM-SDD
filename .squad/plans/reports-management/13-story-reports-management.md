# Story 13 — Reports & Management — Ticket Reports, SLA Performance, Agent Performance, Customer Satisfaction & Management Dashboard

## Prerequisites

- **Story 12 completed and committed:** [../customer-portal/12-story-customer-portal.md](../customer-portal/12-story-customer-portal.md) (`PORTAL-001`). At planning time `git log` showed `ff6891d feat(portal): … (PORTAL-001)` on top of `master`. The working tree had only the untracked `.squad/stories/reports-management/` and `.squad/plans/reports-management/` folders. That confirms the intake §2 claim: **the PORTAL-001 commit exists.** **Run `git status` and `git log --oneline -3` before implementing. Stop and report if any application file is modified or if `PORTAL-001` is not in `git log`.** Do not depend on the exact commit id.
- **Story 12 data:** `TicketFeedback` (`backend/src/Crm.Domain/Tickets/TicketFeedback.cs` lines 5–46) has one row per ticket (unique `UX_TicketFeedback_TicketId`, `TicketFeedbackConfiguration.cs` line 23). `Rating` is 1–5 (check constraint, line 13). `CreatedAtUtc` is set once at first submission (`Create`, line 30). `Update` changes only `Rating`, `Comment` and `UpdatedAtUtc` (lines 34–46). The plan overview of Story 12 states: "**Reports & Management** reads `TicketFeedback` for CSAT metrics. It must add its own staff read model."
- **Stories 04–05 completed:** [../ticket-management/04-story-ticket-creation-management.md](../ticket-management/04-story-ticket-creation-management.md), [../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md).
  - `Ticket.CreatedAtUtc` never changes after `Ticket.Create` (`Ticket.cs` lines 52–79).
  - `Status`, `Category` and `Priority` are **current values only** (`UpdateDetails` lines 82–121, `TransitionTo` lines 203–224). They are stored by name (`TicketConfiguration.cs` lines 20–22).
  - `TicketActivity` is append-only (`TicketActivity.cs` lines 3–8). `StatusChanged` stores `OldValue`/`NewValue` as status names and **always has an actor** (`StatusChanged(…, Guid actorUserId, …)`, lines 34–35). `ActorUserId == null` only for automation events (auto-assign, auto-escalate: lines 43–47). Automation never changes a status.
- **Story 09 completed:** [../sla-automation/09-story-sla-basic-automation.md](../sla-automation/09-story-sla-basic-automation.md).
  - The SLA snapshot columns are on `Ticket` (`Ticket.cs` lines 32–44). Tracked ⇔ `SlaFirstResponseDueAtUtc != null` (line 44).
  - `FirstRespondedAtUtc` is the manual **Mark as Responded** record. It is set once (lines 166–184) and sends nothing.
  - `SlaResolvedAtUtc` is the **first** transition to Resolved only. It is never cleared by resume or reopen (lines 41–42, 212–215, S10).
  - **The one state rule** is `SlaEvaluator.Target` (`backend/src/Crm.Application/Sla/SlaEvaluator.cs` lines 46–64): completed ⇒ `c <= due ? Met : Breached`; not completed ⇒ `now > due ? Breached : Pending`. It uses instants only and is calendar-independent.
  - **Warning** (the app's existing "at risk") needs a pending target, a business calendar, and an enabled `SlaWarningDue` rule whose own lead is reached (lines 59–61, F1).
  - The SQL twin of the breach rule is `TicketRepository.IsBreached` (`backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs` lines 86–91). It drives the ticket list `slaBreached` filter (lines 55–62).
- **Story 06 completed:** [../agent-dashboard/06-story-agent-dashboard-ticket-overview-work-queue.md](../agent-dashboard/06-story-agent-dashboard-ticket-overview-work-queue.md). It is the aggregation precedent:
  - `AgentDashboardRepository.GetWorkloadCountsAsync` uses `GroupBy(_ => 1)` with conditional `Count` (lines 11–20).
  - The read model is a separate port (`IAgentDashboardRepository`).
  - The page has a Refresh / Retry pipeline (`agent-dashboard-page.ts` lines 59–113).
  - **The Agent Dashboard is not changed.**
- **Story 02 completed:** [../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md).
  - `AuthorizationPolicies.SuperAdmin` (`AuthorizationPolicies.cs` line 5) is registered as `RequireRole(UserRoles.SuperAdmin)` (`SecurityRegistration.cs` line 63). The fallback is authenticated-only (line 62).
  - Roles are assigned **only at account creation** (`IdentityService.cs` line 78; bootstrap `IdentityBootstrapper.cs` line 73). The code has no `RemoveFromRoleAsync`. An account's current role therefore equals its role at the time of any past action (R5).
- Applied migrations end with `AddTicketFeedback`. **This story adds no migration, no table, no column and no index.**
- Tooling: .NET SDK 10, EF Core 10 (SQL Server), Mediator (source-generated), FluentValidation, xUnit 2.9.3 (`Crm.UnitTests.csproj` references `Crm.Application` only), Angular / Material 22.2, Vitest via `ng test`.
  - Baseline tests at `ff6891d`: **backend 12, frontend 12** (12 `[Fact]` under `backend/tests`, 12 `it(` under `frontend/src`).
  - **No chart library is installed** (`frontend/package.json`). **No new NuGet or npm package.**
- Intake: `.squad/stories/reports-management/REPORT-001/intake.md` (`attachments/` is empty). Business decisions **RM1–RM6 are approved and not reopened.**
- Shared contracts touched (additive only):
  - Application: a new `Crm.Application/Reports/` folder, a new port `IReportingRepository`, and `ReportRules`.
  - Infrastructure:
    - a new `ReportingRepository` (+1 DI line);
    - `TicketRepository.IsBreached` changes from `private` to `internal` (no behaviour change).
  - API: a new `ReportsController` under `/api/v1/reports`, plus `Contracts/Reports/ReportContracts.cs`.
  - Angular:
    - `app.routes.ts` (+1 route) and `NAV_ITEMS` (+1 SuperAdmin entry);
    - a new `features/reports/` folder.
  - Docs.
- **No change** to: domain entities, EF configurations, `CrmDbContext`, migrations, `SlaEvaluator`, `ISlaMonitoringRepository`, `IAgentDashboardRepository`, the SLA worker, automation, portal, AI, KB, the ticket APIs, or any existing test.

---

## Story Goal

Deliver all five **Reports & Management** areas for **SuperAdmin only** (RM1). Every report is real, read-only and SQL-aggregated:

1. **Ticket Reports (RM2):**
   - `GET /api/v1/reports/tickets` returns the number of tickets **created** in the period.
   - It also returns their **current** status, category and priority distributions. Every enum member is listed, including zeros, and each distribution reconciles with the volume.
   - It returns a creation trend (daily buckets for ≤ 31 days, otherwise 7-day buckets).
2. **SLA Performance (RM3):** `GET /api/v1/reports/sla` returns, for the tickets created in the period, per target (first response, resolution):
   - Tracked tickets are classified as `Met`, `Breached` or `Pending`, using exactly the `SlaEvaluator` state rule. Untracked tickets are `NotApplicable`.
   - **Compliance = met / (met + breached)**, or `null` (N/A) when nothing has been decided yet.
   - A cohort compliance trend.
   - A **live** block, labelled as not affected by the date filter:
     - active tickets with a breached target;
     - at-risk response and resolution targets, using the existing SLA **Warning** semantics.
3. **Agent Performance (RM4):** `GET /api/v1/reports/agents` returns one row per Agent account:
   - **live** current assignment: Open, In Progress, Active, active priority mix, supplementary Resolved/Closed;
   - **live** overdue active tickets (≥ 1 breached target, counted once per ticket);
   - **period** recorded resolution actions: `StatusChanged → Resolved` activity in the period, credited to the **recorded actor**. The row also gives the distinct tickets.
   - Totals include unassigned active tickets and the resolution actions by non-Agent actors (not credited).
4. **Customer Satisfaction (RM5):** `GET /api/v1/reports/customer-satisfaction` covers feedback **first submitted** in the period (`TicketFeedback.CreatedAtUtc`), using the **current** rating and comment:
   - count, mean rating, positive percentage (4–5) and a 1–5 distribution;
   - participation: tickets created in the period and currently Resolved/Closed that have feedback ÷ all such tickets;
   - the 20 most recent rows, with ticket reference and subject, rating, comment and submission date.
5. **Management Dashboard (RM6):** an Angular page `/reports` composes the **same four endpoints** with the same period.
   - KPI cards are labelled **Period** or **Live snapshot**.
   - Simple bar charts.
   - Links to the four reports keep the selected period.
   - Manual Refresh only.
6. All five screens share one period control: **Last 7 / 30 / 90 days / Custom From–To**, default **Last 30 days**. Periods are UTC calendar days; the server resolves and validates them.
   - The UI handles loading, empty, error and Retry states.
   - Rates read `N/A` instead of `0%`. Every number is printed as text next to its bar. Pages work at 400 px.
7. Exactly **one** new backend unit test and **one** new frontend unit test, a new `docs/reports-and-management.md`, small doc updates, and a manual API/SQL/browser matrix mapped to intake §13 checks 1–18.

**Not in scope** (do **not** build):
- Agent or Customer report access.
- Drill-down ticket lists.
- Exports, scheduled or e-mailed reports, polling, caching.
- Saved, custom or per-widget filters.
- Per-category or per-priority SLA breakdowns.
- Agent scores or rankings, or CSAT per Agent.
- A CSAT trend.
- New tables, indexes, migrations or materialized aggregates.
- Any AI or Gemini call.
- Any write of any kind.
- Communication Channels (stays last).

### Conflicts and decisions (surface these in the implementation report)

| # | Intake point | Resolution in this plan |
|---|---|---|
| R1 | §7: "Prefer any existing app at-risk semantics; otherwise a fixed window." | **Existing semantics are reused.** "At risk" means `SlaTargetView.IsWarning`: a pending target, a configured business calendar, and an enabled `SlaWarningDue` rule matching the ticket whose own lead is reached (`SlaEvaluator.cs` lines 59–61). This is the same flag as the ticket badges, the `/sla` overview and the worker alerts. It is evaluated with `SlaEvaluator.CreateContextAsync` + `ISlaMonitoringRepository.ListActiveTrackedAsync(SlaRules.OverviewScanLimit)` (500, `SlaRules.cs` line 16), exactly as `GetSlaOverviewQueryHandler` does (`GetSlaOverviewQuery.cs` lines 17–22). The response carries `scannedActiveTickets`, `scanLimitReached`, `enabledWarningRules` and `calendarConfigured`, so the UI can say **why** at risk is 0: "No SLA warning rules are enabled" or "No business calendar is configured". Breached targets are never at risk (`IsWarning` requires `Pending`). **No new fixed window is invented.** |
| R2 | §7: accurate met/breached/pending/non-applicable; no double counting; reopened tickets. | For each cohort ticket and target, one SQL `CASE` reproduces `SlaEvaluator.Target` exactly:<br>• `due IS NULL` ⇒ `NotApplicable` (pre-SLA ticket, or SLA disabled at creation);<br>• `completed IS NOT NULL` ⇒ `completed <= due ? Met : Breached`;<br>• otherwise `due < @now ? Breached : Pending`.<br>Every ticket lands in **exactly one** outcome per target, so the four counts always sum to the cohort size. **Resolution uses `SlaResolvedAtUtc` = the first resolution only** (S10). A reopened or re-resolved ticket keeps its first-resolution outcome and is never counted twice. Worker executions and alerts are **not** read, so repeated evaluations cannot inflate breaches. A late recorded response stays `Breached` (`completed > due`). |
| R3 | §7: distinguish recorded response actions from actual messages. | Labels everywhere say **"Recorded first response (Mark as Responded)"**. The docs and the UI hint add: "A recorded response is an internal acknowledgement, not a message sent to the customer." No AI draft, note, view or alert is read. |
| R4 | §7: response target of a ticket resolved without a recorded response. | **Code reality:** `RecordFirstResponse` is allowed only on Open/In Progress (`Ticket.cs` line 169). A ticket resolved without Mark as Responded therefore keeps `FirstRespondedAtUtc = NULL`. `SlaEvaluator` reports it `Pending` until the due instant passes, then `Breached`. The report uses the **same** rule, so it matches the ticket's SLA card. Documented as "no response was recorded before the deadline". Not reclassified. |
| R5 | §8/§11: attribute resolution actions to the recorded actor; historical role. | Count `TicketActivities` with `Type = StatusChanged`, `NewValue = 'Resolved'`, `OccurredAtUtc` in the period and `ActorUserId IS NOT NULL`, grouped by `ActorUserId`, with actions and `COUNT(DISTINCT TicketId)`. Credit a group to an Agent row **only if the actor is currently an Agent account**. **Roles never change after creation** (Prerequisites, Story 02), so the current role is the role at action time: exact, not approximate. SuperAdmin or other actors go to `resolutionActionsByOthers` (shown, never credited). `Tickets.AssignedAgentUserId` is **never** read for this metric. Legacy tickets without activity rows contribute nothing (documented limitation). |
| R6 | §8: overdue workload; once per ticket; same SLA semantics. | Overdue = `Status IN (Open, InProgress)` AND `TicketRepository.IsBreached(now)` (≥ 1 target completed late or open past due), grouped by the current `AssignedAgentUserId`. It is a `WHERE` filter on `Tickets`, so a ticket with both targets breached counts **once**. This is the **same predicate** as the staff ticket list `slaBreached=true`, so `/tickets?status=…&slaBreached=true` as SuperAdmin cross-checks it. `IsBreached` becomes `internal static` (`TicketRepository.cs` line 87), the same as `Scope` (line 83). |
| R7 | §9: participation denominator. | **Participation = (tickets created in the period, currently Resolved or Closed, that have a feedback row) ÷ (tickets created in the period, currently Resolved or Closed).** `null` (N/A) when the denominator is 0. The feedback may have been submitted at any time. This is a **ticket-created cohort with current eligibility**, deliberately different from the rating metrics (feedback-submission cohort). The label is: "Feedback participation among tickets created in the period and currently Resolved/Closed". A reopened ticket with feedback is **not** in the denominator until it is Resolved/Closed again (matches `TicketFeedback.IsEligible`, line 25). |
| R8 | §5: one calendar/timezone rule, half-open ranges, labels. | **UTC calendar days.**<br>• `Last N days` = the N UTC days ending **today (UTC) inclusive**.<br>• `Custom` = `from..to` inclusive, from 2000-01-01, at most 366 days, `to` at most today (UTC) + 1 (tolerates clients ahead of UTC).<br>• SQL uses `[from 00:00Z, to+1 00:00Z)`.<br>• Buckets: daily when ≤ 31 days, else consecutive 7-day buckets **starting at `from`** (the last one may be shorter). No week-start convention.<br>• Day grouping uses `DATEPART(year/month/day)` of the stored `datetimeoffset`. Every write uses `TimeProvider.GetUtcNow()` (offset +00:00), so this is the UTC day.<br>The UI prints the resolved range with "(UTC)" and shows timestamps in UTC. |
| R9 | §10/§11: dashboard reuses report calculations; avoid inconsistent numbers. | **No `management-overview` endpoint.** The Angular dashboard calls the four report endpoints with the identical period (`forkJoin`, per-section error capture). Every dashboard number is therefore **the same response object** that the report page renders. No formula is duplicated anywhere. Each section shows its own `generatedAtUtc`. Live values can differ by seconds between sections, which is documented. |
| R10 | §11: live total open/in-progress for the dashboard. | The Ticket report is a creation cohort, so it cannot show the live total. The **Agent report totals** carry live `openTickets` / `inProgressTickets` over **all** tickets (assigned or not), computed from the same workload aggregation. The dashboard "Live: Open / In Progress" card reads them. |
| R11 | §11: SQL aggregation; no new tables or indexes. | Every metric is a grouped SQL statement. Only already-aggregated cells (≤ distinct day × dimension combinations) or the bounded `ListActiveTrackedAsync` (≤ 500, existing) are materialized. **No index is added.** `TicketFeedback.CreatedAtUtc` and `TicketActivities (Type, OccurredAtUtc)` have none, which is acceptable for the training dataset. A follow-up is noted in the docs. Cohort queries use the existing `IX (CreatedAtUtc, Id)` (`TicketConfiguration.cs` line 45). |
| R12 | §12: charts. | No chart package. A tiny presentational `ReportBars` component draws horizontal CSS bars, with `aria-hidden` bars and the **number always printed as text**. It is used for the distributions, trends and Agent workload. Trends render as one row per bucket (≤ 31 daily or ≤ 53 weekly rows), which is readable at 400 px. |
| R13 | §4: SuperAdmin on every endpoint; validated input; no arbitrary dimensions. | `ReportsController` has `[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]` at **class level** (precedent `SlaController.cs` line 17). The only inputs are `preset`, `from` and `to`. All dimensions are fixed in code. `ReportPeriodRules` validates the period. It is the **first validator with a constructor dependency** (`TimeProvider`). Validators are resolved from DI (`ValidationBehavior.cs` line 6, `Application/DependencyInjection.cs` lines 11–12), so that is supported. |
| R14 | §7 / `/sla` overview consistency. | The live "active tickets with a breached target" count is SQL over **all** active tracked tickets. The `/sla` overview `breachedCount` instead covers at most 500 rows and excludes active tickets whose **both** targets are already completed (`SlaMonitoringRepository.cs` line 78; e.g. reopened after an on-time first resolution with a late response). The two numbers can differ in those cases. Documented, not "fixed". |

### Recorded decisions

| Concern | Decision |
|---|---|
| Naming | Application `Crm.Application.Reports`: use cases under `Crm.Application/Reports/<UseCase>/`, shared types in `Crm.Application/Reports/`. Port `IReportingRepository` in `Crm.Application.Abstractions.Persistence`. Infrastructure `ReportingRepository` in `Persistence/Repositories/`. API `Crm.Api.Contracts.Reports`, `ReportsController`. Angular `frontend/src/app/features/reports/`. |
| CQRS shape | Queries only: `GetTicketReportQuery`, `GetSlaReportQuery`, `GetAgentPerformanceReportQuery`, `GetCustomerSatisfactionReportQuery`. Each implements `IReportPeriodQuery (Preset, From, To)`. No `IUnitOfWork`, no command, no tracking (`AsNoTracking` everywhere). |
| Routes | `GET /api/v1/reports/tickets`, `/sla`, `/agents`, `/customer-satisfaction`, all with `?preset=Last7Days|Last30Days|Last90Days|Custom&from=yyyy-MM-dd&to=yyyy-MM-dd`. `from`/`to` are ignored unless `preset=Custom`. Angular: `/reports` (dashboard), `/reports/tickets`, `/reports/sla`, `/reports/agents`, `/reports/customer-satisfaction`. The period travels as the same query parameters. |
| Limits (`ReportRules`) | `MaxRangeDays` 366 · `DailyBucketMaxDays` 31 · `WeeklyBucketDays` 7 · `RecentFeedbackLimit` 20 · `EarliestDate` 2000-01-01. At-risk scan reuses `SlaRules.OverviewScanLimit` (500). |
| Rounding | Percentages are `double?` 0–100, rounded to 1 decimal on the server (`ReportMath.Percent`). The mean rating is `double?` rounded to 2 decimals. Counts are `int`. `null` ⇒ the UI prints "N/A". |
| Errors | Invalid period ⇒ 400 `ValidationProblemDetails` with keys `preset`/`from`/`to` (camel-cased by `GlobalExceptionHandler.cs` line 25). Unknown preset name or malformed date ⇒ 400 from model binding (`[ApiController]`). Anonymous ⇒ 401. Agent/Customer ⇒ 403. |
| Text safety | Feedback comments and ticket subjects render with interpolation and `.user-text` (`productivity-page.scss` lines 50–54). **Never** `[innerHTML]`. The CSAT list exposes no customer name, email, assignee or internal note. |
| UI placement | One nav entry "Reports" (`insights`, `/reports`, `exact: false`, `roles: ['SuperAdmin']`) after "SLA & Automation". Agent/Customer navigation is unchanged. No Home card. |

---

## Context — Read These Files First

1. `.squad/stories/reports-management/REPORT-001/intake.md` — the contract: §3 (RM1–RM6), §4 role table, §5 date rules, §6–§10 the five areas, §11 API, §12 Angular, §13 the 1+1 test policy and the 18-step acceptance matrix, §14 out of scope.
2. [../customer-portal/12-story-customer-portal.md](../customer-portal/12-story-customer-portal.md) — tone, the "Conflicts" / "Recorded decisions" tables, and the Verification and Done Criteria style.
3. `backend/src/Crm.Domain/Tickets/Ticket.cs`:
   - lines 16–44: fields, SLA snapshot, `IsSlaTracked`;
   - lines 166–184: `RecordFirstResponse`, Open/InProgress only, once;
   - lines 203–224: `TransitionTo` and the first-resolution-only `SlaResolvedAtUtc`.

   `TicketActivity.cs` lines 3–23 and 34–35 (`StatusChanged` names, actor always set). `TicketActivityType.cs` lines 4–14. `TicketStatus.cs`, `TicketCategory.cs`, `TicketPriority.cs` (declaration order = display order). `TicketWorkflow.cs` lines 6–14 (reopen `Closed → Open`, resume `Resolved → InProgress`). `TicketFeedback.cs` lines 10–46.
4. `backend/src/Crm.Application/Sla/SlaEvaluator.cs` lines 13–18 (`CreateContextAsync`), 20–34 (`Evaluate`), 46–64 (**the state rule to mirror in SQL**, the warning rule). `SlaViews.cs` lines 7–27 (`TicketSlaInput`, `SlaTargetView.IsWarning`, `SlaEvaluationContext.WarningRules` / `Calendar`).
5. `backend/src/Crm.Application/Sla/GetSlaOverview/GetSlaOverviewQuery.cs` lines 15–35 (context + bounded scan + evaluate: **the at-risk template**). `backend/src/Crm.Application/Abstractions/Persistence/ISlaMonitoringRepository.cs` lines 18–19 and 29. `backend/src/Crm.Infrastructure/Persistence/Repositories/SlaMonitoringRepository.cs` lines 72–113 (`ListActiveTrackedAsync`, R14 filter line 78). `backend/src/Crm.Application/Common/Validation/SlaRules.cs` lines 15–16.
6. `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs` lines 12, 55–62 (`slaBreached` filter), 83–84 (`internal static Scope`), 86–91 (`IsBreached`, **made `internal`**).
7. `backend/src/Crm.Infrastructure/Persistence/Repositories/AgentDashboardRepository.cs` lines 11–20 (`GroupBy(_ => 1)` + conditional `Count`) and 77–80. `backend/src/Crm.Application/AgentDashboard/AgentDashboardSnapshot.cs` (record + rules-class style). `backend/src/Crm.Application/AgentDashboard/GetAgentDashboard/GetAgentDashboardQuery.cs` (handler with `TimeProvider`, `GeneratedAtUtc`).
8. `backend/src/Crm.Infrastructure/Identity/IdentityService.cs` lines 22–30 (role subquery) and 82–88 (`ListByRoleAsync`: **the Agent-role SQL filter to copy**), line 78 (role assigned at creation only). `backend/src/Crm.Infrastructure/Identity/ApplicationUser.cs` lines 6–12 (`FirstName`, `LastName`, `IsActive`). `backend/src/Crm.Domain/Identity/UserRoles.cs`.
9. `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` lines 22–42 (`Tickets`, `TicketActivities`, `TicketFeedback`, Identity `Users`/`UserRoles`/`Roles` from `IdentityDbContext`). `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketConfiguration.cs` lines 20–22 (enums by name), 45 (creation index). `TicketActivityConfiguration.cs` lines 16, 27. `TicketFeedbackConfiguration.cs` lines 13–23.
10. `backend/src/Crm.Infrastructure/DependencyInjection.cs` lines 41–60 (repository registrations; add after line 60). `backend/src/Crm.Application/DependencyInjection.cs` lines 11–14 (validators from DI, `TimeProvider`, `SlaEvaluator` registration).
11. `backend/src/Crm.Api/Controllers/SlaController.cs` lines 15–18 and 56–59 (class-level SuperAdmin policy, `EndpointDescription` style). `AgentDashboardController.cs` lines 12–20. `backend/src/Crm.Api/Contracts/AgentDashboard/AgentDashboardResponse.cs` (response records with `static From`). `backend/src/Crm.Api/DependencyInjection.cs` lines 16–19 (enums as names, `allowIntegerValues: false`, lowercase URLs). `backend/src/Crm.Api/Contracts/Sla/SlaContracts.cs` line 21 (`DateOnly` already used in contracts).
12. `backend/src/Crm.Application/Common/Behaviors/ValidationBehavior.cs` lines 6–27. `backend/src/Crm.Api/Errors/GlobalExceptionHandler.cs` lines 23–31. `backend/src/Crm.Application/Tickets/ListTickets/ListTicketsValidator.cs` (rule style, `IsInEnum`).
13. `backend/tests/Crm.UnitTests/AgentDashboard/GetAgentDashboardQueryHandlerTests.cs` (59 lines) — **the template for the single new backend test** (fake repository records its inputs, `FixedTimeProvider` lines 55–58). `backend/tests/Crm.UnitTests/Sla/TicketCreationAutomationTests.cs` lines 46 and 54–55 (fakes for `ISlaConfigurationRepository` / `IAutomationRuleRepository`). `backend/src/Crm.Application/Abstractions/Persistence/ISlaConfigurationRepository.cs` and `IAutomationRuleRepository.cs` (members to stub).
14. `frontend/src/app/app.routes.ts` lines 80–85 (insert after the `portal` route, before the comment on line 85). `frontend/src/app/core/layout/navigation.ts` lines 13–30 (insert after line 28). `frontend/src/app/core/auth/auth-guards.ts` lines 14–19 (`roleGuard`).
15. `frontend/src/app/features/agent-dashboard/agent-dashboard-page.ts` lines 59–113 (`Subject` + `switchMap` + per-request `catchError`, never stale numbers after failure, `refresh()`). `agent-dashboard-page.html` lines 1–60 (toolbar, progress bar, error + Retry, summary cards). `agent-dashboard-page.scss` lines 19–34 (`.summary-grid`, `.count`).
16. `frontend/src/app/shared/ui/productivity-page.scss` (`.toolbar`, `.rows`, `.row`, `.row-title`, `.meta`, `.user-text`, `.hint`, `.error`). `frontend/src/app/shared/ui/page-header/page-header.ts`, `shared/ui/status-badge/status-badge.ts`. `frontend/angular.json` production budgets (`anyComponentStyle` 4 kB warning / 8 kB error).
17. `frontend/src/app/features/tickets/ticket-models.ts` lines 5–12 (`TICKET_*` constants and types), 132–145 (`statusLabel`). `frontend/src/app/core/api/api-url.ts` (`versioned`), `core/api/describe-http-error.ts`. `frontend/src/app/features/tickets/tickets-api.ts` lines 24–49 (`HttpParams`, empty values omitted).
18. `frontend/src/app/features/portal/portal-timeline.spec.ts` and `features/knowledge-base/markdown.spec.ts` (pure-function Vitest specs, no TestBed) — **the pattern for the single new frontend test.**
19. Docs:
   - `docs/identity-and-access.md` line 41 (last matrix row) and line 90 (`/api/health` row; insert before it);
   - `docs/sla-and-automation.md` lines 42 and 46 (Warning and state definitions);
   - `docs/agent-dashboard.md` line 70;
   - `docs/customer-portal.md` lines 15 and 74;
   - `README.md` line 3.

---

## Product rules (from story)

### Role matrix (server-enforced)

| Operation | SuperAdmin | Agent | Customer | Anonymous |
|---|---|---|---|---|
| `GET /api/v1/reports/tickets` | ✔ organisation-wide | 403 | 403 | 401 |
| `GET /api/v1/reports/sla` | ✔ | 403 | 403 | 401 |
| `GET /api/v1/reports/agents` | ✔ | 403 | 403 | 401 |
| `GET /api/v1/reports/customer-satisfaction` | ✔ | 403 | 403 | 401 |
| Angular `/reports/**` + nav entry | ✔ | hidden, redirected to `/` | hidden, redirected to `/` | sign-in |
| Any write through reports | none exists | — | — | — |

### Metric definitions (the single source; repeat them verbatim in `docs/reports-and-management.md`)

| Metric | Scope / anchor | Definition |
|---|---|---|
| Created tickets | Period · `Tickets.CreatedAtUtc` | Count of tickets with `CreatedAtUtc ∈ [start, endExclusive)` |
| Status / category / priority | Period cohort, **current** value | Grouped counts of the same cohort; every enum member is listed (zeros included); each sums to Created tickets |
| Creation trend | Period · `CreatedAtUtc` | Created tickets per daily or 7-day bucket; empty buckets = 0 |
| Response / resolution outcome | Period cohort, current outcome | Per target, exactly one of `NotApplicable` (untracked), `Met`, `Breached`, `Pending` (R2) |
| Compliance | Period cohort | `met / (met + breached)`, N/A when 0. Pending and NotApplicable are excluded and shown separately |
| SLA compliance trend | Period cohort per creation bucket | Same formula per bucket; N/A when the bucket has no decided target |
| Active tickets with a breached target | **Live**, all active tickets | `Status ∈ {Open, InProgress}` AND `IsBreached(now)` |
| At-risk response / resolution targets | **Live**, active tracked tickets (≤ 500 scanned) | `SlaTargetView.IsWarning` (R1) |
| Agent Open / In Progress / Active (= Open + In Progress) | **Live** | Tickets whose **current** `AssignedAgentUserId` = the Agent |
| Agent active priority mix | **Live** | Active assigned tickets grouped by current priority |
| Agent Resolved / Closed (supplementary) | **Live** | Current assignment with that status; **not** "handled historically" |
| Agent overdue | **Live** | Active assigned tickets with `IsBreached(now)`, once per ticket (R6) |
| Unassigned active | **Live** | `AssignedAgentUserId IS NULL` AND Open/InProgress |
| Recorded resolution actions | Period · `TicketActivities.OccurredAtUtc` | `StatusChanged` to `Resolved` by that Agent as recorded actor; plus distinct tickets (R5) |
| Resolution actions by others | Period | Same events whose actor is not an Agent account (e.g. SuperAdmin); never credited |
| Feedback received | Period · `TicketFeedback.CreatedAtUtc` | Rows first submitted in the period |
| Average rating | same cohort, **current** rating | Arithmetic mean, 2 decimals; N/A when 0 rows. Missing feedback is never 0 stars |
| Positive rating % | same cohort | `count(rating ≥ 4) / feedback received`, N/A when 0 |
| Rating distribution | same cohort | Counts for 1..5 (zeros included), sum = feedback received |
| Feedback participation | Ticket-created cohort, current eligibility | R7 |
| Recent feedback | same cohort as Feedback received | Newest first submission first, at most 20, current rating and comment, submission date |

---

## Backend Tasks

### 1 — Application: rules, period, shared types

**Create file: `backend/src/Crm.Application/Common/Validation/ReportRules.cs`** (style of `SlaRules.cs`):

```csharp
namespace Crm.Application.Common.Validation;

public static class ReportRules
{
    public const int MaxRangeDays = 366;
    public const int DailyBucketMaxDays = 31;   // longer periods use 7-day buckets starting at the period start
    public const int WeeklyBucketDays = 7;
    public const int RecentFeedbackLimit = 20;
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);
}
```

**Create file: `backend/src/Crm.Application/Reports/ReportPeriod.cs`**

```csharp
using Crm.Application.Common.Validation;

namespace Crm.Application.Reports;

/// <summary>REPORT-001 shared date selection. Stored by name in query strings ("Last30Days").</summary>
public enum ReportPreset
{
    Last7Days = 0,
    Last30Days = 1,
    Last90Days = 2,
    Custom = 3,
}

/// <summary>Every report query carries the same three inputs (validated by ReportPeriodRules).</summary>
public interface IReportPeriodQuery
{
    ReportPreset Preset { get; }
    DateOnly? From { get; }
    DateOnly? To { get; }
}

/// <summary>
/// R8: UTC calendar days, From..To inclusive, SQL range [StartUtc, EndExclusiveUtc). "Last N days" ends today (UTC) inclusive.
/// BucketDays = 1 for periods of at most 31 days, else 7 (consecutive buckets starting at From; the last one may be shorter).
/// </summary>
public sealed record ReportPeriod(
    ReportPreset Preset, DateOnly From, DateOnly To, DateTimeOffset StartUtc, DateTimeOffset EndExclusiveUtc, int BucketDays)
{
    public int Days => To.DayNumber - From.DayNumber + 1;

    public static DateOnly TodayUtc(DateTimeOffset now) => DateOnly.FromDateTime(now.UtcDateTime);

    public static ReportPeriod Resolve(ReportPreset preset, DateOnly? from, DateOnly? to, DateOnly todayUtc)
    {
        var (start, end) = preset switch
        {
            ReportPreset.Last7Days => (todayUtc.AddDays(-6), todayUtc),
            ReportPreset.Last30Days => (todayUtc.AddDays(-29), todayUtc),
            ReportPreset.Last90Days => (todayUtc.AddDays(-89), todayUtc),
            _ => (from!.Value, to!.Value), // Custom: the validator requires both
        };
        var days = end.DayNumber - start.DayNumber + 1;
        return new ReportPeriod(
            preset, start, end, Utc(start), Utc(end.AddDays(1)),
            days <= ReportRules.DailyBucketMaxDays ? 1 : ReportRules.WeeklyBucketDays);
    }

    private static DateTimeOffset Utc(DateOnly day) => new(day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
}
```

**Create file: `backend/src/Crm.Application/Reports/ReportPeriodRules.cs`**

```csharp
using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Reports;

internal static class ReportPeriodRules
{
    /// <summary>
    /// The one period validation, added by every report validator. Keys: preset, from, to. from/to are ignored unless Custom.
    /// "to" may be today (UTC) + 1 so a client ahead of UTC can pick its local today.
    /// </summary>
    public static void AddReportPeriodRules<T>(this AbstractValidator<T> validator, TimeProvider timeProvider)
        where T : IReportPeriodQuery
    {
        validator.RuleFor(q => q.Preset).IsInEnum();
        validator.When(q => q.Preset == ReportPreset.Custom, () =>
        {
            validator.RuleFor(q => q.From)
                .NotNull().WithMessage("Choose a start date for a custom range.")
                .GreaterThanOrEqualTo(ReportRules.EarliestDate).WithMessage("The start date must be on or after 2000-01-01.");
            validator.RuleFor(q => q.To)
                .NotNull().WithMessage("Choose an end date for a custom range.")
                .Must(to => to is null || to.Value <= ReportPeriod.TodayUtc(timeProvider.GetUtcNow()).AddDays(1))
                .WithMessage("The end date cannot be in the future.")
                .Must((q, to) => q.From is null || to is null || q.From.Value <= to.Value)
                .WithMessage("The start date must be on or before the end date.")
                .Must((q, to) => q.From is null || to is null || to.Value.DayNumber - q.From.Value.DayNumber + 1 <= ReportRules.MaxRangeDays)
                .WithMessage($"A custom range can cover at most {ReportRules.MaxRangeDays} days.");
        });
    }
}
```

**Create file: `backend/src/Crm.Application/Reports/ReportMath.cs`**

```csharp
namespace Crm.Application.Reports;

/// <summary>Pure helpers shared by every report, so the same metric is computed one way.</summary>
internal static class ReportMath
{
    /// <summary>0–100 rounded to 1 decimal; null (N/A) when the denominator is 0 — never 0 %.</summary>
    public static double? Percent(int numerator, int denominator) =>
        denominator == 0 ? null : Math.Round(100.0 * numerator / denominator, 1);

    /// <summary>Every enum member in declaration order, zeros included (RM2 "including zeros").</summary>
    public static IReadOnlyList<ReportCount<TEnum>> CountByMember<TCell, TEnum>(
        IEnumerable<TCell> cells, Func<TCell, TEnum> key, Func<TCell, int> count)
        where TEnum : struct, Enum
    {
        var totals = cells.GroupBy(key).ToDictionary(g => g.Key, g => g.Sum(count));
        return Enum.GetValues<TEnum>().Select(v => new ReportCount<TEnum>(v, totals.GetValueOrDefault(v))).ToList();
    }

    /// <summary>Bucket start/end pairs covering the whole period (R8).</summary>
    public static IReadOnlyList<(DateOnly Start, DateOnly End)> Buckets(ReportPeriod period)
    {
        var buckets = new List<(DateOnly, DateOnly)>();
        for (var start = period.From; start <= period.To; start = start.AddDays(period.BucketDays))
        {
            var end = start.AddDays(period.BucketDays - 1);
            buckets.Add((start, end > period.To ? period.To : end));
        }

        return buckets;
    }

    /// <summary>-1 when the day lies outside the period (defensive; SQL filters by the same range).</summary>
    public static int BucketIndex(ReportPeriod period, DateOnly day) =>
        day < period.From || day > period.To ? -1 : (day.DayNumber - period.From.DayNumber) / period.BucketDays;
}
```

### 2 — Application: read models and port

**Create file: `backend/src/Crm.Application/Reports/ReportDetails.cs`**

```csharp
using Crm.Domain.Tickets;

namespace Crm.Application.Reports;

public sealed record ReportCount<TKey>(TKey Value, int Count);

public sealed record ReportTrendPoint(DateOnly Start, DateOnly End, int Count);

// ---- Raw aggregated cells returned by IReportingRepository (already grouped in SQL) ----

public sealed record TicketCohortCell(DateOnly Day, TicketStatus Status, TicketCategory Category, TicketPriority Priority, int Count);

/// <summary>R2: one outcome per tracked target; NotApplicable = ticket not SLA-tracked.</summary>
public enum ReportSlaOutcome
{
    NotApplicable = 0,
    Met = 1,
    Breached = 2,
    Pending = 3,
}

public sealed record SlaCohortCell(DateOnly Day, ReportSlaOutcome FirstResponse, ReportSlaOutcome Resolution, int Count);

public sealed record ReportAgent(Guid UserId, string Name, bool IsActive);

/// <summary>All tickets grouped by CURRENT assignee (null = unassigned), status and priority.</summary>
public sealed record WorkloadCell(Guid? AgentUserId, TicketStatus Status, TicketPriority Priority, int Count);

public sealed record AssigneeCount(Guid? AgentUserId, int Count);

public sealed record ResolutionActionCount(Guid ActorUserId, int Actions, int DistinctTickets);

public sealed record RatingCount(int Rating, int Count);

public sealed record FeedbackParticipationCounts(int EligibleTickets, int TicketsWithFeedback);

/// <summary>R13 minimal context: no customer, assignee or internal data.</summary>
public sealed record RecentFeedback(
    Guid TicketId, string TicketSubject, int Rating, string? Comment, DateTimeOffset SubmittedAtUtc, DateTimeOffset UpdatedAtUtc);

// ---- Report results ----

public sealed record TicketReport(
    ReportPeriod Period,
    int CreatedTickets,
    IReadOnlyList<ReportCount<TicketStatus>> ByStatus,
    IReadOnlyList<ReportCount<TicketCategory>> ByCategory,
    IReadOnlyList<ReportCount<TicketPriority>> ByPriority,
    IReadOnlyList<ReportTrendPoint> Trend,
    DateTimeOffset GeneratedAtUtc);

/// <summary>CohortTickets = NotApplicable + Met + Breached + Pending. Applicable = CohortTickets − NotApplicable. CompliancePercent = Met / (Met + Breached).</summary>
public sealed record SlaTargetSummary(int CohortTickets, int NotApplicable, int Applicable, int Met, int Breached, int Pending, double? CompliancePercent);

public sealed record SlaTrendBucket(
    DateOnly Start, DateOnly End,
    int ResponseMet, int ResponseBreached, double? ResponseCompliancePercent,
    int ResolutionMet, int ResolutionBreached, double? ResolutionCompliancePercent);

/// <summary>Live, not affected by the period. At-risk = SLA Warning over at most ScannedActiveTickets (R1).</summary>
public sealed record SlaLiveStatus(
    int ActiveTicketsWithBreach, int ResponseAtRisk, int ResolutionAtRisk,
    int ScannedActiveTickets, bool ScanLimitReached, int EnabledWarningRules, bool CalendarConfigured);

public sealed record SlaReport(
    ReportPeriod Period, SlaTargetSummary FirstResponse, SlaTargetSummary Resolution, IReadOnlyList<SlaTrendBucket> Trend,
    SlaLiveStatus Live, DateTimeOffset GeneratedAtUtc);

public sealed record AgentPerformanceRow(
    Guid AgentUserId, string Name, bool IsActive,
    int Open, int InProgress, int ActiveAssigned, int OverdueActive, int Resolved, int Closed,
    IReadOnlyList<ReportCount<TicketPriority>> ActiveByPriority,
    int ResolutionActions, int ResolvedTicketsDistinct);

/// <summary>OpenTickets/InProgressTickets cover ALL tickets (assigned or not) — the live totals used by the dashboard (R10).</summary>
public sealed record AgentPerformanceTotals(
    int OpenTickets, int InProgressTickets, int ActiveAssignedToAgents, int OverdueActiveAssignedToAgents,
    int UnassignedActive, int ResolutionActionsByAgents, int ResolutionActionsByOthers);

public sealed record AgentPerformanceReport(
    ReportPeriod Period, IReadOnlyList<AgentPerformanceRow> Agents, AgentPerformanceTotals Totals, DateTimeOffset GeneratedAtUtc);

public sealed record FeedbackParticipation(int EligibleTickets, int TicketsWithFeedback, double? Percent);

public sealed record CustomerSatisfactionReport(
    ReportPeriod Period, int FeedbackCount, double? AverageRating, int PositiveCount, double? PositivePercent,
    IReadOnlyList<ReportCount<int>> Distribution, FeedbackParticipation Participation,
    IReadOnlyList<RecentFeedback> Recent, int RecentLimit, DateTimeOffset GeneratedAtUtc);
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IReportingRepository.cs`**

```csharp
using Crm.Application.Reports;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// REPORT-001 read-only aggregates (SuperAdmin, organisation-wide; no scope). Every method is one grouped SQL statement
/// (participation: two counts) over AsNoTracking queries; nothing is written. Ranges are half-open [startUtc, endExclusiveUtc).
/// </summary>
public interface IReportingRepository
{
    /// <summary>Tickets created in range, grouped by UTC creation day and CURRENT status, category, priority.</summary>
    Task<IReadOnlyList<TicketCohortCell>> ListTicketCohortAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default);

    /// <summary>Tickets created in range, grouped by UTC creation day and the SlaEvaluator outcome of each target at "now" (R2).</summary>
    Task<IReadOnlyList<SlaCohortCell>> ListSlaCohortAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Open/InProgress tickets with at least one breached target (TicketRepository.IsBreached). Live.</summary>
    Task<int> CountActiveBreachedTicketsAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Accounts in the Agent role (active and inactive), ordered LastName, FirstName, Id.</summary>
    Task<IReadOnlyList<ReportAgent>> ListAgentsAsync(CancellationToken cancellationToken = default);

    /// <summary>All tickets grouped by current assignee (null = unassigned), status, priority. Live.</summary>
    Task<IReadOnlyList<WorkloadCell>> ListWorkloadAsync(CancellationToken cancellationToken = default);

    /// <summary>Open/InProgress tickets with IsBreached(now), grouped by current assignee — once per ticket (R6). Live.</summary>
    Task<IReadOnlyList<AssigneeCount>> ListOverdueActiveByAssigneeAsync(DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>StatusChanged → Resolved events in range grouped by recorded actor (R5). Assignment is never read.</summary>
    Task<IReadOnlyList<ResolutionActionCount>> ListResolutionActionsAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default);

    /// <summary>Feedback first submitted in range (TicketFeedback.CreatedAtUtc) grouped by CURRENT rating.</summary>
    Task<IReadOnlyList<RatingCount>> ListFeedbackRatingsAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default);

    /// <summary>R7: tickets created in range and currently Resolved/Closed; how many have a feedback row (any submission date).</summary>
    Task<FeedbackParticipationCounts> GetFeedbackParticipationAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default);

    /// <summary>Feedback first submitted in range, newest CreatedAtUtc first (Id tie-breaker), at most limit.</summary>
    Task<IReadOnlyList<RecentFeedback>> ListRecentFeedbackAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, int limit, CancellationToken cancellationToken = default);
}
```

A **new** port, not methods on `ITicketRepository` or `IAgentDashboardRepository`. Adding members there would break the hand-written fakes in existing tests (`CreateTicketCommandHandlerTests`, `GetAgentDashboardQueryHandlerTests`, …). This follows the Story 12 P4 precedent.

### 3 — Application: Ticket report

**Create file: `backend/src/Crm.Application/Reports/GetTicketReport/GetTicketReportQuery.cs`** (query, handler, validator in one file, like `ListKnowledgeArticlesQuery.cs`):

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.Reports.GetTicketReport;

public sealed record GetTicketReportQuery(ReportPreset Preset, DateOnly? From, DateOnly? To) : IQuery<TicketReport>, IReportPeriodQuery;

public sealed class GetTicketReportQueryHandler(IReportingRepository reporting, TimeProvider timeProvider)
    : IQueryHandler<GetTicketReportQuery, TicketReport>
{
    public async ValueTask<TicketReport> Handle(GetTicketReportQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var period = ReportPeriod.Resolve(query.Preset, query.From, query.To, ReportPeriod.TodayUtc(now));
        var cells = await reporting.ListTicketCohortAsync(period.StartUtc, period.EndExclusiveUtc, cancellationToken);

        // One statement ⇒ volume, each distribution and the trend are sums of the same cells and always reconcile.
        var buckets = ReportMath.Buckets(period);
        var trend = new int[buckets.Count];
        foreach (var cell in cells)
        {
            var index = ReportMath.BucketIndex(period, cell.Day);
            if (index >= 0)
            {
                trend[index] += cell.Count;
            }
        }

        return new TicketReport(
            period,
            cells.Sum(c => c.Count),
            ReportMath.CountByMember(cells, c => c.Status, c => c.Count),
            ReportMath.CountByMember(cells, c => c.Category, c => c.Count),
            ReportMath.CountByMember(cells, c => c.Priority, c => c.Count),
            buckets.Select((b, i) => new ReportTrendPoint(b.Start, b.End, trend[i])).ToList(),
            now);
    }
}

public sealed class GetTicketReportValidator : AbstractValidator<GetTicketReportQuery>
{
    public GetTicketReportValidator(TimeProvider timeProvider) => this.AddReportPeriodRules(timeProvider);
}
```

### 4 — Application: SLA report

**Create file: `backend/src/Crm.Application/Reports/GetSlaReport/GetSlaReportQuery.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Crm.Application.Sla;
using FluentValidation;
using Mediator;

namespace Crm.Application.Reports.GetSlaReport;

public sealed record GetSlaReportQuery(ReportPreset Preset, DateOnly? From, DateOnly? To) : IQuery<SlaReport>, IReportPeriodQuery;

/// <summary>
/// Cohort outcomes (R2) for tickets created in the period, plus a LIVE block (R1): breached active tickets over all active tickets
/// and at-risk targets via the shared SlaEvaluator over the same bounded scan as the /sla overview. Read-only.
/// </summary>
public sealed class GetSlaReportQueryHandler(
    IReportingRepository reporting,
    ISlaMonitoringRepository monitoring,
    SlaEvaluator evaluator,
    TimeProvider timeProvider) : IQueryHandler<GetSlaReportQuery, SlaReport>
{
    public async ValueTask<SlaReport> Handle(GetSlaReportQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var period = ReportPeriod.Resolve(query.Preset, query.From, query.To, ReportPeriod.TodayUtc(now));
        var cells = await reporting.ListSlaCohortAsync(period.StartUtc, period.EndExclusiveUtc, now, cancellationToken);

        var buckets = ReportMath.Buckets(period);
        var trend = buckets.Select(b => (b.Start, b.End, Cells: new List<SlaCohortCell>())).ToList();
        foreach (var cell in cells)
        {
            var index = ReportMath.BucketIndex(period, cell.Day);
            if (index >= 0)
            {
                trend[index].Cells.Add(cell);
            }
        }

        var context = await evaluator.CreateContextAsync(cancellationToken);
        var active = await monitoring.ListActiveTrackedAsync(SlaRules.OverviewScanLimit, cancellationToken);
        var views = active.Select(r => SlaEvaluator.Evaluate(r.Sla, context)).ToList();
        var live = new SlaLiveStatus(
            await reporting.CountActiveBreachedTicketsAsync(now, cancellationToken),
            views.Count(v => v.FirstResponse.IsWarning),
            views.Count(v => v.Resolution.IsWarning),
            active.Count,
            active.Count >= SlaRules.OverviewScanLimit,
            context.WarningRules.Count,
            context.Calendar is not null);

        return new SlaReport(
            period,
            Summarize(cells, c => c.FirstResponse),
            Summarize(cells, c => c.Resolution),
            trend.Select(b =>
            {
                var (responseMet, responseBreached) = Decided(b.Cells, c => c.FirstResponse);
                var (resolutionMet, resolutionBreached) = Decided(b.Cells, c => c.Resolution);
                return new SlaTrendBucket(
                    b.Start, b.End,
                    responseMet, responseBreached, ReportMath.Percent(responseMet, responseMet + responseBreached),
                    resolutionMet, resolutionBreached, ReportMath.Percent(resolutionMet, resolutionMet + resolutionBreached));
            }).ToList(),
            live,
            now);
    }

    private static SlaTargetSummary Summarize(IReadOnlyList<SlaCohortCell> cells, Func<SlaCohortCell, ReportSlaOutcome> outcome)
    {
        int Count(ReportSlaOutcome o) => cells.Where(c => outcome(c) == o).Sum(c => c.Count);
        var total = cells.Sum(c => c.Count);
        var notApplicable = Count(ReportSlaOutcome.NotApplicable);
        var met = Count(ReportSlaOutcome.Met);
        var breached = Count(ReportSlaOutcome.Breached);
        return new SlaTargetSummary(
            total, notApplicable, total - notApplicable, met, breached, Count(ReportSlaOutcome.Pending), ReportMath.Percent(met, met + breached));
    }

    private static (int Met, int Breached) Decided(IEnumerable<SlaCohortCell> cells, Func<SlaCohortCell, ReportSlaOutcome> outcome)
    {
        var list = cells.ToList();
        return (list.Where(c => outcome(c) == ReportSlaOutcome.Met).Sum(c => c.Count),
                list.Where(c => outcome(c) == ReportSlaOutcome.Breached).Sum(c => c.Count));
    }
}

public sealed class GetSlaReportValidator : AbstractValidator<GetSlaReportQuery>
{
    public GetSlaReportValidator(TimeProvider timeProvider) => this.AddReportPeriodRules(timeProvider);
}
```

`SlaEvaluator` is already registered scoped (`Application/DependencyInjection.cs` line 14). `ISlaMonitoringRepository` is unchanged.

### 5 — Application: Agent performance report

**Create file: `backend/src/Crm.Application/Reports/GetAgentPerformanceReport/GetAgentPerformanceReportQuery.cs`**

```csharp
public sealed record GetAgentPerformanceReportQuery(ReportPreset Preset, DateOnly? From, DateOnly? To)
    : IQuery<AgentPerformanceReport>, IReportPeriodQuery;

public sealed class GetAgentPerformanceReportQueryHandler(IReportingRepository reporting, TimeProvider timeProvider)
    : IQueryHandler<GetAgentPerformanceReportQuery, AgentPerformanceReport>
{
    public async ValueTask<AgentPerformanceReport> Handle(GetAgentPerformanceReportQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var period = ReportPeriod.Resolve(query.Preset, query.From, query.To, ReportPeriod.TodayUtc(now));

        var agents = await reporting.ListAgentsAsync(cancellationToken);
        var workload = await reporting.ListWorkloadAsync(cancellationToken);
        var overdue = (await reporting.ListOverdueActiveByAssigneeAsync(now, cancellationToken))
            .ToDictionary(o => o.AgentUserId ?? Guid.Empty, o => o.Count);
        var actions = (await reporting.ListResolutionActionsAsync(period.StartUtc, period.EndExclusiveUtc, cancellationToken))
            .ToDictionary(a => a.ActorUserId);

        static bool IsActive(TicketStatus s) => s is TicketStatus.Open or TicketStatus.InProgress;

        var rows = agents.Select(agent =>
        {
            var mine = workload.Where(c => c.AgentUserId == agent.UserId).ToList();
            int Status(TicketStatus s) => mine.Where(c => c.Status == s).Sum(c => c.Count);
            var open = Status(TicketStatus.Open);
            var inProgress = Status(TicketStatus.InProgress);
            var recorded = actions.GetValueOrDefault(agent.UserId);
            return new AgentPerformanceRow(
                agent.UserId, agent.Name, agent.IsActive,
                open, inProgress, open + inProgress, overdue.GetValueOrDefault(agent.UserId),
                Status(TicketStatus.Resolved), Status(TicketStatus.Closed),
                ReportMath.CountByMember(mine.Where(c => IsActive(c.Status)), c => c.Priority, c => c.Count),
                recorded?.Actions ?? 0, recorded?.DistinctTickets ?? 0);
        }).ToList();

        var agentIds = agents.Select(a => a.UserId).ToHashSet();
        var totals = new AgentPerformanceTotals(
            workload.Where(c => c.Status == TicketStatus.Open).Sum(c => c.Count),
            workload.Where(c => c.Status == TicketStatus.InProgress).Sum(c => c.Count),
            rows.Sum(r => r.ActiveAssigned),
            rows.Sum(r => r.OverdueActive),
            workload.Where(c => c.AgentUserId is null && IsActive(c.Status)).Sum(c => c.Count),
            actions.Values.Where(a => agentIds.Contains(a.ActorUserId)).Sum(a => a.Actions),
            actions.Values.Where(a => !agentIds.Contains(a.ActorUserId)).Sum(a => a.Actions)); // SuperAdmin etc.: shown, never credited (R5)

        return new AgentPerformanceReport(period, rows, totals, now);
    }
}

public sealed class GetAgentPerformanceReportValidator : AbstractValidator<GetAgentPerformanceReportQuery>
{
    public GetAgentPerformanceReportValidator(TimeProvider timeProvider) => this.AddReportPeriodRules(timeProvider);
}
```

`Guid.Empty` stands in for the null key only inside the dictionary; Agent ids are never empty. Rows keep the `ListAgentsAsync` order (name). **No ranking, sorting by numbers or score.**

### 6 — Application: Customer satisfaction report

**Create file: `backend/src/Crm.Application/Reports/GetCustomerSatisfactionReport/GetCustomerSatisfactionReportQuery.cs`**

```csharp
public sealed record GetCustomerSatisfactionReportQuery(ReportPreset Preset, DateOnly? From, DateOnly? To)
    : IQuery<CustomerSatisfactionReport>, IReportPeriodQuery;

public sealed class GetCustomerSatisfactionReportQueryHandler(IReportingRepository reporting, TimeProvider timeProvider)
    : IQueryHandler<GetCustomerSatisfactionReportQuery, CustomerSatisfactionReport>
{
    public async ValueTask<CustomerSatisfactionReport> Handle(GetCustomerSatisfactionReportQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var period = ReportPeriod.Resolve(query.Preset, query.From, query.To, ReportPeriod.TodayUtc(now));

        var ratings = await reporting.ListFeedbackRatingsAsync(period.StartUtc, period.EndExclusiveUtc, cancellationToken);
        var count = ratings.Sum(r => r.Count);
        var positive = ratings.Where(r => r.Rating >= 4).Sum(r => r.Count);
        double? average = count == 0 ? null : Math.Round((double)ratings.Sum(r => r.Rating * r.Count) / count, 2);
        var distribution = Enumerable.Range(TicketFeedback.MinRating, TicketFeedback.MaxRating)
            .Select(r => new ReportCount<int>(r, ratings.Where(x => x.Rating == r).Sum(x => x.Count)))
            .ToList();

        var participation = await reporting.GetFeedbackParticipationAsync(period.StartUtc, period.EndExclusiveUtc, cancellationToken);
        var recent = await reporting.ListRecentFeedbackAsync(
            period.StartUtc, period.EndExclusiveUtc, ReportRules.RecentFeedbackLimit, cancellationToken);

        return new CustomerSatisfactionReport(
            period, count, average, positive, ReportMath.Percent(positive, count), distribution,
            new FeedbackParticipation(
                participation.EligibleTickets, participation.TicketsWithFeedback,
                ReportMath.Percent(participation.TicketsWithFeedback, participation.EligibleTickets)),
            recent, ReportRules.RecentFeedbackLimit, now);
    }
}

public sealed class GetCustomerSatisfactionReportValidator : AbstractValidator<GetCustomerSatisfactionReportQuery>
{
    public GetCustomerSatisfactionReportValidator(TimeProvider timeProvider) => this.AddReportPeriodRules(timeProvider);
}
```

(`Enumerable.Range(1, 5)` = 1..5.) The mean is computed from the SQL-grouped `(rating, count)` cells (≤ 5 rows), so it is exact and needs no extra statement.

No `Application/DependencyInjection.cs` change: handlers and validators are discovered (line 11 plus the source-generated Mediator).

### 7 — Infrastructure: `ReportingRepository`

**File: `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs`** line 87: change `private static Expression<Func<Ticket, bool>> IsBreached(DateTimeOffset now)` to **`internal static`**. Change nothing else. Extend the comment on line 86 with "Shared with ReportingRepository (REPORT-001 R6)."

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/ReportingRepository.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Reports;
using Crm.Domain.Identity;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

/// <summary>REPORT-001: read-only, organisation-wide aggregates. Grouped in SQL; only aggregated cells are materialized.</summary>
internal sealed class ReportingRepository(CrmDbContext dbContext) : IReportingRepository
{
    public async Task<IReadOnlyList<TicketCohortCell>> ListTicketCohortAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default)
    {
        // DATEPART on the stored datetimeoffset: every write uses TimeProvider.GetUtcNow() (offset +00:00) ⇒ UTC day (R8).
        var rows = await CreatedIn(startUtc, endExclusiveUtc)
            .GroupBy(t => new { t.CreatedAtUtc.Year, t.CreatedAtUtc.Month, t.CreatedAtUtc.Day, t.Status, t.Category, t.Priority })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Status, g.Key.Category, g.Key.Priority, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new TicketCohortCell(new DateOnly(r.Year, r.Month, r.Day), r.Status, r.Category, r.Priority, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<SlaCohortCell>> ListSlaCohortAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // R2: the SlaEvaluator.Target rule as CASE expressions (completed ⇒ completed <= due ? Met : Breached;
        // else due < now ? Breached : Pending; untracked ⇒ NotApplicable). One outcome per target per ticket.
        var rows = await CreatedIn(startUtc, endExclusiveUtc)
            .Select(t => new
            {
                t.CreatedAtUtc.Year,
                t.CreatedAtUtc.Month,
                t.CreatedAtUtc.Day,
                First = t.SlaFirstResponseDueAtUtc == null ? ReportSlaOutcome.NotApplicable
                    : t.FirstRespondedAtUtc != null
                        ? (t.FirstRespondedAtUtc <= t.SlaFirstResponseDueAtUtc ? ReportSlaOutcome.Met : ReportSlaOutcome.Breached)
                        : (t.SlaFirstResponseDueAtUtc < now ? ReportSlaOutcome.Breached : ReportSlaOutcome.Pending),
                Resolution = t.SlaResolutionDueAtUtc == null ? ReportSlaOutcome.NotApplicable
                    : t.SlaResolvedAtUtc != null
                        ? (t.SlaResolvedAtUtc <= t.SlaResolutionDueAtUtc ? ReportSlaOutcome.Met : ReportSlaOutcome.Breached)
                        : (t.SlaResolutionDueAtUtc < now ? ReportSlaOutcome.Breached : ReportSlaOutcome.Pending),
            })
            .GroupBy(x => new { x.Year, x.Month, x.Day, x.First, x.Resolution })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.First, g.Key.Resolution, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new SlaCohortCell(new DateOnly(r.Year, r.Month, r.Day), r.First, r.Resolution, r.Count)).ToList();
    }

    public Task<int> CountActiveBreachedTicketsAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Active().Where(TicketRepository.IsBreached(now)).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ReportAgent>> ListAgentsAsync(CancellationToken cancellationToken = default) =>
        // Same role filter as IdentityService.ListByRoleAsync; order on the entity before projecting the record.
        await (from u in dbContext.Users.AsNoTracking()
               where dbContext.UserRoles.Any(ur => ur.UserId == u.Id
                   && dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == UserRoles.Agent))
               orderby u.LastName, u.FirstName, u.Id
               select new ReportAgent(u.Id, u.FirstName + " " + u.LastName, u.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkloadCell>> ListWorkloadAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Tickets.AsNoTracking()
            .GroupBy(t => new { t.AssignedAgentUserId, t.Status, t.Priority })
            .Select(g => new WorkloadCell(g.Key.AssignedAgentUserId, g.Key.Status, g.Key.Priority, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssigneeCount>> ListOverdueActiveByAssigneeAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        // A WHERE on Tickets ⇒ a ticket with both targets breached is counted once (R6).
        await Active().Where(TicketRepository.IsBreached(now))
            .GroupBy(t => t.AssignedAgentUserId)
            .Select(g => new AssigneeCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ResolutionActionCount>> ListResolutionActionsAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default)
    {
        var resolved = nameof(TicketStatus.Resolved); // StatusChanged stores the target status name (TicketActivity.StatusChanged)
        return await dbContext.TicketActivities.AsNoTracking()
            .Where(a => a.Type == TicketActivityType.StatusChanged && a.NewValue == resolved
                && a.OccurredAtUtc >= startUtc && a.OccurredAtUtc < endExclusiveUtc && a.ActorUserId != null)
            .GroupBy(a => a.ActorUserId!.Value)
            .Select(g => new ResolutionActionCount(g.Key, g.Count(), g.Select(a => a.TicketId).Distinct().Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RatingCount>> ListFeedbackRatingsAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default) =>
        await SubmittedIn(startUtc, endExclusiveUtc)
            .GroupBy(f => f.Rating)
            .Select(g => new RatingCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<FeedbackParticipationCounts> GetFeedbackParticipationAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default)
    {
        var eligible = CreatedIn(startUtc, endExclusiveUtc)
            .Where(t => t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed);
        var total = await eligible.CountAsync(cancellationToken);
        var withFeedback = await eligible.Where(t => dbContext.TicketFeedback.Any(f => f.TicketId == t.Id)).CountAsync(cancellationToken);
        return new FeedbackParticipationCounts(total, withFeedback);
    }

    public async Task<IReadOnlyList<RecentFeedback>> ListRecentFeedbackAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, int limit, CancellationToken cancellationToken = default) =>
        await (from f in SubmittedIn(startUtc, endExclusiveUtc)
               join t in dbContext.Tickets.AsNoTracking() on f.TicketId equals t.Id
               orderby f.CreatedAtUtc descending, f.Id descending
               select new RecentFeedback(t.Id, t.Subject, f.Rating, f.Comment, f.CreatedAtUtc, f.UpdatedAtUtc))
            .Take(limit)
            .ToListAsync(cancellationToken);

    private IQueryable<Ticket> CreatedIn(DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc) =>
        dbContext.Tickets.AsNoTracking().Where(t => t.CreatedAtUtc >= startUtc && t.CreatedAtUtc < endExclusiveUtc);

    private IQueryable<Ticket> Active() =>
        dbContext.Tickets.AsNoTracking().Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress);

    private IQueryable<TicketFeedback> SubmittedIn(DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc) =>
        dbContext.TicketFeedback.AsNoTracking().Where(f => f.CreatedAtUtc >= startUtc && f.CreatedAtUtc < endExclusiveUtc);
}
```

**Translation notes for the executor:**
- If EF rejects grouping on the `ReportSlaOutcome` CASE projection in `ListSlaCohortAsync`, replace the enum constants with `int` codes (`0..3`) in the anonymous projection and cast to `ReportSlaOutcome` when building `SlaCohortCell`. If EF still cannot translate the `GroupBy` over the projection, materialize **only** the per-ticket projection `(Year, Month, Day, First, Resolution)` and group in memory. That is one small row per cohort ticket. **Report that fallback in the implementation report.** Do not load entities.
- If `ListResolutionActionsAsync` fails to translate `g.Select(a => a.TicketId).Distinct().Count()`, run a second grouped statement over `.Select(a => new { Actor = a.ActorUserId!.Value, a.TicketId }).Distinct().GroupBy(x => x.Actor)`.
- Verify the generated SQL once with `LogTo` / the console logger in Development: no `SELECT` of full ticket or activity rows.

**File: `backend/src/Crm.Infrastructure/DependencyInjection.cs`**: after line 60 (`IHelpCenterRepository`) add:

```csharp
        services.AddScoped<IReportingRepository, ReportingRepository>();
```

### 8 — API: contracts and controller

**Create file: `backend/src/Crm.Api/Contracts/Reports/ReportContracts.cs`.** Response records mirror the Application records 1:1, each with a `static From`, in the style of `AgentDashboardResponse.cs`:

```csharp
using Crm.Application.Reports;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Reports;

public sealed record ReportPeriodResponse(ReportPreset Preset, DateOnly From, DateOnly To, DateTimeOffset StartUtc, DateTimeOffset EndExclusiveUtc, int BucketDays, int Days)
{
    public static ReportPeriodResponse From(ReportPeriod p) => new(p.Preset, p.From, p.To, p.StartUtc, p.EndExclusiveUtc, p.BucketDays, p.Days);
}

public sealed record ReportCountResponse<TKey>(TKey Value, int Count);

public sealed record ReportTrendPointResponse(DateOnly Start, DateOnly End, int Count);

public sealed record TicketReportResponse(
    ReportPeriodResponse Period, int CreatedTickets,
    IReadOnlyList<ReportCountResponse<TicketStatus>> ByStatus,
    IReadOnlyList<ReportCountResponse<TicketCategory>> ByCategory,
    IReadOnlyList<ReportCountResponse<TicketPriority>> ByPriority,
    IReadOnlyList<ReportTrendPointResponse> Trend, DateTimeOffset GeneratedAtUtc)
{
    public static TicketReportResponse From(TicketReport r) => new(
        ReportPeriodResponse.From(r.Period), r.CreatedTickets,
        r.ByStatus.Select(c => new ReportCountResponse<TicketStatus>(c.Value, c.Count)).ToList(),
        r.ByCategory.Select(c => new ReportCountResponse<TicketCategory>(c.Value, c.Count)).ToList(),
        r.ByPriority.Select(c => new ReportCountResponse<TicketPriority>(c.Value, c.Count)).ToList(),
        r.Trend.Select(t => new ReportTrendPointResponse(t.Start, t.End, t.Count)).ToList(), r.GeneratedAtUtc);
}

// Same pattern for:
//  SlaTargetSummaryResponse(cohortTickets, notApplicable, applicable, met, breached, pending, compliancePercent)
//  SlaTrendBucketResponse(start, end, responseMet, responseBreached, responseCompliancePercent, resolutionMet, resolutionBreached, resolutionCompliancePercent)
//  SlaLiveStatusResponse(activeTicketsWithBreach, responseAtRisk, resolutionAtRisk, scannedActiveTickets, scanLimitReached, enabledWarningRules, calendarConfigured)
//  SlaReportResponse(period, firstResponse, resolution, trend, live, generatedAtUtc)
//  AgentPerformanceRowResponse(agentUserId, name, isActive, open, inProgress, activeAssigned, overdueActive, resolved, closed,
//      activeByPriority: ReportCountResponse<TicketPriority>[], resolutionActions, resolvedTicketsDistinct)
//  AgentPerformanceTotalsResponse(openTickets, inProgressTickets, activeAssignedToAgents, overdueActiveAssignedToAgents,
//      unassignedActive, resolutionActionsByAgents, resolutionActionsByOthers)
//  AgentPerformanceReportResponse(period, agents, totals, generatedAtUtc)
//  FeedbackParticipationResponse(eligibleTickets, ticketsWithFeedback, percent)
//  RecentFeedbackResponse(ticketId, ticketSubject, rating, comment, submittedAtUtc, updatedAtUtc)
//  CustomerSatisfactionReportResponse(period, feedbackCount, averageRating, positiveCount, positivePercent,
//      distribution: ReportCountResponse<int>[], participation, recent, recentLimit, generatedAtUtc)
```

Write every record out in full. No `// Same pattern` comment remains in the committed file.

**Create file: `backend/src/Crm.Api/Controllers/ReportsController.cs`**

```csharp
using Crm.Api.Authentication;
using Crm.Api.Contracts.Reports;
using Crm.Application.Reports;
using Crm.Application.Reports.GetAgentPerformanceReport;
using Crm.Application.Reports.GetCustomerSatisfactionReport;
using Crm.Application.Reports.GetSlaReport;
using Crm.Application.Reports.GetTicketReport;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/reports")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    private const string Period = "Requires role: SuperAdmin (Agent and Customer: 403). Read-only. preset = Last7Days | Last30Days (default) | Last90Days | Custom; from/to (yyyy-MM-dd, UTC calendar days, inclusive) are required for Custom and ignored otherwise; at most 366 days, from >= 2000-01-01, to <= today (UTC) + 1. Percentages are 0-100 with 1 decimal, null = N/A (no denominator). Buckets: daily up to 31 days, else 7-day buckets starting at from.";

    [EndpointDescription(Period + " Tickets CREATED in the period: volume, CURRENT status/category/priority of that cohort (every value, zeros included; each sums to createdTickets) and the creation trend.")]
    [HttpGet("tickets")]
    public async Task<ActionResult<TicketReportResponse>> Tickets(
        [FromQuery] ReportPreset preset = ReportPreset.Last30Days, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(TicketReportResponse.From(await sender.Send(new GetTicketReportQuery(preset, from, to), cancellationToken)));

    [EndpointDescription(Period + " For tickets created in the period, per target: notApplicable (not SLA-tracked), met, breached (late or past due without a recorded completion), pending; compliance = met / (met + breached). First response = recorded 'Mark as Responded' (internal, not a sent message); resolution = first transition to Resolved. live: NOT affected by the period — active tickets with a breached target, and at-risk targets (SLA Warning rules) over at most 500 active tracked tickets.")]
    [HttpGet("sla")]
    public async Task<ActionResult<SlaReportResponse>> Sla(
        [FromQuery] ReportPreset preset = ReportPreset.Last30Days, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(SlaReportResponse.From(await sender.Send(new GetSlaReportQuery(preset, from, to), cancellationToken)));

    [EndpointDescription(Period + " One row per Agent account. LIVE (not affected by the period): current assignment by status and active priority, overdue = Open/In Progress with at least one breached SLA target (once per ticket). PERIOD: resolutionActions = recorded transitions to Resolved performed by that Agent (event actor and time; never the current assignee); a ticket resolved twice counts twice, resolvedTicketsDistinct once. Actions by SuperAdmin or other accounts are totalled separately and not credited.")]
    [HttpGet("agents")]
    public async Task<ActionResult<AgentPerformanceReportResponse>> Agents(
        [FromQuery] ReportPreset preset = ReportPreset.Last30Days, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(AgentPerformanceReportResponse.From(await sender.Send(new GetAgentPerformanceReportQuery(preset, from, to), cancellationToken)));

    [EndpointDescription(Period + " Feedback FIRST SUBMITTED in the period (current rating/comment after edits): count, average (2 decimals), positive (4-5) percentage, 1-5 distribution, the 20 most recent rows. participation = tickets created in the period and currently Resolved/Closed that have feedback / all such tickets. Missing feedback is never a zero rating.")]
    [HttpGet("customer-satisfaction")]
    public async Task<ActionResult<CustomerSatisfactionReportResponse>> CustomerSatisfaction(
        [FromQuery] ReportPreset preset = ReportPreset.Last30Days, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(CustomerSatisfactionReportResponse.From(await sender.Send(new GetCustomerSatisfactionReportQuery(preset, from, to), cancellationToken)));
}
```

**No `[AllowAnonymous]`.** No POST/PUT/DELETE. No `ETag` (read-only aggregates).

### 9 — Backend unit test (the single new one)

**Create file: `backend/tests/Crm.UnitTests/Reports/GetSlaReportQueryHandlerTests.cs`** (structure of `GetAgentDashboardQueryHandlerTests.cs`):

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Automation;
using Crm.Application.Reports;
using Crm.Application.Reports.GetSlaReport;
using Crm.Application.Sla;
using Crm.Domain.Automation;
using Crm.Domain.Sla;

namespace Crm.UnitTests.Reports;

public sealed class GetSlaReportQueryHandlerTests
{
    [Fact]
    public async Task Handle_Last7Days_ClassifiesCohortIntoRatesExcludingPendingAndNotApplicableWithNaForUndecidedBuckets()
    {
        var now = new DateTimeOffset(2030, 1, 10, 12, 0, 0, TimeSpan.Zero);
        var d4 = new DateOnly(2030, 1, 4);
        var reporting = new FakeReportingRepository(
        [
            new SlaCohortCell(d4, ReportSlaOutcome.Met, ReportSlaOutcome.Met, 3),
            new SlaCohortCell(d4, ReportSlaOutcome.Breached, ReportSlaOutcome.Pending, 1),       // late response stays breached
            new SlaCohortCell(d4.AddDays(1), ReportSlaOutcome.NotApplicable, ReportSlaOutcome.NotApplicable, 2), // untracked
            new SlaCohortCell(d4.AddDays(2), ReportSlaOutcome.Pending, ReportSlaOutcome.Pending, 1),
        ], activeBreached: 4);
        var clock = new FixedTimeProvider(now);
        var handler = new GetSlaReportQueryHandler(
            reporting, new EmptyMonitoringRepository(),
            new SlaEvaluator(new NoCalendarConfiguration(), new NoWarningRules(), clock), clock);

        var result = await handler.Handle(new GetSlaReportQuery(ReportPreset.Last7Days, null, null), CancellationToken.None);

        // Half-open UTC range of the 7 days ending today, and "now" passed for the pending/breached split.
        Assert.Equal(new DateTimeOffset(2030, 1, 4, 0, 0, 0, TimeSpan.Zero), reporting.StartUtc);
        Assert.Equal(new DateTimeOffset(2030, 1, 11, 0, 0, 0, TimeSpan.Zero), reporting.EndExclusiveUtc);
        Assert.Equal(now, reporting.Now);

        Assert.Equal(new SlaTargetSummary(7, 2, 5, 3, 1, 1, 75.0), result.FirstResponse);   // 3 / (3 + 1)
        Assert.Equal(new SlaTargetSummary(7, 2, 5, 3, 0, 2, 100.0), result.Resolution);     // pending excluded
        Assert.Equal(7, result.Trend.Count);                                                 // daily buckets
        Assert.Equal(75.0, result.Trend[0].ResponseCompliancePercent);
        Assert.Null(result.Trend[1].ResponseCompliancePercent);                              // only not-applicable ⇒ N/A, not 0 %
        Assert.Null(result.Trend[3].ResponseCompliancePercent);                              // empty day ⇒ N/A
        Assert.Equal(0, result.Trend[3].ResponseMet + result.Trend[3].ResponseBreached);
        Assert.Equal(new SlaLiveStatus(4, 0, 0, 0, false, 0, false), result.Live);
        Assert.Equal(now, result.GeneratedAtUtc);
    }

    // Fakes (private sealed; NotSupportedException for every member the handler must not call):
    //  FakeReportingRepository(IReadOnlyList<SlaCohortCell> cells, int activeBreached): ListSlaCohortAsync records startUtc,
    //    endExclusiveUtc, now and returns cells; CountActiveBreachedTicketsAsync returns activeBreached. All others throw.
    //  EmptyMonitoringRepository : ISlaMonitoringRepository — ListActiveTrackedAsync returns []; the other two throw.
    //  NoCalendarConfiguration : ISlaConfigurationRepository — FindCalendarAsync returns null; others throw.
    //  NoWarningRules : IAutomationRuleRepository — ListEnabledAsync returns []; others throw.
    //  FixedTimeProvider — as GetAgentDashboardQueryHandlerTests lines 55–58.
}
```

**No existing test changes.**

---

## Frontend Tasks

### 10 — Models, API service, pure helpers

**Create file: `frontend/src/app/features/reports/reports-models.ts`**

```ts
import type { TicketCategory, TicketPriority, TicketStatus } from '../tickets/ticket-models';

export const REPORT_PRESETS = ['Last7Days', 'Last30Days', 'Last90Days', 'Custom'] as const;
export type ReportPreset = (typeof REPORT_PRESETS)[number];
export const DEFAULT_PRESET: ReportPreset = 'Last30Days';

export const PRESET_LABELS: Readonly<Record<ReportPreset, string>> = {
  Last7Days: 'Last 7 days',
  Last30Days: 'Last 30 days',
  Last90Days: 'Last 90 days',
  Custom: 'Custom',
};

/** What travels in the URL and to the API. from/to (yyyy-MM-dd) are only meaningful for Custom. */
export interface ReportPeriodParams { preset: ReportPreset; from: string | null; to: string | null; }

/** Matches Crm.Api ReportPeriodResponse. UTC calendar days, from..to inclusive. */
export interface ReportPeriod {
  preset: ReportPreset; from: string; to: string; startUtc: string; endExclusiveUtc: string; bucketDays: number; days: number;
}

export interface ReportCount<T> { value: T; count: number; }
export interface ReportTrendPoint { start: string; end: string; count: number; }

/** Matches Crm.Api TicketReportResponse. */
export interface TicketReport {
  period: ReportPeriod; createdTickets: number;
  byStatus: ReportCount<TicketStatus>[]; byCategory: ReportCount<TicketCategory>[]; byPriority: ReportCount<TicketPriority>[];
  trend: ReportTrendPoint[]; generatedAtUtc: string;
}

/** compliancePercent null = no decided target (N/A). */
export interface SlaTargetSummary {
  cohortTickets: number; notApplicable: number; applicable: number; met: number; breached: number; pending: number;
  compliancePercent: number | null;
}
export interface SlaTrendBucket {
  start: string; end: string;
  responseMet: number; responseBreached: number; responseCompliancePercent: number | null;
  resolutionMet: number; resolutionBreached: number; resolutionCompliancePercent: number | null;
}
/** Live — not affected by the period. */
export interface SlaLiveStatus {
  activeTicketsWithBreach: number; responseAtRisk: number; resolutionAtRisk: number;
  scannedActiveTickets: number; scanLimitReached: boolean; enabledWarningRules: number; calendarConfigured: boolean;
}
export interface SlaReport {
  period: ReportPeriod; firstResponse: SlaTargetSummary; resolution: SlaTargetSummary; trend: SlaTrendBucket[];
  live: SlaLiveStatus; generatedAtUtc: string;
}

export interface AgentPerformanceRow {
  agentUserId: string; name: string; isActive: boolean;
  open: number; inProgress: number; activeAssigned: number; overdueActive: number; resolved: number; closed: number;
  activeByPriority: ReportCount<TicketPriority>[]; resolutionActions: number; resolvedTicketsDistinct: number;
}
export interface AgentPerformanceTotals {
  openTickets: number; inProgressTickets: number; activeAssignedToAgents: number; overdueActiveAssignedToAgents: number;
  unassignedActive: number; resolutionActionsByAgents: number; resolutionActionsByOthers: number;
}
export interface AgentPerformanceReport {
  period: ReportPeriod; agents: AgentPerformanceRow[]; totals: AgentPerformanceTotals; generatedAtUtc: string;
}

export interface RecentFeedback {
  ticketId: string; ticketSubject: string; rating: number; comment: string | null; submittedAtUtc: string; updatedAtUtc: string;
}
export interface CustomerSatisfactionReport {
  period: ReportPeriod; feedbackCount: number; averageRating: number | null; positiveCount: number; positivePercent: number | null;
  distribution: ReportCount<number>[];
  participation: { eligibleTickets: number; ticketsWithFeedback: number; percent: number | null };
  recent: RecentFeedback[]; recentLimit: number; generatedAtUtc: string;
}
```

**Create file: `frontend/src/app/features/reports/reports-api.ts`** (style of `tickets-api.ts` lines 24–49):

```ts
@Injectable({ providedIn: 'root' })
export class ReportsApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  tickets(period: ReportPeriodParams): Observable<TicketReport> { return this.get('reports/tickets', period); }
  sla(period: ReportPeriodParams): Observable<SlaReport> { return this.get('reports/sla', period); }
  agents(period: ReportPeriodParams): Observable<AgentPerformanceReport> { return this.get('reports/agents', period); }
  customerSatisfaction(period: ReportPeriodParams): Observable<CustomerSatisfactionReport> {
    return this.get('reports/customer-satisfaction', period);
  }

  /** preset always; from/to only for Custom (the server ignores them otherwise). */
  private get<T>(path: string, { preset, from, to }: ReportPeriodParams): Observable<T> {
    let params = new HttpParams().set('preset', preset);
    if (preset === 'Custom' && from && to) {
      params = params.set('from', from).set('to', to);
    }
    return this.http.get<T>(this.apiUrl.versioned(path), { params });
  }
}
```

**Create file: `frontend/src/app/features/reports/report-format.ts`** (pure; no Angular injection):

```ts
import { ParamMap } from '@angular/router';
import { DEFAULT_PRESET, REPORT_PRESETS, ReportPeriodParams, ReportPreset } from './reports-models';

const DATE = /^\d{4}-\d{2}-\d{2}$/;

/** null ⇒ 'N/A' (never 0 %). Server values already carry 1 decimal. */
export function formatPercent(value: number | null): string {
  return value === null ? 'N/A' : `${value.toFixed(1)}%`;
}

export function formatAverage(value: number | null): string {
  return value === null ? 'N/A' : `${value.toFixed(2)} / 5`;
}

/** Bar width 0–100 relative to the largest value; 0 when everything is 0. */
export function barPercent(value: number, max: number): number {
  return max <= 0 ? 0 : Math.round((100 * value) / max);
}

/** URL ⇒ period. Unknown preset or a Custom without two well-formed dates ⇒ the default (Last 30 days). */
export function parsePeriodParams(params: ParamMap): ReportPeriodParams {
  const preset = params.get('preset');
  if (!preset || !(REPORT_PRESETS as readonly string[]).includes(preset)) {
    return { preset: DEFAULT_PRESET, from: null, to: null };
  }
  if (preset !== 'Custom') {
    return { preset: preset as ReportPreset, from: null, to: null };
  }
  const from = params.get('from');
  const to = params.get('to');
  return from && to && DATE.test(from) && DATE.test(to)
    ? { preset: 'Custom', from, to }
    : { preset: DEFAULT_PRESET, from: null, to: null };
}

/** Period ⇒ URL query params (null removes from/to for presets). */
export function toPeriodQueryParams(p: ReportPeriodParams): Record<string, string | null> {
  return p.preset === 'Custom' ? { preset: p.preset, from: p.from, to: p.to } : { preset: p.preset, from: null, to: null };
}
```

### 11 — Shared UI: period filter and bars

**Create file: `frontend/src/app/features/reports/report-period-filter.ts`** (selector `crm-report-period-filter`, standalone, `OnPush`, inline template). Imports `ReactiveFormsModule`, `MatButtonToggleModule`, `MatFormFieldModule`, `MatInputModule`, `MatButtonModule`, `MatIconModule`.

```ts
readonly value = input.required<ReportPeriodParams>();
readonly loading = input(false);
readonly changed = output<ReportPeriodParams>();
readonly refresh = output<void>();
protected readonly presets = REPORT_PRESETS;
protected readonly labels = PRESET_LABELS;
protected readonly form = inject(NonNullableFormBuilder).group({ from: [''], to: [''] });
protected readonly customOpen = signal(false);
protected readonly customError = signal<string | null>(null);

constructor() {
  effect(() => {           // keep the form in sync with the URL (back/forward, links from the dashboard)
    const v = this.value();
    untracked(() => {
      this.customOpen.set(v.preset === 'Custom');
      this.form.setValue({ from: v.from ?? '', to: v.to ?? '' });
      this.customError.set(null);
    });
  });
}

protected selectPreset(preset: ReportPreset): void {
  if (preset === 'Custom') { this.customOpen.set(true); return; } // wait for Apply
  this.changed.emit({ preset, from: null, to: null });
}

protected applyCustom(): void {
  const { from, to } = this.form.getRawValue();
  if (!from || !to) { this.customError.set('Choose both a start and an end date.'); return; }
  if (from > to) { this.customError.set('The start date must be on or before the end date.'); return; } // yyyy-MM-dd sorts lexically
  this.customError.set(null);
  this.changed.emit({ preset: 'Custom', from, to });
}
```

Template:
- `<mat-button-toggle-group aria-label="Report period" [value]="customOpen() ? 'Custom' : value().preset" (change)="selectPreset($event.value)">` with one toggle per preset (`labels[p]`).
- When `customOpen()`: two `<mat-form-field>` with `<input matInput type="date" formControlName="from">` / `to` (labels "From (UTC)" / "To (UTC)"), an **Apply** button, and `customError()` shown as `<p class="error" role="alert">`.
- A **Refresh** button (`<mat-icon>refresh</mat-icon> Refresh`, disabled while `loading()`) emits `refresh`.

Native `type="date"` gives `yyyy-MM-dd` directly, with no `DateAdapter` and no timezone shift. Server-side 400 messages (range > 366 days, future end date) are displayed by the pages via `describeHttpError`.

**Create file: `frontend/src/app/features/reports/report-bars.ts`** (selector `crm-report-bars`, inline template and styles, ≤ 1 kB):

```ts
export interface ReportBar { label: string; value: number; display?: string; }

@Component({
  selector: 'crm-report-bars',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (items().length === 0 || max() === 0) { <p class="hint">{{ emptyText() }}</p> }
    <ul class="bars" [attr.aria-label]="label()">
      @for (item of items(); track item.label) {
        <li>
          <span class="bar-label">{{ item.label }}</span>
          <span class="bar-track" aria-hidden="true"><span class="bar-fill" [style.inline-size.%]="percent(item.value)"></span></span>
          <span class="bar-value">{{ item.display ?? item.value }}</span>
        </li>
      }
    </ul>`,
  styles: `
    .bars { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--crm-space-2); }
    li { display: grid; grid-template-columns: minmax(6rem, 30%) 1fr auto; align-items: center; gap: var(--crm-space-3); }
    .bar-track { block-size: 12px; background: var(--mat-sys-surface-container-high); border-radius: var(--crm-radius-pill); }
    .bar-fill { display: block; block-size: 100%; background: var(--mat-sys-primary); border-radius: inherit; }
    .bar-value { font: var(--mat-sys-label-large); }
    .hint { margin: 0; color: var(--mat-sys-on-surface-variant); }`,
})
export class ReportBars {
  readonly items = input.required<ReportBar[]>();
  readonly label = input.required<string>();
  readonly emptyText = input('No data for this period.');
  protected readonly max = computed(() => Math.max(0, ...this.items().map(i => i.value)));
  protected percent(value: number): number { return barPercent(value, this.max()); }
}
```

The value text is always rendered, so the bars are decoration and the numbers stay readable without them (§12). For percentage bars (SLA trend) callers pass `value` = the rate (or 0) and `display` = `formatPercent(rate)`. N/A is then printed as text.

**Create file: `frontend/src/app/features/reports/reports-page.scss`** (shared by all five pages, ≤ 2 kB, logical properties):

```scss
section { margin-block-start: var(--crm-space-6); }
h2 { font: var(--mat-sys-title-large); margin-block: 0 var(--crm-space-2); }
.summary-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(170px, 1fr)); gap: var(--crm-space-4); margin-block-start: var(--crm-space-4); }
.summary-card mat-card-content { display: grid; gap: var(--crm-space-1); padding-block: var(--crm-space-4); }
.count { font: var(--mat-sys-display-small); overflow-wrap: anywhere; }
.scope { font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); text-transform: uppercase; }
.charts { display: grid; grid-template-columns: repeat(auto-fit, minmax(280px, 1fr)); gap: var(--crm-space-4); }
.table-scroll { overflow-x: auto; }
table { border-collapse: collapse; inline-size: 100%; min-inline-size: 640px; }
th, td { text-align: start; padding: var(--crm-space-2) var(--crm-space-3); border-block-end: 1px solid var(--mat-sys-outline-variant); }
td.num, th.num { text-align: end; }
.report-links { display: flex; flex-wrap: wrap; gap: var(--crm-space-3); }
```

Every report page uses `styleUrls: ['../../shared/ui/productivity-page.scss', './reports-page.scss']`.

### 12 — Routes and navigation

**Create file: `frontend/src/app/features/reports/reports.routes.ts`**

```ts
export const REPORTS_ROUTES: Routes = [
  { path: '', title: 'Management Dashboard | Support CRM', loadComponent: () => import('./management-dashboard-page').then(m => m.ManagementDashboardPage) },
  { path: 'tickets', title: 'Ticket Reports | Support CRM', loadComponent: () => import('./ticket-report-page').then(m => m.TicketReportPage) },
  { path: 'sla', title: 'SLA Performance | Support CRM', loadComponent: () => import('./sla-report-page').then(m => m.SlaReportPage) },
  { path: 'agents', title: 'Agent Performance | Support CRM', loadComponent: () => import('./agent-report-page').then(m => m.AgentReportPage) },
  { path: 'customer-satisfaction', title: 'Customer Satisfaction | Support CRM', loadComponent: () => import('./csat-report-page').then(m => m.CsatReportPage) },
];
```

**File: `frontend/src/app/app.routes.ts`**: insert after the `portal` route (after line 84, before the comment on line 85):

```ts
      {
        path: 'reports',
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/reports/reports.routes').then(m => m.REPORTS_ROUTES),
      },
```

**File: `frontend/src/app/core/layout/navigation.ts`**: insert after line 28 (`SLA & Automation`):

```ts
  { label: 'Reports', icon: 'insights', path: '/reports', exact: false, roles: ['SuperAdmin'] },
```

### 13 — Page load pattern (all five pages)

Every page follows `agent-dashboard-page.ts` lines 59–113, driven by the URL:

```ts
private readonly route = inject(ActivatedRoute);
private readonly router = inject(Router);
private readonly api = inject(ReportsApi);
private readonly reloads = new Subject<void>();
protected readonly period = signal<ReportPeriodParams>({ preset: DEFAULT_PRESET, from: null, to: null });
protected readonly report = signal<TicketReport | null>(null); // page-specific type
protected readonly loading = signal(true);
protected readonly error = signal<string | null>(null);

constructor() {
  merge(
    this.route.queryParamMap.pipe(map(parsePeriodParams), tap(p => this.period.set(p))),
    this.reloads.pipe(map(() => this.period())),
  )
    .pipe(
      tap(() => { this.loading.set(true); this.error.set(null); }),
      switchMap(p => this.api.tickets(p).pipe(        // page-specific call
        catchError((error: HttpErrorResponse) => {
          this.report.set(null);                       // never show stale numbers after a failure
          this.error.set(describeHttpError(error, 'Could not load the report.'));
          this.loading.set(false);
          return EMPTY;
        }),
      )),
      takeUntilDestroyed(),
    )
    .subscribe(r => { this.report.set(r); this.loading.set(false); });
}

protected onPeriodChanged(p: ReportPeriodParams): void {
  void this.router.navigate([], { relativeTo: this.route, queryParams: toPeriodQueryParams(p), queryParamsHandling: 'merge' });
}
protected refresh(): void { this.reloads.next(); } // manual Refresh / Retry: same period, new GET, no write
```

Common template header (every page):
- `<crm-page-header title="…" subtitle="…" />`.
- `<p><a mat-button routerLink="/reports" queryParamsHandling="preserve">← Management Dashboard</a></p>` (not on the dashboard itself).
- `<crm-report-period-filter [value]="period()" [loading]="loading()" (changed)="onPeriodChanged($event)" (refresh)="refresh()" />`.
- `@if (report(); as r)`: `<p class="hint">Period: {{ r.period.from | date: 'mediumDate' : 'UTC' }} – {{ r.period.to | date: 'mediumDate' : 'UTC' }} (UTC calendar days) · Updated {{ r.generatedAtUtc | date: 'medium' : 'UTC' }} UTC</p>`.
- `@if (loading()) { <mat-progress-bar mode="indeterminate" /> }`.
- `@if (error(); as m) { <p class="error" role="alert">{{ m }}</p> <button mat-stroked-button type="button" (click)="refresh()">Retry</button> }`.

### 14 — Ticket Reports page

**Create files: `frontend/src/app/features/reports/ticket-report-page.ts` / `.html`**: `TicketReportPage`.
- Header: "Ticket Reports" / "Tickets created in the selected period, with their current status, category and priority."
- Summary card: `.scope` "Period · by creation date", label "Tickets created", `.count` `r.createdTickets`.
- `.hint`: "Status, category and priority are the **current** values of the tickets created in this period, not their values at creation."
- `.charts` with three cards, each with a `<crm-report-bars>`:
  - "Current status": `byStatus` mapped to `{ label: statusLabel(c.value), value: c.count }`;
  - "Category": `byCategory`;
  - "Priority": `byPriority`.

  Each card footer reads "Total: {{ r.createdTickets }}".
- Card "Ticket creation trend": `<crm-report-bars label="Tickets created per day/week">`. Items:
  - `label` = `start | date: 'MMM d' : 'UTC'`, plus ` – {{ end }}` when `r.period.bucketDays > 1`;
  - `value` = `count`.

  Hint: "{{ r.period.bucketDays === 1 ? 'Daily' : '7-day' }} buckets by creation date (UTC). This is a creation trend, not a status-change trend."
- Empty state when `createdTickets === 0`: "No tickets were created in this period." The bars still render all enum rows with 0.

### 15 — SLA Performance page

**Create files: `frontend/src/app/features/reports/sla-report-page.ts` / `.html`**: `SlaReportPage`.
- Header: "SLA Performance" / "First-response and resolution targets of tickets created in the selected period."
- `.hint` (always visible):
  - "First response = a recorded **Mark as Responded** action (internal acknowledgement, not a message sent to the customer). Resolution = the first transition to Resolved."
  - "Compliance = met ÷ (met + breached). Pending and not-tracked targets are excluded."
- Two cards: "First response" (`r.firstResponse`) and "Resolution" (`r.resolution`). Each card shows:
  - `.scope` "Period · current outcome for tickets created in the period";
  - `.count` `formatPercent(s.compliancePercent)` with label "Compliance";
  - a small `.readonly`-style list: "Met {{ s.met }} · Breached {{ s.breached }} · Pending {{ s.pending }} · Not tracked {{ s.notApplicable }} · Tracked {{ s.applicable }} of {{ s.cohortTickets }}";
  - `@if (s.compliancePercent === null)` the hint "N/A — no target has been met or breached yet."
- Card "Compliance trend (by ticket creation date)". Two `<crm-report-bars>`:
  - "First response": items `{ label: bucket label, value: b.responseCompliancePercent ?? 0, display: formatPercent(b.responseCompliancePercent) + ' (' + b.responseMet + '/' + (b.responseMet + b.responseBreached) + ')' }`;
  - "Resolution": the same with the resolution fields.

  Hint: "Each bucket shows the current outcome of tickets created in it, not a historical point-in-time value. N/A = nothing decided."
- Section "Live now" with `.scope` "Live snapshot · not affected by the date filter":
  - cards "Active tickets with a breached target" (`live.activeTicketsWithBreach`), "First responses at risk" (`live.responseAtRisk`), "Resolutions at risk" (`live.resolutionAtRisk`);
  - hint "At risk = SLA Warning: pending targets within the lead time of an enabled SLA warning rule (same as SLA & Automation). Breached targets are never counted as at risk.";
  - conditional hints:
    - `!live.calendarConfigured` ⇒ "No business calendar is configured, so no target can be flagged at risk.";
    - `live.enabledWarningRules === 0` ⇒ "No SLA warning rules are enabled, so no target is flagged at risk.";
    - `live.scanLimitReached` ⇒ "At-risk counts cover the first {{ live.scannedActiveTickets }} active tracked tickets (oldest deadline first).";
  - link `<a mat-stroked-button routerLink="/sla">Open SLA & Automation</a>`.
- No breached-ticket table (out of scope).

### 16 — Agent Performance page

**Create files: `frontend/src/app/features/reports/agent-report-page.ts` / `.html`**: `AgentReportPage`.
- Header: "Agent Performance" / "Current workload per Agent and recorded resolution actions in the selected period."
- Totals cards with `.scope` labels:
  - Live: "Active assigned to Agents" (`totals.activeAssignedToAgents`), "Overdue (breached SLA)" (`totals.overdueActiveAssignedToAgents`), "Unassigned active" (`totals.unassignedActive`);
  - Period: "Resolution actions by Agents" (`totals.resolutionActionsByAgents`).
- `@if (totals.resolutionActionsByOthers > 0)` hint: "{{ n }} resolution actions in this period were performed by SuperAdmin or other non-Agent accounts and are not credited to any Agent."
- Card "Active workload (live)": `<crm-report-bars label="Active tickets per Agent">` with items `{ label: a.name, value: a.activeAssigned }`.
- `.table-scroll` > `<table>` with `<caption class="hint">Live columns reflect current assignment and are not affected by the date filter. "Resolution actions" are transitions to Resolved recorded by the Agent in the period; a ticket resolved again after reopening counts again ("Tickets" counts it once).</caption>`. Columns (`<th scope="col">`; numeric columns `class="num"`):
  - Agent (name plus `<crm-status-badge label="Inactive" tone="neutral" />` when `!isActive`);
  - Open; In Progress; Active; Overdue;
  - Active by priority: `Urgent n · High n · Medium n · Low n`, from `activeByPriority` in reverse declaration order;
  - Resolved*; Closed*;
  - Resolution actions; Tickets resolved.

  Footer note: "* currently assigned tickets with this status (supplementary, not historical)."
- Empty: `agents.length === 0` ⇒ "No Agent accounts exist yet." **No ranking, no sorting by numbers, no score.**

### 17 — Customer Satisfaction page

**Create files: `frontend/src/app/features/reports/csat-report-page.ts` / `.html`**: `CsatReportPage`.
- Header: "Customer Satisfaction" / "Feedback first submitted in the selected period, with the current rating after any edits."
- Cards (all `.scope` "Period · by first feedback submission", except participation):
  - "Feedback received" (`feedbackCount`);
  - "Average rating" (`formatAverage(averageRating)`);
  - "Positive (4–5 stars)" (`formatPercent(positivePercent)` plus "{{ positiveCount }} of {{ feedbackCount }}").
- Participation card: `.scope` "Period · tickets created in the period". Label "Feedback participation", value `formatPercent(participation.percent)`, text "{{ ticketsWithFeedback }} of {{ eligibleTickets }} tickets created in this period and currently Resolved/Closed have feedback." Hint when `eligibleTickets === 0`: "N/A — no eligible tickets."
- Card "Rating distribution": `<crm-report-bars>` items `{ label: c.value + ' ★', value: c.count }` for 1..5, footer "Total: {{ feedbackCount }}".
- Card "Recent feedback" (`.rows`, at most `recentLimit`). Each row shows:
  - `<a class="row-title" [routerLink]="['/tickets', f.ticketId]">{{ f.ticketSubject }}</a>`;
  - `<crm-status-badge [label]="f.rating + ' / 5'" [tone]="f.rating >= 4 ? 'success' : f.rating <= 2 ? 'error' : 'neutral'" />`;
  - `@if (f.comment) { <p class="user-text">{{ f.comment }}</p> } @else { <p class="hint">No comment.</p> }`;
  - `.meta` "Submitted {{ f.submittedAtUtc | date: 'medium' : 'UTC' }} UTC · Ref {{ f.ticketId }}", plus " · edited" when `f.updatedAtUtc !== f.submittedAtUtc`.

  Empty: "No feedback was submitted in this period."
- Hint: "Ratings are not attributed to Agents. Missing feedback is not counted as a rating."

### 18 — Management Dashboard page

**Create files: `frontend/src/app/features/reports/management-dashboard-page.ts` / `.html`**: `ManagementDashboardPage`. The same URL-driven pipeline as task 13, but the inner call is:

```ts
type Section<T> = { data: T | null; error: string | null };
const section = <T>(source: Observable<T>, fallback: string): Observable<Section<T>> =>
  source.pipe(
    map(data => ({ data, error: null })),
    catchError((e: HttpErrorResponse) => of({ data: null, error: describeHttpError(e, fallback) })),
  );

// inside switchMap(p => …): the SAME four endpoints the report pages use (R9) — no dashboard-specific numbers.
forkJoin({
  tickets: section(this.api.tickets(p), 'Could not load ticket figures.'),
  sla: section(this.api.sla(p), 'Could not load SLA figures.'),
  agents: section(this.api.agents(p), 'Could not load Agent figures.'),
  csat: section(this.api.customerSatisfaction(p), 'Could not load satisfaction figures.'),
})
```

Signal `overview = signal<{ tickets: Section<TicketReport>; sla: Section<SlaReport>; agents: Section<AgentPerformanceReport>; csat: Section<CustomerSatisfactionReport> } | null>(null)`. Each section renders its own error plus "Retry" (which calls `refresh()`), so one failing endpoint does not hide the others.

Template:
- Header: "Management Dashboard" / "Key figures across tickets, SLA, Agents and customer satisfaction."
- Period filter (task 11) and the period hint.
- `<nav class="report-links" aria-label="Reports">` with four `mat-stroked-button` links (`/reports/tickets`, `/reports/sla`, `/reports/agents`, `/reports/customer-satisfaction`), each `queryParamsHandling="preserve"`.
- Section "Period figures" (`.scope` on every card):
  - "Tickets created" (`tickets.createdTickets`) — by creation date;
  - "First-response compliance" (`formatPercent(sla.firstResponse.compliancePercent)` + "{{ met }}/{{ met + breached }} decided");
  - "Resolution compliance" (same with resolution);
  - "Average rating" (`formatAverage(csat.averageRating)` + "{{ feedbackCount }} feedback") — by feedback submission date;
  - "Resolution actions by Agents" (`agents.totals.resolutionActionsByAgents`) — by event date.
- Section "Live snapshot · not affected by the date filter":
  - "Open" (`agents.totals.openTickets`), "In Progress" (`agents.totals.inProgressTickets`), "Active assigned to Agents", "Unassigned active";
  - "Active tickets with a breached SLA target" (`sla.live.activeTicketsWithBreach`);
  - "SLA targets at risk" (`sla.live.responseAtRisk + sla.live.resolutionAtRisk`).
- `.charts`:
  - `<crm-report-bars label="Tickets created">` from `tickets.trend`;
  - `<crm-report-bars label="Active tickets per Agent">` from `agents.agents`;
  - `<crm-report-bars label="Rating distribution">` from `csat.distribution`.

  Each has a `View report` link (preserve query params).
- No auto-refresh, timers or polling. **Refresh** in the filter issues four new GETs.

### 19 — No other frontend changes

`AgentDashboardPage`, staff ticket pages, portal pages, `AuthService` and `roleGuard` are unchanged. No root store, no `localStorage`. No AI service is imported anywhere under `features/reports/`.

### 20 — Frontend unit test (the single new one)

**Create file: `frontend/src/app/features/reports/report-format.spec.ts`** (pattern of `portal-timeline.spec.ts`, no TestBed):

```ts
import { convertToParamMap } from '@angular/router';
import { barPercent, formatAverage, formatPercent, parsePeriodParams, toPeriodQueryParams } from './report-format';

describe('report formatting and shared period', () => {
  it('prints N/A instead of 0 % for undefined rates, scales bars and round-trips the shared period query', () => {
    expect(formatPercent(null)).toBe('N/A');
    expect(formatPercent(75)).toBe('75.0%');
    expect(formatAverage(null)).toBe('N/A');
    expect(formatAverage(4.333)).toBe('4.33 / 5');
    expect(barPercent(0, 0)).toBe(0);
    expect(barPercent(5, 20)).toBe(25);

    expect(parsePeriodParams(convertToParamMap({}))).toEqual({ preset: 'Last30Days', from: null, to: null });
    expect(parsePeriodParams(convertToParamMap({ preset: 'Last7Days', from: '2030-01-01' }))).toEqual({ preset: 'Last7Days', from: null, to: null });
    expect(parsePeriodParams(convertToParamMap({ preset: 'Custom', from: '2030-01-01', to: '2030-01-31' })))
      .toEqual({ preset: 'Custom', from: '2030-01-01', to: '2030-01-31' });
    expect(parsePeriodParams(convertToParamMap({ preset: 'Custom', from: '2030-01-01' }))).toEqual({ preset: 'Last30Days', from: null, to: null });
    expect(parsePeriodParams(convertToParamMap({ preset: 'Yearly' }))).toEqual({ preset: 'Last30Days', from: null, to: null });
    expect(toPeriodQueryParams({ preset: 'Last90Days', from: '2030-01-01', to: '2030-01-02' })).toEqual({ preset: 'Last90Days', from: null, to: null });
  });
});
```

---

## Documentation Tasks

### 21 — `docs/reports-and-management.md` (new)

Sections:

1. Scope (RM1–RM6, five areas, SuperAdmin only, read-only).
2. Period rules (R8: presets, UTC days, half-open range, validation limits, buckets).
3. **Metric definitions** — copy the "Metric definitions" table verbatim.
4. SLA semantics (R1–R4, R14: evaluator equivalence, first resolution only, recorded response ≠ message, at-risk = Warning, scan limit, `/sla` overview difference).
5. Agent attribution (R5, R6: recorded actor, roles never change, non-Agent actions, overdue once per ticket, legacy tickets without activity).
6. CSAT (submission cohort, current rating, participation R7, no Agent attribution).
7. Dashboard composition (R9, R10; period vs live labels).
8. API table (4 routes, parameters, 400/401/403).
9. Angular routes and navigation.
10. Performance notes and follow-ups (R11: no new indexes; candidate indexes `TicketFeedback (CreatedAtUtc)` and `TicketActivities (Type, OccurredAtUtc)` for larger data, requiring separate review).
11. Out of scope / future (exports, schedules, Communication Channels last).

### 22 — Other docs

- `docs/identity-and-access.md`:
  - Matrix (after line 41): rows "Management dashboard and reports (tickets, SLA, Agents, CSAT)" — SuperAdmin ✔, Agent —, Customer —.
  - Routes table (before the `/api/health` row, line 90): `GET /api/v1/reports/tickets`, `/sla`, `/agents`, `/customer-satisfaction`, each "SuperAdmin", 200.
- `docs/agent-dashboard.md` line 70: replace "SuperAdmin or company-wide dashboard, analytics, SLA (Reports & Management, SLA & Automation)." with "SuperAdmin or company-wide dashboard and analytics: shipped in REPORT-001 ([reports-and-management.md](reports-and-management.md)); the Agent Dashboard stays Agent-only."
- `docs/customer-portal.md` line 15: replace "CSAT reports (future Reports & Management)" with "CSAT reports (shipped in REPORT-001, SuperAdmin only)". Line 74: replace "CSAT reporting belongs to Reports & Management." with "CSAT reporting: see [reports-and-management.md](reports-and-management.md)."
- `README.md` line 3: change "… and the Customer Portal (`PORTAL-001`);" to "…, the Customer Portal (`PORTAL-001`) and Reports & Management (`REPORT-001`);".

---

## Edge Cases & Failure Modes

- **Agent / Customer token on any `/reports/*` route:** 403 (class-level `SuperAdmin` policy, `ReportsController`). No token ⇒ 401 (fallback policy, `SecurityRegistration.cs` line 62). A forged `role` claim fails JWT signature validation. A deactivated or stamp-changed SuperAdmin token is rejected by the existing `OnTokenValidated` check (`SecurityRegistration.cs` lines 50–56).
- **Agent or Customer opening `/reports` in Angular:** `roleGuard('SuperAdmin')` redirects to `/`. The nav entry is not rendered (`roles: ['SuperAdmin']`, filtered in `shell.ts`).
- **Invalid period input:**
  - `preset=Yearly` or `from=2030-13-01` ⇒ 400 from model binding;
  - `preset=Custom` without `from`/`to`, `from > to`, `from < 2000-01-01`, `to > today+1`, or a range > 366 days ⇒ 400 `ValidationProblemDetails` with `from`/`to` keys (`ReportPeriodRules`);
  - the Angular filter blocks the missing and inverted cases first.

  A numeric preset (`preset=7`) is rejected by `IsInEnum`. MVC's enum binder accepts numeric strings, so `preset=1` binds to `Last30Days`. That is harmless and documented.
- **`from`/`to` sent with a non-Custom preset:** ignored (validator `When`, `ReportPeriod.Resolve`). The response `period` shows the resolved range, so the caller sees what was used.
- **Empty period (no tickets / no feedback):**
  - counts are 0, every enum bucket is listed with 0, and trend buckets are 0;
  - compliance, positive %, average and participation are `null` ⇒ "N/A" (`ReportMath.Percent`, `formatPercent`), never 0 %;
  - `ReportBars` shows "No data for this period." when all values are 0.
- **Midnight boundary:** a ticket at exactly `to+1 00:00:00Z` is excluded and one at `from 00:00:00Z` is included (half-open, `CreatedIn`). A day is counted once.
- **Non-UTC offsets in stored timestamps:** all writes use `TimeProvider.GetUtcNow()` (offset 0), so `DATEPART` gives the UTC day. A manually inserted row with another offset is still range-filtered correctly (`datetimeoffset` comparisons are instant-based), but it is bucketed by its local day. `BucketIndex` returns -1 for a day outside the period and the cell is skipped from the trend only. Documented.
- **Status/category/priority changed after creation:** the cohort uses current values (`ListTicketCohortAsync` groups current columns). Totals still reconcile because every ticket is in exactly one cell.
- **Concurrent ticket creation during a report:** each report's cohort is one statement, so its own totals always reconcile. The dashboard's four calls are separate statements and can differ by tickets created in between. Refresh re-reads.
- **Untracked (pre-SLA or SLA-disabled) tickets:** `NotApplicable` for both targets. They are counted in `cohortTickets` but not in `applicable` or the compliance denominator.
- **Late recorded first response:** `FirstRespondedAtUtc > due` ⇒ `Breached` (CASE in `ListSlaCohortAsync`), never Met.
- **Ticket resolved without a recorded first response (R4):** `Pending` until the first-response due instant passes, then `Breached`. This is the same as the ticket SLA card.
- **Reopened and re-resolved tickets:** resolution outcome from `SlaResolvedAtUtc` (first resolution, never cleared). One outcome per ticket. Reopening does not create a second target or a second count.
- **Active ticket with a past late response but an on-time first resolution:** counted in "active tickets with a breached target" (`IsBreached`). The `/sla` overview may omit it (R14). Documented.
- **No calendar / no warning rules:** at-risk = 0 with an explanatory hint (`calendarConfigured`, `enabledWarningRules`).
- **More than 500 active tracked tickets:** at-risk counts cover the first 500 by due order (`ListActiveTrackedAsync`); `scanLimitReached = true` with a hint. The breach count is unbounded SQL.
- **Ticket with both targets breached and assigned to an Agent:** overdue counts it once (`ListOverdueActiveByAssigneeAsync` is a `WHERE` on `Tickets`).
- **Reassignment:** live workload and overdue follow the new assignee on the next refresh. Recorded resolution actions stay with the recorded actor (`ListResolutionActionsAsync` never reads `AssignedAgentUserId`).
- **SuperAdmin resolves a ticket assigned to an Agent:** the action goes to `resolutionActionsByOthers`. The Agent's `resolutionActions` is unchanged.
- **Ticket resolved, resumed, resolved again by the same Agent in the period:** `resolutionActions` +2, `resolvedTicketsDistinct` +1.
- **Deactivated Agent:** still listed (with an Inactive badge) with their remaining assignments and past actions. Tickets assigned to them are not reallocated.
- **Ticket assigned to a non-Agent account** (not possible through the API; only by manual DB edits): included in the live Open/In Progress totals, but not in any Agent row or `unassignedActive`. Documented.
- **Legacy tickets without activity rows:** contribute no resolution actions (nothing is recorded). Documented. No action is inferred from the current status.
- **CSAT edited after submission:** the rating and comment are current. The cohort and the displayed date stay `CreatedAtUtc` (`SubmittedIn`). Editing from 2 to 5 moves the distribution and mean but not the period membership.
- **Feedback on a ticket that was later reopened:** still counted in the feedback metrics (submission cohort). It is excluded from the participation denominator and numerator until the ticket is Resolved/Closed again (R7).
- **Feedback submitted in the period for a ticket created before the period:** counted in the rating metrics, but not in participation (different cohort, labelled).
- **Untrusted comment text** (`<script>`, long text, emoji): rendered with interpolation in `.user-text` (pre-wrap, `overflow-wrap: anywhere`). No `[innerHTML]` under `features/reports/`. Verified by grep.
- **Narrow screens (400 px):** summary and chart grids collapse to one column (`minmax(170px|280px, 1fr)`). The Agent table scrolls inside `.table-scroll` (no page scroll). Bar rows wrap labels. The toggle group wraps.
- **URL tampering** (`/reports/sla?preset=Custom&from=bad`): `parsePeriodParams` falls back to Last 30 days. Server validation remains authoritative.
- **Read-only guarantee:** no handler takes `IUnitOfWork` and every query is `AsNoTracking`. Reports never call `SaveChanges`, the SLA worker, automation, notifications or `IAiTextGenerator` (grep check in Verification).
- **API down / timeout:** each page shows the error with Retry. The dashboard shows per-section errors. No stale numbers remain after a failure (`report.set(null)`).

---

## Test Plan

1. **New backend unit test:** `backend/tests/Crm.UnitTests/Reports/GetSlaReportQueryHandlerTests.cs` → `Handle_Last7Days_ClassifiesCohortIntoRatesExcludingPendingAndNotApplicableWithNaForUndecidedBuckets` (task 9). It covers the period resolution (half-open UTC range), the compliance denominator, pending/non-applicable exclusion, N/A buckets, and the live block wiring. Fakes per `GetAgentDashboardQueryHandlerTests.cs` and `TicketCreationAutomationTests.cs`.
2. **New frontend unit test:** `frontend/src/app/features/reports/report-format.spec.ts` (task 20). It covers N/A formatting, bar scaling, and the shared period URL contract. Pattern of `portal-timeline.spec.ts`.
3. **Existing tests:** all must pass **unchanged** (no interface used by an existing fake changes; `TicketRepository.IsBreached` visibility is internal only). Baseline: backend 12, frontend 12. After: **13 and 13**.
4. **Manual (not automated):** API (PowerShell `Invoke-RestMethod` / curl), SQL (LocalDB `CrmDev`), browser. See Verification Steps 5–17. **No** Playwright/E2E, integration, SQL or performance test projects (intake §13/§14).

---

## Verification Steps

1. **Baseline:** in the repo root, `git status` ⇒ only the untracked reports-management intake/plan folders. `git log --oneline -3` ⇒ `PORTAL-001` present. In `backend/`: `dotnet test` ⇒ 12 passed. In `frontend/`: `npm test -- --watch=false` ⇒ 12 passed. Record both, plus the current `ng build` warnings (bundle budget baseline).
2. **Backend builds and tests:** in `backend/`: `dotnet build` (0 errors; list pre-existing warnings separately) and `dotnet test` ⇒ 13 passed.
3. **No schema change and layering:** in `backend/`:
   - `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ no changes.
   - `git status --porcelain backend/src/Crm.Infrastructure/Persistence/Migrations` ⇒ empty.
   - `grep -rn "EntityFrameworkCore\|Microsoft.AspNetCore" src/Crm.Application/Reports` ⇒ nothing.
   - `grep -rn "IUnitOfWork\|SaveChanges\|IAiTextGenerator\|Gemini" src/Crm.Application/Reports src/Crm.Infrastructure/Persistence/Repositories/ReportingRepository.cs src/Crm.Api/Controllers/ReportsController.cs` ⇒ nothing.
4. **Frontend builds and tests:** in `frontend/`: `npx ng build` (no errors; compare budget warnings with step 1; every reports component style ≤ 4 kB) and `npm test -- --watch=false` ⇒ 13 passed. `grep -rn "innerHTML" src/app/features/reports` ⇒ nothing.
5. **Setup (nothing existing is deleted, reset or reassigned without recording it):** `.\run-dev.ps1`.
   - As SuperAdmin ensure an enabled SLA policy and calendar exist (record the current state, do not overwrite silently). Create Agents "Report Alpha" (`report.alpha@example.test`) and "Report Beta".
   - Tokens: `$admin`, `$alpha`, `$beta`, and an existing Customer `$cust`, from `POST /api/v1/auth/login`. Never paste them into the report.
   - Create tickets T1–T6 as SuperAdmin for any customer, with varied category/priority. Assign T1–T3 to Alpha, T4 to Beta, leave T5–T6 unassigned.
6. **Authorization (§13.2):** for each of the 4 routes:
   - no token ⇒ 401;
   - `$alpha` ⇒ 403;
   - `$cust` ⇒ 403;
   - `$admin` ⇒ 200.

   Also check `$admin` `GET /api/v1/reports/tickets?preset=Custom&from=2030-02-01&to=2030-01-01` ⇒ 400 `to`; `?preset=Custom` ⇒ 400 `from`, `to`; `?preset=Custom&from=2025-01-01&to=2026-06-30` ⇒ 400 (366 days); `?preset=Yearly` ⇒ 400.
7. **Ticket volume and grouping (§13.3–§13.6):**
   - `GET …/reports/tickets?preset=Last7Days` ⇒ `createdTickets` = SQL `SELECT COUNT(*) FROM Tickets WHERE CreatedAtUtc >= '<period.startUtc>' AND CreatedAtUtc < '<period.endExclusiveUtc>'`.
   - SQL `SELECT Status, COUNT(*) … GROUP BY Status` (and `Category`, `Priority`) ⇒ matches `byStatus`/`byCategory`/`byPriority`, every enum member present, each list sums to `createdTickets`.
   - Trend: `SELECT CAST(CreatedAtUtc AS date), COUNT(*) … GROUP BY CAST(CreatedAtUtc AS date)` ⇒ the matching daily buckets.
   - `?preset=Custom` with a range that excludes today ⇒ T1–T6 not counted.
   - Change T2's priority and move T2 Open→InProgress (staff UI) ⇒ refresh ⇒ T2 moves bucket; `createdTickets` is unchanged.
   - `?preset=Last90Days` ⇒ `period.bucketDays = 7` and buckets start at `period.from`.
8. **SLA (§13.7–§13.8):**
   - For T1: Mark as Responded **before** the due time. Then move it InProgress→Resolved.
   - For T3: (optional) wait for or create a past-due response target (a ticket created with a short Urgent target), then Mark as Responded **after** the due time.
   - SQL per-target classification:
     ```sql
     SELECT Id,
       CASE WHEN SlaFirstResponseDueAtUtc IS NULL THEN 'NA' WHEN FirstRespondedAtUtc IS NOT NULL THEN IIF(FirstRespondedAtUtc <= SlaFirstResponseDueAtUtc,'Met','Breached') WHEN SlaFirstResponseDueAtUtc < SYSDATETIMEOFFSET() THEN 'Breached' ELSE 'Pending' END AS FirstOutcome,
       CASE WHEN SlaResolutionDueAtUtc IS NULL THEN 'NA' WHEN SlaResolvedAtUtc IS NOT NULL THEN IIF(SlaResolvedAtUtc <= SlaResolutionDueAtUtc,'Met','Breached') WHEN SlaResolutionDueAtUtc < SYSDATETIMEOFFSET() THEN 'Breached' ELSE 'Pending' END AS ResolutionOutcome
     FROM Tickets WHERE CreatedAtUtc >= '<startUtc>' AND CreatedAtUtc < '<endExclusiveUtc>'
     ```
     Grouped counts ⇒ match `firstResponse`/`resolution`. Compliance = met/(met+breached), or `null` when 0.
   - The late response stays `Breached`.
   - Open each ticket's SLA card in the staff UI ⇒ the same Met/Breached/Pending per target.
   - `live.activeTicketsWithBreach` = `GET /api/v1/tickets?slaBreached=true&status=Open` total + the same for `InProgress`.
   - At risk: with an enabled `SlaWarningDue` rule and a calendar, compare `live.responseAtRisk + live.resolutionAtRisk` with the Warning tickets on `/sla` (allowing for R14). Disable the rules ⇒ at-risk 0 plus the "No SLA warning rules are enabled" hint. Restore the rules.
   - Reopen T1 (Resolved→Closed→Open) and resolve it again ⇒ its resolution outcome unchanged (first resolution); counts unchanged.
9. **Agent workloads and attribution (§13.9–§13.11):**
   - `GET …/reports/agents` ⇒ Alpha `open/inProgress/active` = SQL `SELECT Status, COUNT(*) FROM Tickets WHERE AssignedAgentUserId='<alpha>' GROUP BY Status`. Same check for Beta. `totals.unassignedActive` = SQL count of unassigned Open/InProgress.
   - Reassign T3 Alpha→Beta ⇒ refresh ⇒ the workloads move. Alpha's resolution actions are unchanged.
   - As `$alpha` resolve T2 (InProgress→Resolved); as `$admin` resolve T4 (Beta's ticket) ⇒ Alpha `resolutionActions +1`; Beta unchanged; `totals.resolutionActionsByOthers +1`.
   - SQL `SELECT ActorUserId, COUNT(*), COUNT(DISTINCT TicketId) FROM TicketActivities WHERE Type='StatusChanged' AND NewValue='Resolved' AND OccurredAtUtc >= '<startUtc>' AND OccurredAtUtc < '<endExclusiveUtc>' GROUP BY ActorUserId` ⇒ matches.
   - As `$alpha` resume T2 (Resolved→InProgress) and resolve it again ⇒ `resolutionActions +1`, `resolvedTicketsDistinct` unchanged.
   - Overdue: a breached active ticket assigned to Alpha with both targets breached ⇒ Alpha `overdueActive` counts it once. Cross-check `/tickets?slaBreached=true` filtered to Alpha's tickets.
10. **CSAT (§13.12–§13.14):**
    - As the owning Customer, submit feedback on two Resolved/Closed tickets via the portal: ratings 2 and 5, one comment `<script>alert(1)</script> thanks`.
    - `GET …/reports/customer-satisfaction` ⇒ `feedbackCount 2`, `averageRating 3.5`, `positivePercent 50.0`, distribution `[0,1,0,0,1]`.
    - SQL `SELECT Rating, COUNT(*) FROM TicketFeedback WHERE CreatedAtUtc >= '<startUtc>' AND CreatedAtUtc < '<endExclusiveUtc>' GROUP BY Rating` ⇒ matches.
    - Edit the 2 to 4 ⇒ average 4.5, positive 100.0, `submittedAtUtc` unchanged, `updatedAtUtc` later ("edited" in the UI).
    - Participation: SQL
      ```sql
      SELECT COUNT(*), SUM(CASE WHEN EXISTS (SELECT 1 FROM TicketFeedback f WHERE f.TicketId = t.Id) THEN 1 ELSE 0 END)
      FROM Tickets t WHERE t.CreatedAtUtc >= '<startUtc>' AND t.CreatedAtUtc < '<endExclusiveUtc>' AND t.Status IN ('Resolved','Closed')
      ```
      ⇒ matches `participation`.
    - Reopen one feedback ticket ⇒ the denominator and numerator drop by one; feedback metrics unchanged.
    - A Custom period with no tickets ⇒ participation `null` / "N/A".
    - Recent list: newest submission first, the comment shown literally (no alert), no customer name or email in the JSON.
11. **Dashboard consistency (§13.15):** for one period, every dashboard card equals the matching field of the four report responses (DevTools Network: exactly four GETs, no POST/PUT). Period cards and live cards are labelled. Refresh ⇒ four new GETs. SQL row counts of `Tickets`, `TicketActivities`, `TicketFeedback`, `SlaAlerts`, `AutomationRuleExecutions` and `TaskReminderNotifications` are identical before and after 10 refreshes.
12. **Browser (§13.16):** as SuperAdmin:
    - nav shows "Reports";
    - all five pages load;
    - presets and Custom work; the URL carries the period; the dashboard links keep it;
    - Custom validation messages appear; server 400 text appears for a > 366-day range;
    - empty period ⇒ zero bars and N/A;
    - stop the API ⇒ error plus Retry on each page and per dashboard section.
13. **Narrow screen:** DevTools ~400 px on every reports page ⇒ no horizontal page scroll. Cards and charts are one column. The Agent table scrolls inside its box. Toggles wrap.
14. **Visibility (§13.1):** as Agent and as Customer ⇒ no "Reports" nav entry. `/reports` and `/reports/sla` in the URL bar ⇒ redirected to `/`. The Agent Dashboard is unchanged.
15. **No AI (§13.17):** with DevTools Network open, use every reports page ⇒ no `/ai/` request. Backend logs show no Gemini call.
16. **Regression smoke (§13.17):**
    - sign-in/out for each role;
    - staff ticket create/edit/assign/transition/escalate/activity;
    - Mark as Responded;
    - `/sla` overview and worker alerts (bell);
    - Agent Dashboard counts;
    - Customer Portal submission and feedback;
    - KB staff and Help Center;
    - the AI card works only on click.
17. **OpenAPI:** `curl -s http://localhost:5080/openapi/v1.json` ⇒ the 4 report operations, each "Requires role: SuperAdmin", `preset` enum as strings, `from`/`to` as `date`, no `[AllowAnonymous]`.
18. **Record honestly:**
    - list which of steps 5–17 were run and the observed results;
    - anything not run (especially the optional late-response SLA case, the at-risk comparison, and browser steps 12–15) is **Unverified**, not passed;
    - report `dotnet build`, `dotnet test`, `ng build`, `npm test`, `has-pending-model-changes`, any EF translation fallback taken (task 7 notes), R1–R14, and remaining limitations;
    - keep pre-existing warnings separate;
    - list the demo data created in step 5.

    **Do not commit or push before the user accepts REPORT-001.**

---

## Done Criteria

- [ ] Every `/api/v1/reports/*` endpoint is SuperAdmin-only (Agent/Customer 403, anonymous 401). Inputs are limited to a validated `preset`/`from`/`to`. No endpoint writes anything or calls AI (RM1, R13).
- [ ] Ticket report: created-ticket volume by `CreatedAtUtc`, current status/category/priority distributions with all members (zeros included) that reconcile with the volume, and a daily or 7-day creation trend, all from one grouped SQL statement (RM2).
- [ ] SLA report: per-target NotApplicable/Met/Breached/Pending identical to `SlaEvaluator`; compliance = met/(met+breached) with N/A; first resolution only; a late response stays breached; cohort trend; a live block (breached active tickets, at-risk via the existing Warning semantics, with scope, scan and configuration flags) labelled as not filtered by the period (RM3, R1–R4, R14).
- [ ] Agent report: live Open/In Progress/Active per current assignee, active priority mix, supplementary Resolved/Closed, and overdue counted once per ticket via `IsBreached`; period resolution actions attributed to the recorded actor, with distinct tickets; non-Agent actions shown but not credited; unassigned active shown separately; no ranking (RM4, R5, R6).
- [ ] CSAT report: submission-date cohort with current rating; count, mean (N/A when empty), positive %, 1–5 distribution, documented participation numerator/denominator, and the 20 most recent rows with safe text and no customer PII (RM5, R7).
- [ ] Management dashboard composes the same four endpoints for one shared period, labels Period vs Live cards, has simple bar charts, links to the four reports keeping the period, and has manual Refresh with per-section errors and no polling (RM6, R9, R10).
- [ ] Shared period control (7/30/90/Custom, default 30, UTC labels) on all five screens; loading, empty, error/Retry and N/A states; works at 400 px.
- [ ] No migration, table, index or package added. `has-pending-model-changes` reports none. Existing row counts are unchanged by report use.
- [ ] Exactly one new backend test and one new frontend test. All existing tests pass (13/13 after). Both builds succeed.
- [ ] `docs/reports-and-management.md` created. Identity, Agent Dashboard, Customer Portal docs and README updated.
- [ ] Manual matrix (Verification 5–17) recorded honestly. Unrun steps are marked Unverified. Nothing is committed or pushed until the user reviews and accepts REPORT-001.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
