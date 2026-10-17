# Story 06 — Agent Dashboard — Ticket Overview & Work Queue

## Prerequisites

- **Story 05 completed:** [../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md) (`TICK-002`, verified committed as `c2f7264`): `TicketStatus { Open, InProgress, Resolved, Closed }`, `Ticket.IsEscalated`, live reassignment via `PUT /tickets/{id}/assignee`, the SQL `agentScope` in `TicketRepository`, ticket detail page with workflow actions and activity.
- **Story 04 completed:** [../ticket-management/04-story-ticket-creation-management.md](../ticket-management/04-story-ticket-creation-management.md) (`TICK-001`, `b44c824`): `Ticket`, `ITicketRepository`, `TicketsController`, Angular `features/tickets/` (list `/tickets`, create `/tickets/new`, detail `/tickets/:id`).
- **Stories 01–03 completed:** [../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md) (Mediator/CQRS, `ApiRoutes.V1`, `GlobalExceptionHandler`), [../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md) (JWT `sub` / `role`, `OnTokenValidated` active-account check, `AuthorizationPolicies`, `roleGuard`, `NAV_ITEMS.roles`), [../customer-management/03-story-customer-profiles-contact-management.md](../customer-management/03-story-customer-profiles-contact-management.md) (Customer details route `/customers/:id`, `Staff` policy on `CustomersController`).
- Applied migrations (verified in `backend/src/Crm.Infrastructure/Persistence/Migrations/`): `20261014234527_InitialCreate`, `20261015011303_AddIdentityAndRefreshSessions`, `20261017183245_AddCustomers`, `20261017194039_AddTickets`, `20261017203222_AddTicketWorkflowAndActivity`. **This story adds no migration and edits none of them.**
- Tooling unchanged: .NET SDK **10.0.400** (`backend/global.json`), EF Core **10.0.13**, Mediator **3.0.2** (source-generated), xUnit 2.9.3, Angular / Material **22.2**, Vitest via `@angular/build:unit-test`. **No new NuGet or npm packages.**
- Intake: `.squad/stories/agent-dashboard/DASH-001/intake.md` (no attachments). This is the first story of the `agent-dashboard` feature.
- Shared contracts touched deliberately (additive only): `AuthorizationPolicies` (+`Agent`), `SecurityRegistration` (+1 policy), `TicketRepository.Scope` (visibility `private` → `internal`, body unchanged), `NAV_ITEMS` (+1 entry), `app.routes.ts` (+1 child route). **No existing API contract, DTO, entity, configuration or Angular ticket/customer file changes.**

---

## Story Goal

Deliver the first read-only **Agent Dashboard** ("My Dashboard") on top of existing ticket and customer data:

1. **Agent-only** endpoint `GET /api/v1/agent-dashboard` (new `Agent` policy). SuperAdmin and Customer ⇒ **403**. The Agent id comes **only** from the validated JWT `sub`; the endpoint has no parameters.
2. **Workload summary** computed in SQL over **all** tickets whose current `AssignedAgentUserId` equals the caller: **Open**, **In Progress**, **Active** (= Open + In Progress), **Escalated** (escalated and Open/InProgress), **High / Urgent** (Open/InProgress and priority High/Urgent). Overlapping counts, never summed in the UI.
3. **Attention queue:** at most **10** assigned `Open` / `InProgress` tickets ordered in SQL: escalated first → priority Urgent, High, Medium, Low → oldest `UpdatedAtUtc` → `Id`. Each item carries subject, category, priority, status, escalation flag, customer id / name / email, last-updated time.
4. **Angular** `features/agent-dashboard/`: route `/agent-dashboard` (guard `roleGuard('Agent')`), nav entry **My Dashboard** (Agent only), four non-clickable summary cards, the ordered queue with links to the existing ticket detail and customer detail screens, **View all my tickets** (`/tickets`), **Create ticket** (`/tickets/new`), **Refresh** button, loading / error + Retry / empty states, responsive logical-property CSS.
5. Exactly **one** new backend and **one** new frontend happy-path unit test; new `docs/agent-dashboard.md` plus doc/README references.

**Not in scope** (do **not** build): tasks / reminders, quick replies / canned responses, team collaboration, SuperAdmin or company-wide dashboard, performance analytics, SLA, charts, polling / sockets / timers / background jobs, cached or persisted counters, new tables or migrations, inline ticket editing / status / assignment / escalation controls on the dashboard, customer editing, changes to `/api/v1/tickets*` or `/api/v1/customers*` contracts, Ticket list query-string filters, Customer Portal.

### Recorded decisions

| Concern | Decision |
|---|---|
| Route | `GET /api/v1/agent-dashboard` — a per-caller **singleton** resource like `/api/v1/me`; documented as an exception to plural naming in `docs/api-conventions.md` line 3. Angular path `/agent-dashboard` (no conflict in `app.routes.ts` lines 22–41). |
| Authorization | New `AuthorizationPolicies.Agent = "Agent"` → `policy.RequireRole(UserRoles.Agent)`. Controller class carries `[Authorize(Policy = AuthorizationPolicies.Agent)]`. SuperAdmin ⇒ 403 (does **not** reuse `Staff`). Disabled / password-changed accounts are already rejected by `OnTokenValidated` (`SecurityRegistration.cs` lines 41–57) ⇒ 401. |
| Actor | Controller passes `User.GetUserId()` (`ClaimsPrincipalExtensions.cs` line 9) into `GetAgentDashboardQuery(Guid AgentUserId)`. No `[FromQuery]` / body parameters exist, so `?agentId=` is ignored by model binding and cannot widen scope. |
| Ownership scoping | One predicate: `TicketRepository.Scope(tickets, agentUserId)` (`TicketRepository.cs` lines 67–69) — the same SQL filter used by ticket list/detail/activity. Change its modifier `private static` → `internal static`; the new repository calls it for both statements. |
| Repository | New `IAgentDashboardRepository` (Application abstraction) + `AgentDashboardRepository` (Infrastructure). **Not** added to `ITicketRepository`, so the two existing hand-written fakes (`CreateTicketCommandHandlerTests`, `TransitionTicketStatusCommandHandlerTests`) stay untouched. |
| Counts | One SQL statement: scope → `Where(Open or InProgress)` → `GroupBy(_ => 1)` → `Select` with filtered `g.Count(predicate)` (`COUNT(CASE WHEN …)`). No rows ⇒ `null` ⇒ all zeros. `Active = Open + InProgress` is computed in the handler (statuses are disjoint). |
| Queue | Second SQL statement: same scope + active filter, inner join `Customers`, `orderby` escalated rank, explicit priority rank (`CASE`, **never** alphabetical on the `nvarchar` column), `UpdatedAtUtc` ASC, `Id` ASC, then `Take(10)`. `AsNoTracking`. |
| Snapshot semantics | Live on every request; no caching, no persisted counters. Counts and queue are two statements without an explicit transaction: a write committed between them can make them differ by that one change until the next refresh (documented). `GeneratedAtUtc` (from `TimeProvider`) is returned and shown as "Updated …". |
| Index / migration | The existing `(AssignedAgentUserId, CreatedAtUtc DESC, Id DESC)` index (`TicketConfiguration.cs` line 41) has `AssignedAgentUserId` as its leading key, so both statements can seek to the caller's rows. It does **not** necessarily cover every field the statements filter, group, order or select (`Status`, `Priority`, `IsEscalated`, `UpdatedAtUtc`, `Subject`, `Category`, `CustomerId`): SQL Server may read those via key lookups on the Agent's rows, and the queue order (escalation / priority rank / `UpdatedAtUtc` / `Id`) is a sort over that per-Agent set, not an index-ordered scan. Per-Agent row counts are small, so this is acceptable. **No new index, no migration.** |
| Customer context | Name (`FirstName + " " + LastName`, as `TicketRepository.Project` line 89) and `Email` from `Customers`, plus `CustomerId` for the link to `/customers/:id` (Agents already have `Staff` access to `GET /api/v1/customers/{id}`). No Identity fields. |
| Summary-card links | `TicketListPage` does not read query parameters (`ticket-list-page.ts` lines 74–80 initialise filters to `null`), so cards are **non-clickable**. Only "View all my tickets" links to `/tickets`. |
| UI refresh | Load in the constructor (runs on every navigation to the route, i.e. on revisit) and on the **Refresh** / **Retry** buttons. `switchMap` cancels an in-flight load. On failure, hide the cards and queue and show the error + Retry (never stale numbers or zeros). |
| Queue rendering | Semantic `<ol>` (order is meaningful), each item a wrapped flex row; no `mat-table` (better on handset widths). |

---

## Context — Read These Files First

1. `.squad/stories/agent-dashboard/DASH-001/intake.md` — §2 audience, §3 metric table, §4 ordering, §6 UI, Acceptance criteria are the contract.
2. [../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md) — tone, layering and test style this story follows (Recorded decisions ~lines 26–48; Test Plan; Verification Steps).
3. `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs` — `Scope` (lines 67–69, **the** ownership filter), `Project` (lines 71–101: `join c in dbContext.Customers`, customer name expression line 89, `orderby` placed before `select` because EF cannot order by record members), `ListAsync` `CountAsync` + `Skip/Take` (58–62).
4. `backend/src/Crm.Domain/Tickets/Ticket.cs` lines 15–29 (`Status`, `Priority`, `AssignedAgentUserId`, `UpdatedAtUtc`, `IsEscalated`); `TicketStatus.cs` lines 7–13; `TicketPriority.cs` lines 4–10 (`Low=0 … Urgent=3`, stored as names); `TicketCategory.cs`.
5. `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketConfiguration.cs` lines 20–22 (`HasConversion<string>()` — why priority needs an explicit rank) and lines 40–42 (indexes).
6. `backend/src/Crm.Application/Tickets/TicketActor.cs` lines 7–11 and `GetTicket/GetTicketQuery.cs` lines 7–14 (query + handler in one file, primary-constructor handler, `IQuery<T>` / `IQueryHandler<,>`). `Diagnostics/GetSystemStatus/GetSystemStatusQuery.cs` lines 5–15 (`TimeProvider` injection in a query handler).
7. `backend/src/Crm.Application/Abstractions/Persistence/ITicketRepository.cs` lines 7–31 (doc-comment style for repository contracts).
8. `backend/src/Crm.Infrastructure/DependencyInjection.cs` lines 37–40 (repository registrations).
9. `backend/src/Crm.Api/Controllers/TicketsController.cs` lines 23–26 (class attributes), 28–50 (`EndpointDescription` wording "Requires role: …"), 107 (actor from JWT). `Crm.Api/ApiRoutes.cs` line 10. `Contracts/Tickets/TicketResponse.cs` lines 7–33 (`From(...)` mapping style).
10. `backend/src/Crm.Api/Authentication/AuthorizationPolicies.cs` lines 3–8; `SecurityRegistration.cs` lines 61–65 (policy chain) and 41–57 (active-account check); `ClaimsPrincipalExtensions.cs` line 9; `Crm.Domain/Identity/UserRoles.cs` line 7.
11. `backend/src/Crm.Api/DependencyInjection.cs` lines 48–58 — Mediator scans the `Crm.Application` assembly (`typeof(GetSystemStatusQuery)`); new handlers need **no** registration.
12. `backend/tests/Crm.UnitTests/Tickets/TransitionTicketStatusCommandHandlerTests.cs` — whole file (91 lines): test naming, hand-written private fakes, `FixedTimeProvider` (lines 87–90). `Crm.UnitTests.csproj` references only `Crm.Application`.
13. `frontend/src/app/app.routes.ts` lines 22–41 (shell children, `roleGuard`, comment at line 40); `core/layout/navigation.ts` lines 12–19; `core/auth/auth-guards.ts` lines 14–19; `core/layout/shell/shell.ts` lines 33–36 (role filter).
14. `frontend/src/app/features/tickets/ticket-list-page.ts` lines 89–111 (`Subject` + `switchMap` + per-request `catchError` load pattern), `ticket-list-page.html` lines 56–62 (progress bar, `role="alert"` error) and 95–104 (priority / status / Escalated badges); `ticket-models.ts` lines 4–11 (types), 67–76 (`priorityTone`), 118–131 (`statusLabel`, `statusTone`).
15. `frontend/src/app/features/tickets/tickets-api.ts` lines 17–21 and 44 (`ApiUrl.versioned`); `tickets-api.spec.ts` whole (52 lines) — the spec pattern to copy.
16. `frontend/src/app/features/home/home-page.html` lines 5–30 (`mat-card appearance="outlined"`, refresh button with `<mat-icon>refresh</mat-icon>`); `features/tickets/ticket-detail-page.scss` (`.hint`, `.form-error` styles); `src/styles.scss` lines 16–27 (`--crm-*` tokens); `shared/ui/status-badge/status-badge.ts`, `shared/ui/page-header/page-header.ts`; `core/api/describe-http-error.ts` lines 5–14.
17. Docs: `docs/ticket-management.md` (section 7 lines 94–100, section 12 lines 144–149), `docs/identity-and-access.md` lines 7–22 (matrix) and 26–49 (routes), `docs/api-conventions.md` line 3, `docs/frontend-ui-foundation.md` lines 14–15, `README.md` line 3 and lines 103–110.

---

## Product rules (from story)

| Metric / rule | Definition (server-side, current assignee = caller) |
|---|---|
| **Open** | `Status == Open` |
| **In Progress** | `Status == InProgress` |
| **Active** | `Open + In Progress` (disjoint) — shown as a caption, not a fifth card |
| **Escalated** | `IsEscalated && Status in (Open, InProgress)` |
| **High / Urgent** | `Status in (Open, InProgress) && Priority in (High, Urgent)` |
| Overlap | A ticket can count in Open/In Progress **and** Escalated **and** High/Urgent. UI text: "Counts overlap — one ticket can appear in several cards." |
| Attention queue | ≤ 10 active tickets; `IsEscalated` desc → Urgent, High, Medium, Low → `UpdatedAtUtc` asc → `Id` asc. Display ordering only — not an SLA, does not change priority. |
| Excluded | Resolved / Closed, unassigned, other Agents' tickets, tickets the Agent created but no longer holds. |

---

## Backend Tasks

No Domain changes. No EF configuration or migration changes.

### 1 — Application: read models and repository contract

**Create file: `backend/src/Crm.Application/AgentDashboard/AgentDashboardSnapshot.cs`**

```csharp
using Crm.Domain.Tickets;

namespace Crm.Application.AgentDashboard;

/// <summary>
/// Live, read-only view of the tickets currently assigned to one Agent (DASH-001). Built per request; nothing is cached or stored.
/// Counts overlap (one ticket can be Open, Escalated and High/Urgent at once) and must never be added together.
/// </summary>
public sealed record AgentDashboardSnapshot(
    AgentWorkloadSummary Summary,
    IReadOnlyList<AttentionTicket> AttentionQueue,
    int AttentionQueueLimit,
    DateTimeOffset GeneratedAtUtc);

/// <summary>Active = Open + InProgress (disjoint). Escalated and HighOrUrgent count only Open/InProgress tickets.</summary>
public sealed record AgentWorkloadSummary(int Open, int InProgress, int Active, int Escalated, int HighOrUrgent);

/// <summary>Raw SQL aggregate; Active is derived by the handler.</summary>
public sealed record AgentWorkloadCounts(int Open, int InProgress, int Escalated, int HighOrUrgent);

/// <summary>Queue row. Customer name/email joined from Customers at query time; no Identity fields.</summary>
public sealed record AttentionTicket(
    Guid Id,
    string Subject,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    bool IsEscalated,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset UpdatedAtUtc);

public static class AgentDashboardRules
{
    public const int AttentionQueueLimit = 10;
}
```

> Namespace is `Crm.Application.AgentDashboard`; **do not** name any type `AgentDashboard` (would clash with the namespace).

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IAgentDashboardRepository.cs`**

```csharp
using Crm.Application.AgentDashboard;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Read-only DASH-001 projections. Both methods are restricted in SQL to tickets whose CURRENT AssignedAgentUserId
/// equals agentUserId (same filter as ITicketRepository's agentScope) and to Open/InProgress tickets.
/// </summary>
public interface IAgentDashboardRepository
{
    /// <summary>One aggregate statement; all zeros when the Agent has no active tickets.</summary>
    Task<AgentWorkloadCounts> GetWorkloadCountsAsync(Guid agentUserId, CancellationToken cancellationToken = default);

    /// <summary>Escalated first, then Urgent > High > Medium > Low, then oldest UpdatedAtUtc, then Id. Ordered and limited in SQL.</summary>
    Task<IReadOnlyList<AttentionTicket>> ListAttentionQueueAsync(Guid agentUserId, int limit, CancellationToken cancellationToken = default);
}
```

### 2 — Application: query + handler

**Create file: `backend/src/Crm.Application/AgentDashboard/GetAgentDashboard/GetAgentDashboardQuery.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Mediator;

namespace Crm.Application.AgentDashboard.GetAgentDashboard;

/// <summary>AgentUserId comes from the validated JWT (sub) only — never from the request.</summary>
public sealed record GetAgentDashboardQuery(Guid AgentUserId) : IQuery<AgentDashboardSnapshot>;

public sealed class GetAgentDashboardQueryHandler(IAgentDashboardRepository dashboard, TimeProvider timeProvider)
    : IQueryHandler<GetAgentDashboardQuery, AgentDashboardSnapshot>
{
    public async ValueTask<AgentDashboardSnapshot> Handle(GetAgentDashboardQuery query, CancellationToken cancellationToken)
    {
        var counts = await dashboard.GetWorkloadCountsAsync(query.AgentUserId, cancellationToken);
        var queue = await dashboard.ListAttentionQueueAsync(query.AgentUserId, AgentDashboardRules.AttentionQueueLimit, cancellationToken);

        var summary = new AgentWorkloadSummary(
            counts.Open, counts.InProgress, counts.Open + counts.InProgress, counts.Escalated, counts.HighOrUrgent);
        return new AgentDashboardSnapshot(summary, queue, AgentDashboardRules.AttentionQueueLimit, timeProvider.GetUtcNow());
    }
}
```

- No validator (the query has no client input). Not `ITransactionalCommand`; no `IUnitOfWork`.
- `TimeProvider` is already resolvable (used by `LoginCommandHandler`, `GetSystemStatusQueryHandler`).

### 3 — Infrastructure: repository

**File: `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs`** — line 68: change `private static IQueryable<Ticket> Scope(` to `internal static IQueryable<Ticket> Scope(`. Update the comment on line 67 to: `// Applied first, in SQL: an Agent can never see or edit a ticket assigned to someone else or unassigned. Also used by AgentDashboardRepository.` **No other change** in this file.

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/AgentDashboardRepository.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.AgentDashboard;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class AgentDashboardRepository(CrmDbContext dbContext) : IAgentDashboardRepository
{
    public async Task<AgentWorkloadCounts> GetWorkloadCountsAsync(Guid agentUserId, CancellationToken cancellationToken = default) =>
        await ActiveAssignedTo(agentUserId)
            .GroupBy(_ => 1) // single group => one COUNT(CASE WHEN ...) statement
            .Select(g => new AgentWorkloadCounts(
                g.Count(t => t.Status == TicketStatus.Open),
                g.Count(t => t.Status == TicketStatus.InProgress),
                g.Count(t => t.IsEscalated),
                g.Count(t => t.Priority == TicketPriority.High || t.Priority == TicketPriority.Urgent)))
            .SingleOrDefaultAsync(cancellationToken)
        ?? new AgentWorkloadCounts(0, 0, 0, 0);

    public async Task<IReadOnlyList<AttentionTicket>> ListAttentionQueueAsync(
        Guid agentUserId, int limit, CancellationToken cancellationToken = default) =>
        await (
                from t in ActiveAssignedTo(agentUserId)
                join c in dbContext.Customers on t.CustomerId equals c.Id
                // Priority is stored as a name: rank explicitly, never alphabetically.
                orderby t.IsEscalated ? 0 : 1,
                    t.Priority == TicketPriority.Urgent ? 0
                    : t.Priority == TicketPriority.High ? 1
                    : t.Priority == TicketPriority.Medium ? 2
                    : 3,
                    t.UpdatedAtUtc,
                    t.Id
                select new AttentionTicket(
                    t.Id,
                    t.Subject,
                    t.Category,
                    t.Priority,
                    t.Status,
                    t.IsEscalated,
                    t.CustomerId,
                    c.FirstName + " " + c.LastName,
                    c.Email,
                    t.UpdatedAtUtc))
            .Take(limit)
            .ToListAsync(cancellationToken);

    // The one ownership filter shared with ticket list/detail/activity, then "needs attention" = Open or InProgress.
    private IQueryable<Ticket> ActiveAssignedTo(Guid agentUserId) =>
        TicketRepository.Scope(dbContext.Tickets.AsNoTracking(), agentUserId)
            .Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress);
}
```

- Because `ActiveAssignedTo` already restricts to Open/InProgress, the Escalated and High/Urgent predicates are exactly the §3 definitions.
- `Customer` property names: confirm `FirstName`, `LastName`, `Email` against `TicketRepository.cs` lines 89–90 (identical expressions).

**File: `backend/src/Crm.Infrastructure/DependencyInjection.cs`** — after line 40 add:

```csharp
        services.AddScoped<IAgentDashboardRepository, AgentDashboardRepository>();
```

### 4 — API: policy, contract, controller

**File: `backend/src/Crm.Api/Authentication/AuthorizationPolicies.cs`** — after line 6 add:

```csharp
    public const string Agent = "Agent";       // Agent role only (SuperAdmin excluded)
```

**File: `backend/src/Crm.Api/Authentication/SecurityRegistration.cs`** — in the chain at lines 63–65 add (before the `Customer` policy, keep the trailing `;` on the last call):

```csharp
            .AddPolicy(AuthorizationPolicies.Agent, policy => policy.RequireRole(UserRoles.Agent))
```

**Create file: `backend/src/Crm.Api/Contracts/AgentDashboard/AgentDashboardResponse.cs`**

```csharp
using Crm.Application.AgentDashboard;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.AgentDashboard;

/// <summary>Counts overlap and are not additive; Active = Open + InProgress. AttentionQueue is the top AttentionQueueLimit only.</summary>
public sealed record AgentDashboardResponse(
    AgentWorkloadSummaryResponse Summary,
    IReadOnlyList<AttentionTicketResponse> AttentionQueue,
    int AttentionQueueLimit,
    DateTimeOffset GeneratedAtUtc)
{
    public static AgentDashboardResponse From(AgentDashboardSnapshot s) =>
        new(new AgentWorkloadSummaryResponse(s.Summary.Open, s.Summary.InProgress, s.Summary.Active, s.Summary.Escalated, s.Summary.HighOrUrgent),
            s.AttentionQueue.Select(AttentionTicketResponse.From).ToList(),
            s.AttentionQueueLimit,
            s.GeneratedAtUtc);
}

public sealed record AgentWorkloadSummaryResponse(int Open, int InProgress, int Active, int Escalated, int HighOrUrgent);

/// <summary>Customer shown by id + name + email only (no Identity fields).</summary>
public sealed record AttentionTicketResponse(
    Guid Id,
    string Subject,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    bool IsEscalated,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    DateTimeOffset UpdatedAtUtc)
{
    public static AttentionTicketResponse From(AttentionTicket t) =>
        new(t.Id, t.Subject, t.Category, t.Priority, t.Status, t.IsEscalated, t.CustomerId, t.CustomerName, t.CustomerEmail, t.UpdatedAtUtc);
}
```

JSON (camelCase, enums as names via the global converter):

```json
{
  "summary": { "open": 3, "inProgress": 2, "active": 5, "escalated": 1, "highOrUrgent": 2 },
  "attentionQueue": [
    { "id": "…", "subject": "Cannot sign in", "category": "Technical", "priority": "Urgent", "status": "InProgress",
      "isEscalated": true, "customerId": "…", "customerName": "Ada Lovelace", "customerEmail": "ada@example.com",
      "updatedAtUtc": "2030-01-01T09:00:00+00:00" }
  ],
  "attentionQueueLimit": 10,
  "generatedAtUtc": "2030-01-01T10:00:00+00:00"
}
```

**Create file: `backend/src/Crm.Api/Controllers/AgentDashboardController.cs`**

```csharp
using Crm.Api.Authentication;
using Crm.Api.Contracts.AgentDashboard;
using Crm.Application.AgentDashboard.GetAgentDashboard;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/agent-dashboard")]
[Authorize(Policy = AuthorizationPolicies.Agent)]
public sealed class AgentDashboardController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: Agent (SuperAdmin and Customer: 403). Read-only live snapshot of the tickets currently assigned to the caller (taken from the token; no parameters). Summary counts overlap and are not additive: open, inProgress, active (= open + inProgress), escalated (escalated and Open/InProgress), highOrUrgent (Open/InProgress with priority High or Urgent). attentionQueue: at most 10 Open/InProgress tickets, escalated first, then Urgent, High, Medium, Low, then oldest updatedAtUtc, then id — a display order, not an SLA.")]
    [HttpGet]
    public async Task<ActionResult<AgentDashboardResponse>> Get(CancellationToken cancellationToken) =>
        Ok(AgentDashboardResponse.From(await sender.Send(new GetAgentDashboardQuery(User.GetUserId()), cancellationToken)));
}
```

- Route is lowercase by `LowercaseUrls`; final URL `/api/v1/agent-dashboard`.
- **Do not** touch `TicketsController`, `CustomersController`, `GlobalExceptionHandler`.

### 5 — Backend unit test (single happy path)

**Create file: `backend/tests/Crm.UnitTests/AgentDashboard/GetAgentDashboardQueryHandlerTests.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.AgentDashboard;
using Crm.Application.AgentDashboard.GetAgentDashboard;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.AgentDashboard;

public sealed class GetAgentDashboardQueryHandlerTests
{
    [Fact]
    public async Task Handle_Agent_ReturnsOwnScopedOverlappingCountsActiveTotalAndBoundedQueue()
    {
        // Arrange: FakeAgentDashboardRepository returning counts (Open 3, InProgress 2, Escalated 1, HighOrUrgent 2)
        // and two AttentionTicket rows (escalated Urgent first, then High); FixedTimeProvider(now).
        // Act: handler.Handle(new GetAgentDashboardQuery(agentId), CancellationToken.None).
        // Assert:
        //  - repository received agentId for BOTH calls (CountsRequestedFor, QueueRequestedFor) and limit == 10;
        //  - Summary == (3, 2, Active 5, 1, 2) — Active is Open + InProgress, not the sum of all cards;
        //  - AttentionQueue is the repository list in the same order (ids equal by index);
        //  - AttentionQueueLimit == 10 and GeneratedAtUtc == now.
    }

    // private sealed class FakeAgentDashboardRepository : IAgentDashboardRepository — records the agent id / limit it was called with.
    // private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider — copy from TransitionTicketStatusCommandHandlerTests lines 87–90.
}
```

Write the test body in full (the comments describe required assertions). Hand-written fakes only; no mocking library. **Do not modify** any existing test file.

---

## Frontend Tasks

### 6 — Models and API

**Create file: `frontend/src/app/features/agent-dashboard/agent-dashboard-models.ts`**

```ts
import { TicketCategory, TicketPriority, TicketStatus } from '../tickets/ticket-models';

/** Matches Crm.Api AgentWorkloadSummaryResponse. Counts overlap — never add them up. active = open + inProgress. */
export interface AgentWorkloadSummary {
  open: number;
  inProgress: number;
  active: number;
  escalated: number;
  highOrUrgent: number;
}

/** Matches Crm.Api AttentionTicketResponse. */
export interface AttentionTicket {
  id: string;
  subject: string;
  category: TicketCategory;
  priority: TicketPriority;
  status: TicketStatus;
  isEscalated: boolean;
  customerId: string;
  customerName: string;
  customerEmail: string;
  updatedAtUtc: string;
}

/** Matches Crm.Api AgentDashboardResponse. */
export interface AgentDashboard {
  summary: AgentWorkloadSummary;
  attentionQueue: AttentionTicket[];
  attentionQueueLimit: number;
  generatedAtUtc: string;
}
```

**Create file: `frontend/src/app/features/agent-dashboard/agent-dashboard-api.ts`**

```ts
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { AgentDashboard } from './agent-dashboard-models';

@Injectable({ providedIn: 'root' })
export class AgentDashboardApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  /** No parameters: the server derives the Agent from the access token and scopes every number to them. */
  get(): Observable<AgentDashboard> {
    return this.http.get<AgentDashboard>(this.apiUrl.versioned('agent-dashboard'));
  }
}
```

### 7 — Dashboard page

**Create file: `frontend/src/app/features/agent-dashboard/agent-dashboard-page.ts`**

- Selector `crm-agent-dashboard-page`, `ChangeDetectionStrategy.OnPush`, `templateUrl` / `styleUrl`.
- Imports: `RouterLink`, `DatePipe`, `PageHeader`, `StatusBadge`, `MatCardModule`, `MatButtonModule`, `MatIconModule`, `MatProgressBarModule`.
- State (signals): `dashboard = signal<AgentDashboard | null>(null)`, `loading = signal(true)`, `error = signal<string | null>(null)`.
- Expose helpers `priorityTone`, `statusLabel`, `statusTone` from `../tickets/ticket-models`.
- Load pattern copied from `ticket-list-page.ts` lines 89–111: a `private readonly requests = new Subject<void>()`, piped through `switchMap(() => this.api.get().pipe(catchError(...)))` and `takeUntilDestroyed(this.destroyRef)`.
  - `catchError`: `this.dashboard.set(null)` (**drop stale data**), `this.error.set(describeHttpError(error, 'Could not load your dashboard.'))`, `this.loading.set(false)`, `return EMPTY`.
  - `subscribe(result => { this.dashboard.set(result); this.loading.set(false); })`.
- `protected refresh(): void { this.loading.set(true); this.error.set(null); this.requests.next(); }` — called at the end of the constructor and by the Refresh / Retry buttons. No timers, no intervals.

**Create file: `frontend/src/app/features/agent-dashboard/agent-dashboard-page.html`** — structure:

1. `<crm-page-header title="My Dashboard" subtitle="Live overview of the tickets currently assigned to you." />`
2. `.toolbar` row: `@if (dashboard(); as d) { <span class="hint">Updated {{ d.generatedAtUtc | date: 'medium' }}</span> }`, then buttons: `<button mat-stroked-button type="button" (click)="refresh()" [disabled]="loading()"><mat-icon>refresh</mat-icon> Refresh</button>`, `<a mat-stroked-button routerLink="/tickets">View all my tickets</a>`, `<a mat-flat-button routerLink="/tickets/new"><mat-icon>add</mat-icon> Create ticket</a>`.
3. `@if (loading()) { <mat-progress-bar mode="indeterminate" /> }`
4. `@if (error(); as message) { <p class="error" role="alert">{{ message }}</p> <button mat-stroked-button type="button" (click)="refresh()">Retry</button> }`
5. `@if (dashboard(); as d) {` (rendered only after a successful load; hidden after a failed one):
   - `<section aria-labelledby="workload-heading">` with `<h2 id="workload-heading">Workload</h2>`, `<p class="hint">{{ d.summary.active }} active tickets (Open + In Progress). Counts overlap — one ticket can appear in several cards.</p>` and `.summary-grid` of four `<mat-card appearance="outlined" class="summary-card">` (**no** `routerLink`): label + big number for **Open** (`d.summary.open`), **In Progress** (`d.summary.inProgress`), **Escalated** (`d.summary.escalated`), **High / Urgent** (`d.summary.highOrUrgent`).
   - `<section aria-labelledby="queue-heading">` with `<h2 id="queue-heading">Needs attention</h2>` and `<p class="hint">Top {{ d.attentionQueueLimit }} of your Open and In Progress tickets: escalated first, then Urgent, High, Medium, Low, then longest without an update. This is a display order, not an SLA.</p>`
   - Empty: `@if (d.attentionQueue.length === 0) { <p>No Open or In Progress tickets are assigned to you right now.</p> }` (shown also when the Agent only has Resolved/Closed tickets).
   - Else `<ol class="queue">` with `@for (t of d.attentionQueue; track t.id)` `<li class="queue-item">`:
     - `<a class="subject" [routerLink]="['/tickets', t.id]">{{ t.subject }}</a>`
     - badges: `@if (t.isEscalated) { <crm-status-badge label="Escalated" tone="error" /> }`, `<crm-status-badge [label]="t.priority" [tone]="priorityTone(t.priority)" />`, `<crm-status-badge [label]="statusLabel(t.status)" [tone]="statusTone(t.status)" />`
     - meta line: `{{ t.category }}` · `<a [routerLink]="['/customers', t.customerId]">{{ t.customerName }}</a>` · `<span class="email">{{ t.customerEmail }}</span>` · `Updated {{ t.updatedAtUtc | date: 'medium' }}`
   - `@if (d.summary.active > d.attentionQueue.length) { <p class="hint">Showing {{ d.attentionQueue.length }} of {{ d.summary.active }} active tickets. <a routerLink="/tickets">View all my tickets</a></p> }`
6. No edit / status / assignment / escalation controls anywhere on the page.

**Create file: `frontend/src/app/features/agent-dashboard/agent-dashboard-page.scss`** — logical properties only (`docs/frontend-ui-foundation.md` line 13):

```scss
.toolbar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: flex-end;
  gap: var(--crm-space-3);
  margin-block-end: var(--crm-space-4);
}

section {
  margin-block-start: var(--crm-space-6);
}

h2 {
  font: var(--mat-sys-title-large);
  margin-block: 0 var(--crm-space-2);
}

.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(160px, 1fr));
  gap: var(--crm-space-4);
  margin-block-start: var(--crm-space-4);
}

.summary-card mat-card-content {
  display: grid;
  gap: var(--crm-space-1);
  padding-block: var(--crm-space-4);
}

.count {
  font: var(--mat-sys-display-small);
}

.queue {
  list-style: none;
  margin: 0;
  padding: 0;
  display: grid;
  gap: var(--crm-space-3);
}

.queue-item {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: var(--crm-space-2) var(--crm-space-3);
  padding: var(--crm-space-3) var(--crm-space-4);
  border: 1px solid var(--mat-sys-outline-variant);
  border-radius: var(--crm-radius-md);
}

.subject {
  flex: 1 1 240px;
  font: var(--mat-sys-title-medium);
}

.meta {
  flex-basis: 100%;
  color: var(--mat-sys-on-surface-variant);
  font: var(--mat-sys-body-medium);
}

.email {
  overflow-wrap: anywhere;
}

.hint {
  margin: 0;
  color: var(--mat-sys-on-surface-variant);
  font: var(--mat-sys-body-medium);
}

.error {
  color: var(--mat-sys-error);
}
```

(Use classes `count` and `meta` in the template for the number and the meta line.)

### 8 — Route and navigation

**Create file: `frontend/src/app/features/agent-dashboard/agent-dashboard.routes.ts`**

```ts
import { Routes } from '@angular/router';

export const AGENT_DASHBOARD_ROUTES: Routes = [
  {
    path: '',
    title: 'My Dashboard | Support CRM',
    loadComponent: () => import('./agent-dashboard-page').then(m => m.AgentDashboardPage),
  },
];
```

**File: `frontend/src/app/app.routes.ts`** — insert before the `tickets` entry (line 25):

```ts
      {
        path: 'agent-dashboard',
        canActivate: [roleGuard('Agent')],
        loadChildren: () => import('./features/agent-dashboard/agent-dashboard.routes').then(m => m.AGENT_DASHBOARD_ROUTES),
      },
```

Keep `''` → Home as the default for every role (do **not** redirect).

**File: `frontend/src/app/core/layout/navigation.ts`** — insert after the Home entry (line 14):

```ts
  { label: 'My Dashboard', icon: 'dashboard', path: '/agent-dashboard', exact: false, roles: ['Agent'] },
```

### 9 — Frontend unit test (single happy path)

**Create file: `frontend/src/app/features/agent-dashboard/agent-dashboard-api.spec.ts`** — copy the setup of `tickets-api.spec.ts` lines 1–21 (`provideHttpClient()`, `provideHttpClientTesting()`, `API_CONFIG` `{ baseUrl: '/api', version: 'v1' }`).

```ts
describe('AgentDashboardApi', () => {
  it('requests the caller-scoped dashboard without any agent parameter and returns the typed snapshot', () => {
    // api.get().subscribe(d => (result = d));
    // const request = http.expectOne(r => r.url === '/api/v1/agent-dashboard');
    // expect(request.request.method).toBe('GET');
    // expect(request.request.params.keys()).toEqual([]);   // ownership comes from the token only
    // request.flush({ summary: { open: 3, inProgress: 2, active: 5, escalated: 1, highOrUrgent: 2 },
    //   attentionQueue: [ { id: 't1', subject: 'Cannot sign in', category: 'Technical', priority: 'Urgent', status: 'InProgress',
    //     isEscalated: true, customerId: 'c1', customerName: 'Ada Lovelace', customerEmail: 'ada@example.com',
    //     updatedAtUtc: '2030-01-01T09:00:00Z' } ],
    //   attentionQueueLimit: 10, generatedAtUtc: '2030-01-01T10:00:00Z' });
    // expect(result?.summary.active).toBe(5);
    // expect(result?.attentionQueue[0].customerName).toBe('Ada Lovelace');
    // expect(result?.attentionQueueLimit).toBe(10);
    // http.verify();
  });
});
```

Write the body in full. **Do not modify** any existing spec.

---

## Documentation Tasks

### 10 — `docs/agent-dashboard.md` (new)

Sections:

1. **Audience** — Agent only (`AuthorizationPolicies.Agent`); SuperAdmin keeps Home / Tickets (no admin dashboard; future Reports & Management); Customer no access. Nav entry "My Dashboard" (`/agent-dashboard`, `roleGuard('Agent')`).
2. **Data scope** — tickets whose **current** `AssignedAgentUserId` = token `sub`; same SQL filter as tickets (`TicketRepository.Scope`); no request parameter; reassignment is reflected on the next load; created-by is irrelevant.
3. **Metrics** — the Product rules table (Open, In Progress, Active, Escalated, High / Urgent), "counts overlap, never add them", Resolved/Closed excluded (still in Tickets).
4. **Attention queue** — ≤ 10, ordering rule, "display order, not an SLA; priority is never changed", fields shown, links (ticket detail, customer detail), "View all my tickets", "Create ticket".
5. **Refresh semantics** — live per request; loads on every visit and on Refresh; no caching, polling, sockets, timers or stored counters; counts and queue are two consecutive queries (a change committed between them shows fully on the next refresh); failed loads show an error with Retry, never zeros or stale numbers.
6. **API** — `GET /api/v1/agent-dashboard`, Agent ⇒ 200; SuperAdmin / Customer ⇒ 403; unauthenticated / disabled ⇒ 401; response shape (JSON above).
7. **Persistence** — read-only, no migration, no new index. The existing `AssignedAgentUserId`-leading index `(AssignedAgentUserId, CreatedAtUtc, Id)` supports the Agent filter; it does not necessarily cover the other filtered, grouped, ordered or selected columns (those may be looked up and sorted per Agent), which is acceptable at per-Agent volumes. Ticket statements per load: two (grouped counts + queue), or five if the four-`CountAsync` fallback was needed — all in SQL under the Agent scope.
8. **Deferred Agent Dashboard capabilities** — tasks & reminders, quick replies, team collaboration (need new business models; later stories); SuperAdmin dashboard / analytics / SLA (Reports & Management, SLA & Automation); clickable filtered summary cards (requires Ticket list query-parameter support).

### 11 — Other docs

- `docs/identity-and-access.md`: matrix (after line 22) add row `| View own Agent Dashboard (assigned-ticket workload) | — | ✔ | — |`; routes table (after line 47) add `| GET | `/api/v1/agent-dashboard` | Agent | 200 |`.
- `docs/api-conventions.md` line 3: change "Singleton resources `/api/v1/auth/*` and `/api/v1/me` are" to "Singleton resources `/api/v1/auth/*`, `/api/v1/me` and `/api/v1/agent-dashboard` (per-caller) are".
- `docs/ticket-management.md` section 12 (lines 144–149): add bullet "- **Agent Dashboard (DASH-001):** read-only consumer of this module — see [agent-dashboard.md](agent-dashboard.md). It adds no ticket operations."
- `README.md` line 3: after "ticket workflow, assignment & history (`TICK-002`)" add " and the Agent Dashboard (`DASH-001`)" (fix the list commas while there: "identity & access (`AUTH-001`), customer profiles & contacts (`CUST-001`), ticket creation & management (`TICK-001`), ticket workflow, assignment & history (`TICK-002`) and the Agent Dashboard (`DASH-001`)"). Conventions list (after line 110): `- [Agent dashboard](docs/agent-dashboard.md)`.

---

## Edge Cases & Failure Modes

- **SuperAdmin calls `GET /api/v1/agent-dashboard`** (with or without `?agentId=<agent>`) — `Agent` policy ⇒ **403** before the handler runs (`AgentDashboardController` class attribute). The Angular route guard `roleGuard('Agent')` redirects to `/` and the nav entry is hidden (`shell.ts` lines 33–36); both are UX only.
- **Customer calls the endpoint** ⇒ 403 (policy). **No / expired token** ⇒ 401 (fallback policy). **Disabled Agent or changed password** ⇒ 401 via `OnTokenValidated` (`SecurityRegistration.cs` lines 53–56); the interceptor's refresh fails and the user is signed out.
- **Client sends `?agentId=` or any query string** — the action has no bound parameters; values are ignored; scope stays `User.GetUserId()`.
- **Agent with zero tickets** — `GroupBy` yields no group ⇒ `SingleOrDefaultAsync` returns `null` ⇒ `new AgentWorkloadCounts(0,0,0,0)`; queue `[]`; UI shows four `0` cards and the empty-queue text. This is a **successful** zero, distinct from an error.
- **Agent with only Resolved / Closed tickets** — excluded by `ActiveAssignedTo`; all counts 0, queue empty.
- **Ticket created by the Agent but reassigned elsewhere / unassigned** — `Scope` filters on `AssignedAgentUserId` only ⇒ excluded.
- **Reassignment while the dashboard is open** — numbers are a snapshot; after Refresh (or revisiting) the ticket disappears for the old Agent and appears for the new one if Open/InProgress. Clicking the stale ticket link before refreshing opens the detail page, which shows its existing 404 message ("Ticket '…' was not found.").
- **Write between the two SQL statements** — counts and queue can disagree by that change until the next refresh. Accepted and documented (no transaction / snapshot isolation added).
- **More than 10 active tickets** — queue holds exactly 10 (`Take(limit)` after `orderby` in SQL); the hint "Showing 10 of N active tickets" + "View all my tickets" link appears (Verification 8).
- **Ties** — equal escalation, priority and `UpdatedAtUtc` ⇒ `Id` ascending (SQL Server `uniqueidentifier` ordering; deterministic across requests). Document only "stable tie-breaker", not a specific GUID order.
- **Priority ordering** — `Priority` is `nvarchar(20)` (`TicketConfiguration.cs` line 21); alphabetical order would be High, Low, Medium, Urgent. The explicit conditional rank must translate to `CASE WHEN [t].[Priority] = N'Urgent' THEN 0 …`; verify in the logged SQL (Verification 6).
- **EF translation of `GroupBy(_ => 1)` + filtered `Count`** — supported by EF Core 10 on SQL Server. If the build/run reports "could not be translated", replace `GetWorkloadCountsAsync` with four `CountAsync` calls on `ActiveAssignedTo(agentUserId)` with the same predicates (same scope; four `SELECT COUNT(*)` statements) and note it in `docs/agent-dashboard.md` section 7. Expected ticket statements per dashboard load: **two** with the preferred grouped count (1 aggregate + 1 queue); **five** with the fallback (4 counts + 1 queue). In both cases every statement runs in SQL with `WHERE [t].[AssignedAgentUserId] = @…` and the Open/InProgress filter; never `ToListAsync` / `AsEnumerable` tickets to count or sort in memory (Verification 6).
- **Customer join** — `CustomerId` is a required `Restrict` FK (`TicketConfiguration.cs` line 25), so the inner join never drops a ticket.
- **Long subject / email on handset** — `.subject` flex-basis wraps; `.email` uses `overflow-wrap: anywhere`; no horizontal page scroll.
- **Refresh clicked repeatedly** — `switchMap` cancels the previous request; button disabled while `loading()`.
- **Refresh fails after a successful load** — `catchError` sets `dashboard` to `null`, so the old numbers disappear and only the error + Retry show. Never show `0` on failure.
- **API offline** — `describeHttpError` returns "The API is not reachable. Start the backend and try again." (`describe-http-error.ts` line 7).
- **Transient SQL error** — `EnableRetryOnFailure` (`Crm.Infrastructure/DependencyInjection.cs` line 33) retries each read; a final failure ⇒ 500 ProblemDetails ⇒ UI error state.
- **Schema drift** — no model change is made; `dotnet ef migrations has-pending-model-changes` must report none (Verification 2).

---

## Test Plan

1. **Unit (backend) — add:** `backend/tests/Crm.UnitTests/AgentDashboard/GetAgentDashboardQueryHandlerTests.cs` → `Handle_Agent_ReturnsOwnScopedOverlappingCountsActiveTotalAndBoundedQueue` (task 5). Pattern: `Tickets/TransitionTicketStatusCommandHandlerTests.cs`.
2. **Unit (frontend) — add:** `frontend/src/app/features/agent-dashboard/agent-dashboard-api.spec.ts` → "requests the caller-scoped dashboard without any agent parameter and returns the typed snapshot" (task 9). Pattern: `features/tickets/tickets-api.spec.ts`.
3. **Keep unchanged (must still pass):** backend `Auth/LoginCommandHandlerTests.cs`, `Customers/CreateCustomerCommandHandlerTests.cs`, `Diagnostics/GetSystemStatusQueryHandlerTests.cs`, `Tickets/CreateTicketCommandHandlerTests.cs`, `Tickets/TransitionTicketStatusCommandHandlerTests.cs`; frontend `core/api/api-url.spec.ts`, `core/auth/auth-service.spec.ts`, `features/customers/customers-api.spec.ts`, `features/tickets/tickets-api.spec.ts`, `features/tickets/ticket-workflow.spec.ts`.
4. **Manual (authorization, aggregate correctness, ordering, UI states, regression):** Verification Steps 4–10.

No other tests, frameworks, mocking libraries, integration / SQL / API / E2E / negative-path / load suites.

---

## Verification Steps

1. **Backend builds and tests:** in `backend/` run `dotnet build` (0 errors) then `dotnet test` (**6** passed: 5 existing + `GetAgentDashboardQueryHandlerTests`).
2. **No schema change, layering intact:** in `backend/`: `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ "No changes have been made to the model since the last migration."; `git status --porcelain backend/src/Crm.Infrastructure/Persistence/Migrations` is empty; `grep -rn "EntityFrameworkCore\|Microsoft.AspNetCore" src/Crm.Application src/Crm.Domain --include=*.cs` returns nothing; `git diff --stat -- src/Crm.Api/Controllers/TicketsController.cs src/Crm.Api/Contracts src/Crm.Application/Tickets src/Crm.Domain src/Crm.Infrastructure/Persistence/Configurations` shows no changes (new files under `Contracts/AgentDashboard/` are untracked, not diffs). `git diff src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs` shows only the `private` → `internal` change and its comment.
3. **Frontend builds and tests:** in `frontend/` run `npx ng build` (no errors; budget warnings acceptable) and `npm test -- --watch=false` (**6** passed).
4. **Setup — dedicated DASH-001 Agents (nothing existing is deleted, reset or reassigned):** from the repo root `.\run-dev.ps1` (or `dotnet run --project backend/src/Crm.Api --launch-profile http` + `npm start` in `frontend/`). "Agent One" / "Agent Two" already hold tickets from TICK-002 verification, so **do not use them for exact-count checks**. As SuperAdmin (`admin@admin.com`) create three new Agents on `/agents/new`: "Dash Alpha" (`dash.alpha@example.test`), "Dash Bravo" (`dash.bravo@example.test`), "Dash Charlie" (`dash.charlie@example.test`). If an email already exists from an earlier run, use a new suffix (e.g. `dash.alpha2@example.test`) — never reuse an Agent that already has tickets.
   - **Baseline (must show 0 assigned for each new Agent before continuing):** `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT u.Email, COUNT(t.Id) AS Assigned FROM AspNetUsers u LEFT JOIN Tickets t ON t.AssignedAgentUserId = u.Id WHERE u.Email LIKE 'dash.%@example.test' GROUP BY u.Email; SELECT COUNT(*) AS TicketsBefore FROM Tickets;"` — save `TicketsBefore`.
   - As SuperAdmin, against any existing customer, create for **Dash Alpha**, one at a time in this order: T1 Low, T2 Urgent, T3 High, T4 Medium, T5 Urgent, T6 Urgent, T7 High; then Start progress on T3 and T4; Escalate T3 and T5 (any reason); Start progress → Resolve T6; Start progress → Resolve → Close T7; then create T9 Low. Create one Medium ticket for **Dash Bravo** (T8). Finally edit T1's subject so T1 has the newest `UpdatedAtUtc` of the two Low tickets. Result for Dash Alpha: T1 Open/Low, T2 Open/Urgent, T3 InProgress/High escalated, T4 InProgress/Medium, T5 Open/Urgent escalated, T6 Resolved, T7 Closed, T9 Open/Low.
5. **Agent UI (http://localhost:4200, signed in as Dash Alpha):** nav shows **My Dashboard** (SuperAdmin and Customer sessions do not). Cards: Open **4** (T1, T2, T5, T9), In Progress **2** (T3, T4), caption "6 active tickets", Escalated **2** (T3, T5), High / Urgent **3** (T2, T3, T5) — T6/T7/T8 never counted. Queue order: T5 (escalated Urgent), T3 (escalated High), T2 (Urgent), T4 (Medium), T9 then T1 (both Low; T9 has the older `UpdatedAtUtc`). Ticket subject opens `/tickets/<id>` with the TICK-002 workflow actions; customer name opens `/customers/<id>`. "View all my tickets" → `/tickets`; "Create ticket" → `/tickets/new`. Cards are not links. "Updated …" timestamp changes on Refresh.
6. **SQL correctness (aggregates and ordering run in SQL):** with `Logging:LogLevel:Microsoft.EntityFrameworkCore.Database.Command` = `Information` in `appsettings.Development.json` (revert afterwards), load Dash Alpha's dashboard once and inspect the commands logged for that request:
   - **Preferred grouped-count implementation — exactly two `SELECT` statements against `Tickets`:** (a) one aggregate with `COUNT(CASE WHEN …)`, `WHERE [t].[AssignedAgentUserId] = @…` and the `Open`/`InProgress` status filter; (b) one queue statement with the same `WHERE`, `ORDER BY CASE WHEN [t].[IsEscalated] … CASE WHEN [t].[Priority] = N'Urgent' THEN 0 …, [t].[UpdatedAtUtc], [t].[Id]` and `TOP(@…)`.
   - **Documented four-`CountAsync` fallback (only if implemented) — exactly five `SELECT` statements:** four `SELECT COUNT(*)` statements, each with `WHERE [t].[AssignedAgentUserId] = @…` and the status filter, plus queue statement (b).
   - Either way: every statement carries the `AssignedAgentUserId` predicate, and no statement returns unbounded ticket rows for in-memory counting or sorting (the only row-returning statement has `TOP`).
   - Cross-check counts: `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT Status, Priority, IsEscalated, COUNT(*) FROM Tickets WHERE AssignedAgentUserId = '<dash-alpha-id>' GROUP BY Status, Priority, IsEscalated"`.
7. **Reassignment, isolation and empty state:** as SuperAdmin reassign T2 to Dash Bravo. Dash Alpha clicks Refresh ⇒ Open 3, High / Urgent 2, T2 gone. Dash Bravo's dashboard shows T2 and T8 (Open 2, High / Urgent 1). Resolve T4 as Dash Alpha ⇒ after Refresh In Progress 1, T4 gone from the queue. Sign in as **Dash Charlie** (still no tickets) ⇒ four `0` cards and "No Open or In Progress tickets are assigned to you right now."
8. **Top-10 queue limit (more than 10 active tickets):** as SuperAdmin create for **Dash Charlie**, one at a time in this order (so `UpdatedAtUtc` values are distinct and increasing): Q1 Low, Q2 Medium, Q3 High, Q4 Urgent, Q5 Low, Q6 Medium, Q7 High, Q8 Urgent, Q9 Low, Q10 Urgent, Q11 Medium, Q12 High, Q13 Urgent. Then: Start progress on Q9 and Q10; Escalate Q10, then Q5; Start progress → Resolve Q13.
   - Expected counts: Open **10** (Q1–Q8, Q11, Q12), In Progress **2** (Q9, Q10), caption "12 active tickets", Escalated **2** (Q5, Q10), High / Urgent **6** (Q3, Q4, Q7, Q8, Q10, Q12).
   - Expected order of all 12 active tickets: Q10 (escalated Urgent), Q5 (escalated Low), Q4, Q8 (Urgent, older first), Q3, Q7, Q12 (High), Q2, Q6, Q11 (Medium), Q1, Q9 (Low). The endpoint returns **exactly the first 10** — `Q10, Q5, Q4, Q8, Q3, Q7, Q12, Q2, Q6, Q11`. Q1 and Q9 are **not** returned (lowest rank, which proves ordering happens before `TOP`); Q13 (Resolved) never appears.
   - API (Dash Charlie token): `GET /api/v1/agent-dashboard` ⇒ `attentionQueue` length **10** with ids in the order above, `attentionQueueLimit` 10, `summary.active` 12. UI: 10 list items in the same order and "Showing 10 of 12 active tickets." with "View all my tickets".
   - The final `Id` tie-breaker cannot be exercised through the UI (tickets changed one at a time never share an `UpdatedAtUtc`); confirm it via the trailing `[t].[Id]` in the step 6 `ORDER BY`.
9. **Server-side authorization (Postman CLI or curl; tokens via `POST /api/v1/auth/login`):**
   - Dash Alpha token: `GET /api/v1/agent-dashboard` ⇒ 200 with Dash Alpha's post-step-7 numbers (Open 3, In Progress 1, Escalated 2, High / Urgent 2); `GET /api/v1/agent-dashboard?agentId=<dash-bravo-id>` ⇒ 200 with **Dash Alpha's** numbers (unchanged).
   - SuperAdmin token: `GET /api/v1/agent-dashboard` and `?agentId=<dash-alpha-id>` ⇒ **403**. Customer token ⇒ **403**. No token ⇒ **401**. Disable **Dash Bravo** (a DASH-001 test Agent; do not disable Agent One / Agent Two) as SuperAdmin, then call with Dash Bravo's old access token ⇒ **401**. Re-enable Dash Bravo afterwards if wanted; its tickets are untouched either way.
   - `curl -s http://localhost:5080/openapi/v1.json` contains `/api/v1/agent-dashboard` with the "Requires role: Agent" description and string enums for `priority` / `status` / `category`.
   - Record the exact calls and status codes in the PR description.
   - **Data preserved:** `SELECT COUNT(*) FROM Tickets` = `TicketsBefore` + 22 (9 tickets from step 4, 13 from step 8); no pre-existing ticket was edited, reassigned or deleted.
10. **UI states and regression:** stop the API and click Refresh ⇒ cards/queue disappear, "The API is not reachable…" + Retry shown (no zeros); restart and Retry ⇒ data returns. Narrow the browser to ~360 px ⇒ cards stack, queue items wrap, no horizontal page scroll. As SuperAdmin, typing `/agent-dashboard` in the address bar redirects to Home; Home, Tickets, Customers, Agents, Profile, login/logout still work. Ticket list/detail/create and workflow actions (TICK-001/002) unchanged. `grep -rnE "(margin|padding)-(left|right)|text-align: *(left|right)" frontend/src` returns nothing; `grep -rnE "setInterval|interval\(|timer\(" frontend/src/app/features/agent-dashboard` returns nothing.

---

## Done Criteria

- [ ] Existing ticket and customer modules are reused unchanged (only `TicketRepository.Scope` visibility changed); no workflow, assignment, escalation, history or customer API duplicated or extended; no stored data changed.
- [ ] `GET /api/v1/agent-dashboard` exists, is read-only, typed, documented in OpenAPI, restricted by the new `Agent` policy (SuperAdmin / Customer 403), and has no request parameters; the Agent id comes only from the JWT.
- [ ] Counts are computed in SQL over all tickets currently assigned to the caller with the exact Open / In Progress / Escalated / High / Urgent definitions; Active = Open + In Progress; overlap explained in UI and docs.
- [ ] The attention queue holds ≤ 10 Open/InProgress tickets ordered in SQL (escalated, Urgent > High > Medium > Low, oldest `UpdatedAtUtc`, `Id`) before `Take(10)`; labelled as a top-10 display order, not an SLA; with 12 active tickets exactly the expected 10 are returned in order (Verification 8).
- [ ] Queue items show subject (link to ticket detail), category, priority, status, escalation badge, customer name (link to customer detail), email and last-updated; "View all my tickets" and "Create ticket" reuse `/tickets` and `/tickets/new`; no inline editing controls.
- [ ] Reassignment and resolution are reflected after Refresh / revisit for both Agents (Verification 7).
- [ ] Loading, error + Retry, empty, zero and Refresh states behave as specified; failed loads never show zero or stale numbers; no timers or polling.
- [ ] My Dashboard nav entry and `/agent-dashboard` route are Agent-only; SuperAdmin Home and all existing navigation are unchanged; layout works on desktop and ~360 px with logical CSS properties only.
- [ ] No migration, no model changes (`has-pending-model-changes` clean), no new packages, no secrets; counts and queue run in SQL under the Agent scope (2 ticket statements, or 5 with the documented fallback — Verification 6).
- [ ] Manual verification used dedicated DASH-001 Agents with a zero-ticket baseline; no existing development record was deleted, reset or reassigned (Verification 4, 9).
- [ ] `dotnet build`, `dotnet test` (6 passed), `npx ng build`, `npm test -- --watch=false` (6 passed) succeed; exactly one new backend and one new frontend test; manual Verification 4–10 pass and the API checks run are recorded.
- [ ] `docs/agent-dashboard.md` created; `docs/identity-and-access.md`, `docs/api-conventions.md`, `docs/ticket-management.md` and `README.md` updated; deferred tasks/reminders, quick replies, team collaboration and Reports & Management / SLA are listed as future work.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 07.**
