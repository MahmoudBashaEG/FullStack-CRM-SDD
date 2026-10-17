# Story 03 — Customer Profiles & Contact Management

## Prerequisites

- **Story 01 completed:** [../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md) (`ARCH-001`): Clean Architecture solution, source-generated Mediator, FluentValidation, `IUnitOfWork` / `ITransactionalCommand` / `TransactionBehavior`, `GlobalExceptionHandler`, Angular Material shell, `ApiUrl`.
- **Story 02 completed:** [../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md) (`AUTH-001`): ASP.NET Core Identity on `CrmDbContext` (`Guid` keys), `UserRoles`, `IIdentityService`, `POST /api/v1/auth/register`, `GET/PUT /api/v1/me`, fallback auth policy, `AuthService` / `authGuard` / `roleGuard`, `NavItem.roles`, Profile page. This story **extends** that code in place; it creates no new solution, login system, signup route or role.
- Both stories are committed on `master` (`4992662`, `56e1fe1`). Current migrations: `20261014234527_InitialCreate` (empty) and `20261015011303_AddIdentityAndRefreshSessions`.
- Verified on the planning machine (2026-10-17): .NET SDK **10.0.400**, `dotnet-ef` **10.0.13**, EF Core / Identity packages **10.0.13**, Angular / Material **22.2**, Vitest **5**.
- The intake's `attachments/` folder (`.squad/stories/customer-management/CUST-001/attachments/`) is **empty**; the referenced `azm_squad_customer_support_crm 1(2).pdf` is not on disk. The same CRM feature list exists at `.squad/stories/project-architecture/project-init-architecture/attachments/azm_squad_customer_support_crm 1.pdf` (business context only).
- Shared contracts this story changes deliberately (and must document): `docs/api-conventions.md` (first list/pagination contract), `docs/identity-and-access.md` (role matrix, routes, deferred list), `RegisterCustomerCommand` (now also creates a CRM profile) and `UpdateMyProfileCommand` (now transactional, mirrors to the CRM profile).

---

## Story Goal

Add the first Customer Management capability — CRM customer profiles and contacts — to the existing CRM:

1. **One `Customers` table** with a stable `Customer.Id` (`Guid`), required first/last name and contact email, optional phone and company, UTC created/updated timestamps and an **optional, unique** `UserId` link to `AspNetUsers.Id`.
2. **Two kinds of customer in one list:** *registered* (linked to an Identity user in the `Customer` role) and *contact-only* (created by SuperAdmin/Agent, no Identity user, no password, no login).
3. **Signup linkage:** the existing `POST /api/v1/auth/register` creates the Identity user **and** its CRM profile in one transaction. **Backfill:** an idempotent startup initializer creates exactly one profile for every pre-existing `Customer`-role account (never for Agents or SuperAdmin).
4. **Staff API** `/api/v1/customers` (SuperAdmin + Agent): list/search/paginate, view, create contact-only, update. **Customer self-service** `/api/v1/me/customer-profile` (Customer role, own record from the JWT subject only): view and update.
5. **Angular:** lazy `customers` feature (list with search + paginator + registered/contact-only badge, create "No login account created", detail/edit) for SuperAdmin/Agent; the existing Profile page gains the Company field for Customers.
6. Exactly **one** new backend and **one** new frontend happy-path unit test; new `docs/customer-management.md` plus updates to existing docs.

**Not in scope** (do **not** build): customer notes, attachments, interaction/ticket history, tickets, invitations, email verification, claiming/merging/linking contacts to accounts (by email or otherwise), customer deletion, bulk import/export, advanced sort/filter, per-agent assignment, departments/branches, impersonation, Customer Portal, localization/RTL, integration/E2E/negative-path tests.

### Recorded decisions

| Concern | Decision |
|---|---|
| Entity | `Crm.Domain.Customers.Customer`, table `Customers`, `Id` = `Guid.CreateVersion7()` generated in code (`ValueGeneratedNever`). Never reused, never deleted in this story. |
| Account link | Nullable `UserId` FK → `AspNetUsers.Id`, `OnDelete(Restrict)`, **unique filtered index** `[UserId] IS NOT NULL` ⇒ at most one profile per account, any number of contact-only rows. `UserId` is set only by the server (signup / backfill); no request DTO carries it. |
| Field ownership (linked customer) | **Identity (`AspNetUsers`) is authoritative** for login email, first name, last name and phone. The CRM row keeps a **mirror** of those four (needed for list/search) plus **`Company`, which only the CRM owns**. Every write path that changes a linked customer's name/phone updates **both** rows in **one transaction**: `PUT /api/v1/me`, `PUT /api/v1/me/customer-profile`, `PUT /api/v1/customers/{id}`. Login email is never changed by any path (AUTH-001 rule); a request with a different email for a registered customer ⇒ **422**. |
| Field ownership (contact-only) | The CRM row owns everything. Staff may change the contact email; it never touches `AspNetUsers`. |
| Email matching | **No** automatic link/merge between a contact-only row and an account with the same email (AUTH-001 does not verify email ownership). Duplicate contact emails are allowed and documented. |
| Signup | `RegisterCustomerCommand` (already `ITransactionalCommand`) adds the `Customer` row after `CreateUserAsync`; `EfUnitOfWork` saves it in the same transaction ⇒ no account without a profile, no profile without an account. |
| Backfill | Startup `CustomerProfileInitializer` (Infrastructure), called from `Program.cs` right after `BootstrapIdentityAsync`, before `app.Run()`. Skips with a warning when migrations are pending; inserts profiles only for `Customer`-role users with none; idempotent; no data in the migration. `CreatedAtUtc`/`UpdatedAtUtc` = the account's `CreatedAtUtc`. |
| Authorization | New policies `AuthorizationPolicies.Staff` (roles SuperAdmin, Agent) and `AuthorizationPolicies.Customer` (role Customer). Self-service handlers receive the user id from `User.GetUserId()` only. |
| List contract | `GET /api/v1/customers?page=1&pageSize=20&search=…` — one-based `page` (default 1, min 1, max 1 000 000), `pageSize` (default 20, min 1, values > 100 are **clamped** to 100), optional `search` (trimmed, ≤ 100 chars, matches first name, last name, "first last", email, phone; case-insensitive under the default SQL Server collation). Order: `CreatedAtUtc DESC, Id DESC`. Response `PagedResponse<CustomerResponse>` `{ items, page, pageSize, totalCount }` — only list endpoints use it; other endpoints keep direct DTOs. |
| Concurrency | Last write wins (no row version) — acceptable for the training CRM; documented. |
| Field limits | First/last name 100, email 256, phone 32 (reuse `AccountRules`); company 200 (new `CustomerRules`). |

---

## Context — Read These Files First

1. `.squad/stories/customer-management/CUST-001/intake.md` — Description §1–7, the role table in §4 and the Acceptance criteria are the contract.
2. `backend/src/Crm.Application/Auth/RegisterCustomer/RegisterCustomerCommand.cs` — lines 11–12: `RegisterCustomerCommand : ICommand<UserAccount>, ITransactionalCommand`; lines 17–30: email pre-check, then `identity.CreateUserAsync(..., UserRoles.Customer, ...)`. The CRM profile is added here.
3. `backend/src/Crm.Application/Profile/UpdateMyProfile/UpdateMyProfileCommand.cs` — lines 7–9 (comment "Single save, so deliberately not transactional." — changes in this story), lines 14–18 handler.
4. `backend/src/Crm.Application/Abstractions/Identity/IIdentityService.cs` — line 18 `UpdateProfileAsync(Guid, UserProfileUpdate, …)`; `UserAccount.cs` lines 4–17 (`UserAccount`, `NewUserAccount`, `UserProfileUpdate`). **No change** to this interface (keeps `LoginCommandHandlerTests` fakes compiling).
5. `backend/src/Crm.Infrastructure/Identity/IdentityService.cs` — lines 62–80 `CreateUserAsync` (UserManager saves immediately on the scoped `CrmDbContext`), lines 90–101 `UpdateProfileAsync` (loads the tracked `ApplicationUser`, `userManager.UpdateAsync` ⇒ `SaveChanges` on the shared context — this also flushes any tracked `Customer` change, harmless inside the transaction).
6. `backend/src/Crm.Infrastructure/Persistence/EfUnitOfWork.cs` — lines 11–44: `ChangeTracker.Clear()` → begin → handler → final `SaveChangesAsync` → commit. Transactional handlers **do not** call `SaveChangesAsync` themselves (same as `ChangeMyPasswordCommandHandler`); single-save handlers call it once.
7. `backend/src/Crm.Application/Common/Behaviors/TransactionBehavior.cs` — lines 21–37: only `ITransactionalCommand` messages are wrapped.
8. `backend/src/Crm.Application/Common/Validation/AccountRules.cs` — lines 9–28: `NameMaxLength`, `EmailMaxLength`, `PhoneMaxLength`, `PersonName()`, `AccountEmail()`, `OptionalPhone()`, `NormalizeOptional()`. Reuse them.
9. `backend/src/Crm.Application/Agents/AgentLookup.cs` (lines 7–15), `Agents/GetAgent/GetAgentQuery.cs`, `Agents/UpdateAgent/UpdateAgentCommand.cs` + `UpdateAgentValidator.cs` — query/command + handler in one file, `public sealed` handlers with primary-constructor DI, validators next to the command.
10. `backend/src/Crm.Application/Abstractions/Persistence/IRefreshSessionRepository.cs` and `backend/src/Crm.Infrastructure/Persistence/Repositories/RefreshSessionRepository.cs` — narrow per-aggregate repository pattern (`Add` stages; reads `AsNoTracking`).
11. `backend/src/Crm.Domain/Identity/RefreshSession.cs` — lines 7–32: entity style (`private` ctor for EF, `private set`, static `Create`). `Crm.Domain/Common/DomainException.cs` ⇒ 422.
12. `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` — line 14 `DbSet<RefreshSession>`; lines 16–22 `base.OnModelCreating` then `ApplyConfigurationsFromAssembly` (keep that order).
13. `backend/src/Crm.Infrastructure/Persistence/Configurations/RefreshSessionConfiguration.cs` — lines 10–21: table/key/index/FK-to-`ApplicationUser` style. `ApplicationUserConfiguration.cs` lines 11–13 (name 100, phone 32).
14. `backend/src/Crm.Infrastructure/Identity/IdentityBootstrapper.cs` — lines 21–30 pending-migration guard and lines 11–19 constructor shape; `IdentityBootstrapExtensions.cs` lines 5–13 (scope + `RunAsync`). The customer initializer copies both.
15. `backend/src/Crm.Infrastructure/DependencyInjection.cs` — lines 36–37 (repository registrations), lines 56–58 (`IIdentityService`, `IdentityBootstrapper`).
16. `backend/src/Crm.Api/Program.cs` — line 17 `await app.Services.BootstrapIdentityAsync();` (insert the initializer call after it).
17. `backend/src/Crm.Api/Authentication/AuthorizationPolicies.cs` (lines 3–6) and `SecurityRegistration.cs` lines 61–63 (`AddAuthorizationBuilder` … `AddPolicy(AuthorizationPolicies.SuperAdmin, …)`); `ClaimsPrincipalExtensions.cs` lines 9–12 `GetUserId()`.
18. `backend/src/Crm.Api/Controllers/AgentsController.cs` (lines 15–51) and `MeController.cs` (lines 12–30) — controller shape: `[ApiController]`, `[Route(ApiRoutes.V1 + "/…")]`, `[EndpointDescription("Requires role: …")]`, `CreatedAtAction`, `Response.From(...)` mapping.
19. `backend/src/Crm.Api/Contracts/Agents/AgentResponse.cs` (lines 5–10) and `Contracts/Profile/UpdateMyProfileRequest.cs` — DTO record style.
20. `backend/src/Crm.Api/DependencyInjection.cs` — lines 25–29: OpenAPI document description (append the list convention).
21. `backend/tests/Crm.UnitTests/Auth/LoginCommandHandlerTests.cs` — lines 9–75: test style with private hand-written fakes (`NotSupportedException` for unused members), `FakeUnitOfWork.SaveCount`. Test project references only `Crm.Application` (`Crm.UnitTests.csproj` line 22) — Domain comes transitively.
22. `frontend/src/app/app.routes.ts` — lines 25–30: `agents` child route with `roleGuard('SuperAdmin')`; comment line 30 marks where features are added.
23. `frontend/src/app/core/layout/navigation.ts` — lines 13–17 `NAV_ITEMS`.
24. `frontend/src/app/core/auth/auth-guards.ts` — lines 14–19 `roleGuard(...roles)`; `auth-service.ts` lines 19–21 (`currentUser`, `role`) and 63–65 `updateCurrentUser`.
25. `frontend/src/app/features/agents/` — `agents-api.ts` (lines 7–35), `agent-models.ts`, `agents.routes.ts`, `agent-list-page.ts/.html/.scss`, `agent-create-page.ts/.html`, `agent-detail-page.ts/.html` (lines 1–40). Copy these patterns (OnPush, `inject()`, signals, `takeUntilDestroyed`, `describeHttpError`, `MatSnackBar`, `crm-` selectors, logical CSS).
26. `frontend/src/app/features/profile/profile-api.ts` (lines 7–34), `profile-page.ts` (lines 43–118), `profile-page.html` (lines 8–56), `profile-page.scss`.
27. `frontend/src/app/shared/ui/status-badge/status-badge.ts` (tones `success | error | pending | neutral`), `shared/ui/page-header/page-header.ts`.
28. `frontend/src/app/core/auth/auth-service.spec.ts` — lines 8–33: Vitest + `TestBed` + `HttpTestingController` spec pattern.
29. `docs/api-conventions.md` (line 20 "Pagination … deferred"), `docs/identity-and-access.md` (lines 7–14 role matrix, 18–32 routes, 74 deferred), `docs/persistence-and-transactions.md` (line 7 identity writes), `docs/frontend-ui-foundation.md`, `README.md` (line 3 summary, lines 49–54 conventions list).

---

## Product rules (from story)

| Situation | Current (AUTH-001) | New (CUST-001) |
|---|---|---|
| Public signup | Creates Identity user + `Customer` role | Same route and request; additionally creates one linked CRM profile in the same transaction |
| Existing `Customer` accounts | No CRM data | Exactly one linked profile created at startup (idempotent) |
| SuperAdmin / Agent accounts | — | Never get a CRM profile |
| `PUT /api/v1/me` | Updates Identity name/phone, single save | Same contract; for a Customer with a profile also mirrors name/phone to the CRM row (transactional) |
| Staff managing customers | Not possible | List/search/view/create contact-only/update via `/api/v1/customers` |
| Company field | Deferred | CRM-only field on the customer profile |

---

## Backend Tasks

> Run commands from `backend/`. Every handler is `public sealed` (Mediator's source generator ignores non-public handlers). Validators are `public sealed class <UseCase>Validator : AbstractValidator<…>` next to the command. **No new NuGet packages.**

### 1 — Domain: `Customer` entity

**Create file: `backend/src/Crm.Domain/Customers/Customer.cs`**

```csharp
using Crm.Domain.Common;

namespace Crm.Domain.Customers;

/// <summary>
/// CRM customer profile/contact. Separate from the Identity account: UserId is null for
/// contact-only customers and set (by the server only) for registered customers.
/// For a registered customer, Identity owns email/name/phone and this row mirrors them;
/// Company is CRM-only.
/// </summary>
public sealed class Customer
{
    private Customer() { } // EF Core

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string? Company { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsRegistered => UserId is not null;

    /// <summary>Staff-created record. No Identity user, password or login.</summary>
    public static Customer CreateContact(
        Guid id, string firstName, string lastName, string email, string? phoneNumber, string? company, DateTimeOffset now) =>
        new()
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            Company = company,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

    /// <summary>Profile of a registered Customer account (signup or backfill).</summary>
    public static Customer CreateForAccount(
        Guid id, Guid userId, string firstName, string lastName, string email, string? phoneNumber, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = id,
            UserId = userId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };

    public void UpdateDetails(string firstName, string lastName, string? phoneNumber, string? company, DateTimeOffset now)
    {
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Company = company;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Contact-only: replaces the business email (never touches any login).
    /// Registered: the email is the login email (AUTH-001: not editable); an equal value
    /// (ignoring case) is accepted as "unchanged", anything else is a rule violation.
    /// </summary>
    public void ChangeContactEmail(string email, DateTimeOffset now)
    {
        if (IsRegistered)
        {
            if (!string.Equals(email, Email, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException("A registered customer's email is their login email and cannot be changed.");
            }

            return;
        }

        if (string.Equals(email, Email, StringComparison.Ordinal))
        {
            return;
        }

        Email = email;
        UpdatedAtUtc = now;
    }
}
```

### 2 — Application: shared types, rules and repository contract

**Create file: `backend/src/Crm.Application/Common/Paging/PagedResult.cs`**

```csharp
namespace Crm.Application.Common.Paging;

/// <summary>One page of a server-side paginated list. Page is one-based.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 1_000_000; // keeps (page - 1) * pageSize far from int overflow
}
```

**Create file: `backend/src/Crm.Application/Common/Validation/CustomerRules.cs`**

```csharp
using FluentValidation;

namespace Crm.Application.Common.Validation;

public static class CustomerRules
{
    public const int CompanyMaxLength = 200;
    public const int SearchMaxLength = 100;

    public static IRuleBuilderOptions<T, string?> OptionalCompany<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(CompanyMaxLength);
}
```

**Create file: `backend/src/Crm.Application/Customers/CustomerDetails.cs`**

```csharp
using Crm.Domain.Customers;

namespace Crm.Application.Customers;

/// <summary>Read model returned by every customer use case. Never exposes UserId.</summary>
public sealed record CustomerDetails(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Company,
    bool IsRegistered,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static CustomerDetails From(Customer customer) =>
        new(customer.Id, customer.FirstName, customer.LastName, customer.Email, customer.PhoneNumber,
            customer.Company, customer.IsRegistered, customer.CreatedAtUtc, customer.UpdatedAtUtc);
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/ICustomerRepository.cs`**

```csharp
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>Add only stages; the caller saves through IUnitOfWork (or the transaction does).</summary>
public interface ICustomerRepository
{
    void Add(Customer customer);

    /// <summary>Tracked, for updates.</summary>
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked, for updates. The profile linked to an Identity account, if any.</summary>
    Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<CustomerDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerDetails?> FindDetailsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>search: already trimmed, null/empty = no filter. Ordered CreatedAtUtc DESC, Id DESC.</summary>
    Task<PagedResult<CustomerDetails>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Customers/CustomerAccountSync.cs`** — the single place that writes the CRM mirror back to Identity.

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Domain.Customers;

namespace Crm.Application.Customers;

internal static class CustomerAccountSync
{
    /// <summary>
    /// Identity is authoritative for a linked customer's name/phone: push the (already updated)
    /// CRM values to the account. Call only inside an ITransactionalCommand so both rows commit together.
    /// No-op for contact-only customers.
    /// </summary>
    public static async Task SyncLinkedAccountAsync(this IIdentityService identity, Customer customer, CancellationToken cancellationToken)
    {
        if (customer.UserId is { } userId)
        {
            await identity.UpdateProfileAsync(
                userId, new UserProfileUpdate(customer.FirstName, customer.LastName, customer.PhoneNumber), cancellationToken);
        }
    }
}
```

### 3 — Application: staff use cases (`Crm.Application/Customers/…`)

**Create file: `backend/src/Crm.Application/Customers/CreateCustomer/CreateCustomerCommand.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Crm.Domain.Customers;
using Mediator;

namespace Crm.Application.Customers.CreateCustomer;

/// <summary>Contact-only customer: no Identity user, password, role or login. Single save — not transactional.</summary>
public sealed record CreateCustomerCommand(string FirstName, string LastName, string Email, string? PhoneNumber, string? Company)
    : ICommand<CustomerDetails>;

public sealed class CreateCustomerCommandHandler(
    ICustomerRepository customers,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateCustomerCommand, CustomerDetails>
{
    public async ValueTask<CustomerDetails> Handle(CreateCustomerCommand command, CancellationToken cancellationToken)
    {
        // Duplicate contact emails are allowed on purpose (docs/customer-management.md §6); no Identity lookup.
        var customer = Customer.CreateContact(
            Guid.CreateVersion7(),
            command.FirstName.Trim(),
            command.LastName.Trim(),
            command.Email.Trim(),
            AccountRules.NormalizeOptional(command.PhoneNumber),
            AccountRules.NormalizeOptional(command.Company),
            timeProvider.GetUtcNow());

        customers.Add(customer);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return CustomerDetails.From(customer);
    }
}
```

**Create file: `backend/src/Crm.Application/Customers/CreateCustomer/CreateCustomerValidator.cs`**

```csharp
using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Customers.CreateCustomer;

public sealed class CreateCustomerValidator : AbstractValidator<CreateCustomerCommand>
{
    public CreateCustomerValidator()
    {
        RuleFor(c => c.FirstName).PersonName();
        RuleFor(c => c.LastName).PersonName();
        RuleFor(c => c.Email).AccountEmail();
        RuleFor(c => c.PhoneNumber).OptionalPhone();
        RuleFor(c => c.Company).OptionalCompany();
    }
}
```

**Create file: `backend/src/Crm.Application/Customers/UpdateCustomer/UpdateCustomerCommand.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Mediator;

namespace Crm.Application.Customers.UpdateCustomer;

/// <summary>Registered customer: CRM row + Identity name/phone = two saves, hence transactional.</summary>
public sealed record UpdateCustomerCommand(Guid Id, string FirstName, string LastName, string Email, string? PhoneNumber, string? Company)
    : ICommand<CustomerDetails>, ITransactionalCommand;

public sealed class UpdateCustomerCommandHandler(
    ICustomerRepository customers,
    IIdentityService identity,
    TimeProvider timeProvider) : ICommandHandler<UpdateCustomerCommand, CustomerDetails>
{
    public async ValueTask<CustomerDetails> Handle(UpdateCustomerCommand command, CancellationToken cancellationToken)
    {
        var customer = await customers.GetByIdAsync(command.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer '{command.Id}' was not found.");

        var now = timeProvider.GetUtcNow();
        customer.ChangeContactEmail(command.Email.Trim(), now); // 422 for a changed login email, before any write
        customer.UpdateDetails(
            command.FirstName.Trim(),
            command.LastName.Trim(),
            AccountRules.NormalizeOptional(command.PhoneNumber),
            AccountRules.NormalizeOptional(command.Company),
            now);

        await identity.SyncLinkedAccountAsync(customer, cancellationToken);
        return CustomerDetails.From(customer); // EfUnitOfWork saves and commits after the handler
    }
}
```

**Create file: `backend/src/Crm.Application/Customers/UpdateCustomer/UpdateCustomerValidator.cs`** — same five rules as `CreateCustomerValidator` (`PersonName` ×2, `AccountEmail`, `OptionalPhone`, `OptionalCompany`).

**Create file: `backend/src/Crm.Application/Customers/GetCustomer/GetCustomerQuery.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Customers.GetCustomer;

public sealed record GetCustomerQuery(Guid Id) : IQuery<CustomerDetails>;

public sealed class GetCustomerQueryHandler(ICustomerRepository customers) : IQueryHandler<GetCustomerQuery, CustomerDetails>
{
    public async ValueTask<CustomerDetails> Handle(GetCustomerQuery query, CancellationToken cancellationToken) =>
        await customers.FindDetailsByIdAsync(query.Id, cancellationToken)
            ?? throw new NotFoundException($"Customer '{query.Id}' was not found.");
}
```

**Create file: `backend/src/Crm.Application/Customers/ListCustomers/ListCustomersQuery.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Mediator;

namespace Crm.Application.Customers.ListCustomers;

public sealed record ListCustomersQuery(string? Search, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<CustomerDetails>>;

public sealed class ListCustomersQueryHandler(ICustomerRepository customers)
    : IQueryHandler<ListCustomersQuery, PagedResult<CustomerDetails>>
{
    public async ValueTask<PagedResult<CustomerDetails>> Handle(ListCustomersQuery query, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize); // documented cap: larger values are clamped
        return await customers.ListAsync(search, query.Page, pageSize, cancellationToken);
    }
}
```

**Create file: `backend/src/Crm.Application/Customers/ListCustomers/ListCustomersValidator.cs`**

```csharp
using Crm.Application.Common.Paging;
using Crm.Application.Common.Validation;
using FluentValidation;

namespace Crm.Application.Customers.ListCustomers;

public sealed class ListCustomersValidator : AbstractValidator<ListCustomersQuery>
{
    public ListCustomersValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Search).MaximumLength(CustomerRules.SearchMaxLength);
    }
}
```

### 4 — Application: customer self-service use cases (`Crm.Application/Profile/…`)

**Create file: `backend/src/Crm.Application/Profile/GetMyCustomerProfile/GetMyCustomerProfileQuery.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Customers;
using Mediator;

namespace Crm.Application.Profile.GetMyCustomerProfile;

/// <summary>UserId always comes from the validated JWT subject, never from the request.</summary>
public sealed record GetMyCustomerProfileQuery(Guid UserId) : IQuery<CustomerDetails>;

public sealed class GetMyCustomerProfileQueryHandler(ICustomerRepository customers)
    : IQueryHandler<GetMyCustomerProfileQuery, CustomerDetails>
{
    public async ValueTask<CustomerDetails> Handle(GetMyCustomerProfileQuery query, CancellationToken cancellationToken) =>
        await customers.FindDetailsByUserIdAsync(query.UserId, cancellationToken)
            ?? throw new NotFoundException("Customer profile not found.");
}
```

**Create file: `backend/src/Crm.Application/Profile/UpdateMyCustomerProfile/UpdateMyCustomerProfileCommand.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Application.Customers;
using Mediator;

namespace Crm.Application.Profile.UpdateMyCustomerProfile;

/// <summary>No email field: the login email is not editable (AUTH-001). CRM row + Identity = transactional.</summary>
public sealed record UpdateMyCustomerProfileCommand(Guid UserId, string FirstName, string LastName, string? PhoneNumber, string? Company)
    : ICommand<CustomerDetails>, ITransactionalCommand;

public sealed class UpdateMyCustomerProfileCommandHandler(
    ICustomerRepository customers,
    IIdentityService identity,
    TimeProvider timeProvider) : ICommandHandler<UpdateMyCustomerProfileCommand, CustomerDetails>
{
    public async ValueTask<CustomerDetails> Handle(UpdateMyCustomerProfileCommand command, CancellationToken cancellationToken)
    {
        var customer = await customers.GetByUserIdAsync(command.UserId, cancellationToken)
            ?? throw new NotFoundException("Customer profile not found.");

        customer.UpdateDetails(
            command.FirstName.Trim(),
            command.LastName.Trim(),
            AccountRules.NormalizeOptional(command.PhoneNumber),
            AccountRules.NormalizeOptional(command.Company),
            timeProvider.GetUtcNow());

        await identity.SyncLinkedAccountAsync(customer, cancellationToken);
        return CustomerDetails.From(customer);
    }
}
```

**Create file: `backend/src/Crm.Application/Profile/UpdateMyCustomerProfile/UpdateMyCustomerProfileValidator.cs`** — rules: `FirstName.PersonName()`, `LastName.PersonName()`, `PhoneNumber.OptionalPhone()`, `Company.OptionalCompany()`.

### 5 — Application: integrate with AUTH-001 signup and `/me`

**File: `backend/src/Crm.Application/Auth/RegisterCustomer/RegisterCustomerCommand.cs`**

- Line 12: change the trailing comment to `// create user + add role + CRM profile, all in one transaction`.
- Add `using Crm.Application.Abstractions.Persistence;` and `using Crm.Domain.Customers;`.
- Line 14: constructor becomes `RegisterCustomerCommandHandler(IIdentityService identity, ICustomerRepository customers)`.
- Replace lines 25–29 (`return await identity.CreateUserAsync(…);`) with:

```csharp
        var user = await identity.CreateUserAsync(
            new NewUserAccount(email, command.FirstName.Trim(), command.LastName.Trim(), AccountRules.NormalizeOptional(command.PhoneNumber)),
            command.Password,
            UserRoles.Customer,
            cancellationToken);

        // Linked CRM profile; saved by EfUnitOfWork in the same transaction (no account without a profile).
        // Never linked to an existing contact-only record, even with the same email (docs/customer-management.md §6).
        customers.Add(Customer.CreateForAccount(
            Guid.CreateVersion7(), user.Id, user.FirstName, user.LastName, user.Email, user.PhoneNumber, user.CreatedAtUtc));

        return user;
```

`AuthController.Register` (lines 24–30) and `RegisterCustomerRequest` stay **unchanged**.

**File: `backend/src/Crm.Application/Profile/UpdateMyProfile/UpdateMyProfileCommand.cs`** — replace the whole file with:

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Mediator;

namespace Crm.Application.Profile.UpdateMyProfile;

/// <summary>
/// Identity update + (for Customers with a CRM profile) the mirrored CRM row = two saves, hence transactional.
/// Contract of PUT /api/v1/me is unchanged; Company is edited via /api/v1/me/customer-profile.
/// </summary>
public sealed record UpdateMyProfileCommand(Guid UserId, string FirstName, string LastName, string? PhoneNumber)
    : ICommand<UserAccount>, ITransactionalCommand;

public sealed class UpdateMyProfileCommandHandler(
    IIdentityService identity,
    ICustomerRepository customers,
    TimeProvider timeProvider) : ICommandHandler<UpdateMyProfileCommand, UserAccount>
{
    public async ValueTask<UserAccount> Handle(UpdateMyProfileCommand command, CancellationToken cancellationToken)
    {
        var user = await identity.UpdateProfileAsync(
            command.UserId,
            new UserProfileUpdate(command.FirstName.Trim(), command.LastName.Trim(), AccountRules.NormalizeOptional(command.PhoneNumber)),
            cancellationToken);

        // Staff accounts have no CRM profile; Customers keep their CRM mirror in sync.
        var customer = await customers.GetByUserIdAsync(command.UserId, cancellationToken);
        customer?.UpdateDetails(user.FirstName, user.LastName, user.PhoneNumber, customer.Company, timeProvider.GetUtcNow());

        return user;
    }
}
```

`UpdateMyProfileValidator.cs` and `MeController.Update` stay unchanged. **Do not** change `IIdentityService`.

### 6 — Infrastructure: EF configuration, DbSet, repository

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`**

```csharp
using Crm.Domain.Customers;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.LastName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.Property(c => c.PhoneNumber).HasMaxLength(32);
        builder.Property(c => c.Company).HasMaxLength(200);
        builder.Ignore(c => c.IsRegistered);

        // Optional 1:1 with an Identity account; many contact-only rows may have no account.
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<Customer>(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");

        builder.HasIndex(c => new { c.CreatedAtUtc, c.Id }).IsDescending(true, true); // default list order
        builder.HasIndex(c => c.Email);                                                 // not unique: duplicates allowed
    }
}
```

**File: `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`** — add `using Crm.Domain.Customers;` and, after line 14:

```csharp
    public DbSet<Customer> Customers => Set<Customer>();
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/CustomerRepository.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(CrmDbContext dbContext) : ICustomerRepository
{
    public void Add(Customer customer) => dbContext.Customers.Add(customer);

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Customers.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Customers.SingleOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public Task<CustomerDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Project(dbContext.Customers.Where(c => c.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public Task<CustomerDetails?> FindDetailsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Project(dbContext.Customers.Where(c => c.UserId == userId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<CustomerDetails>> ListAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Customers.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
        {
            // Parameterized; EF Core escapes LIKE wildcards for string.Contains. Case-insensitive under the default collation.
            query = query.Where(c =>
                c.FirstName.Contains(search) ||
                c.LastName.Contains(search) ||
                (c.FirstName + " " + c.LastName).Contains(search) ||
                c.Email.Contains(search) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await Project(query
                .OrderByDescending(c => c.CreatedAtUtc)
                .ThenByDescending(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDetails>(items, page, pageSize, totalCount);
    }

    // Filter/order BEFORE projecting (same rule as IdentityService.Project); IsRegistered is not mapped.
    private static IQueryable<CustomerDetails> Project(IQueryable<Customer> customers) =>
        customers.AsNoTracking().Select(c => new CustomerDetails(
            c.Id, c.FirstName, c.LastName, c.Email, c.PhoneNumber, c.Company, c.UserId != null, c.CreatedAtUtc, c.UpdatedAtUtc));
}
```

### 7 — Infrastructure: idempotent backfill initializer

**Create file: `backend/src/Crm.Infrastructure/Customers/CustomerProfileInitializer.cs`**

```csharp
using Crm.Domain.Customers;
using Crm.Domain.Identity;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Crm.Infrastructure.Customers;

/// <summary>
/// Idempotent: creates one linked CRM profile for every Customer-role account that has none
/// (accounts created before CUST-001). Never touches Agents/SuperAdmin, passwords, roles or tokens.
/// </summary>
internal sealed class CustomerProfileInitializer(
    CrmDbContext dbContext,
    ILogger<CustomerProfileInitializer> logger)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
        if (pending.Count > 0)
        {
            logger.LogWarning(
                "Customer profile initialization skipped: {Count} pending migration(s). Run 'dotnet ef database update' (README) and restart.",
                pending.Count);
            return;
        }

        var missing = await (
                from u in dbContext.Users.AsNoTracking()
                join ur in dbContext.UserRoles on u.Id equals ur.UserId
                join r in dbContext.Roles on ur.RoleId equals r.Id
                where r.Name == UserRoles.Customer && !dbContext.Customers.Any(c => c.UserId == u.Id)
                select new { u.Id, u.FirstName, u.LastName, u.Email, u.PhoneNumber, u.CreatedAtUtc })
            .ToListAsync(cancellationToken);

        if (missing.Count == 0)
        {
            return;
        }

        foreach (var u in missing)
        {
            dbContext.Customers.Add(Customer.CreateForAccount(
                Guid.CreateVersion7(), u.Id, u.FirstName, u.LastName, u.Email!, u.PhoneNumber, u.CreatedAtUtc));
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken); // one implicit transaction: all or nothing
            logger.LogInformation("Customer profile initialization created {Count} profile(s).", missing.Count);
        }
        catch (DbUpdateException exception)
        {
            // Unique UserId index: another process created a profile first. Nothing was saved; the next start retries.
            logger.LogWarning(exception, "Customer profile initialization did not complete; it will retry on next start.");
        }
    }
}
```

**Create file: `backend/src/Crm.Infrastructure/Customers/CustomerProfileInitializerExtensions.cs`**

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.Customers;

public static class CustomerProfileInitializerExtensions
{
    /// <summary>Call after BootstrapIdentityAsync and before app.Run(), so no request races the backfill.</summary>
    public static async Task InitializeCustomerProfilesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CustomerProfileInitializer>().RunAsync(cancellationToken);
    }
}
```

**File: `backend/src/Crm.Infrastructure/DependencyInjection.cs`**

- Add `using Crm.Infrastructure.Customers;`.
- After line 37 add `services.AddScoped<ICustomerRepository, CustomerRepository>();`.
- After line 57 add `services.AddScoped<CustomerProfileInitializer>();`.

**File: `backend/src/Crm.Api/Program.cs`** — add `using Crm.Infrastructure.Customers;`; after line 17 insert:

```csharp
// One CRM profile per pre-existing Customer account (idempotent). Skipped with a warning while migrations are pending.
await app.Services.InitializeCustomerProfilesAsync();
```

### 8 — API: policies, contracts, controllers

**File: `backend/src/Crm.Api/Authentication/AuthorizationPolicies.cs`** — lines 3–6 become:

```csharp
public static class AuthorizationPolicies
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Staff = "Staff";       // SuperAdmin or Agent
    public const string Customer = "Customer"; // Customer role only
}
```

**File: `backend/src/Crm.Api/Authentication/SecurityRegistration.cs`** — lines 61–63 become:

```csharp
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()) // auth by default
            .AddPolicy(AuthorizationPolicies.SuperAdmin, policy => policy.RequireRole(UserRoles.SuperAdmin))
            .AddPolicy(AuthorizationPolicies.Staff, policy => policy.RequireRole(UserRoles.SuperAdmin, UserRoles.Agent))
            .AddPolicy(AuthorizationPolicies.Customer, policy => policy.RequireRole(UserRoles.Customer));
```

**Create file: `backend/src/Crm.Api/Contracts/Common/PagedResponse.cs`**

```csharp
namespace Crm.Api.Contracts.Common;

/// <summary>List endpoints only. page is one-based.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
```

**Create file: `backend/src/Crm.Api/Contracts/Customers/CustomerResponse.cs`**

```csharp
using Crm.Application.Customers;

namespace Crm.Api.Contracts.Customers;

/// <summary>isRegistered = linked to a login account. The account id itself is never exposed.</summary>
public sealed record CustomerResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Company,
    bool IsRegistered,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc)
{
    public static CustomerResponse From(CustomerDetails c) =>
        new(c.Id, c.FirstName, c.LastName, c.Email, c.PhoneNumber, c.Company, c.IsRegistered, c.CreatedAtUtc, c.UpdatedAtUtc);
}
```

**Create files in `backend/src/Crm.Api/Contracts/Customers/`:**

```csharp
// CreateCustomerRequest.cs — no password, role or userId: contact-only records never get a login.
namespace Crm.Api.Contracts.Customers;
public sealed record CreateCustomerRequest(string FirstName, string LastName, string Email, string? PhoneNumber, string? Company);

// UpdateCustomerRequest.cs — for registered customers Email must equal the current login email (else 422).
namespace Crm.Api.Contracts.Customers;
public sealed record UpdateCustomerRequest(string FirstName, string LastName, string Email, string? PhoneNumber, string? Company);
```

**Create file: `backend/src/Crm.Api/Contracts/Profile/UpdateMyCustomerProfileRequest.cs`**

```csharp
namespace Crm.Api.Contracts.Profile;

/// <summary>No email: the login email is not editable.</summary>
public sealed record UpdateMyCustomerProfileRequest(string FirstName, string LastName, string? PhoneNumber, string? Company);
```

**Create file: `backend/src/Crm.Api/Controllers/CustomersController.cs`**

```csharp
using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.Customers;
using Crm.Application.Common.Paging;
using Crm.Application.Customers.CreateCustomer;
using Crm.Application.Customers.GetCustomer;
using Crm.Application.Customers.ListCustomers;
using Crm.Application.Customers.UpdateCustomer;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/customers")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class CustomersController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: SuperAdmin or Agent. One-based page (default 1), pageSize (default 20, values above 100 are clamped to 100), optional search on name/email/phone. Newest first.")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<CustomerResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListCustomersQuery(search, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<CustomerResponse>(
            result.Items.Select(CustomerResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent.")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(CustomerResponse.From(await sender.Send(new GetCustomerQuery(id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin or Agent. Creates a contact-only customer; no login account is created.")]
    [HttpPost]
    [ProducesResponseType<CustomerResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<CustomerResponse>> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await sender.Send(
            new CreateCustomerCommand(request.FirstName, request.LastName, request.Email, request.PhoneNumber, request.Company),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = customer.Id }, CustomerResponse.From(customer));
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. A registered customer's email cannot change (422); their name/phone changes also update their account.")]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerResponse>> Update(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var customer = await sender.Send(
            new UpdateCustomerCommand(id, request.FirstName, request.LastName, request.Email, request.PhoneNumber, request.Company),
            cancellationToken);
        return Ok(CustomerResponse.From(customer));
    }
}
```

**File: `backend/src/Crm.Api/Controllers/MeController.cs`** — add usings `Crm.Api.Contracts.Customers`, `Crm.Application.Profile.GetMyCustomerProfile`, `Crm.Application.Profile.UpdateMyCustomerProfile`; append after the `ChangePassword` action (after line 41):

```csharp
    [EndpointDescription("Requires role: Customer (own linked CRM profile only).")]
    [Authorize(Policy = AuthorizationPolicies.Customer)]
    [HttpGet("customer-profile")]
    public async Task<ActionResult<CustomerResponse>> GetCustomerProfile(CancellationToken cancellationToken) =>
        Ok(CustomerResponse.From(await sender.Send(new GetMyCustomerProfileQuery(User.GetUserId()), cancellationToken)));

    [EndpointDescription("Requires role: Customer (own linked CRM profile only). Email is not editable; name/phone also update the account.")]
    [Authorize(Policy = AuthorizationPolicies.Customer)]
    [HttpPut("customer-profile")]
    public async Task<ActionResult<CustomerResponse>> UpdateCustomerProfile(
        UpdateMyCustomerProfileRequest request, CancellationToken cancellationToken)
    {
        var customer = await sender.Send(
            new UpdateMyCustomerProfileCommand(User.GetUserId(), request.FirstName, request.LastName, request.PhoneNumber, request.Company),
            cancellationToken);
        return Ok(CustomerResponse.From(customer));
    }
```

**File: `backend/src/Crm.Api/DependencyInjection.cs`** — line 27: after `"Success: direct DTO with 200/201(+Location)/204. "` append `"Lists: ?page (one-based, default 1)&pageSize (default 20, max 100) -> { items, page, pageSize, totalCount }. "`.

### 9 — Migration

From `backend/` (API stopped):

```bash
dotnet ef migrations add AddCustomers --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations -- --environment Development
```

Inspect `Persistence/Migrations/*_AddCustomers.cs` before applying. `Up` must contain **only** `CreateTable("Customers", …)` (FK `FK_Customers_AspNetUsers_UserId`, `onDelete: ReferentialAction.Restrict`) and three `CreateIndex` calls (`IX_Customers_UserId` unique with `filter: "[UserId] IS NOT NULL"`, `IX_Customers_CreatedAtUtc_Id` with `descending: new[] { true, true }`, `IX_Customers_Email`). It must **not** contain `DropTable`, `AlterColumn` on Identity tables, `InsertData` or `Sql(`. `Down` drops only `Customers`. Then apply:

```bash
dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development
```

### 10 — Backend unit test (single happy path)

**Create file: `backend/tests/Crm.UnitTests/Customers/CreateCustomerCommandHandlerTests.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Application.Customers.CreateCustomer;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

public sealed class CreateCustomerCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidContact_CreatesUnlinkedContactOnlyCustomerAndSavesOnce()
    {
        var customers = new FakeCustomerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateCustomerCommandHandler(customers, unitOfWork, TimeProvider.System);

        var result = await handler.Handle(
            new CreateCustomerCommand("  Grace ", " Hopper ", " grace@example.com ", "  ", " Navy Labs "),
            CancellationToken.None);

        var stored = Assert.Single(customers.Added);
        Assert.Null(stored.UserId);
        Assert.False(result.IsRegistered);
        Assert.Equal(stored.Id, result.Id);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Grace", result.FirstName);
        Assert.Equal("Hopper", result.LastName);
        Assert.Equal("grace@example.com", result.Email);
        Assert.Null(result.PhoneNumber);
        Assert.Equal("Navy Labs", result.Company);
        Assert.Equal(result.CreatedAtUtc, result.UpdatedAtUtc);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public List<Customer> Added { get; } = [];
        public void Add(Customer customer) => Added.Add(customer);

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerDetails?> FindDetailsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<CustomerDetails>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

`LoginCommandHandlerTests.cs` and `GetSystemStatusQueryHandlerTests.cs` stay unchanged (`IIdentityService` is not modified).

---

## Frontend Tasks

> Run commands from `frontend/`. Follow the `features/agents/` patterns exactly: standalone components, `ChangeDetectionStrategy.OnPush`, `inject()`, signals, `takeUntilDestroyed(this.destroyRef)`, `describeHttpError`, `MatSnackBar`, `crm-` selectors, logical CSS properties only. **No new npm packages.**

### 11 — Customers feature: models and API

**Create file: `frontend/src/app/features/customers/customer-models.ts`**

```ts
/** Matches Crm.Api CustomerResponse. */
export interface Customer {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  company: string | null;
  isRegistered: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

/** Matches Crm.Api PagedResponse<T>. page is one-based. */
export interface PagedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface CustomerListParams {
  page: number;
  pageSize: number;
  search: string | null;
}

/** No password/role/userId: contact-only customers never get a login. */
export interface CreateCustomerRequest {
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  company: string | null;
}

/** For registered customers, email must be the unchanged login email. */
export type UpdateCustomerRequest = CreateCustomerRequest;
```

**Create file: `frontend/src/app/features/customers/customers-api.ts`**

```ts
import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from '../../core/api/api-url';
import {
  CreateCustomerRequest,
  Customer,
  CustomerListParams,
  PagedResponse,
  UpdateCustomerRequest,
} from './customer-models';

@Injectable({ providedIn: 'root' })
export class CustomersApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  list({ page, pageSize, search }: CustomerListParams): Observable<PagedResponse<Customer>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    const term = search?.trim();
    if (term) {
      params = params.set('search', term);
    }
    return this.http.get<PagedResponse<Customer>>(this.apiUrl.versioned('customers'), { params });
  }

  get(id: string): Observable<Customer> {
    return this.http.get<Customer>(this.apiUrl.versioned(`customers/${id}`));
  }

  create(body: CreateCustomerRequest): Observable<Customer> {
    return this.http.post<Customer>(this.apiUrl.versioned('customers'), body);
  }

  update(id: string, body: UpdateCustomerRequest): Observable<Customer> {
    return this.http.put<Customer>(this.apiUrl.versioned(`customers/${id}`), body);
  }
}
```

### 12 — Customers feature: routes, navigation

**Create file: `frontend/src/app/features/customers/customers.routes.ts`** — `CUSTOMERS_ROUTES`: `''` → `CustomerListPage` (title "Customers | Support CRM"), `'new'` → `CustomerCreatePage` ("New customer | Support CRM"), `':id'` → `CustomerDetailPage` ("Customer | Support CRM"); all `loadComponent`, same shape as `agents.routes.ts` lines 3–19.

**File: `frontend/src/app/app.routes.ts`** — insert before the `agents` route (line 25):

```ts
      {
        path: 'customers',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/customers/customers.routes').then(m => m.CUSTOMERS_ROUTES),
      },
```

**File: `frontend/src/app/core/layout/navigation.ts`** — insert between the Home (line 14) and Agents (line 15) entries:

```ts
  { label: 'Customers', icon: 'groups', path: '/customers', exact: false, roles: ['SuperAdmin', 'Agent'] },
```

### 13 — Customer list page

**Create file: `frontend/src/app/features/customers/customer-list-page.ts`**

```ts
import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';
import { EMPTY, Subject, catchError, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { describeHttpError } from '../../core/api/describe-http-error';
import { PageHeader } from '../../shared/ui/page-header/page-header';
import { StatusBadge } from '../../shared/ui/status-badge/status-badge';
import { Customer, CustomerListParams } from './customer-models';
import { CustomersApi } from './customers-api';

@Component({
  selector: 'crm-customer-list-page',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    PageHeader,
    StatusBadge,
    MatTableModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressBarModule,
  ],
  templateUrl: './customer-list-page.html',
  styleUrl: './customer-list-page.scss',
})
export class CustomerListPage {
  private readonly api = inject(CustomersApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly requests = new Subject<CustomerListParams>();

  protected readonly columns = ['name', 'email', 'phone', 'company', 'type', 'actions'];
  protected readonly pageSizeOptions = [10, 20, 50, 100];
  protected readonly searchControl = new FormControl('', { nonNullable: true });
  protected readonly customers = signal<Customer[]>([]);
  protected readonly totalCount = signal(0);
  protected readonly page = signal(1); // one-based, as the API
  protected readonly pageSize = signal(20);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.requests
      .pipe(
        // A newer search/page cancels the older request. Errors are caught per request (inside switchMap),
        // so a failed request does not complete the outer stream and later searches/pages still load.
        switchMap(params =>
          this.api.list(params).pipe(
            catchError((error: HttpErrorResponse) => {
              this.error.set(describeHttpError(error, 'Could not load customers.'));
              this.loading.set(false);
              return EMPTY;
            }),
          ),
        ),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(result => {
        this.customers.set(result.items);
        this.totalCount.set(result.totalCount);
        this.page.set(result.page);
        this.pageSize.set(result.pageSize);
        this.loading.set(false);
      });

    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.load(1));

    this.load(1);
  }

  protected onPage(event: PageEvent): void {
    this.pageSize.set(event.pageSize);
    this.load(event.pageIndex + 1);
  }

  private load(page: number): void {
    this.loading.set(true);
    this.error.set(null);
    this.requests.next({ page, pageSize: this.pageSize(), search: this.searchControl.value });
  }
}
```

> **Do not** move the error handling into `subscribe({ error })`: an error reaching the outer stream would terminate it permanently. With `catchError` inside `switchMap`, a failed request shows the message, and the next search or page change clears it (`load()` resets `error`) and requests again. No automatic retry.

**Create file: `frontend/src/app/features/customers/customer-list-page.html`**

- `<crm-page-header title="Customers" subtitle="Registered customers and contacts managed by the support team." />`
- `.toolbar` row: `mat-form-field appearance="outline" class="search"` with `<mat-label>Search</mat-label>`, `<input matInput type="search" [formControl]="searchControl" placeholder="Name, email or phone" />`, `<mat-icon matSuffix>search</mat-icon>`; and `<a mat-flat-button routerLink="new"><mat-icon>person_add</mat-icon> New customer</a>`.
- `@if (loading())` → `<mat-progress-bar mode="indeterminate" />`; `@if (error(); as message)` → `<p class="error" role="alert">{{ message }}</p>`.
- When not loading and no error: if `customers().length === 0` → `searchControl.value ? 'No customers match your search.' : 'No customers yet. Create the first contact.'`; otherwise a `.table-container` with `mat-table [dataSource]="customers()"` and columns:
  - `name` — `{{ c.firstName }} {{ c.lastName }}`
  - `email` — `{{ c.email }}`
  - `phone` — `{{ c.phoneNumber ?? '—' }}`
  - `company` — `{{ c.company ?? '—' }}`
  - `type` — `@if (c.isRegistered) { <crm-status-badge label="Registered" tone="success" /> } @else { <crm-status-badge label="Contact only" tone="neutral" /> }`
  - `actions` — `<a mat-button [routerLink]="c.id">View</a>`
- Below the table (still inside the non-empty branch): `<mat-paginator [length]="totalCount()" [pageIndex]="page() - 1" [pageSize]="pageSize()" [pageSizeOptions]="pageSizeOptions" (page)="onPage($event)" aria-label="Customer pages" />`.

**Create file: `frontend/src/app/features/customers/customer-list-page.scss`** — copy `agent-list-page.scss` and change `.toolbar` to `display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: var(--crm-space-4);` and add `.search { flex: 1 1 280px; max-inline-size: 420px; }`. Logical properties only.

### 14 — Customer create page

**Create files: `frontend/src/app/features/customers/customer-create-page.ts` / `.html` / `.scss`** — copy `agent-create-page.*` and adapt:

- Form (`NonNullableFormBuilder`): `firstName` (required, maxLength 100), `lastName` (required, maxLength 100), `email` (required, `Validators.email`, maxLength 256), `phoneNumber` (maxLength 32), `company` (maxLength 200). **No password field.**
- Header: `<crm-page-header title="New customer" subtitle="Add a customer contact for the support team." />`.
- At the top of the card content, a note: `<p class="no-login-note"><mat-icon aria-hidden="true">info</mat-icon> No login account created. This contact cannot sign in; it is a CRM record only.</p>` (import `MatIconModule`; style with `display: flex; gap: var(--crm-space-2); align-items: center; color: var(--mat-sys-on-surface-variant);`).
- Fields labelled "First name", "Last name", "Contact email", "Phone (optional)", "Company (optional)"; `autocomplete="off"`; `mat-error` texts like the agent page ("Company is too long (max 200 characters)").
- Submit button "Create contact" → `api.create({ firstName: trim, lastName: trim, email: trim, phoneNumber: trim || null, company: trim || null })` → snackbar "Customer created." (3000 ms) → `router.navigate(['/customers', customer.id])`; on error `describeHttpError(error, 'Could not create the customer.')`. Cancel: `<a mat-button routerLink="/customers">Cancel</a>`.

### 15 — Customer detail / edit page

**Create files: `frontend/src/app/features/customers/customer-detail-page.ts` / `.html` / `.scss`** — copy the structure of `agent-detail-page.*` (only the "Details" pattern; no status/password cards):

- Reads `id` from `inject(ActivatedRoute).snapshot.paramMap.get('id') ?? ''`; loads `api.get(id)`; `loadError` on failure (`'Could not load the customer.'`).
- `<crm-page-header title="Customer" subtitle="Profile and contact details." />`, back link `<a mat-button routerLink="/customers">Back to customers</a>`.
- Card "Summary" — `dl.readonly-list`: "Customer ID" (`{{ current.id }}`), "Type" (badge "Registered"/`success` or "Contact only"/`neutral`), "Created" (`{{ current.createdAtUtc | date: 'medium' }}`), "Last updated" (`{{ current.updatedAtUtc | date: 'medium' }}`). Import `DatePipe`. For registered customers add the line `<p class="hint">Has a login account. Name and phone changes also update the customer's account; the login email cannot be changed.</p>`; for contact-only `<p class="hint">No login account. Changing the contact email never creates or changes a login.</p>`.
- Card "Edit details" — form `firstName`, `lastName`, `email`, `phoneNumber`, `company` with the same validators as the create page. In `show(customer)`: `form.reset({...})`, then `customer.isRegistered ? form.controls.email.disable() : form.controls.email.enable()`. Label the email field "Login email (read-only)" when disabled, otherwise "Contact email". Submit uses `form.getRawValue()` (includes the disabled email, so a registered customer's unchanged email is sent) → `api.update(id, {…trimmed, empty optional → null})` → `show(updated)` + snackbar "Customer updated." (3000 ms); on error `describeHttpError(error, 'Could not update the customer.')`.
- SCSS: copy `agent-detail-page.scss`; add `.hint { margin: 0; color: var(--mat-sys-on-surface-variant); font: var(--mat-sys-body-medium); }`.

### 16 — Profile page: customer contact fields (reuse, no new screen)

**File: `frontend/src/app/features/profile/profile-api.ts`**

- Add interfaces after line 16:

```ts
/** Matches Crm.Api CustomerResponse (own linked CRM profile). */
export interface MyCustomerProfile {
  id: string;
  firstName: string;
  lastName: string;
  email: string;
  phoneNumber: string | null;
  company: string | null;
  isRegistered: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpdateMyCustomerProfileRequest {
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  company: string | null;
}
```

- Add methods to `ProfileApi`:

```ts
  getMyCustomerProfile(): Observable<MyCustomerProfile> {
    return this.http.get<MyCustomerProfile>(this.apiUrl.versioned('me/customer-profile'));
  }

  updateMyCustomerProfile(body: UpdateMyCustomerProfileRequest): Observable<MyCustomerProfile> {
    return this.http.put<MyCustomerProfile>(this.apiUrl.versioned('me/customer-profile'), body);
  }
```

**File: `frontend/src/app/features/profile/profile-page.ts`**

- Add `protected readonly isCustomer = computed(() => this.auth.role() === 'Customer');` (import `computed`) and `protected readonly customerProfile = signal<MyCustomerProfile | null>(null);`.
- Add `company: ['', [Validators.maxLength(200)]]` to `profileForm` (lines 53–57).
- Add `protected readonly customerProfileLoading = signal(false);` and `protected readonly customerProfileError = signal<string | null>(null);`.
- Add a method used both by the constructor and by the Retry button:

```ts
  protected loadCustomerProfile(): void {
    this.customerProfileLoading.set(true);
    this.customerProfileError.set(null);
    this.api
      .getMyCustomerProfile()
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: profile => {
          this.customerProfile.set(profile);
          this.profileForm.controls.company.setValue(profile.company ?? '');
          this.customerProfileLoading.set(false);
        },
        error: (error: HttpErrorResponse) => {
          this.customerProfile.set(null);
          this.customerProfileError.set(describeHttpError(error, 'Could not load your customer details.'));
          this.customerProfileLoading.set(false);
        },
      });
  }
```

- In the constructor, after the existing `getMe()` subscription: `if (this.isCustomer()) { this.loadCustomerProfile(); }`.
- In `saveProfile()` (lines 90–118): build `firstName`, `lastName`, `phoneNumber` as today. If `this.customerProfile()` is not null, call `api.updateMyCustomerProfile({ firstName, lastName, phoneNumber, company: value.company.trim() || null })`; on success `customerProfile.set(p)`, and update the identity view with `const current = this.user(); if (current) { const updated = { ...current, firstName: p.firstName, lastName: p.lastName, phoneNumber: p.phoneNumber }; this.user.set(updated); this.auth.updateCurrentUser(updated); }`, then the existing snackbar "Profile saved.". Otherwise keep the existing `api.updateMe(...)` path unchanged. Share the error handling (`'Could not save your profile.'`).

**File: `frontend/src/app/features/profile/profile-page.html`**

- After the Phone field (line 45), add:

```html
        @if (customerProfile()) {
          <mat-form-field appearance="outline">
            <mat-label>Company (optional)</mat-label>
            <input matInput formControlName="company" autocomplete="organization" />
            @if (profileForm.controls.company.invalid) {
              <mat-error>Company is too long (max 200 characters)</mat-error>
            }
          </mat-form-field>
        }
```

- In the read-only list (lines 14–19), after Role add `@if (customerProfile(); as p) { <dt>Customer ID</dt><dd>{{ p.id }}</dd> }`. Email stays read-only (it is already displayed as text, not an input).
- Directly after the read-only list (before the `<form>`), show the load failure with a retry (import `MatButtonModule` is already present):

```html
      @if (customerProfileError(); as message) {
        <div class="form-error customer-profile-error" role="alert">
          <span>{{ message }} Company and Customer ID are unavailable until your customer details load.</span>
          <button mat-button type="button" (click)="loadCustomerProfile()" [disabled]="customerProfileLoading()">Retry</button>
        </div>
      }
```

- **File: `frontend/src/app/features/profile/profile-page.scss`** — add `.customer-profile-error { display: flex; flex-wrap: wrap; align-items: center; gap: var(--crm-space-2); margin-block-end: var(--crm-space-4); }`.
- While `customerProfile()` is null (loading or failed), the Company field and Customer ID stay hidden and Save uses the existing `PUT /me` path (name/phone only, still mirrored to the CRM row server-side); the error banner stays visible until a retry succeeds, so the page never presents the CRM profile as loaded.
- Staff (SuperAdmin/Agent) see the page exactly as before.

### 17 — Frontend unit test (single happy path)

**Create file: `frontend/src/app/features/customers/customers-api.spec.ts`**

```ts
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from '../../core/api/api-config';
import { Customer, PagedResponse } from './customer-models';
import { CustomersApi } from './customers-api';

describe('CustomersApi', () => {
  it('requests a one-based page with a trimmed search term and returns the typed page', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } },
      ],
    });
    const api = TestBed.inject(CustomersApi);
    const http = TestBed.inject(HttpTestingController);
    let result: PagedResponse<Customer> | undefined;

    api.list({ page: 2, pageSize: 20, search: '  ada ' }).subscribe(page => (result = page));
    const request = http.expectOne(r => r.url === '/api/v1/customers');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('search')).toBe('ada');
    request.flush({
      items: [
        {
          id: 'c1', firstName: 'Ada', lastName: 'Lovelace', email: 'ada@example.com', phoneNumber: null,
          company: 'Analytical Engines', isRegistered: false,
          createdAtUtc: '2030-01-01T00:00:00Z', updatedAtUtc: '2030-01-01T00:00:00Z',
        },
      ],
      page: 2,
      pageSize: 20,
      totalCount: 21,
    });

    expect(result?.totalCount).toBe(21);
    expect(result?.items[0].isRegistered).toBe(false);
    http.verify();
  });
});
```

---

## Documentation Tasks

### 18 — `docs/customer-management.md` (create)

Concise bullets/tables:

1. **CRM customers vs Identity accounts** — `Customers` table, stable `Id` for future tickets/notes; *registered* = `UserId` set (Customer-role account, can sign in); *contact-only* = `UserId` null (no password/role/token, cannot sign in). Unique filtered index ⇒ at most one profile per account. No deletion in this story.
2. **Field ownership and sync** — the Recorded decisions rows "Field ownership (linked customer)" and "(contact-only)"; list the three write paths that keep Identity and CRM in sync in one transaction (`PUT /me`, `PUT /me/customer-profile`, `PUT /customers/{id}`); login email never changes; `Company` is CRM-only; last write wins.
3. **Role matrix** — the intake §4 table (List/search, View any, Create contact-only, Edit contact-only, Edit registered customer, View/Edit own linked profile) with ✔/—; server enforces roles (`Staff`/`Customer` policies) and ownership (JWT subject only); Angular guards/menus are UX only.
4. **API** — routes table: `GET /api/v1/customers` (Staff, 200, paged), `POST /api/v1/customers` (Staff, 201 + Location), `GET/PUT /api/v1/customers/{id}` (Staff, 200; 404 unknown; 422 changed login email), `GET/PUT /api/v1/me/customer-profile` (Customer, 200; 404 when no profile). List contract: `page` one-based default 1 (1…1 000 000), `pageSize` default 20 (≥ 1, > 100 clamped to 100), `search` ≤ 100 chars on first/last/full name, email, phone; order newest first, `Id` tie-breaker; response `{ items, page, pageSize, totalCount }`.
5. **Signup linkage and backfill** — signup creates account + profile atomically; startup initializer (after identity bootstrap, before serving requests) creates profiles for pre-existing Customer accounts only; idempotent; skipped while migrations are pending (apply migrations, restart); timestamps = account creation time.
6. **Duplicate contact email trade-off** — no auto-link/merge by email because signup does not verify email ownership (linking would hand a stranger a staff-created record); two records may share an email; staff can tell them apart by the Registered/Contact only badge; a verified claim/merge workflow is a future story.
7. **Future stories** — CUST-002 notes, CUST-003 attachments, CUST-004 interaction history and Ticket Management reference `Customers.Id` (not `AspNetUsers.Id`); deferred: verified linking/merge, deletion/archiving, invitations, per-agent assignment, departments/branches, optimistic concurrency, full-text search.

### 19 — Update existing docs

- **`docs/api-conventions.md`** — replace line 20 with: "**Pagination:** list endpoints take one-based `page` (default 1) and `pageSize` (default 20, values above 100 are clamped to 100) plus feature-specific filters (e.g. `search`), use a stable default order with an `Id` tie-breaker, and return `PagedResponse<T>` (`Crm.Api/Contracts/Common`) `{ items, page, pageSize, totalCount }`. Non-list endpoints keep returning the DTO directly. First used by `GET /api/v1/customers` ([customer-management.md](customer-management.md))." In line 19 append: "Staff endpoints use `AuthorizationPolicies.Staff` (SuperAdmin or Agent); Customer self-service endpoints use `AuthorizationPolicies.Customer` and take the user id from the token only."
- **`docs/identity-and-access.md`** — role matrix (lines 7–14): add rows "Manage CRM customers (list / view / create contact-only / edit) | ✔ | ✔ | —" and "View / edit own CRM customer profile | — | — | ✔". Routes table (lines 18–30): add `GET / POST /api/v1/customers` (SuperAdmin, Agent), `GET / PUT /api/v1/customers/{id}` (SuperAdmin, Agent), `GET / PUT /api/v1/me/customer-profile` (Customer). After line 32 add: "Signup also creates the linked CRM customer profile (see [customer-management.md](customer-management.md))." Line 74: remove "customer CRM records and company field," and "agent ticket / customer permissions" → "agent ticket permissions".
- **`docs/persistence-and-transactions.md`** — append to line 7: "Writes spanning Identity and CRM rows (signup + customer profile; linked customer name/phone sync) are `ITransactionalCommand`s for the same reason."
- **`docs/frontend-ui-foundation.md`** — after line 12 add: "**Lists:** server-side paging with `MatPaginator` (`pageIndex = page - 1`), debounced (300 ms) search field, `StatusBadge` for record type/status."
- **`README.md`** — line 3: "contains the architectural foundation (`ARCH-001`), identity & access (`AUTH-001`) and customer profiles & contacts (`CUST-001`)". Conventions list (after line 54): add `- [Customer management](docs/customer-management.md)`.

---

## Edge Cases & Failure Modes

- **Migrations pending at startup** — `CustomerProfileInitializer.RunAsync` logs a warning and returns (task 7), like `IdentityBootstrapper` lines 23–30. Customers then have no profile: `GET /me/customer-profile` ⇒ 404 and the Profile page shows "Customer profile not found." with a Retry button; name/phone still save via `PUT /me` (task 16). Fix: `dotnet ef database update`, restart the API.
- **Backfill re-run** — the `!dbContext.Customers.Any(c => c.UserId == u.Id)` filter makes later starts insert nothing; no log line when nothing is missing.
- **Backfill unique-index conflict** (two API instances starting together) — `SaveChangesAsync` fails with `DbUpdateException` on `IX_Customers_UserId`; nothing from that batch is saved; warning logged; next start completes it. Single-instance dev cannot hit this because the initializer runs before `app.Run()`.
- **Agents / SuperAdmin** — never selected by the backfill join (`r.Name == UserRoles.Customer`), never created by signup (role fixed to Customer), and `/me/customer-profile` is forbidden for them (policy `Customer` ⇒ 403 ProblemDetails via `UseStatusCodePages`).
- **Signup failure after the account insert** (e.g. DB error inserting `Customers`) — `RegisterCustomerCommand` is `ITransactionalCommand`; `EfUnitOfWork` rolls back the user, role and profile together; the client sees 500 and can retry with the same email.
- **Transient SQL error during signup or updates** — `EfUnitOfWork` retries the whole handler after `ChangeTracker.Clear()`; `Guid.CreateVersion7()` is re-generated on retry; no duplicates.
- **Contact-only record with the same email as a new signup** — signup's `EmailExistsAsync` checks Identity users only, so registration succeeds; a second, linked `Customers` row is created; the contact-only row is untouched and stays unlinked (task 5 comment, docs §6).
- **Staff creates a contact whose email matches an existing account or contact** — allowed (no uniqueness, no Identity lookup in `CreateCustomerCommandHandler`); no login is created or changed.
- **Client sends `userId`, `isRegistered`, `id`, `createdAtUtc` or `password` in a body** — not on any request record; System.Text.Json ignores unknown members; the server sets them.
- **Staff edits a registered customer's email** — `Customer.ChangeContactEmail` throws `DomainException` ⇒ 422 "A registered customer's email is their login email and cannot be changed." before any write. Same value with different casing ⇒ accepted, stored value unchanged. Angular disables the field, so this only happens via direct API calls.
- **Staff edits a registered customer's name/phone** — `CustomerAccountSync.SyncLinkedAccountAsync` updates `AspNetUsers` in the same transaction; the customer sees the new name at their next `GET /me` (JWT has no name claims, so no token refresh is needed).
- **Customer edits name/phone via the old `PUT /me`** — `UpdateMyProfileCommandHandler` mirrors them onto the CRM row (task 5); Company is preserved.
- **Linked account vanished** — impossible while `FK_Customers_AspNetUsers_UserId` is `Restrict`; if `UpdateProfileAsync` ever throws `NotFoundException`, the transaction rolls back and the API returns 404.
- **Customer tries another customer's record** — `/api/v1/customers/*` requires `Staff` ⇒ 403; `/me/customer-profile` reads only `User.GetUserId()` (no id in route or body).
- **Unknown or malformed id** — `GET/PUT /customers/{id:guid}` unknown ⇒ 404 "Customer '…' was not found."; non-GUID ⇒ route does not match ⇒ 404.
- **List parameters** — `page=0`/negative or `pageSize=0` ⇒ 400 ValidationProblemDetails (`ListCustomersValidator`); `page` > 1 000 000 ⇒ 400; `pageSize=500` ⇒ clamped, response `pageSize` = 100; non-numeric `page` ⇒ 400 from `[ApiController]` model binding; page beyond the last ⇒ 200 with empty `items` and the real `totalCount`.
- **Search input** — whitespace-only ⇒ no filter; > 100 chars ⇒ 400; `%`, `_`, `[` are matched literally (EF Core parameterizes and escapes `string.Contains`); case-insensitive under the default `SQL_Latin1_General_CP1_CI_AS` collation of LocalDB (a case-sensitive database collation would make search case-sensitive — documented, not handled).
- **Paging stability** — new rows inserted while paging shift later pages (offset paging); acceptable and documented. Equal `CreatedAtUtc` values (backfilled accounts created in the same instant) are ordered by `Id DESC`, so pages never overlap.
- **Unicode / long text** — all string columns are `nvarchar`; validators enforce 100/100/256/32/200 before EF; leading/trailing whitespace trimmed; empty optional fields stored as `NULL` (`AccountRules.NormalizeOptional`).
- **Concurrent edits** — last write wins (no row version); documented in `docs/customer-management.md` §2.
- **Profile page for a Customer when `/me/customer-profile` fails** — `customerProfileError` shows the ProblemDetails message (or "Could not load your customer details.") with a Retry button that calls `loadCustomerProfile()` again (disabled while loading); `customerProfile` stays null, so Company and Customer ID are hidden and Save uses `PUT /me` (still mirrored server-side). A successful retry clears the error and shows the CRM fields (task 16).
- **Rapid typing in the list search** — 300 ms debounce + `switchMap` cancels stale requests, so results always match the latest term.
- **List HTTP error** — `catchError` inside `switchMap` shows the message and returns `EMPTY` for that request only; the outer stream stays alive, so the next search or page change clears the error and loads normally (task 13).
- **EF translation** — `IsRegistered` is ignored in the model; queries use `c.UserId != null` (task 6). Filtering/sorting happens before `Project` (same rule as `IdentityService` lines 22–30). If `(c.FirstName + " " + c.LastName).Contains(search)` fails to translate, drop that one predicate (first and last name are still matched individually) and note it in the doc.
- **Migration content** — if `migrations add` produces changes to Identity tables or `RefreshSessions`, the model was edited unintentionally: `dotnet ef migrations remove …` and fix the configuration before applying.

---

## Test Plan

1. **Unit (backend) — add:** `backend/tests/Crm.UnitTests/Customers/CreateCustomerCommandHandlerTests.cs` → `Handle_ValidContact_CreatesUnlinkedContactOnlyCustomerAndSavesOnce` (task 10), hand-written fakes in the style of `Auth/LoginCommandHandlerTests.cs`. Keep `Auth/LoginCommandHandlerTests.cs` and `Diagnostics/GetSystemStatusQueryHandlerTests.cs` unchanged.
2. **Unit (frontend) — add:** `frontend/src/app/features/customers/customers-api.spec.ts` → "requests a one-based page with a trimmed search term and returns the typed page" (task 17), pattern of `core/auth/auth-service.spec.ts`. Keep `core/auth/auth-service.spec.ts` and `core/api/api-url.spec.ts`.
3. **Manual smoke (happy paths only):** Verification Steps 4–9.

No other tests, frameworks, mocking libraries, integration/SQL/API/E2E suites or negative-path checks.

---

## Migration / Rollback

- **Forward:** `AddCustomers` only creates the `Customers` table, its FK to `AspNetUsers` and three indexes. No existing table or row is altered; no data is inserted by the migration. Profiles for existing Customer accounts are created by the startup initializer after the migration is applied.
- **Rollback (local):** stop the API; from `backend/`: `dotnet ef database update AddIdentityAndRefreshSessions --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` (drops `Customers` — all CRM profiles and contact-only records are lost; Identity accounts, roles and sessions are untouched), then `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` and revert the code (an AUTH-001 build would fail signup only if the `Customers` code remained).
- **Half-applied state:** EF applies the migration's DDL in one transaction on SQL Server; on failure nothing is created — fix and re-run `database update`. If the API starts between migration and code deployment, the initializer simply runs on the next start. A failed backfill save leaves no partial rows (single `SaveChangesAsync`).

---

## Verification Steps

1. **Backend builds and tests:** in `backend/` run `dotnet build` (0 errors) then `dotnet test` (**3** tests passed: the two existing + `CreateCustomerCommandHandlerTests`).
2. **Layer direction:** `grep -rn "Microsoft.AspNetCore\|EntityFrameworkCore" backend/src/Crm.Application backend/src/Crm.Domain --include=*.cs` returns nothing; `git diff --stat -- backend/src/Crm.Application/Abstractions/Identity` shows no changes.
3. **Migration + existing data preserved:** *before* applying task 9, record `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT COUNT(*) FROM AspNetUsers; SELECT COUNT(*) FROM AspNetUsers u JOIN AspNetUserRoles ur ON ur.UserId = u.Id JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE r.Name = 'Customer';"` (use the database name from your `ConnectionStrings:CrmDatabase` secret; `CrmDev` in the AUTH-001 setup). If the count is 0, register one customer through the running app **before** applying the migration (and before building the CUST-001 code), so the backfill has something to do. After `database update`: `SELECT MigrationId FROM __EFMigrationsHistory` lists three migrations ending with `…_AddCustomers`; `SELECT COUNT(*) FROM AspNetUsers` is unchanged; `grep -n "InsertData\|Sql(\|DropTable(\"AspNet" backend/src/Crm.Infrastructure/Persistence/Migrations/*_AddCustomers.cs` returns nothing except `DropTable("Customers")` in `Down`.
4. **Backfill:** `dotnet run --project src/Crm.Api --launch-profile http` → log "Customer profile initialization created N profile(s)." with N = the Customer count from step 3. `sqlcmd … -Q "SELECT COUNT(*) FROM Customers WHERE UserId IS NOT NULL; SELECT COUNT(*) FROM Customers c JOIN AspNetUserRoles ur ON ur.UserId = c.UserId JOIN AspNetRoles r ON r.Id = ur.RoleId WHERE r.Name <> 'Customer';"` → N and 0. Restart the API: no initialization log line; counts unchanged. The pre-existing customer can still sign in with the same password.
5. **Frontend builds and tests:** in `frontend/` run `npx ng build` (no errors; budget warnings acceptable) and `npm test -- --watch=false` (**3** tests passed).
6. **Staff flows (browser, `npm start`, http://localhost:4200):** sign in as `admin@admin.com` → "Customers" appears in navigation. Customers → "New customer" shows "No login account created" and no password field → create "Grace Hopper", `grace@example.com`, company "Navy Labs" → detail page shows type "Contact only". Edit company and contact email → "Customer updated.". Back to the list: Grace is first (newest), badge "Contact only"; the backfilled customer shows "Registered". Type "hopper" in Search → only matching rows; search by part of the phone number and email also works; change page size to 10 and page forward when more than 10 rows exist. `sqlcmd … -Q "SELECT COUNT(*) FROM AspNetUsers WHERE Email = 'grace@example.com'"` → 0. Sign out, sign in as an Agent → same Customers screens work; Agents menu still hidden.
7. **Registered customer edit by staff:** open the registered customer's detail → email field disabled and labelled "Login email (read-only)"; change the phone → saved; `sqlcmd … -Q "SELECT PhoneNumber FROM AspNetUsers WHERE Id = (SELECT UserId FROM Customers WHERE Id = '<customer-id>')"` shows the new phone.
8. **New signup creates a linked profile:** sign out; "Create an account" with a new email → Home. `sqlcmd … -Q "SELECT c.Id, c.Email, c.UserId FROM Customers c JOIN AspNetUsers u ON u.Id = c.UserId WHERE u.Email = '<new-email>'"` → exactly one row. Register another account using `grace@example.com` → succeeds; `SELECT Email, UserId FROM Customers WHERE Email = 'grace@example.com'` → two rows, one with `UserId` NULL (the contact) and one linked (no merge).
9. **Customer self-service:** as the new customer, Profile shows email (read-only), Customer ID and a Company field; change last name, phone and company → "Profile saved."; reload → values persist; the shell header shows the new name. As staff, the same customer's detail page shows the updated name/phone/company. The Customer has no "Customers" navigation entry and navigating to `/customers` redirects to Home.
10. **OpenAPI:** `curl -s http://localhost:5080/openapi/v1.json` lists `/api/v1/customers`, `/api/v1/customers/{id}`, `/api/v1/me/customer-profile` with "Requires role: …" descriptions.
11. **Regression:** AUTH-001 flows still work — login/logout, reload keeps the session (`POST /api/v1/auth/refresh` 200), SuperAdmin Agents list/create, Agent Profile save via `PUT /me`; `grep -rnE "(margin|padding)-(left|right)|text-align: *(left|right)" frontend/src` returns nothing; `grep -rnE "localStorage|sessionStorage" frontend/src/app --include=*.ts` matches only `auth-service.spec.ts`.

No negative-path manual checks are part of verification.

---

## Done Criteria

- [ ] Changes extend ARCH-001 + AUTH-001 in place; no new solution, signup route, login mechanism or role; `IIdentityService` unchanged; Domain/Application reference no ASP.NET Core/EF packages (Verification 2).
- [ ] `Customer` has a stable `Id`, required first/last name and contact email, optional phone/company, UTC `CreatedAtUtc`/`UpdatedAtUtc` and an optional `UserId` with a unique filtered index (task 6).
- [ ] SuperAdmin and Agent create contact-only customers via `POST /api/v1/customers` without any Identity user, password, token or role (Verification 6).
- [ ] SuperAdmin and Agent list/search/paginate, view and edit customers through `Staff`-protected APIs and the Angular Customers screens (Verification 6–7).
- [ ] `GET /api/v1/customers` implements one-based `page` (default 1), `pageSize` (default 20, clamped at 100), name/email/phone `search`, `CreatedAtUtc DESC, Id DESC` ordering and `{ items, page, pageSize, totalCount }`; documented in `docs/api-conventions.md`.
- [ ] `POST /api/v1/auth/register` (unchanged contract) creates the account and exactly one linked profile in one transaction (Verification 8).
- [ ] Pre-existing Customer accounts get exactly one linked profile through the idempotent startup initializer; Agents/SuperAdmin get none (Verification 4).
- [ ] Contact-only customers stay unlinked; no linking or merging by email anywhere; duplicate emails documented (Verification 8, docs §6).
- [ ] Only the authenticated Customer views/edits their own profile via `/api/v1/me/customer-profile` using `User.GetUserId()`; Customers get 403 on `/api/v1/customers*`.
- [ ] Login email is never changed (422 for registered customers; no email field in self-service); name/phone stay identical in `AspNetUsers` and `Customers` after every write path; contact-only email edits never touch `AspNetUsers`.
- [ ] Existing accounts, roles, passwords, refresh sessions and SuperAdmin bootstrap behave as before (Verification 4, 11).
- [ ] Migration `AddCustomers` is additive only (no drops, alters of existing tables or seeded rows) and applied without recreating the database (Verification 3).
- [ ] All new endpoints follow `/api/v1`, `…Request`/`…Response`, camelCase, direct DTO success codes (200/201+Location), FluentValidation 400, 404/422 ProblemDetails; role requirements appear in OpenAPI (Verification 10).
- [ ] Angular Customers feature is lazy-loaded, guarded with `roleGuard('SuperAdmin', 'Agent')`, listed in `NAV_ITEMS` for those roles, uses `ApiUrl`, Material components, `StatusBadge`/`PageHeader` and logical CSS; the existing Profile page is extended (Company, Customer ID) rather than duplicated.
- [ ] `dotnet build`, `dotnet test` (3 passed), `npx ng build`, `npm test -- --watch=false` (3 passed) succeed; manual flows in Verification 3–9 pass; no extra test types added.
- [ ] `docs/customer-management.md` created; `docs/api-conventions.md`, `docs/identity-and-access.md`, `docs/persistence-and-transactions.md`, `docs/frontend-ui-foundation.md` and `README.md` updated.
- [ ] No notes, attachments, interaction/ticket history, invitations, verified linking/merge, deletion, import/export, Customer Portal or other out-of-scope features were added.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 04.**
