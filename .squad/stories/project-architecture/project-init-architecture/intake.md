# Project Init Architecture — Story Intake

## Feature

- **Feature name (display):** Project Architecture & Initial Setup
- **Feature slug (folder under `plans/`):** `project-architecture`

## Tracker

- **Tracker type:** `none`
- **Work item id:** `ARCH-001`
- **Work item type:** `Technical / Architecture Story`
- **Status:** `Draft`
- **Assignee:** `Unassigned`
- **Labels:** `architecture, project-setup, monorepo, aspnet-core, angular, cqrs, clean-architecture, ef-core, sql-server, database-transactions, api-contract, api-versioning, future-compatibility, ai-led-ui-design, happy-path-unit-tests, squad-kit`

## Title

```
Project Init Architecture
```

---

## Description

```
Goal
Set up the technical foundation for a simple full-stack Customer Support CRM using Spec-Driven Development (SDD) with Squad-Kit. This is an architecture and scaffolding story, not a CRM business-feature implementation story.

1. Repository and SDD structure
- Use one Git repository with the backend and frontend under the same repository root.
- Place .NET work in backend/ and Angular work in frontend/.
- Preserve the Squad-Kit .squad/ story and planning workflow in the same repository; use the current intake as the source for generating the architecture plan.
- Keep shared project setup instructions in a root README.md.
- Establish meaningful .gitignore rules for .NET, Angular/Node, local build outputs, and machine-specific or secret configuration.

2. Backend technology and architecture
- Use C# with .NET ASP.NET Core to expose versionable RESTful HTTP APIs. Do not mandate an unconfirmed .NET or package version in this intake; select mutually compatible supported versions during planning.
- Apply Clean Architecture with four clearly separated projects/layers:
  * Domain: core domain models and domain rules; no dependence on external frameworks or other application layers.
  * Application: use cases, CQRS requests/handlers, interfaces/abstractions, and validation; depends on Domain, not Infrastructure.
  * Infrastructure: Entity Framework Core persistence, SQL Server access, and implementations of Application abstractions.
  * API: HTTP controllers/endpoints, request/response contracts, error mapping, and dependency-injection composition root.
- Enforce inward-pointing dependency direction; controllers must not contain business or persistence logic.
- Use CQRS to separate commands (write operations) and queries (read operations), dispatched to their respective handlers through a Mediator pattern. The plan may choose a compatible mediator library or documented equivalent without inventing a mandatory package.
- Establish the following lightweight, consistent API contract policy now; future business endpoints must follow it when their separate feature intakes are implemented:
  * Route versioning: business API routes follow `/api/v1/{resource}` (plural, lowercase, predictable resource names); do not build or plan `v2` or a migration strategy in this story. Infrastructure-only `GET /api/health` remains intentionally unversioned as `/api/health`.
  * DTO naming: C# request/response contracts use clear PascalCase names ending in `Request` and `Response` (e.g. `CreateTicketRequest`, `TicketResponse`); JSON property names use camelCase. Keep API DTOs distinct from Domain entities and EF Core persistence models. Do not create these sample CRM DTOs as real business features now.
  * Success responses: return the relevant JSON DTO directly (no unnecessary universal response envelope), with appropriate HTTP status codes: `200 OK` for normal reads/updates, `201 Created` and `Location` when creating a resource, and `204 No Content` where no response body is needed.
  * Error responses: use ASP.NET Core `ProblemDetails`/`ValidationProblemDetails` consistently for validation and other errors (including meaningful status, title, detail, and validation errors when applicable). Centralize exception handling rather than returning ad hoc error payloads.
  * OpenAPI: expose and document the active endpoints and these conventions; a separate API-versioning framework is optional, not required solely to establish the `/api/v1` convention.
  * Angular: keep the backend base URL and API path/version settings in one API client/configuration location; use typed request/response interfaces matching the API's JSON contracts, not EF Core entities.
- Do not finalize list pagination, sorting, searching, or filtering policies here. The first CRM story with a collection/list endpoint will establish those conventions in its own intake.
- Register dependencies with ASP.NET Core dependency injection; use structured application logging and environment-specific configuration.

3. Data access and local database
- Use Entity Framework Core as the ORM and Microsoft SQL Server as the local development database.
- Define a DbContext in Infrastructure and isolate EF Core/SQL Server details from Domain and Application.
- Support EF Core migrations with documented commands for creating/updating the local database.
- Read connection strings and other secrets from local environment configuration or .NET user secrets; do not commit credentials.
- Require ACID atomicity for every logical command-side database change: if a command modifies multiple related rows/tables, all intended database writes must commit together or none may persist.
- For writes persisted by one SaveChangesAsync() call, retain EF Core's default transactional behavior; prefer collecting related Add/Update/Delete changes in one scoped DbContext and saving once. Do not add redundant explicit transactions to every simple save.
- If a single business command needs multiple SaveChangesAsync() calls or multiple dependent database-write steps, enclose all of them in one explicit EF Core/SQL Server transaction; commit only after all required steps succeed, and roll back/dispose without committing if any required step fails or is cancelled. Never allow successful early saves to be committed independently of a later failing step.
- Establish a reusable, well-defined transactional command/unit-of-work boundary: declare the transaction orchestration contract in Application (or use an equivalent application-facing command pipeline) and implement EF Core-specific behavior in Infrastructure. Use a consistently scoped DbContext for the affected writes. Do not put transaction orchestration in controllers or Domain entities, and avoid unnecessary repository/unit-of-work abstractions that merely duplicate EF Core without a clear purpose.
- Do not require write transactions for read-only CQRS queries. Do not disable EF Core automatic transactions. If SQL Server transient-failure retries are enabled, ensure explicitly managed transactions cooperate with EF Core execution strategies so the full transaction is retried as a unit; never blindly retry only part of a multi-step write.
- Transactional atomicity in this story is scoped to one local SQL Server database; calls to email, WhatsApp, payment services, and other external systems are not covered by a SQL transaction. Future stories requiring reliable database-plus-message changes should specify an outbox/event-delivery design separately.
- This story sets up database infrastructure and the reusable transaction strategy only; CRM business entities/tables belong to later feature stories.

4. Frontend technology and architecture
- Use Angular with TypeScript for the frontend application.
- Organize Angular by feature, with app/core/ for application-wide infrastructure, app/shared/ for reusable UI/utilities, and app/features/ for separately owned business feature areas as they are implemented.
- Keep feature-specific components, routes, API-facing services, and models within the owning feature where practical; avoid a single monolithic shared feature module.
- Establish basic routing, an application shell, and a centralized configuration/API access mechanism.
- Angular must communicate with ASP.NET Core over HTTP/JSON; it must never access SQL Server directly.
- Configure the local frontend-to-backend development connection (e.g. a dev proxy or appropriately restricted CORS configuration) without hardcoding machine-specific URLs throughout the application.
- No UI mockups, Figma files, screen designs, or visual design system are provided. Let the AI design an appropriate clean, professional, easy-to-use Customer Support CRM interface from the attached CRM features and the data actually available for each story. Do not block implementation waiting for user-supplied designs or invented UX requirements.
- In this architecture story, establish a coherent, responsive visual foundation: choose suitable colors, typography, spacing, navigation layout, and basic reusable UI building blocks. The AI may select an Angular-compatible component library or lightweight styling approach where useful; document the choice and avoid unnecessary UI dependencies.
- Choose navigation and layout with future customers, tickets, communications, dashboard, knowledge base, and reporting modules in mind, but do not implement those business screens yet. Keep the design mobile-friendly. Avoid hardcoded assumptions that the layout can only work left-to-right; prefer direction-neutral CSS logical properties where straightforward. Do not implement translations, locale switching, or full RTL functionality now.
- For future feature stories, AI may decide screen structure, presentation patterns, empty states, and controls based on the data, requirements, and user workflows available in the relevant intake and attachments; do not invent unsupported business fields or workflows merely for visual design.

5. Minimum architectural connectivity demonstration (not an automated test)
- Expose a lightweight backend GET /api/health endpoint that returns a successful response when the API is running; it is an infrastructure check, not a business endpoint.
- Provide a minimal Angular startup screen consistent with the AI-selected design that requests the backend health endpoint and shows the resulting connection status. Implement sensible error handling in application code, but do not require negative-path tests.
- Ensure backend and frontend can both start locally from the same repository with separate documented commands.

6. Verification and project documentation
- Testing must stay minimal due to project time constraints: use only basic unit tests for the backend and frontend, and cover happy (successful/expected) scenarios only. Provide one simple passing backend unit-test example and one simple passing frontend unit-test example.
- Do not require unit tests for failure handling, edge cases, rollback, cancellation, invalid inputs, or unavailable services. Those runtime behaviors may still be implemented where required by the architecture, especially database atomicity, but they are not part of the testing deliverables.
- Do not create or require SQL/database integration tests, API/integration tests, browser/E2E tests, contract tests, visual snapshot tests, load/performance tests, or other testing suites. Avoid heavyweight mocking/test frameworks, excessive test counts, and extra test infrastructure.
- Document prerequisites, solution structure, local SQL Server configuration, migrations, how to run each app, how to run the minimal happy-path unit tests, and the SDD/Squad-Kit story-to-plan workflow.
- Provide a clear starting point so future CRM feature stories can add Domain/Application/Infrastructure/API changes and Angular feature folders without restructuring the repository.

7. Future CRM capabilities — compatibility only; defer requirements and planning
- Users, authentication, authorization, roles/permissions, Arabic/English localization, RTL behavior, multi-department, and multi-branch capabilities are explicitly reserved for separate, later feature intakes.
- For ARCH-001, only preserve basic modular extensibility: avoid deeply coupling the UI shell or API contracts to a specific role, language direction, department, or branch. Do not create speculative entity relationships, claims/permissions systems, translation resources, tenancy/branch isolation models, or related workflows.
- Do not generate plans, tasks, designs, acceptance criteria, implementation, or tests for those deferred capabilities in this architecture story. Their precise requirements and technical decisions must be driven by their own future intakes; nothing here preselects an implementation.

Business context (attachment)
The attached Customer Support CRM core-features document describes future scope: Customer Management, Ticket Management, Communication Channels, Agent Dashboard, SLA & Automation, Knowledge Base, AI Features, Customer Portal, Reports & Management, Security & Administration, Integrations, and Platform (including Arabic/English and web/mobile-friendly support). These are architectural context only; this story does not deliver their functionality.
```

---

## Acceptance criteria

```
- [ ] One Git repository contains backend/, frontend/, .squad/, and a root README.md; no separate frontend/backend repositories are required.
- [ ] The backend solution builds successfully and contains distinct Domain, Application, Infrastructure, and API projects with the specified dependency direction.
- [ ] The backend runs locally as an ASP.NET Core REST API and exposes GET /api/health with a successful JSON response.
- [ ] CQRS request/handler organization and mediator dispatch are wired and demonstrable using a minimal non-business example or unit test, without adding CRM business functionality.
- [ ] EF Core is configured for local SQL Server; a DbContext and repeatable migration workflow exist, and setup instructions explain how to initialize the database from a clean local environment.
- [ ] A documented application-wide database transaction policy ensures command-side writes are atomic: use one SaveChangesAsync() where possible and preserve EF Core's default transaction behavior; use one explicit SQL Server transaction if a command needs multiple saves or dependent persistence steps.
- [ ] Transaction orchestration is reusable at the application-command boundary (e.g. an application-facing unit-of-work contract or mediator command pipeline), with EF Core-specific code isolated in Infrastructure; controllers and Domain code do not own commit/rollback logic.
- [ ] Multi-step command transaction handling is designed/implemented to commit only after all writes succeed and to roll back/dispose without committing on failure or cancellation. No failure-path or rollback unit tests are required under this time-boxed story.
- [ ] The transaction design avoids unnecessary explicit transactions for reads/single saves; documents the single-database boundary and the correct interaction with an EF Core execution strategy if retries are enabled.
- [ ] The API has working dependency injection, centralized exception/validation response conventions, logging, and OpenAPI documentation.
- [ ] The project establishes and documents an API contract standard: future business endpoints use `/api/v1/{resource}`, infrastructure health stays at `/api/health`, C# DTO names end in `Request`/`Response`, JSON uses camelCase, successful responses use direct DTOs with appropriate status codes, and errors use `ProblemDetails`/`ValidationProblemDetails`. No CRM business endpoints/DTOs or `v2` implementation are introduced.
- [ ] Angular's central API access/configuration supports the documented base URL and versioned business route prefix, with typed transport contracts kept separate from domain/persistence models. Pagination/filtering decisions remain for feature-specific future intakes.
- [ ] No passwords, connection credentials, or other secrets are committed to the repository.
- [ ] The Angular application builds and starts locally, uses feature-based folders (core/, shared/, features/), and has basic routing/application shell.
- [ ] With no supplied UI designs, the AI chooses and implements a coherent, professional, responsive Angular application shell and visual styling based on the CRM attachment; the plan records any UI library/design decisions. Future feature screens remain out of scope.
- [ ] The Angular application calls GET /api/health through a centralized HTTP/API mechanism and displays a clear connection status; negative-path tests are not required.
- [ ] Local frontend/backend connectivity is configured without storing developer-specific service URLs in scattered source files.
- [ ] One simple backend happy-path unit test and one simple frontend happy-path unit test pass. No negative-path/edge-case unit tests or other automated testing types are required or introduced.
- [ ] The README explains prerequisites, project/layer layout, run/build/test commands, local SQL Server setup, EF Core migrations, and how future Squad-Kit intakes become plans.
- [ ] The Angular shell avoids needless left-to-right-only layout assumptions (using CSS logical properties where practical), without implementing language switching or RTL functionality in ARCH-001.
- [ ] Roles/permissions, localization/RTL behavior, and multi-department/multi-branch capabilities are explicitly deferred to separate future intakes. ARCH-001 contains only light compatibility guardrails and does not plan, design, model, or implement those features.
- [ ] The attached CRM core-feature list is treated as future business scope only; no Customer/Ticket/AI/Portal business workflows or domain tables are implemented under this story.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| `attachments/azm_squad_customer_support_crm 1(2).pdf` | Two-page Customer Support CRM core-features source document; context for later feature stories, not implementation scope for this architecture story. |

---

## Dependencies

- **Blocked by / related ids:** None. Foundational story (`ARCH-001`).
- **Depends on code areas or other stories:** None. Creates initial backend/, frontend/, and shared repository foundations. Future CRM feature stories depend on this architecture story.

## Extra notes

- Keep the initial solution lightweight: a single ASP.NET Core backend and one Angular frontend; do not introduce microservices or unnecessary infrastructure.
- CRM feature names from the attachment should guide future modular boundaries, not trigger implementation in ARCH-001.
- API contract conventions are the only additions to be established now; do not introduce placeholder CRM endpoints, business DTOs, list-query pagination/filtering design, or a second API version.
- The chosen supported .NET/Angular/package versions and mediator implementation should be recorded in the generated plan.
- The plan must identify where transaction boundaries are owned and how they are reused in future CQRS commands. Implement the necessary rollback logic to preserve atomicity, but do not add failure/rollback/transaction-specific tests in this story: testing is strictly minimal, happy-path-only unit testing.
- UI/UX design is delegated to the AI. There are no supplied screenshots, mockups, or Figma files; use the CRM requirements attachment and the available API/data contracts as the basis for choosing a coherent, responsive visual system. For future CRM features, make context-appropriate UI decisions within each feature story without asking for a full predefined design first.

## Technical hints

- Repos/roots: `.` (one repository). Primary languages: `C#` and `TypeScript`.
- Suggested backend layout: `backend/src/Crm.Domain`, `backend/src/Crm.Application`, `backend/src/Crm.Infrastructure`, `backend/src/Crm.Api`, and `backend/tests/Crm.UnitTests`.
- Suggested frontend layout: `frontend/src/app/core`, `frontend/src/app/shared`, `frontend/src/app/features`, and application routing at `frontend/src/app`.
- UI guidance: AI-designed professional CRM shell and responsive styling; let the AI select a suitable Angular-compatible UI component/styling approach and document it. Use real requirements/data from attachments as the basis for future business screens; do not generate complete fake business pages in ARCH-001.
- Testing guidance: keep backend/frontend unit testing to one simple successful/happy-path example per side; no negative/edge cases, integration, SQL, API, E2E, or other test suites.
- Primary development stack: ASP.NET Core REST, CQRS + Mediator, EF Core + local SQL Server, Angular HTTP client and routing.
- Persistence policy: each write command is one logical atomic unit; single SaveChangesAsync() is transactional by default, while commands requiring multiple SaveChangesAsync() calls need a coordinated explicit transaction (BeginTransactionAsync / CommitAsync, with rollback on failure). Keep this in Infrastructure behind an application-facing boundary; read-only queries do not start write transactions.
- If a retrying EF Core execution strategy is configured, wrap a user-initiated transaction in that execution strategy rather than treating each write as separately retryable.
- Minimal health contract: `GET /api/health` -> HTTP 200 with a small JSON status payload; this does not need a database dependency, and is the documented versioning exception.
- Future business API convention: `/api/v1/{resource}`; PascalCase C# `...Request`/`...Response` DTOs, camelCase JSON, normal HTTP success statuses with direct DTO payloads, centralized `ProblemDetails`/`ValidationProblemDetails` errors. Keep Angular API base URL and path/version centralized. Defer pagination/filtering to future list-feature intakes.
- Future capabilities (roles/permissions, localization/RTL, departments/branches) require distinct later intakes and are not to be designed or planned from this architecture story.
- Use feature-oriented organization within the Application layer when business features are added; enforce Clean Architecture boundaries across layers.

## Out of scope

- Implementing CRM workflows, screens, or database entities for Customer Management, Tickets, communications, SLA, knowledge base, AI, customer portal, reports, security/administration, integrations, or platform customization.
- Implementing **or planning** authentication, authorization/roles, audit trails, or provider integrations (these belong to later business/security intakes).
- Planning/designing/implementing localization infrastructure or language switching, full RTL support, and multi-department/multi-branch schemas, logic, tenancy, or access/isolation rules. These need their own future intakes; only minimal architecture compatibility constraints are in ARCH-001.
- Creating speculative CRM DTOs, business endpoints, a second API version, or collection pagination/filtering standards before the corresponding feature intakes.
- Production hosting, cloud infrastructure, CI/CD pipelines, Docker/Kubernetes, microservices, observability platforms, or deployment automation.
- Negative-path, exception-path, rollback, edge-case, and cancellation unit tests; SQL/API integration tests; E2E/browser automation; contract tests; visual regression/snapshot tests; performance testing; and any additional testing infrastructure beyond minimal happy-path unit tests.
- Requiring preexisting mockups, Figma files, or manual UI design input; implementing full CRM feature pages in this architecture story.
- Finalizing all future CRM domain models, endpoints, and UI designs before their individual story planning.
