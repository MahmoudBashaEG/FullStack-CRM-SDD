# Story 04 — Ticket Creation & Management

## Prerequisites

- **Story 01 completed:** [../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md) (`ARCH-001`): Clean Architecture solution, source-generated Mediator, FluentValidation, `IUnitOfWork` / `ITransactionalCommand` / `TransactionBehavior`, `GlobalExceptionHandler`, Angular Material shell, `ApiUrl`.
- **Story 02 completed:** [../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md) (`AUTH-001`): Identity users (`Guid` keys) with `IsActive`, roles `SuperAdmin` / `Agent` / `Customer`, `IIdentityService.FindByIdAsync` (returns `Role` + `IsActive`), JWT with `sub` + `role` claims re-validated per request (`SecurityRegistration.OnTokenValidated`), SuperAdmin-only `GET /api/v1/agents`.
- **Story 03 completed:** [../customer-management/03-story-customer-profiles-contact-management.md](../customer-management/03-story-customer-profiles-contact-management.md) (`CUST-001`): `Crm.Domain.Customers.Customer` / `Customers.Id`, `ICustomerRepository`, `PagedResult<T>` / `Paging` (Application), `PagedResponse<T>` (API + Angular `customer-models.ts`), `AuthorizationPolicies.Staff`, `CustomersApi.list` with `search`. This story follows Story 03's file layout, list contract and test style **exactly**.
- All three are committed on `master` (`4992662`, `56e1fe1`, `0200e16`). Current migrations: `20261014234527_InitialCreate`, `20261015011303_AddIdentityAndRefreshSessions`, `20261017183245_AddCustomers`.
- Tooling as verified for Story 03 (2026-10-17): .NET SDK **10.0.400** (`backend/global.json`), `dotnet-ef` **10.0.13**, Angular / Material **22.2**, Vitest. **No new NuGet or npm packages** — `MatSelectModule` and `MatAutocompleteModule` ship with the installed `@angular/material`.
- The intake (`.squad/stories/ticket-management/TICK-001/intake.md`) has no attachments; business context only lives in `.squad/stories/project-architecture/project-init-architecture/attachments/azm_squad_customer_support_crm 1.pdf` (not needed).
- Shared contracts this story changes deliberately (and must document): global JSON enum serialization (`Crm.Api/DependencyInjection.cs` — no existing contract uses an enum, verified by grep), `NAV_ITEMS`, `app.routes.ts`, `docs/api-conventions.md`, `docs/identity-and-access.md`, `docs/customer-management.md`.

---

## Story Goal

Add the first Ticket Management capability — ticket creation, initial assignment and basic management — to the existing CRM:

1. **One `Tickets` table** with an immutable `Ticket.Id` (`Guid`, generated in code), required `CustomerId` (FK → `Customers.Id`, registered **or** contact-only), `Subject`, `Description`, `Category`, `Priority`, system-managed `Status` (always `Open` in this story), `CreatedByUserId` (FK → `AspNetUsers.Id`), nullable `AssignedAgentUserId` (FK → `AspNetUsers.Id`) and UTC `CreatedAtUtc` / `UpdatedAtUtc`.
2. **Creation-time assignment enforced by the backend:** an Agent's ticket is assigned to that Agent (no other assignee ever honoured); a SuperAdmin's ticket is assigned to a selected **active Agent** or left **unassigned**. Creator always comes from the JWT.
3. **Staff API** `/api/v1/tickets` (`Staff` policy): `GET` list/search/filter/paginate, `POST` create, `GET /{id}` details, `PUT /{id}` edit Subject/Description/Category/Priority. SuperAdmin sees every ticket; an Agent sees and edits **only tickets assigned to them** — enforced inside the SQL query.
4. **Angular:** lazy `tickets` feature (list with search + customer/status/category/priority filters + paginator, create with customer autocomplete and role-specific assignment UI, details/edit) for SuperAdmin and Agent; "Tickets" navigation entry.
5. Exactly **one** new backend and **one** new frontend happy-path unit test; new `docs/ticket-management.md` plus updates to existing docs.

**Not in scope** (do **not** build): changing or adding an assignee after creation, Agent self-claim, Agent-to-Agent transfer, status transitions or any status other than `Open`, Resolve/Close/Reopen, escalation, activity history/timeline (all **TICK-002**); Customer Portal ticket submission/tracking; SLA, workload/rule-based routing, notifications; category/priority administration; comments, internal notes, attachments; ticket deletion; bulk import/export; human-readable ticket numbers; integration/E2E/negative-path automated tests.

### Recorded decisions

| Concern | Decision |
|---|---|
| Entity | `Crm.Domain.Tickets.Ticket`, table `Tickets`, `Id` = `Guid.CreateVersion7()` generated in code (`ValueGeneratedNever`). Never deleted. No human-readable number in TICK-001 — the GUID is the ticket identifier shown in the UI. |
| Fixed sets | C# enums in `Crm.Domain.Tickets`: `TicketCategory { General, Technical, Billing, Other }`, `TicketPriority { Low, Medium, High, Urgent }`, `TicketStatus { Open }` (TICK-002 appends further members). Stored as **strings** (`nvarchar(20)`, `HasConversion<string>()`). No category/priority tables or admin screens — a fixed set needs no data administration, and string storage keeps rows readable and TICK-002 additive. |
| JSON | Enums serialize as their names (`"Billing"`, `"Medium"`, `"Open"`) via a global `JsonStringEnumConverter(allowIntegerValues: false)`. Query-string enums bind by name (case-insensitive) through MVC model binding. |
| Defaults | `Status` is set to `Open` by `Ticket.Create` only — no request carries a status. `Priority` omitted on create ⇒ `Medium`. `Category` is required (no default). |
| Field limits | `Subject` 1–200, `Description` 1–4000 (`nvarchar(4000)`), both trimmed; whitespace-only ⇒ 400. Search ≤ 100 chars. New `TicketRules`. |
| Actor | `TicketActor(Guid UserId, bool IsSuperAdmin)` is built in `TicketsController` from `User.GetUserId()` and `User.IsInRole(UserRoles.SuperAdmin)` only (the `Staff` policy guarantees "not SuperAdmin" ⇒ Agent; `OnTokenValidated` already rejects disabled accounts). It is never bound from a request. |
| Initial assignment | Agent actor ⇒ `AssignedAgentUserId = actor.UserId`; a submitted `assignedAgentUserId` equal to the actor is harmless, **any other non-null value ⇒ 400** (`assignedAgentUserId`). SuperAdmin actor ⇒ `null` stays unassigned; a value must resolve via `IIdentityService.FindByIdAsync` to `{ Role: "Agent", IsActive: true }`, else **400** (covers unknown ids, disabled Agents, SuperAdmin and Customer accounts). Unknown customer ⇒ **400** (`customerId`). Errors are FluentValidation `ValidationException`s ⇒ `ValidationProblemDetails` keyed by camelCase field — form-friendly. |
| Record-level access | Every ticket read/write goes through `ITicketRepository` with `Guid? agentScope` (`null` for SuperAdmin, the Agent's id otherwise). The scope is applied as the **first** `Where` in SQL, so no filter, id or body can widen it. A ticket outside the scope ⇒ **404** (existence is not revealed). This includes the read-back after create/update: both handlers call `FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, …)` — **no handler passes `agentScope: null` for an actor**. |
| Edit | `PUT /api/v1/tickets/{id}` changes only `Subject`, `Description`, `Category`, `Priority` (+ `UpdatedAtUtc`). The request has no customer, creator, assignee, status or timestamp fields; `Ticket` has no method that changes them. |
| Read model | `TicketDetails` (flat) = ticket fields + `CustomerName`, `CustomerEmail`, `CreatedByName`, `AssignedAgentName` joined from `Customers` / `AspNetUsers` at query time (no copies stored). Never exposes password hashes, stamps or other Identity fields. |
| Active-Agent dropdown | Only SuperAdmin ever needs it, and the existing `GET /api/v1/agents` is SuperAdmin-only ⇒ the Angular create page reuses `AgentsApi.list()` (filtered to `isActive`) **only when the role is SuperAdmin**. No new endpoint, no policy relaxation. The server re-validates the choice. |
| Customer selection | Reuses `GET /api/v1/customers?search=` (`CustomersApi.list`) through a new `CustomerPicker` autocomplete in the tickets feature, for both the create form and the list's customer filter. |
| List contract | `GET /api/v1/tickets?page=1&pageSize=20&search=&customerId=&status=&category=&priority=` — Story 03's paging rules (one-based, default 20, clamp 100, max page 1 000 000). `search` matches `Subject` (contains) or, when it parses as a GUID, the exact ticket `Id`. Order `CreatedAtUtc DESC, Id DESC`. Response `PagedResponse<TicketResponse>`. |
| Transactions | Create and update each save once ⇒ **not** `ITransactionalCommand` (`docs/persistence-and-transactions.md` line 4). |
| Concurrency | Last write wins (no row version), same as customers; documented. |

---

## Context — Read These Files First

1. `.squad/stories/ticket-management/TICK-001/intake.md` — §2 (fields), §3 (assignment table), §4 (permissions table), §5 (API) and the Acceptance criteria are the contract.
2. `.squad/plans/customer-management/03-story-customer-profiles-contact-management.md` — tasks 1–10 and 11–17: the exact layering, file naming, list contract and test style this story copies.
3. `backend/src/Crm.Domain/Customers/Customer.cs` — lines 11–64: entity style (`private` EF ctor, `private set`, static factory, `UpdateDetails(..., now)`). `Crm.Domain/Identity/UserRoles.cs` lines 4–8 (`SuperAdmin`, `Agent`, `Customer` constants).
4. `backend/src/Crm.Application/Abstractions/Identity/IIdentityService.cs` — line 10 `FindByIdAsync(Guid, …)`; `UserAccount.cs` lines 4–13 (`Role`, `IsActive`). **No change** to this interface.
5. `backend/src/Crm.Application/Agents/AgentLookup.cs` — lines 10–14: the `user is { Role: UserRoles.Agent }` pattern the assignee check extends with `IsActive: true`.
6. `backend/src/Crm.Application/Abstractions/Persistence/ICustomerRepository.cs` — line 18 `FindDetailsByIdAsync(Guid, …)`: used to validate `CustomerId` (no interface change, so `CreateCustomerCommandHandlerTests`' fake keeps compiling).
7. `backend/src/Crm.Application/Customers/` — `CustomerDetails.cs` (lines 6–20, read-model record), `CreateCustomer/CreateCustomerCommand.cs` (lines 12–35, single-save handler with `TimeProvider`), `GetCustomer/GetCustomerQuery.cs` (lines 7–13), `ListCustomers/ListCustomersQuery.cs` (lines 7–18, page-size clamp) + `ListCustomersValidator.cs` (lines 7–14).
8. `backend/src/Crm.Application/Common/Paging/PagedResult.cs` (lines 4–11) and `Common/Validation/CustomerRules.cs` (lines 5–12) — reuse `Paging`; copy the rules-class shape for `TicketRules`.
9. `backend/src/Crm.Infrastructure/Identity/IdentityService.cs` — lines 154–158: how `ValidationException(IEnumerable<ValidationFailure>)` is thrown from non-validator code; lines 22–30: "filter/order BEFORE projecting" rule.
10. `backend/src/Crm.Api/Errors/GlobalExceptionHandler.cs` — lines 22–29: `ValidationException` ⇒ 400 with camelCased keys; lines 30–35 `NotFoundException` ⇒ 404.
11. `backend/src/Crm.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs` — lines 10–28: table/key/FK-to-`ApplicationUser`/descending-index style. `Persistence/Repositories/CustomerRepository.cs` lines 25–54: paging + projection style.
12. `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` — lines 15–16 `DbSet`s; lines 18–24 `OnModelCreating` (keep the order).
13. `backend/src/Crm.Infrastructure/DependencyInjection.cs` — lines 37–39 repository registrations.
14. `backend/src/Crm.Api/Controllers/CustomersController.cs` — lines 15–58: controller shape for a `Staff` resource with a paged list. `Authentication/ClaimsPrincipalExtensions.cs` lines 9–12 `GetUserId()`; `SecurityRegistration.cs` lines 35–36 (`RoleClaimType = CrmClaimTypes.Role`, so `User.IsInRole` reads the `role` claim) and lines 61–65 (`Staff` policy).
15. `backend/src/Crm.Api/DependencyInjection.cs` — line 14 `services.AddControllers();`, lines 25–30 OpenAPI description.
16. `backend/src/Crm.Api/Contracts/Common/PagedResponse.cs` (line 4) and `Contracts/Customers/CustomerResponse.cs` (lines 6–19).
17. `backend/tests/Crm.UnitTests/Customers/CreateCustomerCommandHandlerTests.cs` — whole file: test style with private hand-written fakes (`NotSupportedException` for unused members) and `FakeUnitOfWork.SaveCount`. The test project references only `Crm.Application` (`Crm.UnitTests.csproj` line 22).
18. `frontend/src/app/app.routes.ts` — lines 25–29 `customers` route with `roleGuard('SuperAdmin', 'Agent')`.
19. `frontend/src/app/core/layout/navigation.ts` — lines 13–18 `NAV_ITEMS`.
20. `frontend/src/app/core/auth/auth-service.ts` — line 21 `role` computed signal; `auth-models.ts` line 1 `UserRole`.
21. `frontend/src/app/features/customers/` — `customer-models.ts` (lines 2–26: `Customer`, `PagedResponse<T>`, `CustomerListParams`), `customers-api.ts` (lines 13–38), `customers.routes.ts`, `customer-list-page.ts/.html/.scss` (switchMap + inner `catchError` request stream, toolbar, paginator), `customer-create-page.ts/.html/.scss` (lines 32–77 submit flow, `.form-card`, `.actions`), `customer-detail-page.ts/.html/.scss` (load + `show()` + `save()`, `.readonly-list`, `.hint`), `customers-api.spec.ts` (spec pattern).
22. `frontend/src/app/features/agents/agents-api.ts` (lines 12–14 `list()`) and `agent-models.ts` (lines 2–10 `Agent.isActive`).
23. `frontend/src/app/shared/ui/status-badge/status-badge.ts` — line 3 `StatusTone = 'success' | 'error' | 'pending' | 'neutral'`; `shared/ui/page-header/page-header.ts`; `core/api/describe-http-error.ts`.
24. Docs: `docs/api-conventions.md` (lines 19–21), `docs/identity-and-access.md` (lines 7–16 role matrix, 20–35 routes, line 81 deferred), `docs/customer-management.md` (line 62), `docs/frontend-ui-foundation.md` (line 10), `README.md` (line 3 summary, lines 105–109 conventions list).

---

## Product rules (from story)

| Situation | Before (CUST-001) | New (TICK-001) |
|---|---|---|
| Tickets | Do not exist | Staff create/list/view/edit tickets for any CRM customer (registered or contact-only) |
| Agent creates a ticket | — | Auto-assigned to that Agent; no selector; other assignee ids rejected |
| SuperAdmin creates a ticket | — | Assign to an active Agent or leave unassigned |
| Agent visibility | — | Only tickets assigned to them (list, details, edit) |
| SuperAdmin visibility | — | All tickets, including unassigned ones |
| Assignment / status after creation | — | Read-only (TICK-002) |
| Customer role | — | No access to `/api/v1/tickets*` (403) or the Tickets UI |

---

## Backend Tasks

> Run commands from `backend/`. Every handler is `public sealed` (Mediator's source generator ignores non-public handlers). Validators are `public sealed class <UseCase>Validator : AbstractValidator<…>` next to the command. **No new NuGet packages.**

### 1 — Domain: `Ticket` and its fixed sets

**Create file: `backend/src/Crm.Domain/Tickets/TicketCategory.cs`**

```csharp
namespace Crm.Domain.Tickets;

/// <summary>Fixed set for TICK-001 (no administration). Stored as the member name.</summary>
public enum TicketCategory
{
    General = 0,
    Technical = 1,
    Billing = 2,
    Other = 3,
}
```

**Create file: `backend/src/Crm.Domain/Tickets/TicketPriority.cs`**

```csharp
namespace Crm.Domain.Tickets;

/// <summary>Fixed set, chosen by staff (never calculated). Medium is the default on creation.</summary>
public enum TicketPriority
{
    Low = 0,
    Medium = 1,
    High = 2,
    Urgent = 3,
}
```

**Create file: `backend/src/Crm.Domain/Tickets/TicketStatus.cs`**

```csharp
namespace Crm.Domain.Tickets;

/// <summary>
/// System-managed. TICK-001 only creates Open tickets and has no transitions;
/// TICK-002 appends the other lifecycle states (stored as names, so appending is additive).
/// </summary>
public enum TicketStatus
{
    Open = 0,
}
```

**Create file: `backend/src/Crm.Domain/Tickets/Ticket.cs`**

```csharp
namespace Crm.Domain.Tickets;

/// <summary>
/// Support ticket for a CRM customer (Customers.Id — registered or contact-only, never AspNetUsers.Id).
/// CreatedByUserId / AssignedAgentUserId are Identity user ids set by the server only.
/// TICK-001: customer, creator, assignment and status never change after creation.
/// </summary>
public sealed class Ticket
{
    private Ticket() { } // EF Core

    public Guid Id { get; private set; }
    public Guid CustomerId { get; private set; }
    public string Subject { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketCategory Category { get; private set; }
    public TicketPriority Priority { get; private set; }
    public TicketStatus Status { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid? AssignedAgentUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>New tickets are always Open. The caller has already resolved the initial assignee.</summary>
    public static Ticket Create(
        Guid id,
        Guid customerId,
        string subject,
        string description,
        TicketCategory category,
        TicketPriority priority,
        Guid createdByUserId,
        Guid? assignedAgentUserId,
        DateTimeOffset now) =>
        new()
        {
            Id = id,
            CustomerId = customerId,
            Subject = subject,
            Description = description,
            Category = category,
            Priority = priority,
            Status = TicketStatus.Open,
            CreatedByUserId = createdByUserId,
            AssignedAgentUserId = assignedAgentUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

    /// <summary>TICK-001 basic edit: only these four fields.</summary>
    public void UpdateDetails(string subject, string description, TicketCategory category, TicketPriority priority, DateTimeOffset now)
    {
        Subject = subject;
        Description = description;
        Category = category;
        Priority = priority;
        UpdatedAtUtc = now;
    }
}
```

### 2 — Application: shared types, rules and repository contract

**Create file: `backend/src/Crm.Application/Common/Validation/TicketRules.cs`**

```csharp
namespace Crm.Application.Common.Validation;

public static class TicketRules
{
    public const int SubjectMaxLength = 200;
    public const int DescriptionMaxLength = 4000;
    public const int SearchMaxLength = 100;
}
```

**Create file: `backend/src/Crm.Application/Tickets/TicketActor.cs`**

```csharp
namespace Crm.Application.Tickets;

/// <summary>
/// The authenticated staff member, built by the API from the validated JWT only (sub + role claims) —
/// never from a request body or query. Staff = SuperAdmin or Agent, so !IsSuperAdmin means Agent.
/// </summary>
public sealed record TicketActor(Guid UserId, bool IsSuperAdmin)
{
    /// <summary>null = every ticket (SuperAdmin); otherwise only tickets assigned to this Agent.</summary>
    public Guid? AgentScope => IsSuperAdmin ? null : UserId;
}
```

**Create file: `backend/src/Crm.Application/Tickets/TicketDetails.cs`**

```csharp
using Crm.Domain.Tickets;

namespace Crm.Application.Tickets;

/// <summary>
/// Read model returned by every ticket use case. Names are joined at query time from Customers / AspNetUsers
/// (no copies stored). Exposes no Identity secrets.
/// </summary>
public sealed record TicketDetails(
    Guid Id,
    string Subject,
    string Description,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    Guid CreatedByUserId,
    string CreatedByName,
    Guid? AssignedAgentUserId,
    string? AssignedAgentName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

/// <summary>Optional list filters; Search is already trimmed (null/empty = no filter).</summary>
public sealed record TicketListFilter(
    string? Search,
    Guid? CustomerId,
    TicketStatus? Status,
    TicketCategory? Category,
    TicketPriority? Priority);
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/ITicketRepository.cs`**

```csharp
using Crm.Application.Common.Paging;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Add only stages; the caller saves through IUnitOfWork.
/// agentScope: null = no restriction (SuperAdmin); otherwise only tickets whose AssignedAgentUserId equals it.
/// The scope is applied in SQL before any other filter.
/// </summary>
public interface ITicketRepository
{
    void Add(Ticket ticket);

    /// <summary>Tracked, for updates. Null when missing or outside the scope.</summary>
    Task<Ticket?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default);

    Task<TicketDetails?> FindDetailsByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default);

    /// <summary>Ordered CreatedAtUtc DESC, Id DESC.</summary>
    Task<PagedResult<TicketDetails>> ListAsync(
        TicketListFilter filter, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default);
}
```

### 3 — Application: use cases (`Crm.Application/Tickets/…`)

**Create file: `backend/src/Crm.Application/Tickets/CreateTicket/CreateTicketCommand.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;
using Crm.Domain.Tickets;
using FluentValidation;
using FluentValidation.Results;
using Mediator;

namespace Crm.Application.Tickets.CreateTicket;

/// <summary>
/// Single save — not transactional. Creator and (for Agents) assignee come from Actor, never from the client.
/// Category is nullable only so a missing JSON field fails validation instead of defaulting to General.
/// </summary>
public sealed record CreateTicketCommand(
    TicketActor Actor,
    Guid CustomerId,
    string Subject,
    string Description,
    TicketCategory? Category,
    TicketPriority? Priority,
    Guid? AssignedAgentUserId) : ICommand<TicketDetails>;

public sealed class CreateTicketCommandHandler(
    ITicketRepository tickets,
    ICustomerRepository customers,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateTicketCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(CreateTicketCommand command, CancellationToken cancellationToken)
    {
        // Registered and contact-only customers are both valid owners; no Identity account is required.
        if (await customers.FindDetailsByIdAsync(command.CustomerId, cancellationToken) is null)
        {
            throw Invalid(nameof(command.CustomerId), "The selected customer does not exist.");
        }

        var assignedAgentUserId = await ResolveInitialAssigneeAsync(command, cancellationToken);

        var ticket = Ticket.Create(
            Guid.CreateVersion7(),
            command.CustomerId,
            command.Subject.Trim(),
            command.Description.Trim(),
            command.Category!.Value, // validator: NotNull
            command.Priority ?? TicketPriority.Medium,
            command.Actor.UserId,
            assignedAgentUserId,
            timeProvider.GetUtcNow());

        tickets.Add(ticket);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Read back under the caller's own scope, like every other ticket read. Always visible here:
        // an Agent's new ticket is assigned to that Agent; SuperAdmin's scope is unrestricted.
        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }

    /// <summary>
    /// Agent: always themselves (self-assignment only, never routing); another submitted id is rejected, never honoured.
    /// SuperAdmin: null = unassigned, otherwise an existing, active Agent account (not SuperAdmin, not Customer).
    /// </summary>
    private async Task<Guid?> ResolveInitialAssigneeAsync(CreateTicketCommand command, CancellationToken cancellationToken)
    {
        if (!command.Actor.IsSuperAdmin)
        {
            if (command.AssignedAgentUserId is { } requested && requested != command.Actor.UserId)
            {
                throw Invalid(nameof(command.AssignedAgentUserId), "Agents cannot choose an assignee; tickets you create are assigned to you.");
            }

            return command.Actor.UserId;
        }

        if (command.AssignedAgentUserId is not { } agentId)
        {
            return null;
        }

        var agent = await identity.FindByIdAsync(agentId, cancellationToken);
        if (agent is not { Role: UserRoles.Agent, IsActive: true })
        {
            throw Invalid(nameof(command.AssignedAgentUserId), "Select an active Agent or leave the ticket unassigned.");
        }

        return agentId;
    }

    // 400 ValidationProblemDetails keyed by the camelCased field (GlobalExceptionHandler), like IdentityService.ThrowIfFailed.
    private static ValidationException Invalid(string property, string message) =>
        new([new ValidationFailure(property, message)]);
}
```

**Create file: `backend/src/Crm.Application/Tickets/CreateTicket/CreateTicketValidator.cs`**

```csharp
using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Tickets.CreateTicket;

public sealed class CreateTicketValidator : AbstractValidator<CreateTicketCommand>
{
    public CreateTicketValidator()
    {
        RuleFor(c => c.CustomerId).NotEmpty();
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(TicketRules.SubjectMaxLength);         // NotEmpty also rejects whitespace-only
        RuleFor(c => c.Description).NotEmpty().MaximumLength(TicketRules.DescriptionMaxLength);
        RuleFor(c => c.Category).NotNull().IsInEnum();
        RuleFor(c => c.Priority).IsInEnum();                                                    // null = Medium
        RuleFor(c => c.AssignedAgentUserId).NotEqual(Guid.Empty);
    }
}
```

**Create file: `backend/src/Crm.Application/Tickets/UpdateTicket/UpdateTicketCommand.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Tickets;
using Mediator;

namespace Crm.Application.Tickets.UpdateTicket;

/// <summary>Only Subject/Description/Category/Priority. Single save — not transactional.</summary>
public sealed record UpdateTicketCommand(
    TicketActor Actor,
    Guid Id,
    string Subject,
    string Description,
    TicketCategory? Category,
    TicketPriority? Priority) : ICommand<TicketDetails>;

public sealed class UpdateTicketCommandHandler(
    ITicketRepository tickets,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateTicketCommand, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(UpdateTicketCommand command, CancellationToken cancellationToken)
    {
        // An Agent asking for a ticket not assigned to them gets the same 404 as for a missing id.
        var ticket = await tickets.GetByIdAsync(command.Id, command.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.Id}' was not found.");

        ticket.UpdateDetails(
            command.Subject.Trim(),
            command.Description.Trim(),
            command.Category!.Value, // validator: NotNull
            command.Priority!.Value, // validator: NotNull
            timeProvider.GetUtcNow());

        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Same scope as the load above (the assignment cannot change here), so the ticket is still visible.
        return (await tickets.FindDetailsByIdAsync(ticket.Id, command.Actor.AgentScope, cancellationToken))!;
    }
}
```

**Create file: `backend/src/Crm.Application/Tickets/UpdateTicket/UpdateTicketValidator.cs`** — rules: `Id` `NotEmpty()`; `Subject` `NotEmpty().MaximumLength(TicketRules.SubjectMaxLength)`; `Description` `NotEmpty().MaximumLength(TicketRules.DescriptionMaxLength)`; `Category` `NotNull().IsInEnum()`; `Priority` `NotNull().IsInEnum()`.

**Create file: `backend/src/Crm.Application/Tickets/GetTicket/GetTicketQuery.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Tickets.GetTicket;

public sealed record GetTicketQuery(TicketActor Actor, Guid Id) : IQuery<TicketDetails>;

public sealed class GetTicketQueryHandler(ITicketRepository tickets) : IQueryHandler<GetTicketQuery, TicketDetails>
{
    public async ValueTask<TicketDetails> Handle(GetTicketQuery query, CancellationToken cancellationToken) =>
        await tickets.FindDetailsByIdAsync(query.Id, query.Actor.AgentScope, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{query.Id}' was not found.");
}
```

**Create file: `backend/src/Crm.Application/Tickets/ListTickets/ListTicketsQuery.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Domain.Tickets;
using Mediator;

namespace Crm.Application.Tickets.ListTickets;

public sealed record ListTicketsQuery(
    TicketActor Actor,
    string? Search,
    Guid? CustomerId,
    TicketStatus? Status,
    TicketCategory? Category,
    TicketPriority? Priority,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<TicketDetails>>;

public sealed class ListTicketsQueryHandler(ITicketRepository tickets)
    : IQueryHandler<ListTicketsQuery, PagedResult<TicketDetails>>
{
    public async ValueTask<PagedResult<TicketDetails>> Handle(ListTicketsQuery query, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize); // documented cap: larger values are clamped
        var filter = new TicketListFilter(search, query.CustomerId, query.Status, query.Category, query.Priority);

        // Requested filters only ever narrow the actor's scope; they can never widen it.
        return await tickets.ListAsync(filter, query.Actor.AgentScope, query.Page, pageSize, cancellationToken);
    }
}
```

**Create file: `backend/src/Crm.Application/Tickets/ListTickets/ListTicketsValidator.cs`**

```csharp
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Tickets.ListTickets;

public sealed class ListTicketsValidator : AbstractValidator<ListTicketsQuery>
{
    public ListTicketsValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Search).MaximumLength(TicketRules.SearchMaxLength);
        RuleFor(q => q.Status).IsInEnum();   // null passes
        RuleFor(q => q.Category).IsInEnum();
        RuleFor(q => q.Priority).IsInEnum();
    }
}
```

### 4 — Infrastructure: EF configuration, DbSet, repository

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`**

```csharp
using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Subject).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(4000);

        // Fixed sets stored by name: readable rows, and TICK-002 can append statuses without remapping numbers.
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        // References only (no copies): CRM customer, creating user, optional assigned Agent. Nothing cascades.
        builder.HasOne<Customer>().WithMany().HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.AssignedAgentUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.CreatedAtUtc, t.Id }).IsDescending(true, true);                               // SuperAdmin default list
        builder.HasIndex(t => new { t.AssignedAgentUserId, t.CreatedAtUtc, t.Id }).IsDescending(false, true, true); // Agent-scoped list
        builder.HasIndex(t => new { t.CustomerId, t.CreatedAtUtc, t.Id }).IsDescending(false, true, true);          // customer filter
    }
}
```

**File: `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`** — add `using Crm.Domain.Tickets;` and, after line 16:

```csharp
    public DbSet<Ticket> Tickets => Set<Ticket>();
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class TicketRepository(CrmDbContext dbContext) : ITicketRepository
{
    public void Add(Ticket ticket) => dbContext.Tickets.Add(ticket);

    public Task<Ticket?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) =>
        Scope(dbContext.Tickets, agentScope).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<TicketDetails?> FindDetailsByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) =>
        Project(Scope(dbContext.Tickets.AsNoTracking(), agentScope).Where(t => t.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<TicketDetails>> ListAsync(
        TicketListFilter filter, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Scope(dbContext.Tickets.AsNoTracking(), agentScope);

        if (filter.CustomerId is { } customerId)
        {
            query = query.Where(t => t.CustomerId == customerId);
        }

        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (filter.Category is { } category)
        {
            query = query.Where(t => t.Category == category);
        }

        if (filter.Priority is { } priority)
        {
            query = query.Where(t => t.Priority == priority);
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            // Parameterized; EF Core escapes LIKE wildcards for string.Contains. A full GUID also matches the ticket id.
            var search = filter.Search;
            query = Guid.TryParse(search, out var ticketId)
                ? query.Where(t => t.Id == ticketId || t.Subject.Contains(search))
                : query.Where(t => t.Subject.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await Project(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<TicketDetails>(items, page, pageSize, totalCount);
    }

    // Applied first, in SQL: an Agent can never see or edit a ticket assigned to someone else or unassigned.
    private static IQueryable<Ticket> Scope(IQueryable<Ticket> tickets, Guid? agentScope) =>
        agentScope is { } agentId ? tickets.Where(t => t.AssignedAgentUserId == agentId) : tickets;

    // Display names are joined, not stored. Ordered here (newest first, Id tie-breaker) because EF cannot order
    // by members of the constructed record (same rule as IdentityService.Project). Inner joins never drop rows:
    // CustomerId and CreatedByUserId are required FKs.
    private IQueryable<TicketDetails> Project(IQueryable<Ticket> tickets) =>
        from t in tickets
        join c in dbContext.Customers on t.CustomerId equals c.Id
        join creator in dbContext.Users on t.CreatedByUserId equals creator.Id
        from agent in dbContext.Users.Where(u => u.Id == t.AssignedAgentUserId).DefaultIfEmpty()
        orderby t.CreatedAtUtc descending, t.Id descending
        select new TicketDetails(
            t.Id,
            t.Subject,
            t.Description,
            t.Category,
            t.Priority,
            t.Status,
            t.CustomerId,
            c.FirstName + " " + c.LastName,
            c.Email,
            t.CreatedByUserId,
            creator.FirstName + " " + creator.LastName,
            t.AssignedAgentUserId,
            agent == null ? null : agent.FirstName + " " + agent.LastName,
            t.CreatedAtUtc,
            t.UpdatedAtUtc);
}
```

**File: `backend/src/Crm.Infrastructure/DependencyInjection.cs`** — after line 39 add:

```csharp
        services.AddScoped<ITicketRepository, TicketRepository>();
```

### 5 — API: enum JSON, contracts, controller

**File: `backend/src/Crm.Api/DependencyInjection.cs`**

- Add `using System.Text.Json.Serialization;`.
- Replace line 14 with:

```csharp
        // System.Text.Json web defaults (camelCase properties); enums travel as their names ("Billing"), never as integers.
        services.AddControllers()
            .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
        services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false))); // OpenAPI schema generation
```

- Line 28: after the `"Lists: …"` string append `"Enums: string names, e.g. \"Billing\". "`.

**Create file: `backend/src/Crm.Api/Contracts/Tickets/TicketResponse.cs`**

```csharp
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Tickets;

/// <summary>Customer / creator / assigned Agent are shown by id + display name only (no Identity fields).</summary>
public sealed record TicketResponse(
    Guid Id,
    string Subject,
    string Description,
    TicketCategory Category,
    TicketPriority Priority,
    TicketStatus Status,
    Guid CustomerId,
    string CustomerName,
    string CustomerEmail,
    Guid CreatedByUserId,
    string CreatedByName,
    Guid? AssignedAgentUserId,
    string? AssignedAgentName,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static TicketResponse From(TicketDetails t) =>
        new(t.Id, t.Subject, t.Description, t.Category, t.Priority, t.Status, t.CustomerId, t.CustomerName, t.CustomerEmail,
            t.CreatedByUserId, t.CreatedByName, t.AssignedAgentUserId, t.AssignedAgentName, t.CreatedAtUtc, t.UpdatedAtUtc);
}
```

**Create files in `backend/src/Crm.Api/Contracts/Tickets/`:**

```csharp
// CreateTicketRequest.cs — no status, creator or timestamps. assignedAgentUserId: SuperAdmin only (active Agent or null);
// Agents must omit it (their tickets are assigned to them). priority omitted = Medium.
using Crm.Domain.Tickets;
namespace Crm.Api.Contracts.Tickets;
public sealed record CreateTicketRequest(
    Guid CustomerId, string Subject, string Description, TicketCategory? Category, TicketPriority? Priority, Guid? AssignedAgentUserId);

// UpdateTicketRequest.cs — the only editable fields in TICK-001.
using Crm.Domain.Tickets;
namespace Crm.Api.Contracts.Tickets;
public sealed record UpdateTicketRequest(string Subject, string Description, TicketCategory? Category, TicketPriority? Priority);
```

(Two files, each with its own `using`/`namespace` lines as shown.)

**Create file: `backend/src/Crm.Api/Controllers/TicketsController.cs`**

```csharp
using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.Tickets;
using Crm.Application.Common.Paging;
using Crm.Application.Tickets;
using Crm.Application.Tickets.CreateTicket;
using Crm.Application.Tickets.GetTicket;
using Crm.Application.Tickets.ListTickets;
using Crm.Application.Tickets.UpdateTicket;
using Crm.Domain.Identity;
using Crm.Domain.Tickets;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/tickets")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class TicketsController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: SuperAdmin (all tickets) or Agent (only tickets assigned to them). One-based page (default 1), pageSize (default 20, values above 100 are clamped to 100), optional search on subject or exact ticket id, filters customerId/status/category/priority. Newest first.")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TicketResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] Guid? customerId,
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketCategory? category,
        [FromQuery] TicketPriority? priority,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListTicketsQuery(Actor, search, customerId, status, category, priority, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<TicketResponse>(
            result.Items.Select(TicketResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them; others return 404).")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(TicketResponse.From(await sender.Send(new GetTicketQuery(Actor, id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin or Agent. Status is always Open. Agent: the ticket is assigned to the caller; assignedAgentUserId must be omitted. SuperAdmin: assignedAgentUserId = an active Agent, or omitted/null for unassigned.")]
    [HttpPost]
    [ProducesResponseType<TicketResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<TicketResponse>> Create(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await sender.Send(
            new CreateTicketCommand(
                Actor, request.CustomerId, request.Subject, request.Description, request.Category, request.Priority, request.AssignedAgentUserId),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, TicketResponse.From(ticket));
    }

    [EndpointDescription("Requires role: SuperAdmin (any ticket) or Agent (only tickets assigned to them). Updates subject, description, category and priority only; customer, creator, assignment and status never change here.")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketResponse>> Update(Guid id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await sender.Send(
            new UpdateTicketCommand(Actor, id, request.Subject, request.Description, request.Category, request.Priority),
            cancellationToken);
        return Ok(TicketResponse.From(ticket));
    }

    // From the validated JWT only (sub + role claims; RoleClaimType = "role"). Never from the request.
    private TicketActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
```

### 6 — Migration

From `backend/` (API stopped):

```bash
dotnet ef migrations add AddTickets --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations -- --environment Development
```

Inspect `Persistence/Migrations/*_AddTickets.cs` before applying. `Up` must contain **only** `CreateTable("Tickets", …)` with three foreign keys (`FK_Tickets_Customers_CustomerId`, `FK_Tickets_AspNetUsers_CreatedByUserId`, `FK_Tickets_AspNetUsers_AssignedAgentUserId`, all `onDelete: ReferentialAction.Restrict`), `Category`/`Priority`/`Status` as `nvarchar(20)`, `Description` as `nvarchar(4000)`, and `CreateIndex` calls for `IX_Tickets_CreatedAtUtc_Id` (`descending: new[] { true, true }`), `IX_Tickets_AssignedAgentUserId_CreatedAtUtc_Id`, `IX_Tickets_CustomerId_CreatedAtUtc_Id` and the convention FK index `IX_Tickets_CreatedByUserId`. It must **not** contain `DropTable` (in `Up`), `AlterColumn`, `AddColumn` on existing tables, `InsertData` or `Sql(`. `Down` drops only `Tickets`. The snapshot diff must touch only the `Ticket` entity. Then apply:

```bash
dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development
```

### 7 — Backend unit test (single happy path)

**Create file: `backend/tests/Crm.UnitTests/Tickets/CreateTicketCommandHandlerTests.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Application.Tickets;
using Crm.Application.Tickets.CreateTicket;
using Crm.Domain.Customers;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Tickets;

public sealed class CreateTicketCommandHandlerTests
{
    [Fact]
    public async Task Handle_AgentCreatesTicket_AssignsItToTheAgentAsOpenAndSavesOnce()
    {
        var agentId = Guid.CreateVersion7();
        var customerId = Guid.CreateVersion7();
        var tickets = new FakeTicketRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateTicketCommandHandler(
            tickets, new FakeCustomerRepository(customerId), new UnusedIdentityService(), unitOfWork, TimeProvider.System);

        var result = await handler.Handle(
            new CreateTicketCommand(
                new TicketActor(agentId, IsSuperAdmin: false),
                customerId,
                "  Cannot sign in ",
                " The portal rejects my password. ",
                TicketCategory.Technical,
                Priority: null,
                AssignedAgentUserId: null),
            CancellationToken.None);

        var stored = Assert.Single(tickets.Added);
        Assert.NotEqual(Guid.Empty, stored.Id);
        Assert.Equal(customerId, stored.CustomerId);
        Assert.Equal("Cannot sign in", stored.Subject);
        Assert.Equal("The portal rejects my password.", stored.Description);
        Assert.Equal(TicketCategory.Technical, stored.Category);
        Assert.Equal(TicketPriority.Medium, stored.Priority);   // default
        Assert.Equal(TicketStatus.Open, stored.Status);         // always Open on creation
        Assert.Equal(agentId, stored.CreatedByUserId);          // from the actor, not the client
        Assert.Equal(agentId, stored.AssignedAgentUserId);      // Agent self-assignment
        Assert.Equal(stored.CreatedAtUtc, stored.UpdatedAtUtc);
        Assert.Equal(stored.Id, result.Id);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class FakeTicketRepository : ITicketRepository
    {
        public List<Ticket> Added { get; } = [];
        public void Add(Ticket ticket) => Added.Add(ticket);

        public Task<TicketDetails?> FindDetailsByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default)
        {
            var t = Added.Single(x => x.Id == id);
            return Task.FromResult<TicketDetails?>(new TicketDetails(
                t.Id, t.Subject, t.Description, t.Category, t.Priority, t.Status, t.CustomerId, "Ada Lovelace", "ada@example.com",
                t.CreatedByUserId, "Agent One", t.AssignedAgentUserId, "Agent One", t.CreatedAtUtc, t.UpdatedAtUtc));
        }

        public Task<Ticket?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<TicketDetails>> ListAsync(TicketListFilter filter, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeCustomerRepository(Guid existingId) : ICustomerRepository
    {
        public Task<CustomerDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<CustomerDetails?>(id == existingId
                ? new CustomerDetails(id, "Ada", "Lovelace", "ada@example.com", null, null, false, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow)
                : null);

        public void Add(Customer customer) => throw new NotSupportedException();
        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerDetails?> FindDetailsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<CustomerDetails>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    // An Agent's ticket needs no account lookup: any call here would mean a client-supplied assignee was considered.
    private sealed class UnusedIdentityService : IIdentityService
    {
        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> CreateUserAsync(NewUserAccount account, string password, string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserAccount>> ListByRoleAsync(string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> UpdateProfileAsync(Guid userId, UserProfileUpdate update, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.FromResult(1); }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
```

Existing tests (`Auth/LoginCommandHandlerTests.cs`, `Customers/CreateCustomerCommandHandlerTests.cs`, `Diagnostics/GetSystemStatusQueryHandlerTests.cs`) stay unchanged — no existing interface is modified.

---

## Frontend Tasks

> Run commands from `frontend/`. Follow the `features/customers/` patterns exactly: standalone components, `ChangeDetectionStrategy.OnPush`, `inject()`, signals, `takeUntilDestroyed(this.destroyRef)`, `describeHttpError`, `MatSnackBar`, `crm-` selectors, logical CSS properties only. **No new npm packages.** The tickets feature imports `CustomersApi` / `Customer` / `PagedResponse` from `../customers/` and `AgentsApi` / `Agent` from `../agents/` (reuse, no copies).

### 8 — Tickets feature: models and API

**Create file: `frontend/src/app/features/tickets/ticket-models.ts`**

```ts
import { StatusTone } from '../../shared/ui/status-badge/status-badge';

/** Fixed sets — must match Crm.Domain.Tickets enums (serialized by name). */
export const TICKET_CATEGORIES = ['General', 'Technical', 'Billing', 'Other'] as const;
export const TICKET_PRIORITIES = ['Low', 'Medium', 'High', 'Urgent'] as const;
/** TICK-001 has only Open; TICK-002 adds the other lifecycle states. */
export const TICKET_STATUSES = ['Open'] as const;

export type TicketCategory = (typeof TICKET_CATEGORIES)[number];
export type TicketPriority = (typeof TICKET_PRIORITIES)[number];
export type TicketStatus = (typeof TICKET_STATUSES)[number];

/** Matches Crm.Api TicketResponse. */
export interface Ticket {
  id: string;
  subject: string;
  description: string;
  category: TicketCategory;
  priority: TicketPriority;
  status: TicketStatus;
  customerId: string;
  customerName: string;
  customerEmail: string;
  createdByUserId: string;
  createdByName: string;
  assignedAgentUserId: string | null;
  assignedAgentName: string | null;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface TicketListParams {
  page: number;
  pageSize: number;
  search: string | null;
  customerId: string | null;
  status: TicketStatus | null;
  category: TicketCategory | null;
  priority: TicketPriority | null;
}

/** No status/creator/timestamps. assignedAgentUserId is sent only by SuperAdmin (null = unassigned). */
export interface CreateTicketRequest {
  customerId: string;
  subject: string;
  description: string;
  category: TicketCategory;
  priority: TicketPriority;
  assignedAgentUserId: string | null;
}

/** The only editable fields in TICK-001. */
export interface UpdateTicketRequest {
  subject: string;
  description: string;
  category: TicketCategory;
  priority: TicketPriority;
}

export function priorityTone(priority: TicketPriority): StatusTone {
  switch (priority) {
    case 'Urgent':
      return 'error';
    case 'High':
      return 'pending';
    default:
      return 'neutral';
  }
}
```

**Create file: `frontend/src/app/features/tickets/tickets-api.ts`**

```ts
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import { PagedResponse } from '../customers/customer-models';
import { CreateTicketRequest, Ticket, TicketListParams, UpdateTicketRequest } from './ticket-models';

@Injectable({ providedIn: 'root' })
export class TicketsApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  /** Empty filters are omitted. The server additionally restricts Agents to their own tickets. */
  list({ page, pageSize, search, customerId, status, category, priority }: TicketListParams): Observable<PagedResponse<Ticket>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const term = search?.trim();
    if (term) {
      params = params.set('search', term);
    }
    if (customerId) {
      params = params.set('customerId', customerId);
    }
    if (status) {
      params = params.set('status', status);
    }
    if (category) {
      params = params.set('category', category);
    }
    if (priority) {
      params = params.set('priority', priority);
    }
    return this.http.get<PagedResponse<Ticket>>(this.apiUrl.versioned('tickets'), { params });
  }

  get(id: string): Observable<Ticket> {
    return this.http.get<Ticket>(this.apiUrl.versioned(`tickets/${id}`));
  }

  create(body: CreateTicketRequest): Observable<Ticket> {
    return this.http.post<Ticket>(this.apiUrl.versioned('tickets'), body);
  }

  update(id: string, body: UpdateTicketRequest): Observable<Ticket> {
    return this.http.put<Ticket>(this.apiUrl.versioned(`tickets/${id}`), body);
  }
}
```

### 9 — Routes and navigation

**Create file: `frontend/src/app/features/tickets/tickets.routes.ts`** — `TICKETS_ROUTES`: `''` → `TicketListPage` (title "Tickets | Support CRM"), `'new'` → `TicketCreatePage` ("New ticket | Support CRM"), `':id'` → `TicketDetailPage` ("Ticket | Support CRM"); all `loadComponent`, same shape as `customers.routes.ts` lines 3–19.

**File: `frontend/src/app/app.routes.ts`** — insert before the `customers` route (line 25):

```ts
      {
        path: 'tickets',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/tickets/tickets.routes').then(m => m.TICKETS_ROUTES),
      },
```

**File: `frontend/src/app/core/layout/navigation.ts`** — insert between the Home (line 14) and Customers (line 15) entries:

```ts
  { label: 'Tickets', icon: 'confirmation_number', path: '/tickets', exact: false, roles: ['SuperAdmin', 'Agent'] },
```

### 10 — Customer picker (reuses the customer search API)

**Create file: `frontend/src/app/features/tickets/customer-picker.ts`**

```ts
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { catchError, debounceTime, distinctUntilChanged, filter, map, of, startWith, switchMap, tap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { Customer } from '../customers/customer-models';
import { CustomersApi } from '../customers/customers-api';

/**
 * Autocomplete over GET /api/v1/customers?search= (registered and contact-only customers).
 * Emits the chosen customer, or null as soon as the user types again (the selection is cleared).
 */
@Component({
  selector: 'crm-customer-picker',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatFormFieldModule, MatInputModule, MatAutocompleteModule, MatIconModule],
  template: `
    <mat-form-field appearance="outline" class="picker">
      <mat-label>{{ label() }}</mat-label>
      <input
        matInput
        type="search"
        [formControl]="query"
        [matAutocomplete]="auto"
        placeholder="Search by name, email or phone" />
      <mat-icon matSuffix>person_search</mat-icon>
      <mat-autocomplete #auto="matAutocomplete" [displayWith]="display" (optionSelected)="selected.emit($event.option.value)">
        @for (c of options(); track c.id) {
          <mat-option [value]="c">{{ c.firstName }} {{ c.lastName }} · {{ c.email }}</mat-option>
        } @empty {
          <mat-option disabled>No customers match.</mat-option>
        }
      </mat-autocomplete>
      @if (lookupError(); as message) {
        <mat-hint class="lookup-error">{{ message }}</mat-hint>
      } @else if (hint()) {
        <mat-hint>{{ hint() }}</mat-hint>
      }
    </mat-form-field>
  `,
  styles: `
    .picker {
      inline-size: 100%;
    }
    .lookup-error {
      color: var(--mat-sys-error);
    }
  `,
})
export class CustomerPicker {
  private readonly api = inject(CustomersApi);

  readonly label = input('Customer');
  readonly hint = input<string>();
  readonly selected = output<Customer | null>();

  protected readonly query = new FormControl<string | Customer>('', { nonNullable: true });
  protected readonly options = signal<Customer[]>([]);
  protected readonly lookupError = signal<string | null>(null);

  constructor() {
    // Typing after a selection clears it, so the parent never keeps a customer the field no longer shows.
    this.query.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(value => {
        if (typeof value === 'string') {
          this.selected.emit(null);
        }
      });

    this.query.valueChanges
      .pipe(
        startWith(''),
        filter((value): value is string => typeof value === 'string'),
        map(value => value.trim()),
        debounceTime(300),
        distinctUntilChanged(),
        tap(() => this.lookupError.set(null)),
        // Newest 10 matches; a newer term cancels the older request; errors stay per request.
        switchMap(search =>
          this.api.list({ page: 1, pageSize: 10, search }).pipe(
            map(page => page.items),
            catchError((error: HttpErrorResponse) => {
              this.lookupError.set(describeHttpError(error, 'Could not search customers.'));
              return of([] as Customer[]);
            }),
          ),
        ),
        takeUntilDestroyed(),
      )
      .subscribe(items => this.options.set(items));
  }

  protected readonly display = (value: Customer | string | null): string =>
    typeof value === 'string' ? value : value ? `${value.firstName} ${value.lastName}` : '';
}
```

### 11 — Ticket list page

**Create file: `frontend/src/app/features/tickets/ticket-list-page.ts`** — copy `customer-list-page.ts` and adapt:

- Imports additionally: `computed`, `MatSelectModule` (`@angular/material/select`), `DatePipe`, `AuthService` (`../../core/auth/auth-service`), `CustomerPicker`, `Customer` (`../customers/customer-models`), `Ticket`, `TicketListParams`, `TicketCategory`, `TicketPriority`, `TicketStatus`, `TICKET_CATEGORIES`, `TICKET_PRIORITIES`, `TICKET_STATUSES`, `priorityTone` (`./ticket-models`), `TicketsApi`. Selector `crm-ticket-list-page`; `templateUrl`/`styleUrl` `./ticket-list-page.html/.scss`.
- Fields:

```ts
  private readonly api = inject(TicketsApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<TicketListParams>();
  private readonly auth = inject(AuthService);

  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin'); // wording only; the API scopes the data
  protected readonly columns = ['subject', 'customer', 'category', 'priority', 'status', 'assignee', 'created', 'actions'];
  protected readonly pageSizeOptions = [10, 20, 50, 100];
  protected readonly categories = TICKET_CATEGORIES;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly statuses = TICKET_STATUSES;
  protected readonly priorityTone = priorityTone;
  protected readonly filters = inject(NonNullableFormBuilder).group({
    search: [''],
    status: [null as TicketStatus | null],
    category: [null as TicketCategory | null],
    priority: [null as TicketPriority | null],
  });
  protected readonly customerId = signal<string | null>(null);
  protected readonly tickets = signal<Ticket[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1);
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
```

- Constructor: identical request stream to `CustomerListPage` (switchMap with inner `catchError` setting `error` to `describeHttpError(error, 'Could not load tickets.')`, returning `EMPTY`), then:

```ts
    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load(1));

    this.load(1);
```

- Methods:

```ts
  protected onCustomer(customer: Customer | null): void {
    const id = customer?.id ?? null;
    if (id !== this.customerId()) {
      this.customerId.set(id);
      this.load(1);
    }
  }

  protected hasFilters(): boolean {
    const f = this.filters.getRawValue();
    return !!(f.search.trim() || f.status || f.category || f.priority || this.customerId());
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.load(event.pageIndex + 1);
  }

  private load(page: number): void {
    const f = this.filters.getRawValue();
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({
      page,
      pageSize: this.pageSize(),
      search: f.search,
      customerId: this.customerId(),
      status: f.status,
      category: f.category,
      priority: f.priority,
    });
  }
```

**Create file: `frontend/src/app/features/tickets/ticket-list-page.html`**

- `<crm-page-header title="Tickets" [subtitle]="isSuperAdmin() ? 'All support tickets.' : 'Tickets assigned to you.'" />`
- `.toolbar` (`[formGroup]="filters"`): `mat-form-field.search` "Search" with `<input matInput type="search" formControlName="search" placeholder="Subject or ticket ID" />` + `<mat-icon matSuffix>search</mat-icon>`; `<crm-customer-picker class="customer-filter" label="Customer" (selected)="onCustomer($event)" />`; three `mat-form-field.filter` with `mat-select` — "Status" (`formControlName="status"`), "Category", "Priority" — each starting with `<mat-option [value]="null">Any</mat-option>` then `@for (v of statuses|categories|priorities; track v) { <mat-option [value]="v">{{ v }}</mat-option> }`; and `<a mat-flat-button routerLink="new"><mat-icon>add</mat-icon> New ticket</a>`.
- `@if (loading())` → `<mat-progress-bar mode="indeterminate" />`; `@if (error(); as message)` → `<p class="error" role="alert">{{ message }}</p>`.
- When not loading and no error: if `tickets().length === 0` → `hasFilters() ? 'No tickets match your filters.' : (isSuperAdmin() ? 'No tickets yet. Create the first ticket.' : 'No tickets are assigned to you yet.')`; otherwise `.table-container` with `mat-table [dataSource]="tickets()"` and columns:
  - `subject` — `<a [routerLink]="t.id">{{ t.subject }}</a>`
  - `customer` — `{{ t.customerName }}`
  - `category` — `{{ t.category }}`
  - `priority` — `<crm-status-badge [label]="t.priority" [tone]="priorityTone(t.priority)" />`
  - `status` — `<crm-status-badge [label]="t.status" tone="pending" />`
  - `assignee` — `{{ t.assignedAgentName ?? 'Unassigned' }}`
  - `created` — `{{ t.createdAtUtc | date: 'medium' }}`
  - `actions` — `<a mat-button [routerLink]="t.id">View</a>`
- Paginator exactly as `customer-list-page.html` lines 72–78, `aria-label="Ticket pages"`.

**Create file: `frontend/src/app/features/tickets/ticket-list-page.scss`** — copy `customer-list-page.scss`; change `.search` to `flex: 1 1 220px; max-inline-size: 320px;`; add `.customer-filter { flex: 1 1 260px; max-inline-size: 360px; }` and `.filter { flex: 0 1 160px; }`. Logical properties only.

### 12 — Ticket create page

**Create files: `frontend/src/app/features/tickets/ticket-create-page.ts` / `.html` / `.scss`** — copy `customer-create-page.*` and adapt:

- Additional imports: `computed`, `MatSelectModule`, `AuthService`, `AgentsApi` + `Agent` (`../agents/…`), `CustomerPicker`, `Customer`, ticket models, `TicketsApi`. Selector `crm-ticket-create-page`.
- Fields:

```ts
  private readonly api = inject(TicketsApi);
  private readonly agentsApi = inject(AgentsApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(NonNullableFormBuilder);

  protected readonly isSuperAdmin = computed(() => this.auth.role() === 'SuperAdmin');
  protected readonly categories = TICKET_CATEGORIES;
  protected readonly priorities = TICKET_PRIORITIES;
  protected readonly form = this.fb.group({
    customerId: this.fb.control<string | null>(null, Validators.required),
    subject: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(200)]],
    description: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(4000)]],
    category: this.fb.control<TicketCategory | null>(null, Validators.required),
    priority: this.fb.control<TicketPriority>('Medium', Validators.required),
    assignedAgentUserId: this.fb.control<string | null>(null), // SuperAdmin only; null = Unassigned
  });
  protected readonly activeAgents = signal<Agent[]>([]);
  protected readonly agentsError = signal<string | null>(null);
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
```

- Constructor: only when `this.isSuperAdmin()`: `this.agentsApi.list().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: agents => this.activeAgents.set(agents.filter(a => a.isActive)), error: (error: HttpErrorResponse) => this.agentsError.set(describeHttpError(error, 'Could not load agents.') + ' You can still create the ticket unassigned.') })`. **Agents never call `/api/v1/agents`** (SuperAdmin-only endpoint).
- `onCustomer(customer: Customer | null)`: `this.form.controls.customerId.setValue(customer?.id ?? null); this.form.controls.customerId.markAsTouched();`.
- `submit()`: same guard as the customer page; body:

```ts
      {
        customerId: value.customerId!,
        subject: value.subject.trim(),
        description: value.description.trim(),
        category: value.category!,
        priority: value.priority,
        // Agents never send an assignee: the server assigns the ticket to them.
        assignedAgentUserId: this.isSuperAdmin() ? value.assignedAgentUserId : null,
      }
```

  → snackbar "Ticket created." (3000 ms) → `router.navigate(['/tickets', ticket.id])`; on error `describeHttpError(error, 'Could not create the ticket.')`.
- HTML (`<crm-page-header title="New ticket" subtitle="Log a support request for an existing customer." />`, `mat-card.form-card`, `form.form-grid`):
  1. `<crm-customer-picker label="Customer" hint="Registered customers and contacts. Create missing contacts under Customers first." (selected)="onCustomer($event)" />`, followed by `@if (form.controls.customerId.invalid && form.controls.customerId.touched) { <p class="form-error">Select a customer from the list</p> }`.
  2. "Subject" input (`mat-error`: "Subject is required (max 200 characters)"); "Description" `<textarea matInput formControlName="description" rows="6">` (`mat-error`: "Description is required (max 4000 characters)").
  3. "Category" `mat-select` (`@for` over `categories`; `mat-error` "Choose a category"); "Priority" `mat-select` (`@for` over `priorities`).
  4. Read-only status line: `<p class="readonly-field"><span class="label">Status</span> <crm-status-badge label="Open" tone="pending" /> <span class="hint">New tickets always start as Open.</span></p>` (import `StatusBadge`).
  5. Assignment: `@if (isSuperAdmin()) {` `mat-form-field` "Assigned agent (optional)" with `<mat-select formControlName="assignedAgentUserId"><mat-option [value]="null">Unassigned</mat-option>@for (a of activeAgents(); track a.id) {<mat-option [value]="a.id">{{ a.firstName }} {{ a.lastName }}</mat-option>}</mat-select>` and `<mat-hint>Only active agents are listed.</mat-hint>`; `@if (agentsError(); as message) { <p class="form-error" role="alert">{{ message }}</p> }` `} @else {` `<p class="assignment-note"><mat-icon aria-hidden="true">assignment_ind</mat-icon> Assigned to you automatically.</p>` `}`.
  6. Form `error()` paragraph, `.actions` with `<a mat-button routerLink="/tickets">Cancel</a>` and `<button mat-flat-button type="submit" [disabled]="submitting()">Create ticket</button>`.
- SCSS: copy `customer-create-page.scss`; rename `.no-login-note` to `.assignment-note` (same rules); add `.readonly-field { display: flex; flex-wrap: wrap; align-items: center; gap: var(--crm-space-2); margin: 0; }`, `.readonly-field .label { color: var(--mat-sys-on-surface-variant); }`, `.hint { color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }`.

### 13 — Ticket details / edit page

**Create files: `frontend/src/app/features/tickets/ticket-detail-page.ts` / `.html` / `.scss`** — copy `customer-detail-page.*` and adapt:

- Imports additionally `MatSelectModule`, ticket models, `TicketsApi`. Selector `crm-ticket-detail-page`. `ticket = signal<Ticket | null>(null)`; load `api.get(id)`; `loadError` fallback `'Could not load the ticket.'` (an Agent opening a ticket not assigned to them gets the API's 404 message "Ticket '…' was not found.").
- Form: `subject`, `description` (same validators as the create page), `category: this.fb.control<TicketCategory>('General', Validators.required)`, `priority: this.fb.control<TicketPriority>('Medium', Validators.required)`; `show(ticket)` → `form.reset({ subject, description, category, priority })`.
- `save()` → `api.update(id, { subject: trim, description: trim, category, priority })` → `show(updated)` + snackbar "Ticket updated." (3000 ms); on error `describeHttpError(error, 'Could not update the ticket.')`.
- HTML: `<crm-page-header title="Ticket" subtitle="Ticket details." />`, back link `<a mat-button routerLink="/tickets">Back to tickets</a>`, then `.detail-stack` with:
  - Card "Summary" — `dl.readonly-list`: "Ticket ID" (`{{ current.id }}`), "Status" (`<crm-status-badge [label]="current.status" tone="pending" />`), "Priority" (badge with `priorityTone`), "Category", "Customer" (`<a [routerLink]="['/customers', current.customerId]">{{ current.customerName }}</a> · {{ current.customerEmail }}`), "Created by" (`{{ current.createdByName }}`), "Assigned agent" (`{{ current.assignedAgentName ?? 'Unassigned' }}`), "Created" / "Last updated" (`| date: 'medium'`). Below: `<p class="hint">Assignment and status are read-only here.</p>`.
  - Card "Edit details" — Subject input, Description textarea (`rows="6"`), Category and Priority `mat-select`s, `saveError()` paragraph, Save button. **No** assignee, status, customer or transfer controls.
- SCSS: copy `customer-detail-page.scss` unchanged (`.detail-stack`, `.readonly-list`, `.hint`, `.form-grid`, `.form-error`).

### 14 — Frontend unit test (single happy path)

**Create file: `frontend/src/app/features/tickets/tickets-api.spec.ts`**

```ts
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from '../../core/api/api-config';
import { PagedResponse } from '../customers/customer-models';
import { Ticket } from './ticket-models';
import { TicketsApi } from './tickets-api';

describe('TicketsApi', () => {
  it('requests a filtered one-based page, omits empty filters and returns the typed page', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } },
      ],
    });
    const api = TestBed.inject(TicketsApi);
    const http = TestBed.inject(HttpTestingController);
    let result: PagedResponse<Ticket> | undefined;

    api
      .list({ page: 1, pageSize: 20, search: '  login ', customerId: 'c1', status: 'Open', category: null, priority: 'High' })
      .subscribe(page => (result = page));
    const request = http.expectOne(r => r.url === '/api/v1/tickets');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('search')).toBe('login');
    expect(request.request.params.get('customerId')).toBe('c1');
    expect(request.request.params.get('status')).toBe('Open');
    expect(request.request.params.get('priority')).toBe('High');
    expect(request.request.params.has('category')).toBe(false);
    request.flush({
      items: [
        {
          id: 't1', subject: 'Cannot sign in', description: 'Password rejected.', category: 'Technical', priority: 'High',
          status: 'Open', customerId: 'c1', customerName: 'Ada Lovelace', customerEmail: 'ada@example.com',
          createdByUserId: 'a1', createdByName: 'Agent One', assignedAgentUserId: 'a1', assignedAgentName: 'Agent One',
          createdAtUtc: '2030-01-01T00:00:00Z', updatedAtUtc: '2030-01-01T00:00:00Z',
        },
      ],
      page: 1,
      pageSize: 20,
      totalCount: 1,
    });

    expect(result?.totalCount).toBe(1);
    expect(result?.items[0].assignedAgentName).toBe('Agent One');
    http.verify();
  });
});
```

---

## Documentation Tasks

### 15 — `docs/ticket-management.md` (create)

Concise bullets/tables:

1. **Ticket entity** — `Tickets` table fields (Recorded decisions "Entity" + §2 of the intake): `Id` (GUID v7, immutable, the ticket identifier — no sequential number yet), `CustomerId` → `Customers.Id` (registered or contact-only; never `AspNetUsers.Id`; not editable), `Subject` (≤ 200), `Description` (≤ 4000), `Category`, `Priority`, `Status`, `CreatedByUserId` and nullable `AssignedAgentUserId` → `AspNetUsers.Id`, `CreatedAtUtc` / `UpdatedAtUtc` (UTC). All FKs `Restrict`; names are joined at read time, not copied. No deletion.
2. **Fixed sets** — categories `General`, `Technical`, `Billing`, `Other`; priorities `Low`, `Medium` (default), `High`, `Urgent` (chosen by staff, never calculated); status `Open` only (set by the server on creation; no transitions in TICK-001). Stored and serialized by name. Changing a set = code change + doc update (no admin UI).
3. **Creation-time assignment** — the intake §3 table (Agent ⇒ self, other ids rejected with 400; SuperAdmin ⇒ active Agent or unassigned; disabled/non-Agent/SuperAdmin ids rejected with 400; Customer cannot create). Creator always from the token. Self-assignment is not routing.
4. **Visibility and editing** — the intake §4 table; enforcement: `Staff` policy + `TicketActor` from the JWT + `agentScope` applied first in every SQL query; out-of-scope ids ⇒ 404; unassigned tickets are visible to SuperAdmin only; Angular guards/menus are UX only. Last write wins on concurrent edits.
5. **API** — routes table: `GET /api/v1/tickets` (Staff, 200 paged), `POST /api/v1/tickets` (Staff, 201 + Location; 400 unknown customer / invalid assignee), `GET /api/v1/tickets/{id}` (Staff, 200; 404 unknown or not visible), `PUT /api/v1/tickets/{id}` (Staff, 200; 404 unknown or not visible). List contract: Story 03 paging + `search` (subject contains, or exact ticket GUID), `customerId`, `status`, `category`, `priority`; order `CreatedAtUtc DESC, Id DESC`. Request/response field list. Active-Agent dropdown reuses SuperAdmin-only `GET /api/v1/agents`; customer selection reuses `GET /api/v1/customers?search=`.
6. **Future stories** — TICK-002: post-creation assignment/reassignment (SuperAdmin only), assigning unassigned tickets, status workflow (Resolve/Close/Reopen…), manual escalation, activity history. Customer Portal: customer self-service submission/tracking. SLA & Automation: SLA targets, rule/workload-based assignment, notifications. Never: Agent self-claim or Agent-to-Agent transfer.

### 16 — Update existing docs

- **`docs/api-conventions.md`** — after line 20 add: "- **Enums:** serialized as their PascalCase member names (e.g. `\"Billing\"`) by a global `JsonStringEnumConverter` with integers rejected; query-string enum filters bind by name. First used by `/api/v1/tickets` ([ticket-management.md](ticket-management.md))." In line 19 append: "Record-level rules (e.g. Agents see only tickets assigned to them) are applied inside the query from the token-derived actor; records outside the caller's scope return 404."
- **`docs/identity-and-access.md`** — role matrix: after line 15 add rows "Create tickets (Agent: assigned to self; SuperAdmin: active Agent or unassigned) | ✔ | ✔ | —" and "List / view / edit tickets | ✔ all | ✔ assigned to them | —". Routes table: after line 34 add `GET / POST /api/v1/tickets` (SuperAdmin, Agent | 200 / 201) and `GET / PUT /api/v1/tickets/{id}` (SuperAdmin, Agent — Agent only own assigned | 200). Line 81: remove ", agent ticket permissions" (now implemented; see [ticket-management.md](ticket-management.md)).
- **`docs/customer-management.md`** — line 62: replace "and Ticket Management reference `Customers.Id`." with "reference `Customers.Id`; tickets already do (`Tickets.CustomerId`, see [ticket-management.md](ticket-management.md))."
- **`docs/frontend-ui-foundation.md`** — after line 10 add: "- **Pickers:** select existing records through server-side search (`CustomerPicker` in `features/tickets/` wraps `MatAutocomplete` over `GET /api/v1/customers`); fixed sets use `MatSelect`."
- **`README.md`** — line 3: "contains the architectural foundation (`ARCH-001`), identity & access (`AUTH-001`), customer profiles & contacts (`CUST-001`) and ticket creation & management (`TICK-001`)". Conventions list: after line 109 add `- [Ticket management](docs/ticket-management.md)`.

---

## Edge Cases & Failure Modes

- **Agent submits `assignedAgentUserId` of another user** (direct API call) — `CreateTicketCommandHandler.ResolveInitialAssigneeAsync` ⇒ 400 `assignedAgentUserId` "Agents cannot choose an assignee…"; nothing saved. Own id or omitted ⇒ self-assigned. Angular never sends it for Agents (task 12).
- **SuperAdmin assigns a disabled Agent, a SuperAdmin (including themselves), a Customer account or an unknown id** — `identity.FindByIdAsync` result is not `{ Role: Agent, IsActive: true }` ⇒ 400 `assignedAgentUserId` "Select an active Agent or leave the ticket unassigned.". `Guid.Empty` ⇒ 400 from `CreateTicketValidator`.
- **Agent disabled after the dropdown loaded** — the server re-checks at submit ⇒ 400; SuperAdmin picks another Agent or Unassigned.
- **Agent disabled after a ticket was assigned** — the ticket keeps `AssignedAgentUserId` (FK `Restrict`; accounts are never deleted); the disabled Agent cannot sign in (`OnTokenValidated`); SuperAdmin still sees the name. Reassignment is TICK-002.
- **Unknown or `Guid.Empty` `customerId`** — empty ⇒ validator 400; unknown ⇒ handler 400 `customerId` "The selected customer does not exist.". The FK would also reject it at save (500) — the handler check runs first.
- **Contact-only customer** — valid owner (no `UserId` check anywhere); same flow as registered.
- **Missing `category` in JSON** — `TicketCategory?` stays null ⇒ 400 (does not silently become `General`). Missing `priority` on create ⇒ `Medium`; on update ⇒ 400.
- **Enum values as integers or unknown names in the body** (`"category": 2`, `"category": "Sales"`) — `JsonStringEnumConverter(allowIntegerValues: false)` fails binding ⇒ 400 ValidationProblemDetails from `[ApiController]`. Query `?status=Closed` ⇒ binding error ⇒ 400; `?priority=7` binds as an undefined value ⇒ `ListTicketsValidator` `IsInEnum` ⇒ 400.
- **Client sends `status`, `createdByUserId`, `createdAtUtc`, `id` or `customerId` (on update)** — not on any request record; System.Text.Json ignores unknown members; the server sets/keeps them. `Ticket.UpdateDetails` cannot change them.
- **Agent requests another ticket by id** (`GET`/`PUT /tickets/{id}`) — `Scope` adds `AssignedAgentUserId == actor` in SQL ⇒ null ⇒ 404 "Ticket '…' was not found." (same as a missing id). Unassigned tickets behave the same for Agents.
- **Agent tries to widen the list** — `customerId`/`status`/`category`/`priority`/`search` are AND-ed after `Scope`; there is no assignee parameter; `?assignedAgentUserId=` is ignored by model binding.
- **Customer role or anonymous** — `Staff` policy ⇒ 403 / 401 ProblemDetails via `UseStatusCodePages`; Angular `roleGuard` redirects to Home and `NAV_ITEMS` hides "Tickets".
- **Whitespace-only subject/description** — FluentValidation `NotEmpty` ⇒ 400; Angular `Validators.pattern(/\S/)` blocks submit first. Leading/trailing whitespace trimmed before storage. Length is validated on the raw value (≤ 200 / ≤ 4000), like customers.
- **Search** — whitespace-only ⇒ no filter; > 100 chars ⇒ 400; `%`, `_`, `[` matched literally (EF parameterizes `Contains`); a full GUID matches the ticket id **or** subjects containing it; partial ids do not match ids (documented). Case-insensitive under the default `SQL_Latin1_General_CP1_CI_AS` collation.
- **List parameters** — Story 03 rules: `page < 1` or `> 1 000 000` / `pageSize < 1` ⇒ 400; `pageSize > 100` ⇒ clamped, response shows 100; page past the end ⇒ 200 with empty `items` and real `totalCount`.
- **Ordering / paging stability** — `orderby t.CreatedAtUtc descending, t.Id descending` inside `TicketRepository.Project` before `Skip/Take`; equal timestamps break ties by `Id`; inserts while paging shift later pages (offset paging, documented).
- **EF translation** — the left join `dbContext.Users.Where(u => u.Id == t.AssignedAgentUserId).DefaultIfEmpty()` and `agent == null ? null : …` translate to `LEFT JOIN … CASE`. If EF reports an untranslatable `orderby` after the joins, move the `orderby` to the `Ticket` source (`tickets.OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)`) before the joins and keep `Skip/Take` after the projection; verify the order in Verification 6.
- **OpenAPI shows enums as integers** — `ConfigureHttpJsonOptions` is not used by the OpenAPI generator in this setup. Fix by adding `[JsonConverter(typeof(JsonStringEnumConverter<TicketCategory>))]` (and the Priority/Status equivalents) on the three enums in `Crm.Domain/Tickets/` (System.Text.Json is part of the BCL — no package) and note it in `docs/ticket-management.md`; runtime JSON is already correct through `AddJsonOptions`.
- **Customer picker** — typing after a selection emits `null` (parent clears `customerId`, create form becomes invalid, list reloads without the filter); API failure shows "Could not search customers." under the field and later keystrokes retry; at most 10 suggestions (newest first) — refine the search to find older customers.
- **Agents dropdown load failure (SuperAdmin)** — `agentsError` shown; "Unassigned" remains selectable, so creation still works.
- **Concurrent edits** — last write wins; `UpdatedAtUtc` reflects the latest save.
- **Read-back after save** — `CreateTicketCommandHandler` and `UpdateTicketCommandHandler` re-read with `command.Actor.AgentScope`. It cannot return null in TICK-001 (an Agent's new ticket is self-assigned; an update never changes the assignment), so the `!` matches `IdentityService.CreateUserAsync` line 79. If TICK-002 ever lets an update move a ticket out of the caller's scope, that handler must return a different result instead of relying on `!`.
- **`run-dev.ps1` and migrations** — the script (lines 22–37) runs `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` from `backend/` **before** it starts the API (line 48) and the Angular dev server (line 54), throws "Migration failed (exit code …)" on a non-zero exit (lines 30–32), and skips the step entirely with `-SkipMigrations`. The API itself never applies migrations (`IdentityBootstrapper` / `CustomerProfileInitializer` only log a warning when migrations are pending). Consequences: (a) with `-SkipMigrations` and `AddTickets` unapplied, ticket endpoints return 500 "Invalid object name 'Tickets'"; (b) an already running API from an earlier session can lock build outputs and make the script's `dotnet ef` build fail — close the "CRM API" window first; (c) the script launches windows with `pwsh` (PowerShell 7), so it needs PowerShell 7 installed.
- **Migration content** — if `migrations add` also alters `Customers`, Identity or `RefreshSessions`, the model changed unintentionally: `dotnet ef migrations remove …`, fix, regenerate. If EF additionally emits `IX_Tickets_CustomerId` / `IX_Tickets_AssignedAgentUserId` single-column indexes, that is acceptable (redundant but harmless).

---

## Test Plan

1. **Unit (backend) — add:** `backend/tests/Crm.UnitTests/Tickets/CreateTicketCommandHandlerTests.cs` → `Handle_AgentCreatesTicket_AssignsItToTheAgentAsOpenAndSavesOnce` (task 7): Agent actor, no submitted assignee, default priority; asserts trimmed fields, `Medium`, `Open`, creator = assignee = actor, one save, and (via `UnusedIdentityService`) no account lookup. Hand-written fakes as in `Customers/CreateCustomerCommandHandlerTests.cs`.
2. **Unit (frontend) — add:** `frontend/src/app/features/tickets/tickets-api.spec.ts` → "requests a filtered one-based page, omits empty filters and returns the typed page" (task 14), pattern of `features/customers/customers-api.spec.ts`.
3. **Keep unchanged:** `Auth/LoginCommandHandlerTests.cs`, `Customers/CreateCustomerCommandHandlerTests.cs`, `Diagnostics/GetSystemStatusQueryHandlerTests.cs`, `core/auth/auth-service.spec.ts`, `core/api/api-url.spec.ts`, `features/customers/customers-api.spec.ts`.
4. **Manual smoke (happy paths + the critical role/assignment, migration and enum checks the intake requires):** Verification Steps 3–12.

No other tests, frameworks, mocking libraries, integration/SQL/API/E2E suites or automated negative-path checks.

---

## Migration / Rollback

- **Forward:** `AddTickets` only creates `Tickets`, its three `Restrict` FKs and indexes. No existing table, column or row is altered; no data is inserted.
- **Rollback (local):** stop the API; from `backend/`: `dotnet ef database update AddCustomers --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` (drops `Tickets` — all tickets are lost; accounts, customers and sessions are untouched), then `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` and revert the code.
- **Half-applied state:** SQL Server applies the migration DDL in one transaction; on failure nothing is created — fix and re-run `database update`. Running the new API against a database without `Tickets` fails only on ticket endpoints (500 "Invalid object name 'Tickets'") — apply the migration (`.\run-dev.ps1` without `-SkipMigrations` runs `dotnet ef database update` before starting the API; the API never migrates on its own) and retry.

---

## Verification Steps

1. **Backend builds and tests:** in `backend/` run `dotnet build` (0 errors) then `dotnet test` (**4** tests passed: the three existing + `CreateTicketCommandHandlerTests`).
2. **Layer direction and untouched contracts:** `grep -rn "Microsoft.AspNetCore\|EntityFrameworkCore" backend/src/Crm.Application backend/src/Crm.Domain --include=*.cs` returns nothing; `git diff --stat -- backend/src/Crm.Application/Abstractions/Identity backend/src/Crm.Application/Abstractions/Persistence/ICustomerRepository.cs backend/src/Crm.Infrastructure/Persistence/Migrations/20261014234527_InitialCreate.cs backend/src/Crm.Infrastructure/Persistence/Migrations/20261015011303_AddIdentityAndRefreshSessions.cs backend/src/Crm.Infrastructure/Persistence/Migrations/20261017183245_AddCustomers.cs` shows no changes.
3. **`AddTickets` migration is applied safely** (API stopped; run from `backend/`; database name from your `ConnectionStrings:CrmDatabase` secret — `CrmDev` per README line 42):
   1. **Baseline:** `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT COUNT(*) FROM AspNetUsers; SELECT COUNT(*) FROM AspNetUserRoles; SELECT COUNT(*) FROM Customers; SELECT COUNT(*) FROM RefreshSessions; SELECT MigrationId FROM __EFMigrationsHistory;"` — record the four counts; history ends with `20261017183245_AddCustomers`.
   2. **Static review:** `grep -n "InsertData\|Sql(\|AlterColumn\|AddColumn\|RenameColumn\|DropColumn\|DropIndex" src/Crm.Infrastructure/Persistence/Migrations/*_AddTickets.cs` returns nothing, and `grep -n "DropTable" src/Crm.Infrastructure/Persistence/Migrations/*_AddTickets.cs` matches only `DropTable(name: "Tickets")` inside `Down`.
   3. **SQL preview before applying:** `dotnet ef migrations list --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` shows `…_AddTickets (Pending)` as the only pending migration; `dotnet ef migrations script AddCustomers AddTickets --project src/Crm.Infrastructure --startup-project src/Crm.Api --output $env:TEMP/AddTickets.sql -- --environment Development` produces only `CREATE TABLE [Tickets]` (with the three `FOREIGN KEY … REFERENCES [Customers]/[AspNetUsers]` constraints, no `ON DELETE CASCADE`), `CREATE INDEX … ON [Tickets]` statements and one `INSERT INTO [__EFMigrationsHistory]` — no `ALTER`, `DROP` or `UPDATE` against any existing table.
   4. **Apply** with `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` (or via `.\run-dev.ps1`, step 4). Never use `EnsureCreated`, `database drop` or a recreated database.
   5. **After:** `migrations list` shows no pending migrations; `__EFMigrationsHistory` lists four rows ending with `…_AddTickets`; the four counts from 3.1 are unchanged; `sqlcmd … -Q "SELECT name FROM sys.indexes WHERE object_id = OBJECT_ID('Tickets'); SELECT name FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID('Tickets');"` lists the PK + task 6 indexes and the three FKs. Existing accounts still sign in (step 11).
4. **Setup and `run-dev.ps1` behaviour:** first confirm the script still matches this plan — `run-dev.ps1` lines 22–37 run `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` from `backend/` unless `-SkipMigrations` is passed and throw on a non-zero exit; lines 45–55 then start `dotnet run --project src/Crm.Api --launch-profile http` and `npm start` in separate `pwsh` windows. If the script differs, update this step, Edge Cases and Migration / Rollback before continuing. Close any running "CRM API" window, then run `.\run-dev.ps1` (without `-SkipMigrations`): the console prints "Applying pending migrations..." and EF reports the database is already up to date (step 3 applied `AddTickets`) — `__EFMigrationsHistory` still has exactly four rows; the API log shows no "pending migration(s)" warning from the identity bootstrap or customer profile initializer. (Alternative without the script: `dotnet run --project src/Crm.Api --launch-profile http` + `npm start`.) As `admin@admin.com` create (Agents page) two Agents, "Agent One" and "Agent Two", and a third "Agent Off" which you then **disable**. Ensure at least one registered and one contact-only customer exist (Customers page).
5. **Frontend builds and tests:** in `frontend/` run `npx ng build` (no errors; budget warnings acceptable) and `npm test -- --watch=false` (**4** tests passed).
6. **SuperAdmin flows (browser, http://localhost:4200):** "Tickets" appears in navigation between Home and Customers. Tickets → "New ticket": status shows "Open" read-only; the "Assigned agent (optional)" dropdown lists "Unassigned", Agent One and Agent Two — **not** Agent Off or the admin. Create ticket A for the contact-only customer, category Billing, priority left at Medium, assigned to Agent One → detail page shows Status Open, Priority Medium, Created by = admin, Assigned agent = Agent One. Create ticket B for the registered customer, Technical/Urgent, **Unassigned** → detail shows "Unassigned". List: B above A (newest first); Urgent badge is red; search "billing" subject text / paste A's full ID finds A; Customer filter (pick the contact-only customer) shows only A; Priority = Urgent shows only B; Status = Open shows both; page size 10 + paging work with > 10 tickets. Edit B's subject, description, category and priority → "Ticket updated.", Last updated changes, Created/customer/creator/assignee unchanged. `sqlcmd … -Q "SELECT Subject, Status, Category, Priority, CreatedByUserId, AssignedAgentUserId FROM Tickets"` shows string enums and `NULL` assignee for B.
7. **Agent flows:** sign in as Agent One → Tickets list shows **only** ticket A (B is unassigned, invisible). "New ticket" shows "Assigned to you automatically." and no agent dropdown; create ticket C → detail shows Created by = Assigned agent = Agent One. Edit C's priority → saved. Sign in as Agent Two → list is empty ("No tickets are assigned to you yet."); open `http://localhost:4200/tickets/<A-id>` → "Ticket '<A-id>' was not found.".
8. **Critical server-side role/assignment checks (manual, once):** get tokens with `curl -s -X POST http://localhost:5080/api/v1/auth/login -H "Content-Type: application/json" -d "{\"email\":\"<agent-two-email>\",\"password\":\"<password>\"}"` (copy `accessToken`). With Agent Two's token: `GET /api/v1/tickets/<A-id>` ⇒ 404; `POST /api/v1/tickets` with `"assignedAgentUserId": "<agent-one-id>"` ⇒ 400 (`errors.assignedAgentUserId`) and no new row. With the admin token: `POST` with Agent Off's id ⇒ 400; with the admin's own user id ⇒ 400. With a Customer account's token: `GET /api/v1/tickets` ⇒ 403. (Postman CLI may be used instead of curl.)
9. **Customer UI:** sign in as a registered Customer → no "Tickets" entry; `/tickets` redirects to Home.
10. **OpenAPI:** `curl -s http://localhost:5080/openapi/v1.json` lists `/api/v1/tickets` and `/api/v1/tickets/{id}` with "Requires role: …" descriptions, and `TicketCategory`/`TicketPriority`/`TicketStatus` as string enums (if integers, apply the Edge Case fix).
11. **Regression:** AUTH-001 — login/logout, reload keeps the session, SuperAdmin Agents list/create/disable, Profile save; CUST-001 — customers list/search/create/edit and Customer self-service profile unchanged; `grep -rnE "(margin|padding)-(left|right)|text-align: *(left|right)" frontend/src` returns nothing; `grep -rnE "localStorage|sessionStorage" frontend/src/app --include=*.ts` matches only `auth-service.spec.ts`.
12. **Enums stored and serialized as strings** (after steps 6–7 created tickets A, B, C):
    - **Storage:** `sqlcmd … -Q "SELECT DISTINCT Category, Priority, Status FROM Tickets; SELECT c.name, t.name AS type, c.max_length FROM sys.columns c JOIN sys.types t ON t.user_type_id = c.user_type_id WHERE c.object_id = OBJECT_ID('Tickets') AND c.name IN ('Category','Priority','Status');"` → values are names (`Billing`, `Technical`, `Medium`, `Urgent`, `Open`, …), never digits; all three columns are `nvarchar` with `max_length` 40 (= 20 characters).
    - **Response JSON:** `curl -s http://localhost:5080/api/v1/tickets/<A-id> -H "Authorization: Bearer <admin-token>"` contains `"category":"Billing"`, `"priority":"Medium"`, `"status":"Open"` (strings, not numbers); `GET /api/v1/tickets?status=Open&priority=urgent` (name, case-insensitive) returns ticket B.
    - **Request JSON:** `POST /api/v1/tickets` with `"category":"Other"` succeeds (201, response `"category":"Other"`); the same body with `"category":2` ⇒ 400 (integers rejected by `JsonStringEnumConverter(allowIntegerValues: false)`).
    - **Contract:** step 10 shows the three enums as `type: string` with their member names in `/openapi/v1.json`.
    - **Regression:** `GET /api/v1/customers` and `GET /api/v1/agents` responses keep the same shape as before (they contain no enums).

---

## Done Criteria

- [ ] Changes extend ARCH-001, AUTH-001 and CUST-001 in place: no new solution, login, Agent model or role; `IIdentityService` and `ICustomerRepository` unchanged; Domain/Application reference no ASP.NET Core/EF packages (Verification 2).
- [ ] `Ticket` has an immutable GUID `Id`, required `CustomerId` (FK → `Customers.Id`), `Subject`, `Description`, `Category`, `Priority`, server-set `Status = Open`, `CreatedByUserId`, nullable `AssignedAgentUserId` (FKs → `AspNetUsers.Id`) and UTC timestamps (tasks 1, 4).
- [ ] Registered and contact-only customers are both valid ticket owners; no Customer login is required (Verification 6).
- [ ] An Agent's ticket is created by and assigned to that Agent from the JWT; any other submitted assignee is rejected (Verification 7–8, unit test).
- [ ] A SuperAdmin's ticket is assigned to a selected active Agent or left unassigned; disabled, SuperAdmin, Customer and unknown ids are rejected (Verification 6, 8).
- [ ] New tickets are always `Open`; categories `General/Technical/Billing/Other` and priorities `Low/Medium/High/Urgent` are fixed and documented; `Medium` is the default.
- [ ] SuperAdmin lists/searches/filters/paginates/views/edits all tickets; an Agent only tickets assigned to them (404 otherwise); Customer gets 403 on `/api/v1/tickets*` (Verification 6–9).
- [ ] `GET /api/v1/tickets` uses the Story 03 paging contract, searches subject / exact ticket id, filters by customer/status/category/priority, and orders `CreatedAtUtc DESC, Id DESC`.
- [ ] Details/edit responses expose only `TicketResponse` fields; `PUT` changes only subject/description/category/priority — never customer, creator, assignment or status.
- [ ] Authorization is enforced by the `Staff` policy, the token-derived `TicketActor` and the SQL `agentScope`, never by Angular guards or client-supplied ids.
- [ ] Angular Tickets navigation, list/search/filters/paginator, create (customer autocomplete, read-only Open status, "Assigned to you automatically" vs active-Agent dropdown with Unassigned) and details/edit work for SuperAdmin and Agent, with loading/empty/error states.
- [ ] Migration `AddTickets` is additive only (static review + SQL script preview), applied without recreating the database, leaves no pending migrations and keeps existing row counts unchanged (Verification 3).
- [ ] `run-dev.ps1` was checked against this plan (migrates before starting the API unless `-SkipMigrations`; fails fast on migration errors; the API never migrates itself) and the plan's statements about it match the script (Verification 4).
- [ ] `Category`, `Priority` and `Status` are stored as `nvarchar(20)` names and serialized as strings in requests, responses, query strings and OpenAPI; integer enum values are rejected (Verification 10, 12).
- [ ] Create and update read the result back with `command.Actor.AgentScope`; no ticket handler reads with `agentScope: null` on behalf of an actor (`grep -rn "agentScope: null" backend/src` returns nothing).
- [ ] AUTH-001 and CUST-001 behaviour is unchanged (Verification 11).
- [ ] `dotnet build`, `dotnet test` (4 passed), `npx ng build`, `npm test -- --watch=false` (4 passed) succeed; manual Verification 3–12 pass; no extra test types added.
- [ ] `docs/ticket-management.md` created; `docs/api-conventions.md`, `docs/identity-and-access.md`, `docs/customer-management.md`, `docs/frontend-ui-foundation.md` and `README.md` updated, distinguishing TICK-001 initial assignment from TICK-002 reassignment/workflow/escalation/history, Customer Portal and SLA & Automation.
- [ ] No reassignment, self-claim, transfer, status transitions, escalation, history, comments, attachments, deletion, routing or other out-of-scope features were added.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 05.**
