# Story 02 — Users, Roles, Authentication & Authorization

## Prerequisites

- **Story 01 completed:** [../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md) (`ARCH-001`). It created `backend/` (Clean Architecture, source-generated Mediator, FluentValidation, `IUnitOfWork`/`ITransactionalCommand`/`TransactionBehavior`, `GlobalExceptionHandler`) and `frontend/` (Angular 22 standalone, Material shell, `ApiUrl`, dev proxy). This story **extends** that code; it does not re-scaffold anything.
- Shared contracts owned by Story 01 that this story changes deliberately (and must document): `docs/api-conventions.md` (new 401/409 mappings, auth-by-default, `/auth` and `/me` singleton routes) and `docs/frontend-ui-foundation.md` (auth folder, guards, role-aware navigation).
- Verified on the planning machine (2026-10-15): .NET SDK **10.0.400**; EF Core packages **10.0.13**; `Microsoft.AspNetCore.OpenApi` 10.0.13 depends on `Microsoft.OpenApi` **[2.12.2, 3.0.0)** (namespace `Microsoft.OpenApi`, interface types such as `IOpenApiSecurityScheme`); `Mediator.Abstractions` 3.0.2 exposes `ICommand`, `ICommand<T>`, `ICommandHandler<TCommand>` and `ICommandHandler<TCommand,TResponse>`; user secret `ConnectionStrings:CrmDatabase` is already set for `src/Crm.Api`.
- The intake path passed to `/squad-plan` (`.squad/stories/identity-access-management/AUTH-001/intake.md`) does not exist; the AUTH-001 intake is `.squad/stories/identity-access-management/users-roles-authentication-authorization/intake.md`. Its `attachments/` folder is **empty**.
- **Secrets:** the initial SuperAdmin password was shared in chat. **Never** write it into any file, command example, log or commit. Every document and command in this plan uses the placeholder `<initial-superadmin-password>`.

---

## Story Goal

Add the identity and access-control foundation to the existing CRM:

1. **Three server-controlled roles** — `SuperAdmin`, `Agent`, `Customer` — provisioned idempotently at startup together with the initial SuperAdmin `admin@admin.com` (password from user secrets, hashed by ASP.NET Core Identity).
2. **Backend** — ASP.NET Core Identity (EF Core stores, `Guid` keys) in `Crm.Infrastructure`; one new EF Core migration; short-lived HS256 JWT access tokens; rotating, single-use, hashed-at-rest refresh sessions with family revocation on reuse; refresh token delivered in a `Secure; HttpOnly; SameSite=Strict` cookie; double-submit CSRF token for cookie-authenticated endpoints; authentication required by default; SuperAdmin policy for Agent management; per-request account-state check (active flag + security stamp) so disable / password change / password reset cut off existing JWTs immediately; login lockout + rate limiting.
3. **API** (`/api/v1`): `auth/register`, `auth/login`, `auth/refresh`, `auth/logout`, `me` (GET/PUT), `me/password`, `agents` (list/get/create/update/status/password).
4. **Angular** — in-memory access token, silent session restore on reload via the refresh cookie, auth interceptor with single-flight refresh, guards, Sign In / Sign Up pages, role-aware shell navigation with sign-out, Profile page (edit + change password), SuperAdmin Agent list / create / detail (edit, enable/disable, set password).
5. Exactly **one** new backend happy-path unit test and **one** new frontend happy-path unit test.
6. Documentation: role matrix, bootstrap/secret setup (placeholders only), routes, session/expiry behaviour, local cookie/HTTPS notes, deferred decisions.

**Not in scope** (do **not** build): tickets, customer CRM records/interactions/notes/attachments, customer portal, dashboards/reports; email confirmation, forgot-password, SMTP/SMS; MFA, SSO/OAuth; custom roles/permission admin; session/device management screens; email change for any account; company/organization field (no customer domain exists yet — deferred to Customer Management); localization/RTL; departments/branches/tenancy; Docker/CI/CD; integration/E2E/negative-path tests.

### Recorded decisions

| Concern | Decision |
|---|---|
| Identity store | `Microsoft.AspNetCore.Identity.EntityFrameworkCore` **10.0.13** in `Crm.Infrastructure`; `AddIdentityCore<ApplicationUser>().AddRoles<IdentityRole<Guid>>().AddEntityFrameworkStores<CrmDbContext>()` (no cookie auth, no `SignInManager`). |
| JWT | Issue with `Microsoft.IdentityModel.JsonWebTokens` **8.19.2** (`JsonWebTokenHandler`) in Infrastructure; validate with `Microsoft.AspNetCore.Authentication.JwtBearer` **10.0.13** in `Crm.Api`. HS256, key = Base64 of ≥ 32 random bytes from user secrets `Jwt:SigningKey`. |
| Access token | 10 min (`Jwt:AccessTokenLifetime`), claims `sub`, `role`, `sstamp`, `jti`, `iat`/`nbf`/`exp`. No email, names or phone in the token. Held in Angular memory only. |
| Refresh token | 32 random bytes, Base64Url; stored only as uppercase hex SHA-256 (`RefreshSessions.TokenHash`); 7 days sliding (`Jwt:RefreshTokenLifetime`); single-use; reuse ⇒ whole family revoked. |
| Concurrent refresh (multi-tab) | **Reuse grace window** of 30 s (`Jwt:RefreshTokenReuseGracePeriod`). A token rotated less than 30 s ago, presented again while its family still has an active session, is a benign race (several tabs sent the same cookie at once): the server issues a **sibling** session in the same family instead of revoking it. Outside the window, or once the family is dead, presenting a rotated token is replay ⇒ family revoked. Same rule after losing the row-level rotation race. No extra infrastructure; `0` disables the window. |
| Logout scope | Revokes the **whole family** of the presented token (one browser login = one family; tabs share the cookie), so sibling sessions created by the grace rule never outlive logout. |
| Cookies | `crm_refresh` (HttpOnly, Secure, SameSite=Strict, Path=`/api/v1/auth`, Expires = session expiry); `XSRF-TOKEN` (not HttpOnly, Secure, SameSite=Strict, Path=`/`, same expiry). Angular's built-in XSRF support sends `X-XSRF-TOKEN`. |
| Revocation | Every authenticated request re-reads `IsActive` + `SecurityStamp` (`JwtBearerEvents.OnTokenValidated`). Password change/reset and disable rotate the stamp and revoke all refresh sessions of the user. |
| Brute force | Identity lockout: 5 failures ⇒ 15 min. Rate limiter policy `credentials`: 10 requests / minute / remote IP on `auth/login` and `auth/register`. Uniform login error "Invalid email or password." |
| Passwords | Min 8, max 128 chars, no composition rules (Identity `PasswordOptions` + FluentValidation). |
| Bootstrap | `IdentityBootstrapper` runs after `builder.Build()` in `Program.cs`; skips with a warning when migrations are pending; never resets an existing admin; never promotes an unrelated account. |

---

## Context — Read These Files First

1. `.squad/stories/identity-access-management/users-roles-authentication-authorization/intake.md` — sections 1–8 of the Description and the Acceptance criteria are the contract. Extra notes: SuperAdmin-only agent creation; no password in files.
2. `.squad/stories/project-architecture/project-init-architecture/attachments/azm_squad_customer_support_crm 1.pdf` — CRM feature list (the intake's `…1(2).pdf` is not on disk; this story's `attachments/` is empty). Business context only (Security & Administration, Customer Management). Gitignored.
3. `backend/src/Crm.Api/Program.cs` — lines 13–30: pipeline (`UseExceptionHandler`, `UseStatusCodePages`, dev-only `MapOpenApi`/`MapScalarApiReference`, `MapControllers`). Bootstrap, authentication, authorization and rate limiting are inserted here.
4. `backend/src/Crm.Api/DependencyInjection.cs` — lines 10–43: `AddPresentation`. Lines 20–28 are the OpenAPI document transformer (extend it); lines 30–40 register Mediator with `Assemblies = [typeof(GetSystemStatusQuery)]` (covers the whole `Crm.Application` assembly — no change needed) and the behaviour order Logging → Validation → Transaction.
5. `backend/src/Crm.Api/Errors/GlobalExceptionHandler.cs` — lines 20–48: exception → `ProblemDetails` switch. New `UnauthorizedException` (401) and `ConflictException` (409) cases go after the `DomainException` case (ends line 41). Lines 50–57 log only status/title — keep it that way (no credentials in logs).
6. `backend/src/Crm.Api/Controllers/HealthController.cs` — lines 8–10: must get `[AllowAnonymous]` once the fallback policy exists.
7. `backend/src/Crm.Api/ApiRoutes.cs` — `ApiRoutes.V1 = "api/v1"` (line 10). Controllers use `[Route(ApiRoutes.V1 + "/…")]`.
8. `backend/src/Crm.Api/Contracts/Health/HealthResponse.cs` — record DTO style for new `Contracts/<Feature>/` files.
9. `backend/src/Crm.Application/Abstractions/Persistence/IUnitOfWork.cs` (lines 7–20) and `Abstractions/Messaging/ITransactionalCommand.cs` (line 8) — the only transaction APIs. Handlers never begin transactions themselves.
10. `backend/src/Crm.Application/Common/Behaviors/TransactionBehavior.cs` — lines 21–37: wraps only `ITransactionalCommand` messages via `ExecuteInTransactionAsync`; **any exception rolls back everything the handler wrote**, which drives the Login and Renew-session designs below.
11. `backend/src/Crm.Application/Common/Behaviors/LoggingBehavior.cs` — lines 16–25 log only `typeof(TMessage).Name`. Commands carrying passwords/tokens rely on this; **do not** add message-content logging.
12. `backend/src/Crm.Application/Diagnostics/GetSystemStatus/GetSystemStatusQuery.cs` — query + handler in one file, `public sealed` handler with primary-constructor DI; copy this shape for every new use case.
13. `backend/src/Crm.Application/DependencyInjection.cs` — lines 9–14: validators registered with `includeInternalTypes: true`; `TimeProvider.System` singleton.
14. `backend/src/Crm.Infrastructure/DependencyInjection.cs` — lines 11–33: lazy `AddDbContext<CrmDbContext>` with retry-on-failure; `IUnitOfWork` registration at line 31.
15. `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` — lines 5–14: currently `DbContext`; `OnModelCreating` applies configurations **before** `base.OnModelCreating` (must be reversed for `IdentityDbContext`).
16. `backend/src/Crm.Infrastructure/Persistence/EfUnitOfWork.cs` — lines 11–44: execution strategy → `ChangeTracker.Clear()` → `BeginTransactionAsync` → operation → `SaveChangesAsync` → `CommitAsync`. `UserManager` and `ExecuteUpdateAsync` calls made by a handler run on the same scoped `CrmDbContext` and therefore inside this transaction.
17. `backend/src/Crm.Infrastructure/Persistence/Migrations/20261014234527_InitialCreate.cs` — empty `Up`/`Down`; the new migration is the first with tables.
18. `backend/src/Crm.Api/appsettings.json` (lines 1–13) and `Properties/launchSettings.json` (`http` profile on `http://localhost:5080`, lines 4–12).
19. `backend/tests/Crm.UnitTests/Diagnostics/GetSystemStatusQueryHandlerTests.cs` — xUnit test style (`public sealed class`, `[Fact]`, `Handle_…` names). Test project references only `Crm.Application` (csproj line 22); no mocking library — use hand-written fakes.
20. `frontend/src/app/app.config.ts` (lines 7–14), `app.routes.ts` (lines 4–14), `core/api/api-url.ts` (lines 12–19), `core/api/health-api.ts` (typed API service pattern), `core/api/problem-details.ts`.
21. `frontend/src/app/core/layout/navigation.ts` (lines 1–11), `core/layout/shell/shell.ts` (lines 29–35), `shell.html` (lines 12–24 nav loop, 29–36 toolbar), `shell.scss` (lines 21–32).
22. `frontend/src/app/features/home/home-page.ts` (lines 18–24 `describeError`, 33–51) and `home-page.html` (lines 1–3) — page/component pattern: `OnPush`, `inject()`, signals, `takeUntilDestroyed`, `crm-` selectors.
23. `frontend/src/app/shared/ui/page-header/page-header.ts`, `shared/ui/status-badge/status-badge.ts` (tones `success | error | pending | neutral`) — reuse in new pages.
24. `frontend/src/app/core/api/api-url.spec.ts` — Vitest + `TestBed` spec style (`describe`/`it`/`expect` globals).
25. `frontend/proxy.conf.json` — `/api` → `http://localhost:5080`. Cookies are set for the `localhost:4200` origin through this proxy; **no change** to this file.
26. `docs/api-conventions.md`, `docs/persistence-and-transactions.md`, `docs/frontend-ui-foundation.md`, `README.md` (lines 36–61 secrets/migrations, 84–97 tests/conventions) — documentation to extend.

---

## Backend Tasks

> Run commands from `backend/` unless stated. Every new handler is `public sealed` (the Mediator source generator ignores non-public handlers). Validators are `public sealed class <UseCase>Validator : AbstractValidator<…>` in a `<UseCase>Validator.cs` file next to the command (README line 26 convention).

### 1 — Packages

```bash
dotnet add src/Crm.Infrastructure package Microsoft.AspNetCore.Identity.EntityFrameworkCore --version 10.0.13
dotnet add src/Crm.Infrastructure package Microsoft.IdentityModel.JsonWebTokens --version 8.19.2
dotnet add src/Crm.Infrastructure package Microsoft.Extensions.Options.ConfigurationExtensions --version 10.0.13
dotnet add src/Crm.Api package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.13
```

**Do not** add packages to `Crm.Domain` or `Crm.Application` (verification step 2 checks this).

### 2 — Domain: roles and refresh session

**Create file: `backend/src/Crm.Domain/Identity/UserRoles.cs`**

```csharp
namespace Crm.Domain.Identity;

/// <summary>The only roles in the system. Server-assigned; never accepted from clients.</summary>
public static class UserRoles
{
    public const string SuperAdmin = "SuperAdmin";
    public const string Agent = "Agent";
    public const string Customer = "Customer";

    public static readonly IReadOnlyList<string> All = [SuperAdmin, Agent, Customer];
}
```

**Create file: `backend/src/Crm.Domain/Identity/RefreshSessionRevocationReason.cs`**

```csharp
namespace Crm.Domain.Identity;

public enum RefreshSessionRevocationReason
{
    Rotated,
    Logout,
    ReuseDetected,
    PasswordChanged,
    PasswordReset,
    AccountDisabled,
    AccountInvalid,
}
```

**Create file: `backend/src/Crm.Domain/Identity/RefreshSession.cs`**

```csharp
namespace Crm.Domain.Identity;

/// <summary>
/// One issued refresh token. Only the SHA-256 hash of the token is stored.
/// All rotations of one login share a FamilyId; reuse of a rotated token revokes the family.
/// </summary>
public sealed class RefreshSession
{
    private RefreshSession() { } // EF Core

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RevokedAtUtc { get; private set; }
    public RefreshSessionRevocationReason? RevocationReason { get; private set; }
    public Guid? ReplacedBySessionId { get; private set; }

    public static RefreshSession Create(
        Guid id, Guid userId, Guid familyId, string tokenHash, DateTimeOffset createdAtUtc, DateTimeOffset expiresAtUtc) =>
        new()
        {
            Id = id,
            UserId = userId,
            FamilyId = familyId,
            TokenHash = tokenHash,
            CreatedAtUtc = createdAtUtc,
            ExpiresAtUtc = expiresAtUtc,
        };
}
```

### 3 — Application: exceptions, abstractions, shared validation

**Create file: `backend/src/Crm.Application/Common/Exceptions/UnauthorizedException.cs`**

```csharp
namespace Crm.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 401 by the API. Message must never reveal whether an account exists.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);
```

**Create file: `backend/src/Crm.Application/Common/Exceptions/ConflictException.cs`**

```csharp
namespace Crm.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 409 by the API.</summary>
public sealed class ConflictException(string message) : Exception(message);
```

**Create file: `backend/src/Crm.Application/Abstractions/Identity/IIdentityService.cs`**

```csharp
namespace Crm.Application.Abstractions.Identity;

/// <summary>
/// Application-facing identity boundary. Implemented in Infrastructure over ASP.NET Core Identity.
/// Methods that write persist immediately on the request-scoped DbContext, so they join the
/// transaction opened by TransactionBehavior for ITransactionalCommand messages.
/// </summary>
public interface IIdentityService
{
    Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>Returns the account on success; null for unknown email, wrong password, locked out or disabled. Records failed attempts (lockout).</summary>
    Task<UserAccount?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken = default);

    Task<UserAccount> CreateUserAsync(NewUserAccount account, string password, string role, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserAccount>> ListByRoleAsync(string role, CancellationToken cancellationToken = default);
    Task<UserAccount> UpdateProfileAsync(Guid userId, UserProfileUpdate update, CancellationToken cancellationToken = default);

    /// <summary>False when the current password is wrong. Rotates the security stamp on success.</summary>
    Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Replaces the password without the old one (SuperAdmin reset). Rotates the security stamp.</summary>
    Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default);

    /// <summary>Sets the active flag and rotates the security stamp.</summary>
    Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Identity/UserAccount.cs`**

```csharp
namespace Crm.Application.Abstractions.Identity;

/// <summary>Read model of an account. Never returned to clients as-is (SecurityStamp is internal).</summary>
public sealed record UserAccount(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? PhoneNumber,
    string Role,
    bool IsActive,
    string SecurityStamp,
    DateTimeOffset CreatedAtUtc);

public sealed record NewUserAccount(string Email, string FirstName, string LastName, string? PhoneNumber);

public sealed record UserProfileUpdate(string FirstName, string LastName, string? PhoneNumber);
```

**Create file: `backend/src/Crm.Application/Abstractions/Identity/ITokenService.cs`**

```csharp
namespace Crm.Application.Abstractions.Identity;

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAtUtc);

public interface ITokenService
{
    AccessToken CreateAccessToken(UserAccount user, DateTimeOffset now);
    TimeSpan RefreshTokenLifetime { get; }

    /// <summary>How long after rotation a re-presented token counts as a concurrent tab, not replay. Zero disables.</summary>
    TimeSpan RefreshTokenReuseGracePeriod { get; }
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IRefreshSessionRepository.cs`**

```csharp
using Crm.Domain.Identity;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Revoke* methods execute immediately (set-based UPDATE) inside the current transaction, if any.
/// Add only stages; the caller saves through IUnitOfWork.
/// </summary>
public interface IRefreshSessionRepository
{
    Task<RefreshSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
    void Add(RefreshSession session);

    /// <summary>Revokes one session only if it is still unrevoked. False = someone else revoked/rotated it first.</summary>
    Task<bool> TryRevokeAsync(Guid sessionId, RefreshSessionRevocationReason reason, Guid? replacedBySessionId, DateTimeOffset now, CancellationToken cancellationToken = default);

    /// <summary>True when the family still has an unrevoked, unexpired session (the login is alive).</summary>
    Task<bool> AnyActiveInFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default);

    Task<int> RevokeFamilyAsync(Guid familyId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default);
    Task<int> RevokeAllForUserAsync(Guid userId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Common/Validation/AccountRules.cs`**

```csharp
using FluentValidation;

namespace Crm.Application.Common.Validation;

public static class AccountRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 32;

    /// <summary>Min 8, max 128, no composition rules (intake section 4).</summary>
    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(PasswordMinLength).MaximumLength(PasswordMaxLength);

    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(NameMaxLength);

    public static IRuleBuilderOptions<T, string> AccountEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().EmailAddress().MaximumLength(EmailMaxLength);

    public static IRuleBuilderOptions<T, string?> OptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(PhoneMaxLength);

    /// <summary>Trimmed value, or null when empty.</summary>
    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
```

### 4 — Application: auth session building blocks

**Create file: `backend/src/Crm.Application/Auth/RefreshTokens.cs`**

```csharp
using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Crm.Application.Auth;

/// <summary>Opaque refresh tokens: 256 random bits; only the SHA-256 hash is ever persisted.</summary>
public static class RefreshTokens
{
    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
```

**Create file: `backend/src/Crm.Application/Auth/AuthSession.cs`**

```csharp
using Crm.Application.Abstractions.Identity;

namespace Crm.Application.Auth;

/// <summary>Result of login/renewal. RefreshToken is plaintext only in memory, for the cookie.</summary>
public sealed record AuthSession(
    AccessToken AccessToken,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAtUtc,
    UserAccount User);
```

**Create file: `backend/src/Crm.Application/Auth/AuthSessionFactory.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;

namespace Crm.Application.Auth;

/// <summary>Stages a new refresh session (hash only) and creates the access token. Caller saves.</summary>
public sealed class AuthSessionFactory(ITokenService tokenService, IRefreshSessionRepository sessions)
{
    public AuthSession Issue(UserAccount user, Guid familyId, DateTimeOffset now, Guid? sessionId = null)
    {
        var refreshToken = RefreshTokens.Generate();
        var expiresAtUtc = now.Add(tokenService.RefreshTokenLifetime);

        sessions.Add(RefreshSession.Create(
            sessionId ?? Guid.CreateVersion7(), user.Id, familyId, RefreshTokens.Hash(refreshToken), now, expiresAtUtc));

        return new AuthSession(tokenService.CreateAccessToken(user, now), refreshToken, expiresAtUtc, user);
    }
}
```

**File: `backend/src/Crm.Application/DependencyInjection.cs`** — after line 12 (`TryAddSingleton(TimeProvider.System)`) add:

```csharp
        services.AddScoped<Auth.AuthSessionFactory>();
```

### 5 — Application: Auth use cases

**Create file: `backend/src/Crm.Application/Auth/RegisterCustomer/RegisterCustomerCommand.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Auth.RegisterCustomer;

/// <summary>Public signup. Role is always Customer — there is no role input.</summary>
public sealed record RegisterCustomerCommand(string FirstName, string LastName, string Email, string Password, string? PhoneNumber)
    : ICommand<UserAccount>, ITransactionalCommand; // create user + add role = two saves

public sealed class RegisterCustomerCommandHandler(IIdentityService identity)
    : ICommandHandler<RegisterCustomerCommand, UserAccount>
{
    public async ValueTask<UserAccount> Handle(RegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim();
        if (await identity.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        return await identity.CreateUserAsync(
            new NewUserAccount(email, command.FirstName.Trim(), command.LastName.Trim(), AccountRules.NormalizeOptional(command.PhoneNumber)),
            command.Password,
            UserRoles.Customer,
            cancellationToken);
    }
}
```

**Create file: `backend/src/Crm.Application/Auth/RegisterCustomer/RegisterCustomerValidator.cs`** — rules: `FirstName` `PersonName()`, `LastName` `PersonName()`, `Email` `AccountEmail()`, `Password` `NewPassword()`, `PhoneNumber` `OptionalPhone()`.

**Create file: `backend/src/Crm.Application/Auth/Login/LoginCommand.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Auth.Login;

/// <summary>
/// NOT ITransactionalCommand on purpose: a failed attempt throws 401, and the failed-attempt
/// counter written by CheckCredentialsAsync must survive (a transaction would roll it back).
/// </summary>
public sealed record LoginCommand(string Email, string Password) : ICommand<AuthSession>;

public sealed class LoginCommandHandler(
    IIdentityService identity,
    AuthSessionFactory sessionFactory,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<LoginCommand, AuthSession>
{
    public const string InvalidCredentials = "Invalid email or password.";

    public async ValueTask<AuthSession> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await identity.CheckCredentialsAsync(command.Email.Trim(), command.Password, cancellationToken)
            ?? throw new UnauthorizedException(InvalidCredentials);

        var session = sessionFactory.Issue(user, familyId: Guid.CreateVersion7(), timeProvider.GetUtcNow());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return session;
    }
}
```

**Create file: `backend/src/Crm.Application/Auth/Login/LoginValidator.cs`** — `Email` `NotEmpty().MaximumLength(AccountRules.EmailMaxLength)`, `Password` `NotEmpty().MaximumLength(AccountRules.PasswordMaxLength)`. **No** min-length rule on login (would leak policy detail and is irrelevant for verification).

**Create file: `backend/src/Crm.Application/Auth/RenewSession/RenewSessionCommand.cs`** (folder is `RenewSession`, not `RefreshSession`, to avoid a namespace/type clash with `Crm.Domain.Identity.RefreshSession`)

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Identity;
using Mediator;

namespace Crm.Application.Auth.RenewSession;

/// <summary>
/// Rotates a refresh token. Transactional (revoke old + insert new are atomic).
/// Rejections are RETURNED, not thrown, so that reuse-detection revocations still commit.
/// A token rotated within the grace period is a concurrent tab, not replay: it gets a sibling session.
/// </summary>
public sealed record RenewSessionCommand(string RefreshToken) : ICommand<RenewSessionResult>, ITransactionalCommand;

public sealed record RenewSessionResult(AuthSession? Session)
{
    public static readonly RenewSessionResult Rejected = new((AuthSession?)null);
}

public sealed class RenewSessionCommandHandler(
    IRefreshSessionRepository sessions,
    IIdentityService identity,
    AuthSessionFactory sessionFactory,
    ITokenService tokenService,
    TimeProvider timeProvider) : ICommandHandler<RenewSessionCommand, RenewSessionResult>
{
    public async ValueTask<RenewSessionResult> Handle(RenewSessionCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return RenewSessionResult.Rejected;
        }

        var now = timeProvider.GetUtcNow();
        var tokenHash = RefreshTokens.Hash(command.RefreshToken);
        var current = await sessions.FindByTokenHashAsync(tokenHash, cancellationToken);
        if (current is null || current.ExpiresAtUtc <= now)
        {
            return RenewSessionResult.Rejected;
        }

        if (current.RevokedAtUtc is not null)
        {
            return await HandleRevokedAsync(current, now, cancellationToken);
        }

        var user = await ActiveUserOrRevokeFamilyAsync(current, now, cancellationToken);
        if (user is null)
        {
            return RenewSessionResult.Rejected;
        }

        var nextSessionId = Guid.CreateVersion7();
        if (await sessions.TryRevokeAsync(current.Id, RefreshSessionRevocationReason.Rotated, nextSessionId, now, cancellationToken))
        {
            return new RenewSessionResult(sessionFactory.Issue(user, current.FamilyId, now, nextSessionId));
        }

        // Lost the row-level race to another request with the same token (it has now committed):
        // re-read and apply the same grace rule instead of assuming theft.
        var winner = await sessions.FindByTokenHashAsync(tokenHash, cancellationToken);
        return winner is null ? RenewSessionResult.Rejected : await HandleRevokedAsync(winner, now, cancellationToken);
    }

    private async Task<RenewSessionResult> HandleRevokedAsync(RefreshSession presented, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (presented.RevocationReason != RefreshSessionRevocationReason.Rotated)
        {
            return RenewSessionResult.Rejected; // logout, password change, disable, reuse: already dead
        }

        var rotatedRecently = presented.RevokedAtUtc >= now - tokenService.RefreshTokenReuseGracePeriod;
        if (rotatedRecently && await sessions.AnyActiveInFamilyAsync(presented.FamilyId, now, cancellationToken))
        {
            // Benign race: another tab of the same login rotated this token moments ago.
            // Issue a sibling session in the same family; the old token stays revoked.
            var user = await ActiveUserOrRevokeFamilyAsync(presented, now, cancellationToken);
            return user is null
                ? RenewSessionResult.Rejected
                : new RenewSessionResult(sessionFactory.Issue(user, presented.FamilyId, now));
        }

        if (!rotatedRecently)
        {
            // A rotated token replayed after the grace window: assume theft, kill the family.
            await sessions.RevokeFamilyAsync(presented.FamilyId, RefreshSessionRevocationReason.ReuseDetected, now, cancellationToken);
        }
        return RenewSessionResult.Rejected;
    }

    private async Task<UserAccount?> ActiveUserOrRevokeFamilyAsync(RefreshSession session, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var user = await identity.FindByIdAsync(session.UserId, cancellationToken);
        if (user is { IsActive: true })
        {
            return user;
        }

        await sessions.RevokeFamilyAsync(session.FamilyId, RefreshSessionRevocationReason.AccountInvalid, now, cancellationToken);
        return null;
    }
}
```

Rules encoded above (the executor must not change them):
- **Grace applies only to `Rotated` rows.** Tokens revoked by logout, password change/reset, disable or reuse detection are never revived.
- **Grace requires a live family** (`AnyActiveInFamilyAsync`). After logout or a family revocation, a recently rotated token is simply rejected.
- **Siblings are not linked** through `ReplacedBySessionId`; they are ordinary sessions of the same family, revoked together by logout, reuse detection, password change/reset and disable.
- **Replay after the window** (`RevokedAtUtc < now - grace`) revokes the family exactly as before.
- With `RefreshTokenReuseGracePeriod = 00:00:00` the handler behaves as strict rotation (every re-presented rotated token after the instant of rotation is replay).

No validator for `RenewSessionCommand` (an empty token must yield 401, not 400).

**Create file: `backend/src/Crm.Application/Auth/Logout/LogoutCommand.cs`**

```csharp
public sealed record LogoutCommand(string? RefreshToken) : ICommand;

public sealed class LogoutCommandHandler(IRefreshSessionRepository sessions, TimeProvider timeProvider)
    : ICommandHandler<LogoutCommand>
{
    public async ValueTask<Unit> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.RefreshToken))
        {
            return Unit.Value;
        }

        // Revoke the whole login (family), including sibling sessions created by concurrent tabs.
        var session = await sessions.FindByTokenHashAsync(RefreshTokens.Hash(command.RefreshToken), cancellationToken);
        if (session is not null)
        {
            await sessions.RevokeFamilyAsync(session.FamilyId, RefreshSessionRevocationReason.Logout, timeProvider.GetUtcNow(), cancellationToken);
        }
        return Unit.Value;
    }
}
```

(namespace `Crm.Application.Auth.Logout`; usings: `Crm.Application.Abstractions.Persistence`, `Crm.Domain.Identity`, `Mediator`.)

### 6 — Application: Profile (self-service) use cases

All take `Guid UserId` supplied by the controller from the validated `sub` claim — the client never sends a user id, which is the ownership guarantee.

| File (under `backend/src/Crm.Application/Profile/`) | Message | Behaviour |
|---|---|---|
| `GetMyProfile/GetMyProfileQuery.cs` | `GetMyProfileQuery(Guid UserId) : IQuery<UserAccount>` | `identity.FindByIdAsync` ?? throw `NotFoundException("Account not found.")`. |
| `UpdateMyProfile/UpdateMyProfileCommand.cs` + `UpdateMyProfileValidator.cs` | `UpdateMyProfileCommand(Guid UserId, string FirstName, string LastName, string? PhoneNumber) : ICommand<UserAccount>` (single save — **not** transactional) | `identity.UpdateProfileAsync(UserId, new UserProfileUpdate(FirstName.Trim(), LastName.Trim(), AccountRules.NormalizeOptional(PhoneNumber)))`. Validator: `PersonName()` ×2, `OptionalPhone()`. |
| `ChangeMyPassword/ChangeMyPasswordCommand.cs` + `ChangeMyPasswordValidator.cs` | `ChangeMyPasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : ICommand, ITransactionalCommand` | See below. Validator: `CurrentPassword` `NotEmpty()`, `NewPassword` `NewPassword()`. |

`ChangeMyPasswordCommandHandler(IIdentityService identity, IRefreshSessionRepository sessions, TimeProvider timeProvider)`:

```csharp
    public async ValueTask<Unit> Handle(ChangeMyPasswordCommand command, CancellationToken cancellationToken)
    {
        if (!await identity.ChangePasswordAsync(command.UserId, command.CurrentPassword, command.NewPassword, cancellationToken))
        {
            throw new ValidationException(
                [new ValidationFailure(nameof(ChangeMyPasswordCommand.CurrentPassword), "Current password is incorrect.")]);
        }

        await sessions.RevokeAllForUserAsync(command.UserId, RefreshSessionRevocationReason.PasswordChanged, timeProvider.GetUtcNow(), cancellationToken);
        return Unit.Value;
    }
```

(`using FluentValidation; using FluentValidation.Results;`.) `GlobalExceptionHandler` camel-cases the key to `currentPassword`.

### 7 — Application: Agents (SuperAdmin) use cases

**Create file: `backend/src/Crm.Application/Agents/AgentLookup.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Identity;

namespace Crm.Application.Agents;

internal static class AgentLookup
{
    /// <summary>Only Agent accounts are reachable through /agents; SuperAdmin/Customer ids yield 404.</summary>
    public static async Task<UserAccount> GetAgentOrThrowAsync(this IIdentityService identity, Guid id, CancellationToken cancellationToken)
    {
        var user = await identity.FindByIdAsync(id, cancellationToken);
        return user is { Role: UserRoles.Agent } ? user : throw new NotFoundException($"Agent '{id}' was not found.");
    }
}
```

| File (under `backend/src/Crm.Application/Agents/`) | Message | Behaviour |
|---|---|---|
| `ListAgents/ListAgentsQuery.cs` | `ListAgentsQuery : IQuery<IReadOnlyList<UserAccount>>` | `identity.ListByRoleAsync(UserRoles.Agent)` (active and disabled). No pagination (deferred per `docs/api-conventions.md` line 17). |
| `GetAgent/GetAgentQuery.cs` | `GetAgentQuery(Guid Id) : IQuery<UserAccount>` | `GetAgentOrThrowAsync`. |
| `CreateAgent/CreateAgentCommand.cs` + `CreateAgentValidator.cs` | `CreateAgentCommand(string FirstName, string LastName, string Email, string? PhoneNumber, string Password) : ICommand<UserAccount>, ITransactionalCommand` | Same shape as `RegisterCustomerCommandHandler` but role `UserRoles.Agent`. Validator identical rule set. |
| `UpdateAgent/UpdateAgentCommand.cs` + `UpdateAgentValidator.cs` | `UpdateAgentCommand(Guid Id, string FirstName, string LastName, string? PhoneNumber) : ICommand<UserAccount>` | `GetAgentOrThrowAsync`, then `UpdateProfileAsync`. Email is **not** editable in this story. |
| `SetAgentStatus/SetAgentStatusCommand.cs` | `SetAgentStatusCommand(Guid Id, bool IsActive) : ICommand, ITransactionalCommand` | `GetAgentOrThrowAsync`; `SetActiveAsync(Id, IsActive)`; when `!IsActive` also `RevokeAllForUserAsync(Id, AccountDisabled, now)`. |
| `SetAgentPassword/SetAgentPasswordCommand.cs` + `SetAgentPasswordValidator.cs` | `SetAgentPasswordCommand(Guid Id, string NewPassword) : ICommand, ITransactionalCommand` | `GetAgentOrThrowAsync`; `SetPasswordAsync`; `RevokeAllForUserAsync(Id, PasswordReset, now)`. Validator: `NewPassword` `NewPassword()`. No forced change on next login. |

Authorization (SuperAdmin) is enforced in the API layer (task 13); handlers additionally restrict targets to Agent accounts via `AgentLookup`.

### 8 — Infrastructure: Identity model and DbContext

**Create file: `backend/src/Crm.Infrastructure/Identity/ApplicationUser.cs`**

```csharp
using Microsoft.AspNetCore.Identity;

namespace Crm.Infrastructure.Identity;

/// <summary>Identity persistence model. Never leaves Infrastructure. UserName == Email.</summary>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

Future Customer Management links CRM customer records to `ApplicationUser.Id` (stable `Guid`); no CRM customer table is created now.

**File: `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`** — replace lines 1–15 with:

```csharp
using Crm.Domain.Identity;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public const string ConnectionStringName = "CrmDatabase";

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's model first, so feature configurations can refine it.
        base.OnModelCreating(modelBuilder);
        // Feature stories add IEntityTypeConfiguration<T> classes under Persistence/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }
}
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/ApplicationUserConfiguration.cs`** — `IEntityTypeConfiguration<ApplicationUser>`: `FirstName` and `LastName` `.IsRequired().HasMaxLength(100)`; `PhoneNumber` `.HasMaxLength(32)`. Keep Identity's default table names (`AspNetUsers`, …).

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/RefreshSessionConfiguration.cs`**

```csharp
internal sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("RefreshSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.TokenHash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.HasIndex(s => s.TokenHash).IsUnique();
        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.FamilyId);
        builder.Property(s => s.RevocationReason).HasConversion<string>().HasMaxLength(32);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/RefreshSessionRepository.cs`** — `internal sealed class RefreshSessionRepository(CrmDbContext dbContext) : IRefreshSessionRepository`:

- `FindByTokenHashAsync`: `dbContext.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(s => s.TokenHash == tokenHash, ct)`.
- `Add`: `dbContext.RefreshSessions.Add(session)`.
- `TryRevokeAsync`: `Where(s => s.Id == sessionId && s.RevokedAtUtc == null).ExecuteUpdateAsync(set => set.SetProperty(s => s.RevokedAtUtc, (DateTimeOffset?)now).SetProperty(s => s.RevocationReason, (RefreshSessionRevocationReason?)reason).SetProperty(s => s.ReplacedBySessionId, replacedBySessionId), ct) == 1`.
- `AnyActiveInFamilyAsync`: `dbContext.RefreshSessions.AnyAsync(s => s.FamilyId == familyId && s.RevokedAtUtc == null && s.ExpiresAtUtc > now, ct)` (served by the `FamilyId` index).
- `RevokeFamilyAsync` / `RevokeAllForUserAsync`: same `ExecuteUpdateAsync`, filtered by `FamilyId` / `UserId` **and** `RevokedAtUtc == null`; return the affected row count. (Already-revoked rows keep their original reason and timestamp, so a `Rotated` row stays detectable and its grace window is measured from the real rotation time.)

### 9 — Infrastructure: options, token service, identity service

**Create file: `backend/src/Crm.Infrastructure/Identity/JwtOptions.cs`**

```csharp
using Microsoft.IdentityModel.Tokens;

namespace Crm.Infrastructure.Identity;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    /// <summary>Base64 of >= 32 random bytes. User secrets / environment only.</summary>
    public string SigningKey { get; set; } = string.Empty;
    public TimeSpan AccessTokenLifetime { get; set; } = TimeSpan.FromMinutes(10);
    public TimeSpan RefreshTokenLifetime { get; set; } = TimeSpan.FromDays(7);
    /// <summary>Concurrent-tab tolerance for re-presented rotated tokens. Zero = strict rotation.</summary>
    public TimeSpan RefreshTokenReuseGracePeriod { get; set; } = TimeSpan.FromSeconds(30);

    public SymmetricSecurityKey CreateSigningKey() => new(Convert.FromBase64String(SigningKey));

    internal bool HasValidSigningKey()
    {
        try { return Convert.FromBase64String(SigningKey).Length >= 32; }
        catch (FormatException) { return false; }
    }
}
```

**Create file: `backend/src/Crm.Infrastructure/Identity/BootstrapSuperAdminOptions.cs`** — `public sealed class BootstrapSuperAdminOptions { public const string SectionName = "Bootstrap:SuperAdmin"; public string Email { get; set; } = "admin@admin.com"; public string FirstName { get; set; } = "System"; public string LastName { get; set; } = "Administrator"; public string? Password { get; set; } }`.

**Create file: `backend/src/Crm.Infrastructure/Identity/CrmClaimTypes.cs`** — `public static class CrmClaimTypes { public const string Subject = "sub"; public const string Role = "role"; public const string SecurityStamp = "sstamp"; }`.

**Create file: `backend/src/Crm.Infrastructure/Identity/JwtTokenService.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Infrastructure.Identity;

internal sealed class JwtTokenService(IOptions<JwtOptions> options) : ITokenService
{
    private readonly JsonWebTokenHandler handler = new();

    public TimeSpan RefreshTokenLifetime => options.Value.RefreshTokenLifetime;

    public TimeSpan RefreshTokenReuseGracePeriod => options.Value.RefreshTokenReuseGracePeriod;

    public AccessToken CreateAccessToken(UserAccount user, DateTimeOffset now)
    {
        var jwt = options.Value;
        var expiresAtUtc = now.Add(jwt.AccessTokenLifetime);
        var token = handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAtUtc.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [CrmClaimTypes.Subject] = user.Id.ToString(),
                [CrmClaimTypes.Role] = user.Role,
                [CrmClaimTypes.SecurityStamp] = user.SecurityStamp,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
            SigningCredentials = new SigningCredentials(jwt.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });
        return new AccessToken(token, expiresAtUtc);
    }
}
```

**Create file: `backend/src/Crm.Infrastructure/Identity/IdentityService.cs`** — `internal sealed class IdentityService(UserManager<ApplicationUser> userManager, CrmDbContext dbContext, TimeProvider timeProvider) : IIdentityService`. Implementation rules:

- **Projection used by `FindByIdAsync` and `ListByRoleAsync`** (one query, no tracking, no Identity types leak):

  ```csharp
  private IQueryable<UserAccount> Accounts() =>
      from u in dbContext.Users.AsNoTracking()
      let role = (from ur in dbContext.UserRoles
                  join r in dbContext.Roles on ur.RoleId equals r.Id
                  where ur.UserId == u.Id
                  select r.Name).FirstOrDefault()
      select new UserAccount(u.Id, u.Email!, u.FirstName, u.LastName, u.PhoneNumber, role ?? string.Empty, u.IsActive, u.SecurityStamp!, u.CreatedAtUtc);
  ```

  `FindByIdAsync` → `Accounts().SingleOrDefaultAsync(a => a.Id == userId, ct)`; `ListByRoleAsync` → `Accounts().Where(a => a.Role == role).OrderBy(a => a.LastName).ThenBy(a => a.FirstName).ToListAsync(ct)`.
- `EmailExistsAsync` → `await userManager.FindByEmailAsync(email) is not null`.
- `CheckCredentialsAsync`:
  1. `user = await userManager.FindByEmailAsync(email)`; if null → run `userManager.PasswordHasher.VerifyHashedPassword(DummyUser, DummyHash, password)` (static `Lazy<string>` hash of a random string created once) to equalise timing; return null.
  2. If `!user.IsActive` or `await userManager.IsLockedOutAsync(user)` → return null.
  3. If `await userManager.CheckPasswordAsync(user, password)` → `await userManager.ResetAccessFailedCountAsync(user)`; return `await FindByIdAsync(user.Id, ct)`.
  4. Else `await userManager.AccessFailedAsync(user)`; return null.
- `CreateUserAsync`: `new ApplicationUser { Id = Guid.CreateVersion7(), UserName = account.Email, Email = account.Email, FirstName, LastName, PhoneNumber, IsActive = true, CreatedAtUtc = timeProvider.GetUtcNow() }`; `ThrowIfFailed(await userManager.CreateAsync(user, password))`; `ThrowIfFailed(await userManager.AddToRoleAsync(user, role))`; return `(await FindByIdAsync(user.Id, ct))!`.
- `UpdateProfileAsync`: load with `userManager.FindByIdAsync(userId.ToString())` ?? throw `NotFoundException`; set the three fields; `ThrowIfFailed(await userManager.UpdateAsync(user))`; return projection.
- `ChangePasswordAsync`: `var result = await userManager.ChangePasswordAsync(user, current, next)`; if `result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.PasswordMismatch))` return false; else `ThrowIfFailed(result)`; return true.
- `SetPasswordAsync`: `if (await userManager.HasPasswordAsync(user)) ThrowIfFailed(await userManager.RemovePasswordAsync(user));` then `ThrowIfFailed(await userManager.AddPasswordAsync(user, newPassword))` (both rotate the stamp; the command is transactional so a failure restores the old hash).
- `SetActiveAsync`: `user.IsActive = isActive; ThrowIfFailed(await userManager.UpdateSecurityStampAsync(user));` (persists both).
- `private static void ThrowIfFailed(IdentityResult result)`: if `Succeeded` return; if any code is `DuplicateEmail`/`DuplicateUserName` → `throw new ConflictException("An account with this email already exists.")`; else throw FluentValidation `ValidationException` with one `ValidationFailure` per error, property `"password"` for codes starting with `Password`, `"email"` for codes containing `Email` or `UserName`, otherwise `""`. Never include submitted values.

**Create file: `backend/src/Crm.Infrastructure/Identity/IdentityBootstrapper.cs`** — `internal sealed class IdentityBootstrapper(CrmDbContext dbContext, RoleManager<IdentityRole<Guid>> roleManager, UserManager<ApplicationUser> userManager, IUnitOfWork unitOfWork, IOptions<BootstrapSuperAdminOptions> options, TimeProvider timeProvider, ILogger<IdentityBootstrapper> logger)` with `public async Task RunAsync(CancellationToken ct)`:

```csharp
        var pending = (await dbContext.Database.GetPendingMigrationsAsync(ct)).ToList();
        if (pending.Count > 0)
        {
            logger.LogWarning("Identity bootstrap skipped: {Count} pending migration(s). Run 'dotnet ef database update' (README) and restart.", pending.Count);
            return;
        }

        foreach (var role in UserRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roleManager.CreateAsync(new IdentityRole<Guid>(role)), $"create role {role}");
            }
        }

        var admin = options.Value;
        var existing = await userManager.FindByEmailAsync(admin.Email);
        if (existing is not null)
        {
            if (!await userManager.IsInRoleAsync(existing, UserRoles.SuperAdmin))
            {
                logger.LogError("Bootstrap configuration issue: the configured SuperAdmin email belongs to an account without the SuperAdmin role. It was NOT promoted; resolve manually.");
            }
            return; // never recreate, never reset the password
        }

        if (string.IsNullOrWhiteSpace(admin.Password))
        {
            throw new InvalidOperationException(
                "The initial SuperAdmin account does not exist and 'Bootstrap:SuperAdmin:Password' is not configured. " +
                "Run: dotnet user-secrets set \"Bootstrap:SuperAdmin:Password\" \"<initial-superadmin-password>\" --project src/Crm.Api (see README), then restart.");
        }

        await unitOfWork.ExecuteInTransactionAsync(async _ =>
        {
            var user = new ApplicationUser
            {
                Id = Guid.CreateVersion7(), UserName = admin.Email, Email = admin.Email,
                FirstName = admin.FirstName, LastName = admin.LastName, IsActive = true, CreatedAtUtc = timeProvider.GetUtcNow(),
            };
            EnsureSucceeded(await userManager.CreateAsync(user, admin.Password), "create the SuperAdmin account");
            EnsureSucceeded(await userManager.AddToRoleAsync(user, UserRoles.SuperAdmin), "assign the SuperAdmin role");
            return true;
        }, ct);

        logger.LogInformation("Initial SuperAdmin account provisioned.");
```

`EnsureSucceeded(IdentityResult, string action)` throws `InvalidOperationException($"Identity bootstrap could not {action}: {string.Join("; ", result.Errors.Select(e => e.Description))}")` (descriptions never contain the password).

**Create file: `backend/src/Crm.Infrastructure/Identity/IdentityBootstrapExtensions.cs`**

```csharp
public static class IdentityBootstrapExtensions
{
    /// <summary>Call after builder.Build(). EF design-time tools stop at Build(), so this never runs during 'dotnet ef'.</summary>
    public static async Task BootstrapIdentityAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityBootstrapper>().RunAsync(cancellationToken);
    }
}
```

### 10 — Infrastructure: registration

**File: `backend/src/Crm.Infrastructure/DependencyInjection.cs`** — after line 31 (`services.AddScoped<IUnitOfWork, EfUnitOfWork>();`) insert:

```csharp
        services.AddScoped<IRefreshSessionRepository, RefreshSessionRepository>();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty; // UserName == email; allow any email characters
                options.Password.RequiredLength = AccountRules.PasswordMinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<CrmDbContext>();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IdentityBootstrapper>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.HasValidSigningKey(),
                "Jwt:SigningKey must be Base64 of at least 32 random bytes. Set it with 'dotnet user-secrets' (see README).")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience are required.")
            .Validate(o => o.AccessTokenLifetime > TimeSpan.Zero && o.RefreshTokenLifetime > o.AccessTokenLifetime,
                "Jwt lifetimes must be positive and RefreshTokenLifetime must exceed AccessTokenLifetime.")
            .Validate(o => o.RefreshTokenReuseGracePeriod >= TimeSpan.Zero && o.RefreshTokenReuseGracePeriod <= TimeSpan.FromMinutes(2),
                "Jwt:RefreshTokenReuseGracePeriod must be between 00:00:00 and 00:02:00.")
            .ValidateOnStart();

        services.AddOptions<BootstrapSuperAdminOptions>()
            .Bind(configuration.GetSection(BootstrapSuperAdminOptions.SectionName));
```

Add usings: `Crm.Application.Abstractions.Identity`, `Crm.Application.Common.Validation`, `Crm.Infrastructure.Identity`, `Crm.Infrastructure.Persistence.Repositories`, `Microsoft.AspNetCore.Identity`.

### 11 — Configuration

**File: `backend/src/Crm.Api/appsettings.json`** — after line 12 (`"AllowedHosts": "*"`) add (comma on line 12):

```json
  "Jwt": {
    "Issuer": "Crm.Api",
    "Audience": "Crm.Web",
    "AccessTokenLifetime": "00:10:00",
    "RefreshTokenLifetime": "7.00:00:00",
    "RefreshTokenReuseGracePeriod": "00:00:30"
  },
  "Bootstrap": {
    "SuperAdmin": {
      "Email": "admin@admin.com",
      "FirstName": "System",
      "LastName": "Administrator"
    }
  }
```

**No** `SigningKey` or `Password` in any appsettings file. Set them locally (PowerShell, from `backend/`):

```powershell
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64))) --project src/Crm.Api
dotnet user-secrets set "Bootstrap:SuperAdmin:Password" "<initial-superadmin-password>" --project src/Crm.Api
```

The executor **asks the user** to run the second command with the real password; it must not appear in this session's commands or files.

### 12 — API: security registration

**Create file: `backend/src/Crm.Api/Authentication/AuthorizationPolicies.cs`** — `public static class AuthorizationPolicies { public const string SuperAdmin = "SuperAdmin"; }` and `public static class RateLimitPolicies { public const string Credentials = "credentials"; }`.

**Create file: `backend/src/Crm.Api/Authentication/ClaimsPrincipalExtensions.cs`**

```csharp
public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(CrmClaimTypes.Subject), out var id)
            ? id
            : throw new UnauthorizedException("The access token has no valid subject.");
}
```

(usings: `System.Security.Claims`, `Crm.Application.Common.Exceptions`, `Crm.Infrastructure.Identity`; namespace `Crm.Api.Authentication`. Controllers using `[EnableRateLimiting]` also need `using Microsoft.AspNetCore.RateLimiting;`.)

**Create file: `backend/src/Crm.Api/Authentication/SecurityRegistration.cs`**

```csharp
using System.Security.Claims;
using System.Threading.RateLimiting;
using Crm.Application.Abstractions.Identity;
using Crm.Domain.Identity;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Crm.Api.Authentication;

internal static class SecurityRegistration
{
    public static IServiceCollection AddSecurity(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;
                bearer.MapInboundClaims = false; // keep "sub" / "role" as issued
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.CreateSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = CrmClaimTypes.Subject,
                    RoleClaimType = CrmClaimTypes.Role,
                };
                bearer.Events = new JwtBearerEvents
                {
                    // Server-side account-state check: disable / password change / reset revoke JWTs immediately.
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal!;
                        var stamp = principal.FindFirstValue(CrmClaimTypes.SecurityStamp);
                        if (!Guid.TryParse(principal.FindFirstValue(CrmClaimTypes.Subject), out var userId) || stamp is null)
                        {
                            context.Fail("Invalid token claims.");
                            return;
                        }

                        var identity = context.HttpContext.RequestServices.GetRequiredService<IIdentityService>();
                        var user = await identity.FindByIdAsync(userId, context.HttpContext.RequestAborted);
                        if (user is null || !user.IsActive || !string.Equals(user.SecurityStamp, stamp, StringComparison.Ordinal))
                        {
                            context.Fail("Account state changed.");
                        }
                    },
                };
            });

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build()) // auth by default
            .AddPolicy(AuthorizationPolicies.SuperAdmin, policy => policy.RequireRole(UserRoles.SuperAdmin));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Credentials, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        return services;
    }
}
```

**Create file: `backend/src/Crm.Api/Authentication/AuthCookies.cs`**

```csharp
using System.Buffers.Text;
using System.Security.Cryptography;

namespace Crm.Api.Authentication;

/// <summary>Refresh cookie (HttpOnly, auth path only) + double-submit CSRF cookie (readable by Angular).</summary>
internal static class AuthCookies
{
    public const string RefreshCookieName = "crm_refresh";
    public const string CsrfCookieName = "XSRF-TOKEN";
    public const string CsrfHeaderName = "X-XSRF-TOKEN";
    private const string RefreshCookiePath = "/" + ApiRoutes.V1 + "/auth";

    public static void Issue(HttpResponse response, string refreshToken, DateTimeOffset expiresAtUtc)
    {
        response.Cookies.Append(RefreshCookieName, refreshToken, Options(RefreshCookiePath, httpOnly: true, expiresAtUtc));
        response.Cookies.Append(CsrfCookieName, Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32)), Options("/", httpOnly: false, expiresAtUtc));
    }

    public static void Clear(HttpResponse response)
    {
        response.Cookies.Delete(RefreshCookieName, Options(RefreshCookiePath, httpOnly: true, expires: null));
        response.Cookies.Delete(CsrfCookieName, Options("/", httpOnly: false, expires: null));
    }

    private static CookieOptions Options(string path, bool httpOnly, DateTimeOffset? expires) => new()
    {
        Path = path,
        HttpOnly = httpOnly,
        Secure = true,               // always; browsers accept Secure cookies on http://localhost
        SameSite = SameSiteMode.Strict,
        Expires = expires,
        IsEssential = true,
    };
}
```

**Create file: `backend/src/Crm.Api/Authentication/ValidateCsrfAttribute.cs`**

```csharp
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Crm.Api.Authentication;

/// <summary>Double-submit check for cookie-authenticated endpoints (refresh, logout). 403 on mismatch.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ValidateCsrfAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var request = context.HttpContext.Request;
        var cookie = request.Cookies[AuthCookies.CsrfCookieName];
        var header = request.Headers[AuthCookies.CsrfHeaderName].ToString();
        if (string.IsNullOrEmpty(cookie) || string.IsNullOrEmpty(header) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(cookie), Encoding.UTF8.GetBytes(header)))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status403Forbidden); // UseStatusCodePages adds ProblemDetails
        }
    }
}
```

**File: `backend/src/Crm.Api/DependencyInjection.cs`**:
- Line 41 → after the `AddMediator` block, before `return services;` (line 42): add `services.AddSecurity();` and `using Crm.Api.Authentication;`.
- Lines 20–28 (OpenAPI transformer) → append `" Auth: Bearer JWT from POST /api/v1/auth/login; anonymous only where stated."` to `Description`, and before `return Task.CompletedTask;` add:

  ```csharp
              document.Components ??= new OpenApiComponents();
              document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
              document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
              {
                  Type = SecuritySchemeType.Http,
                  Scheme = "bearer",
                  BearerFormat = "JWT",
                  Description = "Access token from /api/v1/auth/login or /api/v1/auth/refresh.",
              };
  ```

  with `using Microsoft.OpenApi;`.

**File: `backend/src/Crm.Api/Errors/GlobalExceptionHandler.cs`** — after line 41 (end of the `DomainException` case) insert:

```csharp
            UnauthorizedException unauthorized => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication failed.",
                Detail = unauthorized.Message,
            },
            ConflictException conflict => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict.",
                Detail = conflict.Message,
            },
```

(`Crm.Application.Common.Exceptions` is already imported at line 2.)

**File: `backend/src/Crm.Api/Controllers/HealthController.cs`** — add `[AllowAnonymous]` between lines 8 and 9 and `using Microsoft.AspNetCore.Authorization;`.

**File: `backend/src/Crm.Api/Program.cs`** — replace lines 13–30 with:

```csharp
var app = builder.Build();

// Roles + initial SuperAdmin (idempotent). Skipped with a warning while migrations are pending.
await app.Services.BootstrapIdentityAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();              // /openapi/v1.json
    app.MapScalarApiReference().AllowAnonymous();   // /scalar
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
```

Add `using Crm.Infrastructure.Identity;`.

### 13 — API: contracts and controllers

**Contracts** (PascalCase records; `Crm.Api/Contracts/<Feature>/`):

| File | Record |
|---|---|
| `Contracts/Auth/LoginRequest.cs` | `LoginRequest(string Email, string Password)` |
| `Contracts/Auth/RegisterCustomerRequest.cs` | `RegisterCustomerRequest(string FirstName, string LastName, string Email, string Password, string? PhoneNumber)` — **no role field** |
| `Contracts/Auth/AuthSessionResponse.cs` | `AuthSessionResponse(string AccessToken, DateTimeOffset AccessTokenExpiresAtUtc, CurrentUserResponse User)` + `static From(AuthSession s)` |
| `Contracts/Profile/CurrentUserResponse.cs` | `CurrentUserResponse(Guid Id, string Email, string FirstName, string LastName, string? PhoneNumber, string Role)` + `static From(UserAccount u)` |
| `Contracts/Profile/UpdateMyProfileRequest.cs` | `UpdateMyProfileRequest(string FirstName, string LastName, string? PhoneNumber)` |
| `Contracts/Profile/ChangePasswordRequest.cs` | `ChangePasswordRequest(string CurrentPassword, string NewPassword)` |
| `Contracts/Agents/AgentResponse.cs` | `AgentResponse(Guid Id, string Email, string FirstName, string LastName, string? PhoneNumber, bool IsActive, DateTimeOffset CreatedAtUtc)` + `static From(UserAccount u)` |
| `Contracts/Agents/CreateAgentRequest.cs` | `CreateAgentRequest(string FirstName, string LastName, string Email, string? PhoneNumber, string Password)` |
| `Contracts/Agents/UpdateAgentRequest.cs` | `UpdateAgentRequest(string FirstName, string LastName, string? PhoneNumber)` |
| `Contracts/Agents/SetAgentStatusRequest.cs` | `SetAgentStatusRequest(bool IsActive)` |
| `Contracts/Agents/SetAgentPasswordRequest.cs` | `SetAgentPasswordRequest(string NewPassword)` |

No response contains a password, hash, security stamp or refresh token.

**Create file: `backend/src/Crm.Api/Controllers/AuthController.cs`** — `[ApiController] [Route(ApiRoutes.V1 + "/auth")] public sealed class AuthController(ISender sender) : ControllerBase`. Every action `[AllowAnonymous]` and `[EndpointDescription("Anonymous. …")]`:

| Action | Attributes | Body |
|---|---|---|
| `Register(RegisterCustomerRequest request, CancellationToken)` → `ActionResult<CurrentUserResponse>` | `[HttpPost("register")]`, `[EnableRateLimiting(RateLimitPolicies.Credentials)]`, `[ProducesResponseType<CurrentUserResponse>(201)]` | send `RegisterCustomerCommand`; `return CreatedAtAction(nameof(MeController.Get), "Me", null, CurrentUserResponse.From(user));` |
| `Login(LoginRequest request, CancellationToken)` → `ActionResult<AuthSessionResponse>` | `[HttpPost("login")]`, `[EnableRateLimiting(RateLimitPolicies.Credentials)]` | send `LoginCommand`; `AuthCookies.Issue(Response, session.RefreshToken, session.RefreshTokenExpiresAtUtc)`; `return Ok(AuthSessionResponse.From(session));` |
| `Refresh(CancellationToken)` → `ActionResult<AuthSessionResponse>` | `[HttpPost("refresh")]`, `[ValidateCsrf]` | `var result = await sender.Send(new RenewSessionCommand(Request.Cookies[AuthCookies.RefreshCookieName] ?? string.Empty), ct);` if `result.Session is null` → `AuthCookies.Clear(Response); return Problem(statusCode: 401, title: "Session expired.", detail: "Sign in again.");` else `Issue` + `Ok(...)` |
| `Logout(CancellationToken)` → `IActionResult` | `[HttpPost("logout")]`, `[ValidateCsrf]` | send `LogoutCommand(Request.Cookies[AuthCookies.RefreshCookieName])`; `AuthCookies.Clear(Response)`; `return NoContent();` |

**Create file: `backend/src/Crm.Api/Controllers/MeController.cs`** — `[Route(ApiRoutes.V1 + "/me")] [Authorize]`, description "Requires: any authenticated user (own account only).":

- `[HttpGet] Get(ct)` → `Ok(CurrentUserResponse.From(await sender.Send(new GetMyProfileQuery(User.GetUserId()), ct)))`.
- `[HttpPut] Update(UpdateMyProfileRequest, ct)` → `Ok(CurrentUserResponse.From(...UpdateMyProfileCommand(User.GetUserId(), …)))`.
- `[HttpPost("password")] ChangePassword(ChangePasswordRequest, ct)` → send `ChangeMyPasswordCommand`; `AuthCookies.Clear(Response)`; `NoContent()`. (All sessions, including this one, are revoked; the client signs in again.)

**Create file: `backend/src/Crm.Api/Controllers/AgentsController.cs`** — `[Route(ApiRoutes.V1 + "/agents")] [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]`, every action `[EndpointDescription("Requires role: SuperAdmin.")]`:

- `[HttpGet] List` → `Ok(agents.Select(AgentResponse.From).ToList())`.
- `[HttpGet("{id:guid}")] Get(Guid id)` → `Ok(AgentResponse.From(...GetAgentQuery(id)))`.
- `[HttpPost] Create(CreateAgentRequest)` → `CreatedAtAction(nameof(Get), new { id = agent.Id }, AgentResponse.From(agent))`.
- `[HttpPut("{id:guid}")] Update(Guid id, UpdateAgentRequest)` → `Ok(...)`.
- `[HttpPut("{id:guid}/status")] SetStatus(Guid id, SetAgentStatusRequest)` → `NoContent()`.
- `[HttpPut("{id:guid}/password")] SetPassword(Guid id, SetAgentPasswordRequest)` → `NoContent()`.

Controllers stay thin: map → `sender.Send` → map (`docs/api-conventions.md` line 16).

### 14 — Migration

From `backend/` (with the SQL Server user secret already set):

```bash
dotnet ef migrations add AddIdentityAndRefreshSessions --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations -- --environment Development
```

Inspect the generated `…_AddIdentityAndRefreshSessions.cs`: `Up` must contain only `CreateTable` (`AspNetRoles`, `AspNetUsers`, `AspNetRoleClaims`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserRoles`, `AspNetUserTokens`, `RefreshSessions`) and `CreateIndex`; **no** `InsertData`, `Sql(`, `DropTable` or password values. Do not hand-edit it. Then apply:

```bash
dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development
```

Never use `EnsureCreated` or drop the database.

### 15 — Backend unit test (single happy path)

**Create file: `backend/tests/Crm.UnitTests/Auth/LoginCommandHandlerTests.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Auth;
using Crm.Application.Auth.Login;
using Crm.Domain.Identity;

namespace Crm.UnitTests.Auth;

public sealed class LoginCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCredentials_IssuesTokensAndPersistsOnlyRefreshTokenHash()
    {
        var customer = new UserAccount(Guid.NewGuid(), "customer@example.com", "Ada", "Lovelace", null,
            UserRoles.Customer, IsActive: true, SecurityStamp: "stamp", CreatedAtUtc: DateTimeOffset.UtcNow);
        var sessions = new FakeRefreshSessionRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new LoginCommandHandler(
            new FakeIdentityService(customer),
            new AuthSessionFactory(new FakeTokenService(), sessions),
            unitOfWork,
            TimeProvider.System);

        var session = await handler.Handle(new LoginCommand("customer@example.com", "correct horse"), CancellationToken.None);

        Assert.Equal("access-token", session.AccessToken.Token);
        Assert.Equal(UserRoles.Customer, session.User.Role);
        var stored = Assert.Single(sessions.Added);
        Assert.Equal(RefreshTokens.Hash(session.RefreshToken), stored.TokenHash);
        Assert.NotEqual(session.RefreshToken, stored.TokenHash);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class FakeIdentityService(UserAccount account) : IIdentityService
    {
        public Task<UserAccount?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAccount?>(account);

        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> CreateUserAsync(NewUserAccount a, string password, string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserAccount>> ListByRoleAsync(string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> UpdateProfileAsync(Guid userId, UserProfileUpdate update, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeTokenService : ITokenService
    {
        public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(7);
        public TimeSpan RefreshTokenReuseGracePeriod => TimeSpan.FromSeconds(30);
        public AccessToken CreateAccessToken(UserAccount user, DateTimeOffset now) => new("access-token", now.AddMinutes(10));
    }

    private sealed class FakeRefreshSessionRepository : IRefreshSessionRepository
    {
        public List<RefreshSession> Added { get; } = [];
        public void Add(RefreshSession session) => Added.Add(session);

        public Task<RefreshSession?> FindByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> TryRevokeAsync(Guid sessionId, RefreshSessionRevocationReason reason, Guid? replacedBySessionId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AnyActiveInFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RevokeFamilyAsync(Guid familyId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<int> RevokeAllForUserAsync(Guid userId, RefreshSessionRevocationReason reason, DateTimeOffset now, CancellationToken cancellationToken = default) => throw new NotSupportedException();
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

No new test packages or projects.

---

## Frontend Tasks

> Run commands from `frontend/`. Components: standalone, `ChangeDetectionStrategy.OnPush`, `crm-` selectors, `inject()`, signals, `NonNullableFormBuilder` reactive forms, Material components, CSS logical properties only (`docs/frontend-ui-foundation.md` line 10).

### 16 — Auth core

**Create file: `frontend/src/app/core/auth/auth-models.ts`**

```ts
export type UserRole = 'SuperAdmin' | 'Agent' | 'Customer';

/** Matches Crm.Api CurrentUserResponse. */
export interface CurrentUser {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  role: UserRole;
}

/** Matches Crm.Api AuthSessionResponse. The refresh token is never visible to JS (HttpOnly cookie). */
export interface AuthSessionResponse {
  accessToken: string;
  accessTokenExpiresAtUtc: string;
  user: CurrentUser;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterCustomerRequest {
  firstName: string;
  lastName: string;
  email: string;
  password: string;
  phoneNumber: string | null;
}
```

**Create file: `frontend/src/app/core/auth/auth-api.ts`** — `@Injectable({ providedIn: 'root' }) export class AuthApi` (same shape as `health-api.ts`), all URLs via `ApiUrl.versioned`:
- `login(body: LoginRequest)` → `POST auth/login` → `AuthSessionResponse`
- `register(body: RegisterCustomerRequest)` → `POST auth/register` → `CurrentUser`
- `refresh()` → `POST auth/refresh` (body `null`) → `AuthSessionResponse`
- `logout()` → `POST auth/logout` (body `null`) → `void`

**Create file: `frontend/src/app/core/auth/auth-service.ts`**

```ts
import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, finalize, firstValueFrom, map, of, shareReplay, switchMap, tap } from 'rxjs';
import { AuthApi } from './auth-api';
import { AuthSessionResponse, CurrentUser, LoginRequest, RegisterCustomerRequest } from './auth-models';

const CSRF_COOKIE = 'XSRF-TOKEN';

/** Session state. The access token lives only in this signal — never in localStorage/sessionStorage. */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly api = inject(AuthApi);
  private readonly router = inject(Router);
  private readonly document = inject(DOCUMENT);
  private readonly session = signal<AuthSessionResponse | null>(null);
  private refreshInFlight: Observable<boolean> | null = null;

  readonly currentUser = computed(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);
  readonly role = computed(() => this.session()?.user.role ?? null);

  accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  login(request: LoginRequest): Observable<CurrentUser> {
    return this.api.login(request).pipe(
      tap(session => this.session.set(session)),
      map(session => session.user),
    );
  }

  register(request: RegisterCustomerRequest): Observable<CurrentUser> {
    return this.api.register(request).pipe(
      switchMap(() => this.login({ email: request.email, password: request.password })),
    );
  }

  /** Single-flight: concurrent callers share one POST /auth/refresh. Emits false when the session is gone. */
  refresh(): Observable<boolean> {
    this.refreshInFlight ??= this.api.refresh().pipe(
      map(session => {
        this.session.set(session);
        return true;
      }),
      catchError(() => {
        this.session.set(null);
        return of(false);
      }),
      finalize(() => (this.refreshInFlight = null)),
      shareReplay(1),
    );
    return this.refreshInFlight;
  }

  /** App initializer: silently regain an access token after a reload. Never rejects. */
  restoreSession(): Promise<void> {
    const hasCsrfCookie = this.document.cookie.split('; ').some(c => c.startsWith(`${CSRF_COOKIE}=`));
    return hasCsrfCookie ? firstValueFrom(this.refresh()).then(() => undefined) : Promise.resolve();
  }

  updateCurrentUser(user: CurrentUser): void {
    this.session.update(session => (session ? { ...session, user } : session));
  }

  logout(): void {
    this.api.logout().pipe(catchError(() => of(null))).subscribe(() => this.endSession());
  }

  /** Drops the in-memory session and returns to sign-in. */
  endSession(): void {
    this.session.set(null);
    void this.router.navigateByUrl('/sign-in');
  }
}
```

**Create file: `frontend/src/app/core/auth/auth-interceptor.ts`**

```ts
import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { ApiUrl } from '../api/api-url';
import { AuthService } from './auth-service';

/** Adds the bearer token to /api/v1 calls; on 401 renews once via the refresh cookie and retries. */
export const authInterceptor: HttpInterceptorFn = (request, next) => {
  const auth = inject(AuthService);
  const apiUrl = inject(ApiUrl);
  const isBusinessApi = request.url.startsWith(apiUrl.versioned(''));
  const isSessionEndpoint = request.url.startsWith(apiUrl.versioned('auth/'));
  if (!isBusinessApi || isSessionEndpoint) {
    return next(request);
  }

  const withToken = (req: HttpRequest<unknown>) => {
    const token = auth.accessToken();
    return token ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : req;
  };

  return next(withToken(request)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse) || error.status !== 401) {
        return throwError(() => error);
      }
      return auth.refresh().pipe(
        switchMap(renewed => {
          if (!renewed) {
            auth.endSession();
            return throwError(() => error);
          }
          return next(withToken(request));
        }),
      );
    }),
  );
};
```

**Create file: `frontend/src/app/core/auth/auth-guards.ts`**

```ts
import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { UserRole } from './auth-models';
import { AuthService } from './auth-service';

/** Usability only — the API enforces every rule. */
export const authGuard: CanActivateFn = (_route, state) =>
  inject(AuthService).isAuthenticated() ||
  inject(Router).createUrlTree(['/sign-in'], { queryParams: { returnUrl: state.url } });

export const guestGuard: CanActivateFn = () =>
  !inject(AuthService).isAuthenticated() || inject(Router).createUrlTree(['/']);

export function roleGuard(...roles: UserRole[]): CanActivateFn {
  return () => {
    const role = inject(AuthService).role();
    return (role !== null && roles.includes(role)) || inject(Router).createUrlTree(['/']);
  };
}
```

**Create file: `frontend/src/app/core/api/describe-http-error.ts`** — `export function describeHttpError(error: HttpErrorResponse, fallback: string): string`: status `0` → "The API is not reachable. Start the backend and try again."; `429` → "Too many attempts. Wait a minute and try again."; otherwise `(error.error as ProblemDetails | null)?.detail ?? problem?.title ?? fallback`. Do not change `home-page.ts` `describeError` (lines 18–24).

**File: `frontend/src/app/app.config.ts`** — replace lines 1–14:

```ts
import { provideHttpClient, withFetch, withInterceptors, withXsrfConfiguration } from '@angular/common/http';
import { ApplicationConfig, inject, provideAppInitializer, provideBrowserGlobalErrorListeners } from '@angular/core';
import { provideRouter } from '@angular/router';
import { routes } from './app.routes';
import { provideApiConfig } from './core/api/api-config';
import { authInterceptor } from './core/auth/auth-interceptor';
import { AuthService } from './core/auth/auth-service';

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideRouter(routes),
    provideHttpClient(
      withFetch(),
      withInterceptors([authInterceptor]),
      // Must match Crm.Api AuthCookies (double-submit CSRF for refresh/logout).
      withXsrfConfiguration({ cookieName: 'XSRF-TOKEN', headerName: 'X-XSRF-TOKEN' }),
    ),
    provideApiConfig(),
    provideAppInitializer(() => inject(AuthService).restoreSession()),
  ]
};
```

### 17 — Routing and navigation

**File: `frontend/src/app/app.routes.ts`** — replace lines 1–14:

```ts
import { Routes } from '@angular/router';
import { authGuard, guestGuard, roleGuard } from './core/auth/auth-guards';
import { Shell } from './core/layout/shell/shell';

export const routes: Routes = [
  {
    path: 'sign-in',
    title: 'Sign in | Support CRM',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/sign-in-page').then(m => m.SignInPage),
  },
  {
    path: 'sign-up',
    title: 'Create account | Support CRM',
    canActivate: [guestGuard],
    loadComponent: () => import('./features/auth/sign-up-page').then(m => m.SignUpPage),
  },
  {
    path: '',
    component: Shell,
    canActivate: [authGuard],
    children: [
      { path: '', loadChildren: () => import('./features/home/home.routes').then(m => m.HOME_ROUTES) },
      { path: 'profile', loadChildren: () => import('./features/profile/profile.routes').then(m => m.PROFILE_ROUTES) },
      {
        path: 'agents',
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/agents/agents.routes').then(m => m.AGENTS_ROUTES),
      },
      // Feature stories add: { path: '<feature>', loadChildren: () => import('./features/<feature>/<feature>.routes') ... }
    ],
  },
  { path: '**', redirectTo: '' },
];
```

**File: `frontend/src/app/core/layout/navigation.ts`** — replace lines 1–11:

```ts
import { UserRole } from '../auth/auth-models';

export interface NavItem {
  readonly label: string;
  readonly icon: string; // Material Icons ligature name
  readonly path: string;
  readonly exact: boolean;
  /** Omitted = every signed-in role. Display only; the API enforces access. */
  readonly roles?: readonly UserRole[];
}

/** Feature stories append their entry here when their first screen ships. */
export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Home', icon: 'home', path: '/', exact: true },
  { label: 'Agents', icon: 'badge', path: '/agents', exact: false, roles: ['SuperAdmin'] },
  { label: 'Profile', icon: 'account_circle', path: '/profile', exact: false },
];
```

**File: `frontend/src/app/core/layout/shell/shell.ts`** — line 30: replace `protected readonly navItems = NAV_ITEMS;` with

```ts
  private readonly auth = inject(AuthService);
  protected readonly user = this.auth.currentUser;
  protected readonly navItems = computed(() => {
    const role = this.auth.role();
    return NAV_ITEMS.filter(item => !item.roles || (role !== null && item.roles.includes(role)));
  });

  protected signOut(): void {
    this.auth.logout();
  }
```

Add `computed` to the `@angular/core` import (line 2) and `import { AuthService } from '../../auth/auth-service';`.

**File: `frontend/src/app/core/layout/shell/shell.html`** — line 12: `@for (item of navItems(); track item.path) {`. Line 35: after the title span add:

```html
      <span class="shell__spacer"></span>
      @if (user(); as user) {
        <span class="shell__user">{{ user.firstName }} {{ user.lastName }}</span>
      }
      <button mat-icon-button type="button" aria-label="Sign out" (click)="signOut()">
        <mat-icon>logout</mat-icon>
      </button>
```

**File: `frontend/src/app/core/layout/shell/shell.scss`** — after line 32 add `.shell__spacer { flex: 1 1 auto; }` and `.shell__user { font: var(--mat-sys-label-large); color: var(--mat-sys-on-surface-variant); }` plus, inside the existing `@media (max-width: 599.98px)` block (lines 41–45), `.shell__user { display: none; }`.

**File: `frontend/src/app/features/home/home-page.ts`** — after line 36 add `protected readonly user = inject(AuthService).currentUser;` (import `AuthService` from `../../core/auth/auth-service`). **File: `home-page.html`** lines 1–3 → `<crm-page-header [title]="'Welcome, ' + (user()?.firstName ?? '')" subtitle="You are signed in to Support CRM. Modules appear here as they are delivered." />`. The System status card stays (minimal landing page for every role).

### 18 — Auth feature pages (public)

Folder `frontend/src/app/features/auth/`. Both pages render **outside** the shell: a centred `mat-card appearance="outlined"` (`max-inline-size: 420px; margin-block: var(--crm-space-8); margin-inline: auto; padding: var(--crm-space-4)`), brand row (`support_agent` icon + "Support CRM"), full-width `mat-form-field appearance="outline"` inputs, a `mat-flat-button` submit disabled while `submitting()`, an error paragraph (`role="alert"`, `color: var(--mat-sys-error)`) and a link to the other page.

**Create files: `sign-in-page.ts` / `.html` / `.scss`** — `SignInPage` (`crm-sign-in-page`):
- Form: `email` (`required`, `email`), `password` (`required`). Password input `type="password"` `autocomplete="current-password"`; email `autocomplete="username"`.
- Submit: `auth.login(form.getRawValue() with email trimmed)` → `router.navigateByUrl(route.snapshot.queryParamMap.get('returnUrl') ?? '/')`; error → `error.set(describeHttpError(err, 'Invalid email or password.'))`. Shows the server detail "Invalid email or password." for every credential failure.
- Link: "New customer? Create an account" → `/sign-up`.

**Create files: `sign-up-page.ts` / `.html` / `.scss`** — `SignUpPage` (`crm-sign-up-page`):
- Form: `firstName` (required, maxLength 100), `lastName` (required, maxLength 100), `email` (required, email, maxLength 256), `phoneNumber` (optional, maxLength 32), `password` (required, minLength 8, maxLength 128, hint "At least 8 characters"), `confirmPassword` (required; group validator: must equal `password`, error "Passwords do not match"). `autocomplete="new-password"`.
- Submit: `auth.register({ …, phoneNumber: phone.trim() || null })` → `router.navigateByUrl('/')`; error → `describeHttpError(err, 'Could not create the account.')` (409 shows "An account with this email already exists.").
- No role field of any kind. Link "Already have an account? Sign in".

### 19 — Profile feature

Folder `frontend/src/app/features/profile/`.

- **Create `profile.routes.ts`** — `export const PROFILE_ROUTES: Routes = [{ path: '', title: 'Profile | Support CRM', loadComponent: () => import('./profile-page').then(m => m.ProfilePage) }];`
- **Create `profile-api.ts`** — `ProfileApi`: `getMe()` `GET me` → `CurrentUser`; `updateMe(body: { firstName: string; lastName: string; phoneNumber: string | null })` `PUT me` → `CurrentUser`; `changePassword(body: { currentPassword: string; newPassword: string })` `POST me/password` → `void`.
- **Create `profile-page.ts` / `.html` / `.scss`** — `ProfilePage` (`crm-profile-page`), `crm-page-header` title "Your profile":
  1. Card "Account details": read-only email and role (`crm-status-badge` tone `neutral` with the role); form `firstName`, `lastName`, `phoneNumber` pre-filled from `getMe()`; Save → `updateMe` → `auth.updateCurrentUser(user)` + `MatSnackBar.open('Profile saved.', undefined, { duration: 3000 })`.
  2. Card "Change password": `currentPassword` (required), `newPassword` (required, minLength 8, maxLength 128), `confirmPassword` (must match). Submit → `changePassword` → snackbar "Password changed. Please sign in again." → `auth.endSession()`. On 400 show `problem.errors?.['currentPassword']?.[0]` or `describeHttpError`.
  Cards stacked with `display: grid; gap: var(--crm-space-6); max-inline-size: 640px`.

### 20 — Agents feature (SuperAdmin)

Folder `frontend/src/app/features/agents/`.

- **Create `agent-models.ts`** — `Agent { id; email; firstName; lastName; phoneNumber: string | null; isActive: boolean; createdAtUtc: string }`, `CreateAgentRequest { firstName; lastName; email; phoneNumber: string | null; password }`, `UpdateAgentRequest { firstName; lastName; phoneNumber: string | null }`.
- **Create `agents-api.ts`** — `AgentsApi`: `list()` `GET agents`; `get(id)` `GET agents/{id}`; `create(body)` `POST agents`; `update(id, body)` `PUT agents/{id}`; `setStatus(id, isActive)` `PUT agents/{id}/status` body `{ isActive }`; `setPassword(id, newPassword)` `PUT agents/{id}/password` body `{ newPassword }`.
- **Create `agents.routes.ts`** — `AGENTS_ROUTES`: `''` → `AgentListPage` (title "Agents | Support CRM"), `'new'` → `AgentCreatePage` ("New agent | Support CRM"), `':id'` → `AgentDetailPage` ("Agent | Support CRM"); all `loadComponent`.
- **Create `agent-list-page.ts` / `.html` / `.scss`** — header "Agents" + `mat-flat-button routerLink="new"` "New agent" (icon `person_add`). `mat-table` (wrapped in a container with `overflow-x: auto`) columns: Name (`firstName lastName`), Email, Phone (`—` when null), Status (`crm-status-badge` "Active"/`success` or "Disabled"/`neutral`), action `a mat-button [routerLink]="agent.id"` "Manage". Empty state text "No agents yet. Create the first agent account." Loading via `mat-progress-bar mode="indeterminate"`.
- **Create `agent-create-page.ts` / `.html` / `.scss`** — form: firstName, lastName, email, phoneNumber (optional), password (minLength 8, `type="password"`, `autocomplete="new-password"`, hint "Initial password — the agent can sign in with it immediately"). Submit → `create` → snackbar "Agent created." → `router.navigate(['/agents', agent.id])`. Cancel link back to `/agents`.
- **Create `agent-detail-page.ts` / `.html` / `.scss`** — reads `id` from `ActivatedRoute.snapshot.paramMap`; loads `get(id)`. Three cards:
  1. "Details": read-only email; form firstName/lastName/phoneNumber → `update` → snackbar "Agent updated."
  2. "Access": status badge + `mat-slide-toggle` "Account enabled" bound to `isActive` → `setStatus` → snackbar "Agent enabled." / "Agent disabled. Active sessions were signed out."; on error revert the toggle.
  3. "Set password": `newPassword` (minLength 8) + confirm → `setPassword` → clear the form + snackbar "Password updated. The agent's existing sessions were signed out." Never show any existing password.

### 21 — Frontend unit test (single happy path)

**Create file: `frontend/src/app/core/auth/auth-service.spec.ts`**

```ts
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { API_CONFIG } from '../api/api-config';
import { AuthService } from './auth-service';

describe('AuthService', () => {
  it('keeps the access token in memory after a successful login', () => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([]),
        { provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } },
      ],
    });
    const auth = TestBed.inject(AuthService);
    const http = TestBed.inject(HttpTestingController);

    auth.login({ email: 'customer@example.com', password: 'correct horse' }).subscribe();
    http.expectOne('/api/v1/auth/login').flush({
      accessToken: 'jwt-access-token',
      accessTokenExpiresAtUtc: '2030-01-01T00:00:00Z',
      user: { id: '1', email: 'customer@example.com', firstName: 'Ada', lastName: 'Lovelace', phoneNumber: null, role: 'Customer' },
    });

    expect(auth.accessToken()).toBe('jwt-access-token');
    expect(auth.role()).toBe('Customer');
    expect(localStorage.length + sessionStorage.length).toBe(0);
    http.verify();
  });
});
```

Keep `frontend/src/app/core/api/api-url.spec.ts` unchanged.

---

## Documentation Tasks

### 22 — `docs/identity-and-access.md` (create)

Sections (concise bullets/tables, no real secrets):
1. **Role matrix** — table rows = capabilities, columns SuperAdmin / Agent / Customer: sign in; view/update own profile (all ✔); change own password (all ✔); public signup (Customer only); list/create/edit/enable-disable/set-password Agents (SuperAdmin only); create SuperAdmin (nobody via API). Note: roles are server-assigned; Angular guards are usability only.
2. **API routes** — table: method, route, auth requirement, success code (`POST /api/v1/auth/register` anonymous 201; `login` anonymous 200; `refresh` anonymous + CSRF 200; `logout` anonymous + CSRF 204; `GET/PUT /api/v1/me` authenticated 200; `POST /api/v1/me/password` authenticated 204; `GET/POST /api/v1/agents`, `GET/PUT /api/v1/agents/{id}`, `PUT …/status`, `PUT …/password` SuperAdmin; `GET /api/health` anonymous).
3. **Sessions** — access token 10 min in memory; refresh 7 days sliding, single-use, hashed (SHA-256) in `RefreshSessions`; reuse ⇒ family revoked; **multi-tab grace window** (30 s, `Jwt:RefreshTokenReuseGracePeriod`): a token rotated moments ago by another tab, re-presented while the login is still alive, yields a sibling session instead of a revocation; logout revokes the whole family; cookies and their attributes; CSRF double-submit; reload flow (`provideAppInitializer` → `POST auth/refresh`).
4. **Revocation and JWT trade-offs** — per-request `IsActive` + security-stamp check makes disable / password change / reset immediate; **logout does not rotate the stamp**, so an access token copied before logout stays valid until it expires (≤ 10 min); one extra indexed DB read per authenticated request; the grace window means a thief who replays a just-rotated token **within 30 s, while the victim's login is still active,** obtains a sibling session instead of triggering revocation — accepted because the window is shorter than an access-token lifetime, the sibling dies with logout / password change / disable, and any replay after 30 s still revokes the family. Set the grace period to `00:00:00` for strict rotation.
5. **Brute force** — lockout 5 / 15 min; 10 credential requests/min/IP; uniform login error.
6. **Bootstrap** — the migration creates schema only; on startup roles + `admin@admin.com` are created if absent with the password from `Bootstrap:SuperAdmin:Password` (placeholder only); existing admin never reset; mismatched existing account logged, not promoted; pending migrations ⇒ bootstrap skipped with warning. Rotate the initial password (Profile → Change password) before any non-local deployment.
7. **Local development** — set `Jwt:SigningKey` and `Bootstrap:SuperAdmin:Password` user secrets; cookies are `Secure` and work on `http://localhost:4200` in Chromium-based browsers and Firefox (localhost is a secure context); for Safari or other hosts use HTTPS (`dotnet dev-certs https --trust`, API `https` profile, `ng serve --ssl`, proxy target `https://localhost:7080`). Never relax `Secure`/`SameSite` in code.
8. **Deferred** — email verification (public signup does **not** verify email ownership), forgot-password, MFA/SSO, email change, session/device management, absolute refresh-family lifetime, customer CRM records and company field, custom roles/permissions, agent ticket/customer permissions.

### 23 — Update existing docs

- **`docs/api-conventions.md`** — line 3: add "Singleton resources `/api/v1/auth/*` and `/api/v1/me` are the documented exceptions to plural naming." Table (lines 8–13): add rows `UnauthorizedException | 401 | ProblemDetails`, `ConflictException | 409 | ProblemDetails`. Add bullet: "**Auth:** every endpoint requires a Bearer JWT by default (fallback policy); anonymous endpoints carry `[AllowAnonymous]` and say so in `EndpointDescription`. Role-restricted endpoints use named policies (`AuthorizationPolicies`). 401/403/429 responses are ProblemDetails via `UseStatusCodePages`. See `docs/identity-and-access.md`."
- **`docs/persistence-and-transactions.md`** — add bullet: "**Identity writes:** `UserManager` saves immediately on the scoped `CrmDbContext`, so inside an `ITransactionalCommand` its writes join the transaction. Commands that must keep a write after throwing (login failure counter) are not transactional; commands that must commit a revocation while rejecting (refresh-token reuse) return a result instead of throwing."
- **`docs/frontend-ui-foundation.md`** — `core/` bullet (line 7): add "auth (session service, interceptor, guards)". Add bullet: "**Auth:** access token in memory only (`AuthService`); guards are UX only; `NavItem.roles` filters navigation; public pages (`/sign-in`, `/sign-up`) render outside the shell."
- **`README.md`** — after line 45 add a "### Authentication secrets" subsection with the two `dotnet user-secrets set` commands from task 11 (placeholder password) and the note "On first start with an empty database the API creates the three roles and `admin@admin.com`. Sign in with the configured password; no password change is forced." Line 3: replace "currently contains the architectural foundation only (story `ARCH-001`)" with "contains the architectural foundation (`ARCH-001`) and identity & access (`AUTH-001`)". Conventions list (lines 95–97): add `- [Identity and access](docs/identity-and-access.md)`.

---

## Edge Cases & Failure Modes

- **Migrations not applied** — `IdentityBootstrapper.RunAsync` sees pending migrations, logs a warning and returns; the API starts, but login fails until `dotnet ef database update` is run and the API restarted (task 9).
- **Connection string missing** — `BootstrapIdentityAsync` now touches the database at startup, so the API **fails to start** with the existing "Connection string 'CrmDatabase' is not configured" message from `Crm.Infrastructure/DependencyInjection.cs` lines 17–22. This deliberately replaces ARCH-001's "starts without a DB" behaviour; record it in `docs/identity-and-access.md` §7.
- **`Jwt:SigningKey` missing/short/not Base64** — `ValidateOnStart` (task 10) stops startup with the actionable message. `dotnet ef` is unaffected: design-time tooling stops the host at `Build()`, before `BootstrapIdentityAsync` and before options validation at `StartAsync`.
- **Admin absent and `Bootstrap:SuperAdmin:Password` missing** — startup throws the `InvalidOperationException` from task 9 naming the user-secrets command. Admin present ⇒ the password setting is never read again.
- **`admin@admin.com` exists without the SuperAdmin role** — logged as an error, not promoted, startup continues (task 9).
- **Bootstrap password shorter than 8** — `EnsureSucceeded` throws with Identity's description; the transaction rolls back, no partial admin.
- **Duplicate signup email (any casing)** — `EmailExistsAsync` uses Identity's normalized email ⇒ 409 "An account with this email already exists."; a race past the pre-check is caught by the unique `UserNameIndex` / `DuplicateUserName` code in `ThrowIfFailed` ⇒ 409.
- **Unicode or `'` in emails** — `AllowedUserNameCharacters = string.Empty` disables Identity's username character filter (UserName = email).
- **Client sends `role` in a signup/agent body** — no such property on the request records; System.Text.Json ignores unknown members; the server always assigns the role (tasks 5, 7).
- **SuperAdmin calls `/agents/{id}` with a Customer or SuperAdmin id** — `AgentLookup.GetAgentOrThrowAsync` ⇒ 404; admin accounts cannot be disabled or reset through the API.
- **Wrong password / unknown email / locked out / disabled** — identical 401 "Invalid email or password." (`LoginCommandHandler.InvalidCredentials`); the failure counter persists because `LoginCommand` is not transactional.
- **Brute force** — 6th failure inside the lockout window is rejected even with the right password for 15 min; the 11th credential request per minute per IP gets 429 ProblemDetails (rate limiter + `UseStatusCodePages`).
- **Refresh token replayed after the grace window** — the presented row has `RevocationReason = Rotated` and `RevokedAtUtc` older than 30 s ⇒ `RevokeFamilyAsync(ReuseDetected)` commits (handler returns `Rejected`, no exception, so the transaction commits) ⇒ 401 and cookies cleared; the legitimate holder's next refresh also fails (`RenewSessionCommandHandler.HandleRevokedAsync`, task 5).
- **Several tabs reload / refresh at the same time (same cookie)** — the first request rotates the token; the others either see the row already `Rotated` (sequential case) or lose `TryRevokeAsync` (0 rows, then re-read the committed row). In both cases `HandleRevokedAsync` finds `Rotated` within 30 s and an active family ⇒ each gets its own **sibling** session and 200. All tabs stay signed in; whichever `Set-Cookie` arrives last becomes the browser cookie and the other siblings simply go unused until expiry or logout. Within one tab Angular's single-flight `refresh()` still sends only one request.
- **Lost response / client retry** — the server rotated but the response never arrived; the browser retries with the old cookie within 30 s ⇒ sibling session instead of a forced sign-out.
- **Recently rotated token after logout / password change / disable / reuse detection** — no active session left in the family ⇒ `Rejected` without further writes; grace never revives a dead login.
- **Tokens revoked for any reason other than `Rotated`** — rejected outright, regardless of age.
- **Grace period misconfigured** — values outside `00:00:00`–`00:02:00` stop startup via `ValidateOnStart` (task 10); `00:00:00` restores strict rotation.
- **Logout with sibling sessions** — `LogoutCommandHandler` revokes the presented token's whole family, so siblings from concurrent tabs cannot outlive logout. An unknown/expired cookie on logout is a no-op (204).
- **Transient SQL error during rotation** — `EfUnitOfWork` retries the whole handler after `ChangeTracker.Clear()`; `ExecuteUpdateAsync` statements are inside the rolled-back transaction, so the retry starts clean.
- **Expired refresh token / unknown token / empty cookie** — `Rejected` ⇒ 401 "Session expired." + cookies cleared; Angular `restoreSession` resolves anonymously and guards send the user to `/sign-in`.
- **Missing or mismatched `X-XSRF-TOKEN`** on refresh/logout — `ValidateCsrfAttribute` ⇒ 403. Angular sends the header automatically only for relative, mutating requests (the built-in XSRF interceptor), which all `/api` calls are.
- **Agent disabled while signed in** — `SetActiveAsync` rotates the stamp and all sessions are revoked; the agent's next API call fails `OnTokenValidated` ⇒ 401 ⇒ interceptor refresh fails ⇒ `endSession()` ⇒ sign-in page.
- **Password changed by the user** — all sessions revoked and stamp rotated; `MeController.ChangePassword` clears cookies; Angular ends the session and asks to sign in again.
- **Access token expires while the page is open** — first 401 triggers one refresh and a retry; parallel 401s share the same in-flight refresh.
- **Page reload** — `provideAppInitializer` awaits `restoreSession()` before the first navigation, so `authGuard` sees the restored session. No `XSRF-TOKEN` cookie ⇒ no refresh call.
- **Safari / non-localhost HTTP** — `Secure` cookies are not stored; refresh-on-reload fails. Use the HTTPS setup in `docs/identity-and-access.md` §7; do not weaken cookie flags.
- **Fallback policy blocks OpenAPI/Scalar** — fixed by `.AllowAnonymous()` on both maps in `Program.cs`. If `MapScalarApiReference()` does not return an `IEndpointConventionBuilder` in Scalar 2.18.0, wrap both maps in `app.MapGroup("").AllowAnonymous()` instead.
- **OpenAPI security scheme compile errors** — `Microsoft.OpenApi` 2.x uses namespace `Microsoft.OpenApi` and `IDictionary<string, IOpenApiSecurityScheme>`; confirm with `dotnet list src/Crm.Api package --include-transitive | grep Microsoft.OpenApi`.
- **`ExecuteUpdateAsync` setter type inference** — keep the explicit `(DateTimeOffset?)` and `(RefreshSessionRevocationReason?)` casts in task 8, or the nullable setters do not compile.
- **`IdentityDbContext` order** — `base.OnModelCreating` must run **before** `ApplyConfigurationsFromAssembly` (task 8), otherwise Identity overrides `ApplicationUserConfiguration`.
- **Secrets in logs** — `LoggingBehavior` logs only the message type name; `GlobalExceptionHandler` logs status/title; EF sensitive-data logging stays off; `ThrowIfFailed` never includes submitted values. Do not add request/response body logging.
- **JWT contents** — only `sub`, `role`, `sstamp`, `jti` and time claims. The security stamp is an opaque random value; no PII.

---

## Test Plan

1. **Unit (backend) — add:** `backend/tests/Crm.UnitTests/Auth/LoginCommandHandlerTests.cs` → `Handle_ValidCredentials_IssuesTokensAndPersistsOnlyRefreshTokenHash` (task 15). Keep `Diagnostics/GetSystemStatusQueryHandlerTests.cs`.
2. **Unit (frontend) — add:** `frontend/src/app/core/auth/auth-service.spec.ts` → "keeps the access token in memory after a successful login" (task 21). Keep `core/api/api-url.spec.ts`.
3. **Manual smoke (happy paths only):** see Verification Steps 4–9.

No other tests, mocking libraries, integration/SQL/API/E2E suites or negative-path checks. Security behaviour in Edge Cases is implemented as specified but not separately tested in this story.

---

## Migration / Rollback

- **Forward:** `AddIdentityAndRefreshSessions` only creates tables and indexes; `InitialCreate` is empty, so no existing data is touched.
- **Rollback (local):** stop the API, then from `backend/`: `dotnet ef database update InitialCreate --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` (drops the Identity and `RefreshSessions` tables — all accounts are lost), then `dotnet ef migrations remove …` and revert the code.
- **Half-applied state:** if the migration fails mid-way, SQL Server DDL in the EF migration transaction rolls back; re-run `database update`. If the admin creation fails, the bootstrap transaction rolls back; fix the cause (e.g. password length) and restart — bootstrap is idempotent.

---

## Verification Steps

1. **Backend builds and tests:** in `backend/` run `dotnet build` (0 errors) then `dotnet test` (2 tests passed).
2. **Layer direction:** `dotnet list src/Crm.Domain/Crm.Domain.csproj package` (none) and `dotnet list src/Crm.Application/Crm.Application.csproj package` shows no `Microsoft.AspNetCore.*`, `Microsoft.EntityFrameworkCore*` or `Microsoft.IdentityModel*`; `grep -rn "Microsoft.AspNetCore\|EntityFrameworkCore" src/Crm.Application src/Crm.Domain --include=*.cs` returns nothing.
3. **Database:** after task 14, `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT MigrationId FROM __EFMigrationsHistory; SELECT Name FROM AspNetRoles ORDER BY Name;"` (run after first API start) → two migrations (`…_InitialCreate`, `…_AddIdentityAndRefreshSessions`) and `Agent`, `Customer`, `SuperAdmin`. `grep -n "InsertData\|Sql(" src/Crm.Infrastructure/Persistence/Migrations/*AddIdentityAndRefreshSessions.cs` returns nothing.
4. **Bootstrap + SuperAdmin login:** with user secrets set (task 11), `dotnet run --project src/Crm.Api --launch-profile http`; log shows "Initial SuperAdmin account provisioned." Restart: the message does not repeat and no error is logged. `sqlcmd … -Q "SELECT Email, LEFT(PasswordHash, 6) FROM AspNetUsers"` shows `admin@admin.com` with a hash starting `AQAAAA` (Identity v3 hash). `curl -s http://localhost:5080/api/health` → 200 without a token.
5. **Frontend builds and tests:** in `frontend/` run `npx ng build` (no errors; budget warnings acceptable) and `npm test -- --watch=false` (2 tests passed).
6. **Customer signup and login (browser, Chrome/Edge, `npm start`, http://localhost:4200):** you are redirected to `/sign-in`; open "Create an account", register a customer → lands on Home "Welcome, <first name>" with Home + Profile in navigation (no Agents). Profile: change phone number → "Profile saved."
7. **SuperAdmin creates Agent, Agent logs in:** sign out; sign in as `admin@admin.com` with the configured password → no password-change prompt; Agents appears in navigation. Create an agent with an 8+ character password → detail page. Sign out; sign in as the agent → Home + Profile only.
8. **Session renewal:** while signed in, reload the page → still signed in (DevTools Network shows `POST /api/v1/auth/refresh` 200 with a new `crm_refresh` `Set-Cookie`); DevTools Application → Local/Session Storage are empty; `crm_refresh` shows HttpOnly, Secure, SameSite=Strict, Path `/api/v1/auth`. `sqlcmd … -Q "SELECT TOP 3 FamilyId, RevokedAtUtc, RevocationReason FROM RefreshSessions ORDER BY CreatedAtUtc DESC"` shows the previous row `Rotated` and the newest row unrevoked in the same family. Then open the app in a second tab and reload both tabs quickly (within a few seconds): both stay signed in, each `POST /api/v1/auth/refresh` returns 200, and the same query shows no `ReuseDetected` rows.
9. **Logout:** click Sign out → `/sign-in`; `POST /api/v1/auth/logout` 204; every row of that login's `FamilyId` is revoked (the latest with `RevocationReason = Logout`); reload stays on `/sign-in` in every tab.
10. **OpenAPI:** `curl -s http://localhost:5080/openapi/v1.json` lists `/api/v1/auth/login`, `/api/v1/me`, `/api/v1/agents`, a `Bearer` security scheme and "Requires role: SuperAdmin." descriptions.
11. **No secrets committed:** `git status --short` shows no secrets file; `git grep -n -i "SigningKey\"\s*:" -- backend` and `git grep -n "Bootstrap:SuperAdmin:Password\" \"[^<]"` return nothing; `grep -rnE "localStorage|sessionStorage" frontend/src/app --include=*.ts` matches only the spec.
12. **Regression:** `grep -rnE "(margin|padding)-(left|right)|text-align: *(left|right)" frontend/src` returns nothing; home page still shows "API connected".

No negative-path manual checks are part of verification.

---

## Done Criteria

- [ ] Changes extend ARCH-001 in place (no new scaffold); Domain/Application reference no ASP.NET Core, EF Core or token packages (Verification 2).
- [ ] `UserRoles` defines exactly SuperAdmin, Agent, Customer; bootstrap creates them idempotently (Verification 3–4).
- [ ] `POST /api/v1/auth/register` stores first/last name, unique email, optional phone and an Identity hash, assigns Customer server-side; request has no role field.
- [ ] Customer and Agent can sign in and view/update only their own profile via `/api/v1/me` (user id from the token only).
- [ ] SuperAdmin can list, create, edit, enable/disable and set passwords of Agents; `/api/v1/agents*` requires the SuperAdmin policy and targets only Agent accounts.
- [ ] Fallback policy requires authentication; only auth endpoints, `/api/health` and dev OpenAPI/Scalar are anonymous.
- [ ] All password paths enforce min 8 / max 128 with no composition rules; seeded admin and new/reset agents are not forced to change password.
- [ ] New migration `AddIdentityAndRefreshSessions` creates schema only (no seeded credentials); applied to the existing database without recreation.
- [ ] Startup bootstrap creates `admin@admin.com` only when absent, from `Bootstrap:SuperAdmin:Password` via `UserManager`; never resets it; logs (does not promote) a conflicting account.
- [ ] JWTs are HS256, validated for signature/issuer/audience/lifetime, carry only `sub`/`role`/`sstamp`/`jti`/time claims; signing key only in user secrets.
- [ ] Refresh tokens: random, SHA-256 hash only in `RefreshSessions`, HttpOnly Secure SameSite=Strict cookie, atomic single-use rotation, replay after the 30 s grace window revokes the family; concurrent refreshes from several tabs of a live login each receive a sibling session instead of being signed out (Verification 8).
- [ ] Angular restores the session on reload via the refresh cookie and keeps tokens out of web storage; CSRF double-submit protects refresh/logout; local cookie/HTTPS notes documented.
- [ ] Logout revokes the current login's whole refresh family and clears cookies; disable / password change / reset revoke sessions and immediately invalidate JWTs via the per-request stamp check; trade-offs documented.
- [ ] Angular sign-in, sign-up, profile + change password, role-aware navigation and sign-out, SuperAdmin agent list/create/detail work with the existing Material theme and `ApiUrl`.
- [ ] All new endpoints follow `/api/v1`, `…Request`/`…Response`, camelCase, direct DTO success codes and ProblemDetails; role requirements appear in OpenAPI (Verification 10).
- [ ] `dotnet build`, `dotnet test` (2 passed), `npx ng build`, `npm test -- --watch=false` (2 passed) succeed; manual flows in Verification 4–9 pass; no extra test types added.
- [ ] `docs/identity-and-access.md` created; `docs/api-conventions.md`, `docs/persistence-and-transactions.md`, `docs/frontend-ui-foundation.md` and `README.md` updated; no real password anywhere in the repository.
- [ ] No tickets, customer CRM records, email, localization, departments/branches or other out-of-scope features were added.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 03.**
