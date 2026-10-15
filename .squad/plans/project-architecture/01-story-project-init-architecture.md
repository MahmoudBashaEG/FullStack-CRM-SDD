# Story 01 — Project Init Architecture

## Prerequisites

- None. This is the foundational story (`ARCH-001`); every later CRM feature story depends on it.
- The repository root `C:\Users\mbasg\Desktop\CRM` currently contains only `.squad/`, `.claude/` and a squad-kit-managed `.gitignore`. It is **not yet a Git repository**. There is no application code; every file in this plan is created from scratch.
- Toolchain verified on the planning machine (2026-10-15):
  - .NET SDK **10.0.400** (also 9.0.317 installed — pin 10 with `global.json`).
  - `dotnet-ef` global tool **10.0.12**.
  - Node **24.20.0**, npm **11.19.0**, Angular CLI latest **22.2.2**.
  - SQL Server **LocalDB** instance `MSSQLLocalDB` present; `sqlcmd` 15 installed.

---

## Story Goal

Create the technical foundation of the Customer Support CRM in one repository:

1. `backend/` — an ASP.NET Core (.NET 10) REST API in Clean Architecture (**Domain → Application → Infrastructure → API**) with CQRS via a source-generated mediator, FluentValidation, centralized `ProblemDetails` error handling, structured logging, OpenAPI, EF Core 10 + SQL Server with a repeatable migration workflow, and a **reusable transactional command boundary**.
2. `frontend/` — an Angular 22 standalone app organized as `core/`, `shared/`, `features/`, with routing, a responsive Angular Material shell, a centralized API configuration/URL builder, and a dev proxy to the API.
3. A minimal connectivity demo: `GET /api/health` (dispatched through the mediator as a CQRS query) and a home screen that calls it and shows the connection status.
4. Exactly **one** backend happy-path unit test and **one** frontend happy-path unit test.
5. Root `README.md` plus three `docs/` pages recording the API contract policy, the transaction policy and the UI foundation decisions.

**Not in scope** (do **not** create): any CRM business entity, table, DTO, endpoint or screen (customers, tickets, channels, SLA, KB, AI, portal, reports); authentication/authorization/roles; localization, language switching or RTL behaviour; departments/branches/tenancy; `/api/v2`; pagination/sorting/filtering conventions; Docker, CI/CD, cloud hosting; integration/E2E/SQL/API/negative-path tests.

### Recorded technology decisions

| Concern | Choice | Version |
|---|---|---|
| Backend runtime | .NET (LTS) / ASP.NET Core, `net10.0` | SDK 10.0.400 (`global.json`, `rollForward: latestFeature`) |
| ORM / DB | `Microsoft.EntityFrameworkCore.SqlServer` + `Microsoft.EntityFrameworkCore.Design` | 10.0.13 |
| Mediator | `Mediator.Abstractions` + `Mediator.SourceGenerator` (martinothamar/Mediator, MIT, source-generated; chosen over MediatR because MediatR is now commercially licensed) | 3.0.2 |
| Validation | `FluentValidation` + `FluentValidation.DependencyInjectionExtensions` | 12.2.0 |
| OpenAPI | `Microsoft.AspNetCore.OpenApi` (built-in document) + `Scalar.AspNetCore` (Development-only UI) | 10.0.13 / 2.18.0 |
| Abstractions for class libs | `Microsoft.Extensions.Logging.Abstractions`, `Microsoft.Extensions.Configuration.Abstractions` | 10.0.13 |
| Backend tests | xUnit via the SDK's `dotnet new xunit` template (keep the versions it generates) | template default |
| Frontend | Angular standalone, zoneless (CLI 22 default), SCSS | `@angular/cli@22.2.2` |
| UI library | Angular Material + CDK (M3 theming, accessible, built-in bidi support) | `~22.2.0` |
| Frontend tests | Vitest (Angular CLI 22 default runner) | template default |
| Fonts/icons | Inter (Google Fonts) + Material Icons font | CDN links in `index.html` |

---

## Context — Read These Files First

1. `.squad/stories/project-architecture/project-init-architecture/intake.md` — the full intake; sections 1–7 of the Description and the Acceptance criteria list are the contract for this plan.
2. `.squad/stories/project-architecture/project-init-architecture/attachments/azm_squad_customer_support_crm 1.pdf` — two-page CRM core-feature list (note: the intake's attachment table names it `…1(2).pdf`; the file on disk is `…1.pdf`). Use it **only** to understand future modules (Customers, Tickets, Communications, Dashboard, Knowledge Base, Reports). It is gitignored by the squad-kit block.
3. `.gitignore` — lines 1–8 are the squad-kit managed block (`# Managed by squad-kit — do not edit this block` … `# End squad-kit block`). **Do not modify those lines**; append new rules below line 8.
4. `.squad/README.md` — squad-kit workflow (Intake → Plan → Implement); the root `README.md` must summarize it.
5. `.squad/config.yaml` — `tracker.type: none`, `naming.globalSequence: true`.

---

## Implementation tasks

> Run all commands from the repository root `C:\Users\mbasg\Desktop\CRM` unless a step says otherwise. Shell examples use PowerShell/Git Bash-neutral syntax.

### 1 — Initialize the Git repository and root ignore rules

1. Run `git init` at the repository root (only if `.git/` does not exist). Do **not** commit; the user commits.
2. **File: `.gitignore`** — keep lines 1–8 unchanged; append:

```gitignore

# --- .NET ---
bin/
obj/
*.user
*.suo
*.rsuser
.vs/
TestResults/
*.trx
*.coverage
appsettings.*.local.json

# --- Angular / Node ---
node_modules/
frontend/dist/
frontend/.angular/
coverage/
npm-debug.log*

# --- Local secrets and machine files ---
.env
.env.*
!.env.example
*.pfx
*.mdf
*.ldf
.idea/
.DS_Store
Thumbs.db
```

### 2 — Scaffold the backend solution

Run inside `backend/` (create it first):

```bash
mkdir backend && cd backend
dotnet new globaljson --sdk-version 10.0.400 --roll-forward latestFeature
dotnet new sln -n Crm
dotnet new classlib -n Crm.Domain         -o src/Crm.Domain         -f net10.0
dotnet new classlib -n Crm.Application    -o src/Crm.Application    -f net10.0
dotnet new classlib -n Crm.Infrastructure -o src/Crm.Infrastructure -f net10.0
dotnet new webapi   -n Crm.Api            -o src/Crm.Api            -f net10.0 --use-controllers
dotnet new xunit    -n Crm.UnitTests      -o tests/Crm.UnitTests    -f net10.0

dotnet sln add src/Crm.Domain src/Crm.Application src/Crm.Infrastructure src/Crm.Api tests/Crm.UnitTests

# Dependency direction (inward only)
dotnet add src/Crm.Application    reference src/Crm.Domain
dotnet add src/Crm.Infrastructure reference src/Crm.Application
dotnet add src/Crm.Api            reference src/Crm.Application src/Crm.Infrastructure
dotnet add tests/Crm.UnitTests    reference src/Crm.Application

# Packages
dotnet add src/Crm.Application package Mediator.Abstractions --version 3.0.2
dotnet add src/Crm.Application package FluentValidation --version 12.2.0
dotnet add src/Crm.Application package FluentValidation.DependencyInjectionExtensions --version 12.2.0
dotnet add src/Crm.Application package Microsoft.Extensions.Logging.Abstractions --version 10.0.13

dotnet add src/Crm.Infrastructure package Microsoft.EntityFrameworkCore.SqlServer --version 10.0.13
dotnet add src/Crm.Infrastructure package Microsoft.Extensions.Configuration.Abstractions --version 10.0.13

dotnet add src/Crm.Api package Mediator.SourceGenerator --version 3.0.2
dotnet add src/Crm.Api package Microsoft.EntityFrameworkCore.Design --version 10.0.13
dotnet add src/Crm.Api package Microsoft.AspNetCore.OpenApi --version 10.0.13
dotnet add src/Crm.Api package Scalar.AspNetCore --version 2.18.0

dotnet user-secrets init --project src/Crm.Api
```

Then delete template leftovers:
- `src/Crm.Domain/Class1.cs`, `src/Crm.Application/Class1.cs`, `src/Crm.Infrastructure/Class1.cs`
- `src/Crm.Api/Controllers/WeatherForecastController.cs`, `src/Crm.Api/WeatherForecast.cs`, `src/Crm.Api/Crm.Api.http`
- `tests/Crm.UnitTests/UnitTest1.cs`

**Rules:**
- `Crm.Domain` has **no** project references and **no** package references.
- `Crm.Application` must **not** reference EF Core, SQL Server, ASP.NET Core or `Crm.Infrastructure`.
- In `src/Crm.Api/Crm.Api.csproj`, the `Mediator.SourceGenerator` and `Microsoft.EntityFrameworkCore.Design` references must carry `<PrivateAssets>all</PrivateAssets>` (the `dotnet add` command adds this for `.Design`; add it manually for `Mediator.SourceGenerator` if missing).

**Create file: `backend/Directory.Build.props`**

```xml
<Project>
  <PropertyGroup>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>
</Project>
```

### 3 — Domain layer

**Create file: `backend/src/Crm.Domain/Common/DomainException.cs`**

```csharp
namespace Crm.Domain.Common;

/// <summary>Raised when a domain rule is violated. Mapped to HTTP 422 by the API.</summary>
public class DomainException(string message) : Exception(message);
```

No entities are created in this story.

### 4 — Application layer (CQRS, validation, transaction contract)

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IUnitOfWork.cs`**

```csharp
namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Application-facing persistence boundary. Implemented in Infrastructure over the
/// request-scoped CrmDbContext. Handlers stage changes, then save once.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>One atomic save (EF Core wraps a single SaveChanges in its own transaction).</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> inside one explicit database transaction, then saves
    /// and commits. Rolls back if the operation throws or is cancelled. Retried as a whole
    /// unit when the execution strategy retries. Joins an already-open transaction.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Messaging/ITransactionalCommand.cs`**

```csharp
namespace Crm.Application.Abstractions.Messaging;

/// <summary>
/// Marker for commands that perform more than one SaveChanges or several dependent
/// write steps. TransactionBehavior wraps the whole handler in one explicit transaction.
/// Do NOT apply to queries or to commands that save exactly once.
/// </summary>
public interface ITransactionalCommand;
```

**Create file: `backend/src/Crm.Application/Common/Exceptions/NotFoundException.cs`**

```csharp
namespace Crm.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 404 by the API.</summary>
public sealed class NotFoundException(string message) : Exception(message);
```

**Create file: `backend/src/Crm.Application/Common/Behaviors/LoggingBehavior.cs`**

```csharp
using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Common.Behaviors;

public sealed class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageName = typeof(TMessage).Name;
        var start = Stopwatch.GetTimestamp();
        logger.LogDebug("Handling {MessageName}", messageName);

        var response = await next(message, cancellationToken);

        logger.LogInformation(
            "Handled {MessageName} in {ElapsedMs:0.0} ms",
            messageName,
            Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        return response;
    }
}
```

**Create file: `backend/src/Crm.Application/Common/Behaviors/ValidationBehavior.cs`**

```csharp
using FluentValidation;
using Mediator;

namespace Crm.Application.Common.Behaviors;

public sealed class ValidationBehavior<TMessage, TResponse>(IEnumerable<IValidator<TMessage>> validators)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (validators.Any())
        {
            var context = new ValidationContext<TMessage>(message);
            var results = await Task.WhenAll(validators.Select(v => v.ValidateAsync(context, cancellationToken)));
            var failures = results.SelectMany(r => r.Errors).Where(f => f is not null).ToList();
            if (failures.Count > 0)
            {
                throw new ValidationException(failures);
            }
        }

        return await next(message, cancellationToken);
    }
}
```

**Create file: `backend/src/Crm.Application/Common/Behaviors/TransactionBehavior.cs`**

```csharp
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Application.Common.Behaviors;

/// <summary>
/// The single owner of explicit transactions. Only messages marked ITransactionalCommand
/// are wrapped. IUnitOfWork is resolved lazily so queries never construct a DbContext here.
/// </summary>
public sealed class TransactionBehavior<TMessage, TResponse>(IServiceProvider serviceProvider)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (message is not ITransactionalCommand)
        {
            return next(message, cancellationToken);
        }

        return HandleInTransactionAsync(message, next, cancellationToken);
    }

    private async ValueTask<TResponse> HandleInTransactionAsync(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        return await unitOfWork.ExecuteInTransactionAsync(
            async ct => await next(message, ct),
            cancellationToken);
    }
}
```

(`Microsoft.Extensions.DependencyInjection.Abstractions` arrives transitively via `FluentValidation.DependencyInjectionExtensions` / `Mediator.Abstractions`.)

**Create file: `backend/src/Crm.Application/Diagnostics/GetSystemStatus/GetSystemStatusQuery.cs`** — the non-business CQRS demonstration. Do **not** name the folder/namespace `System` (it shadows the `System` namespace).

```csharp
using Mediator;

namespace Crm.Application.Diagnostics.GetSystemStatus;

public sealed record GetSystemStatusQuery : IQuery<SystemStatus>;

public sealed record SystemStatus(string Status, DateTimeOffset CheckedAtUtc);

public sealed class GetSystemStatusQueryHandler(TimeProvider timeProvider)
    : IQueryHandler<GetSystemStatusQuery, SystemStatus>
{
    public const string Healthy = "Healthy";

    public ValueTask<SystemStatus> Handle(GetSystemStatusQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new SystemStatus(Healthy, timeProvider.GetUtcNow()));
}
```

Handlers and messages **must be `public`** — the mediator source generator runs in `Crm.Api`, another assembly.

**Create file: `backend/src/Crm.Application/DependencyInjection.cs`**

```csharp
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
```

**Folder convention for future features** (document in README, do not create empty folders): `Crm.Application/<Feature>/<UseCase>/` holding `<UseCase>Command|Query.cs`, handler, and `<UseCase>Validator.cs`; per-aggregate repository interfaces in `Crm.Application/Abstractions/Persistence/` (added by the feature story that needs them).

### 5 — Infrastructure layer (EF Core, SQL Server, unit of work)

**Create file: `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`**

```csharp
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public const string ConnectionStringName = "CrmDatabase";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Feature stories add IEntityTypeConfiguration<T> classes under Persistence/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
```

No `DbSet<>` properties in this story.

**Create file: `backend/src/Crm.Infrastructure/Persistence/EfUnitOfWork.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

internal sealed class EfUnitOfWork(CrmDbContext dbContext) : IUnitOfWork
{
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        // Nested transactional command: join the outer transaction; the outer call commits.
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        // Required when EnableRetryOnFailure is on: the WHOLE transaction is the retry unit.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(
            async ct =>
            {
                // A retry must not replay change-tracker state from the failed attempt.
                dbContext.ChangeTracker.Clear();

                await using var transaction = await dbContext.Database.BeginTransactionAsync(ct);
                try
                {
                    var result = await operation(ct);
                    await dbContext.SaveChangesAsync(ct);
                    await transaction.CommitAsync(ct);
                    return result;
                }
                catch
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                    throw;
                }
            },
            cancellationToken);
    }
}
```

**Create file: `backend/src/Crm.Infrastructure/DependencyInjection.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Options are built lazily (first DbContext resolution), so the API starts without a DB.
        services.AddDbContext<CrmDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(CrmDbContext.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{CrmDbContext.ConnectionStringName}' is not configured. " +
                    "Set it with 'dotnet user-secrets' (see README → Local SQL Server).");
            }

            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(CrmDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
            });
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return services;
    }
}
```

`AddDbContext` registers `CrmDbContext` as **scoped** (one per HTTP request) — handlers, repositories and `EfUnitOfWork` in the same request share it. Do not change the lifetime.

### 6 — API layer (composition root, contracts, errors, OpenAPI)

**Create file: `backend/src/Crm.Api/ApiRoutes.cs`**

```csharp
namespace Crm.Api;

/// <summary>Route prefixes. Business controllers use [Route(ApiRoutes.V1 + "/<plural-resource>")].</summary>
public static class ApiRoutes
{
    /// <summary>Infrastructure-only, intentionally unversioned.</summary>
    public const string Health = "api/health";

    /// <summary>Prefix for all business resources, e.g. "api/v1/tickets".</summary>
    public const string V1 = "api/v1";
}
```

**Create file: `backend/src/Crm.Api/Contracts/Health/HealthResponse.cs`**

```csharp
namespace Crm.Api.Contracts.Health;

/// <summary>JSON: { "status": "Healthy", "service": "Crm.Api", "checkedAtUtc": "..." }</summary>
public sealed record HealthResponse(string Status, string Service, DateTimeOffset CheckedAtUtc);
```

**Create file: `backend/src/Crm.Api/Controllers/HealthController.cs`**

```csharp
using Crm.Api.Contracts.Health;
using Crm.Application.Diagnostics.GetSystemStatus;
using Mediator;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.Health)]
public sealed class HealthController(ISender sender) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<HealthResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthResponse>> Get(CancellationToken cancellationToken)
    {
        var status = await sender.Send(new GetSystemStatusQuery(), cancellationToken);
        return Ok(new HealthResponse(status.Status, "Crm.Api", status.CheckedAtUtc));
    }
}
```

The health endpoint does **not** touch the database.

**Create file: `backend/src/Crm.Api/Errors/GlobalExceptionHandler.cs`**

```csharp
using System.Text.Json;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Errors;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found.",
                Detail = notFound.Message,
            },
            DomainException domain => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Business rule violation.",
                Detail = domain.Message,
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = environment.IsDevelopment() ? exception.Message : null,
            },
        };

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Request failed with {StatusCode}: {Title}", problem.Status, problem.Title);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
```

**Create file: `backend/src/Crm.Api/DependencyInjection.cs`**

```csharp
using Crm.Api.Errors;
using Crm.Application.Common.Behaviors;
using Crm.Application.Diagnostics.GetSystemStatus;
using Mediator;

namespace Crm.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddControllers(); // System.Text.Json web defaults: camelCase properties
        services.AddRouting(options => options.LowercaseUrls = true);

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Customer Support CRM API";
            document.Info.Description =
                "Business routes: /api/v1/{plural-resource}. Infrastructure: /api/health (unversioned). " +
                "DTOs: *Request / *Response, camelCase JSON. Success: direct DTO with 200/201(+Location)/204. " +
                "Errors: application/problem+json (ProblemDetails / ValidationProblemDetails).";
            return Task.CompletedTask;
        }));

        services.AddMediator((MediatorOptions options) =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped; // handlers share the request-scoped DbContext
            options.Assemblies = [typeof(GetSystemStatusQuery)];
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),     // outermost
                typeof(ValidationBehavior<,>),
                typeof(TransactionBehavior<,>), // innermost, wraps the handler only
            ];
        });

        return services;
    }
}
```

**File: `backend/src/Crm.Api/Program.cs`** — replace the whole template content:

```csharp
using Crm.Api;
using Crm.Application;
using Crm.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();               // /openapi/v1.json
    app.MapScalarApiReference();    // /scalar
}
else
{
    app.UseHttpsRedirection();
}

app.MapControllers();

app.Run();
```

**File: `backend/src/Crm.Api/Properties/launchSettings.json`** — set the `http` profile `applicationUrl` to `http://localhost:5080`, the `https` profile to `https://localhost:7080;http://localhost:5080`, and `launchBrowser` to `false` in both. Keep `ASPNETCORE_ENVIRONMENT: Development`.

**File: `backend/src/Crm.Api/appsettings.json`** — replace with (no connection string, no secrets):

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning"
    },
    "Console": {
      "FormatterName": "json"
    }
  },
  "AllowedHosts": "*"
}
```

**File: `backend/src/Crm.Api/appsettings.Development.json`** — replace with:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Crm": "Debug"
    },
    "Console": {
      "FormatterName": "simple"
    }
  }
}
```

### 7 — Local database and initial migration

1. Configure the connection string as a **user secret** (stored outside the repo):

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:CrmDatabase" "Server=(localdb)\MSSQLLocalDB;Database=CrmDev;Trusted_Connection=True;TrustServerCertificate=True" --project src/Crm.Api
```

2. Create the initial (empty-model) migration — it creates only `__EFMigrationsHistory` and proves the workflow:

```bash
dotnet ef migrations add InitialCreate --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations -- --environment Development
dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development
```

The trailing `-- --environment Development` is **required**: without it the design-time host runs as Production and does not load user secrets. Commit the generated files under `backend/src/Crm.Infrastructure/Persistence/Migrations/`.

### 8 — Backend unit test (single happy path)

**Create file: `backend/tests/Crm.UnitTests/Diagnostics/GetSystemStatusQueryHandlerTests.cs`**

```csharp
using Crm.Application.Diagnostics.GetSystemStatus;

namespace Crm.UnitTests.Diagnostics;

public sealed class GetSystemStatusQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsHealthyStatus()
    {
        var handler = new GetSystemStatusQueryHandler(TimeProvider.System);

        var result = await handler.Handle(new GetSystemStatusQuery(), CancellationToken.None);

        Assert.Equal(GetSystemStatusQueryHandler.Healthy, result.Status);
    }
}
```

This is the **only** backend test. Do not add mocking libraries.

### 9 — Scaffold the frontend

Run at the repository root:

```bash
npx -y @angular/cli@22.2.2 new crm-frontend --directory frontend --routing --style scss --ssr false --skip-git --ai-config none --defaults
cd frontend
npm install @angular/material@~22.2.0 @angular/cdk@~22.2.0
npx ng generate environments
```

Then delete `frontend/src/app/app.spec.ts` (its assertion targets the template's placeholder page, which is removed). Keep the `frontend/.gitignore` that `ng new` creates.

Angular 22 naming: files have **no** `.component`/`.service` suffix (`shell.ts`, class `Shell`). Follow that throughout.

### 10 — Centralized API configuration and dev proxy

**File: `frontend/src/environments/environment.ts`** and **`frontend/src/environments/environment.development.ts`** — both export:

```ts
export const environment = {
  apiBaseUrl: '/api',
  apiVersion: 'v1',
};
```

(Relative `/api` works for both the dev proxy and a same-origin production deployment. The only place a host/port appears is `proxy.conf.json`.)

**Create file: `frontend/proxy.conf.json`**

```json
{
  "/api": {
    "target": "http://localhost:5080",
    "secure": false,
    "logLevel": "warn"
  }
}
```

**File: `frontend/angular.json`** — in `projects.crm-frontend.architect.serve`, add `"proxyConfig": "proxy.conf.json"` inside `options` (create `options` if absent). No CORS configuration is added to the API — the proxy makes requests same-origin.

**Create file: `frontend/src/app/core/api/api-config.ts`**

```ts
import { InjectionToken, Provider } from '@angular/core';
import { environment } from '../../../environments/environment';

export interface ApiConfig {
  /** API root without trailing slash, e.g. '/api'. */
  readonly baseUrl: string;
  /** Version segment for business routes, e.g. 'v1'. */
  readonly version: string;
}

export const API_CONFIG = new InjectionToken<ApiConfig>('API_CONFIG');

export function provideApiConfig(): Provider {
  return {
    provide: API_CONFIG,
    useValue: { baseUrl: environment.apiBaseUrl, version: environment.apiVersion } satisfies ApiConfig,
  };
}
```

**Create file: `frontend/src/app/core/api/api-url.ts`**

```ts
import { Injectable, inject } from '@angular/core';
import { API_CONFIG } from './api-config';

const trimLeadingSlashes = (path: string) => path.replace(/^\/+/, '');

/** The single place that builds backend URLs. Feature API services must use it. */
@Injectable({ providedIn: 'root' })
export class ApiUrl {
  private readonly config = inject(API_CONFIG);

  /** Business resources: versioned('tickets') -> '/api/v1/tickets'. */
  versioned(path: string): string {
    return `${this.config.baseUrl}/${this.config.version}/${trimLeadingSlashes(path)}`;
  }

  /** Infrastructure endpoints: unversioned('health') -> '/api/health'. */
  unversioned(path: string): string {
    return `${this.config.baseUrl}/${trimLeadingSlashes(path)}`;
  }
}
```

**Create file: `frontend/src/app/core/api/problem-details.ts`**

```ts
/** Mirrors ASP.NET Core ProblemDetails / ValidationProblemDetails (application/problem+json). */
export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  instance?: string;
  traceId?: string;
  errors?: Record<string, string[]>;
}
```

**Create file: `frontend/src/app/core/api/health-api.ts`**

```ts
import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ApiUrl } from './api-url';

/** Transport contract for GET /api/health (matches Crm.Api HealthResponse). */
export interface HealthResponse {
  status: string;
  service: string;
  checkedAtUtc: string;
}

@Injectable({ providedIn: 'root' })
export class HealthApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  getHealth(): Observable<HealthResponse> {
    return this.http.get<HealthResponse>(this.apiUrl.unversioned('health'));
  }
}
```

**File: `frontend/src/app/app.config.ts`** — keep every provider `ng new` generated; add `provideHttpClient(withFetch())` (from `@angular/common/http`) and `provideApiConfig()` to the `providers` array.

### 11 — Visual foundation (Angular Material M3 theme + tokens)

Design direction (record in `docs/frontend-ui-foundation.md`): calm, professional support-desk look; violet primary (echoes the purple of the CRM attachment), cyan tertiary for informational accents; Inter typography; 4/8 px spacing scale; 12 px radius cards; follows the OS light/dark preference via M3 `light-dark()` tokens; side navigation on desktop, overlay drawer on handset.

**File: `frontend/src/index.html`** — set `<title>Support CRM</title>`, keep `lang="en"`, add inside `<head>`:

```html
<link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
<link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap" rel="stylesheet">
<link href="https://fonts.googleapis.com/icon?family=Material+Icons" rel="stylesheet">
```

**File: `frontend/src/styles.scss`** — replace with:

```scss
@use '@angular/material' as mat;

html {
  color-scheme: light dark;
  @include mat.theme((
    color: (
      primary: mat.$violet-palette,
      tertiary: mat.$cyan-palette,
    ),
    typography: Inter,
    density: 0,
  ));
}

:root {
  --crm-space-1: 4px;
  --crm-space-2: 8px;
  --crm-space-3: 12px;
  --crm-space-4: 16px;
  --crm-space-6: 24px;
  --crm-space-8: 32px;
  --crm-radius-md: 12px;
  --crm-radius-pill: 999px;
  --crm-nav-inline-size: 264px;
  --crm-content-max-inline-size: 1200px;
  --crm-success: light-dark(#1b6e3a, #7ad99a);
  --crm-success-container: light-dark(#d7f5e0, #123f20);
}

html,
body {
  block-size: 100%;
}

body {
  margin: 0;
  font-family: var(--mat-sys-body-large-font);
  background: var(--mat-sys-surface);
  color: var(--mat-sys-on-surface);
}
```

**CSS rule for all component styles in this story and later:** use logical properties (`padding-inline`, `margin-block`, `inset-inline-start`, `inline-size`, `text-align: start`) — **never** `left`/`right`/`margin-left`/`padding-right`/`text-align: left`. Do not add `dir` attributes or RTL logic.

### 12 — Shared UI building blocks

**Create file: `frontend/src/app/shared/ui/page-header/page-header.ts`** — standalone component, selector `crm-page-header`, `ChangeDetectionStrategy.OnPush`, inputs `title = input.required<string>()` and `subtitle = input<string>()`. Template: `<header class="page-header"><h1>{{ title() }}</h1>@if (subtitle()) {<p>{{ subtitle() }}</p>}</header>`. Styles: `h1` uses `font: var(--mat-sys-headline-medium)`, `margin-block: 0 var(--crm-space-2)`; `p` uses `font: var(--mat-sys-body-large)` and `color: var(--mat-sys-on-surface-variant)`, `margin: 0`; header `margin-block-end: var(--crm-space-6)`.

**Create file: `frontend/src/app/shared/ui/status-badge/status-badge.ts`** — standalone, selector `crm-status-badge`, OnPush. Export `type StatusTone = 'success' | 'error' | 'pending' | 'neutral'`. Inputs: `label = input.required<string>()`, `tone = input<StatusTone>('neutral')`. Host binding `[attr.data-tone]: tone()`, `role="status"`. Template: `<span class="dot" aria-hidden="true"></span>{{ label() }}`. Styles: `:host { display: inline-flex; align-items: center; gap: var(--crm-space-2); padding-block: var(--crm-space-1); padding-inline: var(--crm-space-3); border-radius: var(--crm-radius-pill); font: var(--mat-sys-label-large); }` and per tone:
- `success` → background `var(--crm-success-container)`, color `var(--crm-success)`
- `error` → `var(--mat-sys-error-container)` / `var(--mat-sys-on-error-container)`
- `pending` → `var(--mat-sys-tertiary-container)` / `var(--mat-sys-on-tertiary-container)`
- `neutral` → `var(--mat-sys-surface-container-high)` / `var(--mat-sys-on-surface-variant)`
- `.dot` is an 8 px circle with `background: currentColor`.

### 13 — Application shell, navigation and routing

**Create file: `frontend/src/app/core/layout/navigation.ts`**

```ts
export interface NavItem {
  readonly label: string;
  readonly icon: string; // Material Icons ligature name
  readonly path: string;
  readonly exact: boolean;
}

/** Feature stories append their entry here when their first screen ships. */
export const NAV_ITEMS: readonly NavItem[] = [
  { label: 'Home', icon: 'home', path: '/', exact: true },
];
```

Do **not** add placeholder entries for unbuilt modules.

**Create file: `frontend/src/app/core/layout/shell/shell.ts`** (+ `shell.html`, `shell.scss`) — standalone component `Shell`, selector `crm-shell`, OnPush. Imports: `RouterOutlet`, `RouterLink`, `RouterLinkActive`, `MatSidenavModule`, `MatToolbarModule`, `MatListModule`, `MatIconModule`, `MatButtonModule`. Fields:

```ts
protected readonly navItems = NAV_ITEMS;
protected readonly isHandset = toSignal(
  inject(BreakpointObserver).observe(Breakpoints.Handset).pipe(map(result => result.matches)),
  { initialValue: false },
);
```

`shell.html`:

```html
<mat-sidenav-container class="shell">
  <mat-sidenav
    #drawer
    class="shell__nav"
    [mode]="isHandset() ? 'over' : 'side'"
    [opened]="!isHandset()">
    <div class="shell__brand">
      <mat-icon aria-hidden="true">support_agent</mat-icon>
      <span>Support CRM</span>
    </div>
    <mat-nav-list aria-label="Main navigation">
      @for (item of navItems; track item.path) {
        <a
          mat-list-item
          [routerLink]="item.path"
          routerLinkActive
          #rla="routerLinkActive"
          [routerLinkActiveOptions]="{ exact: item.exact }"
          [activated]="rla.isActive"
          (click)="isHandset() && drawer.close()">
          <mat-icon matListItemIcon aria-hidden="true">{{ item.icon }}</mat-icon>
          <span matListItemTitle>{{ item.label }}</span>
        </a>
      }
    </mat-nav-list>
  </mat-sidenav>

  <mat-sidenav-content>
    <mat-toolbar class="shell__toolbar">
      @if (isHandset()) {
        <button mat-icon-button type="button" aria-label="Open navigation" (click)="drawer.toggle()">
          <mat-icon>menu</mat-icon>
        </button>
      }
      <span class="shell__title">Customer Support CRM</span>
    </mat-toolbar>
    <main class="shell__content">
      <router-outlet />
    </main>
  </mat-sidenav-content>
</mat-sidenav-container>
```

`shell.scss` (logical properties only): `.shell { block-size: 100dvh; }`; `.shell__nav { inline-size: var(--crm-nav-inline-size); background: var(--mat-sys-surface-container-low); border-inline-end: 1px solid var(--mat-sys-outline-variant); }`; `.shell__brand { display: flex; align-items: center; gap: var(--crm-space-3); padding-block: var(--crm-space-6) var(--crm-space-4); padding-inline: var(--crm-space-6); font: var(--mat-sys-title-large); color: var(--mat-sys-primary); }`; `.shell__toolbar { position: sticky; inset-block-start: 0; z-index: 2; background: var(--mat-sys-surface); border-block-end: 1px solid var(--mat-sys-outline-variant); gap: var(--crm-space-2); }`; `.shell__title { font: var(--mat-sys-title-medium); }`; `.shell__content { box-sizing: border-box; max-inline-size: var(--crm-content-max-inline-size); margin-inline: auto; padding: var(--crm-space-6); }` and at `@media (max-width: 599.98px) { .shell__content { padding: var(--crm-space-4); } }`.

**File: `frontend/src/app/app.html`** — replace the entire placeholder template with `<router-outlet />`. Empty `frontend/src/app/app.scss`. Keep `app.ts` importing `RouterOutlet`.

**File: `frontend/src/app/app.routes.ts`** — replace with:

```ts
import { Routes } from '@angular/router';
import { Shell } from './core/layout/shell/shell';

export const routes: Routes = [
  {
    path: '',
    component: Shell,
    children: [
      { path: '', loadChildren: () => import('./features/home/home.routes').then(m => m.HOME_ROUTES) },
      // Feature stories add: { path: '<feature>', loadChildren: () => import('./features/<feature>/<feature>.routes') ... }
    ],
  },
  { path: '**', redirectTo: '' },
];
```

### 14 — Home feature (connectivity demonstration)

**Create file: `frontend/src/app/features/home/home.routes.ts`**

```ts
import { Routes } from '@angular/router';

export const HOME_ROUTES: Routes = [
  { path: '', title: 'Home · Support CRM', loadComponent: () => import('./home-page').then(m => m.HomePage) },
];
```

**Create file: `frontend/src/app/features/home/home-page.ts`** (+ `home-page.html`, `home-page.scss`) — standalone `HomePage`, selector `crm-home-page`, OnPush. Imports: `PageHeader`, `StatusBadge`, `MatCardModule`, `MatButtonModule`, `MatIconModule`, `DatePipe`.

State model and logic:

```ts
type ConnectionState =
  | { kind: 'checking' }
  | { kind: 'online'; health: HealthResponse }
  | { kind: 'offline'; message: string };

private readonly healthApi = inject(HealthApi);
private readonly destroyRef = inject(DestroyRef);
protected readonly connection = signal<ConnectionState>({ kind: 'checking' });

constructor() { this.checkConnection(); }

protected checkConnection(): void {
  this.connection.set({ kind: 'checking' });
  this.healthApi.getHealth()
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: health => this.connection.set({ kind: 'online', health }),
      error: (error: HttpErrorResponse) => this.connection.set({ kind: 'offline', message: describeError(error) }),
    });
}
```

Module-level helper in the same file:

```ts
function describeError(error: HttpErrorResponse): string {
  if (error.status === 0) {
    return 'The API is not reachable. Start the backend and try again.';
  }
  const problem = error.error as ProblemDetails | null;
  return problem?.title ?? `The API responded with status ${error.status}.`;
}
```

`home-page.html`:
- `<crm-page-header title="Welcome to Support CRM" subtitle="Your workspace for customers, tickets and conversations. Modules appear here as they are delivered." />`
- One `<mat-card appearance="outlined" class="status-card">` with `<mat-card-header><mat-card-title>System status</mat-card-title></mat-card-header>` and `<mat-card-content>` using `@switch (connection().kind)`:
  - `checking` → `<crm-status-badge label="Checking…" tone="pending" />`
  - `online` → `<crm-status-badge label="API connected" tone="success" />` plus `<p>` "{{ health.service }} reported {{ health.status }} at {{ health.checkedAtUtc | date: 'medium' }}." (bind `health` via `@let` or a typed accessor)
  - `offline` → `<crm-status-badge label="API unavailable" tone="error" />` plus `<p>{{ message }}</p>`
- `<mat-card-actions align="end"><button mat-stroked-button type="button" (click)="checkConnection()" [disabled]="connection().kind === 'checking'"><mat-icon>refresh</mat-icon> Check again</button></mat-card-actions>`

`home-page.scss`: `.status-card { max-inline-size: 560px; border-radius: var(--crm-radius-md); }`, `mat-card-content p { margin-block: var(--crm-space-3) 0; color: var(--mat-sys-on-surface-variant); }`.

### 15 — Frontend unit test (single happy path)

**Create file: `frontend/src/app/core/api/api-url.spec.ts`**

```ts
import { TestBed } from '@angular/core/testing';
import { API_CONFIG } from './api-config';
import { ApiUrl } from './api-url';

describe('ApiUrl', () => {
  it('builds versioned business routes from the central config', () => {
    TestBed.configureTestingModule({
      providers: [{ provide: API_CONFIG, useValue: { baseUrl: '/api', version: 'v1' } }],
    });

    const apiUrl = TestBed.inject(ApiUrl);

    expect(apiUrl.versioned('tickets')).toBe('/api/v1/tickets');
  });
});
```

This is the **only** frontend spec file in the project after this story.

### 16 — Documentation

**Create file: `README.md`** (repository root) with these sections, in order:
1. **Customer Support CRM** — one paragraph; link to `docs/`.
2. **Repository layout** — tree of `backend/` (`src/Crm.Domain|Application|Infrastructure|Api`, `tests/Crm.UnitTests`), `frontend/` (`src/app/core|shared|features`), `.squad/`, `docs/`. State the dependency rule: Domain ← Application ← Infrastructure; Api composes Application + Infrastructure; nothing points outward.
3. **Prerequisites** — .NET SDK 10.0.400+, Node 24 LTS (or 22 LTS) + npm, SQL Server LocalDB (or any SQL Server 2019+), `dotnet tool install --global dotnet-ef --version 10.0.13`, squad-kit (optional, for planning).
4. **Local SQL Server** — the `dotnet user-secrets set "ConnectionStrings:CrmDatabase" …` command from task 7; note for full SQL Server use `Server=localhost;Database=CrmDev;Trusted_Connection=True;TrustServerCertificate=True` or SQL auth **only via user secrets/env var `ConnectionStrings__CrmDatabase`**; never commit credentials.
5. **Database migrations** — `database update`, `migrations add <Name>`, `migrations remove` commands from task 7 including `-- --environment Development`; migrations live in `backend/src/Crm.Infrastructure/Persistence/Migrations`.
6. **Run the backend** — `cd backend && dotnet run --project src/Crm.Api --launch-profile http` → `http://localhost:5080/api/health`, OpenAPI `http://localhost:5080/openapi/v1.json`, Scalar UI `http://localhost:5080/scalar`.
7. **Run the frontend** — `cd frontend && npm install && npm start` → `http://localhost:4200` (proxy forwards `/api` to `:5080`).
8. **Tests** — `cd backend && dotnet test`; `cd frontend && npm test -- --watch=false`. State the policy: happy-path unit tests only.
9. **Conventions** — short bullets linking to the three docs below.
10. **Spec-driven workflow (squad-kit)** — `squad new-story <feature-slug>` → fill `intake.md` → `/squad-plan <intake path>` → plan in `.squad/plans/<feature>/NN-story-*.md` → implement in a fresh session attaching only the plan. Future capabilities (roles/permissions, localization/RTL, departments/branches) each need their own intake.

**Create file: `docs/api-conventions.md`** — the API contract policy, verbatim rules:
- Business routes `/api/v1/{resource}` — plural, lowercase (`ApiRoutes.V1`, `LowercaseUrls = true`); `GET /api/health` is the only unversioned route; no `v2`.
- DTOs: PascalCase C# records ending in `Request` / `Response`, in `Crm.Api/Contracts/<Feature>/`; JSON camelCase (ASP.NET Core web defaults); never expose Domain entities or EF models.
- Success: direct DTO, no envelope; `200 OK` reads/updates, `201 Created` + `Location` (`CreatedAtAction`) on create, `204 No Content` when no body.
- Errors: `application/problem+json` only, produced centrally by `GlobalExceptionHandler` and `[ApiController]` model validation. Mapping table: FluentValidation `ValidationException` → 400 `ValidationProblemDetails` (camelCase error keys); `NotFoundException` → 404; `DomainException` → 422; anything else → 500 (detail only in Development). Every problem includes `traceId`.
- Controllers are thin: map request → command/query, `ISender.Send`, map result → response. No business or persistence logic.
- Pagination/sorting/filtering: **deferred** to the first list-endpoint feature intake.
- Angular: all URLs via `ApiUrl` (`versioned()` / `unversioned()`); typed transport interfaces live next to the feature's API service.

**Create file: `docs/persistence-and-transactions.md`** — the transaction policy:
- One request = one scoped `CrmDbContext`, shared by handlers, repositories and `EfUnitOfWork`.
- **Default (single save):** handler stages Add/Update/Remove, calls `IUnitOfWork.SaveChangesAsync` exactly once; EF Core's implicit transaction makes it atomic. No explicit transaction, no marker.
- **Multi-step writes:** the command implements `ITransactionalCommand`; `TransactionBehavior` (innermost pipeline behavior) calls `IUnitOfWork.ExecuteInTransactionAsync`, which runs the handler inside the SQL Server execution strategy → `BeginTransactionAsync` → handler → final `SaveChangesAsync` → `CommitAsync`; any exception/cancellation → `RollbackAsync` and rethrow; nothing commits early. Nested transactional commands join the outer transaction.
- **Retries:** `EnableRetryOnFailure` is on; therefore explicit transactions **must** go through `ExecuteInTransactionAsync` (the execution strategy retries the whole unit, clearing the change tracker first). Handlers of transactional commands must not call external services, because a retry replays the handler.
- **Queries** never open write transactions (`TransactionBehavior` passes them through; prefer `AsNoTracking` in future query repositories).
- **Ownership:** contract in `Crm.Application/Abstractions/Persistence/IUnitOfWork.cs`; EF implementation in `Crm.Infrastructure/Persistence/EfUnitOfWork.cs`; controllers and Domain never begin/commit/rollback.
- **Boundary:** atomicity covers one local SQL Server database only. Email/WhatsApp/SMS/payment calls are outside it; a future story needing DB + message reliability must specify an outbox.
- Do not add generic repositories that wrap `DbSet` 1:1; add narrow per-aggregate interfaces only when a feature needs them.

**Create file: `docs/frontend-ui-foundation.md`** — record: Angular Material 22 chosen (accessible components, M3 tokens, bidi-ready); theme (violet primary, cyan tertiary, Inter, follows OS light/dark); spacing/radius tokens in `styles.scss`; folder rules (`core/` = app-wide singletons: API config, layout, interceptors; `shared/ui/` = presentational building blocks; `features/<name>/` = routes, pages, API service, models for that feature, lazy-loaded via `<name>.routes.ts`); logical-CSS-properties rule; nav entries added to `NAV_ITEMS` only when a screen ships; future screens are designed per feature intake from real data — no placeholder pages.

---

## Edge Cases & Failure Modes

- **Connection string not configured** — the API still starts and `/api/health` returns 200 because `CrmDbContext` options are built lazily (task 5 `AddInfrastructure`) and `TransactionBehavior` resolves `IUnitOfWork` only for `ITransactionalCommand` messages (task 4). The first DB-touching request throws the `InvalidOperationException` from `AddInfrastructure`, which `GlobalExceptionHandler` maps to 500 ProblemDetails. `dotnet ef` commands fail with the same message — fix with task 7 step 1.
- **`dotnet ef` run without `-- --environment Development`** — user secrets are not loaded; the command fails with the "not configured" message. Documented in README section 5.
- **LocalDB not installed / different SQL Server** — use the alternate connection string in README section 4; `TrustServerCertificate=True` is required for local self-signed certificates with `Microsoft.Data.SqlClient` 5+.
- **`dotnet-ef` tool 10.0.12 vs packages 10.0.13** — EF prints a version-mismatch warning only; commands still work. README prerequisites tell new machines to install 10.0.13.
- **Transient SQL failure inside a transactional command** — `EfUnitOfWork.ExecuteInTransactionAsync` retries the entire delegate via the execution strategy after `ChangeTracker.Clear()`; partial writes are rolled back.
- **Commit succeeds but the connection drops before acknowledgement** — the retry could re-apply the work. Out of scope for ARCH-001 (no business writes exist); `docs/persistence-and-transactions.md` notes that commands needing exactly-once semantics must be idempotent or check state.
- **Request cancelled mid-transaction** — `OperationCanceledException` propagates; `catch` calls `RollbackAsync(CancellationToken.None)` and the `await using` disposes the transaction. Nothing commits.
- **Nested transactional command** — `Database.CurrentTransaction` is non-null, so the inner call joins; only the outermost call commits.
- **Mediator 3 options API** — `AddMediator((MediatorOptions options) => …)` with `Assemblies`, `PipelineBehaviors`, `ServiceLifetime` is the 3.x configuration surface. If the build reports that `Assemblies` or `PipelineBehaviors` does not accept the given value, check the package README at `~/.nuget/packages/mediator.sourcegenerator/3.0.2/` and use the documented equivalent (e.g. `typeof(GetSystemStatusQuery).Assembly`); do not switch libraries. Handlers must be `public` or the generator will not see them.
- **Mediator handler not found at runtime** (`MissingMessageHandlerException`) — the source generator is not referenced by `Crm.Api` or `Assemblies` does not include `Crm.Application`. Fix in task 6 `AddPresentation`.
- **xUnit analyzer `xUnit1051`** (if the template generates xUnit v3) — it suggests `TestContext.Current.CancellationToken` instead of `CancellationToken.None`. It is a warning, not an error; either form is acceptable.
- **Backend not running when the home page loads** — `HttpErrorResponse.status === 0` → "API unavailable" badge and "The API is not reachable. Start the backend and try again."; the "Check again" button retries (task 14).
- **Port 5080 in use** — change it in `launchSettings.json` **and** `frontend/proxy.conf.json` only; no other file contains the port.
- **Material bundle size** — `ng build` can emit an initial-bundle budget **warning**; it is not an error. Do not raise budgets unless the build errors.
- **Handset layout** — `BreakpointObserver` switches the sidenav to `over` mode and shows the menu button; navigation clicks close the drawer.
- **Attachment PDF is gitignored** (squad-kit block in `.gitignore`) — expected; do not change that block.

---

## Test Plan

1. **Unit (backend) — add:** `backend/tests/Crm.UnitTests/Diagnostics/GetSystemStatusQueryHandlerTests.cs` → `Handle_ReturnsHealthyStatus`. Delete `UnitTest1.cs`.
2. **Unit (frontend) — add:** `frontend/src/app/core/api/api-url.spec.ts` → "builds versioned business routes from the central config". Delete `frontend/src/app/app.spec.ts`.
3. **Smoke (manual, happy path only, not automated):** run both apps and confirm the home page shows "API connected".

No other tests, test projects, mocking libraries, integration/SQL/API/E2E/snapshot suites are added. No negative-path or edge-case scenarios (automated or manual) are part of this story's verification; the error-handling and rollback code in tasks 5, 6 and 14 is still implemented as specified.

---

## Verification Steps

1. **Backend builds and tests:** in `backend/` run `dotnet build` then `dotnet test` — 0 errors, 1 test passed.
2. **Layer direction:** in `backend/` run `dotnet list src/Crm.Domain/Crm.Domain.csproj reference` (none), `dotnet list src/Crm.Application/Crm.Application.csproj reference` (Domain only), `dotnet list src/Crm.Infrastructure/Crm.Infrastructure.csproj reference` (Application only); and `dotnet list src/Crm.Application/Crm.Application.csproj package` shows no `Microsoft.EntityFrameworkCore*`.
3. **Database:** in `backend/` run `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development`, then `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT MigrationId FROM __EFMigrationsHistory"` — returns one `…_InitialCreate` row.
4. **Backend health endpoint:** `dotnet run --project src/Crm.Api --launch-profile http`; `curl -s http://localhost:5080/api/health` → HTTP 200 `{"status":"Healthy","service":"Crm.Api","checkedAtUtc":"…"}`.
5. **Frontend builds and tests:** in `frontend/` run `npx ng build` (no errors) and `npm test -- --watch=false` (1 test passed).
6. **Frontend connects to backend:** with the API running, `npm start` in `frontend/`, open `http://localhost:4200` — shell with "Support CRM" navigation and a "System status" card showing "API connected".
7. **Architecture and configuration checks:** `git status --short` from the root shows no `bin/`, `obj/`, `node_modules/`, `.angular/` entries; `grep -rniE "password|user id=" backend/src frontend/src` returns nothing; `grep -rnE "(margin|padding)-(left|right)|text-align: *(left|right)" frontend/src` returns nothing; with the API running, `curl -s http://localhost:5080/openapi/v1.json` lists `/api/health`; `grep -rn "5080" frontend/src` returns nothing (port only in `frontend/proxy.conf.json` and `launchSettings.json`).

No negative-path checks (stopped backend, unknown routes, invalid input, failed transactions) are run.

---

## Done Criteria

- [ ] One Git repository at the root contains `backend/`, `frontend/`, `.squad/`, `docs/` and `README.md`; `.gitignore` extends (not edits) the squad-kit block.
- [ ] `backend/Crm.slnx` builds; `Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api` exist with inward-only references (Verification 2).
- [ ] `GET /api/health` returns 200 JSON via `HealthController` → `ISender` → `GetSystemStatusQueryHandler` (CQRS + mediator demonstrated).
- [ ] `CrmDbContext` in Infrastructure; `InitialCreate` migration committed; README explains clean-machine DB initialization.
- [ ] `IUnitOfWork` (Application) + `EfUnitOfWork` (Infrastructure) + `ITransactionalCommand` + `TransactionBehavior` implement the policy: single save by default, one explicit execution-strategy-wrapped transaction for multi-step commands, rollback on failure/cancellation, none for queries; documented in `docs/persistence-and-transactions.md` including the single-database boundary and retry interaction.
- [ ] DI composition in `Program.cs` via `AddApplication` / `AddInfrastructure` / `AddPresentation`; `GlobalExceptionHandler` + `AddProblemDetails`; structured console logging (JSON outside Development); OpenAPI document + Scalar in Development.
- [ ] `docs/api-conventions.md` records `/api/v1/{resource}`, unversioned `/api/health`, `…Request`/`…Response` DTOs, camelCase JSON, direct-DTO success codes, ProblemDetails errors, deferred pagination. No business endpoints/DTOs and no `v2`.
- [ ] Angular uses `API_CONFIG` + `ApiUrl` as the single URL source; `HealthResponse` is a typed transport interface; host/port appears only in `proxy.conf.json`.
- [ ] No secrets committed; connection string lives in user secrets.
- [ ] Angular app builds and starts; `core/`, `shared/`, `features/` folders; shell with routing and lazy `home` feature.
- [ ] Material M3 theme, tokens and shared `PageHeader` / `StatusBadge` implemented; decisions recorded in `docs/frontend-ui-foundation.md`; responsive on handset.
- [ ] Home page calls `/api/health` through `HealthApi` and displays "API connected" when the backend is running (Verification 6). The checking/unavailable states are implemented per task 14 but not verified in this story.
- [ ] `dotnet build` and `npx ng build` both succeed (Verification 1, 5).
- [ ] EF Core migrations apply successfully to the local database (Verification 3).
- [ ] Exactly one backend and one frontend happy-path unit test, both passing; no negative-path, edge-case, integration, API, SQL or E2E tests or manual negative checks were added.
- [ ] README covers prerequisites, layout, run/build/test, SQL Server, migrations and the squad-kit workflow.
- [ ] Styles use CSS logical properties only (Verification 7); no RTL/localization code.
- [ ] No roles/permissions, localization, department/branch, or CRM business entities/screens/endpoints were planned or implemented.
- [ ] `.squad/plans/project-architecture/00-overview.md` lists this story.

**STOP HERE. Report to the user and wait for confirmation before proceeding to Story 02.**
