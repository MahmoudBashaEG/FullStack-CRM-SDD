# Story 07 — Agent Productivity & Collaboration — Tasks, Reminders, Quick Replies & Team Discussions

## Prerequisites

- **Story 06 completed:** [06-story-agent-dashboard-ticket-overview-work-queue.md](06-story-agent-dashboard-ticket-overview-work-queue.md) (`DASH-001`, verified committed as `913eaac`; working tree clean apart from the untracked `.squad/stories/agent-dashboard/DASH-002/` intake): `AuthorizationPolicies.Agent`, `GET /api/v1/agent-dashboard`, `AgentDashboardController`, `IAgentDashboardRepository`, Angular `features/agent-dashboard/`, `TicketRepository.Scope` (now `internal`).
- **Story 05 completed:** [../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md) (`TICK-002`, `c2f7264`): live reassignment (`PUT /tickets/{id}/assignee`, SuperAdmin), `TicketActivity` (append-only), `Tickets.Version` rowversion ⇒ `EfUnitOfWork` 409, ticket detail page with workflow/assignment/activity cards.
- **Story 04 completed:** [../ticket-management/04-story-ticket-creation-management.md](../ticket-management/04-story-ticket-creation-management.md) (`TICK-001`, `b44c824`): `Ticket`, `ITicketRepository` (`FindDetailsByIdAsync(id, agentScope)`), `TicketCategory { General, Technical, Billing, Other }`, `CustomerPicker` autocomplete pattern.
- **Stories 01–03 completed:** [../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md) (Mediator/CQRS pipeline, `ITransactionalCommand`, `GlobalExceptionHandler`), [../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md) (JWT `sub`/`role`, `OnTokenValidated` active-account check, `AuthorizationPolicies`, `roleGuard`, `NAV_ITEMS.roles`, `IIdentityService.FindByIdAsync` / `ListByRoleAsync`), [../customer-management/03-story-customer-profiles-contact-management.md](../customer-management/03-story-customer-profiles-contact-management.md) (`PagedResponse<T>`, paging conventions).
- Applied migrations (verified in `backend/src/Crm.Infrastructure/Persistence/Migrations/`): `20261014234527_InitialCreate`, `20261015011303_AddIdentityAndRefreshSessions`, `20261017183245_AddCustomers`, `20261017194039_AddTickets`, `20261017203222_AddTicketWorkflowAndActivity`. **This story adds exactly one additive migration, `AddAgentProductivityAndCollaboration`, and edits none of the existing ones.**
- Tooling unchanged: .NET SDK **10.0.400** (`backend/global.json`), EF Core **10.0.13**, Mediator **3.0.2** (source-generated), FluentValidation **12.2.0**, xUnit **2.9.3**, Angular / Material / CDK **22.2**, Vitest via `@angular/build:unit-test`. **No new NuGet or npm packages** (`@angular/cdk/clipboard` and `@angular/material/badge` ship in the already-installed packages).
- Intake: `.squad/stories/agent-dashboard/DASH-002/intake.md` (no attachments; `attachments/` is empty). Second story of the `agent-dashboard` feature; one business story with five workstreams (Tasks & Reminders, Quick Replies, Team Collaboration, Dashboard integration, Docs & verification).
- **Business decisions — all five approved by the user** (intake §12): **D1** task ownership, **D2** quick-reply governance, **D3** Option B (primary-Agent-managed collaborator invitations with SuperAdmin oversight), **D4** Option B (persistent in-app reminders incl. missed-on-sign-in), **D5** append-only discussion retention. Record them as **approved** in the docs (intake Done criterion "Documentation records the proposed business rules as approved decisions").
- Shared contracts touched deliberately (additive only): `CrmDbContext` (+5 `DbSet`s), `Infrastructure/DependencyInjection.cs` (+4 repositories), `EfUnitOfWork` (+409 mapping for an explicit allow-list of business unique indexes only; `ExecuteInTransactionAsync` split into a retryable attempt and a never-replayed commit — R1), `Common/Exceptions` (+`ForbiddenException`, +`PreconditionRequiredException`, +`CommitOutcomeUnknownException`) and `GlobalExceptionHandler` (+403, +428, +503 arms), new `ITicketAccessLock` (row-locked read of a ticket's current assignee / a collaborator membership inside a transaction; reads only), new `Crm.Api/Http/EntityTags.cs` (ETag / `If-Match`), `AgentDashboardController` (+1 action, existing `Get` untouched), `NAV_ITEMS` (+3 entries), `app.routes.ts` (+4 child routes), `Shell` toolbar (+bell), `TicketDetailPage` (+2 cards), `AgentDashboardPage` (+1 independent section). **No change** to `TicketsController`, `TicketRepository`, `ITicketRepository`, `TicketActor`, `Ticket`, `TicketActivity`, any existing DTO, `GET /api/v1/agent-dashboard` or its repository.

---

## Story Goal

Complete the remaining three Agent Dashboard capabilities on top of DASH-001 without widening ticket access:

1. **Tasks & in-app reminders** — `AgentTask` records (Pending/Completed, archive/restore, optional due date, optional link to a ticket the owner is currently assigned to), owned by one active Agent. Agents create/manage their own; SuperAdmin creates for, manages and reassigns to any active Agent. One optional reminder per task; when due, the current owner receives **one** persistent in-app notification (bell + unread badge + inbox), surfaced by a 60-second poll while the app is open and as a **missed** reminder on the next sign-in. Idempotent server-side issuance; mark-as-read; no push/email/SMS/background job.
2. **Quick Replies** — plain-text templates: Agents own **Personal** templates, SuperAdmin owns **Shared** templates; search/filter/paging/archive/restore; **copy to clipboard only** (list page and ticket detail panel), labelled "Copies text only — does not send a message".
3. **Team Collaboration** — per-ticket **internal discussion** (append-only, plain text, author + UTC) and **collaborator membership** managed by SuperAdmin (any ticket) or the ticket's **current primary Agent** (own assigned ticket only). Invited Agents use a **separate** `/api/v1/collaborations/*` surface and `/collaboration/tickets/:id` page with minimal context (`id`, subject, status, priority) — never the existing Ticket APIs.
4. **Dashboard integration** — My Dashboard gains an independent **Tasks** section (Pending / Overdue / Due soon counts over the full owned set + next 5 tasks) via the new `GET /api/v1/agent-dashboard/tasks`, plus entry links to My Tasks, Notifications, Quick Replies and My Collaborations. DASH-001's four cards and top-10 queue are unchanged and keep rendering when the tasks section fails.
5. Exactly **one** new backend and **one** new frontend happy-path unit test; manual API / SQL / browser verification for all three groups; new `docs/agent-productivity-and-collaboration.md` plus doc/README updates.

**Not in scope** (do **not** build): outbound email/SMS/WhatsApp/customer replies, OS/browser push, Service Workers, WebSockets/SignalR, background schedulers/hosted services, recurring or multiple reminders, collaboration-message notifications, mentions, typing/presence, attachments, rich text/Markdown/HTML rendering, template variables/AI, message edit/delete, hard delete of any record, SuperAdmin notification inbox, changes to ticket visibility (`TicketRepository.Scope`), ticket APIs, Ticket Activity or DASH-001 metrics, Customer Portal access, new test frameworks/packages, integration/E2E suites.

### Recorded decisions

| Concern | Decision |
|---|---|
| Naming | Domain/Application namespace **`AgentTasks`** and type **`AgentTask`** (never `Task`/`Tasks` — clashes with `System.Threading.Tasks`). Tables `AgentTasks`, `TaskReminderNotifications`, `QuickReplyTemplates`, `TicketCollaborators`, `TicketInternalMessages`. |
| Actor | New `Crm.Application/Common/StaffActor.cs` `StaffActor(Guid UserId, bool IsSuperAdmin)` with `AgentScope` (same shape as `TicketActor`, `TicketActor.cs` lines 7–11, which stays untouched). Controllers build it from the JWT only, like `TicketsController.cs` line 107. |
| IDs | Tasks, templates, collaborator memberships: `Guid.CreateVersion7()` (as `CreateTicketCommand.cs` line 42). Notifications and messages: `bigint IDENTITY` (as `TicketActivityConfiguration.cs` line 14) — insertion order is the tie-breaker. |
| Routes (API) | `/api/v1/tasks` (Staff), `/api/v1/notifications` (Agent), `/api/v1/quick-replies` (Staff), `/api/v1/collaborations` (Staff; `GET /collaborations` Agent only), `GET /api/v1/agent-dashboard/tasks` (Agent, new action on the existing controller). State toggles follow the existing `POST`/`DELETE /tickets/{id}/escalation` precedent (`TicketsController.cs` lines 85–94). |
| Routes (Angular) | `/tasks`, `/tasks/new`, `/tasks/:id` (SuperAdmin, Agent); `/notifications` (Agent); `/quick-replies`, `/quick-replies/new`, `/quick-replies/:id` (SuperAdmin, Agent); `/collaboration`, `/collaboration/tickets/:id` (Agent). No conflicts in `app.routes.ts` lines 22–46. |
| 403 vs 404 | Out-of-scope records whose existence must not leak ⇒ **404** (`NotFoundException`). Visible-but-not-modifiable ⇒ **403** via new `ForbiddenException` (Agent editing a Shared template; collaborator-only Agent managing membership). Role-restricted endpoints ⇒ policy **403**. Invalid fields ⇒ **400**. State conflicts / stale rowversion / duplicate active membership ⇒ **409**. |
| Task ownership (D1) | Agent: owner = caller; another submitted `assignedAgentUserId` ⇒ 400 (mirrors `CreateTicketCommand.cs` lines 66–74). SuperAdmin: `assignedAgentUserId` **required**, must be an active Agent (`{ Role: Agent, IsActive: true }`, as line 82). Only SuperAdmin reassigns (`PUT /tasks/{id}/assignee`, SuperAdmin policy). |
| Ticket link | Optional. On create / link change / reassignment-with-link: the ticket must currently be assigned to the **task owner** — checked inside the command's transaction from the row-locked value returned by new `ITicketAccessLock.LockTicketAsync` (F3; no `ITicketRepository` change, so the two existing fakes stay compiling). Collaborator membership never satisfies this. |
| Link after ticket reassignment | Never auto-transferred/changed. Read model computes `LinkedTicketState` in SQL: `None`, `Accessible` (`ticket.AssignedAgentUserId == task.AssignedAgentUserId`), `Inaccessible`. For Agents an `Inaccessible` link returns **no** ticket id/subject; SuperAdmin always gets id + subject plus the state. Link set/removed only through `PUT`/`DELETE /tasks/{id}/ticket-link`, so a task edit can never unlink a hidden link by accident. |
| Reminder model (D4) | Columns on `AgentTasks`: `RemindAtUtc` (nullable) + `ReminderGeneration` (int). Scheduling a **different** time increments the generation; same time = no-op; cancel sets `RemindAtUtc = null`. Complete and Archive also set `RemindAtUtc = null` (cancel pending delivery); Reopen/Restore never recreate it. |
| Issuance & dedup | `POST /api/v1/notifications/check` (Agent) runs **one** parameterized `INSERT … SELECT` over the caller's own Pending, unarchived, due tasks. It reads `AgentTasks WITH (UPDLOCK, ROWLOCK)` — so it waits for any uncommitted reassignment / completion / archive of that task and then evaluates the **committed** row (required because `CrmDev` runs with `READ_COMMITTED_SNAPSHOT ON`, verified, where plain reads never wait) — and is guarded by `NOT EXISTS (… TaskReminderNotifications WITH (UPDLOCK, HOLDLOCK))`. Unique index `UX_TaskReminderNotifications_TaskGeneration (TaskId, ReminderGeneration)` is the hard guarantee. Recipient = the task's owner **as committed at issuance**. No timers on the server. |
| Reassigned reminders (F2) | **Not yet issued:** no action — the recipient is resolved at issuance, so the reminder follows the task (past-due ⇒ one missed reminder on the new owner's next check). **Issued and unread** (any generation of that task): **transferred** — the same row's `RecipientUserId` becomes the new owner and `TransferredFromUserId` / `TransferredAtUtc` record the last move; never a second row, so `(TaskId, ReminderGeneration)` stays unique and the new owner sees exactly one unread item. Read state is never reset: only unread rows move and they stay unread (`ScheduledForUtc`, `IssuedAtUtc` unchanged). **Issued and read:** not transferred — kept as the reader's history, hidden from every inbox while the reader does not own the task (visible again if the task returns to them). **Transaction:** `ReassignAgentTaskCommand : ITransactionalCommand` — rowversion-checked `UPDATE AgentTasks` (takes the task row's X lock) ⇒ set-based `UPDATE TaskReminderNotifications … WHERE TaskId = @id AND ReadAtUtc IS NULL` ⇒ commit. The former owner loses the items immediately (inbox reads require recipient = current owner). |
| Missed (F6) | `WasMissed = IssuedAtUtc > ScheduledForUtc + 5 min` (`TaskReminderRules.MissedThreshold`). It records **delayed issuance** only: no check reached the server near the scheduled time (CRM closed, signed out, tab suspended by the browser, network or API unavailable). It does **not** prove the Agent was offline or absent and drives nothing but the label "Missed reminder" + hint "Delivered more than 5 minutes after its scheduled time." Delivery itself is unchanged: a missed reminder is issued by the first check after the next sign-in / app open. |
| Task concurrency (F1) | End-to-end optimistic concurrency on **every** `AgentTask` mutation. `AgentTask.Version` (SQL `rowversion`, mapped as a domain `byte[]` property) is returned as `version` (base64) in every `AgentTaskResponse` and as a strong `ETag: "<base64>"` on `GET /tasks/{id}`, `POST /tasks` and every task mutation response. Every task mutation **requires** `If-Match: "<version>"`: missing ⇒ **428** (`PreconditionRequiredException`); `*`, weak `W/…`, several values, non-base64, or a value that does not decode to exactly **8 bytes** (SQL Server `rowversion`) ⇒ **400** (`ifMatch`, R3); stale ⇒ **409** — the handler compares the loaded `Version` with `If-Match` before any rule or change, and a write committed between that load and the save is caught by EF's `WHERE [Version] = @original` (`DbUpdateConcurrencyException` ⇒ existing 409). 409 (not 412) keeps the existing concurrency convention in `docs/api-conventions.md`. No-op mutations still validate `If-Match` and return the unchanged version. Angular sends the version it last displayed; on 409 it reloads, keeps typed input and lets the user re-apply — no automatic retry. Templates, memberships, messages, notifications: no rowversion (single owner / append-only / conditional set-based writes). |
| Duplicate active membership (F4) | Filtered unique index **`UX_TicketCollaborators_ActiveMembership`** `(TicketId, AgentUserId) WHERE RemovedAtUtc IS NULL`, plus the key-range lock taken in the invite transaction (F3). `EfUnitOfWork` maps SQL Server 2601/2627 to 409 **only** when the violated index name is in an explicit allow-list (`KnownUniqueIndexes` — just this index, with its business message). Every other unique violation (incl. `UX_TaskReminderNotifications_TaskGeneration`, which no EF save writes, and all Identity / existing indexes) is rethrown ⇒ logged 500, because it indicates a defect, not a business conflict. Existing `DbUpdateConcurrencyException` ⇒ 409 is unchanged. |
| Collaboration access (D3) | Computed in SQL per request from **current** state (`CollaborationRepository.Accessible`): SuperAdmin ⇒ any ticket; Agent ⇒ `ticket.AssignedAgentUserId == caller` (Primary) **or** (`ticket.AssignedAgentUserId != null` **and** active membership for caller) (Collaborator). Unassigned ⇒ collaborator access suspended. Manage = SuperAdmin or Primary. |
| Write-time authorization (F3) | Reads use point-in-time SQL predicates. **Sensitive writes re-establish authorization at commit time** with lock ⇒ check ⇒ write inside one transaction (`ITransactionalCommand` + existing `TransactionBehavior` / `ExecuteInTransactionAsync`; no new infrastructure): `ITicketAccessLock` reads the ticket row `WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` and, for a collaborator, the active-membership key `WITH (UPDLOCK, HOLDLOCK, ROWLOCK)`; the handler decides from those locked values and writes before commit. A concurrent ticket (re/un)assignment (`UPDATE Tickets` from the unchanged TICK-002 handler) or membership removal therefore either committed first — and the write is rejected (404 / 400 / 409) — or waits until this transaction commits. Applies to: post message, invite, remove collaborator, create task with a ticket link, set ticket link, reassign task keeping its link. Lock order is always `Tickets` → `TicketCollaborators` → `AgentTasks` / new rows; a deadlock with other code (1205) is transient and the whole attempt is rolled back and re-run from scratch under the R1 rules. Locks are held for one short request transaction only. |
| Task ticket picker (F5) | New `GET /api/v1/tasks/linkable-tickets?ownerUserId=&search=` returns ≤ 10 `{ id, subject, status, priority }` of tickets **currently assigned to the owner** (Agent: always the caller — a different `ownerUserId` ⇒ 400; SuperAdmin: `ownerUserId` required and must be an active Agent). Angular `TicketPicker` takes an `ownerUserId` input, is disabled until an owner is selected and clears its selection when the owner changes. Display aid only — the locked server check (F3) stays authoritative. `/api/v1/tickets` is not changed. |
| Transactional retries (R1) | **Verified in code:** `TransactionBehavior.HandleInTransactionAsync` (lines 29–38) passes `ct => next(message, ct)` to `EfUnitOfWork.ExecuteInTransactionAsync`, whose execution-strategy lambda (lines 34–54) calls `ChangeTracker.Clear()` (line 38), begins a transaction, re-invokes the **whole handler**, saves (line 44, unmapped `dbContext.SaveChangesAsync`) and commits (line 45) — the commit sits inside the same `try` as the handler, so a transient commit failure is rolled back and the **entire handler is replayed even if the commit was applied**, and the commit uses the request's cancellation token. Retry safety is therefore made explicit with three rules. **(1) Every attempt starts from nothing:** `ChangeTracker.Clear()` stays the first statement of the lambda and is repeated after a rollback, so entities that a failed attempt's `SaveChangesAsync` had already *accepted* — e.g. `ReassignAgentTask`'s task, whose tracked `Version` was refreshed by the later rolled-back `UPDATE` — are detached; identity resolution can never hand them back. The next attempt re-loads every row, re-checks `If-Match` against the rolled-back (original) version, re-acquires locks and recomputes everything. Set-based statements (`ExecuteUpdateAsync`, `SqlQuery` locks) ran inside the attempt's transaction and rolled back with it. **(2) Transactional handlers are replay-safe:** only primary-constructor dependencies (no mutable fields, no caching across attempts), no external side effects, no catching of database exceptions; the command record (incl. `ExpectedVersion`) is immutable, so a replay validates exactly like the first attempt. **(3) The commit is never replayed:** `ExecuteInTransactionAsync` is restructured (task 5) so handler + save run in a `try` that rolls back and rethrows (transient ⇒ the strategy retries from rule 1), while `CommitAsync(CancellationToken.None)` runs in its own `try` and *any* exception from it becomes non-transient `CommitOutcomeUnknownException` (the strategy only retries transient `SqlException`s, unwrapping `DbUpdateException` only), mapped to **503** "The change may have been saved. Reload before trying again." Clients reload instead of resubmitting. EF's `verifySucceeded` overload is not used: the generic `ExecuteInTransactionAsync` has no per-command verification, and commit-marker / idempotency-key tables would be new infrastructure. This also makes the existing Story 02–05 transactional commands safer (they previously could be replayed after a commit). |
| Raw SQL / set-based retries (R2) | `ExecuteSqlInterpolatedAsync` and `ExecuteUpdateAsync` are **not** assumed to be retried by `EnableRetryOnFailure` (correcting the earlier claim); only LINQ queries and `SaveChanges` are relied on to run through the execution strategy. The two **autocommit** statements — `IssueDueAsync` and `MarkReadAsync` — are wrapped explicitly in `dbContext.Database.CreateExecutionStrategy().ExecuteAsync(...)`. That is safe even under commit uncertainty because each is one conditional, idempotent statement: a replay of `IssueDueAsync` inserts nothing already inserted (`NOT EXISTS` + `UX_TaskReminderNotifications_TaskGeneration`; `issuedNow` may then report 0 while the unread count stays correct), a replay of mark-read affects 0 rows and reports `AlreadyRead`. If every retry fails, the check returns 500, the bell ignores it and the **next 60-second check** is the final retry. `TransferUnreadAsync` and the `ITicketAccessLock` queries create no strategy of their own: they run inside the reassignment / F3 transaction and are retried only as part of a whole attempt (R1). |
| Messages (D5) | Append-only, 1–2,000 chars trimmed, newest-first pages of 20 with "Load older" (same paging UX as `TicketActivityList`). Never touches `Tickets` (no `UpdatedAtUtc`, no `TicketActivity`). |
| Templates (D2) | Scope from role at creation (Agent ⇒ Personal + owner; SuperAdmin ⇒ Shared); request has **no** scope/owner field; immutable after creation. SuperAdmin never sees Personal templates. Category reuses `TicketCategory`. |
| Agent picker for collaborators | Agents cannot call `/api/v1/agents` (SuperAdmin only, `AgentsController.cs` line 17). New `GET /api/v1/collaborations/{ticketId}/eligible-agents` (manage permission) returns id + display name of active Agents excluding the primary and existing collaborators. |
| Polling | Only `NotificationBell` (Agent session): `timer(0, 60_000)` + `exhaustMap`. Dashboard, tasks, templates, discussions refresh on visit / user action only. |
| Time | Server `TimeProvider` is the authority for all comparisons; API stores/returns `DateTimeOffset` UTC; Angular converts `datetime-local` input ⇔ ISO at the UI edge and displays with `DatePipe` in the viewer's local zone. |
| Dashboard | New `GET /api/v1/agent-dashboard/tasks` (separate query + response). `GET /api/v1/agent-dashboard` and `AgentDashboardRepository` are **not** modified. |

---

## Context — Read These Files First

1. `.squad/stories/agent-dashboard/DASH-002/intake.md` — §2 role table, §3.1–3.3 task/reminder lifecycle, §4 templates, §5 collaboration, §6–§9, Acceptance criteria, §12 decisions are the contract.
2. [06-story-agent-dashboard-ticket-overview-work-queue.md](06-story-agent-dashboard-ticket-overview-work-queue.md) — tone, Verification style (dedicated test Agents, zero baselines, SQL log checks). [../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md) — migration task (~lines 799–818) and Migration / Rollback (~lines 1338–1345) wording to mirror.
3. `backend/src/Crm.Domain/Tickets/Ticket.cs` — private EF ctor (line 13), private setters (15–29), static `Create` (37–64), "false = no-op, no timestamp" methods (`UpdateDetails` 67–106, `ChangeAssignee` 109–120), `DomainException` usage (125–128). New entities follow this shape.
4. `backend/src/Crm.Domain/Tickets/TicketActivity.cs` lines 3–22 — append-only entity with `long` identity id; `TicketCategory.cs` lines 3–10 (reused by templates).
5. `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketConfiguration.cs` — `HasConversion<string>().HasMaxLength(20)` (20–22), `Restrict` FKs to `ApplicationUser`/`Customer` (25–27), `IsRowVersion()` (34), descending composite indexes (40–42). `TicketActivityConfiguration.cs` line 14 (`ValueGeneratedOnAdd` identity) and 27.
6. `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` lines 16–19 (DbSets) and 26 (`ApplyConfigurationsFromAssembly` — new configurations need no registration).
7. `backend/src/Crm.Infrastructure/Persistence/EfUnitOfWork.cs` lines 9–20 (`DbUpdateConcurrencyException` ⇒ `ConflictException`; add the allow-listed unique-index arm here, F4) and 22–55 (`ExecuteInTransactionAsync`: execution strategy + explicit transaction — the vehicle for every F2/F3 lock-then-write command).
8. `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs` — `Scope` (67–69, **do not change**), `Project` (74–101: joins to `dbContext.Users` for names, `DefaultIfEmpty` left joins, `orderby` before `select`), `ListAsync` filters + `CountAsync` + `Skip/Take` (19–65), `ListActivityAsync` scope-in-every-statement pattern (103–141).
9. `backend/src/Crm.Infrastructure/Persistence/Repositories/AgentDashboardRepository.cs` — `GroupBy(_ => 1)` filtered-count aggregate pattern to copy for task counts.
10. `backend/src/Crm.Application/Tickets/CreateTicket/CreateTicketCommand.cs` lines 11–93 — command + handler in one file, owner resolution, `Invalid(property, message)` ⇒ 400 helper (90–92), read-back under the caller's scope (55–57). `CreateTicketValidator.cs` lines 6–17. `ChangeAssignee/ChangeTicketAssigneeCommand.cs` lines 20–48 (active-Agent check, no-op first). `TransitionTicket/TransitionTicketStatusCommand.cs` lines 21–42 (`ConflictException` for invalid state). `ListTickets/ListTicketsQuery.cs` lines 19–31 (search trim, `Math.Min(pageSize, Paging.MaxPageSize)`), `ListTicketsValidator.cs` lines 7–18.
11. `backend/src/Crm.Application/Common/Paging/PagedResult.cs` lines 4–11; `Common/Validation/TicketRules.cs` lines 3–9 (rules-constants style); `Common/Exceptions/*.cs` (one-line exception style); `Abstractions/Persistence/ITicketRepository.cs` lines 7–31 (doc comments); `Abstractions/Identity/IIdentityService.cs` lines 8–28 and `UserAccount.cs` lines 4–13.
12. `backend/src/Crm.Infrastructure/Identity/IdentityService.cs` lines 82–88 (`ListByRoleAsync` ordered by last/first name) and `ApplicationUser.cs` lines 6–12 (`FirstName`, `LastName`, `IsActive`).
13. `backend/src/Crm.Api/Controllers/TicketsController.cs` — class attributes (23–26), `EndpointDescription` wording, `[FromQuery]` list binding (28–45), `CreatedAtAction` (52–62), method-level SuperAdmin policy (74–78), `Actor` (106–107). `AgentDashboardController.cs` lines 10–19. `Contracts/Tickets/TicketResponse.cs` lines 7–33 (`From(...)`), `Contracts/Common/PagedResponse.cs` line 4.
14. `backend/src/Crm.Api/Errors/GlobalExceptionHandler.cs` lines 20–60 (exception ⇒ status switch; add `ForbiddenException`). `Authentication/AuthorizationPolicies.cs` lines 3–9; `SecurityRegistration.cs` lines 41–57 (active-account check) and 61–66 (policies — **no new policy needed**).
15. `backend/src/Crm.Api/DependencyInjection.cs` lines 15–18 (string enums), 48–58 (Mediator scans `Crm.Application`; no handler registration). `backend/src/Crm.Infrastructure/DependencyInjection.cs` lines 33 (`EnableRetryOnFailure`) and 37–41 (repository registrations).
16. `backend/tests/Crm.UnitTests/Tickets/TransitionTicketStatusCommandHandlerTests.cs` — whole file (91 lines): naming, hand-written fakes (56–85), `FixedTimeProvider` (87–90). `Crm.UnitTests.csproj` references only `Crm.Application`.
17. `frontend/src/app/app.routes.ts` lines 22–46; `core/layout/navigation.ts` lines 12–20; `core/layout/shell/shell.ts` lines 30–45 and `shell.html` lines 35–42 (toolbar: user name + sign-out); `core/auth/auth-guards.ts` lines 14–19; `core/auth/auth-service.ts` lines 19–21 (`currentUser`, `role` signals).
18. `frontend/src/app/features/agent-dashboard/agent-dashboard-page.ts` lines 47–75 (Subject + `switchMap` + per-request `catchError`, `refresh()`), `agent-dashboard-page.html` lines 1–94 (toolbar, error/Retry, sections).
19. `frontend/src/app/features/tickets/ticket-detail-page.ts` lines 54–117 (signals, SuperAdmin-only agent list, `takeUntilDestroyed`), `ticket-detail-page.html` lines 184–191 (Activity card — new cards go after it, before the `</div>` on line 192); `ticket-activity-list.ts` lines 47–124 (`input.required`, `refresh` input, paged "Load more" with dedupe); `customer-picker.ts` (101 lines — autocomplete pattern for the new `TicketPicker`); `ticket-models.ts` lines 4–11 (`TICKET_CATEGORIES`, `TicketCategory`) and 118–131; `ticket-workflow.ts` lines 27–41 (pure display-rules function).
20. `frontend/src/app/features/tickets/tickets-api.ts` lines 17–49 (`HttpParams`, omitted empty filters, `ApiUrl.versioned`), `tickets-api.spec.ts` (52 lines — the spec pattern to copy), `features/customers/customer-models.ts` lines 14–20 (`PagedResponse<T>`), `features/agents/agents-api.ts` line 12 and `agent-models.ts` (SuperAdmin agent list for task owner select).
21. `frontend/src/app/shared/ui/status-badge/status-badge.ts` (`StatusTone`), `shared/ui/page-header/page-header.ts`, `core/api/describe-http-error.ts` lines 5–14, `src/styles.scss` lines 16–28 (`--crm-*` tokens).
22. Docs: `docs/agent-dashboard.md` (§8 lines 65–69), `docs/identity-and-access.md` (matrix lines 7–24, routes lines 28–51), `docs/api-conventions.md` (line 3, error table ~lines 7–16, Auth bullet), `docs/persistence-and-transactions.md` lines 5 and 9, `docs/frontend-ui-foundation.md` (Lists/Pickers/Navigation bullets), `docs/ticket-management.md` §12 lines 144–150, `README.md` line 3 and lines 105–111.

---

## Product rules (from story)

### Role matrix (server-enforced)

| Operation | SuperAdmin | Agent | Customer |
|---|---|---|---|
| DASH-001 My Dashboard (+ new tasks section) | 403 (unchanged) | own only | 403 |
| Create task | for a selected **active Agent** (required) | for self only | 403 |
| Read / edit / complete / reopen / archive / restore task | all tasks | **own** tasks (others 404) | 403 |
| Change task owner | to an active Agent | never (policy 403) | 403 |
| Schedule / change / cancel reminder | any task (targets the owner) | own tasks | 403 |
| Reminder inbox (check / list / mark read) | 403 (no inbox) | items for tasks they **currently** own | 403 |
| Shared templates | create / edit / archive / restore / read / copy | read / copy active only (edit ⇒ 403) | 403 |
| Personal templates | never visible (404) | own: create / edit / archive / restore / copy; others' ⇒ 404 | 403 |
| Read / post internal discussion | any ticket | current primary assignee, or active collaborator on an **assigned** ticket | 403 |
| Invite / remove collaborators | any ticket | current primary assignee of that ticket only; collaborator-only ⇒ 403; others ⇒ 404 | 403 |
| Ticket details / list / activity / edit / workflow / escalation / assignment | unchanged (TICK-001/002) | unchanged — collaborators get **404** as before | 403 |

### Task urgency (server UTC `now`, Pending and unarchived only)

| State | Definition |
|---|---|
| **Overdue** | `DueAtUtc < now` |
| **Due soon** | `now <= DueAtUtc <= now + 7 days` (inclusive) |
| **No due date** | `DueAtUtc is null` (neither overdue nor due soon) |
| Completed / archived | never overdue / due soon |
| Default list order | `DueAtUtc is null` last → `DueAtUtc` ascending (overdue therefore first) → `Id` ascending |
| Default UI view | Pending, unarchived |

### Reminder rules

| Rule | Enforcement |
|---|---|
| Optional, off by default, max one per task | `RemindAtUtc` nullable column; never set implicitly by a due date |
| Must be in the future when (re)scheduled | handler ⇒ 400 `remindAtUtc` "Choose a future reminder time." |
| At or before due time when a due time exists | handler ⇒ 400 `remindAtUtc` "The reminder must be at or before the due time." Editing the due time to before a pending reminder ⇒ 400 `dueAtUtc` "The due time cannot be before the scheduled reminder. Change or cancel the reminder first." |
| Only Pending, unarchived tasks can (re)schedule | handler ⇒ 409 "Reopen or restore the task before scheduling a reminder." Cancel is always allowed (no-op if none). |
| Different time ⇒ new generation; same time ⇒ no-op | `AgentTask.ScheduleReminder` |
| Complete / Archive ⇒ cancel; Reopen / Restore ⇒ nothing | `AgentTask.Complete`, `Archive` clear `RemindAtUtc` |
| Issued at most once per (task, generation), to the owner committed at issuance | `INSERT … SELECT` with `UPDLOCK` on `AgentTasks`, `NOT EXISTS (UPDLOCK, HOLDLOCK)`, unique index |
| Task reassigned | issued **unread** notifications move to the new owner (same row, still unread, transfer recorded); **read** ones stay as the reader's history; nothing is re-issued or duplicated |
| Issuance racing completion / archive / reassignment | whichever commits first wins: issuance waits on the task row and re-reads it. A reminder issued just before completion/archive stays in the inbox showing the task as Completed/Archived (never as pending work); one issued just before reassignment is transferred by the reassignment |
| Notification ≠ completion | Mark-as-read only sets `ReadAtUtc`; task status untouched |

---

## Backend Tasks

### 1 — Shared Application pieces

**Create file: `backend/src/Crm.Application/Common/StaffActor.cs`**

```csharp
namespace Crm.Application.Common;

/// <summary>
/// The authenticated staff member (SuperAdmin or Agent), built by the API from the validated JWT only (sub + role).
/// Used by DASH-002 features (tasks, quick replies, collaboration). TicketActor stays the ticket module's own actor.
/// </summary>
public sealed record StaffActor(Guid UserId, bool IsSuperAdmin)
{
    /// <summary>null = unrestricted (SuperAdmin); otherwise the Agent's own user id.</summary>
    public Guid? AgentScope => IsSuperAdmin ? null : UserId;
}
```

**Create file: `backend/src/Crm.Application/Common/Exceptions/ForbiddenException.cs`**

```csharp
namespace Crm.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 403 by the API. Only for records the caller can already see but may not change.</summary>
public sealed class ForbiddenException(string message) : Exception(message);
```

**Create file: `backend/src/Crm.Application/Common/Exceptions/PreconditionRequiredException.cs`**

```csharp
namespace Crm.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 428 by the API. A mutation of a versioned resource was sent without If-Match.</summary>
public sealed class PreconditionRequiredException(string message) : Exception(message);
```

**Create file: `backend/src/Crm.Application/Common/Exceptions/CommitOutcomeUnknownException.cs`** (R1)

```csharp
namespace Crm.Application.Common.Exceptions;

/// <summary>
/// Mapped to HTTP 503 by the API. COMMIT failed or was interrupted, so the transaction may or may not have been applied.
/// Deliberately NOT transient and NOT a DbUpdateException, so the execution strategy never replays the handler after a commit attempt.
/// </summary>
public sealed class CommitOutcomeUnknownException(Exception innerException)
    : Exception("The change may have been saved. Reload before trying again.", innerException);
```

**Create file: `backend/src/Crm.Application/Common/Validation/ProductivityRules.cs`**

```csharp
namespace Crm.Application.Common.Validation;

public static class AgentTaskRules
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int SearchMaxLength = 100;
    public const int DashboardPreviewLimit = 5;
    public static readonly TimeSpan DueSoonWindow = TimeSpan.FromDays(7);
}

public static class TaskReminderRules
{
    /// <summary>Issued this long after the scheduled time ⇒ labelled "missed" = delayed issuance (no check reached the server in time).
    /// It does NOT prove the Agent was offline (F6).</summary>
    public static readonly TimeSpan MissedThreshold = TimeSpan.FromMinutes(5);
}

public static class QuickReplyRules
{
    public const int TitleMaxLength = 120;
    public const int BodyMaxLength = 2000;
    public const int SearchMaxLength = 100;
}

public static class CollaborationRules
{
    public const int MessageMaxLength = 2000;
}
```

### 2 — Domain

**Create file: `backend/src/Crm.Domain/AgentTasks/AgentTaskStatus.cs`**

```csharp
namespace Crm.Domain.AgentTasks;

/// <summary>Stored as the member name. Archive is a separate flag, not a status.</summary>
public enum AgentTaskStatus
{
    Pending = 0,
    Completed = 1,
}
```

**Create file: `backend/src/Crm.Domain/AgentTasks/AgentTask.cs`** — follow `Ticket.cs` (private ctor, private setters, `bool` no-op returns, `UpdatedAtUtc` only on real change):

```csharp
using Crm.Domain.Common;

namespace Crm.Domain.AgentTasks;

/// <summary>
/// Agent work item (DASH-002). AssignedAgentUserId / CreatedByUserId are Identity user ids set by the server only.
/// TicketId optionally references Tickets.Id (no ticket data is copied). Never hard-deleted: archive instead.
/// Reminder: at most one; RemindAtUtc null = none. ReminderGeneration increases on every genuine (re)schedule so that
/// each generation is issued at most once (unique index on TaskReminderNotifications).
/// Version: SQL rowversion, exposed to clients as the optimistic-concurrency token (ETag / If-Match).
/// </summary>
public sealed class AgentTask
{
    private AgentTask() { } // EF Core

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public AgentTaskStatus Status { get; private set; }
    public Guid AssignedAgentUserId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? TicketId { get; private set; }
    public DateTimeOffset? DueAtUtc { get; private set; }
    public DateTimeOffset? RemindAtUtc { get; private set; }
    public int ReminderGeneration { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public bool IsArchived { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }
    public byte[] Version { get; private set; } = []; // store-generated rowversion

    public bool IsOpenForReminders => Status == AgentTaskStatus.Pending && !IsArchived;

    /// <summary>New tasks are Pending. The caller has validated owner, ticket link and reminder.</summary>
    public static AgentTask Create(
        Guid id, string title, string? description, Guid assignedAgentUserId, Guid createdByUserId,
        Guid? ticketId, DateTimeOffset? dueAtUtc, DateTimeOffset? remindAtUtc, DateTimeOffset now) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Status = AgentTaskStatus.Pending,
            AssignedAgentUserId = assignedAgentUserId,
            CreatedByUserId = createdByUserId,
            TicketId = ticketId,
            DueAtUtc = dueAtUtc,
            RemindAtUtc = remindAtUtc,
            ReminderGeneration = remindAtUtc is null ? 0 : 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
```

Add these methods (each returns `false` and changes nothing when the state already matches; each sets `UpdatedAtUtc = now` only when it returns `true`):

| Method | Rule |
|---|---|
| `bool UpdateDetails(string title, string? description, DateTimeOffset? dueAtUtc, DateTimeOffset now)` | Throw `DomainException("Restore the task before editing it.")` when `IsArchived`. Ordinal compare of title/description, equality of due. |
| `bool LinkTicket(Guid? ticketId, DateTimeOffset now)` | Throw when archived (same message). `null` = unlink. |
| `bool ScheduleReminder(DateTimeOffset? remindAtUtc, DateTimeOffset now)` | `null` = cancel (`RemindAtUtc = null`, generation unchanged). Non-null and different ⇒ `RemindAtUtc = value; ReminderGeneration++`. Non-null requires `IsOpenForReminders` else `DomainException` (handler checks first and throws 409). |
| `bool Complete(DateTimeOffset now)` | Archived ⇒ `DomainException`. Pending ⇒ `Status = Completed`, `CompletedAtUtc = now`, `RemindAtUtc = null`. Completed ⇒ `false`. |
| `bool Reopen(DateTimeOffset now)` | Archived ⇒ `DomainException`. Completed ⇒ `Pending`, `CompletedAtUtc = null` (reminder **not** recreated). |
| `bool Archive(DateTimeOffset now)` | Not archived ⇒ `IsArchived = true`, `ArchivedAtUtc = now`, `RemindAtUtc = null`. |
| `bool Restore(DateTimeOffset now)` | Archived ⇒ `IsArchived = false`, `ArchivedAtUtc = null` (reminder **not** recreated). |
| `bool Reassign(Guid assignedAgentUserId, bool removeTicketLink, DateTimeOffset now)` | Same owner and `!removeTicketLink` ⇒ `false`. Archived ⇒ `DomainException`. Sets owner; `removeTicketLink` ⇒ `TicketId = null`. Reminder fields untouched (recipient is resolved at issuance). |

> Handlers check `If-Match` first, then the archived/state rules, **before** calling the domain and throw `ConflictException` (409) with the same message; the `DomainException` (422) guards are a safety net only.

**Create file: `backend/src/Crm.Domain/AgentTasks/TaskReminderNotification.cs`**

```csharp
namespace Crm.Domain.AgentTasks;

/// <summary>
/// One issued in-app reminder (DASH-002 D4). Inserted only by the repository's idempotent INSERT…SELECT; unique (TaskId, ReminderGeneration).
/// RecipientUserId = the Agent who currently holds it: set at issuance; when the task is reassigned, UNREAD rows move to the new owner
/// (TransferredFromUserId / TransferredAtUtc record the last move); READ rows never move — they are the reader's history.
/// Changed only by conditional set-based statements in TaskReminderRepository (no load-then-save), so it has no methods.
/// </summary>
public sealed class TaskReminderNotification
{
    private TaskReminderNotification() { } // EF Core

    public long Id { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public int ReminderGeneration { get; private set; }
    public DateTimeOffset ScheduledForUtc { get; private set; }
    public DateTimeOffset IssuedAtUtc { get; private set; }
    public DateTimeOffset? ReadAtUtc { get; private set; }
    public Guid? TransferredFromUserId { get; private set; }
    public DateTimeOffset? TransferredAtUtc { get; private set; }
}
```

**Create file: `backend/src/Crm.Domain/QuickReplies/QuickReplyScope.cs`** — `public enum QuickReplyScope { Personal = 0, Shared = 1 }` (stored by name).

**Create file: `backend/src/Crm.Domain/QuickReplies/QuickReplyTemplate.cs`**

- Properties (private setters): `Guid Id`, `string Title`, `string Body`, `TicketCategory Category`, `QuickReplyScope Scope`, `Guid? OwnerUserId` (set iff Personal), `Guid CreatedByUserId`, `Guid UpdatedByUserId`, `DateTimeOffset CreatedAtUtc`, `DateTimeOffset UpdatedAtUtc`, `bool IsArchived`, `DateTimeOffset? ArchivedAtUtc`.
- `static QuickReplyTemplate CreatePersonal(Guid id, Guid ownerUserId, string title, string body, TicketCategory category, DateTimeOffset now)` (owner = creator = updater).
- `static QuickReplyTemplate CreateShared(Guid id, Guid superAdminUserId, string title, string body, TicketCategory category, DateTimeOffset now)` (`OwnerUserId = null`).
- `bool Update(string title, string body, TicketCategory category, Guid actorUserId, DateTimeOffset now)` — archived ⇒ `DomainException("Restore the quick reply before editing it.")`; ordinal compare; sets `UpdatedByUserId`/`UpdatedAtUtc` only on change.
- `bool Archive(Guid actorUserId, DateTimeOffset now)` / `bool Restore(Guid actorUserId, DateTimeOffset now)`.
- **No** method changes `Scope` or `OwnerUserId`.

**Create file: `backend/src/Crm.Domain/Collaboration/TicketCollaborator.cs`**

```csharp
namespace Crm.Domain.Collaboration;

/// <summary>
/// Explicit invitation of an Agent to one ticket's internal discussion (DASH-002 D3). Grants ONLY the collaboration surface,
/// never ticket details/activity/actions. Removal is recorded (RemovedAtUtc/By), never deleted; re-inviting adds a new row.
/// </summary>
public sealed class TicketCollaborator
{
    private TicketCollaborator() { } // EF Core

    public Guid Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AgentUserId { get; private set; }
    public Guid AddedByUserId { get; private set; }
    public DateTimeOffset AddedAtUtc { get; private set; }
    public Guid? RemovedByUserId { get; private set; }
    public DateTimeOffset? RemovedAtUtc { get; private set; }

    public static TicketCollaborator Invite(Guid id, Guid ticketId, Guid agentUserId, Guid addedByUserId, DateTimeOffset now) =>
        new() { Id = id, TicketId = ticketId, AgentUserId = agentUserId, AddedByUserId = addedByUserId, AddedAtUtc = now };

    public void Remove(Guid removedByUserId, DateTimeOffset now)
    {
        RemovedByUserId = removedByUserId;
        RemovedAtUtc = now;
    }
}
```

**Create file: `backend/src/Crm.Domain/Collaboration/TicketInternalMessage.cs`**

```csharp
namespace Crm.Domain.Collaboration;

/// <summary>
/// Append-only internal staff message on a ticket (DASH-002 D5). Never edited or deleted (no methods exist for it).
/// Not a TicketActivity and never changes the ticket. Id is a store-generated identity (insertion order breaks ties).
/// </summary>
public sealed class TicketInternalMessage
{
    private TicketInternalMessage() { } // EF Core

    public long Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public static TicketInternalMessage Post(Guid ticketId, Guid authorUserId, string body, DateTimeOffset now) =>
        new() { TicketId = ticketId, AuthorUserId = authorUserId, Body = body, CreatedAtUtc = now };
}
```

### 3 — Application: read models and repository contracts

**Create file: `backend/src/Crm.Application/AgentTasks/AgentTaskDetails.cs`**

```csharp
using Crm.Domain.AgentTasks;
using Crm.Domain.Tickets;

namespace Crm.Application.AgentTasks;

public enum LinkedTicketState { None, Accessible, Inaccessible }

/// <summary>Due-state filter for task lists (Pending, unarchived semantics as in docs).</summary>
public enum TaskDueFilter { Overdue, DueSoon, NoDueDate }

/// <summary>
/// Read model. Names joined at query time. LinkedTicketId/Subject are null for an Agent when the link is Inaccessible
/// (the owner is no longer the ticket's assignee); SuperAdmin always receives them. No customer data is ever included.
/// ReminderSentAtUtc = issuance time of the CURRENT generation, null while pending or when no reminder exists.
/// IsOverdue / IsDueSoon are computed in SQL against server UTC.
/// </summary>
public sealed record AgentTaskDetails(
    Guid Id,
    string Title,
    string? Description,
    AgentTaskStatus Status,
    Guid AssignedAgentUserId,
    string AssignedAgentName,
    Guid CreatedByUserId,
    string CreatedByName,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? RemindAtUtc,
    DateTimeOffset? ReminderSentAtUtc,
    LinkedTicketState LinkedTicketState,
    Guid? LinkedTicketId,
    string? LinkedTicketSubject,
    bool IsOverdue,
    bool IsDueSoon,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool IsArchived,
    DateTimeOffset? ArchivedAtUtc,
    byte[] Version);

/// <summary>Search already trimmed. Archived: false (default) = unarchived only, true = archived only.
/// AssignedAgentUserId only narrows (an Agent's scope is applied first).</summary>
public sealed record AgentTaskListFilter(
    string? Search, AgentTaskStatus? Status, TaskDueFilter? DueState, bool Archived, bool? HasReminder, Guid? AssignedAgentUserId);

/// <summary>Pending, unarchived tasks owned by one Agent. Overdue and DueSoon are subsets of Pending (disjoint from each other).</summary>
public sealed record AgentTaskCounts(int Pending, int Overdue, int DueSoon);

public sealed record AgentTaskSummary(
    AgentTaskCounts Counts, IReadOnlyList<AgentTaskDetails> NextTasks, int PreviewLimit, DateTimeOffset GeneratedAtUtc);

public sealed record TaskReminderDetails(
    long Id, Guid TaskId, string TaskTitle, AgentTaskStatus TaskStatus, bool TaskIsArchived,
    DateTimeOffset ScheduledForUtc, DateTimeOffset IssuedAtUtc, DateTimeOffset? ReadAtUtc, DateTimeOffset? TransferredAtUtc);

public enum MarkReadOutcome { NotFound, MarkedRead, AlreadyRead }

/// <summary>Picker row (F5): a ticket currently assigned to the prospective task owner. No customer data.</summary>
public sealed record LinkableTicket(Guid Id, string Subject, TicketStatus Status, TicketPriority Priority);

public sealed record TaskReminderCheck(int UnreadCount, int IssuedNow, DateTimeOffset CheckedAtUtc);
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IAgentTaskRepository.cs`**

```csharp
using Crm.Application.AgentTasks;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Domain.AgentTasks;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Add only stages; the caller saves through IUnitOfWork. Every read applies actor.AgentScope
/// (AssignedAgentUserId == the Agent) in SQL first; SuperAdmin is unrestricted. now = server UTC for due-state columns.
/// </summary>
public interface IAgentTaskRepository
{
    void Add(AgentTask task);

    /// <summary>Tracked, for updates. Null when missing or outside agentScope.</summary>
    Task<AgentTask?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default);

    Task<AgentTaskDetails?> FindDetailsByIdAsync(Guid id, StaffActor actor, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Ordered DueAtUtc null last, DueAtUtc ASC, Id ASC. Filtered, counted and paged in SQL.</summary>
    Task<PagedResult<AgentTaskDetails>> ListAsync(
        AgentTaskListFilter filter, StaffActor actor, DateTimeOffset now, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>One aggregate statement over the Agent's Pending, unarchived tasks; zeros when none.</summary>
    Task<AgentTaskCounts> GetPendingCountsAsync(Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>Picker data (F5): tickets whose CURRENT AssignedAgentUserId is ownerUserId, optional subject search,
    /// newest first, at most limit. Display aid only — linking re-checks under a lock.</summary>
    Task<IReadOnlyList<LinkableTicket>> ListLinkableTicketsAsync(
        Guid ownerUserId, string? search, int limit, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-app reminders. Every read/write is restricted to notifications whose RecipientUserId is agentUserId AND whose task is
/// CURRENTLY owned by agentUserId (except TransferUnreadAsync, called by the reassignment). All writes are conditional
/// set-based statements (no load-then-save).
/// </summary>
public interface ITaskReminderRepository
{
    /// <summary>
    /// Idempotent, concurrency-safe single statement: inserts one notification per due, not-yet-issued reminder generation of the
    /// Agent's Pending, unarchived tasks, reading the task rows under UPDLOCK. Returns the number inserted (0 on repeat calls).
    /// </summary>
    Task<int> IssueDueAsync(Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<int> CountUnreadAsync(Guid agentUserId, CancellationToken cancellationToken = default);

    /// <summary>Newest first (IssuedAtUtc DESC, Id DESC).</summary>
    Task<PagedResult<TaskReminderDetails>> ListAsync(
        Guid agentUserId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Sets ReadAtUtc only while the row is unread, addressed to agentUserId and its task is owned by agentUserId at execution time.</summary>
    Task<MarkReadOutcome> MarkReadAsync(long id, Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>
    /// F2: moves every UNREAD notification of taskId to toUserId (same rows; TransferredFromUserId/TransferredAtUtc set; still unread).
    /// Read rows are untouched. Must run inside the reassignment transaction, AFTER the task row was updated. Returns rows moved.
    /// </summary>
    Task<int> TransferUnreadAsync(Guid taskId, Guid toUserId, DateTimeOffset now, CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/QuickReplies/QuickReplyDetails.cs`**

```csharp
using Crm.Domain.QuickReplies;
using Crm.Domain.Tickets;

namespace Crm.Application.QuickReplies;

/// <summary>CanEdit is computed for the requesting actor (Agent: own Personal; SuperAdmin: Shared). Display only — commands re-check.</summary>
public sealed record QuickReplyDetails(
    Guid Id, string Title, string Body, TicketCategory Category, QuickReplyScope Scope, Guid? OwnerUserId,
    string CreatedByName, string UpdatedByName, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc,
    bool IsArchived, bool CanEdit);

/// <summary>Search already trimmed (title or body). Archived: false = active only (default).</summary>
public sealed record QuickReplyListFilter(string? Search, TicketCategory? Category, QuickReplyScope? Scope, bool Archived);
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IQuickReplyRepository.cs`**

- Visibility predicate (SQL, applied first): SuperAdmin ⇒ `Scope == Shared`; Agent ⇒ `(Scope == Shared && !IsArchived) || (Scope == Personal && OwnerUserId == actor.UserId)`.
- Members: `void Add(QuickReplyTemplate template)`; `Task<QuickReplyTemplate?> GetVisibleAsync(Guid id, StaffActor actor, CancellationToken)` (tracked); `Task<QuickReplyDetails?> FindDetailsByIdAsync(Guid id, StaffActor actor, CancellationToken)`; `Task<PagedResult<QuickReplyDetails>> ListAsync(QuickReplyListFilter filter, StaffActor actor, int page, int pageSize, CancellationToken)` ordered `Title` ASC, `Id` ASC.

**Create file: `backend/src/Crm.Application/Collaboration/CollaborationDetails.cs`**

```csharp
using Crm.Domain.Tickets;

namespace Crm.Application.Collaboration;

public enum CollaborationAccess { SuperAdmin, PrimaryAgent, Collaborator }

/// <summary>Minimal ticket context for the collaboration surface. Deliberately no description, customer or activity data.</summary>
public sealed record CollaborationTicketContext(
    Guid TicketId, string Subject, TicketStatus Status, TicketPriority Priority,
    Guid? AssignedAgentUserId, string? AssignedAgentName, CollaborationAccess Access)
{
    public bool CanManage => Access is CollaborationAccess.SuperAdmin or CollaborationAccess.PrimaryAgent;
}

public sealed record TicketMessageDetails(long Id, Guid TicketId, Guid AuthorUserId, string AuthorName, string Body, DateTimeOffset CreatedAtUtc);

public sealed record TicketCollaboratorDetails(
    Guid AgentUserId, string AgentName, bool IsActive, string AddedByName, DateTimeOffset AddedAtUtc);

public sealed record CollaboratorCandidate(Guid AgentUserId, string AgentName);

/// <summary>A ticket the Agent is an active collaborator on. IsSuspended = ticket currently unassigned (no access).
/// IsPrimaryAssignee = the Agent now holds the ticket (use Ticket Details instead).</summary>
public sealed record MyCollaborationDetails(
    Guid TicketId, string Subject, TicketStatus Status, TicketPriority Priority,
    bool IsSuspended, bool IsPrimaryAssignee, DateTimeOffset AddedAtUtc, DateTimeOffset? LastMessageAtUtc);
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/ICollaborationRepository.cs`**

```csharp
/// <summary>
/// Collaboration access is evaluated from CURRENT state in SQL on every call (never cached):
/// SuperAdmin ⇒ any ticket; Agent ⇒ ticket assigned to them, or assigned to someone AND an active membership exists for them.
/// Methods taking StaffActor return null when the ticket is missing or not accessible.
/// </summary>
public interface ICollaborationRepository
{
    Task<CollaborationTicketContext?> GetContextAsync(Guid ticketId, StaffActor actor, CancellationToken cancellationToken = default);

    /// <summary>Newest first (CreatedAtUtc DESC, Id DESC). The access predicate is part of BOTH statements.</summary>
    Task<PagedResult<TicketMessageDetails>?> ListMessagesAsync(
        Guid ticketId, StaffActor actor, int page, int pageSize, CancellationToken cancellationToken = default);

    void AddMessage(TicketInternalMessage message);
    Task<TicketMessageDetails> GetMessageAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Active memberships only, ordered by Agent last name, first name.</summary>
    Task<IReadOnlyList<TicketCollaboratorDetails>> ListCollaboratorsAsync(Guid ticketId, CancellationToken cancellationToken = default);

    /// <summary>Tracked active membership, or null.</summary>
    Task<TicketCollaborator?> GetActiveMembershipAsync(Guid ticketId, Guid agentUserId, CancellationToken cancellationToken = default);

    void AddCollaborator(TicketCollaborator collaborator);

    /// <summary>Active memberships of agentUserId, newest invitation first (AddedAtUtc DESC, Id DESC).</summary>
    Task<PagedResult<MyCollaborationDetails>> ListMyCollaborationsAsync(
        Guid agentUserId, int page, int pageSize, CancellationToken cancellationToken = default);
}
```

(Add `using Crm.Application.Collaboration; using Crm.Application.Common; using Crm.Application.Common.Paging; using Crm.Domain.Collaboration;`.)

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/ITicketAccessLock.cs`** (F3)

```csharp
namespace Crm.Application.Abstractions.Persistence;

/// <summary>A ticket's current assignment, read under a row lock that is held until the transaction ends.</summary>
public sealed record TicketAssignmentLock(Guid TicketId, Guid? AssignedAgentUserId);

/// <summary>
/// Write-time authorization (DASH-002 F3). Both methods MUST run inside an explicit transaction (an ITransactionalCommand);
/// they throw InvalidOperationException otherwise. UPDLOCK + HOLDLOCK + ROWLOCK keep the returned facts true until commit:
/// a concurrent ticket (re/un)assignment or membership removal/insert of the same key waits. Reads only — changes nothing.
/// </summary>
public interface ITicketAccessLock
{
    /// <summary>Null when the ticket does not exist.</summary>
    Task<TicketAssignmentLock?> LockTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);

    /// <summary>True when agentUserId has an active membership on ticketId. HOLDLOCK also locks the key range when absent,
    /// so a concurrent invite of the same Agent waits.</summary>
    Task<bool> LockActiveMembershipAsync(Guid ticketId, Guid agentUserId, CancellationToken cancellationToken = default);
}
```

### 4 — Application: use cases

Conventions for **every** use case below (copy from `CreateTicketCommand.cs` / `TransitionTicketStatusCommand.cs`):

- One file per use case under `Crm.Application/<Feature>/<UseCase>/`: `sealed record …Command/Query(...) : ICommand<T>/IQuery<T>` + `sealed class …Handler(...)` with primary-constructor dependencies; validator in a sibling `…Validator.cs` (`AbstractValidator<T>`), auto-registered.
- Actor is the first parameter (`StaffActor Actor` or `Guid AgentUserId` for Agent-only endpoints). **Single save — not `ITransactionalCommand`**, **except** the lock-then-write commands of F2/F3: `CreateAgentTaskCommand`, `SetTaskTicketLinkCommand`, `ReassignAgentTaskCommand`, `PostTicketMessageCommand`, `InviteCollaboratorCommand`, `RemoveCollaboratorCommand` implement `ITransactionalCommand` (in addition to `ICommand<T>`) so their row locks are held until commit. Their handlers still call `unitOfWork.SaveChangesAsync` explicitly (the mapped save); `ExecuteInTransactionAsync`'s final save is then a no-op. **Replay rules (R1):** these handlers can run more than once per request — every transient failure before commit re-runs the whole handler on a cleared change tracker — so they keep no instance state, re-load every entity, re-check `If-Match` and locks, perform no external side effects and never catch database exceptions. No-op ⇒ no save.
- Every **task mutation** command carries `byte[] ExpectedVersion` (from `If-Match`) right after `Id`; the handler loads the task (404), then `AgentTaskGuards.EnsureVersion` (409), then every other rule. Validators: `ExpectedVersion` `NotEmpty()`. Read back via the repository's `FindDetails…` under the same actor.
- 400 ⇒ `new ValidationException([new ValidationFailure(property, message)])` (private static `Invalid` helper as `CreateTicketCommand.cs` lines 90–92). 404 ⇒ `NotFoundException($"Task '{id}' was not found.")` / `"Quick reply '{id}' was not found."` / `"Ticket '{id}' was not found."`. 409 ⇒ `ConflictException`. 403 ⇒ `ForbiddenException`.
- `now = timeProvider.GetUtcNow()` once per handler.
- Validators: `Page` `InclusiveBetween(1, Paging.MaxPage)`, `PageSize` `GreaterThanOrEqualTo(1)` (handler clamps with `Math.Min(…, Paging.MaxPageSize)`), enums `IsInEnum()`, ids `NotEqual(Guid.Empty)`, text `NotEmpty().MaximumLength(…)` (NotEmpty rejects whitespace-only). Text is trimmed in the handler; an optional description that is whitespace-only becomes `null`.

#### 4a — Tasks (`Crm.Application/AgentTasks/`)

**Create file: `backend/src/Crm.Application/AgentTasks/AgentTaskGuards.cs`** — internal static helpers shared by task handlers:

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.AgentTasks;
using Crm.Domain.Identity;
using FluentValidation;
using FluentValidation.Results;

namespace Crm.Application.AgentTasks;

internal static class AgentTaskGuards
{
    public static ValidationException Invalid(string property, string message) => new([new ValidationFailure(property, message)]);

    public static async Task EnsureActiveAgentAsync(IIdentityService identity, Guid userId, string property, CancellationToken ct)
    {
        var user = await identity.FindByIdAsync(userId, ct);
        if (user is not { Role: UserRoles.Agent, IsActive: true })
        {
            throw Invalid(property, "Select an active Agent.");
        }
    }

    /// <summary>F1: the If-Match version must equal the loaded row. Checked before any rule, also for no-ops.</summary>
    public static void EnsureVersion(AgentTask task, byte[] expectedVersion)
    {
        if (!task.Version.AsSpan().SequenceEqual(expectedVersion))
        {
            throw new ConflictException("The task was changed by someone else. Reload it and try again.");
        }
    }

    /// <summary>
    /// F3 — call only inside an ITransactionalCommand. The ticket row stays locked until commit, so a concurrent reassignment
    /// either committed before (⇒ rejected here) or waits for this transaction. Collaborator membership never counts.
    /// One generic message for missing / unassigned / someone else's ticket (no existence leak).
    /// </summary>
    public static async Task EnsureTicketAssignedToOwnerAsync(ITicketAccessLock ticketLock, Guid ticketId, Guid ownerUserId, CancellationToken ct)
    {
        var ticket = await ticketLock.LockTicketAsync(ticketId, ct);
        if (ticket?.AssignedAgentUserId != ownerUserId)
        {
            throw Invalid("ticketId", "Select a ticket that is currently assigned to the task owner.");
        }
    }

    /// <summary>Only for a new or changed reminder time.</summary>
    public static void EnsureValidReminder(DateTimeOffset remindAtUtc, DateTimeOffset? dueAtUtc, DateTimeOffset now)
    {
        if (remindAtUtc <= now)
        {
            throw Invalid("remindAtUtc", "Choose a future reminder time.");
        }

        if (dueAtUtc is { } due && remindAtUtc > due)
        {
            throw Invalid("remindAtUtc", "The reminder must be at or before the due time.");
        }
    }

    public static void EnsureNotArchived(AgentTask task)
    {
        if (task.IsArchived)
        {
            throw new ConflictException("The task is archived. Restore it first.");
        }
    }
}
```

Use cases (folder ⇒ message ⇒ rules). Every mutation: load via `GetByIdAsync(id, actor.AgentScope)` ?? 404 ⇒ `EnsureVersion(task, command.ExpectedVersion)` ⇒ rules below ⇒ save only on change ⇒ read back with `FindDetailsByIdAsync(id, actor, now)` (the response carries the new `Version`).

| Folder / message | Handler dependencies | Rules |
|---|---|---|
| `CreateAgentTask/CreateAgentTaskCommand(StaffActor Actor, string Title, string? Description, Guid? AssignedAgentUserId, Guid? TicketId, DateTimeOffset? DueAtUtc, DateTimeOffset? RemindAtUtc) : ICommand<AgentTaskDetails>, ITransactionalCommand` | `IAgentTaskRepository`, `ITicketAccessLock`, `IIdentityService`, `IUnitOfWork`, `TimeProvider` | Full code below. No `If-Match` (nothing exists yet). |
| `GetAgentTask/GetAgentTaskQuery(StaffActor Actor, Guid Id)` | repo, `TimeProvider` | `FindDetailsByIdAsync(id, actor, now)` ?? 404. |
| `ListAgentTasks/ListAgentTasksQuery(StaffActor Actor, string? Search, AgentTaskStatus? Status, TaskDueFilter? DueState, bool Archived = false, bool? HasReminder = null, Guid? AssignedAgentUserId = null, int Page = 1, int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<AgentTaskDetails>>` | repo, `TimeProvider` | Trim search (null if blank); clamp pageSize. Validator: search ≤ 100, enums, paging. |
| `ListLinkableTickets/ListLinkableTicketsQuery(StaffActor Actor, Guid? OwnerUserId, string? Search) : IQuery<IReadOnlyList<LinkableTicket>>` (F5) | repo, `IIdentityService` | Agent: `OwnerUserId` null or the caller, else 400 `ownerUserId` "Agents can only link their own tickets."; owner = caller. SuperAdmin: `OwnerUserId` required (400 "Select an active Agent.") and `EnsureActiveAgentAsync`. `ListLinkableTicketsAsync(owner, trimmed search, 10)`. Validator: search ≤ 100, `OwnerUserId` `NotEqual(Guid.Empty)`. |
| `UpdateAgentTask/UpdateAgentTaskCommand(StaffActor Actor, Guid Id, byte[] ExpectedVersion, string Title, string? Description, DateTimeOffset? DueAtUtc) : ICommand<AgentTaskDetails>` | repo, `IUnitOfWork`, `TimeProvider` | `EnsureNotArchived`; due-vs-reminder check (exact condition in the note below the table) ⇒ 400 `dueAtUtc`; `UpdateDetails` ⇒ save if changed. Never touches link, owner or reminder. |
| `SetTaskTicketLink/SetTaskTicketLinkCommand(StaffActor Actor, Guid Id, byte[] ExpectedVersion, Guid? TicketId) : ICommand<AgentTaskDetails>, ITransactionalCommand` | repo, `ITicketAccessLock`, `IUnitOfWork`, `TimeProvider` | `EnsureNotArchived`; non-null ⇒ `EnsureTicketAssignedToOwnerAsync(ticketLock, ticketId, task.AssignedAgentUserId)` (locked, F3); null = unlink (always allowed, also for an Inaccessible link). `PUT` sends id, `DELETE` sends null. |
| `ScheduleTaskReminder/ScheduleTaskReminderCommand(StaffActor Actor, Guid Id, byte[] ExpectedVersion, DateTimeOffset? RemindAtUtc)` | repo, `IUnitOfWork`, `TimeProvider` | Null (cancel) ⇒ `ScheduleReminder(null)`, always allowed. Non-null: `!task.IsOpenForReminders` ⇒ 409 "Reopen or restore the task before scheduling a reminder."; equal to current `RemindAtUtc` ⇒ no-op; else `EnsureValidReminder(value, task.DueAtUtc, now)` then `ScheduleReminder`. |
| `SetTaskCompletion/SetTaskCompletionCommand(StaffActor Actor, Guid Id, byte[] ExpectedVersion, bool Completed)` | repo, `IUnitOfWork`, `TimeProvider` | `EnsureNotArchived`; `Complete`/`Reopen`; repeat ⇒ no-op (200, unchanged version). Already-issued notifications stay and show the new task state. |
| `SetTaskArchived/SetTaskArchivedCommand(StaffActor Actor, Guid Id, byte[] ExpectedVersion, bool Archived)` | repo, `IUnitOfWork`, `TimeProvider` | `Archive`/`Restore`; repeat ⇒ no-op. |
| `ReassignAgentTask/ReassignAgentTaskCommand(StaffActor Actor, Guid Id, byte[] ExpectedVersion, Guid? AssignedAgentUserId, bool RemoveTicketLink) : ICommand<AgentTaskDetails>, ITransactionalCommand` | repo, `ITaskReminderRepository`, `ITicketAccessLock`, `IIdentityService`, `IUnitOfWork`, `TimeProvider` | Steps below the table. |
| `GetAgentTaskSummary/GetAgentTaskSummaryQuery(Guid AgentUserId) : IQuery<AgentTaskSummary>` | repo, `TimeProvider` | `GetPendingCountsAsync(agentId, now)` + `ListAsync(new(null, Pending, null, false, null, null), new StaffActor(agentId, false), now, 1, AgentTaskRules.DashboardPreviewLimit)` ⇒ `.Items`. No validator. |

> `UpdateAgentTask` due-vs-reminder condition, exactly: `if (command.DueAtUtc != task.DueAtUtc && task.RemindAtUtc is { } remindAt && command.DueAtUtc is { } due && remindAt > due && task.IsOpenForReminders) throw Invalid("dueAtUtc", "…")`.

**`ReassignAgentTaskCommandHandler` steps (F2 + F3, one transaction):**

1. Not SuperAdmin ⇒ `InvalidOperationException` (guarded by the SuperAdmin policy, as `ChangeTicketAssigneeCommand.cs` line 24).
2. `GetByIdAsync(id, null)` ?? 404 ⇒ `EnsureVersion` ⇒ `EnsureNotArchived`. Validator: `AssignedAgentUserId` `NotNull().NotEqual(Guid.Empty)`.
3. Same owner and `!RemoveTicketLink` ⇒ no-op, return (before the active check, as `ChangeTicketAssigneeCommand.cs` lines 30–31).
4. `EnsureActiveAgentAsync(newOwner)`.
5. `task.TicketId is { } t && !RemoveTicketLink` ⇒ `ticketLock.LockTicketAsync(t)`; `AssignedAgentUserId != newOwner` ⇒ 409 "The linked ticket is not assigned to the new owner. Remove the link to reassign the task."
6. `var ownerChanged = task.AssignedAgentUserId != newOwner; task.Reassign(newOwner, RemoveTicketLink, now)` ⇒ `await unitOfWork.SaveChangesAsync()` — the rowversion-checked `UPDATE AgentTasks` now holds the task row's X lock, so a concurrent `IssueDueAsync` (which reads the row with `UPDLOCK`) waits and will then see the new owner.
7. Only if `ownerChanged`: `await reminders.TransferUnreadAsync(task.Id, newOwner, now)` — moves any unread notification, including one the old owner's check inserted and committed just before step 6.
8. Return `FindDetailsByIdAsync`. `TransactionBehavior` commits steps 5–7 together; any exception rolls all of them back (no half-moved reminders, no reassignment without its transfer).
9. **Retry (R1):** a transient failure in steps 5–7 — typically a deadlock on the transfer **after** step 6's `UPDATE` succeeded and its tracked task was accepted with the new `Version` — rolls the transaction back and clears the change tracker; the next attempt restarts at step 2, re-reads the rolled-back row (original `Version`, so the unchanged `If-Match` still matches), re-locks the ticket and redoes steps 3–7. Nothing from the failed attempt is reused. A failure **during commit** is never replayed ⇒ 503 (`CommitOutcomeUnknownException`).

**Create file: `backend/src/Crm.Application/AgentTasks/CreateAgentTask/CreateAgentTaskCommand.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Domain.AgentTasks;
using Mediator;

namespace Crm.Application.AgentTasks.CreateAgentTask;

/// <summary>
/// Transactional (F3): an optional ticket link is authorized from the row-locked ticket, which stays locked until commit.
/// Owner and creator come from Actor, never trusted from the client:
/// Agent ⇒ always themselves (another id is rejected); SuperAdmin ⇒ a required active Agent.
/// Optional reminder must be future and ≤ due. One explicit save.
/// </summary>
public sealed record CreateAgentTaskCommand(
    StaffActor Actor,
    string Title,
    string? Description,
    Guid? AssignedAgentUserId,
    Guid? TicketId,
    DateTimeOffset? DueAtUtc,
    DateTimeOffset? RemindAtUtc) : ICommand<AgentTaskDetails>, ITransactionalCommand;

public sealed class CreateAgentTaskCommandHandler(
    IAgentTaskRepository tasks,
    ITicketAccessLock ticketLock,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateAgentTaskCommand, AgentTaskDetails>
{
    public async ValueTask<AgentTaskDetails> Handle(CreateAgentTaskCommand command, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var ownerId = await ResolveOwnerAsync(command, cancellationToken);

        if (command.RemindAtUtc is { } remindAt)
        {
            AgentTaskGuards.EnsureValidReminder(remindAt, command.DueAtUtc, now);
        }

        // Last check before the write: the lock keeps the ticket's assignment fixed until this transaction commits.
        if (command.TicketId is { } ticketId)
        {
            await AgentTaskGuards.EnsureTicketAssignedToOwnerAsync(ticketLock, ticketId, ownerId, cancellationToken);
        }

        var task = AgentTask.Create(
            Guid.CreateVersion7(),
            command.Title.Trim(),
            string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            ownerId,
            command.Actor.UserId,
            command.TicketId,
            command.DueAtUtc,
            command.RemindAtUtc,
            now);

        tasks.Add(task);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Always visible: an Agent owns it; SuperAdmin is unrestricted. Carries the store-generated Version.
        return (await tasks.FindDetailsByIdAsync(task.Id, command.Actor, now, cancellationToken))!;
    }

    private async Task<Guid> ResolveOwnerAsync(CreateAgentTaskCommand command, CancellationToken cancellationToken)
    {
        if (!command.Actor.IsSuperAdmin)
        {
            if (command.AssignedAgentUserId is { } requested && requested != command.Actor.UserId)
            {
                throw AgentTaskGuards.Invalid(nameof(command.AssignedAgentUserId), "Agents cannot choose a task owner; tasks you create are yours.");
            }

            return command.Actor.UserId;
        }

        if (command.AssignedAgentUserId is not { } agentId)
        {
            throw AgentTaskGuards.Invalid(nameof(command.AssignedAgentUserId), "Select an active Agent.");
        }

        await AgentTaskGuards.EnsureActiveAgentAsync(identity, agentId, nameof(command.AssignedAgentUserId), cancellationToken);
        return agentId;
    }
}
```

**Create file: `…/CreateAgentTask/CreateAgentTaskValidator.cs`** — `Title` NotEmpty + Max 200; `Description` Max 2000; `AssignedAgentUserId` / `TicketId` `NotEqual(Guid.Empty)`. `UpdateAgentTaskValidator` same for Title/Description plus `Id` NotEmpty.

#### 4b — Reminders (`Crm.Application/AgentTasks/Reminders/`)

| Folder / message | Rules |
|---|---|
| `CheckTaskReminders/CheckTaskRemindersCommand(Guid AgentUserId) : ICommand<TaskReminderCheck>` | `issued = IssueDueAsync(agentId, now)`; `unread = CountUnreadAsync(agentId)`; return `(unread, issued, now)`. **No `IUnitOfWork`** — the issuance is one self-contained SQL statement (document in the handler summary). Not transactional. |
| `ListTaskReminders/ListTaskRemindersQuery(Guid AgentUserId, bool UnreadOnly = false, int Page = 1, int PageSize = Paging.DefaultPageSize)` | clamp; repository list. Validator: paging. |
| `MarkTaskReminderRead/MarkTaskReminderReadCommand(Guid AgentUserId, long Id) : ICommand` (returns `Unit`) | `MarkReadAsync(id, agentId, now)`: `NotFound` ⇒ 404 `"Reminder '{id}' was not found."`; `MarkedRead` / `AlreadyRead` ⇒ success (204). One conditional `UPDATE` (no `IUnitOfWork`), so the former owner can never mark a reminder read after it moved to the new owner. Never touches the task. |

#### 4c — Quick replies (`Crm.Application/QuickReplies/`)

| Folder / message | Rules |
|---|---|
| `CreateQuickReply/CreateQuickReplyCommand(StaffActor Actor, string Title, string Body, TicketCategory? Category) : ICommand<QuickReplyDetails>` | Validator: Title NotEmpty ≤ 120, Body NotEmpty ≤ 2000, Category NotNull IsInEnum. Agent ⇒ `CreatePersonal(…, ownerUserId: actor.UserId)`; SuperAdmin ⇒ `CreateShared`. Title/Body trimmed. **No scope/owner in the command.** |
| `GetQuickReply/GetQuickReplyQuery(StaffActor Actor, Guid Id)` | visible ?? 404. |
| `ListQuickReplies/ListQuickRepliesQuery(StaffActor Actor, string? Search, TicketCategory? Category, QuickReplyScope? Scope, bool Archived = false, int Page = 1, int PageSize = 20)` | trim/clamp; validator search ≤ 100, enums, paging. |
| `UpdateQuickReply/UpdateQuickReplyCommand(StaffActor Actor, Guid Id, string Title, string Body, TicketCategory? Category)` | `GetVisibleAsync` ?? 404; `EnsureCanEdit` (below); archived ⇒ 409 "The quick reply is archived. Restore it first."; `Update` ⇒ save if changed. |
| `SetQuickReplyArchived/SetQuickReplyArchivedCommand(StaffActor Actor, Guid Id, bool Archived)` | `GetVisibleAsync` ?? 404; `EnsureCanEdit`; `Archive`/`Restore`; repeat = no-op. |

`EnsureCanEdit(template, actor)` (private static in a `QuickReplyGuards` internal class): SuperAdmin ⇒ `template.Scope == Shared` (always true for visible rows); Agent ⇒ `template.Scope == Personal && template.OwnerUserId == actor.UserId`, otherwise `throw new ForbiddenException("Shared quick replies are managed by SuperAdmin.")`.

#### 4d — Collaboration (`Crm.Application/Collaboration/`)

| Folder / message | Rules |
|---|---|
| `GetCollaborationTicket/GetCollaborationTicketQuery(StaffActor Actor, Guid TicketId) : IQuery<CollaborationTicketContext>` | `GetContextAsync` ?? 404 `"Ticket '{id}' was not found."` |
| `ListTicketMessages/ListTicketMessagesQuery(StaffActor Actor, Guid TicketId, int Page = 1, int PageSize = 20)` | `ListMessagesAsync` ?? 404. |
| `PostTicketMessage/PostTicketMessageCommand(StaffActor Actor, Guid TicketId, string Body) : ICommand<TicketMessageDetails>, ITransactionalCommand` | Validator: Body NotEmpty ≤ 2000. Deps: `ICollaborationRepository`, `ITicketAccessLock`, `IUnitOfWork`, `TimeProvider`. `CollaborationWriteAccess.ResolveAsync` (locked, F3) ?? 404 (any access level may post, any ticket status). `AddMessage(TicketInternalMessage.Post(ticketId, actor.UserId, body.Trim(), now))`, save, `GetMessageAsync(message.Id)`. **Does not load or modify the `Ticket`** (the lock is a read lock; no `UPDATE Tickets`). |
| `ListTicketCollaborators/ListTicketCollaboratorsQuery(StaffActor Actor, Guid TicketId) : IQuery<IReadOnlyList<TicketCollaboratorDetails>>` | context ?? 404 (all participants may see who is in the discussion). |
| `ListCollaboratorCandidates/ListCollaboratorCandidatesQuery(StaffActor Actor, Guid TicketId) : IQuery<IReadOnlyList<CollaboratorCandidate>>` | context ?? 404; `!CanManage` ⇒ 403; `identity.ListByRoleAsync(UserRoles.Agent)` ⇒ `IsActive`, `Id != context.AssignedAgentUserId`, `Id != actor.UserId`, not in active collaborators ⇒ `(Id, FirstName + " " + LastName)`. |
| `InviteCollaborator/InviteCollaboratorCommand(StaffActor Actor, Guid TicketId, Guid AgentUserId) : ICommand<TicketCollaboratorDetails>, ITransactionalCommand` | Validator `AgentUserId` NotEmpty. `ResolveAsync` (locked) ?? 404; not SuperAdmin/PrimaryAgent ⇒ `ForbiddenException("Only SuperAdmin or the ticket's assigned Agent can manage collaborators.")`; `AgentUserId == actor.UserId` ⇒ 400 "You cannot invite yourself."; `AgentUserId == lockedTicket.AssignedAgentUserId` ⇒ 400 "The assigned Agent already has access."; target `{ Role: Agent, IsActive: true }` else 400 "Select an active Agent."; `ticketLock.LockActiveMembershipAsync(ticketId, AgentUserId)` true ⇒ 409 "This Agent is already a collaborator." (the key-range lock makes parallel invites of the same Agent wait, then see the row); `AddCollaborator(TicketCollaborator.Invite(Guid.CreateVersion7(), …, actor.UserId, now))`, save; return the matching row from `ListCollaboratorsAsync`. |
| `RemoveCollaborator/RemoveCollaboratorCommand(StaffActor Actor, Guid TicketId, Guid AgentUserId) : ICommand, ITransactionalCommand` | `ResolveAsync` (locked) ?? 404; not a manager ⇒ 403 (same message); `LockActiveMembershipAsync` false ⇒ 404 `"Collaborator not found."`; `GetActiveMembershipAsync` (tracked) ⇒ `Remove(actor.UserId, now)`, save. Messages are never touched. |
| `ListMyCollaborations/ListMyCollaborationsQuery(Guid AgentUserId, int Page = 1, int PageSize = 20)` | clamp; repository. |

**Create file: `backend/src/Crm.Application/Collaboration/CollaborationWriteAccess.cs`** (F3 — the write-side twin of `CollaborationRepository.Accessible`, evaluated from locked rows; call only inside an `ITransactionalCommand`):

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;

namespace Crm.Application.Collaboration;

internal static class CollaborationWriteAccess
{
    /// <summary>Null = no access (handler throws 404). Locks the ticket row, and for a non-primary Agent the membership key, until commit.</summary>
    public static async Task<(CollaborationAccess Access, TicketAssignmentLock Ticket)?> ResolveAsync(
        ITicketAccessLock locks, StaffActor actor, Guid ticketId, CancellationToken cancellationToken)
    {
        var ticket = await locks.LockTicketAsync(ticketId, cancellationToken);
        if (ticket is null)
        {
            return null;
        }

        if (actor.IsSuperAdmin)
        {
            return (CollaborationAccess.SuperAdmin, ticket);
        }

        if (ticket.AssignedAgentUserId == actor.UserId)
        {
            return (CollaborationAccess.PrimaryAgent, ticket);
        }

        // Unassigned ⇒ suspended, without touching the membership row.
        return ticket.AssignedAgentUserId is not null && await locks.LockActiveMembershipAsync(ticketId, actor.UserId, cancellationToken)
            ? (CollaborationAccess.Collaborator, ticket)
            : null;
    }
}
```

### 5 — Infrastructure: EF configuration, DbSets, repositories, unit of work

**File: `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`** — after line 19 add (plus `using Crm.Domain.AgentTasks; using Crm.Domain.Collaboration; using Crm.Domain.QuickReplies;`):

```csharp
    public DbSet<AgentTask> AgentTasks => Set<AgentTask>();
    public DbSet<TaskReminderNotification> TaskReminderNotifications => Set<TaskReminderNotification>();
    public DbSet<QuickReplyTemplate> QuickReplyTemplates => Set<QuickReplyTemplate>();
    public DbSet<TicketCollaborator> TicketCollaborators => Set<TicketCollaborator>();
    public DbSet<TicketInternalMessage> TicketInternalMessages => Set<TicketInternalMessage>();
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/AgentTaskConfiguration.cs`** (style of `TicketConfiguration.cs`):

```csharp
internal sealed class AgentTaskConfiguration : IEntityTypeConfiguration<AgentTask>
{
    public void Configure(EntityTypeBuilder<AgentTask> builder)
    {
        builder.ToTable("AgentTasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20); // 'Pending' / 'Completed' (raw SQL in TaskReminderRepository relies on it)
        builder.Ignore(t => t.IsOpenForReminders);

        // References only; nothing cascades (accounts, tickets and tasks are never deleted).
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.AssignedAgentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(t => t.TicketId).OnDelete(DeleteBehavior.Restrict);

        // Agent and SuperAdmin can edit the same task: two simultaneous writes cannot both commit (EfUnitOfWork ⇒ 409).
        builder.Property(t => t.Version).IsRowVersion(); // F1: exposed as ETag; EF adds WHERE [Version] = @original to every UPDATE

        builder.HasIndex(t => new { t.AssignedAgentUserId, t.IsArchived, t.Status, t.DueAtUtc, t.Id });       // owner lists, counts, preview
        builder.HasIndex(t => new { t.AssignedAgentUserId, t.RemindAtUtc }).HasFilter("[RemindAtUtc] IS NOT NULL"); // reminder eligibility
    }
}
```

**Create file: `…/Configurations/TaskReminderNotificationConfiguration.cs`** — table `TaskReminderNotifications`; `Id` `ValueGeneratedOnAdd()` (bigint identity); FKs `TaskId` → `AgentTask`, `RecipientUserId` and `TransferredFromUserId` → `ApplicationUser`, all Restrict; `HasIndex(n => new { n.TaskId, n.ReminderGeneration }).IsUnique().HasDatabaseName("UX_TaskReminderNotifications_TaskGeneration")` (**dedup guarantee**; `TaskId` leading also serves `TransferUnreadAsync`); `HasIndex(n => new { n.RecipientUserId, n.IssuedAtUtc, n.Id }).IsDescending(false, true, true)` (inbox page / unread count).

**Create file: `…/Configurations/QuickReplyTemplateConfiguration.cs`** — table `QuickReplyTemplates`; `Id` never generated; `Title` required 120; `Body` required 2000; `Category` and `Scope` `HasConversion<string>().HasMaxLength(20)`; FKs `OwnerUserId`, `CreatedByUserId`, `UpdatedByUserId` → `ApplicationUser` Restrict; indexes `(Scope, IsArchived, Title, Id)` and `(OwnerUserId, IsArchived, Title, Id)`; check constraint `builder.ToTable(t => t.HasCheckConstraint("CK_QuickReplyTemplates_Owner", "([Scope] = N'Personal' AND [OwnerUserId] IS NOT NULL) OR ([Scope] = N'Shared' AND [OwnerUserId] IS NULL)"))` (combine with `ToTable("QuickReplyTemplates", t => …)`).

**Create file: `…/Configurations/TicketCollaboratorConfiguration.cs`** — table `TicketCollaborators`; `Id` never generated; FKs `TicketId` → `Ticket`, `AgentUserId`, `AddedByUserId`, `RemovedByUserId` → `ApplicationUser`, all Restrict; `HasIndex(c => new { c.TicketId, c.AgentUserId }).IsUnique().HasFilter("[RemovedAtUtc] IS NULL").HasDatabaseName("UX_TicketCollaborators_ActiveMembership")` (one active membership; the name is referenced by `EfUnitOfWork.KnownUniqueIndexes` and by the lock query); `HasIndex(c => new { c.AgentUserId, c.RemovedAtUtc })` (My Collaborations / access check).

**Create file: `…/Configurations/TicketInternalMessageConfiguration.cs`** — table `TicketInternalMessages`; `Id` `ValueGeneratedOnAdd()`; `Body` required 2000; FKs `TicketId` → `Ticket`, `AuthorUserId` → `ApplicationUser`, Restrict; `HasIndex(m => new { m.TicketId, m.CreatedAtUtc, m.Id }).IsDescending(false, true, true)`.

**File: `backend/src/Crm.Infrastructure/Persistence/EfUnitOfWork.cs`** (F4) — keep the `DbUpdateConcurrencyException` arm (lines 15–19) unchanged; add `using Microsoft.Data.SqlClient;`, an allow-list and one narrowly filtered catch after it:

```csharp
    // Only unique indexes that encode a business rule a concurrent user can legitimately hit. Any other unique violation is a
    // defect (or an Identity/infrastructure problem) and must surface as a logged 500, never as a misleading 409.
    private static readonly IReadOnlyDictionary<string, string> KnownUniqueIndexes = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["UX_TicketCollaborators_ActiveMembership"] = "This Agent is already a collaborator.",
    };

        // inside SaveChangesAsync, after the concurrency catch:
        catch (DbUpdateException exception) when (TryGetKnownUniqueConflict(exception, out var message))
        {
            throw new ConflictException(message); // nothing was saved
        }

    private static bool TryGetKnownUniqueConflict(DbUpdateException exception, out string message)
    {
        message = string.Empty;
        if (exception.InnerException is not SqlException { Number: 2601 or 2627 } sql)
        {
            return false;
        }

        foreach (var (index, text) in KnownUniqueIndexes)
        {
            if (sql.Message.Contains($"'{index}'", StringComparison.Ordinal)) // SQL Server: "… with unique index 'UX_…'"
            {
                message = text;
                return true;
            }
        }

        return false;
    }
```

Transactional handlers call `IUnitOfWork.SaveChangesAsync` themselves, so their writes go through the mapping above.

**Same file — replace `ExecuteInTransactionAsync` (lines 22–55)** (R1; signature and nested-join behaviour unchanged; add `using Crm.Application.Common.Exceptions;` if not present):

```csharp
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        // Nested transactional command: join the outer transaction; the outer call commits.
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        // Required when EnableRetryOnFailure is on: one attempt = cleared tracker + transaction + WHOLE handler + save.
        // Attempts are retried only up to COMMIT; the commit itself is never replayed.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async ct =>
            {
                // Entities accepted by a failed attempt's SaveChanges were rolled back in the database: never reuse them.
                dbContext.ChangeTracker.Clear();

                await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
                TResult result;
                try
                {
                    result = await operation(ct);
                    await SaveChangesAsync(ct); // mapped save (409 rules); usually a no-op after the handler's own save
                }
                catch
                {
                    try
                    {
                        await transaction.RollbackAsync(CancellationToken.None);
                    }
                    catch
                    {
                        // Connection already broken: SQL Server rolls the transaction back itself. Keep the original exception.
                    }

                    dbContext.ChangeTracker.Clear();
                    throw; // transient ⇒ the strategy re-runs this lambda from the top; anything else surfaces unchanged
                }

                try
                {
                    // Not cancellable: a client disconnect must not turn a commit into an unknown outcome.
                    await transaction.CommitAsync(CancellationToken.None);
                }
                catch (Exception exception)
                {
                    // The commit may have been applied. Never replay the handler: surface a NON-transient exception (503).
                    dbContext.ChangeTracker.Clear();
                    throw new CommitOutcomeUnknownException(exception);
                }

                return result;
            },
            cancellationToken);
    }
```

- Exceptions that must not be retried keep their existing types (`ConflictException` from concurrency / allow-listed unique conflicts, `ValidationException`, `NotFoundException`, `ForbiddenException`, `DomainException`) — none is transient, so the strategy surfaces them on the first attempt.
- **Non-transactional single saves** (`IUnitOfWork.SaveChangesAsync` outside a transaction) keep EF's built-in behaviour: `SaveChanges` runs through the execution strategy and may be replayed after a lost acknowledgement. That replay is still safe — a task update meets the new `Version` ⇒ 409 (the change is applied; the UI reload shows it); a replayed insert keeps its client-generated Guid key ⇒ primary-key violation ⇒ 500 (not allow-listed) — never a duplicate row or a double-applied change.

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/AgentTaskRepository.cs`**

```csharp
internal sealed class AgentTaskRepository(CrmDbContext dbContext) : IAgentTaskRepository
{
    public void Add(AgentTask task) => dbContext.AgentTasks.Add(task);

    public Task<AgentTask?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) =>
        Scope(dbContext.AgentTasks, agentScope).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<AgentTaskDetails?> FindDetailsByIdAsync(Guid id, StaffActor actor, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Project(Scope(dbContext.AgentTasks.AsNoTracking(), actor.AgentScope).Where(t => t.Id == id), actor.IsSuperAdmin, now)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<AgentTaskDetails>> ListAsync(
        AgentTaskListFilter filter, StaffActor actor, DateTimeOffset now, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var soon = now + AgentTaskRules.DueSoonWindow;
        var query = Scope(dbContext.AgentTasks.AsNoTracking(), actor.AgentScope).Where(t => t.IsArchived == filter.Archived);

        if (filter.AssignedAgentUserId is { } ownerId) query = query.Where(t => t.AssignedAgentUserId == ownerId);
        if (filter.Status is { } status) query = query.Where(t => t.Status == status);
        if (filter.HasReminder is { } hasReminder) query = query.Where(t => (t.RemindAtUtc != null) == hasReminder);
        if (!string.IsNullOrEmpty(filter.Search)) { var search = filter.Search; query = query.Where(t => t.Title.Contains(search)); }
        query = filter.DueState switch
        {
            TaskDueFilter.Overdue => query.Where(t => t.Status == AgentTaskStatus.Pending && !t.IsArchived && t.DueAtUtc < now),
            TaskDueFilter.DueSoon => query.Where(t => t.Status == AgentTaskStatus.Pending && !t.IsArchived && t.DueAtUtc >= now && t.DueAtUtc <= soon),
            TaskDueFilter.NoDueDate => query.Where(t => t.DueAtUtc == null),
            _ => query,
        };

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await Project(query, actor.IsSuperAdmin, now).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<AgentTaskDetails>(items, page, pageSize, totalCount);
    }

    public async Task<AgentTaskCounts> GetPendingCountsAsync(Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var soon = now + AgentTaskRules.DueSoonWindow;
        return await dbContext.AgentTasks.AsNoTracking()
                .Where(t => t.AssignedAgentUserId == agentUserId && t.Status == AgentTaskStatus.Pending && !t.IsArchived)
                .GroupBy(_ => 1) // one COUNT(CASE WHEN …) statement, as AgentDashboardRepository
                .Select(g => new AgentTaskCounts(
                    g.Count(),
                    g.Count(t => t.DueAtUtc < now),
                    g.Count(t => t.DueAtUtc >= now && t.DueAtUtc <= soon)))
                .SingleOrDefaultAsync(cancellationToken)
            ?? new AgentTaskCounts(0, 0, 0);
    }

    // F5 picker: same ownership filter as ticket reads (TicketRepository.Scope is internal, as used by AgentDashboardRepository).
    public async Task<IReadOnlyList<LinkableTicket>> ListLinkableTicketsAsync(
        Guid ownerUserId, string? search, int limit, CancellationToken cancellationToken = default)
    {
        var tickets = TicketRepository.Scope(dbContext.Tickets.AsNoTracking(), ownerUserId);
        if (!string.IsNullOrEmpty(search))
        {
            tickets = tickets.Where(t => t.Subject.Contains(search));
        }

        return await tickets
            .OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
            .Select(t => new LinkableTicket(t.Id, t.Subject, t.Status, t.Priority))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    // Applied first, in SQL: an Agent never sees another Agent's task.
    private static IQueryable<AgentTask> Scope(IQueryable<AgentTask> tasks, Guid? agentScope) =>
        agentScope is { } agentId ? tasks.Where(t => t.AssignedAgentUserId == agentId) : tasks;

    // Ordered here (EF cannot order by record members). The ticket is LEFT joined; its subject is returned only when the
    // viewer is SuperAdmin or the task owner is still the ticket's assignee. No customer columns are ever selected.
    private IQueryable<AgentTaskDetails> Project(IQueryable<AgentTask> tasks, bool viewerIsSuperAdmin, DateTimeOffset now)
    {
        var soon = now + AgentTaskRules.DueSoonWindow;
        return
            from t in tasks
            join owner in dbContext.Users on t.AssignedAgentUserId equals owner.Id
            join creator in dbContext.Users on t.CreatedByUserId equals creator.Id
            from tk in dbContext.Tickets.Where(x => x.Id == t.TicketId).DefaultIfEmpty()
            let accessible = tk != null && tk.AssignedAgentUserId == t.AssignedAgentUserId
            let pending = t.Status == AgentTaskStatus.Pending && !t.IsArchived
            orderby t.DueAtUtc == null ? 1 : 0, t.DueAtUtc, t.Id
            select new AgentTaskDetails(
                t.Id, t.Title, t.Description, t.Status,
                t.AssignedAgentUserId, owner.FirstName + " " + owner.LastName,
                t.CreatedByUserId, creator.FirstName + " " + creator.LastName,
                t.DueAtUtc, t.RemindAtUtc,
                dbContext.TaskReminderNotifications
                    .Where(n => n.TaskId == t.Id && n.ReminderGeneration == t.ReminderGeneration)
                    .Select(n => (DateTimeOffset?)n.IssuedAtUtc).FirstOrDefault(),
                t.TicketId == null ? LinkedTicketState.None : accessible ? LinkedTicketState.Accessible : LinkedTicketState.Inaccessible,
                viewerIsSuperAdmin || accessible ? t.TicketId : null,
                viewerIsSuperAdmin || accessible ? tk!.Subject : null,
                pending && t.DueAtUtc < now,
                pending && t.DueAtUtc >= now && t.DueAtUtc <= soon,
                t.CreatedAtUtc, t.UpdatedAtUtc, t.CompletedAtUtc, t.IsArchived, t.ArchivedAtUtc, t.Version);
    }
}
```

- If EF reports that the `let`/conditional projection cannot be translated, inline the `accessible` expression in each place it is used; **never** switch to client evaluation (`AsEnumerable`) — verify in Verification 7.
- If `GroupBy(_ => 1)` with filtered counts fails to translate, fall back to three `CountAsync` calls on the same predicate (same rule as Story 06) and note it in the new doc.

**Create file: `…/Repositories/TaskReminderRepository.cs`**

```csharp
internal sealed class TaskReminderRepository(CrmDbContext dbContext) : ITaskReminderRepository
{
    // One atomic, parameterized statement:
    //  - AgentTasks WITH (UPDLOCK, ROWLOCK): waits for an uncommitted reassignment / completion / archive of the same task and then
    //    evaluates the COMMITTED row. Required because CrmDev runs READ_COMMITTED_SNAPSHOT ON, where a plain read would use the
    //    old row version and could address a reminder to an owner who is losing the task.
    //  - NOT EXISTS … WITH (UPDLOCK, HOLDLOCK): serializes concurrent checks (two tabs, retries) on (TaskId, ReminderGeneration);
    //    UX_TaskReminderNotifications_TaskGeneration is the final guarantee.
    //  Retries (R2): ExecuteSqlInterpolatedAsync is NOT assumed to use the execution strategy, so it is wrapped explicitly. The
    //  statement is idempotent: a replay after a transient error (deadlock victim 1205, broken connection) — even if the first
    //  attempt's commit actually succeeded — inserts nothing twice; issuedNow may then report 0, the unread count stays correct.
    //  If every retry fails, the check returns 500 and the next 60-second check is the final retry.
    public Task<int> IssueDueAsync(Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            ct => dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO [TaskReminderNotifications] ([TaskId], [RecipientUserId], [ReminderGeneration], [ScheduledForUtc], [IssuedAtUtc])
            SELECT t.[Id], t.[AssignedAgentUserId], t.[ReminderGeneration], t.[RemindAtUtc], {now}
            FROM [AgentTasks] AS t WITH (UPDLOCK, ROWLOCK)
            WHERE t.[AssignedAgentUserId] = {agentUserId}
              AND t.[Status] = N'Pending'
              AND t.[IsArchived] = CAST(0 AS bit)
              AND t.[RemindAtUtc] IS NOT NULL
              AND t.[RemindAtUtc] <= {now}
              AND NOT EXISTS (
                  SELECT 1 FROM [TaskReminderNotifications] AS n WITH (UPDLOCK, HOLDLOCK)
                  WHERE n.[TaskId] = t.[Id] AND n.[ReminderGeneration] = t.[ReminderGeneration]);
            """, ct),
            cancellationToken);

    public Task<int> CountUnreadAsync(Guid agentUserId, CancellationToken cancellationToken = default) =>
        Owned(agentUserId).CountAsync(n => n.ReadAtUtc == null, cancellationToken);

    public async Task<PagedResult<TaskReminderDetails>> ListAsync(
        Guid agentUserId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Owned(agentUserId);
        if (unreadOnly) query = query.Where(n => n.ReadAtUtc == null);

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await (
                from n in query
                join t in dbContext.AgentTasks on n.TaskId equals t.Id
                orderby n.IssuedAtUtc descending, n.Id descending
                select new TaskReminderDetails(
                    n.Id, t.Id, t.Title, t.Status, t.IsArchived, n.ScheduledForUtc, n.IssuedAtUtc, n.ReadAtUtc, n.TransferredAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
        return new PagedResult<TaskReminderDetails>(items, page, pageSize, totalCount);
    }

    // Conditional UPDATE evaluated on the row at execution time. If a reassignment has already moved the row (uncommitted), this
    // statement waits on the row lock and then affects 0 rows; if the former owner marks it read first, the transfer skips it
    // (only unread rows move) and it stays as the former owner's read history.
    public async Task<MarkReadOutcome> MarkReadAsync(long id, Guid agentUserId, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // R2: explicit strategy around one idempotent autocommit statement (a replay after a lost acknowledgement affects 0 rows ⇒ AlreadyRead).
        var updated = await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(
            ct => Owned(agentUserId)
                .Where(n => n.Id == id && n.ReadAtUtc == null)
                .ExecuteUpdateAsync(set => set.SetProperty(n => n.ReadAtUtc, now), ct),
            cancellationToken);
        if (updated == 1)
        {
            return MarkReadOutcome.MarkedRead;
        }

        return await Owned(agentUserId).AnyAsync(n => n.Id == id, cancellationToken) ? MarkReadOutcome.AlreadyRead : MarkReadOutcome.NotFound;
    }

    // F2: same rows, never new ones, so (TaskId, ReminderGeneration) stays unique. SQL Server evaluates every SET expression on the
    // pre-update row, so TransferredFromUserId receives the OLD recipient. Runs after the task UPDATE in the reassignment transaction.
    public Task<int> TransferUnreadAsync(Guid taskId, Guid toUserId, DateTimeOffset now, CancellationToken cancellationToken = default) =>
        dbContext.TaskReminderNotifications
            .Where(n => n.TaskId == taskId && n.ReadAtUtc == null && n.RecipientUserId != toUserId)
            .ExecuteUpdateAsync(set => set
                .SetProperty(n => n.TransferredFromUserId, n => n.RecipientUserId)
                .SetProperty(n => n.TransferredAtUtc, now)
                .SetProperty(n => n.RecipientUserId, toUserId), cancellationToken);

    // Recipient AND current owner: a reassigned task's read history disappears for the former owner immediately.
    private IQueryable<TaskReminderNotification> Owned(Guid agentUserId) =>
        dbContext.TaskReminderNotifications.AsNoTracking()
            .Where(n => n.RecipientUserId == agentUserId
                        && dbContext.AgentTasks.Any(t => t.Id == n.TaskId && t.AssignedAgentUserId == agentUserId));
}
```

- `ExecuteUpdateAsync` runs immediately and joins the current transaction (`ReassignAgentTaskCommand`); outside a transaction (`MarkReadAsync`) it is a single atomic statement. Neither uses the change tracker. Only the two autocommit statements (`IssueDueAsync`, `MarkReadAsync`) create an execution strategy (R2); `TransferUnreadAsync` never does — inside a strategy-run attempt EF executes a nested strategy without retrying, and the transfer must be retried only as part of the whole reassignment attempt (R1).
- If `ExecuteUpdateAsync` rejects `AsNoTracking()` in `Owned` for the mark-read update, build the same predicate on `dbContext.TaskReminderNotifications` directly — keep the recipient + current-owner conditions.

> After generating the migration, confirm the column names in the raw SQL (`TaskId`, `RecipientUserId`, `ReminderGeneration`, `ScheduledForUtc`, `IssuedAtUtc`, `AssignedAgentUserId`, `Status`, `IsArchived`, `RemindAtUtc`, and for `TicketAccessLock` `Tickets.AssignedAgentUserId`, `TicketCollaborators.TicketId/AgentUserId/RemovedAtUtc`) match the generated `CreateTable` calls exactly.

**Create file: `…/Repositories/QuickReplyRepository.cs`** — `Visible(IQueryable<QuickReplyTemplate>, StaffActor)` implements the predicate in task 3; `ListAsync` applies `Visible` → `IsArchived == filter.Archived` → optional `Category`, `Scope` → search `t.Title.Contains(s) || t.Body.Contains(s)` → `CountAsync` → projection (join `dbContext.Users` for creator/updater names; `CanEdit = actor.IsSuperAdmin ? t.Scope == QuickReplyScope.Shared : t.Scope == QuickReplyScope.Personal && t.OwnerUserId == actor.UserId`) with `orderby t.Title, t.Id` before `select`, then `Skip/Take`.

**Create file: `…/Repositories/CollaborationRepository.cs`**

```csharp
internal sealed class CollaborationRepository(CrmDbContext dbContext) : ICollaborationRepository
{
    // THE collaboration access rule, evaluated from current rows in every statement.
    // Does NOT use or change TicketRepository.Scope: collaborators never become ticket assignees.
    private IQueryable<Ticket> Accessible(StaffActor actor) =>
        actor.IsSuperAdmin
            ? dbContext.Tickets.AsNoTracking()
            : dbContext.Tickets.AsNoTracking().Where(t =>
                t.AssignedAgentUserId == actor.UserId
                || (t.AssignedAgentUserId != null
                    && dbContext.TicketCollaborators.Any(c =>
                        c.TicketId == t.Id && c.AgentUserId == actor.UserId && c.RemovedAtUtc == null)));

    public Task<CollaborationTicketContext?> GetContextAsync(Guid ticketId, StaffActor actor, CancellationToken cancellationToken = default) =>
        (from t in Accessible(actor).Where(t => t.Id == ticketId)
         from agent in dbContext.Users.Where(u => u.Id == t.AssignedAgentUserId).DefaultIfEmpty()
         select new CollaborationTicketContext(
             t.Id, t.Subject, t.Status, t.Priority, t.AssignedAgentUserId,
             agent == null ? null : agent.FirstName + " " + agent.LastName,
             actor.IsSuperAdmin ? CollaborationAccess.SuperAdmin
             : t.AssignedAgentUserId == actor.UserId ? CollaborationAccess.PrimaryAgent
             : CollaborationAccess.Collaborator))
        .SingleOrDefaultAsync(cancellationToken);
```

- `ListMessagesAsync`: `var visible = Accessible(actor).Where(t => t.Id == ticketId); if (!await visible.AnyAsync()) return null;` then `from m in TicketInternalMessages join t in visible on m.TicketId equals t.Id` for both `CountAsync` and the page (`join author in dbContext.Users`, `orderby m.CreatedAtUtc descending, m.Id descending`) — same structure as `TicketRepository.ListActivityAsync` lines 103–141.
- `GetMessageAsync(id)`: projection by id with author name (`SingleAsync`).
- `ListCollaboratorsAsync(ticketId)`: active rows join `Users` (agent + addedBy), `IsActive` from `ApplicationUser.IsActive`, `orderby agent.LastName, agent.FirstName`.
- `GetActiveMembershipAsync`: tracked `SingleOrDefaultAsync(c => c.TicketId == ticketId && c.AgentUserId == agentUserId && c.RemovedAtUtc == null)`.
- `ListMyCollaborationsAsync(agentUserId, …)`: `from c in TicketCollaborators where c.AgentUserId == agentUserId && c.RemovedAtUtc == null join t in Tickets` ⇒ `IsSuspended = t.AssignedAgentUserId == null`, `IsPrimaryAssignee = t.AssignedAgentUserId == agentUserId`, `LastMessageAtUtc = TicketInternalMessages.Where(m => m.TicketId == t.Id).Max(m => (DateTimeOffset?)m.CreatedAtUtc)`; `orderby c.AddedAtUtc descending, c.Id descending`; count + page.

**Create file: `…/Repositories/TicketAccessLock.cs`** (F3)

```csharp
internal sealed class TicketAccessLock(CrmDbContext dbContext) : ITicketAccessLock
{
    // UPDLOCK: also under READ_COMMITTED_SNAPSHOT, reads the latest COMMITTED row and blocks writers until commit.
    // HOLDLOCK: keeps the lock (and the key range when no row matches) to the end of the transaction. Reads only.
    public async Task<TicketAssignmentLock?> LockTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        var rows = await dbContext.Database.SqlQuery<TicketAssignmentLock>($"""
            SELECT [Id] AS [TicketId], [AssignedAgentUserId]
            FROM [Tickets] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
            WHERE [Id] = {ticketId}
            """).ToListAsync(cancellationToken); // materialize as written: no EF subquery wrapping around the hinted statement
        return rows.SingleOrDefault();
    }

    public async Task<bool> LockActiveMembershipAsync(Guid ticketId, Guid agentUserId, CancellationToken cancellationToken = default)
    {
        EnsureTransaction();
        var rows = await dbContext.Database.SqlQuery<int>($"""
            SELECT 1 AS [Value]
            FROM [TicketCollaborators] WITH (UPDLOCK, HOLDLOCK, ROWLOCK, INDEX([UX_TicketCollaborators_ActiveMembership]))
            WHERE [TicketId] = {ticketId} AND [AgentUserId] = {agentUserId} AND [RemovedAtUtc] IS NULL
            """).ToListAsync(cancellationToken);
        return rows.Count > 0;
    }

    private void EnsureTransaction()
    {
        if (dbContext.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("ITicketAccessLock must be used inside an ITransactionalCommand.");
        }
    }
}
```

- If EF cannot materialize the positional record `TicketAssignmentLock` from `SqlQuery`, add a private Infrastructure row class (`public Guid TicketId { get; init; } public Guid? AssignedAgentUserId { get; init; }`) and map it to the record — keep the hinted SQL unchanged.
- The `INDEX(...)` hint pins the key-range lock to the filtered unique index (the key an invite inserts). If SQL Server rejects the hint, drop only `INDEX(...)`: `UPDLOCK, HOLDLOCK` still lock the scanned range, and `UX_TicketCollaborators_ActiveMembership` + `KnownUniqueIndexes` remain the backstop (409).
- The writers these locks wait for are the existing ones (`ChangeTicketAssigneeCommand` ⇒ `UPDATE Tickets`; `RemoveCollaboratorCommand` ⇒ `UPDATE TicketCollaborators`). No ticket code changes.

**File: `backend/src/Crm.Infrastructure/DependencyInjection.cs`** — after line 41 add:

```csharp
        services.AddScoped<IAgentTaskRepository, AgentTaskRepository>();
        services.AddScoped<ITaskReminderRepository, TaskReminderRepository>();
        services.AddScoped<IQuickReplyRepository, QuickReplyRepository>();
        services.AddScoped<ICollaborationRepository, CollaborationRepository>();
        services.AddScoped<ITicketAccessLock, TicketAccessLock>();
```

### 6 — API: error mapping, contracts, controllers

**File: `backend/src/Crm.Api/Errors/GlobalExceptionHandler.cs`** — add an arm after `NotFoundException` (lines 30–35):

```csharp
            ForbiddenException forbidden => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden.",
                Detail = forbidden.Message,
            },
            PreconditionRequiredException precondition => new ProblemDetails
            {
                Status = StatusCodes.Status428PreconditionRequired,
                Title = "Precondition required.",
                Detail = precondition.Message,
            },
            CommitOutcomeUnknownException unknown => new ProblemDetails // R1: ≥ 500 ⇒ logged with the inner exception (line 62)
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Outcome unknown.",
                Detail = unknown.Message,
            },
```

**Create file: `backend/src/Crm.Api/Http/EntityTags.cs`** (F1 — HTTP concern, so it lives in the API layer; controllers stay thin):

```csharp
using Crm.Application.Common.Exceptions;
using FluentValidation;
using FluentValidation.Results;

namespace Crm.Api.Http;

/// <summary>Strong ETag = the quoted base64 rowversion. Only the exact single value the API returned is accepted.</summary>
internal static class EntityTags
{
    private const int RowVersionLength = 8; // SQL Server rowversion is always 8 bytes

    public static string Format(byte[] version) => $"\"{Convert.ToBase64String(version)}\"";

    /// <summary>
    /// Missing ⇒ 428. '*', weak (W/…), several values, non-base64 or not exactly 8 decoded bytes ⇒ 400 keyed "ifMatch".
    /// A well-formed 8-byte value that is not the current version passes here; the handler then returns 409 (stale).
    /// </summary>
    public static byte[] RequireIfMatch(HttpRequest request)
    {
        var header = request.Headers.IfMatch;
        var raw = header.ToString().Trim();
        if (header.Count == 0 || raw.Length == 0)
        {
            throw new PreconditionRequiredException("Send If-Match with the task version you last loaded.");
        }

        if (header.Count == 1 && raw.Length > 2 && raw[0] == '"' && raw[^1] == '"' && !raw.Contains(','))
        {
            try
            {
                var version = Convert.FromBase64String(raw[1..^1]);
                if (version.Length == RowVersionLength)
                {
                    return version;
                }
            }
            catch (FormatException)
            {
                // fall through to 400
            }
        }

        throw new ValidationException([new ValidationFailure("IfMatch", "If-Match must be the quoted version returned by the API.")]);
    }
}
```

**Contracts** (create under `backend/src/Crm.Api/Contracts/<Feature>/`, records with static `From(...)` exactly like `TicketResponse.cs`):

| File | Records |
|---|---|
| `AgentTasks/AgentTaskRequests.cs` | `CreateAgentTaskRequest(string Title, string? Description, Guid? AssignedAgentUserId, Guid? TicketId, DateTimeOffset? DueAtUtc, DateTimeOffset? RemindAtUtc)` (comment: assignedAgentUserId SuperAdmin-only, Agents omit); `UpdateAgentTaskRequest(string Title, string? Description, DateTimeOffset? DueAtUtc)`; `SetTaskTicketLinkRequest(Guid TicketId)`; `ScheduleTaskReminderRequest(DateTimeOffset RemindAtUtc)` (non-nullable — see note below); `ReassignAgentTaskRequest(Guid? AssignedAgentUserId, bool RemoveTicketLink)`. |
| `AgentTasks/AgentTaskResponse.cs` | `AgentTaskResponse` mirroring `AgentTaskDetails` field-for-field, with `Version` as `string` (`Convert.ToBase64String`) — the same value as the `ETag` header without quotes; `AgentTaskSummaryResponse(int Pending, int Overdue, int DueSoon, IReadOnlyList<AgentTaskResponse> NextTasks, int PreviewLimit, DateTimeOffset GeneratedAtUtc)`; `LinkableTicketResponse(Guid Id, string Subject, TicketStatus Status, TicketPriority Priority)`. |
| `Notifications/TaskReminderResponses.cs` | `TaskReminderResponse(long Id, Guid TaskId, string TaskTitle, AgentTaskStatus TaskStatus, bool TaskIsArchived, DateTimeOffset ScheduledForUtc, DateTimeOffset IssuedAtUtc, DateTimeOffset? ReadAtUtc, bool IsRead, bool WasMissed, DateTimeOffset? TransferredAtUtc)` — `From` computes `IsRead = ReadAtUtc != null`, `WasMissed = IssuedAtUtc > ScheduledForUtc + TaskReminderRules.MissedThreshold` (delayed issuance, F6); `TaskReminderCheckResponse(int UnreadCount, int IssuedNow, DateTimeOffset CheckedAtUtc)`. No recipient / transferred-from user ids are exposed. |
| `QuickReplies/QuickReplyContracts.cs` | `CreateQuickReplyRequest(string Title, string Body, TicketCategory? Category)` (**no scope/owner**); `UpdateQuickReplyRequest(string Title, string Body, TicketCategory? Category)`; `QuickReplyResponse` mirroring `QuickReplyDetails`. |
| `Collaboration/CollaborationContracts.cs` | `CollaborationTicketResponse(Guid TicketId, string Subject, TicketStatus Status, TicketPriority Priority, string? AssignedAgentName, CollaborationAccess Access, bool CanManage)` (no assignee id needed); `TicketMessageResponse(long Id, Guid AuthorUserId, string AuthorName, string Body, DateTimeOffset CreatedAtUtc)`; `PostTicketMessageRequest(string Body)`; `TicketCollaboratorResponse(Guid AgentUserId, string AgentName, bool IsActive, string AddedByName, DateTimeOffset AddedAtUtc)`; `InviteCollaboratorRequest(Guid AgentUserId)`; `CollaboratorCandidateResponse(Guid AgentUserId, string AgentName)`; `MyCollaborationResponse` mirroring `MyCollaborationDetails`. |

> **Reminder request:** `PUT /tasks/{id}/reminder` sends `new ScheduleTaskReminderCommand(Actor, id, EntityTags.RequireIfMatch(Request), request.RemindAtUtc)`; `DELETE /tasks/{id}/reminder` sends `new ScheduleTaskReminderCommand(Actor, id, EntityTags.RequireIfMatch(Request), null)` (cancel). Because `RemindAtUtc` is non-nullable in the request, a missing value in the PUT body fails `[ApiController]` model validation (400).

**Create file: `backend/src/Crm.Api/Controllers/AgentTasksController.cs`** — `[ApiController] [Route(ApiRoutes.V1 + "/tasks")] [Authorize(Policy = AuthorizationPolicies.Staff)] public sealed class AgentTasksController(ISender sender) : ControllerBase`, `private StaffActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));`

F1 protocol in this controller: every action that returns one task sets `Response.Headers.ETag = EntityTags.Format(details.Version)` (private helper `TaskResult(AgentTaskDetails details)` ⇒ sets the header, returns `AgentTaskResponse.From(details)`); every mutation passes `EntityTags.RequireIfMatch(Request)` as the command's `ExpectedVersion`. Mutation descriptions end with "Requires If-Match: the task's ETag (428 if missing, 409 if stale)."

| Verb / route | Command | Result | `EndpointDescription` (start with "Requires role: …") |
|---|---|---|---|
| `GET` | `ListAgentTasksQuery(Actor, search, status, dueState, archived = false, hasReminder, assignedAgentUserId, page = 1, pageSize = 20)` (all `[FromQuery]`) | `PagedResponse<AgentTaskResponse>` | SuperAdmin (all tasks; optional assignedAgentUserId) or Agent (own tasks only; filters only narrow). Default unarchived; order: no-due-date last, due ascending, id. |
| `GET {id:guid}` | `GetAgentTaskQuery` | `AgentTaskResponse` + `ETag` | Agent: own only, others 404. |
| `GET linkable-tickets` | `ListLinkableTicketsQuery(Actor, [FromQuery] ownerUserId, [FromQuery] search)` | `IReadOnlyList<LinkableTicketResponse>` | Picker data only (≤ 10 tickets currently assigned to the owner). Agent: owner is the caller (another ownerUserId ⇒ 400). SuperAdmin: ownerUserId required. Linking is re-validated on write. |
| `POST` | `CreateAgentTaskCommand` | `201` `CreatedAtAction(nameof(Get))` + `ETag` (no `If-Match`) | Agent: owner = caller, omit assignedAgentUserId. SuperAdmin: assignedAgentUserId required (active Agent). Optional ticketId must be assigned to the owner. Optional remindAtUtc future and ≤ dueAtUtc. |
| `PUT {id:guid}` | `UpdateAgentTaskCommand` | `200` | Title, description, due only. |
| `PUT {id:guid}/ticket-link` / `DELETE {id:guid}/ticket-link` | `SetTaskTicketLinkCommand(…, request.TicketId)` / `(…, null)` | `200` | |
| `PUT {id:guid}/reminder` / `DELETE {id:guid}/reminder` | `ScheduleTaskReminderCommand` | `200` | One reminder per task, delivered in-app to the task's current owner only. |
| `POST {id:guid}/completion` / `DELETE {id:guid}/completion` | `SetTaskCompletionCommand(…, true/false)` | `200` | Complete cancels a pending reminder; reopen does not recreate it. |
| `POST {id:guid}/archive` / `DELETE {id:guid}/archive` | `SetTaskArchivedCommand(…, true/false)` | `200` | No hard delete. |
| `PUT {id:guid}/assignee` + `[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]` | `ReassignAgentTaskCommand(…, request.AssignedAgentUserId, request.RemoveTicketLink)` | `200` | SuperAdmin. 409 when the linked ticket is not assigned to the new owner and removeTicketLink is false. |

**Create file: `backend/src/Crm.Api/Controllers/NotificationsController.cs`** — route `ApiRoutes.V1 + "/notifications"`, `[Authorize(Policy = AuthorizationPolicies.Agent)]`:
- `POST check` ⇒ `CheckTaskRemindersCommand(User.GetUserId())` ⇒ `200 TaskReminderCheckResponse`. Description: "Requires role: Agent. Issues any due, not-yet-issued task reminders for the caller (idempotent) and returns the unread count. Called on app open and about every 60 seconds by the Agent UI; not a push channel."
- `GET` (`[FromQuery] bool unreadOnly = false, page, pageSize`) ⇒ `PagedResponse<TaskReminderResponse>`.
- `PUT {id:long}/read` ⇒ `MarkTaskReminderReadCommand` ⇒ `204`.

**Create file: `backend/src/Crm.Api/Controllers/QuickRepliesController.cs`** — route `"/quick-replies"`, Staff: `GET` (search, category, scope, archived, page, pageSize), `GET {id:guid}`, `POST` (201), `PUT {id:guid}`, `POST {id:guid}/archive`, `DELETE {id:guid}/archive`. Descriptions state: "Agent: Shared (read/copy) + own Personal; SuperAdmin: Shared only. Scope is derived from the caller's role. Copying is a client action; nothing is sent."

**Create file: `backend/src/Crm.Api/Controllers/CollaborationsController.cs`** — route `"/collaborations"`, Staff:
- `GET` + `[Authorize(Policy = AuthorizationPolicies.Agent)]` ⇒ `ListMyCollaborationsQuery(User.GetUserId(), page, pageSize)`.
- `GET {ticketId:guid}` ⇒ context. `GET {ticketId:guid}/messages` (page, pageSize). `POST {ticketId:guid}/messages` ⇒ `201` with `CreatedAtAction(nameof(Messages), new { ticketId }, …)`.
- `GET {ticketId:guid}/collaborators`; `GET {ticketId:guid}/eligible-agents`; `POST {ticketId:guid}/collaborators` ⇒ `201`; `DELETE {ticketId:guid}/collaborators/{agentUserId:guid}` ⇒ `204`.
- Description on every action: "Internal staff discussion — never visible to customers. Access: SuperAdmin; the ticket's current assigned Agent; or an invited active collaborator while the ticket is assigned. Does not grant /api/v1/tickets access."

**File: `backend/src/Crm.Api/Controllers/AgentDashboardController.cs`** — after line 18 add (existing `Get` unchanged):

```csharp
    [EndpointDescription("Requires role: Agent (SuperAdmin and Customer: 403). Caller's Pending, unarchived tasks: pending, overdue (due < now) and dueSoon (now ≤ due ≤ now + 7 days) counts over ALL such tasks, plus the next 5 (no-due-date last, due ascending, id). Server UTC. Separate from the ticket snapshot so either can fail independently.")]
    [HttpGet("tasks")]
    public async Task<ActionResult<AgentTaskSummaryResponse>> Tasks(CancellationToken cancellationToken) =>
        Ok(AgentTaskSummaryResponse.From(await sender.Send(new GetAgentTaskSummaryQuery(User.GetUserId()), cancellationToken)));
```

### 7 — Migration

From `backend/`:

```powershell
dotnet ef migrations add AddAgentProductivityAndCollaboration --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations -- --environment Development
```

Review the generated `…_AddAgentProductivityAndCollaboration.cs` before applying — it must contain **only**: five `CreateTable` calls (`AgentTasks` with `Version rowversion`, `TaskReminderNotifications` and `TicketInternalMessages` with `bigint IDENTITY`, `QuickReplyTemplates` with the check constraint, `TicketCollaborators`), FKs with `ReferentialAction.Restrict` only, the indexes from task 5 (`UX_TaskReminderNotifications_TaskGeneration`, filtered `UX_TicketCollaborators_ActiveMembership … WHERE [RemovedAtUtc] IS NULL`, filtered `[RemindAtUtc] IS NOT NULL`), the `TransferredFromUserId` / `TransferredAtUtc` columns on `TaskReminderNotifications`, and the model snapshot update. **No** `AlterColumn`/`AddColumn`/`DropTable` on existing tables, no `Sql(...)` data statements. Apply with `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` (or `.\run-dev.ps1`). Never `EnsureCreated` / drop / recreate.

### 8 — Backend unit test (single happy path)

**Create file: `backend/tests/Crm.UnitTests/AgentTasks/CreateAgentTaskCommandHandlerTests.cs`**

```csharp
namespace Crm.UnitTests.AgentTasks;

public sealed class CreateAgentTaskCommandHandlerTests
{
    [Fact]
    public async Task Handle_AgentCreatesTicketLinkedTaskWithReminder_OwnsPendingTaskAndSchedulesFirstReminderInOneSave()
    {
        // Arrange: agentId; now = 2030-01-01 09:00Z; ticketId; FakeTicketAccessLock returns TicketAssignmentLock(ticketId, agentId)
        //   and records the ticket id it locked; due = now + 2 days; remindAt = now + 1 day.
        // Act: Handle(new CreateAgentTaskCommand(new StaffActor(agentId, false), "  Call back customer  ", "  ", null, ticketId, due, remindAt)).
        // Assert:
        //  - ticketLock.LockedTicketId == ticketId (the link is authorized from the LOCKED current assignment, owner == agentId);
        //  - the added AgentTask: Title "Call back customer" (trimmed), Description null (whitespace ⇒ null),
        //    AssignedAgentUserId == CreatedByUserId == agentId, Status Pending, TicketId == ticketId,
        //    DueAtUtc == due, RemindAtUtc == remindAt, ReminderGeneration == 1, CreatedAtUtc == UpdatedAtUtc == now,
        //    CompletedAtUtc null, IsArchived false;
        //  - unitOfWork.SaveCount == 1 (explicit save; ExecuteInTransactionAsync is not called — the pipeline owns the transaction);
        //  - identity was never called (Agents never pick an owner) — FakeIdentityService throws on every member;
        //  - result is the read-back for that task id under the same actor (repository recorded actor + now) and carries the
        //    repository's Version bytes unchanged.
    }

    // private sealed class FakeAgentTaskRepository : IAgentTaskRepository — records Added task, FindDetails actor/now; returns
    //   AgentTaskDetails built from the added task (LinkedTicketState.Accessible, "Ticket subject", Version [0,0,0,0,0,0,0,1]);
    //   other members NotSupported.
    // private sealed class FakeTicketAccessLock : ITicketAccessLock — LockTicketAsync records the id and returns the snapshot;
    //   LockActiveMembershipAsync NotSupported.
    // private sealed class FakeIdentityService : IIdentityService — every member throws NotSupportedException.
    // private sealed class FakeUnitOfWork : IUnitOfWork — copy TransitionTicketStatusCommandHandlerTests lines 79–85.
    // private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider — copy lines 87–90.
}
```

Write the test body and fakes in full (the comments are the required assertions). Hand-written fakes only; **do not modify** any existing test file. This single test covers the central task path: actor-derived ownership, lock-based ticket-link authorization against the owner, reminder generation, trimming, single save. The handler is invoked directly, so `TransactionBehavior` is not involved; the transaction/lock behaviour itself is verified manually (Verification 5, 8, 11).

---

## Frontend Tasks

Common rules: standalone components, `ChangeDetectionStrategy.OnPush`, signals, `takeUntilDestroyed`, `Subject` + `switchMap` + per-request `catchError` for loads (`agent-dashboard-page.ts` lines 47–75), `describeHttpError` for messages, `MatSnackBar` for success, `PageHeader`, `StatusBadge`, `ApiUrl.versioned`, logical CSS properties only, `white-space: pre-wrap; overflow-wrap: anywhere` for user text, text always rendered with `{{ }}` interpolation (never `[innerHTML]`). On a 404 after load, drop the record and show "This … is no longer available to you." (pattern `ticket-detail-page.ts` lines 219–229).

### 9 — Tasks feature (`frontend/src/app/features/tasks/`)

**Create file: `task-models.ts`**

```ts
import { TicketPriority, TicketStatus } from '../tickets/ticket-models';

export const TASK_STATUSES = ['Pending', 'Completed'] as const;
export type AgentTaskStatus = (typeof TASK_STATUSES)[number];
export type TaskDueFilter = 'Overdue' | 'DueSoon' | 'NoDueDate';
export type LinkedTicketState = 'None' | 'Accessible' | 'Inaccessible';

/** Matches Crm.Api AgentTaskResponse. linkedTicketId/Subject are null for an Agent when the link is Inaccessible. */
export interface AgentTask {
  id: string;
  title: string;
  description: string | null;
  status: AgentTaskStatus;
  assignedAgentUserId: string;
  assignedAgentName: string;
  createdByUserId: string;
  createdByName: string;
  dueAtUtc: string | null;
  remindAtUtc: string | null;
  reminderSentAtUtc: string | null;
  linkedTicketState: LinkedTicketState;
  linkedTicketId: string | null;
  linkedTicketSubject: string | null;
  isOverdue: boolean;
  isDueSoon: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
  completedAtUtc: string | null;
  isArchived: boolean;
  archivedAtUtc: string | null;
  /** Optimistic-concurrency token (base64 rowversion). Sent back as If-Match: "<version>" on every mutation. */
  version: string;
}

/** Matches Crm.Api LinkableTicketResponse (picker only). */
export interface LinkableTicket {
  id: string;
  subject: string;
  status: TicketStatus;
  priority: TicketPriority;
}

export interface TaskListParams {
  page: number;
  pageSize: number;
  search: string | null;
  status: AgentTaskStatus | null;
  dueState: TaskDueFilter | null;
  archived: boolean;
  hasReminder: boolean | null;
  assignedAgentUserId: string | null; // SuperAdmin filter
}

/** assignedAgentUserId: SuperAdmin only (required there); Agents send null. */
export interface CreateTaskRequest {
  title: string;
  description: string | null;
  assignedAgentUserId: string | null;
  ticketId: string | null;
  dueAtUtc: string | null;
  remindAtUtc: string | null;
}

export interface UpdateTaskRequest { title: string; description: string | null; dueAtUtc: string | null; }

/** Matches Crm.Api AgentTaskSummaryResponse. */
export interface AgentTaskSummary {
  pending: number;
  overdue: number;
  dueSoon: number;
  nextTasks: AgentTask[];
  previewLimit: number;
  generatedAtUtc: string;
}

/** <input type="datetime-local"> value (viewer's local zone) ⇒ ISO UTC; '' ⇒ null. */
export function fromLocalInput(value: string): string | null {
  return value ? new Date(value).toISOString() : null;
}

/** ISO UTC ⇒ 'yyyy-MM-ddTHH:mm' in the viewer's local zone for datetime-local inputs; null ⇒ ''. */
export function toLocalInput(iso: string | null): string {
  if (!iso) return '';
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
```

**Create file: `tasks-api.ts`** — `TasksApi` (`providedIn: 'root'`): `list(params)` (omit null/empty filters exactly like `tickets-api.ts` lines 23–45; always send `page`, `pageSize`, `archived`), `get(id)`, `create(body)`, `linkableTickets(ownerUserId: string | null, search: string)` (`GET tasks/linkable-tickets`, omit null `ownerUserId` / empty search), `summary()` (`GET agent-dashboard/tasks`). **Every mutation takes the displayed task's `version` and sends header `If-Match: "<version>"`** (private helper `ifMatch(version)` ⇒ `{ headers: { 'If-Match': '"' + version + '"' } }`): `update(id, version, body)`, `setTicketLink(id, version, ticketId)` (`PUT tasks/{id}/ticket-link`), `removeTicketLink(id, version)` (`DELETE`), `scheduleReminder(id, version, remindAtUtc)` (`PUT tasks/{id}/reminder`), `cancelReminder(id, version)`, `complete(id, version)` (`POST tasks/{id}/completion`), `reopen(id, version)` (`DELETE`), `archive(id, version)` (`POST tasks/{id}/archive`), `restore(id, version)` (`DELETE`), `reassign(id, version, assignedAgentUserId, removeTicketLink)` (`PUT tasks/{id}/assignee`). All return `Observable<AgentTask>` (body includes the new `version`) except list (`PagedResponse<AgentTask>`), `linkableTickets` (`LinkableTicket[]`) and summary. The ETag header is not read; the body `version` is the same value.

**Create file: `ticket-picker.ts`** (F5) — copy `features/tickets/customer-picker.ts` structure (`MatAutocomplete`, debounced `switchMap`, lookup error hint) but query `TasksApi.linkableTickets(ownerUserId(), search)`. Inputs: `ownerUserId = input<string | null>(null)`, `requireOwner = input(false)` (SuperAdmin forms). When `requireOwner() && !ownerUserId()` the input is disabled with hint "Select the task owner first."; an `effect` on `ownerUserId` clears the query and options and emits `selected(null)` whenever the owner changes. Option text `{{ t.subject }} · {{ statusLabel(t.status) }} · {{ t.priority }}`; hint "Only tickets currently assigned to the task owner are listed." Output `selected: LinkableTicket | null`. The server still re-checks under a lock on save (a ticket reassigned meanwhile ⇒ 400 shown inline).

**Create file: `task-list-page.ts|html|scss`** (route `''`):
- Header: `title` = "My Tasks" for Agent, "Tasks" for SuperAdmin; subtitle "Personal work items and optional in-app reminders."
- Filters (`FormGroup`, debounced 300 ms like the ticket list): search (title), status select (Pending default / Completed / Any), due state (Any / Overdue / Due soon / No due date), reminder (Any / With reminder / Without), "Show archived" `mat-slide-toggle`; SuperAdmin only: Agent select from `AgentsApi.list()` (active + inactive, label "(inactive)").
- `<a mat-flat-button routerLink="/tasks/new"><mat-icon>add</mat-icon> New task</a>`.
- List: semantic `<ul>` of rows (not `mat-table`): title link `/tasks/:id`, badges — `Overdue` (`error`), `Due soon` (`pending`), `Completed` (`success`), `Archived` (`neutral`), reminder chip `Reminder {{ remindAtUtc | date: 'short' }}` or `Reminder sent`, due `Due {{ dueAtUtc | date: 'medium' }}`, owner name (SuperAdmin view: "Owner: …"), linked ticket: Accessible ⇒ link to `/tickets/:linkedTicketId` with subject; Inaccessible ⇒ text "Linked ticket no longer accessible" (SuperAdmin: subject + that warning).
- `MatPaginator` (`pageIndex = page - 1`), loading bar, error + Retry, empty "No tasks match these filters."

**Create file: `task-edit-page.ts|html|scss`** (routes `new` and `:id`):
- Form: title (required, ≤ 200), description (≤ 2000, textarea), due `<input matInput type="datetime-local">`, SuperAdmin-only owner `mat-select` (active Agents from `AgentsApi.list()`, required on create), ticket link `<crm-ticket-picker [ownerUserId]="ownerId()" [requireOwner]="isSuperAdmin()" />` where `ownerId()` = the selected owner (SuperAdmin) or the current user (Agent) on create, and the task's `assignedAgentUserId` on edit (a separate "Linked ticket" card with Change / Remove buttons calling `setTicketLink` / `removeTicketLink`), **Remind me** `mat-slide-toggle` + reminder `datetime-local` (required when toggle on).
- Hint under reminder: "Delivered in this app to {{ owner }} only — no email, SMS or push. Times use your local time zone."
- Create: one `create` call with `remindAtUtc` (null when toggle off). Edit page cards: **Details** (`update`), **Reminder** (Save ⇒ `scheduleReminder`, Cancel reminder ⇒ `cancelReminder`; shows "Scheduled for …", "Sent …", or "No reminder"), **Status** (Complete / Reopen / Archive / Restore buttons by state), **Owner** (SuperAdmin only: Agent select + "Remove ticket link" checkbox ⇒ `reassign`; 409 message shown inline), **Linked ticket** (state-aware text as in the list).
- Display "Owner" vs "Linked ticket assignee" distinctly: label owner "Task owner"; never show ticket assignee/customer.
- **Concurrency flow (F1):** `task = signal<AgentTask | null>`; every card action passes `task()!.version`; each successful response replaces `task` (new version), so the next action automatically uses it. Disable all task actions while one is in flight (`busy` signal, as `ticket-detail-page.ts` line 83) so one tab never races itself.
- **409** (stale `If-Match`, rowversion race, or state conflict): show the server message in the card that failed, call `reload()` ⇒ `get(id)` ⇒ `task.set(latest)`; reset a form **only if it is not dirty** (typed input is kept — pattern `ticket-detail-page.ts` lines 119–134 and 219–242); the user reviews and clicks again (sending the new version). No automatic retry/replay.
- **428** (missing `If-Match`): cannot happen from this UI; shown as a generic error via `describeHttpError` (no reload loop). **503** (commit outcome unknown, R1) ⇒ reload the task and show "Your change may have been saved. Check it before trying again."; keep typed input; never resubmit automatically. **404** ⇒ drop the task, "This task is no longer available to you." **400** ⇒ field message inline, no reload.

**Create file: `tasks.routes.ts`** — `TASKS_ROUTES`: `''` (title "Tasks | Support CRM"), `'new'` ("New task | Support CRM"), `':id'` ("Task | Support CRM"), all `loadComponent`.

### 10 — Reminders: bell + inbox

**Create file: `frontend/src/app/core/notifications/notification-models.ts`** — `TaskReminder` (matches `TaskReminderResponse`, incl. `isRead`, `wasMissed`, `transferredAtUtc`, `taskStatus`, `taskIsArchived`) and `TaskReminderCheck { unreadCount: number; issuedNow: number; checkedAtUtc: string }`.

**Create file: `frontend/src/app/core/notifications/notifications-api.ts`** — `check()` (`POST notifications/check`, empty body `{}`), `list(page, pageSize, unreadOnly)`, `markRead(id)` (`PUT notifications/{id}/read`).

**Create file: `frontend/src/app/core/notifications/notification-bell.ts`** (inline template):

```ts
/**
 * Agent-only in-app reminder indicator. The ONLY polling in the app: check on start/sign-in and every 60 s while an Agent
 * session exists (exhaustMap: never overlapping). The server issues each due reminder once; this just asks.
 * Failures never alert repeatedly: the badge hides and the next tick retries.
 */
export const REMINDER_POLL_MS = 60_000;
```

- `private readonly unread = signal<number | null>(null)`; stream: `toObservable(this.auth.role).pipe(switchMap(role => role === 'Agent' ? merge(timer(0, REMINDER_POLL_MS), this.manual) .pipe(exhaustMap(() => this.api.check().pipe(catchError(() => of(null))))) : of(null)), takeUntilDestroyed())`.
- On result: `unread.set(r?.unreadCount ?? null)`; when `r && r.issuedNow > 0` open snackbar `"You have {{n}} new task reminder(s)."` with action "View" ⇒ `router.navigate(['/notifications'])`.
- Template: `<a mat-icon-button routerLink="/notifications" [attr.aria-label]="label()"> <mat-icon [matBadge]="unread() || null" matBadgeColor="warn" [matBadgeHidden]="!unread()" aria-hidden="false">notifications</mat-icon></a>`; `label()` = "Task reminders, N unread" / "Task reminders".
- Expose `refreshNow()` (`this.manual.next()`) for the inbox page via a tiny root service `ReminderBadgeRefresh` (`Subject<void>` in `core/notifications/reminder-badge-refresh.ts`) so marking read updates the badge immediately.

**File: `frontend/src/app/core/layout/shell/shell.ts` / `shell.html`** — import `NotificationBell`; in `shell.html` before the user-name span (line 37) add `@if (isAgent()) { <crm-notification-bell /> }` with `protected readonly isAgent = computed(() => this.auth.role() === 'Agent');`.

**Create files: `frontend/src/app/features/notifications/notifications-page.ts|html|scss` + `notifications.routes.ts`** (`NOTIFICATIONS_ROUTES`, title "Notifications | Support CRM"):
- On load: `check()` then `list(1, 20, unreadOnly)`; toggle "Unread only"; Refresh button; Load more.
- Item: task title link `/tasks/:taskId`; badge "Missed reminder" (`pending`) when `wasMissed`, with hint "Delivered more than 5 minutes after its scheduled time." (F6 — never "you were offline"), else "Reminder"; "Scheduled {{ scheduledForUtc | date: 'medium' }} · Delivered {{ issuedAtUtc | date: 'medium' }}"; when `transferredAtUtc`: "Transferred to you with the task on {{ transferredAtUtc | date: 'medium' }}"; task-state badge `Completed`/`Archived` when applicable (never presented as pending work); unread items bold with **Mark as read** ⇒ `markRead` ⇒ `isRead = true`, `ReminderBadgeRefresh.next()`; a 404 on mark-read (task reassigned meanwhile) removes the item and shows "This reminder moved with its task to another Agent.".
- Text: "Reminders appear here while you use the CRM. Marking a reminder as read does not complete the task."
- Empty: "No reminders yet." Error + Retry.

### 11 — Quick Replies (`frontend/src/app/features/quick-replies/`)

- **`quick-reply-models.ts`**: `QuickReplyScope = 'Personal' | 'Shared'`; `QuickReply` (matches response incl. `canEdit`); `QuickReplyListParams`; `SaveQuickReplyRequest { title; body; category: TicketCategory }` (reuse `TicketCategory`, `TICKET_CATEGORIES` from `../tickets/ticket-models`).
- **`quick-replies-api.ts`**: `list`, `get`, `create`, `update`, `archive` (`POST quick-replies/{id}/archive`), `restore` (`DELETE`).
- **`copy-quick-reply.ts`**: `export function copyQuickReply(clipboard: Clipboard, snackBar: MatSnackBar, body: string): void` — `clipboard.copy(body)` (`@angular/cdk/clipboard`) ⇒ snackbar "Copied to clipboard." or "Could not copy. Select the text and copy it manually." The copied string is exactly `body` (no trimming, no substitution).
- **`quick-reply-list-page`** (`''`): header "Quick Replies", banner hint **"Copies text only — does not send a message."**; filters search (title/body), category, scope (Agent only: Any / Shared / Personal), "Show archived"; New quick reply button (label "New shared quick reply" for SuperAdmin, "New personal quick reply" for Agent); rows with title, scope badge (`Shared` `pending`, `Personal` `neutral`), category, body preview (`.body` with `-webkit-line-clamp: 3`), **Copy** button, Edit link only when `canEdit`, Archive/Restore when `canEdit`. Paginator.
- **`quick-reply-edit-page`** (`new`, `:id`): title (≤ 120), category select, body textarea (≤ 2000, counter), read-only scope line "Scope: Shared (all Agents)" / "Scope: Personal (only you)"; Copy button; Save; Archive/Restore. When `!canEdit` (Agent opening Shared) show read-only view with Copy only.
- **`quick-reply-panel.ts|html`** (for Ticket Details): compact searchable list (`pageSize` 5, active only) with Copy buttons and the same hint; link "Manage quick replies" ⇒ `/quick-replies`.
- **`quick-replies.routes.ts`**: `QUICK_REPLIES_ROUTES` (`''`, `new`, `:id`).

### 12 — Collaboration (`frontend/src/app/features/collaboration/`)

- **`collaboration-models.ts`**: `CollaborationTicket`, `TicketMessage`, `TicketCollaborator`, `CollaboratorCandidate`, `MyCollaboration` (match the responses).
- **`collaboration-api.ts`**: `mine(page, pageSize)` (`GET collaborations`), `ticket(ticketId)`, `messages(ticketId, page, pageSize)`, `post(ticketId, body)`, `collaborators(ticketId)`, `candidates(ticketId)` (`GET collaborations/{id}/eligible-agents`), `invite(ticketId, agentUserId)`, `remove(ticketId, agentUserId)`.
- **`ticket-discussion.ts|html|scss`** — `input.required<string>() ticketId`. Prominent `<p class="internal-banner" role="note"><mat-icon>lock</mat-icon> Internal only — not visible to customers.</p>`. Composer: textarea (required, ≤ 2000, counter) + **Post** (disabled while posting); on success prepend the message, clear input, snackbar "Message posted." Thread: newest first, each `<article>` with author name, `{{ createdAtUtc | date: 'medium' }}`, body (`pre-wrap`); "Load older messages" with id-dedupe (copy `ticket-activity-list.ts` lines 55–124). States: loading, empty "No internal messages yet.", error + Retry; 404 ⇒ emit `accessLost` output; 503 on Post (R1) ⇒ reload page 1, keep the typed text and show "Your message may have been posted. Check the thread before posting again." (same for invite/remove in `ticket-collaborators`: reload the list).
- **`ticket-collaborators.ts|html`** — `input.required ticketId`, `input canManage`. Lists active collaborators (name, "(inactive)" when `!isActive`, "Invited by X on …"). When `canManage`: candidates `mat-select` (loaded from `candidates`) + **Invite**; per-row **Remove** (confirm via `window.confirm('Remove … from this discussion?')`). Hint: "Collaborators can read and post in this discussion only. They cannot open, edit or act on the ticket." 403/409 messages inline.
- **`my-collaborations-page`** (`''`): header "My Collaborations"; paged list: subject, status/priority badges, "Access suspended — ticket is unassigned" (`neutral`) when `isSuspended`, last message time; link `/collaboration/tickets/:id` (or `/tickets/:id` with label "You are now the assigned Agent" when `isPrimaryAssignee`). Empty: "You have not been invited to any ticket discussions."
- **`collaboration-ticket-page`** (`tickets/:id`): loads `ticket(id)`; shows **only** subject, status, priority, assigned Agent name and "You are a collaborator" note; `<crm-ticket-discussion>` and `<crm-ticket-collaborators [canManage]="t.canManage">`. **No** link to `/tickets/:id` unless `access === 'PrimaryAgent' || 'SuperAdmin'`. 404 ⇒ "This discussion is no longer available to you."
- **`collaboration.routes.ts`**: `COLLABORATION_ROUTES` (`''` "My Collaborations | Support CRM", `tickets/:id` "Ticket discussion | Support CRM").

### 13 — Ticket Details integration

**File: `frontend/src/app/features/tickets/ticket-detail-page.ts` / `.html`** — import `TicketDiscussion`, `TicketCollaborators`, `QuickReplyPanel`, `CollaborationApi`. After the Activity card (html lines 184–191, before the closing `</div>` of `.detail-stack` on line 192) add three cards, rendered inside the existing `@if (ticket(); as current)` (so only SuperAdmin / current assignee — the only users who can load the ticket — see them):

```html
    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>Internal discussion</mat-card-title></mat-card-header>
      <mat-card-content>
        <crm-ticket-discussion [ticketId]="current.id" (accessLost)="onAccessLost()" />
      </mat-card-content>
    </mat-card>

    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>Collaborators</mat-card-title></mat-card-header>
      <mat-card-content>
        <crm-ticket-collaborators [ticketId]="current.id" [canManage]="true" />
      </mat-card-content>
    </mat-card>

    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>Quick replies</mat-card-title></mat-card-header>
      <mat-card-content><crm-quick-reply-panel /></mat-card-content>
    </mat-card>
```

`onAccessLost()` reuses the existing 404 handling (`this.ticket.set(null); this.loadError.set('This ticket is no longer available to you. It may have been reassigned.')`). `canManage` is `true` here because ticket-detail viewers are exactly SuperAdmin or the current primary; the server re-checks every invite/remove. **No other change** to existing ticket cards, workflow, form or `TicketsApi`.

### 14 — Dashboard integration

**File: `frontend/src/app/features/agent-dashboard/agent-dashboard-page.ts`** — inject `TasksApi`; add a **second, independent** stream: `taskSummary = signal<AgentTaskSummary | null>(null)`, `tasksLoading`, `tasksError`, `private readonly taskRequests = new Subject<void>()` piped exactly like lines 48–66 (`catchError` sets `taskSummary(null)` + `tasksError(describeHttpError(error, 'Could not load your tasks.'))`). `refresh()` triggers both subjects (lines 71–75 + `this.taskRequests.next()`); add `refreshTasks()` for the section's Retry. The ticket stream, its signals and its error handling are **unchanged**.

**File: `…/agent-dashboard-page.html`** — in the toolbar (lines 3–12) add `<a mat-stroked-button routerLink="/tasks">My Tasks</a>`, `<a mat-stroked-button routerLink="/notifications">Notifications</a>`, `<a mat-stroked-button routerLink="/quick-replies">Quick Replies</a>`, `<a mat-stroked-button routerLink="/collaboration">My Collaborations</a>`. After line 94 (the closing `}` of the `@if (dashboard(); as d)` block — i.e. **outside** it so it renders even when the ticket snapshot failed) add `<section aria-labelledby="tasks-heading">`: `<h2 id="tasks-heading">My tasks</h2>`; its own progress bar / `role="alert"` error + "Retry tasks" button; when loaded: three cards **Pending**, **Overdue**, **Due soon** (`summary-grid` / `summary-card` classes reused), hint "Overdue and Due soon are part of Pending. Due soon = within 7 days.", then up to 5 tasks (`<ol class="queue">`, title link `/tasks/:id`, Overdue / Due soon badges, due time, "Reminder …" chip), empty text "No pending tasks.", and `<a routerLink="/tasks">View all my tasks</a>`. Never render zeros when `tasksError()` is set.

### 15 — Routes and navigation

**File: `frontend/src/app/app.routes.ts`** — insert after the `agents` entry (line 44), before the comment on line 45:

```ts
      {
        path: 'tasks',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/tasks/tasks.routes').then(m => m.TASKS_ROUTES),
      },
      {
        path: 'notifications',
        canActivate: [roleGuard('Agent')],
        loadChildren: () => import('./features/notifications/notifications.routes').then(m => m.NOTIFICATIONS_ROUTES),
      },
      {
        path: 'quick-replies',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/quick-replies/quick-replies.routes').then(m => m.QUICK_REPLIES_ROUTES),
      },
      {
        path: 'collaboration',
        canActivate: [roleGuard('Agent')],
        loadChildren: () => import('./features/collaboration/collaboration.routes').then(m => m.COLLABORATION_ROUTES),
      },
```

**File: `frontend/src/app/core/layout/navigation.ts`** — insert after the Customers entry (line 17):

```ts
  { label: 'Tasks', icon: 'task_alt', path: '/tasks', exact: false, roles: ['SuperAdmin', 'Agent'] },
  { label: 'Quick Replies', icon: 'quickreply', path: '/quick-replies', exact: false, roles: ['SuperAdmin', 'Agent'] },
  { label: 'My Collaborations', icon: 'forum', path: '/collaboration', exact: false, roles: ['Agent'] },
```

(Notifications are reached through the toolbar bell and the dashboard link.)

### 16 — Frontend unit test (single happy path)

**Create file: `frontend/src/app/features/tasks/tasks-api.spec.ts`** — setup copied from `tickets-api.spec.ts` lines 9–21.

```ts
describe('TasksApi', () => {
  it('requests the default My Tasks page (Pending, unarchived) omitting empty filters and returns typed tasks with link state', () => {
    // api.list({ page: 1, pageSize: 20, search: '  call ', status: 'Pending', dueState: null, archived: false,
    //            hasReminder: null, assignedAgentUserId: null }).subscribe(p => (result = p));
    // const request = http.expectOne(r => r.url === '/api/v1/tasks');
    // expect(request.request.method).toBe('GET');
    // expect(request.request.params.get('page')).toBe('1');
    // expect(request.request.params.get('pageSize')).toBe('20');
    // expect(request.request.params.get('search')).toBe('call');
    // expect(request.request.params.get('status')).toBe('Pending');
    // expect(request.request.params.get('archived')).toBe('false');
    // expect(request.request.params.has('dueState')).toBe(false);
    // expect(request.request.params.has('hasReminder')).toBe(false);
    // expect(request.request.params.has('assignedAgentUserId')).toBe(false); // ownership comes from the token
    // request.flush({ items: [ { id: 'k1', title: 'Call back customer', description: null, status: 'Pending',
    //   assignedAgentUserId: 'a1', assignedAgentName: 'Agent One', createdByUserId: 'a1', createdByName: 'Agent One',
    //   dueAtUtc: '2030-01-02T09:00:00Z', remindAtUtc: '2030-01-02T08:00:00Z', reminderSentAtUtc: null,
    //   linkedTicketState: 'Inaccessible', linkedTicketId: null, linkedTicketSubject: null, isOverdue: true, isDueSoon: false,
    //   createdAtUtc: '2030-01-01T09:00:00Z', updatedAtUtc: '2030-01-01T09:00:00Z', completedAtUtc: null,
    //   isArchived: false, archivedAtUtc: null, version: 'AAAAAAAAB9E=' } ], page: 1, pageSize: 20, totalCount: 1 });
    // expect(result?.totalCount).toBe(1);
    // expect(result?.items[0].linkedTicketState).toBe('Inaccessible');
    // expect(result?.items[0].linkedTicketId).toBeNull();
    // expect(result?.items[0].isOverdue).toBe(true);
    // expect(result?.items[0].version).toBe('AAAAAAAAB9E='); // the concurrency token later sent as If-Match
    // http.verify();
  });
});
```

Write the body in full. **Do not modify** any existing spec.

---

## Documentation Tasks

### 17 — `docs/agent-productivity-and-collaboration.md` (new)

Sections:
1. **Decisions** — D1–D5 table marked **Approved** (wording from intake §12, D3 Option B, D4 Option B).
2. **Role and route matrix** — the Product rules role table + every endpoint (method, route, policy, success code).
3. **Tasks** — fields, lifecycle (Pending/Completed, archive/restore, no hard delete, no-op rules), ownership (D1), ticket link rule, behaviour after ticket reassignment (`LinkedTicketState`, "Linked ticket no longer accessible", no auto-transfer), SuperAdmin reassignment and the 409 link rule, urgency definitions, list filters and order, dashboard summary.
4. **Reminders** — creator vs recipient, one per task, future and ≤ due, generations, `POST /notifications/check` + 60 s polling + app-open, missed-on-next-sign-in; **`WasMissed` = issued > 5 min after the scheduled time = delayed issuance, not proof the Agent was offline** (F6); idempotent `INSERT … SELECT` (`UPDLOCK` on `AgentTasks`, `NOT EXISTS (UPDLOCK, HOLDLOCK)`) + `UX_TaskReminderNotifications_TaskGeneration`; mark-as-read ≠ completion; complete/archive cancel, reopen/restore do not recreate; **reassignment (F2)**: pending reminders follow the task, issued unread ones are transferred (same row, stays unread, `TransferredFrom/At` recorded), read ones stay as the reader's hidden history, all in the reassignment transaction; races (issuance vs reassignment/completion: first commit wins); server UTC authority / local display; **deferred**: push, email, SMS, background delivery, recurrence.
5. **Quick replies** — Personal vs Shared governance (D2), immutability of scope/owner, copy-only semantics ("Copies text only — does not send a message"), plain text rendering, archived behaviour.
6. **Team collaboration** — membership managed by SuperAdmin or current primary Agent (D3), what collaborators can and cannot do (no `/api/v1/tickets*`), unassigned ⇒ suspended, reassignment rules, removal semantics, membership history columns, append-only messages (D5), "Ticket Activity vs Internal Discussion vs Task state" comparison table, "a collaborator is not an assignee".
7. **Persistence** — the five tables, FKs (all Restrict), named indexes and why, `AgentTasks.Version` rowversion + ETag/`If-Match` protocol (F1), `KnownUniqueIndexes` 409 allow-list (F4), raw-SQL issuance, `ITicketAccessLock` lock-then-check-then-write transactions and the lock order (F3), and why `UPDLOCK` is required with `READ_COMMITTED_SNAPSHOT ON`; the retry model — R1: every attempt starts from a cleared change tracker and re-runs the whole handler, the commit is never replayed (503); R2: explicit execution strategy around the idempotent autocommit statements, the next check as the last retry.
8. **Frontend** — routes, nav entries, bell polling (the only interval in the app), timezone handling.
9. **Migration impact** — additive only; rollback (see Migration / Rollback).

### 18 — Other docs

- `docs/agent-dashboard.md`: §8 (lines 65–69) replace the first bullet with "- Tasks & reminders, quick replies and team collaboration: delivered by DASH-002 — see [agent-productivity-and-collaboration.md](agent-productivity-and-collaboration.md). The dashboard adds a separate Tasks section (`GET /api/v1/agent-dashboard/tasks`); the ticket snapshot and its metrics are unchanged." Add `GET /api/v1/agent-dashboard/tasks` to §6 API.
- `docs/identity-and-access.md`: matrix (after line 23) add rows for tasks, task reassignment, reminder inbox, shared templates, personal templates, internal discussion, collaborator management (values from the Product rules table); routes table (after line 49) add every new route with its policy.
- `docs/api-conventions.md`: line 3 add `/api/v1/notifications/check` is an action endpoint (document beside the singleton exception); error table add `| ForbiddenException | 403 | ProblemDetails |` `| PreconditionRequiredException | 428 | ProblemDetails |` and `| CommitOutcomeUnknownException | 503 | ProblemDetails ("may have been saved — reload before retrying") |`; Auth bullet append "Records the caller can see but may not change (Shared quick replies for Agents, collaborator management by non-managers) return 403 (`ForbiddenException`)."; new bullet **Optimistic concurrency (versioned resources, first: `/api/v1/tasks`)**: responses carry `version` and a strong `ETag`; every mutation requires `If-Match` with that exact value — missing ⇒ 428, malformed/`*`/weak ⇒ 400, stale ⇒ 409; clients reload and re-apply, never auto-retry. Tickets keep their existing server-side rowversion behaviour. Unique-index conflicts ⇒ 409 only for named business indexes (`KnownUniqueIndexes`); any other ⇒ 500.
- `docs/persistence-and-transactions.md`: line 5 add "`AgentTasks.Version` — exposed as ETag and checked against `If-Match` before the change (F1)" and "unique-index violations ⇒ `ConflictException` only for the allow-listed business indexes in `EfUnitOfWork.KnownUniqueIndexes`; others stay 500 (F4)"; after line 9 add two bullets: "**Idempotent single-statement writes:** `TaskReminderRepository.IssueDueAsync` (one `INSERT … SELECT … NOT EXISTS` with `UPDLOCK` / `HOLDLOCK`), `MarkReadAsync` and `TransferUnreadAsync` (`ExecuteUpdateAsync`) are atomic, conditional statements that bypass the change tracker." and "**Write-time authorization (F3):** commands whose permission depends on another aggregate's current state (ticket assignment, collaborator membership) are `ITransactionalCommand`s that read that state through `ITicketAccessLock` (`UPDLOCK, HOLDLOCK, ROWLOCK`) and write before commit. Plain reads never block under `READ_COMMITTED_SNAPSHOT`, so lock hints are mandatory for such checks. Lock order: `Tickets` → `TicketCollaborators` → `AgentTasks`."; replace the **Retries** bullet (line 7) with: "**Retries:** `EnableRetryOnFailure` is on. Explicit transactions go through `ExecuteInTransactionAsync`: each attempt clears the change tracker, opens a transaction and re-runs the whole handler, so handlers of `ITransactionalCommand`s must be replay-safe (no instance state, no external calls, no swallowed DB exceptions). A failure before commit is rolled back and retried; a failure **during** commit is never retried — it surfaces as `CommitOutcomeUnknownException` (503) and the client reloads. `ExecuteSql*` / `ExecuteUpdate` outside a transaction are not assumed to be retried by EF; wrap them in `CreateExecutionStrategy().ExecuteAsync` only when the statement is idempotent."
- `docs/frontend-ui-foundation.md`: Navigation bullet add the three new entries + toolbar bell; add bullet "**Polling:** only `core/notifications/NotificationBell` polls (60 s, Agent only, `exhaustMap`); every other screen loads on visit / user action." Pickers bullet add `TicketPicker` (`features/tasks/`).
- `docs/ticket-management.md` §12 (lines 144–150): replace "comments/internal notes" in the Deferred bullet with nothing (keep the rest) and add "- **DASH-002:** internal discussion and collaborators are a separate surface ([agent-productivity-and-collaboration.md](agent-productivity-and-collaboration.md)); they never change tickets, Ticket Activity or ticket visibility."
- `README.md` line 3: "… ticket workflow, assignment & history (`TICK-002`), the Agent Dashboard (`DASH-001`) and agent productivity & collaboration (`DASH-002`); …". Conventions list (after line 111): `- [Agent productivity and collaboration](docs/agent-productivity-and-collaboration.md)`.

Never claim outbound sending, push, email or SMS exists.

---

## Edge Cases & Failure Modes

- **Agent sends `assignedAgentUserId` of another Agent on task create** ⇒ 400 `assignedAgentUserId` (`CreateAgentTaskCommandHandler.ResolveOwnerAsync`). Own id ⇒ accepted (same as omitting).
- **SuperAdmin omits owner / picks SuperAdmin, Customer, unknown or disabled user** ⇒ 400 "Select an active Agent." (`AgentTaskGuards.EnsureActiveAgentAsync`).
- **Agent calls `PUT /tasks/{id}/assignee`** ⇒ 403 (SuperAdmin policy). **Customer calls any new endpoint** ⇒ 403 (Staff/Agent policies). **SuperAdmin calls `/notifications/*` or `GET /collaborations`** ⇒ 403 (Agent policy).
- **Agent reads/edits another Agent's task (direct URL)** ⇒ 404 (`AgentTaskRepository.Scope`). Filtering `assignedAgentUserId=<other>` as an Agent ⇒ empty page (filters only narrow).
- **Link to a ticket assigned to someone else / unassigned / nonexistent** ⇒ 400 `ticketId` with one generic message (no existence leak) — `EnsureTicketAssignedToOwnerAsync` uses the owner scope. Invited collaborator linking the ticket ⇒ same 400.
- **Linked ticket reassigned away from the owner** ⇒ task unchanged; Agent sees `Inaccessible`, `linkedTicketId`/`Subject` null, UI "Linked ticket no longer accessible" with Remove link / Complete / Archive available; SuperAdmin sees subject + state. Reassigned back ⇒ `Accessible` again automatically (computed in SQL).
- **SuperAdmin reassigns a linked task to an Agent who does not hold the ticket** ⇒ 409 unless `removeTicketLink: true`; nothing saved.
- **Stale edit (F1)** — two tabs (same or different users) load version V1; tab A saves (V2); tab B saves with `If-Match: "V1"` ⇒ 409 before any change (`AgentTaskGuards.EnsureVersion`); UI reloads, keeps tab B's typed input, user re-applies with V2. Applies to every task mutation (edit, link, reminder, complete/reopen, archive/restore, reassign).
- **Write between load and save (F1)** — `If-Match` matched the loaded row but another request commits before this save ⇒ EF's `WHERE [Version] = @original` updates 0 rows ⇒ `DbUpdateConcurrencyException` ⇒ 409 (`EfUnitOfWork.cs` lines 15–19); inside transactional commands the whole transaction rolls back (e.g. no reminder transfer without its reassignment).
- **Missing / malformed `If-Match`** ⇒ 428 / 400 (`EntityTags.RequireIfMatch`); the handler never runs. `If-Match: *` ⇒ 400 (not supported). A no-op mutation with a stale version ⇒ 409 (the precondition is checked first).
- **Repeated complete / reopen / archive / restore / identical edit** ⇒ 200, no save, `UpdatedAtUtc` unchanged (domain methods return false).
- **Editing an archived task / scheduling on completed or archived** ⇒ 409 ("Restore it first" / "Reopen or restore…"). Cancelling a reminder is always allowed.
- **Reminder in the past, or after due** ⇒ 400 `remindAtUtc`. **Due moved before a pending reminder** ⇒ 400 `dueAtUtc`. **Due date in the past** ⇒ allowed (task becomes Overdue). **Task with reminder but no due date** ⇒ allowed.
- **Re-saving the same reminder time** ⇒ no-op: generation unchanged, an already-issued reminder is not re-sent. **Different time** ⇒ generation + 1 ⇒ one new notification when due.
- **Two tabs / rapid retries / five parallel `POST /notifications/check`** ⇒ one row per (task, generation): `UPDLOCK, HOLDLOCK` + unique index; a transient failure (incl. deadlock victim 1205) is retried by the explicit execution strategy around the statement (R2) and the replay inserts nothing new; if every retry fails, the next 60-second check retries. Verified by Verification 9.
- **Delayed issuance (F6)** — no check reaches the server near the reminder time (signed out, CRM closed, tab suspended, network/API down) ⇒ nothing happens server-side; the first `check` afterwards (normally right after the next sign-in / app open) issues it with a late `IssuedAtUtc` ⇒ `wasMissed = true`. `wasMissed` never claims the Agent was offline; it is only a label.
- **Clock/zone** — the browser converts local input to ISO; comparisons always use server `TimeProvider`. A reminder set 30 s ahead may arrive up to ~60 s late (poll interval) — documented best-effort.
- **Task completed / archived before reminder time** ⇒ `RemindAtUtc` cleared; issuance predicate also requires Pending + unarchived. Reopen/restore ⇒ no reminder until scheduled again.
- **Task reassigned (F2)** — not-yet-issued reminder ⇒ new owner receives it when due (or as missed on next check); former owner never does. Issued **unread** ⇒ the same row moves to the new owner (`TransferUnreadAsync`), stays unread, appears once in their inbox/badge; the former owner's badge drops and their mark-read ⇒ 404. Issued **read** ⇒ stays with the reader as history, hidden from both inboxes; reappears for the reader if the task returns. Repeated reassignment A ⇒ B ⇒ C moves unread rows each time; `TransferredFromUserId` holds only the last previous holder (documented).
- **Issuance vs reassignment (F2)** — check (old owner) commits first ⇒ its new row is moved by the reassignment's `TransferUnreadAsync`; reassignment's task `UPDATE` first ⇒ the check's `UPDLOCK` read waits, then sees the new owner and inserts nothing for the old owner (the new owner's next check issues it). Never two rows (unique index) and never a stranded row addressed to a non-owner.
- **Issuance vs completion / archive** — completion committed first ⇒ check waits and then skips the task (Completed / `RemindAtUtc` NULL); check first ⇒ the notification exists and the inbox shows the task as Completed/Archived, never as pending work.
- **Mark-read vs transfer** — former owner marks read before the transfer runs ⇒ row is read and stays theirs (transfer moves unread rows only); transfer first ⇒ mark-read waits on the row and then affects 0 rows ⇒ 404.
- **Disabled Agent** ⇒ cannot sign in (`OnTokenValidated`), so no checks run; their tasks keep their reminders until SuperAdmin reassigns; cannot be selected as owner or collaborator (400).
- **Bell failures** ⇒ badge hidden, no snackbar, next tick retries; no exponential storm (`exhaustMap` prevents overlap). Signing out ⇒ `role` becomes null ⇒ `switchMap` stops polling.
- **Agent opens or edits a Shared template** ⇒ read/copy OK; `PUT`/archive ⇒ 403 "Shared quick replies are managed by SuperAdmin." **Another Agent's Personal template** ⇒ 404. **SuperAdmin opens a Personal template id** ⇒ 404. **Archived Shared template for an Agent** ⇒ 404 and hidden from lists.
- **Client sends `scope`/`ownerUserId` on create** ⇒ ignored (not in the request record); scope from role.
- **Template text with `<script>` / `{{name}}` / Markdown** ⇒ stored and copied verbatim, rendered escaped by interpolation; clipboard content equals stored body. Clipboard blocked by browser ⇒ failure snackbar.
- **Collaborator calls `GET /api/v1/tickets/{id}`, `/activity`, `PUT`, transitions, escalation** ⇒ 404 exactly as before (no ticket code changed). Collaborator manages membership ⇒ 403.
- **Agent invites on an unassigned ticket or a ticket assigned to someone else** ⇒ 404 (no access at all, evaluated from the locked ticket row). **Self-invite** ⇒ 400. **Inviting the current primary** ⇒ 400. **Inviting a disabled / non-Agent account** ⇒ 400. **Duplicate active invite** ⇒ 409: sequential via the locked membership check; parallel invites wait on the key-range lock and then see the row; the filtered unique index `UX_TicketCollaborators_ActiveMembership` + `KnownUniqueIndexes` is the last line (still 409).
- **Ticket unassigned** ⇒ collaborator `GET`/`POST` ⇒ 404; membership rows kept; My Collaborations shows "Access suspended". SuperAdmin still reads/posts/manages.
- **Ticket reassigned** ⇒ former primary loses ticket details/activity (existing) **and** collaboration access unless they hold an active membership; new primary can manage existing collaborators; memberships unchanged.
- **Collaborator removed while viewing** ⇒ next load/post ⇒ 404 ⇒ "This discussion is no longer available to you."
- **Authorization vs concurrent change (F3)** — post / invite / remove and ticket-linked task writes lock the ticket row (and the poster's membership key) before deciding and hold the locks until commit. A ticket reassignment, unassignment or membership removal committed earlier ⇒ the write is rejected (404 / 400); one arriving later waits (typically milliseconds) and applies after the write, which was legitimate at its commit. Deadlocks are transient (1205): the attempt is rolled back and re-run from a cleared change tracker, re-acquiring the locks (R1). Reads (thread, context, lists) remain point-in-time and may be one change stale until the next load.
- **Message posted on Resolved/Closed ticket** ⇒ allowed; ticket `Status`, `UpdatedAtUtc`, `Version` and `TicketActivities` unchanged (Verification 11 SQL).
- **Author later disabled** ⇒ messages keep the author name (join to `AspNetUsers`, accounts never deleted).
- **Empty / whitespace / 2,001-character message** ⇒ 400 (`NotEmpty`, `MaximumLength(2000)`).
- **DASH-001 ticket snapshot fails but tasks succeed (or vice versa)** ⇒ each section shows its own error/Retry; no zeros for the failed section; the other renders normally.
- **Transient failure after a partially completed transaction (R1)** — e.g. `ReassignAgentTask`: the task `UPDATE` was saved (tracked entity accepted, new `Version` in memory), then the transfer is chosen as deadlock victim ⇒ rollback, `ChangeTracker.Clear()`, full re-run: the task is re-read with its original `Version` (the same `If-Match` still matches), the ticket is re-locked, the transfer re-executed — no stale accepted entity, no partial reassignment, no spurious 409. Same for create task / set link / post / invite / remove.
- **Commit outcome unknown (R1)** — connection lost or timeout during `COMMIT` ⇒ 503 `CommitOutcomeUnknownException`, never replayed. The change may or may not exist; the UI reloads (task, thread, collaborator list) and says so. A user who re-submits a create/post without checking can create a duplicate — the accepted cost of not adding idempotency-key infrastructure. Applies to all existing `ITransactionalCommand`s too (safer than the previous replay).
- **Lost acknowledgement on a non-transactional single save** — EF may replay `SaveChanges`: a task update replay meets the new `Version` ⇒ 409 (already applied; the reload shows it); a replayed insert keeps its Guid key ⇒ primary-key violation ⇒ 500 (not allow-listed) — never a duplicate.
- **Malformed vs stale `If-Match` (R3)** — anything that is not one quoted base64 value decoding to exactly 8 bytes (`"AAAA"`, a 16-byte value, `*`, `W/"…"`, two values) ⇒ 400 before the handler runs; a well-formed 8-byte value that is not the current version ⇒ 409 from `EnsureVersion`.
- **Unrecognized unique violation (F4)** — any 2601/2627 on an index not in `KnownUniqueIndexes` (Identity email, `UX_TaskReminderNotifications_TaskGeneration` hit through an unexpected path, existing ticket/customer indexes) is rethrown ⇒ logged 500 ProblemDetails, never a 409 that would hide a defect.
- **Picker vs reality (F5)** — a ticket offered by the owner-aware picker can be reassigned before Save ⇒ the locked server check returns 400 `ticketId`; SuperAdmin changing the owner clears the selected ticket.
- **`ITicketAccessLock` called outside a transaction** ⇒ `InvalidOperationException` (500) — a programming error caught by Verification 5/11, never a silently unlocked check.
- **Migration half-applied** ⇒ SQL Server runs the migration in one transaction; on failure nothing is applied. New API against the old schema ⇒ only new endpoints fail ("Invalid object name 'AgentTasks'"); existing endpoints keep working.

---

## Test Plan

1. **Unit (backend) — add:** `backend/tests/Crm.UnitTests/AgentTasks/CreateAgentTaskCommandHandlerTests.cs` → `Handle_AgentCreatesTicketLinkedTaskWithReminder_OwnsPendingTaskAndSchedulesFirstReminderInOneSave` (task 8). Pattern: `Tickets/TransitionTicketStatusCommandHandlerTests.cs`.
2. **Unit (frontend) — add:** `frontend/src/app/features/tasks/tasks-api.spec.ts` → "requests the default My Tasks page (Pending, unarchived) omitting empty filters and returns typed tasks with link state" (task 16). Pattern: `features/tickets/tickets-api.spec.ts`.
3. **Keep unchanged (must still pass):** backend `Auth/LoginCommandHandlerTests.cs`, `Customers/CreateCustomerCommandHandlerTests.cs`, `Diagnostics/GetSystemStatusQueryHandlerTests.cs`, `Tickets/CreateTicketCommandHandlerTests.cs`, `Tickets/TransitionTicketStatusCommandHandlerTests.cs`, `AgentDashboard/GetAgentDashboardQueryHandlerTests.cs`; frontend `core/api/api-url.spec.ts`, `core/auth/auth-service.spec.ts`, `features/customers/customers-api.spec.ts`, `features/tickets/tickets-api.spec.ts`, `features/tickets/ticket-workflow.spec.ts`, `features/agent-dashboard/agent-dashboard-api.spec.ts`. No existing fake needs changes (`ITicketRepository` is untouched).
4. **Manual — Tasks & reminders, Quick Replies, Collaboration, regression:** Verification Steps 4–13 (API via Postman CLI or curl, SQL via `sqlcmd`, browser).

No other tests, frameworks, mocking libraries, or integration / SQL / API / E2E / negative-path / load / security suites.

---

## Migration / Rollback

- **Forward:** one additive migration `AddAgentProductivityAndCollaboration` (task 7): five new tables, Restrict FKs to `AspNetUsers`, `Tickets`, `AgentTasks`; no change to existing tables or rows.
- **Pre/post evidence:** before applying, record `SELECT (SELECT COUNT(*) FROM Tickets) T, (SELECT COUNT(*) FROM TicketActivities) A, (SELECT COUNT(*) FROM Customers) C, (SELECT COUNT(*) FROM AspNetUsers) U, (SELECT COUNT(*) FROM RefreshSessions) R, (SELECT COUNT(*) FROM __EFMigrationsHistory) M` — after applying all equal, except `M` + 1.
- **Rollback (local):** stop the API; from `backend/`: `dotnet ef database update AddTicketWorkflowAndActivity --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` — drops the five tables (**all tasks, reminders, templates, memberships and internal messages are lost**); tickets, activity, customers, accounts and sessions remain. Then `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` and revert the code.
- **Half-applied state:** the migration DDL runs in one transaction; a failure applies nothing — fix and re-run `database update`. Code deployed before the schema ⇒ only the new endpoints return 500.

---

## Verification Steps

1. **Backend builds and tests:** in `backend/` run `dotnet build` (0 errors) then `dotnet test` (**7** passed: 6 existing + `CreateAgentTaskCommandHandlerTests`).
2. **Migration review and layering:** in `backend/`:
   - `dotnet ef migrations list --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ only `…_AddAgentProductivityAndCollaboration (Pending)` pending.
   - `dotnet ef migrations script AddTicketWorkflowAndActivity AddAgentProductivityAndCollaboration --project src/Crm.Infrastructure --startup-project src/Crm.Api --output $env:TEMP/AddAgentProductivityAndCollaboration.sql -- --environment Development` ⇒ only `CREATE TABLE` ×5, `CREATE (UNIQUE) INDEX` (incl. `WHERE [RemovedAtUtc] IS NULL`, `WHERE [RemindAtUtc] IS NOT NULL`), the `CK_QuickReplyTemplates_Owner` check, FKs **without** `ON DELETE CASCADE`, one `INSERT INTO [__EFMigrationsHistory]`. No `ALTER TABLE [Tickets]`, `UPDATE`, `DELETE`, `DROP`.
   - Record the pre-migration counts (Migration / Rollback), apply with `dotnet ef database update …`, re-run counts.
   - `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ "No changes…".
   - **Retry-safety review (R1/R2):** in `EfUnitOfWork.ExecuteInTransactionAsync` confirm `ChangeTracker.Clear()` is the first statement of the strategy lambda and runs again after a rollback; handler + save are in a `try` that rolls back and rethrows; `CommitAsync(CancellationToken.None)` is in its own `try` that throws `CommitOutcomeUnknownException`; nothing after a commit attempt can reach the strategy as a transient exception. `grep -rn "CreateExecutionStrategy" src/Crm.Infrastructure --include=*.cs` ⇒ only `EfUnitOfWork.cs` and `TaskReminderRepository.cs` (`IssueDueAsync`, `MarkReadAsync`); `TransferUnreadAsync` and `TicketAccessLock.cs` have none. Every handler of the six transactional commands has only primary-constructor dependencies (no fields). Commit-outcome-unknown cannot be reproduced by hand without fault injection: record it in the PR as "verified by review" together with the 503 mapping in `GlobalExceptionHandler`.
   - `grep -rn "EntityFrameworkCore\|Microsoft.AspNetCore" src/Crm.Application src/Crm.Domain --include=*.cs` ⇒ nothing. `git diff --stat -- src/Crm.Api/Controllers/TicketsController.cs src/Crm.Application/Tickets src/Crm.Domain/Tickets src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs src/Crm.Infrastructure/Persistence/Repositories/AgentDashboardRepository.cs src/Crm.Infrastructure/Persistence/Configurations/TicketConfiguration.cs src/Crm.Infrastructure/Persistence/Migrations/2026101*` ⇒ no changes.
3. **Frontend builds and tests:** in `frontend/` run `npx ng build` (no errors; budget warnings acceptable) and `npm test -- --watch=false` (**7** passed).
4. **Setup — dedicated DASH-002 accounts (nothing existing is deleted, reset or reassigned):** `.\run-dev.ps1`. As SuperAdmin (`admin@admin.com`) create Agents "Prod Alpha" (`prod.alpha@example.test`), "Prod Bravo" (`prod.bravo@example.test`), "Prod Charlie" (`prod.charlie@example.test`) on `/agents/new`; if an email exists use a new suffix (`prod.alpha2@…`). Register one Customer via `/sign-up` (`prod.customer@example.test`). Baseline: `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT COUNT(*) FROM AgentTasks; SELECT COUNT(*) FROM TaskReminderNotifications; SELECT COUNT(*) FROM QuickReplyTemplates; SELECT COUNT(*) FROM TicketCollaborators; SELECT COUNT(*) FROM TicketInternalMessages;"` (all 0 right after migration). As SuperAdmin create tickets **P1** (assigned Prod Alpha), **P2** (assigned Prod Alpha), **P3** (assigned Prod Bravo) on any customer. Obtain tokens via `POST /api/v1/auth/login` (keep them out of logs/PR text; refer to them as `$alpha`, `$bravo`, `$charlie`, `$admin`, `$customer`).
5. **Tasks (browser + API):**
   - As Prod Alpha on `/tasks/new`: create **K1** "Call back customer" linked to P1, due tomorrow; **K2** due yesterday (no link); **K3** no due date; **K4** due in 10 days. My Dashboard Tasks section ⇒ Pending **4**, Overdue **1** (K2), Due soon **1** (K1), list order K2, K1, K4, K3; DASH-001 ticket cards unchanged.
   - Complete K1 ⇒ Pending 3; Reopen ⇒ Pending 4 (`completedAtUtc` null again); Complete twice via API ⇒ second call 200 with identical `updatedAtUtc`. Archive K3 ⇒ gone from default list and dashboard, visible with "Show archived"; Restore ⇒ back.
   - `POST /api/v1/tasks` with `$alpha` and `"assignedAgentUserId": "<prod-bravo-id>"` ⇒ **400**. Link K2 to P3 (Bravo's ticket) ⇒ **400** `ticketId`. `GET /api/v1/tasks/<K1>` with `$bravo` ⇒ **404**; `GET /api/v1/tasks?assignedAgentUserId=<prod-alpha-id>` with `$bravo` ⇒ `totalCount` 0.
   - As SuperAdmin create **K5** for Prod Bravo linked to P3 ⇒ appears in Bravo's list, "Created by" SuperAdmin; omit `assignedAgentUserId` ⇒ 400; target SuperAdmin's own id ⇒ 400. `PUT /api/v1/tasks/<K5>/assignee` with `$alpha` ⇒ **403**.
   - Ticket reassignment: as SuperAdmin reassign **P1** to Prod Bravo. Prod Alpha's K1 ⇒ still Alpha's, "Linked ticket no longer accessible", API `linkedTicketState: "Inaccessible"`, `linkedTicketId: null`, `linkedTicketSubject: null`; SuperAdmin `GET /tasks/<K1>` shows id + subject + `Inaccessible`. Reassign task K1 to Prod Charlie with `removeTicketLink: false` ⇒ **409**; with `true` ⇒ 200, link removed. Reassign P1 back to Alpha afterwards.
   - **Optimistic concurrency (F1), browser:** open K4 in tab A as Prod Alpha and tab B as SuperAdmin (both show the same version). Save a title change in A ⇒ 200. Without reloading, save a different title in B ⇒ **409** "The task was changed by someone else…"; B reloads (A's title shown in the summary, B's typed title kept in the form); Save again ⇒ 200. Repeat with B clicking **Complete** ⇒ 409, Status card reflects the reloaded state, second click succeeds. Repeat with **two tabs of the same Agent** (sequential edits by one user are protected too).
   - **Optimistic concurrency (F1), API:** `GET /api/v1/tasks/<K4>` ⇒ `ETag` header = `"<version>"` from the body. `PUT /api/v1/tasks/<K4>` without `If-Match` ⇒ **428**; `If-Match: *`, `If-Match: "%%%"`, `If-Match: "AAAA"` (3 bytes) and `If-Match: "AAAAAAAAAAAAAAAAAAAAAA=="` (16 bytes) ⇒ **400** (`ifMatch`) with SQL unchanged (R3); a well-formed but invented 8-byte `If-Match: "AAAAAAAAAAE="` ⇒ **409** (stale, not malformed); `If-Match: "<previous version>"` ⇒ **409** and SQL `Title` unchanged; `If-Match: "<current>"` ⇒ 200 with a **different** `ETag`; replaying that request with the now-old ETag ⇒ 409; an identical (no-op) body with the current ETag ⇒ 200 and the **same** ETag. `POST /tasks/<K4>/completion`, `PUT /tasks/<K4>/reminder`, `PUT /tasks/<K4>/ticket-link`, `POST /tasks/<K4>/archive` and `PUT /tasks/<K4>/assignee` (SuperAdmin) without `If-Match` ⇒ **428** each.
   - **Owner-aware picker (F5):** as SuperAdmin on `/tasks/new` the ticket picker is disabled ("Select the task owner first"); choose Prod Bravo ⇒ only P3 offered; switch the owner to Prod Alpha ⇒ selection cleared, only P1/P2 offered. API: `$admin` `GET /api/v1/tasks/linkable-tickets?ownerUserId=<bravo-id>` ⇒ P3 only; without `ownerUserId` ⇒ 400; `$alpha` with `ownerUserId=<bravo-id>` ⇒ 400; `$alpha` without ⇒ P1, P2 only; `$customer` ⇒ 403. The server stays authoritative: `POST /api/v1/tasks` as SuperAdmin with owner Alpha and `ticketId` P3 ⇒ 400 `ticketId`.
   - **Write-time link authorization (F3):** in `sqlcmd` run `BEGIN TRAN; UPDATE Tickets SET AssignedAgentUserId = AssignedAgentUserId WHERE Id = '<P2>';` (keeps the row X-locked; rolled back below, so nothing changes). Within 20 s, as Prod Alpha create a task linked to P2 ⇒ the request **waits**; run `ROLLBACK` ⇒ it completes 201. Repeat with `PUT /tasks/<K2>/ticket-link` (P2, current `If-Match`) ⇒ waits ⇒ 200 after `ROLLBACK`. Then reassign P2 to Prod Bravo **via the API** and repeat the create as Alpha ⇒ 400 `ticketId`; reassign P2 back to Alpha via the API.
   - SQL: `SELECT Id, Status, IsArchived, DueAtUtc, RemindAtUtc, ReminderGeneration FROM AgentTasks` matches; `SELECT COUNT(*) FROM TicketActivities` unchanged by all task operations; `Tickets.UpdatedAtUtc` of P1/P2 changed only by the explicit reassignments.
6. **Reminders — online delivery and dedup:** as Prod Alpha schedule a reminder on K4 for **now + 2 minutes** and keep `/agent-dashboard` open in **two tabs**. Within ~3 minutes the bell badge shows **1** in both tabs and one snackbar appears per tab; `/notifications` lists "Reminder" (not missed). SQL: `SELECT TaskId, ReminderGeneration, COUNT(*) FROM TaskReminderNotifications GROUP BY TaskId, ReminderGeneration HAVING COUNT(*) > 1` ⇒ no rows. Refresh both tabs 5× ⇒ still one row. Mark as read ⇒ badge 0 in this tab immediately, other tab on its next tick; task K4 still Pending.
7. **Reminders, tasks and locks — SQL shape:** with `Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command = Information` in `appsettings.Development.json` (revert afterwards):
   - `POST /api/v1/notifications/check` ⇒ one `INSERT INTO [TaskReminderNotifications] … FROM [AgentTasks] AS t WITH (UPDLOCK, ROWLOCK) WHERE t.[AssignedAgentUserId] = @p… AND NOT EXISTS (… WITH (UPDLOCK, HOLDLOCK) …)` and one `SELECT COUNT(*)` with the `EXISTS (… [AgentTasks] … [AssignedAgentUserId] = @…)` owner filter.
   - Any task edit ⇒ `UPDATE [AgentTasks] … WHERE [Id] = @p AND [Version] = @p` (F1).
   - Task reassignment ⇒ in this order inside one transaction: `SELECT … FROM [Tickets] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` (only when the link is kept), `UPDATE [AgentTasks] … [Version] = @p`, `UPDATE … [TaskReminderNotifications] SET … [TransferredFromUserId] = … [RecipientUserId] … WHERE … [ReadAtUtc] IS NULL`, then commit (F2).
   - Post message / invite ⇒ `SELECT … FROM [Tickets] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` (+ `[TicketCollaborators] WITH (UPDLOCK, HOLDLOCK, ROWLOCK …)` for collaborators / the invitee) before the `INSERT`, in one transaction (F3).
   - `/tasks` list ⇒ `WHERE [a].[AssignedAgentUserId] = @…`, `ORDER BY CASE WHEN [a].[DueAtUtc] IS NULL …, [a].[DueAtUtc], [a].[Id]`, `OFFSET … FETCH`; dashboard tasks summary ⇒ one `COUNT(CASE WHEN …)` statement (or three counts with the documented fallback) — never unbounded row reads.
8. **Reminders — missed, cancel, reschedule, suppression, reassignment:**
   - Validation: `PUT /tasks/<K2>/reminder` with now + 1 h (K2 was due yesterday, so the reminder is after the due time) ⇒ **400** `remindAtUtc`; any past time ⇒ **400**.
   - Missed = delayed issuance (F6): schedule K3 (no due date) for now + 2 min, **sign out**, wait 8 minutes, sign in ⇒ badge 1, inbox shows **Missed reminder** with "Delivered more than 5 minutes after its scheduled time." (no wording claims the Agent was offline). Repeat while signed in but with the API stopped from 1 min before to 6 min after the time ⇒ also labelled missed after the API restarts (same meaning: issuance was delayed).
   - Cancel: schedule K4 for now + 3 min then `DELETE /tasks/<K4>/reminder` ⇒ no notification after 5 minutes. Reschedule: schedule K4 now + 2 min, then change to now + 4 min before the first fires ⇒ exactly one notification at ~4 min, `ReminderGeneration` +1 per change.
   - Suppression: schedule K1 for now + 2 min, complete K1 immediately ⇒ `RemindAtUtc` NULL, no notification; reopen ⇒ still no reminder. Same with Archive/Restore on K3.
   - **Pending reminder follows the task:** SuperAdmin schedules a reminder on Bravo's K5 for now + 2 min (Bravo is the recipient; SuperAdmin `/notifications` ⇒ route redirects home and API 403), then before it fires reassigns K5 to Prod Charlie (with `removeTicketLink: true`) ⇒ Charlie gets it (missed if they sign in later), Bravo never.
   - **Unread transfer (F2):** schedule a reminder on K5 (now Charlie's) for now + 2 min; Charlie keeps the app open until the bell shows it (do **not** mark it read). SuperAdmin reassigns K5 to Prod Bravo. Charlie: badge drops on the next tick, item gone, `PUT /notifications/<id>/read` with `$charlie` ⇒ 404. Bravo: badge +1, the **same** notification id, unread, "Transferred to you with the task on …". SQL: `SELECT Id, ReminderGeneration, RecipientUserId, TransferredFromUserId, TransferredAtUtc, ReadAtUtc FROM TaskReminderNotifications WHERE TaskId = '<K5>'` ⇒ that row now has recipient Bravo, transferred-from Charlie, `ReadAtUtc` NULL; the duplicate query from step 6 ⇒ no rows. Bravo marks it read ⇒ 204; K5 still Pending.
   - **Read history (F2):** schedule another reminder on K5 (Bravo), let it fire, Bravo marks it read; reassign K5 to Charlie ⇒ neither Bravo nor Charlie sees that read item; SQL shows it kept (recipient Bravo, `ReadAtUtc` set, no new transfer). Reassign K5 back to Bravo ⇒ it reappears in Bravo's history as read; no new row.
   - **Issuance vs reassignment / completion (F2), lock demonstration:** schedule K4 (Alpha) for now + 1 min and wait until due **without** opening the app as Alpha. In `sqlcmd`: `BEGIN TRAN; UPDATE AgentTasks SET AssignedAgentUserId = '<charlie-id>' WHERE Id = '<K4>';` (do not commit). Within 20 s call `POST /api/v1/notifications/check` with `$alpha` ⇒ the call **waits**; run `ROLLBACK` ⇒ the call returns `issuedNow: 1` (task still Alpha's). Schedule a new K4 reminder (now + 1 min), wait until due, then `BEGIN TRAN; UPDATE AgentTasks SET Status = N'Completed', CompletedAtUtc = SYSDATETIMEOFFSET(), RemindAtUtc = NULL WHERE Id = '<K4>';` ⇒ call check ⇒ waits ⇒ `COMMIT` ⇒ `issuedNow: 0` and no row for that generation. Reopen K4 in the UI afterwards. (These `sqlcmd` writes touch only DASH-002 test task K4; record them in the PR description.)
   - **Retry after a partially completed transaction (R1):** give K5 (Bravo) one **unread** notification (repeat the unread-transfer setup). In `sqlcmd`: `SET DEADLOCK_PRIORITY HIGH; BEGIN TRAN; UPDATE TaskReminderNotifications SET ReadAtUtc = ReadAtUtc WHERE TaskId = '<K5>';` (no value changes). As SuperAdmin reassign K5 to Prod Charlie with the current `If-Match` ⇒ the request **waits** (its task `UPDATE` succeeded; the transfer is blocked). In `sqlcmd`: `UPDATE AgentTasks SET Title = Title WHERE Id = '<K5>';` ⇒ SQL Server detects the deadlock and, because of the priority, chooses the API's transaction as victim (1205); the `sqlcmd` statement completes. Run `ROLLBACK` within ~5 s. Expected: the reassignment returns **200** (not 409 — the retried attempt re-read the rolled-back task with its original `Version` instead of reusing the accepted entity), the API log shows one EF execution-strategy retry warning for that request, K5's owner is Charlie, the notification row moved exactly once (recipient Charlie, transferred-from Bravo, still unread), the duplicate query returns no rows and `SELECT COUNT(*) FROM TaskReminderNotifications WHERE TaskId = '<K5>'` is unchanged. The other five transactional commands use the same `ExecuteInTransactionAsync` path and are covered by the Verification 2 review.
   - Customer: `$customer` ⇒ `POST /api/v1/notifications/check` **403**; `GET /api/v1/tasks` **403**.
9. **Reminders — concurrent idempotency (API):** schedule K4 for now + 1 min, wait until due, then in PowerShell 7: `1..5 | ForEach-Object -Parallel { Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/v1/notifications/check -Headers @{ Authorization = "Bearer $using:alpha" } -ContentType 'application/json' -Body '{}' }` ⇒ the five responses' `issuedNow` sum to **1**; SQL duplicate query from step 6 ⇒ no rows.
10. **Quick Replies:** as SuperAdmin create Shared "Billing follow-up" (Billing) with body `Hello {{name}}, <b>thanks</b>!` ⇒ list shows it escaped literally; Copy ⇒ paste into Notepad equals the body exactly. As Prod Alpha create Personal "My greeting"; Alpha sees both (scope badges), Copy works from list and from P2 ticket details panel; `PUT /api/v1/quick-replies/<shared-id>` with `$alpha` ⇒ **403**; Prod Bravo `GET /api/v1/quick-replies/<alpha-personal-id>` ⇒ **404** and list excludes it; SuperAdmin `GET` Alpha's personal id ⇒ **404**; `POST` with `"scope":"Shared"` as Alpha ⇒ created as **Personal**. Archive Shared as SuperAdmin ⇒ disappears for Alpha (`GET` 404), SuperAdmin "Show archived" shows it, Restore ⇒ back. Search "follow" (title) and "thanks" (body) both find it. `$customer` ⇒ **403**. SQL: `TicketActivities` and `TicketInternalMessages` counts unchanged by copying.
11. **Collaboration:**
    - SuperAdmin and Prod Alpha exchange messages on P2 from Ticket Details ("Internal only — not visible to customers" banner shown). SQL before/after: `SELECT UpdatedAtUtc, Status, Version FROM Tickets WHERE Id = '<P2>'` unchanged; `SELECT COUNT(*) FROM TicketActivities WHERE TicketId = '<P2>'` unchanged.
    - Prod Alpha invites **Prod Bravo** on P2 (candidates list excludes Alpha and existing collaborators). Bravo: nav **My Collaborations** lists P2 ⇒ `/collaboration/tickets/<P2>` shows subject/status/priority only, existing messages visible, posts a message. Bravo `GET /api/v1/tickets/<P2>` ⇒ **404**, `/activity` ⇒ **404**, `POST /tickets/<P2>/transitions` ⇒ **404**; `POST /api/v1/collaborations/<P2>/collaborators` (inviting Charlie) ⇒ **403**; `POST /api/v1/tasks` linking P2 ⇒ **400**. The collaboration response JSON contains no `description`, `customerName` or `customerEmail`.
    - Alpha invites herself ⇒ 400; invites Bravo again ⇒ 409; Alpha tries to invite on P3 (Bravo's ticket) ⇒ **404**. SuperAdmin invites Charlie on P3 ⇒ 201 and removes him ⇒ 204; Charlie's `GET /api/v1/collaborations/<P3>` ⇒ 404 immediately.
    - Unassign P2 (SuperAdmin) ⇒ Bravo's page/API ⇒ 404, My Collaborations shows "Access suspended"; SuperAdmin still reads/posts. Reassign P2 to Prod Charlie ⇒ Bravo regains collaborator access; **Alpha** (former primary, not invited) ⇒ `GET /api/v1/collaborations/<P2>` **404** and Ticket Details 404; Charlie (new primary) can remove Bravo ⇒ Bravo loses access; all earlier messages still listed with original authors.
    - Message validation: empty and 2,001-character bodies ⇒ 400; Resolve P2 then post ⇒ 201 and status stays Resolved.
    - **Write-time authorization (F3):** (a) *removal vs post* — re-invite Bravo on P2 via the API first, then in `sqlcmd`: `BEGIN TRAN; UPDATE TicketCollaborators SET RemovedAtUtc = SYSDATETIMEOFFSET(), RemovedByUserId = '<charlie-id>' WHERE TicketId = '<P2>' AND AgentUserId = '<bravo-id>' AND RemovedAtUtc IS NULL;`. Within 20 s Bravo posts ⇒ the request **waits**; `COMMIT` ⇒ the post returns **404** and `SELECT COUNT(*) FROM TicketInternalMessages WHERE TicketId = '<P2>'` is unchanged. (b) *assignment vs post* — re-invite Bravo via the API; `BEGIN TRAN; UPDATE Tickets SET AssignedAgentUserId = AssignedAgentUserId WHERE Id = '<P2>';` ⇒ Bravo posts ⇒ waits ⇒ `ROLLBACK` ⇒ 201. (c) *parallel invites* — as the current primary (Charlie) in PowerShell 7: `1..5 | ForEach-Object -Parallel { try { Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/v1/collaborations/<P2>/collaborators -Headers @{ Authorization = "Bearer $using:charlie" } -ContentType 'application/json' -Body '{"agentUserId":"<alpha-id>"}' | Out-Null; 'created' } catch { $_.Exception.Response.StatusCode.value__ } }` ⇒ exactly one `created`, four `409`; `SELECT COUNT(*) FROM TicketCollaborators WHERE TicketId = '<P2>' AND AgentUserId = '<alpha-id>' AND RemovedAtUtc IS NULL` = 1. (The `sqlcmd` membership removal in (a) touches only DASH-002 test data; record it.)
    - `$customer` ⇒ every `/api/v1/collaborations*` call **403**. SQL: `SELECT TicketId, AgentUserId, AddedByUserId, AddedAtUtc, RemovedByUserId, RemovedAtUtc FROM TicketCollaborators` shows the full invite/remove history.
12. **OpenAPI:** `curl -s http://localhost:5080/openapi/v1.json` contains `/api/v1/tasks`, `/api/v1/notifications/check`, `/api/v1/quick-replies`, `/api/v1/collaborations/{ticketId}/messages`, `/api/v1/agent-dashboard/tasks`, each with a "Requires role:" description and string enums.
13. **UI states and regression:** stop the API and click Refresh on My Dashboard ⇒ both sections show their own error + Retry (no zeros); restart ⇒ Retry tasks only reloads tasks. Narrow to ~360 px ⇒ tasks, notifications, quick replies, discussion and collaboration pages wrap without horizontal scroll. As SuperAdmin, `/agent-dashboard`, `/notifications`, `/collaboration` redirect Home; the bell is absent. DASH-001 counts/top-10 order for Dash Alpha (Story 06 data) unchanged; Ticket list/create/detail/workflow/assignment/escalation/activity, Customers, Agents, Profile, login/logout unchanged. `grep -rnE "(margin|padding)-(left|right)|text-align: *(left|right)" frontend/src` ⇒ nothing; `grep -rnE "setInterval|interval\(|timer\(" frontend/src/app` ⇒ only `core/notifications/notification-bell.ts`; `grep -rn "innerHTML" frontend/src/app` ⇒ nothing. Post-run counts: `Tickets` = pre + 3 (P1–P3), `Customers` + 1 (registered customer), `AspNetUsers` + 4, no pre-existing ticket, task or account edited except the listed DASH-002 test records. Record executed checks and any left for manual browser acceptance in the PR description.

---

## Done Criteria

- [ ] Builds on committed DASH-001/TICK-002; no Ticket, Customer, Identity, dashboard-count or history duplication; `TicketsController`, `TicketRepository`, `ITicketRepository`, `Ticket`, `TicketActivity`, `GET /api/v1/agent-dashboard` and DASH-001 metrics unchanged (Verification 2, 13).
- [ ] Role and record scoping match the Product rules matrix on the server; Customer gets 403 on every new endpoint; out-of-scope records 404; visible-but-unmodifiable 403 (`ForbiddenException`).
- [ ] Agent-created tasks are Pending and owned by the caller; SuperAdmin creates/reassigns only to active Agents; Agents cannot target or reassign other Agents' work (Verification 5).
- [ ] Tasks support validated title/description/optional link/optional due, edit, complete/reopen, archive/restore, server paging/filtering/search, stable ordering, no hard delete, no-op rules without `UpdatedAtUtc` bumps.
- [ ] Every task mutation requires `If-Match` with the `version`/`ETag` returned by the API: missing 428, malformed (incl. not exactly 8 decoded bytes) 400, well-formed but stale 409 (also between two tabs of one user and between load and save); the UI reloads on 409, keeps typed input and never auto-retries (Verification 5, 7).
- [ ] Ticket links require the owner to be the current assignee **at commit time** (row-locked check in the create / link / reassign transaction); the SuperAdmin picker lists only the selected owner's tickets while the server stays authoritative; after ticket reassignment no ticket data leaks to the Agent (`Inaccessible`, null id/subject) and no task is transferred; SuperAdmin reassignment enforces the link rule (409 or explicit unlink) (Verification 5).
- [ ] Pending / Overdue / Due soon counts and the next-5 preview are computed in SQL over the full owned set with the documented UTC boundaries; completed/archived tasks never count (Verification 5, 7).
- [ ] One optional reminder per task: future, ≤ due, owner or SuperAdmin schedules/changes/cancels; only the current owner receives it; never Customer, creator-SuperAdmin or collaborators.
- [ ] Due reminders appear via the 60-second bell while signed in and as missed after next sign-in; persisted with read/unread; idempotent across tabs, refreshes and parallel calls (one row per task + generation); mark-read never completes the task (Verification 6–9).
- [ ] Complete/archive cancel pending delivery; reopen/restore do not recreate; on reassignment pending reminders follow the task, issued unread notifications are transferred (same row, still unread, no duplicate), read ones stay as the reader's hidden history — task update and transfer commit together; issuance racing reassignment/completion never strands or duplicates a notification (Verification 8).
- [ ] "Missed reminder" is defined and shown as delayed issuance (> 5 min after the scheduled time), never as proof the Agent was offline.
- [ ] DASH-001 cards/top-10 queue unchanged and Agent-only; tasks section fails independently without fake zeros.
- [ ] Agents manage their own Personal quick replies and read/copy Shared; only SuperAdmin manages Shared; scope/owner derived server-side and immutable; search/category/scope/archived/paging work; copy reproduces the body exactly and sends nothing (Verification 10).
- [ ] SuperAdmin invites/removes on any ticket; the current primary Agent only on their assigned ticket; collaborator-only Agents cannot manage; no self-invite; actor/time recorded for add and remove; posts, invites and removals are authorized from row-locked ticket/membership state inside their transaction, so a concurrent reassignment/removal is respected at commit (Verification 11).
- [ ] Only allow-listed business unique indexes (`UX_TicketCollaborators_ActiveMembership`) map to 409; every other unique violation remains a logged 500.
- [ ] SuperAdmin and current primary read/post from Ticket Details; invited collaborators only through `/api/v1/collaborations/*` and `/collaboration/tickets/:id` with minimal context; collaborators still get 404 on every existing ticket API.
- [ ] Unassignment suspends collaborator access; removal revokes immediately; former primary loses access unless separately invited.
- [ ] Messages are append-only, attributed, paged, labelled internal-only, retained across reassignment/removal, invisible to Customers, and never change `Tickets.UpdatedAtUtc`/`Version` or `TicketActivities` (Verification 11 SQL).
- [ ] New Angular routes/pages (tasks, notifications, quick replies, collaborations, ticket discussion/collaborators/quick-reply panel, dashboard section) are responsive, accessible, permission-aware, with loading/empty/error/Retry states, logical CSS only, no `innerHTML`, existing auth reused; polling only in `NotificationBell`.
- [ ] Transactional commands are retry-safe: every attempt starts from a cleared change tracker and re-runs the whole handler (a deadlock after `ReassignAgentTask`'s task save retries to 200 with exactly one transfer); a commit failure is never replayed and returns 503 `CommitOutcomeUnknownException`, after which the UI reloads (Verification 2, 8).
- [ ] `IssueDueAsync` and `MarkReadAsync` are wrapped in an explicit execution strategy and stay idempotent under replay; no document claims `EnableRetryOnFailure` retries raw SQL / `ExecuteUpdate` implicitly; the next 60-second check is the final retry for issuance.
- [ ] One reviewed additive migration `AddAgentProductivityAndCollaboration` with Restrict FKs and the documented indexes; existing tables, rows and migration history intact (pre/post counts recorded).
- [ ] No external communication, background scheduler, push, sockets, SLA automation or new ticket permissions; no new packages; no secrets or tokens in logs, source or PR text.
- [ ] `dotnet build`, `dotnet test` (7 passed), `npx ng build`, `npm test -- --watch=false` (7 passed); exactly one new backend and one new frontend unit test; manual Verification 4–13 executed and recorded.
- [ ] `docs/agent-productivity-and-collaboration.md` created with D1–D5 recorded as **approved**, exact access boundaries and deferred functionality; `agent-dashboard.md`, `identity-and-access.md`, `api-conventions.md`, `persistence-and-transactions.md`, `frontend-ui-foundation.md`, `ticket-management.md` and `README.md` updated.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**

---

## Implementation progress (squad-implement, awaiting independent review)

Status: **implemented and verified except the browser steps; not committed.**

- Backend tasks 1–8, frontend tasks 9–16 and documentation tasks 17–18 are done, in dependency order. Migration `20261017232626_AddAgentProductivityAndCollaboration` is additive (5 `CREATE TABLE`; pre/post counts T26/A82/C9/U16/R57 unchanged, `__EFMigrationsHistory` 5 → 6; `has-pending-model-changes` clean).
- Tests: `dotnet build` 0 errors; `dotnet test` 7 passed (6 existing + `CreateAgentTaskCommandHandlerTests`); `npx ng build` OK (initial-bundle budget warning only); `npm test -- --watch=false` 7 passed (6 existing + `tasks-api.spec.ts`).
- Manual API/SQL verification executed against local `CrmDev` (dedicated Prod Alpha/Bravo/Charlie accounts, tickets P1–P3): Verification 5 (tasks, scoping, Inaccessible link, F1 If-Match 428/400/409, F5 picker, F3 ticket-link lock waits), 6/8/9 (delivery, dedup incl. 5 parallel checks, cancel/reschedule generation, completion suppression, delayed/missed issuance, unread transfer, read history, issuance-vs-reassignment and issuance-vs-completion lock waits, deadlock retry of `ReassignAgentTask` → 200 with exactly one transfer), 7 (SQL shapes from EF command log), 10 (quick replies), 11 (collaboration incl. F3 removal-vs-post and assignment-lock-vs-post waits and 5 parallel invites → 1 created / 4 × 409), 12 (OpenAPI).
- **Not executed:** Verification 13 browser/UI checks (responsive layout, Retry states, bell badge in two tabs, clipboard paste, two-tab 409 flow, API-down dashboard) — no browser tool was available. Commit-outcome-unknown (503) is "verified by review" only. DASH-001 regression covered by API (`GET /agent-dashboard` 200) and the unchanged-files diff, not by a browser pass.
- Notes: manual `sqlcmd` writes touching `AgentTasks`/`TicketCollaborators` need `-I` (QUOTED_IDENTIFIER). Test SQL writes touched only DASH-002 test data (lock demonstrations used rolled-back/committed updates on K4/K5 and P2 memberships). Done-criteria checkboxes above remain for the reviewer.
