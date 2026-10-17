# Story 05 — Ticket Workflow, Assignment & History

## Prerequisites

- **Story 04 completed:** [04-story-ticket-creation-management.md](04-story-ticket-creation-management.md) (`TICK-001`, commit `b44c824`): `Crm.Domain.Tickets.Ticket`, enums `TicketCategory` / `TicketPriority` / `TicketStatus { Open }` stored as `nvarchar(20)` names, `ITicketRepository` with `agentScope`, `TicketActor`, `TicketDetails`, `TicketsController` (`Staff` policy), global `JsonStringEnumConverter(allowIntegerValues: false)`, Angular `features/tickets/` (list / create / detail, `CustomerPicker`, `TicketsApi`), `docs/ticket-management.md`. This story **extends those files in place** — no second ticket module.
- **Stories 01–03 completed:** [../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md) (`IUnitOfWork`, `GlobalExceptionHandler`, `ConflictException` ⇒ 409, `DomainException` ⇒ 422), [../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md) (`IIdentityService.FindByIdAsync`, `AuthorizationPolicies.SuperAdmin` / `Staff`, SuperAdmin-only `GET /api/v1/agents`), [../customer-management/03-story-customer-profiles-contact-management.md](../customer-management/03-story-customer-profiles-contact-management.md) (`PagedResult<T>` / `Paging` / `PagedResponse<T>`).
- Applied migrations (verified in `backend/src/Crm.Infrastructure/Persistence/Migrations/`): `20261014234527_InitialCreate`, `20261015011303_AddIdentityAndRefreshSessions`, `20261017183245_AddCustomers`, `20261017194039_AddTickets`. **Never edit any of them.**
- Tooling unchanged from Story 04: .NET SDK **10.0.400** (`backend/global.json`), `dotnet-ef`, Angular / Material **22.2**, Vitest, xUnit. **No new NuGet or npm packages.**
- Intake: `.squad/stories/ticket-management/TICK-002/intake.md` (no attachments). The intake's "proposed defaults" (§3 transition matrix, §4 escalation permissions and auto-clear on Resolve) are **adopted unchanged** — running `/squad-plan` on the intake is the approval. If the user changes them, revise this plan before executing.
- Shared contracts this story changes deliberately (and must document): `TicketStatus` (3 appended members), `Ticket` (escalation fields, activity collection, row version), `TicketDetails` / `TicketResponse` (5 appended fields), `TicketListFilter` / `ListTicketsQuery` (`Escalated`), `ITicketRepository` (+1 method), `Ticket.UpdateDetails` signature, `EfUnitOfWork.SaveChangesAsync` (concurrency ⇒ 409), Angular `Ticket` / `TicketListParams` / `TICKET_STATUSES`.

---

## Story Goal

Complete core Ticket Management on top of TICK-001:

1. **Assignment of existing tickets — SuperAdmin only:** assign an unassigned ticket, reassign to another **active Agent**, or unassign (`null`). Agents never assign, claim or transfer. Visibility follows the **current** assignee through the existing SQL `agentScope`.
2. **Lifecycle:** `TicketStatus` gains `InProgress`, `Resolved`, `Closed`. Only five transitions are legal (see Product rules). SuperAdmin may transition any ticket; an Agent only tickets currently assigned to them. New tickets still start `Open`.
3. **Manual escalation:** a separate flag (not a status). SuperAdmin or the assigned Agent escalates an `Open` / `InProgress` ticket with a required reason (≤ 500); only SuperAdmin clears it; resolving an escalated ticket clears it automatically in the same save.
4. **Append-only activity history** (`TicketActivities` table): `Created`, field changes, assignee changes, status changes, escalation / clearance events, written **atomically** with the ticket change. Legacy TICK-001 tickets get **no** invented events.
5. **API:** `PUT /tickets/{id}/assignee`, `POST /tickets/{id}/transitions`, `POST` / `DELETE /tickets/{id}/escalation`, `GET /tickets/{id}/activity` (paged); `escalated` list filter; `TicketResponse` gains escalation fields. Existing `GET` / `POST` / `PUT /tickets` contracts stay backwards compatible.
6. **Angular:** the existing ticket detail page gets role-aware workflow buttons, a SuperAdmin assignment control, escalate / clear escalation, and a paged **Activity** section; the list gets the new statuses, status tones, an escalation badge and an "Escalation" filter.
7. One additive migration, exactly **one** new backend and **one** new frontend happy-path unit test, documentation updates.

**Not in scope** (do **not** build): Agent claim / transfer / self-assignment after creation, routing / workload balancing, SLA timers, auto-close, notifications (email / SMS / WhatsApp), escalation levels / queues / approvals, configurable workflows or custom statuses, comments, internal notes, attachments, ticket deletion / merge / bulk actions, Customer Portal ticket access, event sourcing / message brokers, any new test framework or integration / E2E / negative-path suites.

### Recorded decisions

| Concern | Decision |
|---|---|
| Statuses | Append `InProgress = 1`, `Resolved = 2`, `Closed = 3` to `TicketStatus` (keep `Open = 0`). Stored as names in the existing `Status nvarchar(20)` column — **no column change**, existing `Open` rows stay valid. UI label for `InProgress` is "In Progress". |
| Transition matrix | New `Crm.Domain.Tickets.TicketWorkflow.IsAllowed(from, to)` — the single source of truth. Open→InProgress, InProgress→Resolved, Resolved→Closed, Resolved→InProgress, Closed→Open. Everything else (incl. same-status) is illegal. Angular mirrors it in `ticket-workflow.ts` for button display only. |
| Transition request | `POST /api/v1/tickets/{id}/transitions` body `{ "fromStatus": "InProgress", "toStatus": "Resolved" }`. `fromStatus` is the status the user **saw**: if it differs from the stored status ⇒ **409** (stale UI never silently applies a different transition). Legal-from-current check ⇒ **409**. Missing / unknown enum ⇒ **400**. |
| Assignment | `PUT /api/v1/tickets/{id}/assignee` body `{ "assignedAgentUserId": "<guid>" \| null }`, `AuthorizationPolicies.SuperAdmin` (Agents / Customers ⇒ **403**). Same assignee as now ⇒ **no-op** (200, no event, no `UpdatedAtUtc` change, no save). Otherwise target must resolve via `IIdentityService.FindByIdAsync` to `{ Role: "Agent", IsActive: true }` else **400** `assignedAgentUserId` (same message as create). `Guid.Empty` ⇒ 400 (validator). Status, escalation, creator, customer untouched. |
| Escalation model | Columns on `Tickets`: `IsEscalated bit NOT NULL DEFAULT 0`, `EscalationReason nvarchar(500) NULL`, `EscalatedAtUtc datetimeoffset NULL`, `EscalatedByUserId uniqueidentifier NULL` (FK → `AspNetUsers`, `Restrict`). Escalate: `POST /tickets/{id}/escalation` `{ "reason": "…" }` (SuperAdmin or currently assigned Agent; reason required, trimmed, ≤ 500 raw ⇒ 400). Already escalated or status `Resolved` / `Closed` ⇒ **409**. Clear: `DELETE /tickets/{id}/escalation` (`SuperAdmin` policy; not escalated ⇒ **409**). Clearing nulls the four fields' values (flag false, reason / at / by null); history keeps the events. |
| Auto-clear | `Ticket.TransitionTo(Resolved)` on an escalated ticket clears the flag **and** appends `EscalationAutoCleared` (actor = the user who resolved) in the same `SaveChanges`. Reopen / Resume never re-escalates. |
| Activity storage | New entity `Crm.Domain.Tickets.TicketActivity`, table `TicketActivities`: `Id bigint IDENTITY` (stable, insertion-ordered tie-breaker; discovered by EF as **Added** because the key is store-generated and unset), `TicketId` (FK → `Tickets`, `Restrict`), `Type nvarchar(30)`, `ActorUserId` (FK → `AspNetUsers`, `Restrict`, required), `OccurredAtUtc`, `Field nvarchar(20) NULL`, `OldValue` / `NewValue nvarchar(200) NULL`, `FromAgentUserId` / `ToAgentUserId` (nullable FKs → `AspNetUsers`, `Restrict`), `Reason nvarchar(500) NULL`. Index `(TicketId, OccurredAtUtc DESC, Id DESC)`. Names are joined at read time (accounts are never deleted; FKs `Restrict`), so disabled users still display. No passwords / stamps / profiles copied. |
| Event types | `TicketActivityType { Created, FieldChanged, AssigneeChanged, StatusChanged, Escalated, EscalationCleared, EscalationAutoCleared }` stored by name. `Created` stores the initial assignee in `ToAgentUserId`. `FieldChanged` = **one row per changed field** (`Field` = `Subject` / `Description` / `Category` / `Priority`); Subject / Category / Priority store old + new values; **Description stores no values** (only that it changed — avoids duplicating up to 2×4000 chars). `StatusChanged` stores status names in `OldValue` / `NewValue`. `AssigneeChanged` uses `FromAgentUserId` / `ToAgentUserId`. `Escalated` stores `Reason`. |
| Atomicity | Activities are owned by the `Ticket` aggregate: every mutating domain method appends its own event(s) to the private `_activities` list, so a change **cannot** be made without its event. Handlers save once (`IUnitOfWork.SaveChangesAsync`) — EF inserts activities and updates the ticket in **one implicit transaction**. Commands are therefore **not** `ITransactionalCommand` (`docs/persistence-and-transactions.md` line 4). **Invariants the executor must keep:** (1) each handler calls `SaveChangesAsync` **at most once**, after all domain calls (no save between a ticket change and its events; the read-back is a separate no-tracking query, not a save); (2) every domain method that appends an event also changes a mapped `Tickets` column (at least `UpdatedAtUtc`), so the ticket `UPDATE` (with its row-version check) and the activity `INSERT`s run in the same EF transaction — a row-version failure rolls back the inserts; `Created` travels with the ticket `INSERT`; (3) handlers that return without saving (no-op assignment, no-op `PUT`) have appended **no** events (`ChangeAssignee` / `UpdateDetails` return `false` before recording anything); (4) every rejection (validation, 404, 409) is thrown before the save, and the request-scoped `CrmDbContext` is discarded with the request, so staged events can never reach a later save. `TicketActivity` has no update/delete method; the API exposes no edit/delete. |
| Concurrency | Two **separate** mechanisms, documented as such. **(a) Overlapping requests — server-side row version.** Shadow `rowversion` property `Version` on `Tickets` (`IsRowVersion`). The `Version` checked is the one EF read in the **same request** (`GetByIdAsync`); it is **never sent to or accepted from the client**. It therefore only detects a write that commits **between this request's load and its save**: the loser's `UPDATE … WHERE Version = @loaded` affects 0 rows ⇒ `DbUpdateConcurrencyException` ⇒ `EfUnitOfWork.SaveChangesAsync` ⇒ `ConflictException` ⇒ **409** "The record was changed by another request. Reload it and try again." (nothing saved, no event). It does **not** detect a stale browser page. **(b) Stale client submissions — explicit request preconditions, re-checked against the freshly loaded row.** Transition: `fromStatus` must equal the stored status ⇒ 409. Escalate: not already escalated and Open/InProgress ⇒ 409 otherwise. Clear: currently escalated ⇒ 409 otherwise. Assignment: same assignee ⇒ no-op; otherwise the SuperAdmin's explicit choice is applied (no precondition). Basic edit `PUT`: **no precondition** — a stale form still overwrites (TICK-001 last-write-wins against stale pages is **unchanged** and documented; a client-supplied version would change the TICK-001 contract and is deferred). Net change vs TICK-001: only truly overlapping `PUT`s now get 409 instead of interleaving. No locks, no new infrastructure. |
| No-op basic edit | `PUT /tickets/{id}` with values identical to the stored ones ⇒ 200, **no** save, **no** event, `UpdatedAtUtc` unchanged (before: timestamp bumped). Contract (request / response / 404 scoping) otherwise unchanged; it still cannot edit assignment / status / escalation. |
| Activity read | `GET /api/v1/tickets/{id}/activity?page=1&pageSize=20` — Story 03 paging rules (one-based, default 20, clamp 100, max page 1 000 000), `PagedResponse<TicketActivityResponse>`, ordered `OccurredAtUtc DESC, Id DESC`. Visibility = ticket visibility under `agentScope` (404 otherwise), joined into the same SQL. Response has names, not user ids. |
| Legacy tickets | No backfill. A ticket with no `Created` event pre-dates TICK-002: when the last page is loaded and no `Created` event is present, the UI shows "History tracking started with TICK-002; earlier activity was not recorded." |
| Role checks | Controller policies are authoritative (`SuperAdmin` for assignee + clear, `Staff` for the rest). Handlers for SuperAdmin-only commands additionally throw `InvalidOperationException` if called with a non-SuperAdmin actor (programming-error guard ⇒ 500, never reachable through HTTP). Agent record access = `agentScope` load ⇒ 404. |
| Error codes | 400 = malformed / validation (missing reason, bad enum, invalid assignee). 403 = role (policy). 404 = unknown or outside scope. 409 = state conflicts (stale `fromStatus`, illegal transition, already escalated, escalate Resolved/Closed, clear when not escalated, row-version conflict). 422 (`DomainException`) only if a handler check is bypassed — never expected. |

---

## Context — Read These Files First

1. `.squad/stories/ticket-management/TICK-002/intake.md` — §2 assignment rules, §3 transition matrix, §4 escalation, §5 history, §6 API, §7 UI and the Acceptance criteria are the contract.
2. [04-story-ticket-creation-management.md](04-story-ticket-creation-management.md) — "Recorded decisions" (~lines 27–47) and tasks 1–7 / 8–14: layering, file naming, test style this story extends.
3. `backend/src/Crm.Domain/Tickets/Ticket.cs` — whole file (59 lines): `private Ticket()`, `private set` properties (lines 12–22), `Create(...)` (lines 25–48), `UpdateDetails(...)` (lines 51–58, the only caller is `UpdateTicketCommandHandler`). `TicketStatus.cs` lines 7–10 (`Open = 0`). `Crm.Domain/Common/DomainException.cs` line 4.
4. `backend/src/Crm.Application/Tickets/` — `TicketActor.cs` lines 7–11 (`AgentScope`), `TicketDetails.cs` lines 9–24 (record) and 27–32 (`TicketListFilter`), `CreateTicket/CreateTicketCommand.cs` lines 64–92 (`ResolveInitialAssigneeAsync` active-Agent check + private `Invalid(...)` helper — copy for assignment), `UpdateTicket/UpdateTicketCommand.cs` lines 22–39 (load tracked under scope ⇒ 404, save once, read back with `Actor.AgentScope`), `GetTicket/GetTicketQuery.cs` lines 9–14, `ListTickets/ListTicketsQuery.cs` lines 8–29 and `ListTicketsValidator.cs` lines 9–17.
5. `backend/src/Crm.Application/Abstractions/Persistence/ITicketRepository.cs` lines 12–24, `IUnitOfWork.cs` lines 7–20; `Common/Exceptions/ConflictException.cs` line 4; `Common/Paging/PagedResult.cs` lines 4–11; `Common/Validation/TicketRules.cs` lines 3–8.
6. `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs` — `GetByIdAsync` (13–14), `FindDetailsByIdAsync` (16–17), `ListAsync` filters (19–60), `Scope` (62–64), `Project` (69–90, left join pattern `dbContext.Users.Where(...).DefaultIfEmpty()` and ordering inside the projection).
7. `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketConfiguration.cs` lines 11–32 (string enums, `Restrict` FKs to `ApplicationUser`, descending indexes). `CrmDbContext.cs` lines 16–18 (`DbSet`s). `EfUnitOfWork.cs` lines 8–9 (`SaveChangesAsync`) and 11–44 (transaction path — leave unchanged).
8. `backend/src/Crm.Api/Controllers/TicketsController.cs` — whole file (70 lines): `EndpointDescription` style, `Actor` property (line 69). `Contracts/Tickets/TicketResponse.cs` lines 7–27, `UpdateTicketRequest.cs` line 6. `Authentication/AuthorizationPolicies.cs` + `SecurityRegistration.cs` lines 61–65 (`SuperAdmin`, `Staff` policies; `AgentsController.cs` line 17 shows `[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]`). `Errors/GlobalExceptionHandler.cs` lines 20–60 (400 / 404 / 422 / 409 mapping — **no change**).
9. `backend/src/Crm.Infrastructure/Persistence/Migrations/20261017194039_AddTickets.cs` — reference only (column types); **do not edit**.
10. `backend/tests/Crm.UnitTests/Tickets/CreateTicketCommandHandlerTests.cs` — whole file: fakes (`FakeTicketRepository` lines 50–65 constructs `TicketDetails` positionally at lines 58–60 — needs the 5 new arguments; `FakeUnitOfWork` lines 95–101). Test project references only `Crm.Application` (`Crm.UnitTests.csproj`), which references `Crm.Domain`.
11. `frontend/src/app/features/tickets/ticket-models.ts` — whole file (69 lines): `TICKET_STATUSES` (line 7), `Ticket` (14–30), `TicketListParams` (32–40), `priorityTone` (60–69).
12. `frontend/src/app/features/tickets/tickets-api.ts` lines 13–45 and `tickets-api.spec.ts` (whole: spec style; its `list({...})` literal has **no** `escalated` key — keep it compiling by making the new param optional).
13. `frontend/src/app/features/tickets/ticket-detail-page.ts` (109 lines: load in constructor 59–68, `show` 70–78, `save` 80–108) and `.html` (93 lines: Summary card 11–40 with the "Assignment and status are read-only here." hint at line 38, Edit card 42–91), `.scss` (`.detail-stack`, `.readonly-list`, `.hint`, `.form-grid`, `.form-error`).
14. `frontend/src/app/features/tickets/ticket-list-page.ts` (filters form 70–75, `hasFilters` 123–126, `load` 133–146) and `.html` (status filter 159–167, status cell 236–239).
15. `frontend/src/app/features/tickets/ticket-create-page.ts` ~lines 45–80 — how SuperAdmin-only `AgentsApi.list()` is loaded and filtered by `isActive` (copy for the assignment control). `features/agents/agents-api.ts` lines 12–14, `agent-models.ts` lines 2–10.
16. `frontend/src/app/core/auth/auth-service.ts` line 19 `currentUser` (has `id` and `role`, `auth-models.ts` lines 4–11). `shared/ui/status-badge/status-badge.ts` line 3 (`StatusTone`). `core/api/describe-http-error.ts` lines 4–14.
17. Docs: `docs/ticket-management.md` (whole, 70 lines), `docs/api-conventions.md` line 19, `docs/identity-and-access.md` lines 16–17 (matrix) and 36–37 (routes), `docs/persistence-and-transactions.md` lines 4–5, `docs/frontend-ui-foundation.md` line 11, `README.md` line 3.

---

## Product rules (from story)

| Situation | Before (TICK-001) | New (TICK-002) |
|---|---|---|
| Change assignee of existing ticket | impossible | SuperAdmin only: unassigned→Agent, Agent A→Agent B, Agent→Unassigned. Active Agents only. Agents: 403. |
| Creation-time assignment | Agent ⇒ self; SuperAdmin ⇒ active Agent or null | **unchanged** (+ `Created` event) |
| Agent visibility | tickets assigned to them | **unchanged rule**, now dynamic: reassignment moves list/detail/edit/history access immediately (404 for the former Agent) |
| Statuses | `Open` only | `Open`, `InProgress`, `Resolved`, `Closed`; new tickets `Open` |
| Transitions | none | Open→InProgress ("Start progress"), InProgress→Resolved ("Resolve"), Resolved→Closed ("Close"), Resolved→InProgress ("Resume"), Closed→Open ("Reopen"). SuperAdmin: any ticket (incl. unassigned, no auto-assign). Agent: only currently assigned. |
| Escalation | none | Flag + reason; SuperAdmin or assigned Agent raises on Open/InProgress; only SuperAdmin clears; Resolve auto-clears. |
| `PUT /tickets/{id}` | edits 4 fields, always bumps `UpdatedAtUtc` | edits the same 4 fields; records one `FieldChanged` per changed field; no-op ⇒ no save / event / timestamp change |
| History | none | append-only, newest-first, paged, same visibility as the ticket; legacy tickets show the tracking-start note |

---

## Backend Tasks

All paths relative to `backend/src/` unless stated.

### 1 — Domain: statuses, workflow, activity, ticket operations

**File: `Crm.Domain/Tickets/TicketStatus.cs`** — replace the enum body and summary (do **not** renumber `Open`):

```csharp
namespace Crm.Domain.Tickets;

/// <summary>
/// System-managed lifecycle, changed only through Ticket.TransitionTo (rules in TicketWorkflow).
/// Stored as names, so the TICK-002 members were appended without remapping existing rows.
/// </summary>
public enum TicketStatus
{
    Open = 0,
    InProgress = 1,
    Resolved = 2,
    Closed = 3,
}
```

**Create file: `Crm.Domain/Tickets/TicketWorkflow.cs`**

```csharp
namespace Crm.Domain.Tickets;

/// <summary>TICK-002 transition matrix — the single source of truth. Every pair not listed is illegal.</summary>
public static class TicketWorkflow
{
    public static bool IsAllowed(TicketStatus from, TicketStatus to) => (from, to) switch
    {
        (TicketStatus.Open, TicketStatus.InProgress) => true,     // begin handling
        (TicketStatus.InProgress, TicketStatus.Resolved) => true, // work completed
        (TicketStatus.Resolved, TicketStatus.Closed) => true,     // finish
        (TicketStatus.Resolved, TicketStatus.InProgress) => true, // resolution unsuccessful; resume
        (TicketStatus.Closed, TicketStatus.Open) => true,         // reopen for a new work cycle
        _ => false,
    };

    /// <summary>Manual escalation is only for active work.</summary>
    public static bool CanEscalate(TicketStatus status) => status is TicketStatus.Open or TicketStatus.InProgress;
}
```

**Create file: `Crm.Domain/Tickets/TicketActivityType.cs`**

```csharp
namespace Crm.Domain.Tickets;

/// <summary>Ticket history event kinds. Stored by name.</summary>
public enum TicketActivityType
{
    Created = 0,
    FieldChanged = 1,
    AssigneeChanged = 2,
    StatusChanged = 3,
    Escalated = 4,
    EscalationCleared = 5,
    EscalationAutoCleared = 6,
}
```

**Create file: `Crm.Domain/Tickets/TicketField.cs`**

```csharp
namespace Crm.Domain.Tickets;

/// <summary>Basic-edit fields recorded by FieldChanged events. Stored by name.</summary>
public enum TicketField
{
    Subject = 0,
    Description = 1,
    Category = 2,
    Priority = 3,
}
```

**Create file: `Crm.Domain/Tickets/TicketActivity.cs`**

```csharp
namespace Crm.Domain.Tickets;

/// <summary>
/// Append-only history entry, created only by Ticket's own operations (so a change cannot be saved without its event).
/// Id is a store-generated identity (insertion order = deterministic tie-breaker). Never updated or deleted.
/// User ids reference AspNetUsers; display names are joined at read time.
/// </summary>
public sealed class TicketActivity
{
    private TicketActivity() { } // EF Core

    public long Id { get; private set; }
    public Guid TicketId { get; private set; }
    public TicketActivityType Type { get; private set; }
    public Guid ActorUserId { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public TicketField? Field { get; private set; }
    public string? OldValue { get; private set; }
    public string? NewValue { get; private set; }
    public Guid? FromAgentUserId { get; private set; }
    public Guid? ToAgentUserId { get; private set; }
    public string? Reason { get; private set; }

    internal static TicketActivity Created(Guid ticketId, Guid actorUserId, Guid? assignedAgentUserId, DateTimeOffset now) =>
        New(ticketId, TicketActivityType.Created, actorUserId, now, toAgentUserId: assignedAgentUserId);

    internal static TicketActivity FieldChanged(Guid ticketId, Guid actorUserId, DateTimeOffset now, TicketField field, string? oldValue, string? newValue) =>
        New(ticketId, TicketActivityType.FieldChanged, actorUserId, now, field: field, oldValue: oldValue, newValue: newValue);

    internal static TicketActivity AssigneeChanged(Guid ticketId, Guid actorUserId, DateTimeOffset now, Guid? fromAgentUserId, Guid? toAgentUserId) =>
        New(ticketId, TicketActivityType.AssigneeChanged, actorUserId, now, fromAgentUserId: fromAgentUserId, toAgentUserId: toAgentUserId);

    internal static TicketActivity StatusChanged(Guid ticketId, Guid actorUserId, DateTimeOffset now, TicketStatus from, TicketStatus to) =>
        New(ticketId, TicketActivityType.StatusChanged, actorUserId, now, oldValue: from.ToString(), newValue: to.ToString());

    internal static TicketActivity Escalated(Guid ticketId, Guid actorUserId, DateTimeOffset now, string reason) =>
        New(ticketId, TicketActivityType.Escalated, actorUserId, now, reason: reason);

    internal static TicketActivity EscalationCleared(Guid ticketId, Guid actorUserId, DateTimeOffset now, bool automatic) =>
        New(ticketId, automatic ? TicketActivityType.EscalationAutoCleared : TicketActivityType.EscalationCleared, actorUserId, now);

    private static TicketActivity New(
        Guid ticketId, TicketActivityType type, Guid actorUserId, DateTimeOffset now,
        TicketField? field = null, string? oldValue = null, string? newValue = null,
        Guid? fromAgentUserId = null, Guid? toAgentUserId = null, string? reason = null) =>
        new()
        {
            TicketId = ticketId,
            Type = type,
            ActorUserId = actorUserId,
            OccurredAtUtc = now,
            Field = field,
            OldValue = oldValue,
            NewValue = newValue,
            FromAgentUserId = fromAgentUserId,
            ToAgentUserId = toAgentUserId,
            Reason = reason,
        };
}
```

**File: `Crm.Domain/Tickets/Ticket.cs`** — keep every existing property and `Create`'s signature. Changes:

1. Replace the class summary line 6 with: `/// TICK-002: customer and creator never change; assignment, status and escalation change only through the operations below, each of which appends its own TicketActivity.`
2. Add after `UpdatedAtUtc` (line 22):

```csharp
    public bool IsEscalated { get; private set; }
    public string? EscalationReason { get; private set; }
    public DateTimeOffset? EscalatedAtUtc { get; private set; }
    public Guid? EscalatedByUserId { get; private set; }

    // History owned by the aggregate. Loaded tickets start with an EMPTY list (never Include-d):
    // it only holds the events appended in this unit of work, which EF inserts on save.
    private readonly List<TicketActivity> _activities = [];
    public IReadOnlyCollection<TicketActivity> Activities => _activities;
```

3. In `Create`, change the expression-bodied factory to a block that appends the `Created` event:

```csharp
    {
        var ticket = new Ticket
        {
            // … existing initializer lines 37–47 unchanged …
        };
        ticket._activities.Add(TicketActivity.Created(id, createdByUserId, assignedAgentUserId, now));
        return ticket;
    }
```

4. Replace `UpdateDetails` (lines 50–58) with:

```csharp
    /// <summary>Basic edit (only these four fields). One FieldChanged event per changed field; false = nothing changed (no event, no timestamp).</summary>
    public bool UpdateDetails(
        string subject, string description, TicketCategory category, TicketPriority priority, Guid actorUserId, DateTimeOffset now)
    {
        var changed = false;
        if (!string.Equals(Subject, subject, StringComparison.Ordinal))
        {
            Record(TicketActivity.FieldChanged(Id, actorUserId, now, TicketField.Subject, Subject, subject));
            Subject = subject;
            changed = true;
        }

        if (!string.Equals(Description, description, StringComparison.Ordinal))
        {
            // Values not copied into history (up to 4000 chars each); the event only says it changed.
            Record(TicketActivity.FieldChanged(Id, actorUserId, now, TicketField.Description, null, null));
            Description = description;
            changed = true;
        }

        if (Category != category)
        {
            Record(TicketActivity.FieldChanged(Id, actorUserId, now, TicketField.Category, Category.ToString(), category.ToString()));
            Category = category;
            changed = true;
        }

        if (Priority != priority)
        {
            Record(TicketActivity.FieldChanged(Id, actorUserId, now, TicketField.Priority, Priority.ToString(), priority.ToString()));
            Priority = priority;
            changed = true;
        }

        if (changed)
        {
            UpdatedAtUtc = now;
        }

        return changed;
    }

    /// <summary>The caller has verified the target is an active Agent (or null = unassigned). false = same assignee (no-op).</summary>
    public bool ChangeAssignee(Guid? assignedAgentUserId, Guid actorUserId, DateTimeOffset now)
    {
        if (AssignedAgentUserId == assignedAgentUserId)
        {
            return false;
        }

        Record(TicketActivity.AssigneeChanged(Id, actorUserId, now, AssignedAgentUserId, assignedAgentUserId));
        AssignedAgentUserId = assignedAgentUserId;
        UpdatedAtUtc = now;
        return true;
    }

    /// <summary>Resolving an escalated ticket also clears the escalation (recorded as EscalationAutoCleared).</summary>
    public void TransitionTo(TicketStatus status, Guid actorUserId, DateTimeOffset now)
    {
        if (!TicketWorkflow.IsAllowed(Status, status))
        {
            throw new DomainException($"A ticket cannot move from {Status} to {status}.");
        }

        Record(TicketActivity.StatusChanged(Id, actorUserId, now, Status, status));
        Status = status;
        if (status == TicketStatus.Resolved && IsEscalated)
        {
            ClearEscalationState();
            Record(TicketActivity.EscalationCleared(Id, actorUserId, now, automatic: true));
        }

        UpdatedAtUtc = now;
    }

    public void Escalate(string reason, Guid actorUserId, DateTimeOffset now)
    {
        if (IsEscalated || !TicketWorkflow.CanEscalate(Status))
        {
            throw new DomainException("Only an Open or In Progress ticket that is not already escalated can be escalated.");
        }

        IsEscalated = true;
        EscalationReason = reason;
        EscalatedAtUtc = now;
        EscalatedByUserId = actorUserId;
        Record(TicketActivity.Escalated(Id, actorUserId, now, reason));
        UpdatedAtUtc = now;
    }

    public void ClearEscalation(Guid actorUserId, DateTimeOffset now)
    {
        if (!IsEscalated)
        {
            throw new DomainException("The ticket is not escalated.");
        }

        ClearEscalationState();
        Record(TicketActivity.EscalationCleared(Id, actorUserId, now, automatic: false));
        UpdatedAtUtc = now;
    }

    private void ClearEscalationState()
    {
        IsEscalated = false;
        EscalationReason = null;
        EscalatedAtUtc = null;
        EscalatedByUserId = null;
    }

    private void Record(TicketActivity activity) => _activities.Add(activity);
```

Add `using Crm.Domain.Common;` at the top. `Create` keeps using `ticket._activities.Add(...)` (or `ticket.Record(...)`). **Do not** add any method that changes `CustomerId`, `CreatedByUserId` or `CreatedAtUtc`.

### 2 — Application: read models, rules, repository contract

**File: `Crm.Application/Tickets/TicketDetails.cs`**

- **Append** to `TicketDetails` after `DateTimeOffset UpdatedAtUtc` (line 24 — move the `);`):

```csharp
    DateTimeOffset UpdatedAtUtc,
    bool IsEscalated,
    string? EscalationReason,
    DateTimeOffset? EscalatedAtUtc,
    Guid? EscalatedByUserId,
    string? EscalatedByName);
```

- Append `bool? Escalated` to `TicketListFilter` (after `TicketPriority? Priority`): `/// Escalated: null = any; true = escalated only; false = not escalated.`

**Create file: `Crm.Application/Tickets/TicketActivityDetails.cs`**

```csharp
using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>History read model. Names joined from AspNetUsers at query time; no user ids or Identity fields exposed.</summary>
public sealed record TicketActivityDetails(
    long Id,
    TicketActivityType Type,
    DateTimeOffset OccurredAtUtc,
    string ActorName,
    TicketField? Field,
    string? OldValue,
    string? NewValue,
    string? FromAgentName,
    string? ToAgentName,
    string? Reason);
```

**File: `Crm.Application/Common/Validation/TicketRules.cs`** — add `public const int EscalationReasonMaxLength = 500;`.

**File: `Crm.Application/Abstractions/Persistence/ITicketRepository.cs`** — add (keep existing members untouched):

```csharp
    /// <summary>
    /// Newest first (OccurredAtUtc DESC, Id DESC). Null when the ticket is missing or outside agentScope —
    /// the same visibility rule as the ticket itself, applied in SQL.
    /// </summary>
    Task<PagedResult<TicketActivityDetails>?> ListActivityAsync(
        Guid ticketId, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default);
```

### 3 — Application: use cases

Shared pattern for every command below (copy `UpdateTicketCommandHandler`, `UpdateTicket/UpdateTicketCommand.cs` lines 22–39): load **tracked** with `tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, ct)` ⇒ `NotFoundException($"Ticket '{command.Id}' was not found.")`; check state ⇒ `ConflictException`; call one domain method with `command.Actor.UserId` and `timeProvider.GetUtcNow()`; `await unitOfWork.SaveChangesAsync(ct)` **once**; return `(await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, ct))!`. None implements `ITransactionalCommand`. Never pass `agentScope: null` for an actor.

**3a — File: `Crm.Application/Tickets/UpdateTicket/UpdateTicketCommand.cs`** — update the summary to `/// Only Subject/Description/Category/Priority, each actual change recorded as a FieldChanged event. Single save — not transactional; no-op = no save.` and replace lines 28–35 with:

```csharp
        var changed = ticket.UpdateDetails(
            command.Subject.Trim(),
            command.Description.Trim(),
            command.Category!.Value, // validator: NotNull
            command.Priority!.Value, // validator: NotNull
            command.Actor.UserId,
            timeProvider.GetUtcNow());

        if (changed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
```

Validator unchanged.

**3b — Create file: `Crm.Application/Tickets/ChangeAssignee/ChangeTicketAssigneeCommand.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Identity;
using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace Crm.Application.Tickets.ChangeAssignee;

/// <summary>SuperAdmin only (API policy). null = unassign. Same assignee = no-op. Single save — not transactional.</summary>
public sealed record ChangeTicketAssigneeCommand(TicketActor Actor, Guid Id, Guid? AssignedAgentUserId) : ICommand<TicketDetails>;

public sealed class ChangeTicketAssigneeCommandHandler(
    ITicketRepository tickets,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ChangeTicketAssigneeCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(ChangeTicketAssigneeCommand command, CancellationToken cancellationToken)
    {
        if (!command.Actor.IsSuperAdmin)
        {
            throw new InvalidOperationException("Only SuperAdmin can change a ticket's assignee."); // guarded by the SuperAdmin policy
        }

        var ticket = await tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.Id}' was not found.");

        // No-op first: re-submitting the current (possibly since-disabled) assignee changes nothing and is not an error.
        if (ticket.AssignedAgentUserId != command.AssignedAgentUserId)
        {
            if (command.AssignedAgentUserId is { } agentId)
            {
                var agent = await identity.FindByIdAsync(agentId, cancellationToken);
                if (agent is not { Role: UserRoles.Agent, IsActive: true })
                {
                    throw new ValidationException([new ValidationFailure(
                        nameof(command.AssignedAgentUserId), "Select an active Agent or leave the ticket unassigned.")]);
                }
            }

            ticket.ChangeAssignee(command.AssignedAgentUserId, command.Actor.UserId, timeProvider.GetUtcNow());
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }
}
```

**Create file: `Crm.Application/Tickets/ChangeAssignee/ChangeTicketAssigneeValidator.cs`** — `RuleFor(c => c.Id).NotEmpty(); RuleFor(c => c.AssignedAgentUserId).NotEqual(Guid.Empty);` (style of `CreateTicketValidator.cs` line 15).

**3c — Create file: `Crm.Application/Tickets/TransitionTicket/TransitionTicketStatusCommand.cs`**

```csharp
/// <summary>
/// SuperAdmin: any ticket. Agent: only tickets currently assigned (agentScope ⇒ 404).
/// FromStatus is the status the caller saw; a mismatch is a stale request (409), never silently re-targeted.
/// Nullable enums only so a missing JSON field fails validation. Single save — not transactional.
/// </summary>
public sealed record TransitionTicketStatusCommand(TicketActor Actor, Guid Id, TicketStatus? FromStatus, TicketStatus? ToStatus)
    : ICommand<TicketDetails>;

public sealed class TransitionTicketStatusCommandHandler(
    ITicketRepository tickets,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<TransitionTicketStatusCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(TransitionTicketStatusCommand command, CancellationToken cancellationToken)
    {
        var ticket = await tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.Id}' was not found.");

        var from = command.FromStatus!.Value; // validator: NotNull
        var to = command.ToStatus!.Value;     // validator: NotNull
        if (ticket.Status != from)
        {
            throw new ConflictException($"The ticket is now {ticket.Status}, not {from}. Reload it and try again.");
        }

        if (!TicketWorkflow.IsAllowed(from, to))
        {
            throw new ConflictException($"A ticket cannot move from {from} to {to}.");
        }

        ticket.TransitionTo(to, command.Actor.UserId, timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }
}
```

Usings: `Crm.Application.Abstractions.Persistence`, `Crm.Application.Common.Exceptions`, `Crm.Domain.Tickets`, `Mediator`; namespace `Crm.Application.Tickets.TransitionTicket`.

**Create file: `Crm.Application/Tickets/TransitionTicket/TransitionTicketStatusValidator.cs`** — `Id` NotEmpty; `FromStatus` and `ToStatus` `.NotNull().IsInEnum()`.

**3d — Create file: `Crm.Application/Tickets/EscalateTicket/EscalateTicketCommand.cs`** — record `EscalateTicketCommand(TicketActor Actor, Guid Id, string Reason) : ICommand<TicketDetails>`; handler (`ITicketRepository`, `IUnitOfWork`, `TimeProvider`): scoped load ⇒ 404; `if (ticket.IsEscalated) throw new ConflictException("The ticket is already escalated.");` `if (!TicketWorkflow.CanEscalate(ticket.Status)) throw new ConflictException($"A {ticket.Status} ticket cannot be escalated.");` then `ticket.Escalate(command.Reason.Trim(), command.Actor.UserId, timeProvider.GetUtcNow());`, save once, read back.

**Create file: `Crm.Application/Tickets/EscalateTicket/EscalateTicketValidator.cs`** — `Id` NotEmpty; `RuleFor(c => c.Reason).NotEmpty().MaximumLength(TicketRules.EscalationReasonMaxLength);` (NotEmpty rejects whitespace-only, as in `CreateTicketValidator` line 11).

**3e — Create file: `Crm.Application/Tickets/ClearTicketEscalation/ClearTicketEscalationCommand.cs`** — record `ClearTicketEscalationCommand(TicketActor Actor, Guid Id) : ICommand<TicketDetails>`; handler: same `InvalidOperationException` SuperAdmin guard as 3b; scoped load ⇒ 404; `if (!ticket.IsEscalated) throw new ConflictException("The ticket is not escalated.");` `ticket.ClearEscalation(command.Actor.UserId, now)`; save once; read back. Validator file `ClearTicketEscalationValidator.cs`: `Id` NotEmpty.

**3f — Create file: `Crm.Application/Tickets/ListTicketActivity/ListTicketActivityQuery.cs`**

```csharp
public sealed record ListTicketActivityQuery(TicketActor Actor, Guid TicketId, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<TicketActivityDetails>>;

public sealed class ListTicketActivityQueryHandler(ITicketRepository tickets)
    : IQueryHandler<ListTicketActivityQuery, PagedResult<TicketActivityDetails>>
{
    public async ValueTask<PagedResult<TicketActivityDetails>> Handle(ListTicketActivityQuery query, CancellationToken cancellationToken)
    {
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize); // documented cap
        // Same visibility as the ticket: a former assignee gets 404 immediately after reassignment.
        return await tickets.ListActivityAsync(query.TicketId, query.Actor.AgentScope, query.Page, pageSize, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{query.TicketId}' was not found.");
    }
}
```

**Create file: `Crm.Application/Tickets/ListTicketActivity/ListTicketActivityValidator.cs`** — `TicketId` NotEmpty; `Page` `InclusiveBetween(1, Paging.MaxPage)`; `PageSize` `GreaterThanOrEqualTo(1)` (copy `ListTicketsValidator.cs` lines 11–12).

**3g — File: `Crm.Application/Tickets/ListTickets/ListTicketsQuery.cs`** — insert `bool? Escalated,` after `TicketPriority? Priority,` (line 14) and pass it: `new TicketListFilter(search, query.CustomerId, query.Status, query.Category, query.Priority, query.Escalated)` (line 25). `ListTicketsValidator` needs no change (`IsInEnum` on `Status` now accepts the new members automatically).

`CreateTicketCommandHandler` — **no code change**: `Ticket.Create` appends `Created` and `tickets.Add(ticket)` adds the whole graph.

### 4 — Infrastructure: EF configuration, DbSet, repository, concurrency

**Create file: `Crm.Infrastructure/Persistence/Configurations/TicketActivityConfiguration.cs`**

```csharp
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class TicketActivityConfiguration : IEntityTypeConfiguration<TicketActivity>
{
    public void Configure(EntityTypeBuilder<TicketActivity> builder)
    {
        builder.ToTable("TicketActivities");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd(); // bigint IDENTITY: append order breaks timestamp ties

        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Field).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.OldValue).HasMaxLength(200);
        builder.Property(a => a.NewValue).HasMaxLength(200);
        builder.Property(a => a.Reason).HasMaxLength(500);

        // References only; nothing cascades (accounts and tickets are never deleted).
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.FromAgentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ToAgentUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TicketId, a.OccurredAtUtc, a.Id }).IsDescending(false, true, true); // newest-first history page
    }
}
```

**File: `Crm.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`** — add inside `Configure` (after line 27, before the indexes):

```csharp
        // TICK-002 escalation flag (not a status). IsEscalated defaults to false for existing rows.
        builder.Property(t => t.EscalationReason).HasMaxLength(500);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.EscalatedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Optimistic concurrency: two simultaneous writes to one ticket cannot both commit (EfUnitOfWork ⇒ 409).
        builder.Property<byte[]>("Version").IsRowVersion();

        // Append-only history owned by the ticket; inserted in the same SaveChanges as the ticket change.
        builder.HasMany(t => t.Activities).WithOne().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(t => t.Activities).HasField("_activities").UsePropertyAccessMode(PropertyAccessMode.Field);
```

Update the line 19 comment to `// Fixed sets stored by name: readable rows; TICK-002 appended statuses without remapping numbers.` Do not change the existing three indexes.

**File: `Crm.Infrastructure/Persistence/CrmDbContext.cs`** — add after line 18: `public DbSet<TicketActivity> TicketActivities => Set<TicketActivity>();`

**File: `Crm.Infrastructure/Persistence/EfUnitOfWork.cs`** — replace lines 8–9 with:

```csharp
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // A row version (Tickets.Version) changed since this request loaded the row; nothing was saved.
            throw new ConflictException("The record was changed by another request. Reload it and try again.");
        }
    }
```

Add `using Crm.Application.Common.Exceptions;`. Leave `ExecuteInTransactionAsync` (lines 11–44) unchanged.

**File: `Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs`**

1. In `ListAsync`, after the priority filter (line 42) add:

```csharp
        if (filter.Escalated is { } escalated)
        {
            query = query.Where(t => t.IsEscalated == escalated);
        }
```

2. In `Project` add a left join for the escalating user and the 5 new constructor arguments (after `t.UpdatedAtUtc`):

```csharp
        from escalatedBy in dbContext.Users.Where(u => u.Id == t.EscalatedByUserId).DefaultIfEmpty()
        // … existing orderby …
            t.UpdatedAtUtc,
            t.IsEscalated,
            t.EscalationReason,
            t.EscalatedAtUtc,
            t.EscalatedByUserId,
            escalatedBy == null ? null : escalatedBy.FirstName + " " + escalatedBy.LastName);
```

3. Add `ListActivityAsync`:

```csharp
    public async Task<PagedResult<TicketActivityDetails>?> ListActivityAsync(
        Guid ticketId, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var visibleTicket = Scope(dbContext.Tickets.AsNoTracking(), agentScope).Where(t => t.Id == ticketId);
        if (!await visibleTicket.AnyAsync(cancellationToken))
        {
            return null;
        }

        // The scope is part of every statement, so a reassignment between the two queries cannot leak events.
        var activities =
            from a in dbContext.TicketActivities.AsNoTracking()
            join t in visibleTicket on a.TicketId equals t.Id
            select a;

        var totalCount = await activities.CountAsync(cancellationToken);
        var items = await (
                from a in activities
                join actor in dbContext.Users on a.ActorUserId equals actor.Id
                from fromAgent in dbContext.Users.Where(u => u.Id == a.FromAgentUserId).DefaultIfEmpty()
                from toAgent in dbContext.Users.Where(u => u.Id == a.ToAgentUserId).DefaultIfEmpty()
                orderby a.OccurredAtUtc descending, a.Id descending
                select new TicketActivityDetails(
                    a.Id,
                    a.Type,
                    a.OccurredAtUtc,
                    actor.FirstName + " " + actor.LastName,
                    a.Field,
                    a.OldValue,
                    a.NewValue,
                    fromAgent == null ? null : fromAgent.FirstName + " " + fromAgent.LastName,
                    toAgent == null ? null : toAgent.FirstName + " " + toAgent.LastName,
                    a.Reason))
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TicketActivityDetails>(items, page, pageSize, totalCount);
    }
```

`GetByIdAsync` stays as is (no `Include` of `Activities` — the collection is write-only per request). No DI change (`TicketRepository` already registered at `Crm.Infrastructure/DependencyInjection.cs` line 40).

### 5 — API: contracts and controller

**File: `Crm.Api/Contracts/Tickets/TicketResponse.cs`** — append `bool IsEscalated, string? EscalationReason, DateTimeOffset? EscalatedAtUtc, Guid? EscalatedByUserId, string? EscalatedByName` after `UpdatedAtUtc` and pass `t.IsEscalated, t.EscalationReason, t.EscalatedAtUtc, t.EscalatedByUserId, t.EscalatedByName` in `From`. Update the summary: `/// Customer / creator / assigned Agent / escalating user are shown by id + display name only (no Identity fields).` Existing fields keep their names and order (additive JSON change).

**Create files in `Crm.Api/Contracts/Tickets/`:**

```csharp
// ChangeTicketAssigneeRequest.cs — SuperAdmin only. null = unassigned; otherwise an active Agent's user id.
public sealed record ChangeTicketAssigneeRequest(Guid? AssignedAgentUserId);

// TransitionTicketRequest.cs — fromStatus = the status the caller saw (stale ⇒ 409); toStatus must be allowed by the workflow.
public sealed record TransitionTicketRequest(TicketStatus? FromStatus, TicketStatus? ToStatus);

// EscalateTicketRequest.cs — required, ≤ 500 characters, trimmed.
public sealed record EscalateTicketRequest(string Reason);

// TicketActivityResponse.cs
public sealed record TicketActivityResponse(
    long Id, TicketActivityType Type, DateTimeOffset OccurredAtUtc, string ActorName, TicketField? Field,
    string? OldValue, string? NewValue, string? FromAgentName, string? ToAgentName, string? Reason)
{
    public static TicketActivityResponse From(TicketActivityDetails a) =>
        new(a.Id, a.Type, a.OccurredAtUtc, a.ActorName, a.Field, a.OldValue, a.NewValue, a.FromAgentName, a.ToAgentName, a.Reason);
}
```

(Each file: `namespace Crm.Api.Contracts.Tickets;`, `using Crm.Domain.Tickets;` where enums are used, `using Crm.Application.Tickets;` for `TicketActivityResponse`.)

**File: `Crm.Api/Controllers/TicketsController.cs`**

1. `List` (lines 23–39): add `[FromQuery] bool? escalated,` after `priority`, pass it to `ListTicketsQuery(Actor, search, customerId, status, category, priority, escalated, page, pageSize)`, and extend the description with `, escalated (true/false)`.
2. Update the `Update` description (line 58) to: `"Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them). Updates subject, description, category and priority only; each changed field is recorded in the ticket activity. Customer, creator, assignment, status and escalation never change here."`
3. Add usings for the new Application namespaces and add actions (keep `Actor` at the bottom):

```csharp
    [EndpointDescription("Requires role: SuperAdmin. Assigns, reassigns or unassigns (assignedAgentUserId = null) an existing ticket. The assignee must be an active Agent (400 otherwise). Same assignee = no change. Recorded in the ticket activity.")]
    [HttpPut("{id:guid}/assignee")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    public async Task<ActionResult<TicketResponse>> ChangeAssignee(Guid id, ChangeTicketAssigneeRequest request, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new ChangeTicketAssigneeCommand(Actor, id, request.AssignedAgentUserId), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them; others 404). Allowed: Open→InProgress, InProgress→Resolved, Resolved→Closed, Resolved→InProgress, Closed→Open. fromStatus must equal the current status (409 otherwise); other transitions 409. Resolving clears an active escalation.")]
    [HttpPost("{id:guid}/transitions")]
    public async Task<ActionResult<TicketResponse>> Transition(Guid id, TransitionTicketRequest request, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new TransitionTicketStatusCommand(Actor, id, request.FromStatus, request.ToStatus), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them; others 404). Marks an Open or InProgress ticket as escalated with a required reason (max 500). Already escalated or Resolved/Closed: 409.")]
    [HttpPost("{id:guid}/escalation")]
    public async Task<ActionResult<TicketResponse>> Escalate(Guid id, EscalateTicketRequest request, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new EscalateTicketCommand(Actor, id, request.Reason), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin. Clears an active escalation (409 if not escalated). Recorded in the ticket activity.")]
    [HttpDelete("{id:guid}/escalation")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    public async Task<ActionResult<TicketResponse>> ClearEscalation(Guid id, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new ClearTicketEscalationCommand(Actor, id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets currently assigned to them; others 404). Read-only, append-only history, newest first. One-based page (default 1), pageSize (default 20, clamped to 100).")]
    [HttpGet("{id:guid}/activity")]
    public async Task<ActionResult<PagedResponse<TicketActivityResponse>>> Activity(
        Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListTicketActivityQuery(Actor, id, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<TicketActivityResponse>(
            result.Items.Select(TicketActivityResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }
```

The class-level `[Authorize(Policy = AuthorizationPolicies.Staff)]` stays; method-level `SuperAdmin` is combined with it (both must pass). No change to `GlobalExceptionHandler` or `DependencyInjection`.

### 6 — Migration

From `backend/` (API stopped):

```bash
dotnet ef migrations add AddTicketWorkflowAndActivity --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations -- --environment Development
```

Inspect `Persistence/Migrations/*_AddTicketWorkflowAndActivity.cs` **before applying**. `Up` must contain **only**:

- `AddColumn` on `Tickets`: `IsEscalated` (`bit`, `nullable: false`, `defaultValue: false`), `EscalationReason` (`nvarchar(500)`, nullable), `EscalatedAtUtc` (`datetimeoffset`, nullable), `EscalatedByUserId` (`uniqueidentifier`, nullable), `Version` (`rowversion`, `rowVersion: true`).
- `CreateTable("TicketActivities", …)` with `Id bigint` + `SqlServer:Identity` `"1, 1"`, the columns from task 4, FKs `FK_TicketActivities_Tickets_TicketId`, `FK_TicketActivities_AspNetUsers_ActorUserId`, `…_FromAgentUserId`, `…_ToAgentUserId` — all `ReferentialAction.Restrict`.
- `CreateIndex`: `IX_TicketActivities_TicketId_OccurredAtUtc_Id` (`descending: new[] { false, true, true }`), `IX_TicketActivities_ActorUserId`, `IX_TicketActivities_FromAgentUserId`, `IX_TicketActivities_ToAgentUserId`, `IX_Tickets_EscalatedByUserId`.
- `AddForeignKey` `FK_Tickets_AspNetUsers_EscalatedByUserId` (`Restrict`).

It must **not** contain `AlterColumn` (the `Status` column is unchanged), `DropColumn`, `DropTable`/`DropIndex` (in `Up`), `RenameColumn`, `InsertData`, `UpdateData` or `Sql(`. `Down` reverses exactly these. The snapshot diff touches only `Ticket` and the new `TicketActivity`. If the generated `Version` column carries a `defaultValue:` and the script preview (Verification 3.3) shows a `DEFAULT` on the `rowversion` column, delete that `defaultValue:` argument (the only permitted hand edit). Apply:

```bash
dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development
```

### 7 — Backend unit tests

**7a — File: `backend/tests/Crm.UnitTests/Tickets/CreateTicketCommandHandlerTests.cs`** — compile-only update, **no assertion changes**:
- lines 58–60: append `, false, null, null, null, null` after `t.UpdatedAtUtc` in the `new TicketDetails(...)` call.
- inside `FakeTicketRepository` (after line 64) add: `public Task<PagedResult<TicketActivityDetails>?> ListActivityAsync(Guid ticketId, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();`

**7b — Create file: `backend/tests/Crm.UnitTests/Tickets/TransitionTicketStatusCommandHandlerTests.cs`** — the **one** new backend happy-path test:

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Tickets;
using Crm.Application.Tickets.TransitionTicket;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public sealed class TransitionTicketStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_AssignedAgentResolvesEscalatedTicket_RecordsTransitionAndAutoClearsEscalationInOneSave()
    {
        var agentId = Guid.CreateVersion7();
        var createdAt = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var ticket = Ticket.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Cannot sign in", "Password rejected.",
            TicketCategory.Technical, TicketPriority.High, agentId, agentId, createdAt);
        ticket.TransitionTo(TicketStatus.InProgress, agentId, createdAt.AddMinutes(5));
        ticket.Escalate("Customer blocked for two days.", agentId, createdAt.AddMinutes(10));
        var now = createdAt.AddHours(1);
        var tickets = new FakeTicketRepository(ticket);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new TransitionTicketStatusCommandHandler(tickets, unitOfWork, new FixedTimeProvider(now));

        var result = await handler.Handle(
            new TransitionTicketStatusCommand(
                new TicketActor(agentId, IsSuperAdmin: false), ticket.Id, TicketStatus.InProgress, TicketStatus.Resolved),
            CancellationToken.None);

        Assert.Equal(agentId, tickets.LoadedWithScope);            // loaded under the Agent's own scope
        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        Assert.False(ticket.IsEscalated);                          // auto-cleared on Resolve
        Assert.Null(ticket.EscalationReason);
        Assert.Null(ticket.EscalatedByUserId);
        Assert.Equal(now, ticket.UpdatedAtUtc);
        Assert.Equal(agentId, ticket.AssignedAgentUserId);         // assignment untouched
        Assert.Equal(1, unitOfWork.SaveCount);                     // ticket + events saved together

        var events = ticket.Activities.ToList();                   // Created, StatusChanged, Escalated, StatusChanged, EscalationAutoCleared
        Assert.Equal(5, events.Count);
        var resolved = events[3];
        Assert.Equal(TicketActivityType.StatusChanged, resolved.Type);
        Assert.Equal("InProgress", resolved.OldValue);
        Assert.Equal("Resolved", resolved.NewValue);
        Assert.Equal(agentId, resolved.ActorUserId);
        Assert.Equal(now, resolved.OccurredAtUtc);
        var cleared = events[4];
        Assert.Equal(TicketActivityType.EscalationAutoCleared, cleared.Type);
        Assert.Equal(agentId, cleared.ActorUserId);
        Assert.Equal(now, cleared.OccurredAtUtc);
        Assert.Equal(TicketStatus.Resolved, result.Status);
        Assert.False(result.IsEscalated);
    }

    private sealed class FakeTicketRepository(Ticket ticket) : ITicketRepository
    {
        public Guid? LoadedWithScope { get; private set; }

        public Task<Ticket?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default)
        {
            LoadedWithScope = agentScope;
            var visible = id == ticket.Id && (agentScope is null || agentScope == ticket.AssignedAgentUserId);
            return Task.FromResult(visible ? ticket : null);
        }

        public Task<TicketDetails?> FindDetailsByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) =>
            Task.FromResult<TicketDetails?>(new TicketDetails(
                ticket.Id, ticket.Subject, ticket.Description, ticket.Category, ticket.Priority, ticket.Status, ticket.CustomerId,
                "Ada Lovelace", "ada@example.com", ticket.CreatedByUserId, "Agent One", ticket.AssignedAgentUserId, "Agent One",
                ticket.CreatedAtUtc, ticket.UpdatedAtUtc, ticket.IsEscalated, ticket.EscalationReason, ticket.EscalatedAtUtc,
                ticket.EscalatedByUserId, null));

        public void Add(Ticket added) => throw new NotSupportedException();
        public Task<PagedResult<TicketDetails>> ListAsync(TicketListFilter filter, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<TicketActivityDetails>?> ListActivityAsync(Guid ticketId, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.FromResult(1); }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException(); // not a transactional command
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
```

No other backend tests.

---

## Frontend Tasks

All paths relative to `frontend/src/app/`.

### 8 — Models and API

**File: `features/tickets/ticket-models.ts`**

- Line 6–7: `/** Lifecycle (TICK-002). Changed only through workflow actions, never the edit form. */` `export const TICKET_STATUSES = ['Open', 'InProgress', 'Resolved', 'Closed'] as const;`
- Append to `Ticket` (after `updatedAtUtc`): `isEscalated: boolean; escalationReason: string | null; escalatedAtUtc: string | null; escalatedByUserId: string | null; escalatedByName: string | null;`
- Append to `TicketListParams`: `/** null/undefined = any. Optional so existing callers compile. */ escalated?: boolean | null;`
- Add:

```ts
export const TICKET_FIELDS = ['Subject', 'Description', 'Category', 'Priority'] as const;
export type TicketField = (typeof TICKET_FIELDS)[number];
export type TicketActivityType =
  | 'Created' | 'FieldChanged' | 'AssigneeChanged' | 'StatusChanged' | 'Escalated' | 'EscalationCleared' | 'EscalationAutoCleared';

/** Matches Crm.Api TicketActivityResponse (names only, no user ids). */
export interface TicketActivity {
  id: number;
  type: TicketActivityType;
  occurredAtUtc: string;
  actorName: string;
  field: TicketField | null;
  oldValue: string | null;
  newValue: string | null;
  fromAgentName: string | null;
  toAgentName: string | null;
  reason: string | null;
}

/** null = unassigned. SuperAdmin only. */
export interface ChangeTicketAssigneeRequest { assignedAgentUserId: string | null; }
/** fromStatus = the status shown to the user (stale ⇒ 409). */
export interface TransitionTicketRequest { fromStatus: TicketStatus; toStatus: TicketStatus; }
export interface EscalateTicketRequest { reason: string; }

export function statusLabel(status: TicketStatus): string {
  return status === 'InProgress' ? 'In Progress' : status;
}

export function statusTone(status: TicketStatus): StatusTone {
  switch (status) {
    case 'Resolved':
      return 'success';
    case 'Closed':
      return 'neutral';
    default:
      return 'pending'; // Open, InProgress
  }
}
```

**File: `features/tickets/tickets-api.ts`** — destructure `escalated` in `list` and add `if (escalated != null) { params = params.set('escalated', escalated); }` after the priority block; add methods:

```ts
  changeAssignee(id: string, body: ChangeTicketAssigneeRequest): Observable<Ticket> {
    return this.http.put<Ticket>(this.apiUrl.versioned(`tickets/${id}/assignee`), body);
  }

  transition(id: string, body: TransitionTicketRequest): Observable<Ticket> {
    return this.http.post<Ticket>(this.apiUrl.versioned(`tickets/${id}/transitions`), body);
  }

  escalate(id: string, body: EscalateTicketRequest): Observable<Ticket> {
    return this.http.post<Ticket>(this.apiUrl.versioned(`tickets/${id}/escalation`), body);
  }

  clearEscalation(id: string): Observable<Ticket> {
    return this.http.delete<Ticket>(this.apiUrl.versioned(`tickets/${id}/escalation`));
  }

  /** Newest first; the server returns 404 when the ticket is not (or no longer) visible to the caller. */
  activity(id: string, page: number, pageSize: number): Observable<PagedResponse<TicketActivity>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResponse<TicketActivity>>(this.apiUrl.versioned(`tickets/${id}/activity`), { params });
  }
```

### 9 — Workflow rules for the UI

**Create file: `features/tickets/ticket-workflow.ts`** — mirrors `TicketWorkflow` for **display only**; the server re-checks everything.

```ts
import { CurrentUser } from '../../core/auth/auth-models';
import { Ticket, TicketStatus } from './ticket-models';

export interface TicketTransition {
  to: TicketStatus;
  label: string;
}

/** Must match Crm.Domain.Tickets.TicketWorkflow. */
export const TICKET_TRANSITIONS: Readonly<Record<TicketStatus, readonly TicketTransition[]>> = {
  Open: [{ to: 'InProgress', label: 'Start progress' }],
  InProgress: [{ to: 'Resolved', label: 'Resolve' }],
  Resolved: [
    { to: 'Closed', label: 'Close' },
    { to: 'InProgress', label: 'Resume' },
  ],
  Closed: [{ to: 'Open', label: 'Reopen' }],
};

export interface TicketActions {
  transitions: readonly TicketTransition[];
  canAssign: boolean;
  canEscalate: boolean;
  canClearEscalation: boolean;
}

/** SuperAdmin: everything. Agent: workflow + escalate only on tickets currently assigned to them. Others: nothing. */
export function ticketActions(
  ticket: Pick<Ticket, 'status' | 'isEscalated' | 'assignedAgentUserId'>,
  user: Pick<CurrentUser, 'id' | 'role'> | null,
): TicketActions {
  const isSuperAdmin = user?.role === 'SuperAdmin';
  const canAct = isSuperAdmin || (user?.role === 'Agent' && ticket.assignedAgentUserId === user.id);
  const active = ticket.status === 'Open' || ticket.status === 'InProgress';
  return {
    transitions: canAct ? TICKET_TRANSITIONS[ticket.status] : [],
    canAssign: isSuperAdmin,
    canEscalate: canAct && active && !ticket.isEscalated,
    canClearEscalation: isSuperAdmin && ticket.isEscalated,
  };
}
```

### 10 — Activity section component

**Create file: `features/tickets/ticket-activity-list.ts`** (+ `.html`, `.scss`) — selector `crm-ticket-activity-list`, `OnPush`, imports `DatePipe`, `MatButtonModule`, `MatProgressBarModule`.

```ts
const PAGE_SIZE = 20;

export class TicketActivityList {
  private readonly api = inject(TicketsApi);
  readonly ticketId = input.required<string>();
  /** Parent increments after every successful change ⇒ reload from page 1. */
  readonly refresh = input(0);

  protected readonly items = signal<TicketActivity[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly hasMore = computed(() => this.items().length < this.totalCount());
  /** True once a page has arrived successfully (set in the subscribe success handler) — nothing flashes before data. */
  protected readonly loaded = signal(false);
  /**
   * Claim "earlier activity was not recorded" only when it is true: the oldest page is loaded (no more pages) and it has
   * no "Created" event ⇒ the ticket pre-dates tracking (TICK-001). Hidden while loading, on error, before the first page,
   * and while older pages remain.
   */
  protected readonly showTrackingNote = computed(
    () => this.loaded() && !this.loading() && !this.error() && !this.hasMore() && !this.items().some(a => a.type === 'Created'),
  );
  private readonly requests = new Subject<{ id: string; page: number }>();
  private nextPage = 1;
  // constructor: requests.pipe(switchMap(r => api.activity(r.id, r.page, PAGE_SIZE).pipe(map(result => ({ r, result })),
  //   catchError(err => { error.set(describeHttpError(err, 'Could not load activity.')); loading.set(false); return EMPTY; }))),
  //   takeUntilDestroyed()).subscribe(({ r, result }) => { page 1 ⇒ items.set(result.items); else append items whose id is not already present;
  //   totalCount.set(result.totalCount); nextPage = r.page + 1; loading.set(false); });
  // effect(() => { const id = this.ticketId(); this.refresh(); untracked(() => this.load(id, 1)); });
  protected loadMore(): void { this.load(this.ticketId(), this.nextPage); }
  protected retry(): void { this.load(this.ticketId(), this.items().length === 0 ? 1 : this.nextPage); }
  private load(id: string, page: number): void { this.loading.set(true); this.error.set(null); this.requests.next({ id, page }); }
  protected readonly describe = describeActivity;
}
```

Use the same "switchMap + inner `catchError` returning `EMPTY`" pattern as `ticket-list-page.ts` lines 85–106. Export a pure function in the same file:

```ts
export function describeActivity(a: TicketActivity): { title: string; detail: string | null } {
  switch (a.type) {
    case 'Created':
      return { title: 'Ticket created', detail: a.toAgentName ? `Assigned to ${a.toAgentName}` : 'Unassigned' };
    case 'FieldChanged':
      return a.field === 'Description'
        ? { title: 'Description changed', detail: null }
        : { title: `${a.field} changed`, detail: `${a.oldValue} → ${a.newValue}` };
    case 'AssigneeChanged':
      if (!a.fromAgentName) return { title: 'Assigned', detail: `To ${a.toAgentName}` };
      if (!a.toAgentName) return { title: 'Unassigned', detail: `Was ${a.fromAgentName}` };
      return { title: 'Reassigned', detail: `${a.fromAgentName} → ${a.toAgentName}` };
    case 'StatusChanged':
      return {
        title: a.oldValue === 'Closed' && a.newValue === 'Open' ? 'Reopened' : 'Status changed',
        detail: `${statusLabel(a.oldValue as TicketStatus)} → ${statusLabel(a.newValue as TicketStatus)}`,
      };
    case 'Escalated':
      return { title: 'Escalated', detail: a.reason };
    case 'EscalationCleared':
      return { title: 'Escalation cleared', detail: null };
    case 'EscalationAutoCleared':
      return { title: 'Escalation cleared automatically', detail: 'The ticket was resolved.' };
  }
}
```

Template (`ticket-activity-list.html`): `<mat-progress-bar mode="indeterminate" />` while loading; error `<p class="form-error" role="alert">` + `<button mat-button type="button" (click)="retry()">Retry</button>`; an `<ol class="activity">` of items (`@for (a of items(); track a.id)`) each showing `<strong>{{ describe(a).title }}</strong>`, `{{ a.actorName }} · {{ a.occurredAtUtc | date: 'medium' }}`, and the detail line when non-null; `@if (hasMore() && !loading())` a "Load more" `mat-button`; `@if (showTrackingNote())` `<p class="hint">History tracking started with TICK-002; earlier activity was not recorded.</p>` rendered **after** the list (the oldest end). When `loaded() && items().length === 0` that note is the whole section — **no** generic "No activity yet" text, which would suggest nothing ever happened. Never synthesize a "Created" row on the client from `createdAtUtc` / `createdByName`; creation metadata stays in the Summary card. Styles: copy `.hint` / `.form-error` from `ticket-detail-page.scss`; `.activity { list-style: none; padding: 0; margin: 0; display: grid; gap: var(--crm-space-3); }` — logical properties only (no `left`/`right`).

### 11 — Ticket detail page: workflow, assignment, escalation, activity

**File: `features/tickets/ticket-detail-page.ts`**

- Inject `AuthService` and `AgentsApi`; add `TicketActivityList` to the component `imports` (no new Material modules beyond those already imported).
- New state:

```ts
  private readonly auth = inject(AuthService);
  private readonly agentsApi = inject(AgentsApi);
  protected readonly statusLabel = statusLabel;
  protected readonly statusTone = statusTone;
  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin');
  protected readonly actions = computed(() => {
    const t = this.ticket();
    return t ? ticketActions(t, this.auth.currentUser()) : null;
  });
  protected readonly activeAgents = signal<Agent[]>([]);
  protected readonly agentsError = signal<string | null>(null);
  protected readonly selectedAssignee = signal<string | null>(null);
  protected readonly escalationReason = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(500)],
  });
  protected readonly busy = signal(false);          // one action at a time
  protected readonly actionError = signal<string | null>(null);
  protected readonly activityRefresh = signal(0);
```

- Constructor: after the existing `get`, when `isSuperAdmin()` load `agentsApi.list()` filtered by `isActive` exactly like `ticket-create-page.ts` (~lines 69–80; Agents never call `/api/v1/agents`); on error `agentsError.set(describeHttpError(error, 'Could not load agents.'))`.
- Change `show(ticket)` to `show(ticket: Ticket, resetInputs = true)`: always `this.ticket.set(ticket)`. When `resetInputs` is true: reset the edit form (existing lines 72–77), `this.selectedAssignee.set(ticket.assignedAgentUserId)` and `this.escalationReason.reset('')`. When false (refresh after a 409): reset the edit form **only if** `!this.form.dirty`, keep `escalationReason` as typed, and set `selectedAssignee` to the new assignee — a conflict never discards text the user typed. The initial load and successful actions keep calling `show(ticket)`.
- Add a single action runner used by every new action **and** by `save()`'s error branch:

```ts
  private run(request: Observable<Ticket>, success: string): void {
    if (this.busy()) return;
    this.busy.set(true);
    this.actionError.set(null);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ticket => {
        this.show(ticket);
        this.activityRefresh.update(v => v + 1);
        this.busy.set(false);
        this.snackBar.open(success, undefined, { duration: 3000 });
      },
      error: (error: HttpErrorResponse) => {
        this.busy.set(false);
        this.handleActionError(error);
      },
    });
  }

  /**
   * 404 after load = the ticket left the caller's scope (e.g. reassigned) ⇒ drop stale content.
   * 409 = stale or overlapping change; nothing was saved ⇒ show the server message, refresh ticket + activity
   * so the buttons match the real state, keep the user's typed input, let them retry.
   */
  private handleActionError(error: HttpErrorResponse, target: WritableSignal<string | null> = this.actionError): void {
    if (error.status === 404) {
      this.ticket.set(null);
      this.loadError.set('This ticket is no longer available to you. It may have been reassigned.');
      return;
    }
    target.set(describeHttpError(error, 'The action could not be completed.'));
    if (error.status === 409) {
      this.reload();
    }
  }

  private reload(): void {
    this.api.get(this.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: ticket => {
        this.show(ticket, false); // keep unsaved edits / escalation reason
        this.activityRefresh.update(v => v + 1);
      },
      error: (error: HttpErrorResponse) => this.handleActionError(error),
    });
  }

  protected transition(current: Ticket, to: TicketStatus): void {
    this.run(this.api.transition(this.id, { fromStatus: current.status, toStatus: to }), `Status changed to ${statusLabel(to)}.`);
  }

  protected saveAssignee(): void {
    this.run(this.api.changeAssignee(this.id, { assignedAgentUserId: this.selectedAssignee() }), 'Assignment updated.');
  }

  protected escalate(): void {
    if (this.escalationReason.invalid) { this.escalationReason.markAsTouched(); return; }
    this.run(this.api.escalate(this.id, { reason: this.escalationReason.value.trim() }), 'Ticket escalated.');
  }

  protected clearEscalation(): void {
    this.run(this.api.clearEscalation(this.id), 'Escalation cleared.');
  }
```

- `save()`: also return early when `this.busy()`; on success also `this.activityRefresh.update(v => v + 1)`; in the error branch replace `this.saveError.set(...)` with `this.handleActionError(error, this.saveError)` — 404 ⇒ access-lost message; 409 (overlapping save) ⇒ message under the form, Summary + Activity refreshed, **form values kept** (the form is dirty, so `show(…, false)` does not reset it), and the user can review and press Save again; 400 ⇒ message as before.
- `run(...)` and `save()` clear **both** `actionError` and `saveError` when they start, so an old conflict message never sits next to a later success. Add `WritableSignal` to the `@angular/core` import.

**File: `features/tickets/ticket-detail-page.html`**

- Summary card: status `<crm-status-badge [label]="statusLabel(current.status)" [tone]="statusTone(current.status)" />`; directly after the status `<dd>` add `@if (current.isEscalated) { <dt>Escalation</dt><dd><crm-status-badge label="Escalated" tone="error" /> {{ current.escalationReason }} <span class="hint">— {{ current.escalatedByName }}, {{ current.escalatedAtUtc | date: 'medium' }}</span></dd> }`. Replace the hint at line 38 with `<p class="hint">Status, assignment and escalation change only through the actions below.</p>`.
- Add a **"Workflow"** card after Summary (rendered when `actions(); as a`): one `mat-stroked-button` per `a.transitions` (`(click)="transition(current, t.to)"`, `[disabled]="busy() || saving()"`, text `t.label`; Escalate, Clear escalation and Save assignment use the same `busy() || saving()` guard, and the edit form's Save button becomes `[disabled]="saving() || busy()"` — the user can never race their own two requests into a 409); when `a.transitions.length === 0` show `<p class="hint">No workflow actions are available to you for this ticket.</p>`. Escalation block: `@if (a.canEscalate)` a `mat-form-field` textarea bound to `[formControl]="escalationReason"` (label "Escalation reason", `<mat-hint align="end">{{ escalationReason.value.length }}/500</mat-hint>`, error "A reason is required (max 500 characters)") plus a "Escalate" `mat-flat-button`; `@if (a.canClearEscalation)` a "Clear escalation" `mat-stroked-button`. `@if (actionError(); as message)` `<p class="form-error" role="alert">{{ message }}</p>`.
- Add an **"Assignment"** card **only** `@if (a.canAssign)`: `mat-select` (label "Assigned agent", `[value]="selectedAssignee()"`, `(selectionChange)="selectedAssignee.set($event.value)"`) with `<mat-option [value]="null">Unassigned</mat-option>` + one option per `activeAgents()` (`{{ agent.firstName }} {{ agent.lastName }}`); `agentsError()` shown as `.form-error` ("Unassigned" still works); "Save assignment" `mat-flat-button` `[disabled]="busy() || selectedAssignee() === current.assignedAgentUserId"`. If the current assignee is not in `activeAgents()` (disabled Agent) show `<p class="hint">Current agent {{ current.assignedAgentName }} is inactive; choose an active Agent or Unassigned.</p>`. Agents see the assignee only in the Summary card (read-only).
- Keep the Edit card unchanged.
- Add an **"Activity"** card last: `<crm-ticket-activity-list [ticketId]="current.id" [refresh]="activityRefresh()" />`.

Imports to add in the component: `FormControl`, `computed`, `Observable`, `MatSnackBar` (already), `AuthService`, `AgentsApi`, `Agent`, `TicketActivityList`, `ticketActions`, `statusLabel`, `statusTone`, `TicketStatus`. `.scss`: add `.actions { display: flex; flex-wrap: wrap; gap: var(--crm-space-2); }`.

### 12 — Ticket list page

**File: `features/tickets/ticket-list-page.ts`** — add `escalated: [null as boolean | null]` to `filters` (line 70–75); include `f.escalated !== null` in `hasFilters()`; pass `escalated: f.escalated` in `load()`; expose `statusLabel` and `statusTone`.

**File: `features/tickets/ticket-list-page.html`**

- Status filter options: `{{ statusLabel(v) }}`.
- After the Priority filter add an "Escalation" `mat-select` (`formControlName="escalated"`): `Any` (`null`), `Escalated` (`true`), `Not escalated` (`false`).
- Status cell (line 238): `<crm-status-badge [label]="statusLabel(t.status)" [tone]="statusTone(t.status)" />` followed by `@if (t.isEscalated) { <crm-status-badge label="Escalated" tone="error" /> }`.

No other list changes; Agents keep seeing only their assigned tickets (server-side).

### 13 — Frontend unit test (single happy path)

**Create file: `features/tickets/ticket-workflow.spec.ts`** — the **one** new frontend test (pure function, no TestBed):

```ts
import { ticketActions } from './ticket-workflow';

describe('ticketActions', () => {
  it('lets the assigned Agent resolve and escalate an In Progress ticket, but not reassign or clear escalation', () => {
    const actions = ticketActions(
      { status: 'InProgress', isEscalated: false, assignedAgentUserId: 'agent-1' },
      { id: 'agent-1', role: 'Agent' },
    );

    expect(actions.transitions).toEqual([{ to: 'Resolved', label: 'Resolve' }]);
    expect(actions.canEscalate).toBe(true);
    expect(actions.canAssign).toBe(false);
    expect(actions.canClearEscalation).toBe(false);
  });
});
```

`tickets-api.spec.ts` stays **unchanged** (its `list` call compiles because `escalated` is optional; its flushed object is untyped).

---

## Documentation Tasks

### 14 — `docs/ticket-management.md`

Retitle `# Ticket management (TICK-001, TICK-002)`; update line 3. Section 1: add rows `IsEscalated` / `EscalationReason` / `EscalatedAtUtc` / `EscalatedByUserId` and the `Version` rowversion; change `AssignedAgentUserId` note to "set at creation; changed later only by SuperAdmin". Section 2: statuses `Open`, `InProgress`, `Resolved`, `Closed` + the transition table (Product rules) and who may run them. New sections: **Assignment of existing tickets** (SuperAdmin only, active Agents, no-op rule, visibility moves immediately, no claim/transfer/routing); **Escalation** (flag not status, permissions, reason ≤ 500, auto-clear on Resolve, reopen does not re-escalate); **Activity history** (table `TicketActivities`, event types and stored data incl. "Description values not stored", newest-first paging, visibility, append-only, atomic with the ticket save, actor names joined at read time, **legacy tickets have no events before TICK-002 and none are fabricated** + the UI note text); **Concurrency** (row version ⇒ 409 for overlapping requests only — the version is never sent to the client; stale pages are caught by `fromStatus` / escalation-state preconditions ⇒ 409; stale basic edits and assignment choices remain last-write-wins; after a 409 the UI reloads and keeps typed input); **Migration impact** (`AddTicketWorkflowAndActivity`: additive columns with `IsEscalated = 0` for existing rows, new table, no data rewritten). Section 5 API table: add the five routes with auth and 200/400/403/404/409 outcomes, the `escalated` list filter, the no-op `PUT` behaviour, the new response fields. Section 6: keep Customer Portal and SLA & Automation; add deferred: comments/notes, attachments, escalation levels/notifications, auto-close, configurable workflows, ticket deletion; "Never: Agent self-claim or Agent-to-Agent transfer".

### 15 — Other docs

- `docs/identity-and-access.md`: matrix (lines 16–17) — add rows "Assign / reassign / unassign existing tickets — ✔ / — / —", "Change ticket status (workflow) — ✔ all / ✔ assigned to them / —", "Escalate ticket — ✔ / ✔ assigned / —", "Clear escalation — ✔ / — / —", "View ticket activity — ✔ all / ✔ assigned to them / —"; routes table (lines 36–37) — add the five new routes.
- `docs/api-conventions.md` line 19: add "State conflicts (stale status, illegal transition, optimistic-concurrency failure) return 409 ProblemDetails."
- `docs/persistence-and-transactions.md`: after line 5 add a bullet: "**Optimistic concurrency:** entities with a row version (first: `Tickets.Version`) — `EfUnitOfWork.SaveChangesAsync` turns `DbUpdateConcurrencyException` into `ConflictException` (409); nothing is saved. Aggregate-owned child rows (ticket activity) are inserted in the same single save, so no `ITransactionalCommand` is needed."
- `docs/frontend-ui-foundation.md` after line 11: "**Role-aware actions:** pages compute available actions from the current user and record (`ticketActions` in `features/tickets/ticket-workflow.ts`) for display only; the API re-checks. After a 404 on an action the page drops the stale record and shows an access-lost message."
- `README.md` line 3: add "and ticket workflow, assignment & history (`TICK-002`)".

---

## Edge Cases & Failure Modes

- **Agent calls `PUT /tickets/{id}/assignee` or `DELETE /tickets/{id}/escalation`** (any ticket, incl. their own) — method-level `SuperAdmin` policy ⇒ **403**; handler never runs. A Customer gets 403 on every `/tickets*` route (class-level `Staff`).
- **SuperAdmin assigns a disabled Agent, a SuperAdmin (incl. self), a Customer account or an unknown id** — `ChangeTicketAssigneeCommandHandler` ⇒ 400 `assignedAgentUserId`; `Guid.Empty` ⇒ 400 from the validator; no save, no event.
- **SuperAdmin re-submits the current assignee (also a since-disabled one)** — no-op check runs before validation ⇒ 200 with unchanged `updatedAtUtc`, no event.
- **Body `{}` on `PUT …/assignee`** — `AssignedAgentUserId` binds to `null` ⇒ unassign (documented; the Angular control always sends the field explicitly).
- **Reassignment while the old Agent has the page open** — the old Agent's next `GET` / `PUT` / transition / escalate / activity request loads with `agentScope` ⇒ 404; the page drops the ticket and shows "This ticket is no longer available to you. It may have been reassigned." (`handleActionError`). If both requests race inside one save window, the row version makes the loser 409.
- **Stale UI transition** (ticket changed elsewhere) — `fromStatus` ≠ stored status ⇒ 409 "The ticket is now X, not Y. Reload it and try again."; the page reloads ticket + activity. Nothing saved.
- **Illegal transition** (e.g. `Open→Closed`, `Closed→Resolved`, same status) — `TicketWorkflow.IsAllowed` false ⇒ 409; `Ticket.TransitionTo` would throw `DomainException` (422) only if a handler skipped the check.
- **Unknown or integer status in the transition body** (`"toStatus":"Done"`, `"toStatus":2`) — `JsonStringEnumConverter(allowIntegerValues: false)` ⇒ 400 from `[ApiController]`; missing field ⇒ `null` ⇒ validator 400.
- **SuperAdmin transitions an unassigned ticket** — allowed; `AssignedAgentUserId` stays `null` (no auto-assign).
- **Escalate an already escalated ticket** ⇒ 409 "The ticket is already escalated." (no duplicate event). **Escalate a Resolved/Closed ticket** ⇒ 409. **Whitespace-only or missing reason** ⇒ 400 `reason`; > 500 raw chars ⇒ 400. Leading/trailing spaces trimmed before storage.
- **Clear when not escalated** ⇒ 409. **Assigned Agent tries to clear** ⇒ 403 (policy); the UI never shows the button to Agents.
- **Resolve an escalated ticket** — one save: status `Resolved`, flag cleared, events `StatusChanged` + `EscalationAutoCleared` (actor = resolver). Resume/Reopen never re-escalate. Escalation survives assignment changes and `Open→InProgress`.
- **Overlapping writes to one ticket** (two requests whose load→save windows overlap) — both load the same `Version`; the second `UPDATE … WHERE Version = @old` affects 0 rows ⇒ `DbUpdateConcurrencyException` ⇒ `EfUnitOfWork` ⇒ 409 "The record was changed by another request…"; the EF implicit transaction rolls back that request's activity inserts. Applies to every ticket command, including `PUT`.
- **Stale page, not overlapping** (user B's page is minutes old; A already committed) — the row version **cannot** detect this (B's request loads the current `Version`). Protection comes from the preconditions: transition `fromStatus` ⇒ 409 "The ticket is now X, not Y…"; escalate on an escalated / Resolved / Closed ticket ⇒ 409; clear on a non-escalated ticket ⇒ 409; same assignee ⇒ no-op. A stale **basic edit** (`PUT`) or a stale **assignment** choice is applied as submitted (last write wins, as in TICK-001; documented). History records both changes in order, so nothing is hidden.
- **Angular after a 409** — `handleActionError` shows the server message (`actionError` for actions, `saveError` for the edit form), reloads ticket + activity via `show(ticket, false)` so buttons reflect the real status, keeps the typed escalation reason and dirty edit-form values, and lets the user retry. All buttons are disabled while any request is in flight (`busy() || saving()`), so one user cannot conflict with themselves.
- **Transient SQL failure during the single save** — `EnableRetryOnFailure` (`Crm.Infrastructure/DependencyInjection.cs` line 33) retries `SaveChanges` as a unit; a failed attempt commits nothing (ticket and events together). If a commit succeeded but its acknowledgement was lost, the retried `UPDATE` matches 0 rows (version already changed) ⇒ the retry rolls back ⇒ the user sees 409 although the first attempt was saved; the 409 reload then shows the saved state and its events exactly once.
- **No-op `PUT`** (same four values, or values differing only by trimmed whitespace) — `UpdateDetails` returns false ⇒ no save, no event, `updatedAtUtc` unchanged, 200. A case-only Subject change **is** a change (ordinal compare).
- **Activity entity tracking** — new `TicketActivity` objects are reached through `Ticket._activities` on a tracked ticket; with a store-generated `long` key that is still `0`, EF's `DetectChanges` marks them **Added**. If a save ever fails with "expected to affect 1 row(s) but actually affected 0" on `TicketActivities`, the key was misconfigured — verify `ValueGeneratedOnAdd` and the `bigint IDENTITY` column. `GetByIdAsync` must **not** `Include(Activities)` (would load the whole history for every write).
- **Multiple events in one save** (several fields edited, resolve + auto-clear) share `OccurredAtUtc`; order between them follows the identity values (deterministic). The UI shows them newest-first.
- **Activity paging** — `page < 1`, `> 1 000 000`, `pageSize < 1` ⇒ 400; `pageSize > 100` clamped. Events appended while a user clicks "Load more" shift offsets; the component de-duplicates by `id` and every successful action reloads from page 1.
- **Legacy TICK-001 ticket** — zero events ⇒ empty list + note "History tracking started with TICK-002; earlier activity was not recorded."; after the first tracked change the list shows that change and still shows the note at the end (no `Created` event). Creation metadata remains in the Summary card. Never insert backfill rows.
- **History actor display** — accounts are never deleted (all FKs `Restrict`); disabled users' names still join. Names come from current `FirstName`/`LastName` (a later rename changes how old events display — accepted, documented).
- **OpenAPI enums** — `ConfigureHttpJsonOptions` (`Crm.Api/DependencyInjection.cs` line 18) already makes `TicketActivityType` / `TicketField` / new statuses string enums in `/openapi/v1.json`; verify in Verification 9.
- **Migration `Version` default** — SQL Server rejects a `DEFAULT` on `rowversion`; if the generated SQL contains one, remove `defaultValue:` from that `AddColumn` (task 6). Existing rows receive row versions automatically.
- **EF translation of the escalated-by left join / activity joins** — same `DefaultIfEmpty` pattern as `Project` (`TicketRepository.cs` line 73). If EF reports an untranslatable `orderby`, move `OrderByDescending(a => a.OccurredAtUtc).ThenByDescending(a => a.Id)` onto `activities` before the joins and keep `Skip/Take` after the projection (the Story 04 fallback).
- **`agentScope` read-backs** — no handler passes `agentScope: null`; SuperAdmin-only handlers use `Actor.AgentScope` (= null for SuperAdmin), so the `!` after `FindDetailsByIdAsync` is safe: SuperAdmin always sees the ticket, and Agents' commands never change the assignment.
- **Agents dropdown fails to load (SuperAdmin)** — `agentsError` shown; "Unassigned" is still selectable.
- **`run-dev.ps1`** — applies pending migrations before starting the API (unchanged script, lines 22–37); with `-SkipMigrations` and the migration unapplied, ticket endpoints fail with 500 "Invalid column name 'IsEscalated'" — apply the migration.

---

## Test Plan

1. **Unit (backend) — add:** `backend/tests/Crm.UnitTests/Tickets/TransitionTicketStatusCommandHandlerTests.cs` → `Handle_AssignedAgentResolvesEscalatedTicket_RecordsTransitionAndAutoClearsEscalationInOneSave` (task 7b): scoped load, status change, escalation auto-clear, `UpdatedAtUtc`, unchanged assignee, exact events (type / old / new / actor / time) and exactly one save. Hand-written fakes as in `Tickets/CreateTicketCommandHandlerTests.cs`.
2. **Unit (backend) — compile-only update:** `Tickets/CreateTicketCommandHandlerTests.cs` (task 7a) — new `TicketDetails` arguments + `ListActivityAsync` stub; assertions unchanged, still passes.
3. **Unit (frontend) — add:** `frontend/src/app/features/tickets/ticket-workflow.spec.ts` → "lets the assigned Agent resolve and escalate an In Progress ticket, but not reassign or clear escalation" (task 13).
4. **Keep unchanged:** `Auth/LoginCommandHandlerTests.cs`, `Customers/CreateCustomerCommandHandlerTests.cs`, `Diagnostics/GetSystemStatusQueryHandlerTests.cs`, `core/auth/auth-service.spec.ts`, `core/api/api-url.spec.ts`, `features/customers/customers-api.spec.ts`, `features/tickets/tickets-api.spec.ts`.
5. **Manual (critical role, workflow, escalation, history, migration checks):** Verification Steps 3–12.

No other tests, frameworks, mocking libraries, integration / SQL / API / E2E suites or automated negative-path checks.

---

## Migration / Rollback

- **Forward:** `AddTicketWorkflowAndActivity` adds five columns to `Tickets` (`IsEscalated` default `0`, three nullable escalation columns, `Version rowversion`), one FK + index for `EscalatedByUserId`, and the new `TicketActivities` table with `Restrict` FKs and indexes. No existing column is altered, no row is updated or inserted, and `TicketStatus` needs no schema change (names in `nvarchar(20)`).
- **Rollback (local):** stop the API; from `backend/`: `dotnet ef database update AddTickets --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` (drops `TicketActivities` — all history is lost — and the five `Tickets` columns; tickets, customers, accounts and sessions remain). **Before rolling back**, if any ticket is `InProgress` / `Resolved` / `Closed`, the TICK-001 code cannot read it (unknown enum name ⇒ 500): run `UPDATE Tickets SET Status = 'Open' WHERE Status <> 'Open'` only if you accept losing status, or keep TICK-002 deployed. Then `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` and revert the code.
- **Half-applied state:** SQL Server runs the migration DDL in one transaction; on failure nothing is applied — fix (e.g. the `rowversion` default) and re-run `database update`. The new API against the old schema fails on ticket endpoints only ("Invalid column name …") until the migration is applied.

---

## Verification Steps

1. **Backend builds and tests:** in `backend/` run `dotnet build` (0 errors) then `dotnet test` (**5** tests passed: the four existing + `TransitionTicketStatusCommandHandlerTests`).
2. **Layer direction and untouched contracts:** `grep -rn "Microsoft.AspNetCore\|EntityFrameworkCore" backend/src/Crm.Application backend/src/Crm.Domain --include=*.cs` returns nothing; `git diff --stat -- backend/src/Crm.Infrastructure/Persistence/Migrations/20261014234527_InitialCreate.cs backend/src/Crm.Infrastructure/Persistence/Migrations/20261015011303_AddIdentityAndRefreshSessions.cs backend/src/Crm.Infrastructure/Persistence/Migrations/20261017183245_AddCustomers.cs backend/src/Crm.Infrastructure/Persistence/Migrations/20261017194039_AddTickets.cs backend/src/Crm.Application/Abstractions/Identity backend/src/Crm.Application/Tickets/CreateTicket backend/src/Crm.Api/Errors` shows no changes; `grep -rn "agentScope: null" backend/src` returns nothing.
3. **Migration applied safely** (API stopped; from `backend/`; database `CrmDev` per README):
   1. **Baseline:** `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT COUNT(*) FROM AspNetUsers; SELECT COUNT(*) FROM Customers; SELECT COUNT(*) FROM RefreshSessions; SELECT COUNT(*) FROM Tickets; SELECT Id, Status, AssignedAgentUserId, CreatedByUserId, CustomerId, Category, Priority, UpdatedAtUtc FROM Tickets; SELECT MigrationId FROM __EFMigrationsHistory;"` — save the output; history ends with `20261017194039_AddTickets`. If no tickets exist yet, create two as SuperAdmin/Agent **with the TICK-001 build** first (legacy data for step 8).
   2. **Static review:** `grep -n "AlterColumn\|DropColumn\|RenameColumn\|InsertData\|UpdateData\|Sql(" src/Crm.Infrastructure/Persistence/Migrations/*_AddTicketWorkflowAndActivity.cs` returns nothing; `grep -n "DropTable\|DropForeignKey\|DropIndex\|DropColumn" …` matches only inside `Down`.
   3. **SQL preview:** `dotnet ef migrations list --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` shows only `…_AddTicketWorkflowAndActivity (Pending)`; `dotnet ef migrations script AddTickets AddTicketWorkflowAndActivity --project src/Crm.Infrastructure --startup-project src/Crm.Api --output $env:TEMP/AddTicketWorkflowAndActivity.sql -- --environment Development` contains only `ALTER TABLE [Tickets] ADD …` (5 columns; `[IsEscalated] bit NOT NULL DEFAULT CAST(0 AS bit)`, `[Version] rowversion NOT NULL` **without** DEFAULT), `CREATE TABLE [TicketActivities]` (`[Id] bigint NOT NULL IDENTITY`, FKs without `ON DELETE CASCADE`), `CREATE INDEX` statements, one `ALTER TABLE [Tickets] ADD CONSTRAINT [FK_Tickets_AspNetUsers_EscalatedByUserId]` and one `INSERT INTO [__EFMigrationsHistory]`. No `UPDATE`, `DELETE`, `DROP`.
   4. **Apply:** `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` (or `.\run-dev.ps1`). Never `EnsureCreated` / drop / recreate.
   5. **After:** no pending migrations; the four counts from 3.1 are unchanged; the 3.1 `Tickets` row query returns identical values (IDs, status `Open`, assignee, creator, customer, category, priority, `UpdatedAtUtc`); `SELECT COUNT(*) FROM TicketActivities` = **0** (no backfill); `SELECT COUNT(*) FROM Tickets WHERE IsEscalated = 1` = 0.
4. **Frontend builds and tests:** in `frontend/` run `npx ng build` (no errors; budget warnings acceptable) and `npm test -- --watch=false` (**5** tests passed).
5. **Setup:** `.\run-dev.ps1` (or `dotnet run --project src/Crm.Api --launch-profile http` + `npm start`). Use Agents "Agent One", "Agent Two" and a disabled "Agent Off" (create if missing, as in Story 04 Verification 4).
6. **SuperAdmin flows (http://localhost:4200):** create ticket T unassigned → Activity shows "Ticket created — Unassigned" (actor admin). Assignment card: pick Agent One → "Assignment updated.", Summary shows Agent One, Activity "Assigned — To Agent One". Reassign to Agent Two → "Reassigned — Agent One → Agent Two". "Save assignment" is disabled when the selection equals the current assignee; dropdown lists Unassigned / Agent One / Agent Two only (not Agent Off). Set Unassigned → "Unassigned — Was Agent Two". Workflow on the unassigned ticket: "Start progress" → badge "In Progress", assignee still Unassigned. Escalate with reason "VIP customer" → red "Escalated" badge + reason in Summary; list filter Escalation = Escalated shows T; "Clear escalation" → badge gone, Activity "Escalation cleared". Escalate again, then "Resolve" → status Resolved **and** escalation gone; Activity (newest first) shows "Escalation cleared automatically" and "Status changed In Progress → Resolved". "Close" → "Reopen" → Activity "Reopened Closed → Open"; no escalation reappears. Escalate is not offered on Resolved/Closed. Edit Subject + Priority → two "… changed" events with before → after; Save again without changes → no new event and "Last updated" unchanged. Status filter lists "In Progress", "Resolved", "Closed"; badges use the new tones.
7. **Agent flows:** assign ticket T to Agent One (as admin). Agent One: list shows T; detail shows workflow buttons for T's status and Escalate, **no** Assignment card and **no** Clear escalation; Start progress / Resolve / Resume work; escalate with a reason works. Meanwhile, as admin, reassign T to Agent Two; Agent One clicks any action (or reloads) → "This ticket is no longer available to you. It may have been reassigned." / "Ticket '…' was not found."; T disappears from Agent One's list; Agent Two now sees T with its full activity.
8. **Conflict recovery in the UI (stale page):** as admin open In Progress ticket T in two browser tabs. Tab A: "Resolve". Tab B (still showing In Progress): type text into Subject (do not save) and an escalation reason, then click "Resolve" → error "The ticket is now Resolved, not InProgress. Reload it and try again."; the page refreshes to Resolved with Close / Resume buttons, Activity shows exactly one new "Status changed In Progress → Resolved" (by tab A's user), and the typed Subject text is still in the form. `SELECT COUNT(*) FROM TicketActivities WHERE TicketId = '<T>' AND Type = 'StatusChanged' AND NewValue = 'Resolved'` increased by exactly 1. Tab B's "Save" on the edit form then succeeds (stale basic edit = last write wins, documented) and records only the fields that differ.
9. **Legacy tickets:** open a ticket created before the migration (3.1) → Activity empty with "History tracking started with TICK-002; earlier activity was not recorded."; Created/Created by/Assigned agent still shown in Summary. Make one change → that event appears, the note remains at the bottom (oldest end), no client-side "Created" row is shown, `SELECT Type FROM TicketActivities WHERE TicketId = '<legacy-id>'` has no `Created` row, and the ticket's `Id`, `CustomerId`, `CreatedByUserId`, `CreatedAtUtc` equal the 3.1 values. With more than 20 events on a legacy ticket, the note appears only after "Load more" reaches the oldest page. A ticket created after the migration never shows the note.
10. **Critical server-side checks (manual, curl or Postman CLI; tokens via `POST /api/v1/auth/login`):**
   - Agent token: `PUT /api/v1/tickets/<id>/assignee` `{"assignedAgentUserId":"<agent-one-id>"}` ⇒ **403** (also for their own ticket); `DELETE /api/v1/tickets/<id>/escalation` ⇒ 403; `POST …/transitions` on a ticket assigned to someone else ⇒ 404; `GET …/activity` on such a ticket ⇒ 404.
   - Admin token: assignee = Agent Off id / admin's own id / a Customer user id ⇒ 400 `errors.assignedAgentUserId`; `POST …/transitions` `{"fromStatus":"Open","toStatus":"Closed"}` on an Open ticket ⇒ 409; `{"fromStatus":"Resolved","toStatus":"Closed"}` on an Open ticket ⇒ 409 (stale); `{"toStatus":2}` ⇒ 400; `POST …/escalation` `{"reason":"   "}` ⇒ 400; escalate twice ⇒ second 409; `DELETE …/escalation` on a non-escalated ticket ⇒ 409. After each rejected call, `SELECT COUNT(*) FROM TicketActivities WHERE TicketId = '<id>'` is unchanged.
   - Customer token: `GET /api/v1/tickets/<id>/activity` ⇒ 403.
   - **Concurrency:** in two terminals fire the same valid transition simultaneously (e.g. PowerShell `Start-Job` ×2 with `Invoke-RestMethod`) ⇒ one 200, the other 409 (stale `fromStatus` or row-version message); exactly one `StatusChanged` row is added.
   - `curl -s http://localhost:5080/openapi/v1.json` lists the five new operations with "Requires role: …" descriptions; `TicketStatus` enum shows `Open, InProgress, Resolved, Closed`; `TicketActivityType` / `TicketField` are string enums.
11. **Strings and persistence:** `SELECT DISTINCT Status FROM Tickets; SELECT Type, Field, OldValue, NewValue FROM TicketActivities;` show names (`InProgress`, `StatusChanged`, `Priority`, `Medium`…), never digits; `GET /api/v1/tickets/<id>` returns `"status":"InProgress"`, `"isEscalated":false`; `GET /api/v1/tickets?status=inprogress` filters correctly; `GET /api/v1/tickets?escalated=true` returns only escalated tickets (Agent: only their own). Restart the API and confirm activity is still listed (durable).
12. **Regression:** TICK-001 — Agent create (self-assigned, "Assigned to you automatically."), SuperAdmin create with active Agent / Unassigned, list search/filters/paging, edit; AUTH-001 login/logout/Agents admin/Profile; CUST-001 customers list/create/edit. `grep -rnE "(margin|padding)-(left|right)|text-align: *(left|right)" frontend/src` returns nothing; `grep -rnE "localStorage|sessionStorage" frontend/src/app --include=*.ts` matches only `auth-service.spec.ts`.

---

## Done Criteria

- [ ] TICK-001 module extended in place (same `Ticket`, `Tickets` table, `ITicketRepository`, `TicketsController`, Angular `features/tickets/`); no duplicate ticket system; existing ticket IDs, customers, creators, assignees, categories, priorities and timestamps unchanged (Verification 3.5).
- [ ] Creation unchanged: Agent ⇒ self-assigned; SuperAdmin ⇒ active Agent or Unassigned; every new ticket starts `Open` and gets a `Created` event.
- [ ] Only SuperAdmin can assign / reassign / unassign existing tickets (`SuperAdmin` policy ⇒ 403 for Agents); targets must be active Agents or null; no-op produces no event / timestamp change; Agents cannot claim or transfer.
- [ ] Reassignment moves visibility immediately: former Agent gets 404 on detail / edit / actions / activity and loses the list entry; new Agent gains it; unassigned tickets stay SuperAdmin-only.
- [ ] `TicketStatus` = `Open`, `InProgress`, `Resolved`, `Closed` stored and serialized as names; only the five matrix transitions succeed; Agents only on currently assigned tickets; SuperAdmin on any (no auto-assign).
- [ ] Stale (`fromStatus`) and illegal transitions return 409 without mutating the ticket or history; successful transitions update `UpdatedAtUtc` and add a `StatusChanged` event.
- [ ] Escalation: SuperAdmin or assigned Agent on Open/InProgress with a required reason (≤ 500); duplicate ⇒ 409; only SuperAdmin clears; Resolve auto-clears with an `EscalationAutoCleared` event; reopen does not re-escalate; badge, reason and `escalated` list filter work within Agent scope.
- [ ] `TicketActivities` is append-only (no update/delete API or domain method), each event has actor, UTC time and meaningful change data; field edits record only changed fields; events are saved in the same `SaveChanges` as the ticket change; rejected or no-op requests create no events.
- [ ] Legacy tickets keep their data unchanged, get no fabricated events (no backfill, no client-side "Created" row), and the UI shows the tracking-start note only when the oldest page is loaded and has no `Created` event (Verification 3.5, 9).
- [ ] Activity endpoint is paged (Story 03 rules), ordered `OccurredAtUtc DESC, Id DESC`, scoped exactly like ticket details; Customer ⇒ 403.
- [ ] Detail page shows role-aware workflow buttons, SuperAdmin-only assignment control, escalate / clear escalation, and paged Activity with loading / empty / error / retry / Load more; access loss shows a recoverable message; list shows new statuses, tones, escalation badge and filter.
- [ ] Concurrency: overlapping writes to one ticket ⇒ one succeeds, the other 409 (server-side row version, never client-supplied); stale pages are rejected by `fromStatus` / escalation-state preconditions (409); stale basic edits / assignment choices remain last-write-wins; all three behaviours documented.
- [ ] After any 409 the Angular detail page shows the message, refreshes ticket + activity, keeps typed input and allows retry; buttons are disabled while a request is in flight (Verification 8).
- [ ] Each ticket command saves at most once; a ticket change and its events commit or roll back together; rejected and no-op requests add no events (Verification 8, 10).
- [ ] Migration `AddTicketWorkflowAndActivity` is additive (static review + SQL preview), applied without recreating the database, no backfill rows, counts unchanged; earlier migrations untouched.
- [ ] `dotnet build`, `dotnet test` (5 passed), `npx ng build`, `npm test -- --watch=false` (5 passed) succeed; exactly one new backend and one new frontend test; manual Verification 3–12 pass.
- [ ] `docs/ticket-management.md`, `docs/identity-and-access.md`, `docs/api-conventions.md`, `docs/persistence-and-transactions.md`, `docs/frontend-ui-foundation.md` and `README.md` describe workflow, assignment, escalation, activity (incl. pre-TICK-002 limitation), concurrency, migration impact and deferred features.
- [ ] No out-of-scope features (claim/transfer, routing, SLA, notifications, escalation levels, comments, attachments, deletion, Customer Portal, configurable workflows, event sourcing) were added.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 06.**
