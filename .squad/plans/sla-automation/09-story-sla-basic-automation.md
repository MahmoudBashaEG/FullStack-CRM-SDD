# Story 09 — SLA & Basic Automation — Targets, Calendars, Workflows, Background Jobs, Assignment, Escalation and Notifications

## Prerequisites

- **Story 08 completed:** [../customer-management/08-story-customer-notes-interactions-attachments.md](../customer-management/08-story-customer-notes-interactions-attachments.md) (`CUST-002`, verified committed as `6b569b2` on `master`). Working tree verified clean apart from the untracked `.squad/stories/sla-automation/` intake and `.squad/plans/sla-automation/` folder. **Re-run `git status` before implementing; stop and report if any application file is modified.**
- **Story 07 completed:** [../agent-dashboard/07-story-agent-productivity-collaboration.md](../agent-dashboard/07-story-agent-productivity-collaboration.md) (`DASH-002`, `b9eec58`): `TaskReminderNotification` + `NotificationsController` (Agent-only, task-reminder-specific), `NotificationBell` (Agent-only 60-second poll), `/notifications` page, `ITicketAccessLock`, `EfUnitOfWork.ExecuteInTransactionAsync` (retryable attempt + never-replayed commit), `KnownUniqueIndexes` allow-list, `ForbiddenException`.
- **Story 06 completed:** [../agent-dashboard/06-story-agent-dashboard-ticket-overview-work-queue.md](../agent-dashboard/06-story-agent-dashboard-ticket-overview-work-queue.md) (`DASH-001`, `913eaac`): `IAgentDashboardRepository`, `AttentionTicket`, `AgentDashboardPage`.
- **Story 05 completed:** [../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md) (`TICK-002`, `c2f7264`): `TicketWorkflow`, `Ticket.TransitionTo` / `Escalate` / `ChangeAssignee`, append-only `TicketActivity`, `Tickets.Version` rowversion ⇒ 409.
- **Story 04 completed:** [../ticket-management/04-story-ticket-creation-management.md](../ticket-management/04-story-ticket-creation-management.md) (`TICK-001`, `b44c824`): `Ticket`, `TicketPriority { Low, Medium, High, Urgent }`, `TicketCategory { General, Technical, Billing, Other }`, `CreateTicketCommand` initial-assignee rules.
- **Stories 01–03 completed:** [../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md) (Mediator pipeline, `ITransactionalCommand`, `GlobalExceptionHandler`), [../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md) (`AuthorizationPolicies`, `roleGuard`, `NAV_ITEMS.roles`, `IIdentityService.FindByIdAsync` / `ListByRoleAsync`), [../customer-management/03-story-customer-profiles-contact-management.md](../customer-management/03-story-customer-profiles-contact-management.md) (`PagedResult<T>` / `PagedResponse<T>`).
- Applied migrations (verified in `backend/src/Crm.Infrastructure/Persistence/Migrations/`): `20261014234527_InitialCreate`, `20261015011303_AddIdentityAndRefreshSessions`, `20261017183245_AddCustomers`, `20261017194039_AddTickets`, `20261017203222_AddTicketWorkflowAndActivity`, `20261017232626_AddAgentProductivityAndCollaboration`, `20261018010157_AddCustomerNotesInteractionsAttachments`. **This story adds exactly one migration, `AddSlaAndAutomation`, and edits none of the existing ones.**
- Tooling unchanged: .NET SDK 10 (`backend/global.json`), EF Core **10.0.13**, Mediator **3.0.2** (source-generated), FluentValidation, xUnit **2.9.3**, Angular / Material / CDK **22.2**, Vitest via `ng test`. **No new NuGet or npm packages.** `BackgroundService`, `PeriodicTimer`, `TimeZoneInfo`, `DateOnly` and `TimeOnly` are in the shared framework already referenced by `Crm.Api` (Web SDK).
- Intake: `.squad/stories/sla-automation/SLA-001/intake.md` (no attachments; `attachments/` is empty). First story of the `sla-automation` feature. **Business decisions S1–S12 are all approved** (intake §16) and are not reopened here; the deviations forced by the existing code are listed under **Technical conflicts resolved** below.
- Shared contracts touched deliberately: `Ticket` (+8 nullable SLA columns, +4 methods, `TransitionTo` records the first resolution), `TicketActivity` (`ActorUserId` becomes **nullable** = automation, +3 factories), `TicketActivityType` (+`FirstResponseRecorded`), `TicketDetails` (+optional `Sla`), `TicketActivityDetails` (+`IsAutomatic`), `AttentionTicket` (+optional `SlaIndicator`), `TicketRepository` / `AgentDashboardRepository` (SLA projection + evaluation, left join for the activity actor), `CreateTicketCommandHandler` (+1 dependency), `TicketsController` (+1 action, +1 list filter), `CrmDbContext` (+5 `DbSet`s), Infrastructure / Application DI, `Program.cs` (+worker), `NAV_ITEMS` (+1), `app.routes.ts` (+1 route, `/notifications` guard widened), `Shell` (bell for all staff), `NotificationBell`, `NotificationsPage`, `TicketDetailPage` (+SLA card), `TicketListPage` (+SLA column/filter), `AgentDashboardPage` (+SLA badge), `ticket-activity-list.ts` (`describeActivity`). **No change** to `TicketWorkflow`, `TicketRepository.Scope`, `NotificationsController`, `TaskReminderRepository`, `ITicketAccessLock`, `EfUnitOfWork`, `TransactionBehavior`, any existing migration, or any customer / task / quick-reply / collaboration code.

---

## Story Goal

Turn the ticket CRM into a system that measures, routes, watches and escalates support work, configurable by SuperAdmin without code changes. All nine capabilities ship end-to-end at a basic level:

1. **SLA targets** — one global policy (enabled/disabled) with first-response and resolution targets, in **business minutes**, for each of the four existing priorities.
2. **Business calendar** — one global calendar: IANA/Windows timezone id, working weekdays, one opening/closing local time, explicit holiday dates. Deadlines are computed by adding business minutes and are **persisted as UTC instants** on the ticket at creation.
3. **Rules engine** — SuperAdmin-managed rules with fixed triggers (`TicketCreated`, `SlaWarningDue`, `SlaBreached`), fixed optional conditions (category, priority, assigned/unassigned, SLA kind), fixed actions (`AssignToAgent`, `CreateInAppNotification`, `EscalateTicket`), parameters (target Agent, warning lead minutes), enabled flag and sort order. Validated on save.
4. **Background worker** — a hosted `BackgroundService` inside `Crm.Api` that runs at startup and every `SlaAutomation:PollInterval` (default 60 s), finds due warnings/breaches, evaluates enabled rules and records each effect exactly once (unique execution key in SQL).
5. **Automatic assignment** — on creation of an **unassigned** ticket, the first matching enabled `TicketCreated` rule whose target is an **active Agent** assigns it, recorded in history as "Assigned automatically" by **Automation**.
6. **Automatic escalation** — a matching `SlaBreached` + `EscalateTicket` rule escalates the ticket once through the existing escalation flag, attributed to Automation. Already escalated ⇒ no second event.
7. **In-app alerts** — persistent SLA alerts (warning: assigned Agent, or SuperAdmins when unassigned; breach: assigned Agent **and** SuperAdmins), unread/read, shown in the existing bell and Notifications page, filtered by **current** ticket access.
8. **Ticket SLA monitoring** — Ticket Details SLA card (targets, due times, `Pending / Met / Breached`, warning, remaining/overdue business time, actual times, **Mark as Responded**); SLA indicator (`On track / Warning / Breached / Met / Not tracked`) on the ticket list, its "SLA breached" filter, and the Agent Dashboard queue.
9. **SuperAdmin "SLA & Automation" screens** — Policy, Business Calendar (with a due-date preview), Automation Rules, Overview (warning/breached lists and recent automation results).

Exactly one new backend and one new frontend happy-path unit test; mandatory manual API / SQL / browser matrix for all nine capabilities; new `docs/sla-and-automation.md` and doc updates.

**Not in scope** (do **not** build): email/SMS/WhatsApp/live chat/push/WebSockets/SignalR, customer alerts, any message sending, SLA pause/resume, per-department/per-priority calendars, split shifts, recurring holidays, retroactive recalculation of existing tickets, new SLA cycles on reopen, round-robin/load-balancing/skills routing, visual workflow designers, scripts/expressions/nested conditions, Hangfire/Quartz/queues/distributed locks, multi-instance coordination, charts/KPI reports, changes to ticket visibility (`TicketRepository.Scope`), changes to the DASH-002 reminder endpoints, integration/E2E suites, new packages.

### Technical conflicts resolved (surface these in the implementation report)

| # | Conflict found in the code | Resolution in this plan |
|---|---|---|
| C1 | `TicketActivity.ActorUserId` is a **non-nullable** `Guid` FK to `AspNetUsers` (`TicketActivity.cs` line 15, `TicketActivityConfiguration.cs` line 23) and `ListActivityAsync` **inner-joins** the actor (`TicketRepository.cs` line 121). System actions cannot be attributed without impersonating a user. | `ActorUserId` becomes `Guid?` (`null` = Automation). Migration alters the column to `NULL` (existing rows unchanged). The activity query left-joins the actor and returns `ActorName = "Automation"` + `IsAutomatic = true`. No fake "system" Identity account is created. |
| C2 | The DASH-002 inbox is **Agent-only** (`NotificationsController.cs` line 16, `app.routes.ts` line 52, `shell.html` line 37) and its table is **task-specific** (`TaskReminderNotification.TaskId`, unique `(TaskId, ReminderGeneration)`). S7 requires a SuperAdmin inbox. | New `SlaAlerts` table + `/api/v1/sla-alerts` (**Staff** policy). The existing reminder endpoints/table stay untouched. The **same** bell and **same** `/notifications` page are extended: bell shown to all staff (badge = reminders + SLA alerts for Agents, SLA alerts for SuperAdmin), page gets an "SLA alerts" section; the reminder section stays Agent-only. No second bell or inbox. |
| C3 | Automatic assignment can only apply to tickets created **unassigned**. `CreateTicketCommand.cs` lines 64–88: Agent-created tickets are always self-assigned and SuperAdmin may pick an assignee; there is no customer ticket submission yet. | Rules run only when `AssignedAgentUserId` is `null` after the existing initial-assignee resolution, i.e. SuperAdmin-created unassigned tickets today (S5 "already-assigned tickets unchanged"). Future channels (web form, email) will reuse the same `ITicketCreationAutomation` hook. |
| C4 | Resolving an escalated ticket **auto-clears** the escalation (`Ticket.cs` lines 132–136). | Kept. The SLA breach state, due instants and alert history are independent of the escalation flag, so the breach stays visible after resolve. Documented. |
| C5 | Workflow allows `Resolved → InProgress` and `Closed → Open` (`TicketWorkflow.cs` lines 11–12). | Per **S10**: `SlaResolvedAtUtc` is stamped on the **first** transition to `Resolved` only and never cleared; `FirstRespondedAtUtc` is never cleared. Reopen/resume starts no new cycle; the worker only processes `Open`/`InProgress` tickets whose stamp is still empty. |
| C6 | Every ticket write uses `Tickets.Version` rowversion (`TicketConfiguration.cs` line 34). A worker escalation can race a user edit. | The worker processes each (rule, ticket, kind) in its own short transaction; a 409/`DbUpdateConcurrencyException` rolls back that item only, is logged, and the next poll retries it. No locks or retry loops added. |
| C7 | No background-job infrastructure exists (DASH-002 explicitly excluded hosted services). | New `SlaAutomationWorker : BackgroundService` in `Crm.Api/BackgroundJobs/`, single instance only; documented that multiple API replicas would need coordination (out of scope). |
| C8 | `CreateTicketCommandHandler` gains a dependency; the existing `CreateTicketCommandHandlerTests` constructs it directly (line 21–22). | The existing test gets one extra constructor argument (a 3-line no-op fake). This is a **compile fix to an existing test**, not a new test; its assertions are unchanged. |

### Recorded decisions

| Concern | Decision |
|---|---|
| Storage of policy and calendar | Two singleton aggregates, each row `Id = 1`: `SlaPolicies` (+ owned `SlaPolicyTargets`, PK `(SlaPolicyId, Priority)`) and `BusinessCalendars` (+ owned `BusinessHolidays`, PK `(BusinessCalendarId, Date)`). **No seed data** in the migration (S3: demo hours are not business promises). Until SuperAdmin saves both, `GET` returns `isConfigured: false` and new tickets are `Not tracked`. |
| When a ticket is tracked | At creation only (S1, S4), when the policy exists **and** `IsEnabled` **and** the calendar exists. Otherwise all SLA columns stay `NULL` ⇒ `Not tracked`. Existing tickets are never back-filled. |
| Ticket SLA snapshot | Nullable columns on `Tickets`: `SlaFirstResponseTargetMinutes`, `SlaFirstResponseDueAtUtc`, `SlaResolutionTargetMinutes`, `SlaResolutionDueAtUtc`, `SlaTimeZoneId` (calendar timezone used), `FirstRespondedAtUtc`, `FirstRespondedByUserId` (FK, Restrict), `SlaResolvedAtUtc`. Due instants never change after creation (S4). Both clocks start at `CreatedAtUtc` (shifted to the next open period by the calculator). |
| Business-time math | Pure static `Crm.Domain.Sla.BusinessTimeCalculator` over an immutable `BusinessCalendarRules` (timezone resolved with `TimeZoneInfo.FindSystemTimeZoneById`, which accepts IANA ids such as `Africa/Cairo` on Windows via ICU and on Linux). Day-by-day scan of local working windows, each converted to UTC (`ConvertTimeToUtc`; a local time inside a DST gap moves forward to the first valid minute; an ambiguous local time is interpreted as standard time). Max scan 3,660 days. Minutes are whole numbers (floor). |
| Remaining / elapsed business time (F3) | **S4 unchanged:** stored due instants never change after policy/calendar edits. Remaining/elapsed business minutes are computed at read time from the **current** calendar between `now` (server `TimeProvider`) and the **stored** due instant; negative remaining = overdue. Documented simplification, stated in the UI (SLA card, calendar form) and `docs/sla-and-automation.md` §4. **One calculation everywhere:** display (`SlaEvaluator.Evaluate`) and worker warning processing (`RunSlaAutomationCommandHandler`) both call `SlaEvaluator.RemainingBusinessMinutes` and `SlaEvaluator.IsWarningThresholdReached` with the same current calendar; no calendar ⇒ remaining `null` ⇒ no warning in either path. |
| First response (S2) | `POST /api/v1/tickets/{id}/first-response` (Staff; Agent only on own assigned ticket ⇒ others 404). Allowed when tracked, not yet recorded, status `Open`/`InProgress`. Second call ⇒ **409** "The first response was already recorded." Not tracked ⇒ **409** "This ticket has no SLA tracking." Records server UTC + actor and one `FirstResponseRecorded` activity. Sends nothing. |
| Resolution | Measured by the existing `TransitionTo(Resolved)`; first time only (C5). No new status. |
| States | Per target: `Pending` (not completed, due in future), `Met` (completed ≤ due), `Breached` (completed > due, or not completed and `now > due`). `IsWarning` (F1) = `Pending` and **any** enabled `SlaWarningDue` rule that matches the ticket and kind has reached its own threshold (remaining business minutes ≤ that rule's `WarningLeadMinutes`). Leads are never collapsed to a minimum: with matching leads 30 and 120, 100 minutes remaining is a warning. Ticket indicator: `NotTracked` (no snapshot) → `Breached` (any target breached) → `Met` (both met) → `Warning` (any warning) → `OnTrack`. |
| Rule model | `AutomationRules`: `Name` (1–100), `IsEnabled`, `EnabledAtUtc`, `Trigger`, `Action`, `SortOrder` (1–1000), `CategoryCondition?`, `PriorityCondition?`, `AssignedCondition?` (`true` assigned / `false` unassigned / `null` any), `SlaKindCondition?` (`null` = both kinds), `TargetAgentUserId?`, `WarningLeadMinutes?` (1–10,080). Enums stored by name. No delete — disable instead. |
| Valid combinations | `TicketCreated` → `AssignToAgent` (target **required**, active Agent; no lead, no kind, no assigned condition). `SlaWarningDue` → `CreateInAppNotification` (lead **required**; no target). `SlaBreached` → `CreateInAppNotification` **or** `EscalateTicket` (no lead, no target). Breach "notify + escalate" = **two simple rules** (intake §6 allows this). Anything else ⇒ 400 on the field. |
| Rule order | Ascending `SortOrder`, then `CreatedAtUtc`, then `Id`. At most one assignment per new ticket; first match wins; a rule whose target Agent is no longer active is skipped (logged) and evaluation continues. |
| "Changes affect subsequent events only" | `EnabledAtUtc` is set when a rule is created enabled or re-enabled. The worker only considers SLA events whose **due instant ≥ `EnabledAtUtc`**, so enabling a breach rule does not mass-alert old breaches; deadlines missed while the API was stopped (rule already enabled) are still caught up (S8). |
| Dedup / idempotency | `AutomationRuleExecutions` with unique index **`UX_AutomationRuleExecutions_RuleTicketKind (RuleId, TicketId, SlaKind)`** (`SlaKind` `NULL` for assignment; SQL Server treats `NULL`s as equal in a unique index ⇒ one assignment row per rule+ticket). Alerts and escalation are written in the **same** transaction as the execution row, so a crash/restart can never produce a second alert or escalation. `SlaAlerts` additionally unique `(ExecutionId, RecipientUserId)`. |
| Recipients (S7) | Warning: current assigned Agent; unassigned ⇒ every **active** SuperAdmin. Breach: current assigned Agent (if any) **plus** every active SuperAdmin. Fixed by S7; the rule form shows them as read-only text. Inactive Agents receive nothing. |
| Alert visibility | Listing/marking requires `RecipientUserId = caller` **and** current access: SuperAdmin ⇒ any ticket; Agent ⇒ `ticket.AssignedAgentUserId == caller` (same rule as `TicketRepository.Scope`). Reassigned-away alerts disappear for the former Agent; collaborator membership never grants access. |
| Worker | `SlaAutomationWorker : BackgroundService` (`Crm.Api/BackgroundJobs/`), `PeriodicTimer` with `TimeProvider`, first run immediately at startup (catch-up), then every `SlaAutomation:PollInterval` (default `00:01:00`, allowed 10 s – 1 h). `SlaAutomation:Enabled` (default `true`). Each run creates a DI scope and sends `RunSlaAutomationCommand`. Any exception is logged; the loop continues. |
| Candidate selection (F2) | Per (rule, kind): the rule's category, priority and assigned/unassigned conditions are applied **in the SQL query** together with eligibility, then ordered `due ASC, Id ASC` and limited to 200 (`WorkerBatchSize`). Within a pass the worker pages: the next page excludes ticket ids already attempted in this pass (failed or skipped), so a ticket that keeps failing never blocks the ones behind it; successful items leave the set through the execution key. Paging stops when a page is short, when (warnings) the first candidate of a page has not reached the threshold — remaining business minutes are non-decreasing in the due instant, so no later ticket can have reached it — or after `WorkerMaxPagesPerRule` (10) pages; the next pass starts again from the oldest due. |
| Transactions | `CreateTicket` stays **one `SaveChanges`** (ticket + activities + SLA columns + assignment execution row are inserted atomically, FK-ordered by EF). `RunSlaAutomationCommand` is **not** `ITransactionalCommand`; its handler calls `IUnitOfWork.ExecuteInTransactionAsync` per candidate (precedent: `IdentityBootstrapper.cs` line 60). Each attempt reloads the ticket, re-checks eligibility, then writes. |
| Routes (API) | `GET/PUT /api/v1/sla/policy`, `GET/PUT /api/v1/sla/calendar`, `GET /api/v1/sla/calendar/preview?startUtc=&minutes=`, `GET /api/v1/sla/overview` (SuperAdmin); `GET/POST /api/v1/automation-rules`, `GET/PUT /api/v1/automation-rules/{id}` (SuperAdmin); `GET /api/v1/sla-alerts`, `GET /api/v1/sla-alerts/unread-count`, `PUT /api/v1/sla-alerts/{id}/read` (Staff); `POST /api/v1/tickets/{id}/first-response` (Staff); `GET /api/v1/tickets?slaBreached=true` (Staff, new optional filter). |
| Routes (Angular) | `/sla` (SuperAdmin; tabs Policy / Calendar / Rules / Overview). `/notifications` widened to SuperAdmin + Agent. |
| Time on the wire | `DateTimeOffset` UTC as today; `TimeOnly` as `"HH:mm:ss"`, `DateOnly` as `"yyyy-MM-dd"`, working days as `DayOfWeek` names (`"Sunday"`…), all via the existing `JsonStringEnumConverter` and System.Text.Json defaults. Angular displays instants with `DatePipe` in the viewer's zone and shows the calendar timezone id next to SLA due times. |

---

## Context — Read These Files First

1. `.squad/stories/sla-automation/SLA-001/intake.md` — §1 nine capabilities, §3 role table, §4–§12 behaviour, §14 verification matrix, §16 S1–S12 are the contract.
2. [../agent-dashboard/07-story-agent-productivity-collaboration.md](../agent-dashboard/07-story-agent-productivity-collaboration.md) — "Recorded decisions" table style (lines 29–58), R1/R2 transaction rules (lines 51–52), Verification style. [../customer-management/08-story-customer-notes-interactions-attachments.md](../customer-management/08-story-customer-notes-interactions-attachments.md) — Migration task wording (§6), Migration / Rollback and Verification Steps (~lines 1052–1114).
3. `backend/src/Crm.Domain/Tickets/Ticket.cs` — private setters (15–29), `_activities` + `Record` (31–34, 176), `Create` (37–64; **signature stays unchanged**), `ChangeAssignee` (109–120), `TransitionTo` with auto-clear (123–139), `Escalate` (141–154), `ClearEscalationState` (168–174).
4. `backend/src/Crm.Domain/Tickets/TicketActivity.cs` — `ActorUserId` (15), internal factories (24–40), `New` (42–58). `TicketActivityType.cs` lines 4–13 (append `FirstResponseRecorded = 7`). `TicketPriority.cs` lines 4–10, `TicketCategory.cs` lines 4–10, `TicketStatus.cs` lines 7–13, `TicketWorkflow.cs` lines 6–17 (`CanEscalate`).
5. `backend/src/Crm.Domain/AgentTasks/TaskReminderNotification.cs` lines 3–22 — set-based, method-less notification entity with `long` identity id: the shape for `SlaAlert`. `Crm.Domain/Common/DomainException.cs` line 4.
6. `backend/src/Crm.Application/Tickets/CreateTicket/CreateTicketCommand.cs` — handler (24–58), `ResolveInitialAssigneeAsync` (64–88), `Invalid` helper (91–92). The automation hook goes between `Ticket.Create` (41–50) and `tickets.Add` (52).
7. `backend/src/Crm.Application/Tickets/EscalateTicket/EscalateTicketCommand.cs` lines 16–35 and `TransitionTicket/TransitionTicketStatusCommand.cs` lines 21–42 — load under `AgentScope` ⇒ 404, `ConflictException` for state, single save, read-back. `RecordFirstResponseCommand` copies this shape.
8. `backend/src/Crm.Application/Tickets/TicketDetails.cs` lines 9–38 (read model + `TicketListFilter`), `TicketActivityDetails.cs` lines 6–16, `TicketActor.cs` lines 7–11, `ListTickets/ListTicketsQuery.cs` lines 8–30.
9. `backend/src/Crm.Application/Abstractions/Persistence/ITicketRepository.cs` lines 7–31, `IUnitOfWork.cs` lines 7–20, `Abstractions/Identity/IIdentityService.cs` lines 24–44 (`FindByIdAsync`, `ListByRoleAsync`), `UserAccount.cs` lines 4–13 (`Role`, `IsActive`).
10. `backend/src/Crm.Application/AgentTasks/Reminders/CheckTaskReminders/CheckTaskRemindersCommand.cs`, `ListTaskReminders/ListTaskRemindersQuery.cs` (validator + `Math.Min(pageSize, Paging.MaxPageSize)`), `MarkTaskReminderRead/MarkTaskReminderReadCommand.cs` — the alert use cases mirror these.
11. `backend/src/Crm.Application/DependencyInjection.cs` lines 9–15 (`TimeProvider.System`, scoped registrations), `Common/Behaviors/TransactionBehavior.cs` lines 73–99, `Common/Validation/TicketRules.cs` (constants style).
12. `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs` — `Scope` (68–69, **do not change**), `Project` (74–101), `ListAsync` filters (19–65), `ListActivityAsync` actor join (119–135). `AgentDashboardRepository.cs` (`ListAttentionQueueAsync` lines 21–44, `ActiveAssignedTo` 47–49). `TaskReminderRepository.cs` lines 38–59 and 64–80 (`Owned` + conditional `ExecuteUpdateAsync` mark-read pattern, explicit execution strategy) and 93–96.
13. `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketConfiguration.cs` (string enums 20–22, Restrict FKs 25–31, rowversion 34, indexes 40–42), `TicketActivityConfiguration.cs` (23), `TaskReminderNotificationConfiguration.cs` (identity id 14, FKs 16–18, unique index 21). `CrmDbContext.cs` lines 19–31 (DbSets) and 37 (`ApplyConfigurationsFromAssembly`). `EfUnitOfWork.cs` lines 12–15 (`KnownUniqueIndexes` — **do not add** the new unique indexes; a violation there is a defect) and 34–90. `Infrastructure/DependencyInjection.cs` lines 37–47.
14. `backend/src/Crm.Infrastructure/Identity/IdentityBootstrapper.cs` line 60 — direct `ExecuteInTransactionAsync` use outside a command (precedent for the worker handler).
15. `backend/src/Crm.Api/Controllers/TicketsController.cs` — attributes (23–26), list binding (28–45), escalation actions (85–94), `Actor` (107). `NotificationsController.cs` lines 14–44. `AgentsController.cs` lines 15–26. `Contracts/Tickets/TicketResponse.cs` lines 7–33, `TicketActivityResponse.cs` lines 7–21, `Contracts/AgentDashboard/AgentDashboardResponse.cs` lines 23–37, `Contracts/Notifications/TaskReminderResponses.cs`.
16. `backend/src/Crm.Api/Program.cs` lines 10–13 and 15–21, `DependencyInjection.cs` lines 15–18 (string enums) and 48–58 (Mediator scans `Crm.Application`), `Authentication/AuthorizationPolicies.cs` lines 3–9, `Authentication/SecurityRegistration.cs` lines 63–66, `Errors/GlobalExceptionHandler.cs` lines 20–76, `appsettings.json`.
17. `backend/tests/Crm.UnitTests/Tickets/CreateTicketCommandHandlerTests.cs` (whole file, 104 lines: fakes 50–103) and `TransitionTicketStatusCommandHandlerTests.cs` lines 11–50 (`FixedTimeProvider`, `ActorUserId` asserts at 46 and 50 — must still compile after C1). `Crm.UnitTests.csproj` references only `Crm.Application`.
18. Frontend shell + notifications: `frontend/src/app/app.routes.ts` lines 22–66, `core/layout/navigation.ts` lines 13–23, `core/layout/shell/shell.html` lines 36–39, `shell.ts` lines 32–39, `core/notifications/notification-bell.ts` (whole, 69 lines), `notifications-api.ts`, `notification-models.ts`, `reminder-badge-refresh.ts`, `features/notifications/notifications-page.ts` (whole) and `.html` (whole).
19. Frontend tickets: `features/tickets/ticket-models.ts` (`Ticket` 14–35, `TicketListParams` 37–47, `TicketActivityType` 80–87, `TicketActivity` 90–101), `tickets-api.ts` (23–45 list params, 67–73 escalation calls), `tickets-api.spec.ts` (spec pattern), `ticket-detail-page.ts` (signals 74–100, `run` 199–218), `ticket-detail-page.html` (Summary card 11–47, escalation line 25, Workflow card 49–91), `ticket-activity-list.ts` `describeActivity` (14–38, exhaustive switch), `ticket-list-page.ts` (columns 66, filters 74–80, `hasFilters` 130, request 150), `ticket-list-page.html` (escalation filter 43–50, status column 98–106), `ticket-workflow.ts` (28–41; **do not change `ticketActions`' parameter type** — `ticket-workflow.spec.ts` passes a 3-field literal).
20. Frontend dashboard / shared: `features/agent-dashboard/agent-dashboard-models.ts` lines 13–24, `agent-dashboard-page.html` lines 73–86, `shared/ui/status-badge/status-badge.ts` (`StatusTone`), `shared/ui/page-header/page-header.ts`, `features/agents/agents-api.ts` line 12 + `agent-models.ts` lines 2–10, `core/api/describe-http-error.ts`.
21. Docs: `docs/ticket-management.md` (§5 Escalation line 65, §6 Activity history line 74, §9 API line 115, §12 Future stories line 144), `docs/agent-productivity-and-collaboration.md` §4 Reminders (line 61), `docs/persistence-and-transactions.md` (line 8), `README.md` line 3.

---

## Product rules (from story)

### Role matrix (server-enforced)

| Operation | SuperAdmin | Agent | Customer |
|---|---|---|---|
| SLA data on ticket list / details / dashboard | any ticket | only tickets currently assigned (existing scope; others 404) | 403 |
| `POST /tickets/{id}/first-response` | any tracked active ticket | own assigned ticket only (others 404) | 403 |
| `/sla/*` (policy, calendar, preview, overview) | yes | 403 | 403 |
| `/automation-rules/*` | yes | 403 | 403 |
| `/sla-alerts/*` | own alerts | own alerts for tickets **currently** assigned to them | 403 |
| Manual reassignment | unchanged (TICK-002) | unchanged (never) | 403 |
| Cause automated assignment / escalation | via enabled rules only | never directly | never |

### Current vs new behaviour

| Area | Current | New |
|---|---|---|
| Ticket creation | Unassigned SuperAdmin tickets stay unassigned | First matching enabled `TicketCreated` rule may assign an active Agent; SLA deadlines persisted when SLA is configured and enabled |
| Activity history | Every event has a human actor | `ActorUserId` null = **Automation** (`AssigneeChanged` with reason, `Escalated`); new `FirstResponseRecorded` event |
| Resolve | Status change (+ escalation auto-clear) | Also stamps the first resolution time for SLA (once) |
| Bell / Notifications | Agent-only, task reminders | All staff; SLA alerts for SuperAdmin + Agents, reminders for Agents (unchanged) |
| Ticket list | No SLA | SLA column + "SLA breached" filter; counts/order of DASH-001 unchanged |

### Calendar example (explanation only, verified by the unit test)

`Africa/Cairo`, Sunday–Thursday 09:00–17:00, holiday Sunday 2030-01-06 (example). Ticket created **Thursday 2030-01-03 16:00 local** (14:00 UTC) with a 240-minute target: Thursday contributes 60 min; Friday/Saturday closed; Sunday is a holiday; Monday 2030-01-07 contributes 180 min ⇒ due **Monday 12:00 local = 10:00 UTC**. Without the holiday it would be Sunday 12:00 (intake §5 example).

---

## Backend Tasks

### 1 — Domain: SLA (`backend/src/Crm.Domain/Sla/`)

**Create file: `backend/src/Crm.Domain/Sla/SlaKind.cs`**

```csharp
namespace Crm.Domain.Sla;

/// <summary>The two measured targets. Stored by name.</summary>
public enum SlaKind
{
    FirstResponse = 0,
    Resolution = 1,
}
```

**Create file: `backend/src/Crm.Domain/Sla/SlaPolicy.cs`** — singleton aggregate (`Id = 1`), owned targets.

```csharp
using Crm.Domain.Common;
using Crm.Domain.Tickets;

namespace Crm.Domain.Sla;

/// <summary>
/// SLA-001 S1: the one global policy (row Id 1). Targets are business minutes per existing TicketPriority — exactly one per member.
/// Changes affect tickets created afterwards only (S4): tickets copy their targets and due instants at creation.
/// </summary>
public sealed class SlaPolicy
{
    public const int SingletonId = 1;
    private SlaPolicy() { } // EF Core

    private readonly List<SlaTarget> _targets = [];
    public int Id { get; private set; }
    public bool IsEnabled { get; private set; }
    public IReadOnlyCollection<SlaTarget> Targets => _targets;
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }

    public static SlaPolicy Create(bool isEnabled, IReadOnlyList<SlaTarget> targets, Guid actorUserId, DateTimeOffset now)
    {
        var policy = new SlaPolicy { Id = SingletonId };
        policy.Update(isEnabled, targets, actorUserId, now);
        return policy;
    }

    public void Update(bool isEnabled, IReadOnlyList<SlaTarget> targets, Guid actorUserId, DateTimeOffset now)
    {
        if (targets.Count != Enum.GetValues<TicketPriority>().Length
            || targets.Select(t => t.Priority).Distinct().Count() != targets.Count
            || targets.Any(t => t.FirstResponseMinutes <= 0 || t.ResolutionMinutes <= 0))
        {
            throw new DomainException("Provide one positive first-response and resolution target for every priority.");
        }

        IsEnabled = isEnabled;
        _targets.Clear();
        _targets.AddRange(targets);
        UpdatedAtUtc = now;
        UpdatedByUserId = actorUserId;
    }

    public SlaTarget TargetFor(TicketPriority priority) => _targets.Single(t => t.Priority == priority);
}
```

**Create file: `backend/src/Crm.Domain/Sla/SlaTarget.cs`** — `public sealed record SlaTarget(TicketPriority Priority, int FirstResponseMinutes, int ResolutionMinutes);` (owned type; EF maps a record with a matching constructor). If EF rejects the positional record as an owned type, use a `sealed class` with `private` setters, a private parameterless ctor and the same three properties.

**Create file: `backend/src/Crm.Domain/Sla/BusinessHoliday.cs`** — `public sealed record BusinessHoliday(DateOnly Date, string Name);` (same fallback rule).

**Create file: `backend/src/Crm.Domain/Sla/BusinessCalendar.cs`** — singleton aggregate (`Id = 1`):

```csharp
public sealed class BusinessCalendar
{
    public const int SingletonId = 1;
    private BusinessCalendar() { }

    private readonly List<BusinessHoliday> _holidays = [];
    public int Id { get; private set; }
    public string TimeZoneId { get; private set; } = string.Empty;
    /// <summary>Bit (1 << (int)DayOfWeek) per working day; Sunday = bit 0.</summary>
    public int WorkingDaysMask { get; private set; }
    public TimeOnly OpensAt { get; private set; }
    public TimeOnly ClosesAt { get; private set; }
    public IReadOnlyCollection<BusinessHoliday> Holidays => _holidays;
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }

    public static BusinessCalendar Create(...same args as Update...) { var c = new BusinessCalendar { Id = SingletonId }; c.Update(...); return c; }

    /// <summary>Throws DomainException when: unknown timezone, no working day, OpensAt >= ClosesAt, duplicate holiday dates.</summary>
    public void Update(string timeZoneId, IReadOnlyCollection<DayOfWeek> workingDays, TimeOnly opensAt, TimeOnly closesAt,
        IReadOnlyList<BusinessHoliday> holidays, Guid actorUserId, DateTimeOffset now) { ... }

    public IReadOnlySet<DayOfWeek> WorkingDays => Enum.GetValues<DayOfWeek>().Where(d => (WorkingDaysMask & (1 << (int)d)) != 0).ToHashSet();

    public BusinessCalendarRules ToRules() =>
        new(TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId), WorkingDays, OpensAt, ClosesAt, _holidays.Select(h => h.Date).ToHashSet());
}
```

Validate the timezone in `Update` with `TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _)`. `WorkingDays` is a computed property — mark it `Ignore` in the EF configuration (task 5).

**Create file: `backend/src/Crm.Domain/Sla/BusinessCalendarRules.cs`** — immutable value used by all math:

```csharp
namespace Crm.Domain.Sla;

/// <summary>Resolved calendar. Local windows are [date + OpensAt, date + ClosesAt) on working, non-holiday local dates.</summary>
public sealed record BusinessCalendarRules(
    TimeZoneInfo TimeZone, IReadOnlySet<DayOfWeek> WorkingDays, TimeOnly OpensAt, TimeOnly ClosesAt, IReadOnlySet<DateOnly> Holidays)
{
    public bool TryGetWindowUtc(DateOnly localDate, out DateTimeOffset openUtc, out DateTimeOffset closeUtc)
    {
        openUtc = closeUtc = default;
        if (!WorkingDays.Contains(localDate.DayOfWeek) || Holidays.Contains(localDate))
        {
            return false;
        }

        openUtc = ToUtc(localDate, OpensAt);
        closeUtc = ToUtc(localDate, ClosesAt);
        return closeUtc > openUtc;
    }

    public DateOnly LocalDateOf(DateTimeOffset utc) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(utc, TimeZone).DateTime);

    // DST gap: move forward to the first valid local minute. Ambiguous (fall back): ConvertTimeToUtc uses standard time.
    private DateTimeOffset ToUtc(DateOnly date, TimeOnly time)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        while (TimeZone.IsInvalidTime(local))
        {
            local = local.AddMinutes(1);
        }

        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(local, TimeZone), TimeSpan.Zero);
    }
}
```

**Create file: `backend/src/Crm.Domain/Sla/BusinessTimeCalculator.cs`**

```csharp
using Crm.Domain.Common;

namespace Crm.Domain.Sla;

/// <summary>SLA-001 §5. Pure functions; every input/output instant is UTC. Nights, closed weekdays and holidays never count.</summary>
public static class BusinessTimeCalculator
{
    public const int MaxDaysScanned = 3660;

    /// <summary>startUtc + minutes of business time. A start outside working hours begins at the next open period. minutes > 0.</summary>
    public static DateTimeOffset AddBusinessMinutes(DateTimeOffset startUtc, int minutes, BusinessCalendarRules calendar)
    {
        var remaining = TimeSpan.FromMinutes(minutes);
        var date = calendar.LocalDateOf(startUtc);
        for (var i = 0; i < MaxDaysScanned; i++, date = date.AddDays(1))
        {
            if (!calendar.TryGetWindowUtc(date, out var open, out var close))
            {
                continue;
            }

            var from = startUtc > open ? startUtc : open;
            if (from >= close)
            {
                continue;
            }

            var available = close - from;
            if (available >= remaining)
            {
                return from + remaining;
            }

            remaining -= available;
        }

        throw new DomainException("The business calendar has no working time in the next ten years.");
    }

    /// <summary>Whole business minutes in [fromUtc, toUtc); 0 when toUtc <= fromUtc.</summary>
    public static int BusinessMinutesBetween(DateTimeOffset fromUtc, DateTimeOffset toUtc, BusinessCalendarRules calendar)
    {
        if (toUtc <= fromUtc) return 0;
        var total = TimeSpan.Zero;
        var date = calendar.LocalDateOf(fromUtc);
        var last = calendar.LocalDateOf(toUtc);
        for (var i = 0; i < MaxDaysScanned && date <= last; i++, date = date.AddDays(1))
        {
            if (!calendar.TryGetWindowUtc(date, out var open, out var close)) continue;
            var start = fromUtc > open ? fromUtc : open;
            var end = toUtc < close ? toUtc : close;
            if (end > start) total += end - start;
        }

        return (int)Math.Floor(total.TotalMinutes);
    }

    /// <summary>Positive = business minutes left until dueUtc; negative = business minutes overdue.</summary>
    public static int SignedBusinessMinutesUntil(DateTimeOffset nowUtc, DateTimeOffset dueUtc, BusinessCalendarRules calendar) =>
        nowUtc <= dueUtc ? BusinessMinutesBetween(nowUtc, dueUtc, calendar) : -BusinessMinutesBetween(dueUtc, nowUtc, calendar);
}
```

(Format the braces like the rest of the repo — multi-line `if` blocks; the compressed lines above are for brevity.)

### 2 — Domain: automation (`backend/src/Crm.Domain/Automation/`)

**Create files** (all enums "Stored by name", one per file like `TicketPriority.cs`):

- `AutomationTrigger.cs` — `TicketCreated = 0, SlaWarningDue = 1, SlaBreached = 2`.
- `AutomationAction.cs` — `AssignToAgent = 0, CreateInAppNotification = 1, EscalateTicket = 2`.
- `AutomationOutcome.cs` — `Assigned = 0, Notified = 1, Escalated = 2, AlreadyEscalated = 3, NotEscalatable = 4`.
- `SlaAlertType.cs` — `Warning = 0, Breach = 1`.

**Create file: `backend/src/Crm.Domain/Automation/AutomationRule.cs`**

```csharp
using Crm.Domain.Common;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Domain.Automation;

/// <summary>
/// SLA-001 S9: one fixed trigger → optional fixed conditions → one fixed action. Combination validity is enforced here
/// (DomainException) and, field by field, by the Application validators (400). Never deleted — disabled instead.
/// </summary>
public sealed class AutomationRule
{
    private AutomationRule() { }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsEnabled { get; private set; }
    /// <summary>Last time the rule was created enabled or re-enabled. SLA events due before it are ignored ("subsequent events only").</summary>
    public DateTimeOffset? EnabledAtUtc { get; private set; }
    public AutomationTrigger Trigger { get; private set; }
    public AutomationAction Action { get; private set; }
    public int SortOrder { get; private set; }
    public TicketCategory? CategoryCondition { get; private set; }
    public TicketPriority? PriorityCondition { get; private set; }
    public bool? AssignedCondition { get; private set; }
    public SlaKind? SlaKindCondition { get; private set; }
    public Guid? TargetAgentUserId { get; private set; }
    public int? WarningLeadMinutes { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }

    public static AutomationRule Create(Guid id, AutomationRuleDefinition definition, Guid actorUserId, DateTimeOffset now) { ... CreatedAtUtc = now; Apply(...) }
    public void Update(AutomationRuleDefinition definition, Guid actorUserId, DateTimeOffset now) => Apply(definition, actorUserId, now);

    /// <summary>Pure condition check, shared by ticket creation, the worker and the SLA display (warning lead).</summary>
    public bool Matches(TicketCategory category, TicketPriority priority, bool isAssigned, SlaKind? kind) =>
        (CategoryCondition is null || CategoryCondition == category)
        && (PriorityCondition is null || PriorityCondition == priority)
        && (AssignedCondition is null || AssignedCondition == isAssigned)
        && (SlaKindCondition is null || kind is null || SlaKindCondition == kind);

    public static bool IsValidCombination(AutomationTrigger trigger, AutomationAction action) => (trigger, action) switch
    {
        (AutomationTrigger.TicketCreated, AutomationAction.AssignToAgent) => true,
        (AutomationTrigger.SlaWarningDue, AutomationAction.CreateInAppNotification) => true,
        (AutomationTrigger.SlaBreached, AutomationAction.CreateInAppNotification) => true,
        (AutomationTrigger.SlaBreached, AutomationAction.EscalateTicket) => true,
        _ => false,
    };

    private void Apply(AutomationRuleDefinition d, Guid actorUserId, DateTimeOffset now)
    {
        if (!IsValidCombination(d.Trigger, d.Action)) throw new DomainException($"{d.Trigger} cannot run {d.Action}.");
        // Parameter shape per combination (validator already returned 400 for user input; this is the invariant):
        //   AssignToAgent ⇔ TargetAgentUserId set; SlaWarningDue ⇔ WarningLeadMinutes set;
        //   TicketCreated ⇒ AssignedCondition and SlaKindCondition null.
        if (d.IsEnabled && (!IsEnabled || EnabledAtUtc is null)) EnabledAtUtc = now;
        Name = d.Name; IsEnabled = d.IsEnabled; Trigger = d.Trigger; Action = d.Action; SortOrder = d.SortOrder;
        CategoryCondition = d.CategoryCondition; PriorityCondition = d.PriorityCondition; AssignedCondition = d.AssignedCondition;
        SlaKindCondition = d.SlaKindCondition; TargetAgentUserId = d.TargetAgentUserId; WarningLeadMinutes = d.WarningLeadMinutes;
        UpdatedAtUtc = now; UpdatedByUserId = actorUserId;
    }
}

public sealed record AutomationRuleDefinition(
    string Name, bool IsEnabled, AutomationTrigger Trigger, AutomationAction Action, int SortOrder,
    TicketCategory? CategoryCondition, TicketPriority? PriorityCondition, bool? AssignedCondition, SlaKind? SlaKindCondition,
    Guid? TargetAgentUserId, int? WarningLeadMinutes);
```

**Create file: `backend/src/Crm.Domain/Automation/AutomationRuleExecution.cs`** — append-only processing record, `long` identity id, static `Create(Guid ruleId, Guid ticketId, SlaKind? kind, AutomationTrigger trigger, AutomationAction action, AutomationOutcome outcome, string? detail, DateTimeOffset now)`. Properties: `Id`, `RuleId`, `TicketId`, `SlaKind`, `Trigger`, `Action`, `Outcome`, `Detail` (≤ 300), `OccurredAtUtc`. Doc comment: "Unique (RuleId, TicketId, SlaKind) — the dedup key for every automated effect (S8). Inserted in the same save/transaction as its effect."

**Create file: `backend/src/Crm.Domain/Automation/SlaAlert.cs`** — `long Id`, `long ExecutionId`, `Guid TicketId`, `Guid RecipientUserId`, `SlaAlertType Type`, `SlaKind Kind`, `DateTimeOffset DueAtUtc`, `DateTimeOffset CreatedAtUtc`, `DateTimeOffset? ReadAtUtc`; static `Create(AutomationRuleExecution execution, Guid recipientUserId, SlaAlertType type, SlaKind kind, DateTimeOffset dueAtUtc, DateTimeOffset now)`; no other methods (read state changes only via a conditional set-based update, like `TaskReminderNotification`). Because `ExecutionId` is store-generated, configure the FK with a navigation-less relationship and set it through EF: give `SlaAlert` a private `AutomationRuleExecution? Execution` navigation assigned in `Create` (EF fixes up `ExecutionId` on save). Do not expose it publicly.

### 3 — Domain: ticket changes

**File: `backend/src/Crm.Domain/Tickets/TicketActivityType.cs`** — append `FirstResponseRecorded = 7,` after line 12.

**File: `backend/src/Crm.Domain/Tickets/TicketActivity.cs`**

- Line 15: `public Guid? ActorUserId { get; private set; }` and update the class doc (lines 3–7): "ActorUserId null = performed by SLA-001 automation (no user is impersonated)."
- Line 43 (`New`): parameter `Guid? actorUserId`. Existing factories keep their `Guid actorUserId` parameters (implicit conversion).
- Add factories after line 40:

```csharp
    internal static TicketActivity AutoAssigned(Guid ticketId, DateTimeOffset now, Guid toAgentUserId, string reason) =>
        New(ticketId, TicketActivityType.AssigneeChanged, null, now, fromAgentUserId: null, toAgentUserId: toAgentUserId, reason: reason);

    internal static TicketActivity AutoEscalated(Guid ticketId, DateTimeOffset now, string reason) =>
        New(ticketId, TicketActivityType.Escalated, null, now, reason: reason);

    internal static TicketActivity FirstResponseRecorded(Guid ticketId, Guid actorUserId, DateTimeOffset now) =>
        New(ticketId, TicketActivityType.FirstResponseRecorded, actorUserId, now);
```

**File: `backend/src/Crm.Domain/Tickets/Ticket.cs`**

- Add `using Crm.Domain.Sla;`. After line 29 add the SLA snapshot properties (all `private set`, nullable):

```csharp
    // SLA-001 snapshot, written once at creation (S1/S4). All null = not tracked (pre-SLA tickets or SLA disabled/not configured).
    public int? SlaFirstResponseTargetMinutes { get; private set; }
    public DateTimeOffset? SlaFirstResponseDueAtUtc { get; private set; }
    public int? SlaResolutionTargetMinutes { get; private set; }
    public DateTimeOffset? SlaResolutionDueAtUtc { get; private set; }
    public string? SlaTimeZoneId { get; private set; }
    public DateTimeOffset? FirstRespondedAtUtc { get; private set; }
    public Guid? FirstRespondedByUserId { get; private set; }
    /// <summary>First transition to Resolved only (S10); never cleared by resume/reopen.</summary>
    public DateTimeOffset? SlaResolvedAtUtc { get; private set; }
    public bool IsSlaTracked => SlaFirstResponseDueAtUtc is not null;
```

- Add methods after `ChangeAssignee` (line 120):

```csharp
    /// <summary>Called once, before the first save, by ticket-creation automation. Due instants are already business-time adjusted.</summary>
    public void StartSla(int firstResponseMinutes, DateTimeOffset firstResponseDueAtUtc, int resolutionMinutes, DateTimeOffset resolutionDueAtUtc, string timeZoneId)
    {
        if (IsSlaTracked) throw new DomainException("SLA tracking has already started for this ticket.");
        SlaFirstResponseTargetMinutes = firstResponseMinutes; SlaFirstResponseDueAtUtc = firstResponseDueAtUtc;
        SlaResolutionTargetMinutes = resolutionMinutes; SlaResolutionDueAtUtc = resolutionDueAtUtc; SlaTimeZoneId = timeZoneId;
    }

    /// <summary>Rule-based assignment of an unassigned ticket (S5). The caller has verified an active Agent. Recorded as Automation.</summary>
    public void AssignAutomatically(Guid agentUserId, string ruleName, DateTimeOffset now)
    {
        if (AssignedAgentUserId is not null) throw new DomainException("Only an unassigned ticket can be assigned automatically.");
        Record(TicketActivity.AutoAssigned(Id, now, agentUserId, $"Assigned automatically by rule \"{ruleName}\"."));
        AssignedAgentUserId = agentUserId;
        UpdatedAtUtc = now;
    }

    /// <summary>S2 manual acknowledgement. false = already recorded (only the first counts). Sends nothing.</summary>
    public bool RecordFirstResponse(Guid actorUserId, DateTimeOffset now)
    {
        if (!IsSlaTracked || !TicketWorkflow.CanEscalate(Status)) throw new DomainException("A first response can be recorded only on an active SLA-tracked ticket.");
        if (FirstRespondedAtUtc is not null) return false;
        FirstRespondedAtUtc = now; FirstRespondedByUserId = actorUserId;
        Record(TicketActivity.FirstResponseRecorded(Id, actorUserId, now));
        UpdatedAtUtc = now;
        return true;
    }

    /// <summary>S6 escalation by a breach rule, attributed to Automation (EscalatedByUserId null). Same preconditions as Escalate.</summary>
    public void EscalateAutomatically(string reason, DateTimeOffset now)
    {
        if (IsEscalated || !TicketWorkflow.CanEscalate(Status)) throw new DomainException("Only an Open or In Progress ticket that is not already escalated can be escalated.");
        IsEscalated = true; EscalationReason = reason; EscalatedAtUtc = now; EscalatedByUserId = null;
        Record(TicketActivity.AutoEscalated(Id, now, reason));
        UpdatedAtUtc = now;
    }
```

(Reusing `TicketWorkflow.CanEscalate` for "Open or InProgress" is deliberate — it is the existing "active work" predicate, `TicketWorkflow.cs` line 17.)

- In `TransitionTo` (lines 123–139), after `Status = status;` (line 131) add:

```csharp
        if (status == TicketStatus.Resolved && IsSlaTracked && SlaResolvedAtUtc is null)
        {
            SlaResolvedAtUtc = now; // first resolution only (S10)
        }
```

**Do not** change `Create`'s signature (used by `TransitionTicketStatusCommandHandlerTests.cs` line 16).

### 4 — Application: contracts, SLA evaluation, creation automation

**Create file: `backend/src/Crm.Application/Common/Validation/SlaRules.cs`**

```csharp
namespace Crm.Application.Common.Validation;

public static class SlaRules
{
    public const int MaxTargetMinutes = 525_600;      // one year of business minutes
    public const int MaxWarningLeadMinutes = 10_080;
    public const int RuleNameMaxLength = 100;
    public const int MaxSortOrder = 1000;
    public const int TimeZoneIdMaxLength = 64;
    public const int HolidayNameMaxLength = 100;
    public const int MaxHolidays = 366;
    public const int MaxPreviewMinutes = 525_600;
    public const int WorkerBatchSize = 200;
    public const int WorkerMaxPagesPerRule = 10; // per (rule, kind) per pass ⇒ ≤ 2,000 candidates; the rest next pass
    public const int OverviewListLimit = 50;
    public const int RecentExecutionsLimit = 20;
    public const string AutomationActorName = "Automation";
}
```

**Create file: `backend/src/Crm.Application/Sla/SlaViews.cs`**

```csharp
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.Sla;

/// <summary>Raw persisted SLA columns of one ticket (projected in SQL). Null on TicketDetails when the ticket is not tracked.</summary>
public sealed record TicketSlaSnapshot(
    DateTimeOffset CreatedAtUtc, string TimeZoneId,
    int FirstResponseTargetMinutes, DateTimeOffset FirstResponseDueAtUtc, DateTimeOffset? FirstRespondedAtUtc, string? FirstRespondedByName,
    int ResolutionTargetMinutes, DateTimeOffset ResolutionDueAtUtc, DateTimeOffset? ResolvedAtUtc);

public sealed record TicketSlaInput(TicketCategory Category, TicketPriority Priority, bool IsAssigned, TicketSlaSnapshot Snapshot);

public enum SlaIndicator { NotTracked = 0, OnTrack = 1, Warning = 2, Breached = 3, Met = 4 }
public enum SlaTargetState { Pending = 0, Met = 1, Breached = 2 }

/// <summary>BusinessMinutesRemaining: positive = left, negative = overdue (only for Pending/Breached-without-completion; null once completed).</summary>
public sealed record SlaTargetView(
    SlaKind Kind, int TargetMinutes, DateTimeOffset DueAtUtc, DateTimeOffset? CompletedAtUtc, string? CompletedByName,
    SlaTargetState State, bool IsWarning, int? BusinessMinutesRemaining, int BusinessMinutesElapsed);

public sealed record TicketSlaView(SlaIndicator Indicator, string TimeZoneId, SlaTargetView FirstResponse, SlaTargetView Resolution);

/// <summary>Loaded once per request/run: calendar rules (null = no calendar ⇒ no business minutes, no warnings), enabled SlaWarningDue rules, server now.</summary>
public sealed record SlaEvaluationContext(BusinessCalendarRules? Calendar, IReadOnlyList<Domain.Automation.AutomationRule> WarningRules, DateTimeOffset Now);
```

**Create file: `backend/src/Crm.Application/Sla/SlaEvaluator.cs`** — the **one** shared evaluation service (intake §11). Scoped; deps `ISlaConfigurationRepository`, `IAutomationRuleRepository`, `TimeProvider`.

```csharp
public sealed class SlaEvaluator(ISlaConfigurationRepository configuration, IAutomationRuleRepository rules, TimeProvider timeProvider)
{
    public async Task<SlaEvaluationContext> CreateContextAsync(CancellationToken cancellationToken)
    {
        var calendar = await configuration.FindCalendarAsync(cancellationToken);
        var warningRules = await rules.ListEnabledAsync(AutomationTrigger.SlaWarningDue, cancellationToken);
        return new SlaEvaluationContext(calendar?.ToRules(), warningRules, timeProvider.GetUtcNow());
    }

    public static TicketSlaView Evaluate(TicketSlaInput input, SlaEvaluationContext context)
    {
        var s = input.Snapshot;
        var first = Target(SlaKind.FirstResponse, s.FirstResponseTargetMinutes, s.FirstResponseDueAtUtc, s.FirstRespondedAtUtc, s.FirstRespondedByName, input, context);
        var resolution = Target(SlaKind.Resolution, s.ResolutionTargetMinutes, s.ResolutionDueAtUtc, s.ResolvedAtUtc, null, input, context);
        var indicator =
            first.State == SlaTargetState.Breached || resolution.State == SlaTargetState.Breached ? SlaIndicator.Breached
            : first.State == SlaTargetState.Met && resolution.State == SlaTargetState.Met ? SlaIndicator.Met
            : first.IsWarning || resolution.IsWarning ? SlaIndicator.Warning
            : SlaIndicator.OnTrack;
        return new TicketSlaView(indicator, s.TimeZoneId, first, resolution);
    }

    /// <summary>F3: the ONE remaining-time rule, shared by the display and the worker. Current calendar, stored due instant.
    /// Positive = business minutes left, negative = overdue, null = no calendar.</summary>
    public static int? RemainingBusinessMinutes(DateTimeOffset now, DateTimeOffset due, BusinessCalendarRules? calendar) =>
        calendar is null ? null : BusinessTimeCalculator.SignedBusinessMinutesUntil(now, due, calendar);

    /// <summary>F1: a rule's own threshold — evaluated per rule, never against a minimum or maximum across rules.</summary>
    public static bool IsWarningThresholdReached(AutomationRule rule, int? remainingBusinessMinutes) =>
        remainingBusinessMinutes is { } remaining && remaining >= 0 && rule.WarningLeadMinutes is { } lead && remaining <= lead;

    // State from instants only (calendar-independent). Business minutes need the calendar; when it is missing, elapsed = 0, remaining = null.
    private static SlaTargetView Target(SlaKind kind, int minutes, DateTimeOffset due, DateTimeOffset? completed, string? by,
        TicketSlaInput input, SlaEvaluationContext context) { ... }
}
```

`Target` rules: `State = completed is { } c ? (c <= due ? Met : Breached) : (context.Now > due ? Breached : Pending)`. `BusinessMinutesElapsed = BusinessMinutesBetween(CreatedAtUtc, completed ?? Now)`. `BusinessMinutesRemaining = completed is null ? RemainingBusinessMinutes(Now, due, Calendar) : null`. `IsWarning = State == Pending && context.WarningRules.Any(r => r.Matches(input.Category, input.Priority, input.IsAssigned, kind) && IsWarningThresholdReached(r, remaining))` — **any** matching rule at its own threshold (F1); do **not** use `Min`/`Max` over leads.

**Create file: `backend/src/Crm.Application/Sla/ITicketCreationAutomation.cs`** + **`TicketCreationAutomation.cs`**

```csharp
/// <summary>SLA-001 hook run by CreateTicketCommandHandler after Ticket.Create and before the single save.
/// Stages the SLA snapshot (S1) and at most one rule-based assignment (S5) on the same unit of work.</summary>
public interface ITicketCreationAutomation
{
    Task ApplyAsync(Ticket ticket, CancellationToken cancellationToken);
}

public sealed class TicketCreationAutomation(
    ISlaConfigurationRepository configuration,
    IAutomationRuleRepository rules,
    IIdentityService identity,
    TimeProvider timeProvider) : ITicketCreationAutomation
{
    public async Task ApplyAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        var policy = await configuration.FindPolicyAsync(cancellationToken);
        var calendar = await configuration.FindCalendarAsync(cancellationToken);
        if (policy is { IsEnabled: true } && calendar is not null)
        {
            var target = policy.TargetFor(ticket.Priority);
            var rulesForCalendar = calendar.ToRules();
            ticket.StartSla(
                target.FirstResponseMinutes, BusinessTimeCalculator.AddBusinessMinutes(ticket.CreatedAtUtc, target.FirstResponseMinutes, rulesForCalendar),
                target.ResolutionMinutes, BusinessTimeCalculator.AddBusinessMinutes(ticket.CreatedAtUtc, target.ResolutionMinutes, rulesForCalendar),
                calendar.TimeZoneId);
        }

        if (ticket.AssignedAgentUserId is not null)
        {
            return; // S5: a chosen or self assignment is never overridden
        }

        foreach (var rule in await rules.ListEnabledAsync(AutomationTrigger.TicketCreated, cancellationToken)) // ordered SortOrder, CreatedAtUtc, Id
        {
            if (!rule.Matches(ticket.Category, ticket.Priority, isAssigned: false, kind: null) || rule.TargetAgentUserId is not { } agentId)
            {
                continue;
            }

            var agent = await identity.FindByIdAsync(agentId, cancellationToken);
            if (agent is not { Role: UserRoles.Agent, IsActive: true })
            {
                continue; // inactive/removed target: skip, never assign a disabled account
            }

            var now = timeProvider.GetUtcNow();
            ticket.AssignAutomatically(agentId, rule.Name, now);
            rules.AddExecution(AutomationRuleExecution.Create(
                rule.Id, ticket.Id, null, rule.Trigger, rule.Action, AutomationOutcome.Assigned, $"Assigned to {agent.FirstName} {agent.LastName}", now));
            return; // at most one automatic assignment per ticket
        }
    }
}
```

**File: `backend/src/Crm.Application/Tickets/CreateTicket/CreateTicketCommand.cs`**

- Add primary-ctor parameter `ITicketCreationAutomation automation` after `IIdentityService identity` (line 27).
- Between `Ticket.Create(...)` (ends line 50) and `tickets.Add(ticket);` (line 52) insert `await automation.ApplyAsync(ticket, cancellationToken);`.
- Update the class summary (lines 11–14): "Single save — not transactional: SLA snapshot and an optional automatic assignment (SLA-001) are staged on the same unit of work, so they commit with the ticket or not at all."
- Line 55–57 comment: the read-back still uses the caller's scope. An Agent's ticket is self-assigned so automation never moves it out of the Agent's scope; SuperAdmin is unrestricted.

**File: `backend/src/Crm.Application/Tickets/TicketDetails.cs`** — append an optional last parameter to `TicketDetails` (line 29): `string? EscalatedByName,\n    TicketSlaView? Sla = null);` (`using Crm.Application.Sla;`). Default keeps both existing test fakes (`CreateTicketCommandHandlerTests.cs` line 58, `TransitionTicketStatusCommandHandlerTests.cs` line 68) compiling. Add `bool? SlaBreached = null` as the last parameter of `TicketListFilter` (line 38) with doc "true = at least one SLA target breached (SQL on stored due instants)".

**File: `backend/src/Crm.Application/Tickets/TicketActivityDetails.cs`** — append `bool IsAutomatic` as the last parameter; doc: "IsAutomatic = performed by SLA-001 automation; ActorName is then \"Automation\"."

**File: `backend/src/Crm.Application/Tickets/ListTickets/ListTicketsQuery.cs`** — add `bool? SlaBreached = null` after `Escalated` (line 15) … keep `Page`/`PageSize` last: final order `(Actor, Search, CustomerId, Status, Category, Priority, Escalated, SlaBreached, Page = 1, PageSize = …)`; pass it into `TicketListFilter` (line 26). `ListTicketsValidator.cs` needs no change.

**File: `backend/src/Crm.Application/AgentDashboard/AgentDashboardSnapshot.cs`** — append `SlaIndicator SlaIndicator = SlaIndicator.NotTracked` as the last parameter of `AttentionTicket` (line 48).

**Create file: `backend/src/Crm.Application/Tickets/RecordFirstResponse/RecordFirstResponseCommand.cs`**

```csharp
/// <summary>SLA-001 S2. SuperAdmin (any ticket) or the currently assigned Agent (others 404). Internal tracking only — sends nothing.
/// Single save — not transactional; Tickets.Version turns a concurrent reassignment into 409.</summary>
public sealed record RecordFirstResponseCommand(TicketActor Actor, Guid Id) : ICommand<TicketDetails>;

public sealed class RecordFirstResponseCommandHandler(ITicketRepository tickets, IUnitOfWork unitOfWork, TimeProvider timeProvider)
    : ICommandHandler<RecordFirstResponseCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(RecordFirstResponseCommand command, CancellationToken cancellationToken)
    {
        var ticket = await tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.Id}' was not found.");
        if (!ticket.IsSlaTracked) throw new ConflictException("This ticket has no SLA tracking.");
        if (!TicketWorkflow.CanEscalate(ticket.Status)) throw new ConflictException($"A first response cannot be recorded on a {ticket.Status} ticket.");
        if (!ticket.RecordFirstResponse(command.Actor.UserId, timeProvider.GetUtcNow()))
            throw new ConflictException("The first response was already recorded.");
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }
}
```

**File: `backend/src/Crm.Application/DependencyInjection.cs`** — after line 13 add `services.AddScoped<Sla.SlaEvaluator>();` and `services.AddScoped<Sla.ITicketCreationAutomation, Sla.TicketCreationAutomation>();`.

### 5 — Application: repository contracts

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/ISlaConfigurationRepository.cs`**

```csharp
/// <summary>The two SLA singletons (Id 1). Find* are tracked (for update) and null until first saved. Add only stages.</summary>
public interface ISlaConfigurationRepository
{
    Task<SlaPolicy?> FindPolicyAsync(CancellationToken cancellationToken = default);
    Task<BusinessCalendar?> FindCalendarAsync(CancellationToken cancellationToken = default);
    void Add(SlaPolicy policy);
    void Add(BusinessCalendar calendar);
    /// <summary>Display name of a user id (UpdatedByUserId); null when unknown.</summary>
    Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IAutomationRuleRepository.cs`**

```csharp
public interface IAutomationRuleRepository
{
    void Add(AutomationRule rule);
    void AddExecution(AutomationRuleExecution execution);
    void AddAlert(SlaAlert alert);
    Task<AutomationRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);                 // tracked
    /// <summary>Enabled rules of one trigger, ordered SortOrder, CreatedAtUtc, Id. No tracking.</summary>
    Task<IReadOnlyList<AutomationRule>> ListEnabledAsync(AutomationTrigger trigger, CancellationToken cancellationToken = default);
    /// <summary>All rules (same order) with the target Agent's name and active flag joined.</summary>
    Task<IReadOnlyList<AutomationRuleDetails>> ListDetailsAsync(CancellationToken cancellationToken = default);
    Task<AutomationRuleDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AutomationExecutionDetails>> ListRecentExecutionsAsync(int limit, CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/ISlaMonitoringRepository.cs`**

```csharp
/// <summary>Worker and overview queries over SLA-tracked Open/InProgress tickets. No tracking; no scope (system / SuperAdmin only).</summary>
public interface ISlaMonitoringRepository
{
    /// <summary>Pending target (not completed), due instant in the future. Breach and warning queries both apply, IN SQL and before Take(limit):
    /// Open/InProgress, tracked, due >= query.EnabledSinceUtc, the rule's CategoryCondition / PriorityCondition / AssignedCondition (each only
    /// when set), no execution for (rule, ticket, kind), Id NOT IN query.ExcludedTicketIds. Ordered due ASC, Id ASC (deterministic).</summary>
    Task<IReadOnlyList<SlaCandidate>> ListWarningCandidatesAsync(SlaCandidateQuery query, CancellationToken cancellationToken = default);
    /// <summary>due <= now AND (completed IS NULL OR completed > due), plus every filter listed above.</summary>
    Task<IReadOnlyList<SlaCandidate>> ListBreachCandidatesAsync(SlaCandidateQuery query, CancellationToken cancellationToken = default);
    /// <summary>Tracked Open/InProgress tickets with at least one uncompleted target, for the overview (≤ limit, oldest due first).</summary>
    Task<IReadOnlyList<SlaOverviewRow>> ListActiveTrackedAsync(int limit, CancellationToken cancellationToken = default);
}

public sealed record SlaCandidate(Guid TicketId, TicketCategory Category, TicketPriority Priority, Guid? AssignedAgentUserId, DateTimeOffset DueAtUtc);
/// <summary>F2: one page for one (rule, kind). ExcludedTicketIds = tickets already attempted in this pass (failed/skipped) — never retried twice in a pass.</summary>
public sealed record SlaCandidateQuery(
    SlaKind Kind, TicketCategory? CategoryCondition, TicketPriority? PriorityCondition, bool? AssignedCondition,
    Guid RuleId, DateTimeOffset Now, DateTimeOffset EnabledSinceUtc, IReadOnlyCollection<Guid> ExcludedTicketIds, int Limit);
public sealed record SlaOverviewRow(Guid TicketId, string Subject, TicketStatus Status, string? AssignedAgentName, TicketSlaInput Sla);
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/ISlaAlertRepository.cs`** — mirror of `ITaskReminderRepository` usage:

```csharp
/// <summary>Visible = recipient is the caller AND (caller is SuperAdmin OR ticket.AssignedAgentUserId == caller), evaluated in SQL on every call.</summary>
public interface ISlaAlertRepository
{
    Task<PagedResult<SlaAlertDetails>> ListAsync(StaffActor actor, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default);
    Task<int> CountUnreadAsync(StaffActor actor, CancellationToken cancellationToken = default);
    /// <summary>One conditional UPDATE (explicit execution strategy). NotFound when missing or not visible.</summary>
    Task<MarkReadOutcome> MarkReadAsync(long id, StaffActor actor, DateTimeOffset now, CancellationToken cancellationToken = default);
}
```

Reuse the existing `MarkReadOutcome` enum (`backend/src/Crm.Application/AgentTasks/AgentTaskDetails.cs` line 56, namespace `Crm.Application.AgentTasks`); do not duplicate it.

**Create read models** (one file each, records only): `backend/src/Crm.Application/Automation/AutomationRuleDetails.cs` (all rule fields + `string? TargetAgentName`, `bool? TargetAgentIsActive`, `CreatedAtUtc`, `UpdatedAtUtc`), `AutomationExecutionDetails.cs` (`Id`, `RuleId`, `RuleName`, `TicketId`, `TicketSubject`, `SlaKind?`, `Trigger`, `Action`, `Outcome`, `Detail`, `OccurredAtUtc`), `backend/src/Crm.Application/SlaAlerts/SlaAlertDetails.cs` (`Id`, `TicketId`, `TicketSubject`, `TicketStatus`, `Type`, `Kind`, `DueAtUtc`, `CreatedAtUtc`, `ReadAtUtc`), `backend/src/Crm.Application/Sla/SlaSettingsDetails.cs`:

```csharp
public sealed record SlaPolicyDetails(bool IsConfigured, bool IsEnabled, IReadOnlyList<SlaTarget> Targets, DateTimeOffset? UpdatedAtUtc, string? UpdatedByName);
public sealed record BusinessCalendarDetails(bool IsConfigured, string? TimeZoneId, IReadOnlyList<DayOfWeek> WorkingDays, TimeOnly? OpensAt, TimeOnly? ClosesAt,
    IReadOnlyList<BusinessHoliday> Holidays, DateTimeOffset? UpdatedAtUtc, string? UpdatedByName);
public sealed record SlaDuePreview(DateTimeOffset StartUtc, int Minutes, DateTimeOffset DueAtUtc, string TimeZoneId);
public sealed record SlaOverview(int WarningCount, int BreachedCount, int OnTrackCount, IReadOnlyList<SlaOverviewTicket> Warning,
    IReadOnlyList<SlaOverviewTicket> Breached, IReadOnlyList<AutomationExecutionDetails> RecentExecutions, DateTimeOffset GeneratedAtUtc);
public sealed record SlaOverviewTicket(Guid TicketId, string Subject, TicketStatus Status, string? AssignedAgentName, TicketSlaView Sla);
```

Counts are over the rows returned by `ListActiveTrackedAsync` (≤ 500, `SlaRules`-style constant `OverviewScanLimit = 500` — add it to `SlaRules`); document the cap.

### 6 — Application: use cases

All in the established "record + handler (+ validator) in one folder" style (`CreateTicket/`). SuperAdmin-only endpoints need no actor checks in handlers (policy at the controller), but writers receive the actor id for `UpdatedByUserId`.

`backend/src/Crm.Application/Sla/`:

1. `GetSlaPolicy/GetSlaPolicyQuery.cs` — `GetSlaPolicyQuery : IQuery<SlaPolicyDetails>`. Not configured ⇒ `IsConfigured: false`, `IsEnabled: false`, empty targets.
2. `UpdateSlaPolicy/UpdateSlaPolicyCommand.cs` — `UpdateSlaPolicyCommand(Guid ActorUserId, bool? IsEnabled, IReadOnlyList<SlaTargetInput>? Targets) : ICommand<SlaPolicyDetails>`, `SlaTargetInput(TicketPriority? Priority, int FirstResponseMinutes, int ResolutionMinutes)`. Handler: find ⇒ `Update` or `Create` + `Add` ⇒ one save ⇒ return details. Validator (`UpdateSlaPolicyValidator`): `IsEnabled` NotNull; `Targets` NotNull, count == 4, each `Priority` NotNull + IsInEnum, priorities distinct ("Provide exactly one target per priority."), minutes `InclusiveBetween(1, SlaRules.MaxTargetMinutes)`; add a rule `ResolutionMinutes >= FirstResponseMinutes` with message "The resolution target cannot be shorter than the first-response target." (sensible guard; 400 `targets[i].resolutionMinutes`).
3. `GetBusinessCalendar/GetBusinessCalendarQuery.cs` — not configured ⇒ `IsConfigured: false` with nulls / empty lists.
4. `UpdateBusinessCalendar/UpdateBusinessCalendarCommand.cs` — `(Guid ActorUserId, string TimeZoneId, IReadOnlyList<DayOfWeek>? WorkingDays, TimeOnly? OpensAt, TimeOnly? ClosesAt, IReadOnlyList<HolidayInput>? Holidays)`, `HolidayInput(DateOnly? Date, string? Name)`. Validator: `TimeZoneId` NotEmpty, max 64, `Must(id => TimeZoneInfo.TryFindSystemTimeZoneById(id.Trim(), out _))` "Unknown time zone. Use an IANA id such as Africa/Cairo."; `WorkingDays` NotEmpty ("Select at least one working day."), each IsInEnum, distinct; `OpensAt`/`ClosesAt` NotNull; `OpensAt < ClosesAt` ("Closing time must be after opening time."); holidays ≤ 366, each date NotNull, names trimmed 1–100, dates distinct. Handler stores holidays ordered by date.
5. `PreviewDueDate/PreviewSlaDueDateQuery.cs` — `(DateTimeOffset? StartUtc, int Minutes) : IQuery<SlaDuePreview>`; validator `Minutes` 1–`MaxPreviewMinutes`; handler: calendar missing ⇒ `ConflictException("Save the business calendar first.")`; start defaults to `timeProvider.GetUtcNow()`, normalized `ToUniversalTime()`. This is the deterministic check for capability 2 (no waiting).
6. `GetSlaOverview/GetSlaOverviewQuery.cs` — loads context via `SlaEvaluator.CreateContextAsync`, evaluates `ListActiveTrackedAsync(SlaRules.OverviewScanLimit)` rows, splits by indicator, takes ≤ `OverviewListLimit` of each, adds `ListRecentExecutionsAsync(RecentExecutionsLimit)`.

`backend/src/Crm.Application/Automation/`:

7. `ListAutomationRules/ListAutomationRulesQuery.cs`, `GetAutomationRule/GetAutomationRuleQuery.cs` (404 `NotFoundException($"Automation rule '{id}' was not found.")`).
8. `CreateAutomationRule/CreateAutomationRuleCommand.cs` — `(Guid ActorUserId, string Name, bool? IsEnabled, AutomationTrigger? Trigger, AutomationAction? Action, int SortOrder, TicketCategory? CategoryCondition, TicketPriority? PriorityCondition, bool? AssignedCondition, SlaKind? SlaKindCondition, Guid? TargetAgentUserId, int? WarningLeadMinutes) : ICommand<AutomationRuleDetails>`. `UpdateAutomationRule/UpdateAutomationRuleCommand.cs` — same plus `Guid Id`. Both handlers: when `TargetAgentUserId` is set, `identity.FindByIdAsync` must return `{ Role: Agent, IsActive: true }` else 400 `targetAgentUserId` "Select an active Agent." (same helper style as `CreateTicketCommand.cs` lines 82–84, 91–92). Build `AutomationRuleDefinition` (name trimmed), `Create(Guid.CreateVersion7(), …)` or `GetByIdAsync` + `Update`, one save, return `FindDetailsByIdAsync`.
9. `AutomationRuleValidation.cs` — one shared `internal static void AddDefinitionRules<T>(AbstractValidator<T> v, Func<T, …> …)` **or** two validators with identical rules (`CreateAutomationRuleValidator`, `UpdateAutomationRuleValidator`); prefer a small shared `AbstractValidator<IAutomationRuleInput>` base if the record pair implements a common interface `IAutomationRuleInput` — choose whichever compiles most simply; rules: `Name` NotEmpty max 100; `IsEnabled`, `Trigger`, `Action` NotNull + IsInEnum; `SortOrder` 1–1000; combination valid (`AutomationRule.IsValidCombination`) else 400 `action` "This action is not available for the selected trigger."; `TargetAgentUserId` required iff action `AssignToAgent` (else must be null: "Only Assign to agent uses a target agent."); `WarningLeadMinutes` required iff trigger `SlaWarningDue`, 1–10,080; `AssignedCondition` and `SlaKindCondition` must be null for `TicketCreated` ("New tickets are always unassigned and have no SLA kind condition."); enums IsInEnum.
10. `RunSlaAutomation/RunSlaAutomationCommand.cs` — the worker's unit of work:

```csharp
/// <summary>
/// SLA-001 §7: one worker pass. NOT ITransactionalCommand — each (rule, ticket, kind) runs in its own short transaction through
/// IUnitOfWork.ExecuteInTransactionAsync, so one failure (409 from Tickets.Version, deadlock) never blocks the others; the next pass retries it.
/// Idempotent: the unique execution key makes a replayed or duplicated item insert nothing twice.
/// </summary>
public sealed record RunSlaAutomationCommand : ICommand<SlaAutomationRunResult>;
public sealed record SlaAutomationRunResult(int Processed, int Skipped, int Failed, DateTimeOffset RanAtUtc);

public sealed class RunSlaAutomationCommandHandler(
    IAutomationRuleRepository rules, ISlaMonitoringRepository monitoring, ISlaConfigurationRepository configuration,
    ITicketRepository tickets, IIdentityService identity, IUnitOfWork unitOfWork, TimeProvider timeProvider,
    ILogger<RunSlaAutomationCommandHandler> logger) : ICommandHandler<RunSlaAutomationCommand, SlaAutomationRunResult>
```

Flow per pass:
- `now = timeProvider.GetUtcNow()`; calendar = `configuration.FindCalendarAsync` → `ToRules()` (null ⇒ warning rules are skipped, exactly as the display shows no warning without a calendar; breach rules still run — breach needs no calendar). The same `now` and calendar are used for the whole pass.
- `superAdmins = (await identity.ListByRoleAsync(UserRoles.SuperAdmin)).Where(u => u.IsActive).Select(u => u.Id)`.
- For each enabled rule of `SlaWarningDue` and `SlaBreached` (`ListEnabledAsync`), for each kind in `rule.SlaKindCondition is { } k ? [k] : [FirstResponse, Resolution]`, page through candidates (F2):

```csharp
var attempted = new HashSet<Guid>();
for (var page = 0; page < SlaRules.WorkerMaxPagesPerRule; page++)
{
    var query = new SlaCandidateQuery(kind, rule.CategoryCondition, rule.PriorityCondition, rule.AssignedCondition, rule.Id, now,
        rule.EnabledAtUtc ?? rule.CreatedAtUtc, attempted, SlaRules.WorkerBatchSize);
    var candidates = rule.Trigger == AutomationTrigger.SlaWarningDue
        ? await monitoring.ListWarningCandidatesAsync(query, cancellationToken)
        : await monitoring.ListBreachCandidatesAsync(query, cancellationToken);
    var reachedHorizon = false;
    foreach (var c in candidates)
    {
        // Warnings: due ASC ⇒ remaining business minutes ASC. The first candidate not yet at the threshold ends this (rule, kind).
        if (rule.Trigger == AutomationTrigger.SlaWarningDue
            && !SlaEvaluator.IsWarningThresholdReached(rule, SlaEvaluator.RemainingBusinessMinutes(now, c.DueAtUtc, calendar)))
        {
            reachedHorizon = true;
            break;
        }

        attempted.Add(c.TicketId);
        // process c (below)
    }

    if (reachedHorizon || candidates.Count < SlaRules.WorkerBatchSize)
    {
        break;
    }
}
```

  `rule.Matches(...)` is **not** applied in memory to filter the page (the SQL already did, before `Take`); it is still re-checked on the reloaded ticket inside `ProcessAsync`.
- For each candidate being processed: `try { var outcome = await unitOfWork.ExecuteInTransactionAsync(ct => ProcessAsync(rule, kind, c.TicketId, now, superAdmins, ct), cancellationToken); … } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; } catch (Exception ex) { failed++; logger.LogWarning(ex, "SLA automation failed for rule {RuleId}, ticket {TicketId}, {Kind}; retrying next pass.", …); }`.
- `ProcessAsync` (replay-safe, R1 rules from Story 07): `tickets.GetByIdAsync(id, null)`; re-check on the loaded entity: status `Open`/`InProgress`, tracked, kind's due/completion still satisfies the trigger (breach: `due <= now && (completed is null || completed > due)`; warning: `completed is null && now < due`), for warnings `SlaEvaluator.IsWarningThresholdReached(rule, SlaEvaluator.RemainingBusinessMinutes(now, due, calendar))` with the pass's `now` and calendar (the same helpers the display uses, F3), `rule.Matches(ticket.Category, ticket.Priority, ticket.AssignedAgentUserId is not null, kind)` still true (assignment/category may have changed) — otherwise return `null` (skipped, nothing written; the id stays in `attempted`, so it is not re-fetched this pass). Then:
  - **Notify:** `execution = AutomationRuleExecution.Create(…, Notified, detail: $"{recipients.Count} recipient(s)", now)`; recipients = warning: `ticket.AssignedAgentUserId is { } a && IsActiveAgent(a) ? [a] : superAdmins`; breach: `(assigned active Agent if any) ∪ superAdmins` (distinct). `rules.AddExecution(execution)`; `rules.AddAlert(SlaAlert.Create(execution, r, type, kind, due, now))` per recipient. Zero recipients (no active SuperAdmin and unassigned) ⇒ still record the execution with detail "No active recipient" (so it is not retried every minute).
  - **Escalate:** `ticket.IsEscalated` ⇒ outcome `AlreadyEscalated`; otherwise `ticket.EscalateAutomatically($"SLA breached: {(kind == FirstResponse ? "first response" : "resolution")} was due {due:yyyy-MM-dd HH:mm} UTC (rule \"{rule.Name}\").", now)` ⇒ outcome `Escalated`. (Re-checked status guarantees `CanEscalate`; `NotEscalatable` is used only if that invariant fails defensively.)
  - `await unitOfWork.SaveChangesAsync(ct)` inside the lambda (the outer `ExecuteInTransactionAsync` saves again as a no-op and commits).
- `IsActiveAgent(a)`: `identity.FindByIdAsync(a)` is `{ Role: Agent, IsActive: true }` (cache per pass in a local dictionary created inside `Handle`, not a field).
- Return counts; the worker logs them at `Debug` when nothing happened and `Information` when `Processed > 0`.

`backend/src/Crm.Application/SlaAlerts/` (copy `AgentTasks/Reminders/*` shapes, but with `StaffActor` so SuperAdmin works):

11. `ListSlaAlerts/ListSlaAlertsQuery.cs` — `(StaffActor Actor, bool UnreadOnly = false, int Page = 1, int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<SlaAlertDetails>>` + validator (same as `ListTaskRemindersValidator`).
12. `CountUnreadSlaAlerts/CountUnreadSlaAlertsQuery.cs` — `(StaffActor Actor) : IQuery<int>`.
13. `MarkSlaAlertRead/MarkSlaAlertReadCommand.cs` — `(StaffActor Actor, long Id) : ICommand`; `NotFound` ⇒ `NotFoundException($"SLA alert '{id}' was not found.")`; already read = success. Never touches the ticket.

### 7 — Infrastructure: EF configuration, DbSets, repositories

**Create configurations** in `backend/src/Crm.Infrastructure/Persistence/Configurations/`:

- `SlaPolicyConfiguration.cs` — `ToTable("SlaPolicies")`, `HasKey(Id)`, `Property(Id).ValueGeneratedNever()`, `OwnsMany(p => p.Targets, t => { t.ToTable("SlaPolicyTargets"); t.WithOwner().HasForeignKey("SlaPolicyId"); t.Property(x => x.Priority).HasConversion<string>().HasMaxLength(20); t.HasKey("SlaPolicyId", nameof(SlaTarget.Priority)); })`, `Navigation(p => p.Targets).HasField("_targets").UsePropertyAccessMode(PropertyAccessMode.Field)`, Restrict FK `UpdatedByUserId` → `ApplicationUser`.
- `BusinessCalendarConfiguration.cs` — `ToTable("BusinessCalendars")`, `TimeZoneId` required max 64, `Ignore(c => c.WorkingDays)`, `OpensAt`/`ClosesAt` map to SQL `time` by default, `OwnsMany(Holidays)` → `BusinessHolidays`, key `(BusinessCalendarId, Date)` (`DateOnly` ⇒ `date`), `Name` required max 100; field-backed navigation `_holidays`; Restrict FK `UpdatedByUserId`.
- `AutomationRuleConfiguration.cs` — `ToTable("AutomationRules")`, `ValueGeneratedNever`, `Name` required max 100, all enums `HasConversion<string>().HasMaxLength(30)`, Restrict FKs `TargetAgentUserId`, `UpdatedByUserId` → `ApplicationUser`, index `(Trigger, IsEnabled, SortOrder, CreatedAtUtc, Id)`.
- `AutomationRuleExecutionConfiguration.cs` — `ToTable("AutomationRuleExecutions")`, `bigint IDENTITY` (`ValueGeneratedOnAdd`), enums by name (30), `Detail` max 300, Restrict FKs to `AutomationRule` (`RuleId`) and `Ticket` (`TicketId`), **unique** `HasIndex(e => new { e.RuleId, e.TicketId, e.SlaKind }).IsUnique().HasDatabaseName("UX_AutomationRuleExecutions_RuleTicketKind")` — add `.HasFilter(null)` so EF does **not** generate its default `WHERE [SlaKind] IS NOT NULL` filter for nullable unique columns (the NULL-equal behaviour is what dedups assignment rows). Index `(OccurredAtUtc DESC, Id DESC)` for "recent".
- `SlaAlertConfiguration.cs` — `ToTable("SlaAlerts")`, identity id, enums by name (20), FK `ExecutionId` → `AutomationRuleExecution` via the private navigation (`HasOne<AutomationRuleExecution>("Execution").WithMany().HasForeignKey(a => a.ExecutionId).OnDelete(Restrict)`), Restrict FKs to `Ticket`, `ApplicationUser` (`RecipientUserId`), unique `(ExecutionId, RecipientUserId)` named `UX_SlaAlerts_ExecutionRecipient`, inbox index `(RecipientUserId, CreatedAtUtc DESC, Id DESC)` (as `TaskReminderNotificationConfiguration.cs` line 22).

**File: `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`** — after line 31 add:

```csharp
        // SLA-001 snapshot (all nullable; existing rows stay NULL = not tracked).
        builder.Property(t => t.SlaTimeZoneId).HasMaxLength(64);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.FirstRespondedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Ignore(t => t.IsSlaTracked);
```

and after line 42 two filtered worker indexes:

```csharp
        builder.HasIndex(t => t.SlaFirstResponseDueAtUtc).HasFilter("[SlaFirstResponseDueAtUtc] IS NOT NULL AND [FirstRespondedAtUtc] IS NULL");
        builder.HasIndex(t => t.SlaResolutionDueAtUtc).HasFilter("[SlaResolutionDueAtUtc] IS NOT NULL AND [SlaResolvedAtUtc] IS NULL");
```

(Breach candidates that were completed late are found through the `RuleId` anti-join and the small tracked set; no extra index.)

**File: `TicketActivityConfiguration.cs`** — line 23 stays as is; EF infers the nullable FK from `Guid?`. Update the comment on line 22: "ActorUserId null = SLA-001 automation."

**File: `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`** — after line 30 add `DbSet`s `SlaPolicies`, `BusinessCalendars`, `AutomationRules`, `AutomationRuleExecutions`, `SlaAlerts` (+ `using Crm.Domain.Automation; using Crm.Domain.Sla;`).

**Create repositories** in `backend/src/Crm.Infrastructure/Persistence/Repositories/` (`internal sealed class … (CrmDbContext dbContext)`):

- `SlaConfigurationRepository.cs` — `FindPolicyAsync` = `dbContext.SlaPolicies.SingleOrDefaultAsync(p => p.Id == SlaPolicy.SingletonId)` (owned targets load automatically), same for calendar; `FindUserNameAsync` via `dbContext.Users`.
- `AutomationRuleRepository.cs` — `ListEnabledAsync` `AsNoTracking().Where(r => r.IsEnabled && r.Trigger == trigger).OrderBy(SortOrder).ThenBy(CreatedAtUtc).ThenBy(Id)`; details with left joins to `dbContext.Users` (`TargetAgentName`, `TargetAgentIsActive`); recent executions joined to rule name and ticket subject, ordered `OccurredAtUtc DESC, Id DESC`.
- `SlaMonitoringRepository.cs` — both candidate queries as LINQ over `dbContext.Tickets.AsNoTracking()` with `(t.Status == Open || t.Status == InProgress)`, the kind-specific column pair (write two small private expression helpers or an `if (kind == FirstResponse)` branch — no dynamic SQL), `!dbContext.AutomationRuleExecutions.Any(e => e.RuleId == query.RuleId && e.TicketId == t.Id && e.SlaKind == query.Kind)`, then the rule conditions as plain conditional `Where`s added only when set (`if (query.CategoryCondition is { } category) tickets = tickets.Where(t => t.Category == category);` — same for priority; `AssignedCondition true ⇒ t.AssignedAgentUserId != null`, `false ⇒ == null`), `if (query.ExcludedTicketIds.Count > 0) tickets = tickets.Where(t => !query.ExcludedTicketIds.Contains(t.Id));` (EF translates the collection parameter), and only then `OrderBy(due).ThenBy(t => t.Id).Take(query.Limit)`. **The conditions must be in SQL before `Take`** (F2) — filtering a fetched page in memory would let 200 non-matching tickets hide matching ones indefinitely. `ListActiveTrackedAsync` projects `TicketSlaInput` (with `FirstRespondedByName` left-joined) and the assignee name.
- `SlaAlertRepository.cs` — copy `TaskReminderRepository` patterns (lines 38–96): `Visible(actor)` = `dbContext.SlaAlerts.AsNoTracking().Where(a => a.RecipientUserId == actor.UserId && (actor.IsSuperAdmin || dbContext.Tickets.Any(t => t.Id == a.TicketId && t.AssignedAgentUserId == actor.UserId)))`; list joined to `Tickets` for subject/status, ordered `CreatedAtUtc DESC, Id DESC`; `MarkReadAsync` = explicit `CreateExecutionStrategy().ExecuteAsync(… ExecuteUpdateAsync …)` with the same visibility predicate, then `AnyAsync` to distinguish `AlreadyRead` / `NotFound`.

**File: `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs`**

- Constructor: `internal sealed class TicketRepository(CrmDbContext dbContext, SlaEvaluator slaEvaluator) : ITicketRepository` (`using Crm.Application.Sla;`).
- `ListAsync`: after the `Escalated` filter (line 47) add

```csharp
        if (filter.SlaBreached is { } breached)
        {
            var now = DateTimeOffset.UtcNow; // SQL-side comparison against stored instants; same instant source as TimeProvider.System
            Expression<Func<Ticket, bool>> isBreached = t =>
                (t.SlaFirstResponseDueAtUtc != null && ((t.FirstRespondedAtUtc == null && t.SlaFirstResponseDueAtUtc < now) || t.FirstRespondedAtUtc > t.SlaFirstResponseDueAtUtc))
                || (t.SlaResolutionDueAtUtc != null && ((t.SlaResolvedAtUtc == null && t.SlaResolutionDueAtUtc < now) || t.SlaResolvedAtUtc > t.SlaResolutionDueAtUtc));
            query = breached ? query.Where(isBreached) : query.Where(Not(isBreached));
        }
```

  Prefer injecting `TimeProvider` into the repository and using `timeProvider.GetUtcNow()` instead of `DateTimeOffset.UtcNow` (keeps one clock); implement `false` as the negated predicate written out explicitly (no expression helper needed if simpler: `query.Where(t => !( … ))`).
- `Project` (74–101): change it to return an anonymous row `new { Details = new TicketDetails(…existing 20 args…), Sla = t.SlaFirstResponseDueAtUtc == null ? null : new TicketSlaSnapshot(t.CreatedAtUtc, t.SlaTimeZoneId!, t.SlaFirstResponseTargetMinutes!.Value, t.SlaFirstResponseDueAtUtc.Value, t.FirstRespondedAtUtc, responder == null ? null : responder.FirstName + " " + responder.LastName, t.SlaResolutionTargetMinutes!.Value, t.SlaResolutionDueAtUtc!.Value, t.SlaResolvedAtUtc), t.Category, t.Priority, IsAssigned = t.AssignedAgentUserId != null }`, adding `from responder in dbContext.Users.Where(u => u.Id == t.FirstRespondedByUserId).DefaultIfEmpty()`. Add a private `async Task<List<TicketDetails>> EnrichAsync(rows, ct)`: if no row has `Sla`, return details unchanged; otherwise `var context = await slaEvaluator.CreateContextAsync(ct)` **once** and `row.Details with { Sla = row.Sla is null ? null : SlaEvaluator.Evaluate(new TicketSlaInput(row.Category, row.Priority, row.IsAssigned, row.Sla), context) }`. `FindDetailsByIdAsync` and `ListAsync` materialize rows then call it.
- `ListActivityAsync` (119–135): replace the inner join on line 121 with `from actor in dbContext.Users.Where(u => u.Id == a.ActorUserId).DefaultIfEmpty()` and select `actor == null ? SlaRules.AutomationActorName : actor.FirstName + " " + actor.LastName`, plus `a.ActorUserId == null` as the new last `IsAutomatic` argument. Update the comment above `Project` (lines 71–73) about which joins are inner.

**File: `backend/src/Crm.Infrastructure/Persistence/Repositories/AgentDashboardRepository.cs`** — inject `SlaEvaluator`; in `ListAttentionQueueAsync` (21–44) project an anonymous row with the `TicketSlaSnapshot` (same expression as above, responder name not needed ⇒ pass `null`) and set `SlaIndicator` after materialization (`Evaluate(...).Indicator`, `NotTracked` when null). **Ordering, limit and `GetWorkloadCountsAsync` stay unchanged** (intake §11: no change to counts or queue ownership).

**File: `backend/src/Crm.Infrastructure/DependencyInjection.cs`** — after line 47 register `ISlaConfigurationRepository`, `IAutomationRuleRepository`, `ISlaMonitoringRepository`, `ISlaAlertRepository` (scoped).

### 8 — API: contracts, controllers, worker

**Create file: `backend/src/Crm.Api/Contracts/Sla/SlaContracts.cs`** — requests/responses with `From(...)` mappers (style: `TicketResponse.cs`):

```csharp
public sealed record SlaTargetDto(TicketPriority? Priority, int FirstResponseMinutes, int ResolutionMinutes);
public sealed record UpdateSlaPolicyRequest(bool? IsEnabled, IReadOnlyList<SlaTargetDto>? Targets);
public sealed record SlaPolicyResponse(bool IsConfigured, bool IsEnabled, IReadOnlyList<SlaTargetDto> Targets, DateTimeOffset? UpdatedAtUtc, string? UpdatedByName);
public sealed record HolidayDto(DateOnly? Date, string? Name);
public sealed record UpdateBusinessCalendarRequest(string TimeZoneId, IReadOnlyList<DayOfWeek>? WorkingDays, TimeOnly? OpensAt, TimeOnly? ClosesAt, IReadOnlyList<HolidayDto>? Holidays);
public sealed record BusinessCalendarResponse(bool IsConfigured, string? TimeZoneId, IReadOnlyList<DayOfWeek> WorkingDays, TimeOnly? OpensAt, TimeOnly? ClosesAt, IReadOnlyList<HolidayDto> Holidays, DateTimeOffset? UpdatedAtUtc, string? UpdatedByName);
public sealed record SlaDuePreviewResponse(DateTimeOffset StartUtc, int Minutes, DateTimeOffset DueAtUtc, string TimeZoneId);
public sealed record SlaTargetResponse(SlaKind Kind, int TargetMinutes, DateTimeOffset DueAtUtc, DateTimeOffset? CompletedAtUtc, string? CompletedByName, SlaTargetState State, bool IsWarning, int? BusinessMinutesRemaining, int BusinessMinutesElapsed);
public sealed record TicketSlaResponse(SlaIndicator Indicator, string TimeZoneId, SlaTargetResponse FirstResponse, SlaTargetResponse Resolution);
public sealed record SlaOverviewTicketResponse(Guid TicketId, string Subject, TicketStatus Status, string? AssignedAgentName, TicketSlaResponse Sla);
public sealed record SlaOverviewResponse(int WarningCount, int BreachedCount, int OnTrackCount, IReadOnlyList<SlaOverviewTicketResponse> Warning, IReadOnlyList<SlaOverviewTicketResponse> Breached, IReadOnlyList<AutomationExecutionResponse> RecentExecutions, DateTimeOffset GeneratedAtUtc);
```

**Create file: `backend/src/Crm.Api/Contracts/Automation/AutomationRuleContracts.cs`** — `AutomationRuleRequest` (all definition fields, nullable enums/bools so missing fields fail validation), `AutomationRuleResponse` (details incl. `TargetAgentName`, `TargetAgentIsActive`, `EnabledAtUtc`), `AutomationExecutionResponse`.

**Create file: `backend/src/Crm.Api/Contracts/SlaAlerts/SlaAlertResponses.cs`** — `SlaAlertResponse(long Id, Guid TicketId, string TicketSubject, TicketStatus TicketStatus, SlaAlertType Type, SlaKind Kind, DateTimeOffset DueAtUtc, DateTimeOffset CreatedAtUtc, DateTimeOffset? ReadAtUtc, bool IsRead)`, `SlaAlertCountResponse(int UnreadCount)`. No recipient ids.

**File: `backend/src/Crm.Api/Contracts/Tickets/TicketResponse.cs`** — append `TicketSlaResponse? Sla` (last), mapped with `t.Sla is null ? null : TicketSlaResponse.From(t.Sla)` (add `From` to `TicketSlaResponse`/`SlaTargetResponse`). **`TicketActivityResponse.cs`** — append `bool IsAutomatic`. **`AgentDashboardResponse.cs`** — append `SlaIndicator SlaIndicator` to `AttentionTicketResponse` (line 33) and its `From` (36).

**Create file: `backend/src/Crm.Api/Controllers/SlaController.cs`** — `[ApiController] [Route(ApiRoutes.V1 + "/sla")] [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]`; actions `GET policy`, `PUT policy`, `GET calendar`, `PUT calendar`, `GET calendar/preview` (`[FromQuery] DateTimeOffset? startUtc, [FromQuery] int minutes`), `GET overview`. Each with an `EndpointDescription` starting "Requires role: SuperAdmin." and stating the business rule (e.g. PUT policy: "Exactly one target per priority (Low, Medium, High, Urgent), positive business minutes. Applies to tickets created afterwards only; existing deadlines never change.").

**Create file: `backend/src/Crm.Api/Controllers/AutomationRulesController.cs`** — route `/automation-rules`, SuperAdmin; `GET` list, `GET {id:guid}`, `POST` (201 + `CreatedAtAction`), `PUT {id:guid}`. Description lists the supported combinations verbatim.

**Create file: `backend/src/Crm.Api/Controllers/SlaAlertsController.cs`** — route `/sla-alerts`, `[Authorize(Policy = AuthorizationPolicies.Staff)]`; `GET` (unreadOnly/page/pageSize as `NotificationsController.cs` lines 25–35), `GET unread-count`, `PUT {id:long}/read` (204). Actor: `new StaffActor(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin))`.

**File: `backend/src/Crm.Api/Controllers/TicketsController.cs`**

- `List` (28–45): add `[FromQuery] bool? slaBreached` after `escalated` and pass it to `ListTicketsQuery`; extend the description: "slaBreached=true: tickets with at least one breached SLA target."
- After `ClearEscalation` (line 94) add:

```csharp
    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them; others 404). Records the first customer response for SLA (server time, caller as actor) on an Open/In Progress SLA-tracked ticket. Internal tracking only — nothing is sent to the customer. Already recorded or not tracked: 409.")]
    [HttpPost("{id:guid}/first-response")]
    public async Task<ActionResult<TicketResponse>> RecordFirstResponse(Guid id, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new RecordFirstResponseCommand(Actor, id), cancellationToken)));
```

**Create file: `backend/src/Crm.Api/BackgroundJobs/SlaAutomationOptions.cs`**

```csharp
namespace Crm.Api.BackgroundJobs;

/// <summary>Section "SlaAutomation". Single API instance only (SLA-001 §7) — several replicas would need coordination.</summary>
public sealed class SlaAutomationOptions
{
    public const string SectionName = "SlaAutomation";
    public bool Enabled { get; set; } = true;
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(60);
}
```

**Create file: `backend/src/Crm.Api/BackgroundJobs/SlaAutomationWorker.cs`**

```csharp
/// <summary>
/// SLA-001 S8: in-process poller. Runs once at startup (catches up deadlines missed while the API was stopped), then every PollInterval.
/// Nothing runs while the host is down. Every failure is logged and the loop continues; the database execution key keeps effects single.
/// </summary>
public sealed class SlaAutomationWorker(
    IServiceScopeFactory scopeFactory, IOptions<SlaAutomationOptions> options, TimeProvider timeProvider, ILogger<SlaAutomationWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("SLA automation worker is disabled (SlaAutomation:Enabled = false).");
            return;
        }

        using var timer = new PeriodicTimer(options.Value.PollInterval, timeProvider);
        do
        {
            await RunOnceAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunSlaAutomationCommand(), stoppingToken);
            logger.Log(result.Processed > 0 || result.Failed > 0 ? LogLevel.Information : LogLevel.Debug,
                "SLA automation pass: {Processed} processed, {Skipped} skipped, {Failed} failed.", result.Processed, result.Skipped, result.Failed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SLA automation pass failed (database unavailable or migrations pending?). Retrying next interval.");
        }
    }
}
```

Wrap `ExecuteAsync`'s `WaitForNextTickAsync` in `try/catch (OperationCanceledException)` so shutdown is clean.

**Create file: `backend/src/Crm.Api/BackgroundJobs/BackgroundJobsRegistration.cs`** — `public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)`: `AddOptions<SlaAutomationOptions>().Bind(configuration.GetSection(SlaAutomationOptions.SectionName)).Validate(o => o.PollInterval >= TimeSpan.FromSeconds(10) && o.PollInterval <= TimeSpan.FromHours(1), "SlaAutomation:PollInterval must be between 00:00:10 and 01:00:00.").ValidateOnStart();` then `services.AddHostedService<SlaAutomationWorker>();`.

**File: `backend/src/Crm.Api/Program.cs`** — line 13: `.AddPresentation()` ⇒ `.AddPresentation()\n    .AddBackgroundJobs(builder.Configuration);` (`using Crm.Api.BackgroundJobs;`). The worker starts after `app.Run()` begins, i.e. after `BootstrapIdentityAsync` (line 18).

**File: `backend/src/Crm.Api/appsettings.json`** — add `"SlaAutomation": { "Enabled": true, "PollInterval": "00:01:00" }` after the `Jwt` section. For local verification the executor may temporarily set `"PollInterval": "00:00:15"` in **user secrets or an environment variable** (`SlaAutomation__PollInterval=00:00:15`) — never commit a shorter value.

### 9 — Migration

From `backend/`:

```powershell
dotnet ef migrations add AddSlaAndAutomation --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations -- --environment Development
```

Review the generated `…_AddSlaAndAutomation.cs` before applying. It must contain **only**:

- `CreateTable` ×7: `SlaPolicies`, `SlaPolicyTargets`, `BusinessCalendars`, `BusinessHolidays`, `AutomationRules`, `AutomationRuleExecutions` (`bigint IDENTITY`), `SlaAlerts` (`bigint IDENTITY`); all FKs `ReferentialAction.Restrict` (owned-type FKs may be `Cascade` from owner to owned rows — acceptable, they only cascade from the singleton policy/calendar);
- `AddColumn` ×8 on `Tickets`, all **nullable**, no default values;
- `AlterColumn` on `TicketActivities.ActorUserId` to `nullable: true` (the FK/index may be dropped and re-created around it — acceptable);
- the new FK `Tickets.FirstRespondedByUserId` → `AspNetUsers`, the two filtered ticket indexes, `UX_AutomationRuleExecutions_RuleTicketKind` (**no filter**), `UX_SlaAlerts_ExecutionRecipient`, the inbox/recent indexes;
- the model snapshot update.

**No** `DropTable`, `DropColumn`, `RenameColumn`, `UpdateData`, `InsertData` or `Sql(...)`. Apply with `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` (or `.\run-dev.ps1`). Never `EnsureCreated`, drop or recreate `CrmDev`.

### 10 — Backend unit tests

**Create file (the single new test): `backend/tests/Crm.UnitTests/Sla/TicketCreationAutomationTests.cs`**

```csharp
public sealed class TicketCreationAutomationTests
{
    [Fact]
    public async Task ApplyAsync_UnassignedTicketCreatedThursdayAfternoon_PersistsBusinessDeadlinesAcrossWeekendAndHolidayAndAssignsFirstActiveMatchingRule()
```

Arrange (hand-written fakes, style of `CreateTicketCommandHandlerTests.cs` lines 50–103):
- Calendar `BusinessCalendar.Create("Africa/Cairo", [Sunday, Monday, Tuesday, Wednesday, Thursday], new TimeOnly(9, 0), new TimeOnly(17, 0), [new BusinessHoliday(new DateOnly(2030, 1, 6), "Example holiday")], adminId, now)`.
- Policy enabled; `High` = first response **240**, resolution **960**; other priorities any positive values.
- Ticket: `Ticket.Create(id, customerId, "Invoice wrong", "…", TicketCategory.Billing, TicketPriority.High, adminId, null, createdAt)` with `createdAt = 2030-01-03T14:00:00Z` (Thursday 16:00 Cairo — verify Cairo's offset for that date is UTC+2; Egypt's DST applies only in summer).
- Rules (returned by the fake in `SortOrder` order): rule A `SortOrder 1`, `Billing` → inactive Agent X; rule B `SortOrder 2`, `Billing` → active Agent Y; rule C `SortOrder 3`, any category → active Agent Z.
- Fake identity: X `{ Role: Agent, IsActive: false }`, Y and Z active Agents.

Assert:
- `SlaFirstResponseDueAtUtc == 2030-01-07T10:00:00Z` (Thu 60 min + Mon 180 min; Fri/Sat closed; Sun 2030-01-06 holiday) and `SlaResolutionDueAtUtc == 2030-01-08T14:00:00Z` (Thu 60 + Mon 480 + Tue 420 = 960 ⇒ Tuesday 16:00 local = 14:00 UTC; Cairo is UTC+2 in January). The expected values come from the documented rule, never from the implementation's output.
- `SlaFirstResponseTargetMinutes == 240`, `SlaResolutionTargetMinutes == 960`, `SlaTimeZoneId == "Africa/Cairo"`.
- `AssignedAgentUserId == Y` (A skipped: inactive; C not reached).
- Exactly one execution added with `RuleId == B`, `Outcome == Assigned`, `SlaKind == null`.
- Last activity: `Type == AssigneeChanged`, `ActorUserId == null`, `ToAgentUserId == Y`, `Reason` contains rule B's name.

`TimeZoneInfo.FindSystemTimeZoneById("Africa/Cairo")` works on Windows 10 1903+/11 (ICU) and Linux. If the dev machine throws `TimeZoneNotFoundException`, record it and switch the test (only) to `"Egypt Standard Time"`; do not change production code.

**File: `backend/tests/Crm.UnitTests/Tickets/CreateTicketCommandHandlerTests.cs`** (existing — compile fix only, C8): line 22 add `new NoTicketAutomation()` as the new argument after `new UnusedIdentityService()`, and add

```csharp
    private sealed class NoTicketAutomation : ITicketCreationAutomation
    {
        public Task ApplyAsync(Ticket ticket, CancellationToken cancellationToken) => Task.CompletedTask;
    }
```

No assertion changes. `TransitionTicketStatusCommandHandlerTests.cs` needs no edit (`Assert.Equal(Guid, Guid?)` binds to `Equal<Guid?>`); if the compiler disagrees, change only those two asserts to `Assert.Equal<Guid?>(…)`.

---

## Frontend Tasks

### 11 — SLA models and API

**Create file: `frontend/src/app/features/sla/sla-models.ts`** — mirror the API contracts exactly:

```ts
import { TicketCategory, TicketPriority, TicketStatus } from '../tickets/ticket-models';
import { StatusTone } from '../../shared/ui/status-badge/status-badge';

export const WEEK_DAYS = ['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday'] as const;
export type WeekDay = (typeof WEEK_DAYS)[number];
export const AUTOMATION_TRIGGERS = ['TicketCreated', 'SlaWarningDue', 'SlaBreached'] as const;
export type AutomationTrigger = (typeof AUTOMATION_TRIGGERS)[number];
export type AutomationAction = 'AssignToAgent' | 'CreateInAppNotification' | 'EscalateTicket';
export type SlaKind = 'FirstResponse' | 'Resolution';
export type SlaIndicator = 'NotTracked' | 'OnTrack' | 'Warning' | 'Breached' | 'Met';
export type SlaTargetState = 'Pending' | 'Met' | 'Breached';
export type AutomationOutcome = 'Assigned' | 'Notified' | 'Escalated' | 'AlreadyEscalated' | 'NotEscalatable';

/** Must match Crm.Domain.Automation.AutomationRule.IsValidCombination. Display only — the server validates. */
export const ACTIONS_BY_TRIGGER: Readonly<Record<AutomationTrigger, readonly AutomationAction[]>> = {
  TicketCreated: ['AssignToAgent'],
  SlaWarningDue: ['CreateInAppNotification'],
  SlaBreached: ['CreateInAppNotification', 'EscalateTicket'],
};

export interface SlaTarget { priority: TicketPriority; firstResponseMinutes: number; resolutionMinutes: number; }
export interface SlaPolicy { isConfigured: boolean; isEnabled: boolean; targets: SlaTarget[]; updatedAtUtc: string | null; updatedByName: string | null; }
export interface Holiday { date: string; name: string; }                 // 'yyyy-MM-dd'
export interface BusinessCalendar { isConfigured: boolean; timeZoneId: string | null; workingDays: WeekDay[]; opensAt: string | null; closesAt: string | null; holidays: Holiday[]; updatedAtUtc: string | null; updatedByName: string | null; } // times 'HH:mm:ss'
export interface UpdateBusinessCalendarRequest { timeZoneId: string; workingDays: WeekDay[]; opensAt: string; closesAt: string; holidays: Holiday[]; }
export interface SlaDuePreview { startUtc: string; minutes: number; dueAtUtc: string; timeZoneId: string; }
export interface SlaTargetView { kind: SlaKind; targetMinutes: number; dueAtUtc: string; completedAtUtc: string | null; completedByName: string | null; state: SlaTargetState; isWarning: boolean; businessMinutesRemaining: number | null; businessMinutesElapsed: number; }
export interface TicketSla { indicator: SlaIndicator; timeZoneId: string; firstResponse: SlaTargetView; resolution: SlaTargetView; }
export interface AutomationRule { id: string; name: string; isEnabled: boolean; enabledAtUtc: string | null; trigger: AutomationTrigger; action: AutomationAction; sortOrder: number; categoryCondition: TicketCategory | null; priorityCondition: TicketPriority | null; assignedCondition: boolean | null; slaKindCondition: SlaKind | null; targetAgentUserId: string | null; targetAgentName: string | null; targetAgentIsActive: boolean | null; warningLeadMinutes: number | null; createdAtUtc: string; updatedAtUtc: string; }
export type AutomationRuleRequest = Omit<AutomationRule, 'id' | 'enabledAtUtc' | 'targetAgentName' | 'targetAgentIsActive' | 'createdAtUtc' | 'updatedAtUtc'>;
export interface AutomationExecution { id: number; ruleId: string; ruleName: string; ticketId: string; ticketSubject: string; slaKind: SlaKind | null; trigger: AutomationTrigger; action: AutomationAction; outcome: AutomationOutcome; detail: string | null; occurredAtUtc: string; }
export interface SlaOverviewTicket { ticketId: string; subject: string; status: TicketStatus; assignedAgentName: string | null; sla: TicketSla; }
export interface SlaOverview { warningCount: number; breachedCount: number; onTrackCount: number; warning: SlaOverviewTicket[]; breached: SlaOverviewTicket[]; recentExecutions: AutomationExecution[]; generatedAtUtc: string; }

export function slaIndicatorLabel(i: SlaIndicator): string { /* NotTracked→'Not tracked', OnTrack→'On track', others as is */ }
export function slaIndicatorTone(i: SlaIndicator): StatusTone { /* Breached→error, Warning→pending, Met→success, OnTrack/NotTracked→neutral */ }
/** 135 → '2 h 15 min'; negative → overdue text handled by the caller. */
export function formatBusinessMinutes(minutes: number): string { ... }
```

**Create file: `frontend/src/app/features/sla/sla-api.ts`** — `@Injectable({ providedIn: 'root' }) export class SlaApi` with `getPolicy()`, `savePolicy(body)`, `getCalendar()`, `saveCalendar(body: UpdateBusinessCalendarRequest)`, `previewDue(startUtc: string | null, minutes: number)` (`HttpParams`, omit null start), `getOverview()`, `listRules()`, `getRule(id)`, `createRule(body)`, `updateRule(id, body)` — URLs via `this.apiUrl.versioned('sla/policy')` etc. (style: `tickets-api.ts`).

**Create file: `frontend/src/app/core/notifications/sla-alerts-api.ts`** — `SlaAlertsApi` with `unreadCount()` (`GET sla-alerts/unread-count`), `list(page, pageSize, unreadOnly)`, `markRead(id)`; models `SlaAlert`, `SlaAlertCount` added to `core/notifications/notification-models.ts` (keep existing interfaces untouched).

**File: `frontend/src/app/features/tickets/ticket-models.ts`** — add `sla: TicketSla | null;` to `Ticket` (after line 34; import type from `../sla/sla-models`), `slaBreached?: boolean | null;` to `TicketListParams` (after line 46, optional like `escalated`), `| 'FirstResponseRecorded'` to `TicketActivityType` (line 87), `isAutomatic: boolean;` to `TicketActivity` (line 100). Existing specs flush literals without `sla` — fine for `HttpTestingController` (no compile-time check on flushed JSON).

**File: `frontend/src/app/features/tickets/tickets-api.ts`** — destructure `slaBreached` in `list` (line 23) and `if (slaBreached != null) params = params.set('slaBreached', slaBreached);` after line 43; add `recordFirstResponse(id: string): Observable<Ticket>` → `POST tickets/${id}/first-response` with `{}`.

### 12 — Ticket Details: SLA card and Mark as Responded

**Create file: `frontend/src/app/features/tickets/ticket-sla.ts`** — pure display rules (kept separate so `ticketActions`' parameter type, used by `ticket-workflow.spec.ts`, does not change):

```ts
/** Display only — the server re-checks. SuperAdmin or the assigned Agent, tracked, not yet recorded, Open/InProgress. */
export function canMarkFirstResponse(ticket: Pick<Ticket, 'status' | 'assignedAgentUserId' | 'sla'>, user: Pick<CurrentUser, 'id' | 'role'> | null): boolean
```

**Create files: `frontend/src/app/features/tickets/ticket-sla-card.ts` + `.html`** — standalone OnPush component `crm-ticket-sla-card`, inputs `ticket = input.required<Ticket>()`, `canMarkResponded = input(false)`, `busy = input(false)`; output `markResponded = output<void>()`. Template inside the parent's `mat-card` content:
- `ticket().sla === null` ⇒ "Not tracked — this ticket was created before SLA tracking or while no SLA policy was enabled." (`crm-status-badge` "Not tracked", neutral).
- Otherwise overall indicator badge + "Business calendar: {{ sla.timeZoneId }}" + hint (F3) "Due times were fixed when the ticket was created. Remaining business time is calculated with the current business calendar.", then one block per target ("First response", "Resolution"): target `formatBusinessMinutes(targetMinutes)` business time; due `{{ dueAtUtc | date: 'medium' }}`; state badge `Pending` (pending) / `Met` (success) / `Breached` (error) + `Warning` badge when `isWarning`; remaining: `businessMinutesRemaining >= 0` ⇒ "{{ format }} business time remaining", `< 0` ⇒ "Overdue by {{ format(-x) }} business time"; when completed: "Responded {{ completedAtUtc | date: 'medium' }} by {{ completedByName }}" / "Resolved {{ … }}"; a late completion still shows **Breached** with the actual time.
- `canMarkResponded()` ⇒ `<button mat-flat-button [disabled]="busy()" (click)="markResponded.emit()">Mark as Responded</button>` + hint "Records that you already responded to the customer outside the CRM. Nothing is sent."

**File: `frontend/src/app/features/tickets/ticket-detail-page.ts`** — import `TicketSlaCard` and `canMarkFirstResponse`; add `protected readonly canMarkResponded = computed(() => { const t = this.ticket(); return t ? canMarkFirstResponse(t, this.auth.currentUser()) : false; });` and `protected markResponded(): void { this.run(this.api.recordFirstResponse(this.id), 'First response recorded.'); }` (reuses `run`, lines 199–218, so 404/409 handling is unchanged).

**File: `frontend/src/app/features/tickets/ticket-detail-page.html`** — after the Summary card (closing `</mat-card>` on line 47) insert a new card "SLA" containing `<crm-ticket-sla-card [ticket]="current" [canMarkResponded]="canMarkResponded()" [busy]="busy() || saving()" (markResponded)="markResponded()" />`. Line 25: `{{ current.escalatedByName }}` ⇒ `{{ current.escalatedByName ?? 'Automation' }}`.

**File: `frontend/src/app/features/tickets/ticket-activity-list.ts`** — in `describeActivity` (14–38): `AssigneeChanged` with `a.isAutomatic` ⇒ `{ title: 'Assigned automatically', detail: a.reason ?? \`To ${a.toAgentName}\` }` (check before the existing branches); `Escalated` with `a.isAutomatic` ⇒ `{ title: 'Escalated automatically', detail: a.reason }`; new `case 'FirstResponseRecorded': return { title: 'First response recorded', detail: 'Manual acknowledgement — nothing was sent to the customer.' };`. The actor line in `ticket-activity-list.html` already prints `actorName`, which is "Automation" for system events.

### 13 — Ticket list and Agent Dashboard indicators

**File: `frontend/src/app/features/tickets/ticket-list-page.ts`** — add `'sla'` to `columns` after `'status'` (line 66); add `slaBreached: [null as boolean | null]` to `filters` (after line 79); include `f.slaBreached !== null` in `hasFilters` (line 130) and `slaBreached: f.slaBreached` in the request (near line 150); expose `slaIndicatorLabel`, `slaIndicatorTone`.

**File: `frontend/src/app/features/tickets/ticket-list-page.html`** — after the Escalation filter (lines 43–50) add an "SLA" `mat-select` (`Any` / `Breached` = `true` / `Not breached` = `false`); after the `status` column (ends line 106) add

```html
        <ng-container matColumnDef="sla">
          <th mat-header-cell *matHeaderCellDef>SLA</th>
          <td mat-cell *matCellDef="let t">
            <crm-status-badge [label]="slaIndicatorLabel(t.sla?.indicator ?? 'NotTracked')" [tone]="slaIndicatorTone(t.sla?.indicator ?? 'NotTracked')" />
          </td>
        </ng-container>
```

**File: `frontend/src/app/features/agent-dashboard/agent-dashboard-models.ts`** — add `slaIndicator: SlaIndicator;` to `AttentionTicket` (line 23). **`agent-dashboard-page.ts`** expose the two helpers; **`agent-dashboard-page.html`** after the status badge (line 80) add `@if (t.slaIndicator !== 'NotTracked') { <crm-status-badge [label]="slaIndicatorLabel(t.slaIndicator)" [tone]="slaIndicatorTone(t.slaIndicator)" /> }`. Counts and order unchanged.

### 14 — Bell and Notifications page (shared with DASH-002)

**File: `frontend/src/app/core/notifications/notification-bell.ts`** — inject `SlaAlertsApi`. In the constructor's `switchMap` (lines 47–52): for `role === 'Agent' || role === 'SuperAdmin'` poll `merge(timer(0, REMINDER_POLL_MS), this.refresh.requests).pipe(exhaustMap(() => forkJoin({ reminders: role === 'Agent' ? this.api.check().pipe(catchError(() => of(null))) : of(null), sla: this.slaAlerts.unreadCount().pipe(catchError(() => of(null))) })))`; else `of(null)`. `unread` = sum of the non-null counts (null when both failed). Keep the reminder snackbar logic for `reminders.issuedNow > 0`; add an SLA snackbar "You have new SLA alerts." when the SLA unread count **increased** compared with the previous successful poll (track it in a private field; reset on role change). Label: `Notifications, N unread`. Update the class comment (lines 14–18): still the only poll in the app; now for all staff.

**File: `frontend/src/app/core/layout/shell/shell.ts`** — replace `isAgent` (line 35) with `isStaff = computed(() => this.auth.role() === 'Agent' || this.auth.role() === 'SuperAdmin')`; **`shell.html`** line 37 `@if (isStaff())`.

**File: `frontend/src/app/app.routes.ts`** — line 52 `roleGuard('Agent')` ⇒ `roleGuard('SuperAdmin', 'Agent')`; after the `collaboration` route (ends line 64) add

```ts
      {
        path: 'sla',
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/sla/sla.routes').then(m => m.SLA_ROUTES),
      },
```

**File: `frontend/src/app/core/layout/navigation.ts`** — after the `Agents` item (line 21) add `{ label: 'SLA & Automation', icon: 'timer', path: '/sla', exact: false, roles: ['SuperAdmin'] },`.

**Create files: `frontend/src/app/features/notifications/sla-alert-list.ts` + `.html`** — standalone component `crm-sla-alert-list`, same Subject + `switchMap` + per-request `catchError` + "Load more" dedupe pattern as `notifications-page.ts` lines 43–88 but over `SlaAlertsApi.list`; unread-only toggle; rows: link `['/tickets', a.ticketId]` with `ticketSubject`, badge `Warning` (pending) / `Breach` (error), kind text "First response" / "Resolution", "Due {{ dueAtUtc | date: 'medium' }} · Raised {{ createdAtUtc | date: 'medium' }}", "Mark as read" ⇒ `markRead` ⇒ `ReminderBadgeRefresh.next()`; 404 ⇒ remove row + snackbar "This alert's ticket is no longer assigned to you." Empty state "No SLA alerts." Hint under the heading: "SLA alerts are created by the CRM while it is running; nothing is sent outside the CRM."

**File: `frontend/src/app/features/notifications/notifications-page.ts` / `.html`** — import `SlaAlertList` and `AuthService`; `protected readonly isAgent = computed(() => this.auth.role() === 'Agent')`; in the constructor call `this.load(1)` only when `isAgent()` (SuperAdmin must never call `/notifications/check` — 403). Template: page header subtitle "SLA alerts and task reminders appear here while you use the CRM. Reading one changes nothing on the ticket or task."; first section `<h2>SLA alerts</h2><crm-sla-alert-list />`; then wrap the entire existing reminder markup (lines 3–55) in `@if (isAgent()) { <h2>Task reminders</h2> … }`.

### 15 — SuperAdmin "SLA & Automation" screens (`frontend/src/app/features/sla/`)

**Create file: `sla.routes.ts`** — `export const SLA_ROUTES: Routes = [{ path: '', title: 'SLA & Automation | Support CRM', loadComponent: () => import('./sla-admin-page').then(m => m.SlaAdminPage) }];`

**Create files: `sla-admin-page.ts` + `.html`** — `crm-page-header` "SLA & Automation" (subtitle "Targets, business hours, automation rules and SLA status. Changes apply to tickets created afterwards; existing deadlines never change.") + `mat-tab-group` with four tabs, each hosting one child component (lazy content via `<ng-template matTabContent>`):

1. **`sla-policy-form.ts/.html`** — loads `getPolicy()`; "No SLA configured" banner when `!isConfigured`; enabled `mat-slide-toggle`; a row per `TICKET_PRIORITIES` with two number inputs (minutes, `min 1`, integer) and a live hint `formatBusinessMinutes`; Save ⇒ `savePolicy` ⇒ snackbar "SLA policy saved." ; 400 field errors via `describeHttpError`; "Last saved {{ updatedAtUtc | date }} by {{ updatedByName }}".
2. **`business-calendar-form.ts/.html`** — timezone `mat-form-field` input with `<datalist>` from `Intl.supportedValuesOf('timeZone')` (fallback to free text when unavailable) and hint "IANA id, e.g. Africa/Cairo"; seven `mat-checkbox`es (`WEEK_DAYS`); two `<input type="time">` fields (send `HH:mm:00`); holidays: date input + name + "Add", list with remove buttons, sorted by date; client validation (≥1 day, open < close, unique dates) mirrors the server; hint above Save (F3): "Changes apply to tickets created afterwards. Existing due times never change; remaining business time shown on existing tickets uses the current calendar."; Save ⇒ `saveCalendar`. **Due-date preview** box: `datetime-local` start (default now) + minutes ⇒ `previewDue` ⇒ "Due {{ dueAtUtc | date: 'full' }} (your time) — calendar zone {{ timeZoneId }}". Conflict 409 "Save the business calendar first." shown inline.
3. **`automation-rules.ts/.html`** — table (name, trigger, conditions summary, action, parameters, order, enabled badge, warning "Target agent inactive" when `targetAgentIsActive === false`) + "New rule" / row "Edit" opening an inline edit card (no `MatDialog` — it is not used anywhere in `frontend/src` today; keep to inline cards like the rest of the app). Form: name, enabled toggle, trigger select ⇒ action select filtered by `ACTIONS_BY_TRIGGER` (reset action when invalid), category/priority selects with "Any", assigned select (Any/Assigned/Unassigned — hidden for `TicketCreated`), SLA kind select (Both/First response/Resolution — hidden for `TicketCreated`), target Agent select (only for `AssignToAgent`; options from `AgentsApi.list()` filtered `isActive`), warning lead minutes (only for `SlaWarningDue`), sort order. Read-only recipient text: warning ⇒ "Assigned agent, or SuperAdmins when unassigned"; breach notify ⇒ "Assigned agent and SuperAdmins". Explanatory line: "Notify and escalate on breach are two separate rules." Save ⇒ create/update ⇒ reload list.
4. **`sla-overview.ts/.html`** — `getOverview()` on open + Refresh button: three counts (Warning / Breached / On track, with "of the first 500 active tracked tickets" hint), two lists linking to `/tickets/:id` with assignee and due/overdue text, "Recent automation" list (time, rule, ticket link, outcome, detail). Empty and error/Retry states.

All four use the established loading/error/Retry pattern (`agent-dashboard-page.ts` lines 47–75) and `--crm-*` spacing tokens; forms collapse to one column below ~600 px (reuse the `.form-grid` classes from `ticket-detail-page.scss` where suitable or a local SCSS with a single media query).

### 16 — Frontend unit test (the single new one)

**Create file: `frontend/src/app/features/sla/sla-api.spec.ts`** — pattern `tickets-api.spec.ts`:

```ts
describe('SlaApi', () => {
  it('saves the business calendar with working days, hours and holidays and returns the typed calendar', () => {
    // TestBed with provideHttpClient(), provideHttpClientTesting(), API_CONFIG { baseUrl: '/api', version: 'v1' }
    // api.saveCalendar({ timeZoneId: 'Africa/Cairo', workingDays: ['Sunday','Monday','Tuesday','Wednesday','Thursday'],
    //   opensAt: '09:00:00', closesAt: '17:00:00', holidays: [{ date: '2030-01-06', name: 'Example holiday' }] })
    // expectOne(r => r.url === '/api/v1/sla/calendar'); method PUT; request.body deep-equals the payload
    // flush { isConfigured: true, …same…, updatedAtUtc: '2030-01-01T00:00:00Z', updatedByName: 'System Administrator' }
    // expect(result?.isConfigured).toBe(true); expect(result?.holidays[0].date).toBe('2030-01-06'); http.verify();
  });
});
```

---

## Documentation Tasks

### 17 — `docs/sla-and-automation.md` (new)

Sections: 1 Decisions S1–S12 (approved) and conflicts C1–C8; 2 Role/route matrix (API + Angular); 3 SLA policy and ticket snapshot (columns, "new tickets only", Not tracked); 4 Business-time rules (windows, holidays, start outside hours, DST gap/ambiguity handling, IANA ids via `TimeZoneInfo`, UTC storage; **S4: stored due instants never change**; remaining/elapsed business minutes and warning thresholds use the current calendar — one shared calculation for display and worker (F3)); 5 First response and resolution measurement (S2, S10); 6 Rules engine (triggers/conditions/actions table, valid combinations, ordering, `EnabledAtUtc`, inactive Agent skip, each warning rule evaluated against its own lead — F1); 7 Worker (hosted service, interval config, startup catch-up, per-item transactions, idempotency key, failure logging, candidate selection with rule conditions in SQL + `due, Id` ordering + in-pass exclusion paging (F2), **single instance only — multiple replicas are out of scope and would duplicate polling work although the unique key still prevents duplicate effects**); 8 Alerts (recipients S7, visibility, read state, no external delivery S12); 9 UI; 10 Migration impact; 11 Limitations (overview cap 500, worker 200 × 10 pages per rule and kind per pass, no pause/reopen cycles, no customer alerts).

### 18 — Other docs

- `docs/ticket-management.md` — §5 Escalation (line 65): automatic escalation by `SlaBreached` rules, `EscalatedByUserId` null = Automation; §6 Activity history (line 74): `ActorUserId` nullable = Automation, new `FirstResponseRecorded`, automatic `AssigneeChanged` reason; §9 API (line 115): `POST /tickets/{id}/first-response`, `slaBreached` filter, `sla` on `TicketResponse`; §12 (line 144): link to the new doc.
- `docs/agent-productivity-and-collaboration.md` §4 Reminders (line 61): one sentence — the bell/inbox now also shows SLA alerts (SLA-001); reminder rules unchanged.
- `docs/persistence-and-transactions.md` (after line 8): the worker's per-item `ExecuteInTransactionAsync` pattern and the unique execution key.
- `README.md` line 3: append "and SLA & basic automation (`SLA-001`)" to the delivered-stories sentence (fix the missing comma before "agent productivity" while there).

---

## Edge Cases & Failure Modes

- **SLA not configured / disabled:** policy or calendar missing, or policy disabled ⇒ `TicketCreationAutomation.ApplyAsync` writes no SLA columns ⇒ `sla: null` ⇒ "Not tracked" everywhere; `POST first-response` ⇒ 409 "This ticket has no SLA tracking."; worker finds no candidates. Admin pages show "No SLA configured".
- **Pre-existing tickets:** all eight columns `NULL` after migration ⇒ "Not tracked"; never back-filled; existing activity rows keep their actor (`AlterColumn` changes only nullability).
- **Ticket created outside business hours / on a holiday:** calculator starts at the next window (`AddBusinessMinutes` `from = max(start, open)` and skips non-working dates). Verified by the unit test and the preview endpoint.
- **Calendar with zero business time:** blocked at save (validator: ≥1 working day, `OpensAt < ClosesAt`; domain re-checks). If somehow reached, `AddBusinessMinutes` throws `DomainException` after 3,660 days ⇒ ticket creation 422 rather than a wrong deadline.
- **Unknown timezone id:** 400 `timeZoneId`. A timezone that later disappears from the OS ⇒ `ToRules()` throws `TimeZoneNotFoundException` ⇒ ticket creation 500 and worker pass logged; fix by re-saving the calendar. Documented as an operator issue.
- **DST transitions** (e.g. a zone whose 09:00 falls in a spring-forward gap): `BusinessCalendarRules.ToUtc` moves to the first valid minute; ambiguous times use standard time (`BusinessCalendarRules.cs`). Window length may be 1 h shorter/longer that day — expected.
- **Policy or calendar edited after tickets exist:** stored due instants unchanged (S4); remaining/elapsed business minutes and warning thresholds use the current calendar (documented simplification, shown on the SLA card and calendar form); the worker and the display use the same helpers, so a ticket shown as "Warning" is the ticket the worker alerts on (F3); new tickets use new values.
- **Several matching warning rules with different leads (F1):** each fires at its own threshold (one alert per rule, S11); the display shows Warning as soon as any matching rule's threshold is reached.
- **Many tickets that do not match a rule (F2):** excluded in SQL before the 200 limit, so they never crowd out matching tickets. **A ticket that keeps failing** (e.g. persistent 409): attempted once per pass, then excluded from later pages of that pass; tickets behind it are still processed. More than 2,000 eligible items for one (rule, kind) ⇒ the rest next pass, oldest due first.
- **Priority changed after creation:** deadlines stay (snapshot at creation, S1/S4); documented. Category/priority changes do affect which warning/breach rules match (conditions are evaluated on current ticket data).
- **Mark as Responded twice / two tabs:** second call ⇒ 409 "The first response was already recorded."; if both requests load before either saves, `Tickets.Version` makes the second save 409 (`EfUnitOfWork.cs` lines 23–27). The first timestamp is never overwritten.
- **Mark as Responded by a reassigned-away Agent:** `GetByIdAsync(id, AgentScope)` ⇒ 404 ⇒ the detail page's existing "no longer available" handling. Collaborator-only Agents ⇒ 404 (existing scope). Resolved/Closed ⇒ 409.
- **Response after the deadline:** stored; state **Breached** with the actual time; the breach rule (if its due ≥ `EnabledAtUtc` and not yet executed) still fires once for an Open/InProgress ticket.
- **Resolve, resume, reopen (C5):** first `Resolved` stamps `SlaResolvedAtUtc`; resume/reopen never clears it; the worker skips tickets with the stamp; display keeps Met/Breached from the first resolution.
- **Resolving an auto-escalated ticket (C4):** escalation auto-cleared as today; the `Escalated` (Automation) and `EscalationAutoCleared` events remain in history; the execution row prevents re-escalation if the ticket is resumed.
- **Already manually escalated at breach:** execution recorded with outcome `AlreadyEscalated`, no ticket change, no history event (S6).
- **Auto-assignment rule target deactivated after saving:** skipped at creation (inactive check), next rule evaluated; rules list shows "Target agent inactive". Saving a rule with an inactive/non-Agent/SuperAdmin target ⇒ 400 `targetAgentUserId`.
- **No assignment rule matches:** ticket stays unassigned (existing flow); SuperAdmin assigns manually via `PUT /tickets/{id}/assignee`. Warning/breach alerts for it go to SuperAdmins.
- **Agent or SuperAdmin creates with an assignee:** rules never run (C3). Verified in the matrix.
- **Rule disabled between candidate query and processing:** the item still runs in that pass (rules loaded at the pass start); the next pass excludes it. Acceptable for a 60-second poller; documented.
- **Rule enabled after old breaches:** `EnabledAtUtc` filter ⇒ old breaches ignored; only deadlines due after enabling fire (intake §14 item 3, "changes only subsequent events").
- **API stopped across a deadline:** worker's immediate first pass at startup processes it (candidates are "due ≤ now and no execution"); nothing is claimed to run while stopped.
- **Two polls / restart / replayed transaction:** `UX_AutomationRuleExecutions_RuleTicketKind` and `UX_SlaAlerts_ExecutionRecipient` make a duplicate insert fail; the item is logged as failed and the next query excludes it because the first execution exists. Verified by SQL counts after two polls and a restart. These indexes are **not** added to `EfUnitOfWork.KnownUniqueIndexes` (a violation is a defect, not a user conflict).
- **Worker vs user edit on the same ticket (C6):** escalation save ⇒ `ConflictException` ⇒ transaction rolled back (execution row too) ⇒ logged ⇒ retried next pass.
- **Database down / migrations pending at startup:** each pass logs an error and the host keeps serving; the first pass after the database returns catches up.
- **Worker disabled (`SlaAutomation:Enabled=false`):** one information log; no alerts/escalations; ticket SLA display still works (computed on read).
- **Invalid poll interval** (e.g. `00:00:01`): options validation fails at startup with the explicit message (`ValidateOnStart`).
- **No active SuperAdmin and unassigned ticket:** execution recorded with detail "No active recipient"; nothing retried every minute.
- **Alert for a ticket reassigned away:** hidden for the former Agent (visibility predicate), still visible to SuperAdmin recipients; mark-read ⇒ 404 ⇒ row removed with snackbar. The new assignee does **not** inherit old alerts (only future ones) — documented difference from DASH-002 reminder transfer.
- **SuperAdmin on `/notifications`:** sees SLA alerts only; never calls `/notifications/check` (would be 403). Agent sees both sections; reminder behaviour unchanged.
- **Customer token** on any new endpoint ⇒ 403 (policies); Agent token on `/sla/*` or `/automation-rules/*` ⇒ 403; `/sla` route redirects non-SuperAdmins home (`roleGuard`).
- **Malformed input:** integer enums rejected (`allowIntegerValues: false`) ⇒ 400; missing nullable-enum fields ⇒ 400 from validators; `"opensAt": "9am"` ⇒ JSON binding 400; duplicate holiday dates ⇒ 400; preview minutes 0 ⇒ 400.
- **`slaBreached` filter:** evaluated in SQL on stored instants, combined with the Agent scope (scope first, `TicketRepository.Scope`); `Warning` is not filterable (business-time based) — the overview lists warnings instead.
- **Large lists:** overview evaluates ≤ 500 active tracked tickets; worker ≤ 200 × 10 candidates per (rule, kind) per pass (the rest next pass, oldest due first). Documented.
- **Half-applied migration:** see Migration / Rollback.

---

## Test Plan

1. **Unit (backend, new — the only one):** `backend/tests/Crm.UnitTests/Sla/TicketCreationAutomationTests.cs` — `ApplyAsync_UnassignedTicketCreatedThursdayAfternoon_PersistsBusinessDeadlinesAcrossWeekendAndHolidayAndAssignsFirstActiveMatchingRule` (task 10). Covers capabilities 1, 2, 3, 5 at domain/application level. Hand-written fakes.
2. **Unit (frontend, new — the only one):** `frontend/src/app/features/sla/sla-api.spec.ts` — "saves the business calendar with working days, hours and holidays and returns the typed calendar" (task 16).
3. **Existing tests:** all pass. `CreateTicketCommandHandlerTests.cs` receives the C8 constructor fix only. Record the baseline counts before starting (`dotnet test`, `npm test -- --watch=false`; expected 8 and 8 at `6b569b2`) ⇒ after: baseline + 1 each.
4. **No** integration, API, SQL, E2E or Playwright suites; no new packages (intake §14).
5. **Manual (API via `Invoke-RestMethod`/curl or Postman CLI, SQL via `sqlcmd`, browser):** Verification Steps 4–15 — the mandatory nine-capability matrix.

---

## Migration / Rollback

- **Before applying:** baselines `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT COUNT(*) FROM Tickets; SELECT COUNT(*) FROM TicketActivities; SELECT COUNT(*) FROM Customers; SELECT COUNT(*) FROM AgentTasks; SELECT COUNT(*) FROM TaskReminderNotifications; SELECT COUNT(*) FROM CustomerNotes; SELECT COUNT(*) FROM AspNetUsers;"`. After applying: identical counts; seven new tables exist and are empty; `SELECT COUNT(*) FROM Tickets WHERE SlaFirstResponseDueAtUtc IS NOT NULL` = 0.
- **Changes to existing tables:** `Tickets` +8 nullable columns, +1 FK, +2 filtered indexes; `TicketActivities.ActorUserId` NOT NULL ⇒ NULL. No data rewritten.
- **Rollback (local):** `dotnet ef database update AddCustomerNotesInteractionsAttachments --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development`. **Caveat:** reverting `ActorUserId` to NOT NULL fails if automation activities (NULL actor) exist; before rolling back run `SELECT COUNT(*) FROM TicketActivities WHERE ActorUserId IS NULL` and, if non-zero, decide with the user (these rows are history and must not be deleted silently). Then `dotnet ef migrations remove …` if the migration is discarded.
- **Half-applied state:** SQL Server runs the migration in one transaction ⇒ no partial schema and no `__EFMigrationsHistory` row on failure; fix and re-run. While pending, the worker logs one error per interval and ticket reads fail with 500 (new columns missing) — apply the migration before using the app.

---

## Verification Steps

1. **Backend builds and tests:** in `backend/`: `dotnet build` (0 errors; list pre-existing warnings separately), `dotnet test` (baseline + 1 passed).
2. **Migration review and layering:** in `backend/`:
   - `dotnet ef migrations list --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ only `…_AddSlaAndAutomation (Pending)`.
   - `dotnet ef migrations script AddCustomerNotesInteractionsAttachments AddSlaAndAutomation --project src/Crm.Infrastructure --startup-project src/Crm.Api --output $env:TEMP/AddSlaAndAutomation.sql -- --environment Development` ⇒ matches task 9 (7 `CREATE TABLE`, 8 nullable `ADD`, one `ALTER COLUMN [ActorUserId] uniqueidentifier NULL`, unique index **without** `WHERE`, no `DROP TABLE`/`UPDATE`/`DELETE`).
   - Baselines (Migration / Rollback) ⇒ `dotnet ef database update …` ⇒ counts unchanged.
   - `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ no changes.
   - `grep -rn "EntityFrameworkCore\|Microsoft.AspNetCore" src/Crm.Application src/Crm.Domain --include=*.cs` ⇒ nothing. (`ILogger<T>` in `RunSlaAutomationCommandHandler` is fine: `Crm.Application.csproj` already references `Microsoft.Extensions.Logging.Abstractions` 10.0.13, used by `LoggingBehavior.cs` line 7.)
3. **Frontend builds and tests:** in `frontend/`: `npx ng build` (no errors; budget warnings acceptable), `npm test -- --watch=false` (baseline + 1).
4. **Setup (nothing existing deleted/reset/reassigned):** `.\run-dev.ps1` with `SlaAutomation__PollInterval=00:00:15` set in the shell for faster checks (state it in the report). As SuperAdmin create Agents "Sla Alpha" (`sla.alpha@example.test`), "Sla Bravo" (`sla.bravo@example.test`), and register Customer `sla.customer@example.test`; create contact-only customer "Sam SLA". Tokens `$admin`, `$alpha`, `$bravo`, `$customer` from `POST /api/v1/auth/login` (never paste into the report).
5. **Capability 1 — Policy:** `GET /api/v1/sla/policy` ⇒ `isConfigured: false`. `PUT` with 3 targets ⇒ 400; duplicate priority ⇒ 400; 0 minutes ⇒ 400; four valid targets, `isEnabled: true` (demo: `Urgent` 2/4, `High` 240/960, `Medium` 480/1440, `Low` 960/2880) ⇒ 200. With `$alpha` ⇒ 403; `$customer` ⇒ 403. SQL `SELECT * FROM SlaPolicies; SELECT * FROM SlaPolicyTargets;` ⇒ 1 + 4 rows, priorities by name.
6. **Capability 2 — Calendar:** `PUT /api/v1/sla/calendar` `{ "timeZoneId":"Africa/Cairo","workingDays":["Sunday","Monday","Tuesday","Wednesday","Thursday"],"opensAt":"09:00:00","closesAt":"17:00:00","holidays":[{"date":"2030-01-06","name":"Example holiday"}] }` ⇒ 200. Invalid: `"Mars/Base"`, `workingDays: []`, `opensAt` ≥ `closesAt`, duplicate dates ⇒ 400 each. Preview `GET /api/v1/sla/calendar/preview?startUtc=2030-01-03T14:00:00Z&minutes=240` ⇒ `dueAtUtc` `2030-01-07T10:00:00Z` (weekend + holiday); remove the holiday ⇒ `2030-01-06T10:00:00Z`; `startUtc=2030-01-04T08:00:00Z` (Friday) `minutes=60` ⇒ Sunday 10:00 local. Restore the holiday. For live worker checks, then save a **demo** calendar covering now (all seven days, `00:00:00`–`23:59:00`) and note that it is demo configuration.
7. **Capability 8/1 — Snapshot on create:** create ticket T1 (High, Technical, unassigned) as SuperAdmin ⇒ response `sla.firstResponse.dueAtUtc`/`resolution.dueAtUtc` present, `indicator` `OnTrack`. SQL `SELECT Id, CreatedAtUtc, SlaFirstResponseDueAtUtc, SlaResolutionDueAtUtc, SlaTimeZoneId FROM Tickets WHERE Id='<T1>'` ⇒ UTC instants, `Africa/Cairo`. An older ticket ⇒ `sla: null`. Change the policy (High 300) ⇒ T1's stored due unchanged; a new ticket uses 300 (S4).
8. **Capabilities 3 + 5 — Assignment rules:** create rule R1 `TicketCreated`/`AssignToAgent`, `categoryCondition: Billing`, target Sla Alpha, order 1. Invalid saves ⇒ 400: `TicketCreated`+`EscalateTicket`; missing target; target = SuperAdmin id; `SlaWarningDue` without lead; `$alpha` ⇒ 403. Create Billing ticket T2 unassigned ⇒ assigned to Alpha; activity shows "Assigned automatically" by "Automation" with the rule name; SQL `SELECT RuleId, TicketId, SlaKind, Outcome FROM AutomationRuleExecutions` ⇒ one `Assigned` row; `SELECT ActorUserId, Type, Reason FROM TicketActivities WHERE TicketId='<T2>'` ⇒ `NULL`, `AssigneeChanged`. General ticket T3 ⇒ stays unassigned. SuperAdmin creates Billing ticket with explicit assignee Bravo ⇒ stays Bravo. Agent Bravo creates a Billing ticket ⇒ self-assigned (rules not applied). Disable R1 ⇒ next Billing ticket unassigned; T2 unchanged. Re-enable; deactivate Alpha (`/agents`) ⇒ next Billing ticket unassigned (inactive skip) and the rules list shows "Target agent inactive"; reactivate Alpha.
9. **Capability 4/8 — First response:** as Alpha on T2 `POST /api/v1/tickets/<T2>/first-response` ⇒ 200, `sla.firstResponse.completedAtUtc` = server time, `completedByName` "Sla Alpha", state `Met`; repeat ⇒ 409; as Bravo ⇒ 404; on a not-tracked ticket ⇒ 409; on a Closed ticket ⇒ 409. Activity shows "First response recorded" (no message sent — confirm no new rows in any communication table; none exist). Late response: Urgent ticket T4 (2-minute response target), wait past due (demo calendar), mark ⇒ state `Breached` with actual time kept.
10. **Capability 8 — Resolution:** Urgent T5 resolved within 4 minutes ⇒ `resolution.state` `Met`; Urgent T6 resolved after 4 minutes ⇒ `Breached`; resume T6 (`Resolved→InProgress`) ⇒ `resolution.completedAtUtc` unchanged (S10); full workflow on T5 (Close, Reopen) still works.
11. **Capabilities 4 + 6 + 7 — Worker, warning, breach, escalation, dedup:** rules: R2 `SlaWarningDue`/`CreateInAppNotification` lead 1 (kind Both); R3 `SlaBreached`/`CreateInAppNotification`; R4 `SlaBreached`/`EscalateTicket` kind `FirstResponse`. Create Urgent T7, assign to Alpha (SuperAdmin `PUT assignee`). Within ~1 minute: `GET /api/v1/sla-alerts` as Alpha ⇒ one `Warning`/`FirstResponse`. After the 2-minute due: Alpha ⇒ one `Breach`; SuperAdmin ⇒ one `Breach`; T7 `isEscalated: true`, `escalatedByName` null, activity "Escalated automatically" by Automation. Wait two more polls, then restart the API ⇒ SQL `SELECT RuleId, SlaKind, COUNT(*) FROM AutomationRuleExecutions WHERE TicketId='<T7>' GROUP BY RuleId, SlaKind` ⇒ each 1; `SELECT Type, Kind, RecipientUserId, COUNT(*) FROM SlaAlerts WHERE TicketId='<T7>' GROUP BY Type, Kind, RecipientUserId` ⇒ each 1; `SELECT COUNT(*) FROM TicketActivities WHERE TicketId='<T7>' AND Type='Escalated'` ⇒ 1. Manually escalated Urgent T8 before breach ⇒ execution `AlreadyEscalated`, still one `Escalated` activity. Stop the API across T9's deadline, start it ⇒ breach processed on the first pass (log line). Disabled R3 ⇒ no new breach alerts for later tickets; re-enabling does not alert old breaches (`EnabledAtUtc`). Unassigned breached ticket ⇒ alert only for SuperAdmin. **F1:** add R5 `SlaWarningDue` lead 3, `priorityCondition: High`, next to R2 (lead 1), and temporarily set the High first-response target to 5 minutes: a new High ticket shows indicator `Warning` once ≤ 3 business minutes remain (before R2's threshold), R5's alert arrives then and R2's about a minute before due — two warning executions, one per rule. **F2:** create R6 `SlaBreached`/`CreateInAppNotification` with `categoryCondition: Billing`; with at least one breached General ticket and one breached Billing ticket, SQL `SELECT TicketId FROM AutomationRuleExecutions WHERE RuleId='<R6>'` ⇒ only the Billing ticket, and the EF SQL log shows the `[Category]` predicate inside the candidate query before `TOP`. Restore the High target afterwards.
12. **Capability 7 — Alerts UI:** as Alpha the bell badge counts SLA alerts (+ reminders); `/notifications` shows "SLA alerts" (Warning/Breach badges, kind, due) and "Task reminders" (DASH-002 behaviour unchanged: schedule a 1-minute reminder ⇒ still delivered once). Mark an SLA alert read ⇒ badge decreases. As SuperAdmin the bell appears, `/notifications` shows only SLA alerts and no 403 in the network tab. Reassign T7 to Bravo ⇒ Alpha's T7 alerts disappear (`GET` no longer lists them; `PUT …/read` ⇒ 404); Bravo does not see them; SuperAdmin still does. Invite Alpha as collaborator on T7 ⇒ still hidden for Alpha.
13. **Capability 8 — List/dashboard + authorization (§14 item 9):** `/tickets` SLA column shows On track / Warning / Breached / Met / Not tracked; filter "Breached" ⇒ only breached; `GET /api/v1/tickets?slaBreached=true` with `$bravo` ⇒ only Bravo's assigned ones. My Dashboard (Alpha) queue shows SLA badges; counts identical to before the change (compare with the DASH-001 counts endpoint before/after). `$customer` ⇒ 403 on `/tickets`, `/sla-alerts`, `/sla/*`, `/automation-rules`. Anonymous ⇒ 401.
14. **Capability 9 — Admin UI (browser):** as SuperAdmin `/sla`: Policy tab (not-configured banner on a fresh DB is not reproducible here — verify the text via the empty `GET` state in step 5 or note it), edit and save; Calendar tab: weekdays, hours, add/remove holiday, preview shows due; Rules tab: create each valid combination, invalid combinations not offered, edit, disable, order; Overview tab: counts and lists with working ticket links and recent automation rows. As Alpha `/sla` redirects home and the nav item is absent. Ticket Details SLA card: tracked vs Not tracked, Mark as Responded visible only when allowed, hint text, late-response Breached display. Error/empty states (stop the API ⇒ each tab shows its error + Retry). Narrow width (~400 px DevTools): tabs scroll, forms stack, no horizontal page scroll.
15. **Regression smoke (§14 item 11):** sign in/out each role; customer list/create + notes/interactions/attachments (CUST-001/002); ticket create/edit/assign/transition/escalate/clear/activity (TICK-001/002); My Dashboard (DASH-001); task + reminder, quick-reply copy, collaboration post (DASH-002) ⇒ unchanged.
16. **OpenAPI:** `curl -s http://localhost:5080/openapi/v1.json` lists `/api/v1/sla/policy`, `/sla/calendar`, `/sla/calendar/preview`, `/sla/overview`, `/automation-rules`, `/automation-rules/{id}`, `/sla-alerts`, `/sla-alerts/unread-count`, `/sla-alerts/{id}/read`, `/tickets/{id}/first-response`, each with "Requires role:"; enums as strings.
17. **Record honestly:** list which of steps 4–16 were run, with observed results; anything not run (especially browser steps 12–14) is **Unverified**, not passed. Report `dotnet build`, `dotnet test`, `ng build`, `npm test`, migration status, the poll interval used, and remaining blockers; keep pre-existing warnings separate.

---

## Done Criteria

- [ ] SuperAdmin configures one global SLA policy with positive business-minute first-response and resolution targets for Low/Medium/High/Urgent; Agents/Customers get 403 (§14.1).
- [ ] One global timezone-aware business calendar (working weekdays, one daily interval, holidays) is configurable and validated; deadlines skip closed days and holidays and are stored as UTC (§14.2; unit test + preview).
- [ ] New tickets persist first-response/resolution targets, due instants and calendar timezone when SLA is configured and enabled; older tickets show "Not tracked"; policy/calendar edits never change stored deadlines (S1, S4).
- [ ] Rules engine supports exactly `TicketCreated`/`SlaWarningDue`/`SlaBreached`, the four conditions, the three actions, valid combinations only, enable/disable and ordering, all SuperAdmin-only and validated on save (S9).
- [ ] New unassigned tickets are auto-assigned once by the first matching active-Agent rule, attributed to Automation; non-matching tickets stay unassigned; explicit/self assignments are never overridden; disabled rules and inactive Agents are skipped (§14.3, §14.8).
- [ ] Mark as Responded records server UTC and actor once, sends nothing, and distinguishes Met from late (Breached) (§14.4).
- [ ] Resolution is measured from the first transition to Resolved; reopen/resume start no new cycle; existing workflow unchanged (§14.5, S10).
- [ ] The hosted worker runs at startup and every ~60 s, catches up after restart, and produces exactly one warning alert, one breach alert per recipient and one escalation per (rule, ticket, kind) even across polls and restarts (§14.6–14.7).
- [ ] Warning: the indicator shows Warning when **any** matching enabled warning rule reaches its own lead, and the worker alerts per rule at that same threshold, using the same calculation as the display (F1, F3).
- [ ] Worker candidate queries apply rule category/priority/assignment conditions in SQL before the 200 limit, ordered `due, Id`, with in-pass exclusion paging so no eligible ticket is skipped indefinitely (F2).
- [ ] Existing due instants never change after policy/calendar edits; the UI and docs state that remaining business time uses the current calendar (S4, F3).
- [ ] Alerts are persistent, read/unread, visible in the existing bell and Notifications page for Agents and SuperAdmin, and filtered by current ticket access; DASH-002 reminders still work (§14.6, §14.9).
- [ ] Ticket Details, ticket list (column + breached filter) and Agent Dashboard show backend-computed SLA states without changing DASH-001 counts or ownership (§11).
- [ ] `/sla` SuperAdmin screens (Policy, Calendar, Rules, Overview) work with loading/empty/error states and at mobile width (§14.10).
- [ ] Exactly one migration `AddSlaAndAutomation`, reviewed; existing data counts unchanged; no existing migration edited.
- [ ] Exactly one new backend test and one new frontend test; all existing tests pass; both builds succeed.
- [ ] Conflicts C1–C8 are documented in `docs/sla-and-automation.md` and the implementation report; docs/README updated.
- [ ] Manual API/SQL/browser matrix recorded honestly (unrun = Unverified).
- [ ] Nothing committed or pushed until the user reviews and accepts SLA-001.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
