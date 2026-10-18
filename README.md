# Customer Support CRM

A full-stack Customer Support CRM built spec-first with squad-kit: an ASP.NET Core (.NET 10) REST API in Clean Architecture with CQRS, EF Core and SQL Server, plus an Angular 22 + Angular Material frontend. This repository contains the architectural foundation (`ARCH-001`), identity & access (`AUTH-001`), customer profiles & contacts (`CUST-001`), ticket creation & management (`TICK-001`), ticket workflow, assignment & history (`TICK-002`) and the Agent Dashboard (`DASH-001`), agent productivity & collaboration (`DASH-002`), customer notes, interactions & attachments (`CUST-002`) SLA & basic automation (`SLA-001`) and Knowledge Base management (`KB-001`); CRM business features arrive as separate stories. See [docs/](docs/) for the conventions.

## Repository layout

```
backend/
  Crm.slnx
  src/
    Crm.Domain/          domain rules (no dependencies)
    Crm.Application/     use cases, CQRS messages/handlers, validation, abstractions
    Crm.Infrastructure/  EF Core, SQL Server, IUnitOfWork implementation
    Crm.Api/             controllers, contracts, error mapping, composition root
  tests/Crm.UnitTests/   happy-path unit tests
frontend/
  src/app/core/          app-wide infrastructure (API config, layout)
  src/app/shared/        reusable UI building blocks
  src/app/features/      one folder per business feature (lazy-loaded)
.squad/                  squad-kit stories and plans
docs/                    conventions and decisions
```

Dependency rule: Domain <- Application <- Infrastructure; Api composes Application + Infrastructure. Nothing points outward.

Feature folder convention (backend): `Crm.Application/<Feature>/<UseCase>/` holding `<UseCase>Command|Query.cs`, the handler and `<UseCase>Validator.cs`. Per-aggregate repository interfaces go in `Crm.Application/Abstractions/Persistence/` when a feature needs them.

## Prerequisites

- .NET SDK 10.0.400+
- Node 24 LTS (or 22 LTS) and npm
- SQL Server LocalDB (or any SQL Server 2019+)
- EF Core CLI: `dotnet tool install --global dotnet-ef --version 10.0.13`
- squad-kit (optional, for planning)

## Local SQL Server

The connection string is stored as a .NET user secret (outside the repository):

```bash
cd backend
dotnet user-secrets set "ConnectionStrings:CrmDatabase" "Server=(localdb)\MSSQLLocalDB;Database=CrmDev;Trusted_Connection=True;TrustServerCertificate=True" --project src/Crm.Api
```

For a full SQL Server use `Server=localhost;Database=CrmDev;Trusted_Connection=True;TrustServerCertificate=True`. If SQL authentication is needed, supply it only through user secrets or the `ConnectionStrings__CrmDatabase` environment variable. Never commit credentials.

### Authentication secrets

```powershell
cd backend
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64))) --project src/Crm.Api
dotnet user-secrets set "Bootstrap:SuperAdmin:Password" "<initial-superadmin-password>" --project src/Crm.Api
```

On first start with an empty database the API creates the three roles and `admin@admin.com`. Sign in with the configured password; no password change is forced.

## Database migrations

The trailing `-- --environment Development` is required so user secrets are loaded.

```bash
cd backend
# create / update the database
dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development
# add a migration
dotnet ef migrations add <Name> --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations -- --environment Development
# remove the last (unapplied) migration
dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development
```

Migrations live in `backend/src/Crm.Infrastructure/Persistence/Migrations`.

## Run the backend

```bash
cd backend
dotnet run --project src/Crm.Api --launch-profile http
```

- Health: http://localhost:5080/api/health
- OpenAPI: http://localhost:5080/openapi/v1.json
- Scalar UI: http://localhost:5080/scalar

## Run the frontend

```bash
cd frontend
npm install
npm start
```

Open http://localhost:4200. The dev proxy forwards `/api` to `http://localhost:5080`.

## Tests

```bash
cd backend && dotnet test
cd frontend && npm test -- --watch=false
```

Policy: happy-path unit tests only.

## Conventions

- [API conventions](docs/api-conventions.md)
- [Persistence and transactions](docs/persistence-and-transactions.md)
- [Frontend UI foundation](docs/frontend-ui-foundation.md)
- [Identity and access](docs/identity-and-access.md)
- [Customer management](docs/customer-management.md)
- [Ticket management](docs/ticket-management.md)
- [Agent dashboard](docs/agent-dashboard.md)
- [Agent productivity and collaboration](docs/agent-productivity-and-collaboration.md)

## Spec-driven workflow (squad-kit)

`squad new-story <feature-slug>` -> fill in `intake.md` -> `/squad-plan <intake path>` -> plan in `.squad/plans/<feature>/NN-story-*.md` -> implement in a fresh session attaching only the plan. Future capabilities (roles/permissions, localization/RTL, departments/branches) each need their own intake.
