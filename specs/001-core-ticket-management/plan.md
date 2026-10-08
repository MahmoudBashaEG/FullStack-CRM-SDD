# Implementation Plan: Core Ticket Management

**Branch**: `master` (actual Git checkout) | **Date**: 2026-10-08 | **Spec**: [spec.md](spec.md)

**Input**: `specs/001-core-ticket-management/spec.md` and project constitution v1.0.0.

**Feature identifier**: `001-core-ticket-management`. Spec Kit reports this identifier as
BRANCH from active feature state; it does not indicate a Git feature branch exists.
This is a plan only; no solution, application, or migration has been implemented.

## Summary

Deliver an internal shared support queue: create, view, find, edit, manually assign,
resolve, close, reopen, and review tickets. Use a four-layer backend with command/query
handlers, SQL Server persistence, and a standalone Angular tickets feature. Domain rules
remain independent of infrastructure; ticket changes and immutable history save atomically.

The spec remains authoritative, including A-003: no mandatory customer profile. The unanswered
clarification did not change that assumption. Exclude all escalation, deletion, administration
and future modules listed in A-007/A-008. No tasks or implementation are generated here.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`); Angular/CLI 22, TypeScript 6.0.x,
Node 24.20.0. Pin compatible stable patches, SDK and package lockfiles during implementation.

**Primary Dependencies**: ASP.NET Core 10, built-in DI, JWT bearer authentication,
MediatR 12.5.0 planning baseline, EF Core 10 SQL Server provider; Angular router,
HttpClient, reactive forms, RxJS, `oidc-client-ts` for external identity integration.
See [research.md](research.md) for version and licensing decisions.

**Storage**: Local SQL Server 2022 baseline, Windows Integrated Security with one connection/Windows identity for all database access; Unicode content; one transactional store for reads
and writes; version-controlled EF migrations in Infrastructure. No event store.

**Testing**: xUnit backend tests; real SQL Server integration tests and WebApplicationFactory;
architecture dependency checks; Angular TestBed/Vitest and HttpTestingController;
focused Playwright end-to-end tests.

**Target Platform**: HTTPS web application on supported desktop/mobile browsers; backend
hosted on Windows using the same account as local EF/provisioning commands. No native mobile app or hosting deployment work.

**Project Type**: Backend solution plus browser frontend in one repository.

**Performance Goals**: Advisory benchmark, not a completion gate or business SLA: at 10,000
tickets and 20 active staff, p95 reads under 1 second and mutations under 2 seconds, excluding
identity redirects. Execution/results do not block completion unless adopted in the spec.
SC-001 through SC-005 remain acceptance targets.

**Constraints**: No business logic in controllers; no EF/HTTP types in Domain or Application;
async I/O/cancellation; explicit queue authorization; stale/no-op protection; atomic history.

**Scale/Scope**: One shared support queue; bounded list/history pages. No invented department
or branch ownership fields. Test volume/concurrency above are not asserted business limits.

## Constitution Check

*GATE: Before Phase 0 research and again after Phase 1 design.*

| Principle | Before research | After design |
| --- | --- | --- |
| I. Incomplete requirements | PASS: source and assumptions separated | PASS: A-003 retained; confirmed A-011/A-012 mapped into design |
| II. Spec-driven development | PASS: active spec; planning only | PASS: requirement mapping; later tasks/implementation |
| III. Backend architecture | PASS: required layers and stack | PASS: inward dependencies, feature ports, DI composition |
| IV. Frontend architecture | PASS: tickets owns feature code | PASS: core limited to auth/HTTP; shared only actual reuse |
| V. Quality | PASS: rule/integration checks planned | PASS: real SQL tests, architecture checks, migrations/build gates |
| VI. Platform constraints | PASS: platform compatibility considered | PASS: Unicode, logical CSS, localization keys; no premature modules |

No constitutional exceptions. Review findings are resolved in the revised designs below.
Issuer configuration and approved reference inputs are fail-closed prerequisites. Research resolves
technical choices. Technical baselines are inferred engineering decisions, not business input.

## Project Structure

### Documentation (this feature)

```text
specs/001-core-ticket-management/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/tickets-rest.md
├── contracts/reference-provisioning.md
├── contracts/database-principals.md
├── checklists/requirements.md
└── tasks.md                     # Future speckit-tasks output, not created here
```

### Source Code (repository root)

Planned structure only:

```text
Crm.slnx
src/
├── backend/
│   ├── Crm.Domain/Tickets/
│   ├── Crm.Application/Tickets/{Commands,Queries,Contracts,Ports,Validation}/
│   ├── Crm.Infrastructure/{Persistence/Configurations,Persistence/Migrations,Tickets}/
│   └── Crm.Api/{Controllers,Authorization,Errors}/
└── frontend/
    ├── e2e/tickets/
    └── src/app/
        ├── core/{auth,http}/
        ├── shared/
        └── features/tickets/{pages,components,models,data-access,services}/
tests/backend/
├── Crm.Domain.Tests/
├── Crm.Application.Tests/
├── Crm.IntegrationTests/
└── Crm.ArchitectureTests/
```

**Structure Decision**: A frontend workspace under `src/frontend`; conceptual `src/app`
is `src/frontend/src/app`. Feature routes and colocated frontend tests stay in tickets.
Create only folders with actual responsibility, not empty generic containers.

## Design and Requirement Mapping

### Domain and Application

Ticket is an aggregate with private mutations for details, assignment and lifecycle. It
checks required text, valid enum values, closed edit guards, transitions and no-ops. Application
checks availability/eligibility only when a selected reference ID differs from the saved ID
(creation always selects a new category). Unchanged inactive IDs are retained; clearing an
assignment requires no replacement eligibility check. Domain owns no
persistence annotations, DbContext, HTTP claims or rowversion. History represents changes,
not an event-sourced model. See [data-model.md](data-model.md).

| Requirements | Command/query | Responsibility |
| --- | --- | --- |
| FR-001/002 | CreateTicketCommand | Validate, default, create reference/initial history |
| FR-003 | GetTicketQuery, GetTicketHistoryQuery | Project details and chronological history |
| FR-004 | ListTicketsQuery | Search, AND filters, stable ordering, bounded pages |
| FR-005/006 | UpdateTicketDetailsCommand, GetCategoriesQuery | Validate changed category IDs; retain unchanged inactive category; protected fallback |
| FR-007 | AssignTicketCommand, GetEligibleAgentsQuery | Validate changed non-null agent IDs; retain or clear inactive assignment |
| FR-008/009 | ChangeTicketStatusCommand | Exact transition table; close/reopen |
| FR-010/011/012 | All mutation handlers | Trusted actor/time, atomic history, concurrency/no-ops |
| FR-013 | SupportQueue policy and current actor port | Explicit shared-queue access |
| FR-014 | No use case | No excluded operations |

MediatR handlers orchestrate use cases. Application defines `ITicketMutationStore` for
aggregate loading with opaque version and committing one ticket/history unit; a scoped
mutation operation holds its transaction until success/no-op/rejection. `ITicketReadService`
projects query DTOs; `ITicketReferenceData` supplies read-only categories/eligible agents.
`ICurrentActor` supplies trusted issuer/subject/display snapshot; TimeProvider controls time.
Ports expose no DbSet, IQueryable or EF types. They support aggregate atomicity, concurrency
and testing, rather than wrapping every DbSet with a repository.

Domain references no other project. Application references Domain. Infrastructure references
Application/Domain. API references Application/Infrastructure for composition. Controllers
bind HTTP input, dispatch handlers and map outcomes; no business rules or direct persistence.

### Persistence, Concurrency and History

Infrastructure owns EF Fluent configurations, migrations, foreign keys, unique references,
enum checks, Unicode strings, indices and a shadow SQL Server rowversion. Query projections
are no-tracking. CQRS uses the same database and no event sourcing or message bus.

Every existing-ticket mutation sends expectedVersion in its JSON body, copied from editVersion
on the last ticket detail/mutation response. The base64 rowversion is an aggregate edit token,
not an HTTP representation validator. No ETag, Last-Modified or If-Match contract is used;
responses are no-store. Reference labels/availability can change without changing editVersion.
Use PATCH for details and PUT to replace assignment/status writable state; no validator is
returned on transformed PUT responses. Missing/malformed expectedVersion is 400; stale is
409 ticket_changed, including stale no-ops. No silent replay or last-write-wins.

Infrastructure registers one request-scoped DbContext shared by the mutation/reference ports.
ITicketMutationStore owns the sole transaction and commit/rollback per command; the request
DI scope disposes the context. Handlers orchestrate within the mutation operation and never
start transactions or save directly. ITicketReferenceData uses that same context/transaction.
No generic transaction pipeline or nested transaction. Creation locks its chosen category;
existing mutations first load/lock the ticket using parameterized UPDLOCK/HOLDLOCK and compare
expectedVersion. Lock only newly selected reference rows, category then agent, using HOLDLOCK
until commit. Retained inactive IDs need no availability check; null assignment clears directly.
Status changes perform no category/agent eligibility check. No network calls in the transaction.

Real writes preserve EF's original shadow rowversion predicate. Save ticket and pending history
once, then commit; the store alone completes/disposes its transaction. Valid no-ops complete without
SaveChanges and preserve rowversion, updated time and history. All failures roll back/dispose;
deadlock/lock timeout is 503, not a stale conflict. The design uses version checks plus short
writer serialization, not purely optimistic concurrency. This lock protects no-op decisions
without a separate no-op algorithm. History ordering uses revision then within-revision sequence.

Do not configure EnableRetryOnFailure or any implicit EF retrying execution strategy for this
feature. Do not auto-replay handlers, transactions or HTTP mutations. Queries may be retried by
an explicit user action. Commit outcome can be unknown after disconnect; refresh before retry.
Rollback/disposal cleanup must still run when the request CancellationToken is cancelled.

Bootstrap the fixed, protected Uncategorised category through a migration with a SQL CHECK
that it remains available. Supported provisioning never deletes reference rows and refuses
fallback rename/deactivation. Application operations expose no reference deletion; direct
database alterations are outside supported provisioning and are not blocked by separate credentials. Non-default rows can become
inactive without altering existing tickets. Use one bounded, operator-only installation command
with validated explicit upsert records, no general import/reconciliation framework or admin UI.
[Provisioning contract](contracts/reference-provisioning.md) defines schema and all-or-nothing
behavior, preserved omissions, immutable identity keys, fallback rules and error results.

### Single Windows-Authenticated Database Connection

Project decision: use local SQL Server with ConnectionStrings:Crm and Integrated Security=True
(or Trusted_Connection=True). ASP.NET HTTP runtime, EF Core tools and the reference-provisioning
CLI use the identical configured connection value and the same Windows account within an
environment/run. No dedicated SQL users, named per-process principals, grant/deny scripts,
separate database credentials or SQL username/password are created by this project.

Example local development value:
`Server=localhost;Database=CustomerSupportCrm;Integrated Security=True;Encrypt=True;TrustServerCertificate=True`.
The certificate exception is for the local self-signed SQL instance only; a trusted certificate
uses normal validation. Select the actual local instance during environment setup, not a fabricated
account. The existing Windows account must already have access needed for schema and supported
operations; missing access is a setup prerequisite, not a reason to add SQL-password fallback.

Infrastructure validates the shared connection with SqlConnectionStringBuilder: integrated
security required, local server/catalog present, no User ID/Password keys or alternative auth
mode. All entry points load this one key; do not override identity via impersonation, SqlCredential
or access-token authentication. EF design-time context construction shares this configuration
without starting HTTP. Migration and CLI modes remain explicit invocations, not startup side effects.

[database-principals.md](contracts/database-principals.md) now documents the single-identity
connection contract and its application boundaries. HTTP still requires JWT shared-queue access;
a staff identity is not a SQL login and is never impersonated for database operations.
Reference CLI validates only supported upserts, exits before HTTP, and never changes tickets/history.
History is append-only through supported mutation paths; no history update/delete use case exists.

Trade-off: database least-privilege separation is intentionally not implemented. The shared Windows
identity may have schema and direct-DML rights in every process. Direct SQL or bypassed application
code can therefore alter records despite application-level restrictions; no denied-DML guarantee
is claimed. Supported operation boundaries, immutable Domain history, scoped transactions, strict
CLI validation and absence of delete/admin endpoints enforce the product rules. This is an explicit
project choice and does not change the constitution or feature spec.

Integration/E2E tests use the same local Windows account and shared connection for all child
processes in a run; change only the catalog to a guarded isolated test database. Verify integrated
configuration and matching Windows/SQL identity across entry points; exercise actual allowed
operations and rejected application/CLI operations. Remove three-principal/effective-denial tests.

### REST, Validation and Cancellation

Use thin controllers for `/api/tickets` and protected reference reads. Application validates
required values, choices and selections; Domain reasserts business invariants. Client
validation improves feedback but never establishes validity. Stable enum codes and typed
results map to consistent HTTP statuses and ProblemDetails with field errors and trace IDs.
Never log ticket content/credentials or expose exception/connection internals. Display ticket
text as text, not trusted HTML. Bind CancellationToken from HTTP and propagate through
MediatR, queries, EF operations and transaction disposal. Report success only after commit.

[contracts/tickets-rest.md](contracts/tickets-rest.md) defines requests, responses, pagination,
edit versions, complete history payloads and errors. No delete or admin routes exist.

### Authorization Boundary

JWT bearer validates issuer, audience, signature and expiry from a trusted external issuer.
SupportQueue requires authentication, stable issuer/subject and `crm_support_queue=shared`.
Apply it to every ticket, history and reference endpoint; derive actor from trusted claims,
never request bodies. All authorized staff have the same access regardless of assignment.
No broad fallback allow policy. Keep claim/policy mapping replaceable for future administration.

Frontend core uses oidc-client-ts authorization-code/PKCE with an externally registered SPA
client; issuer, client ID, redirects and scopes are public configuration. No token server,
password forms, role hierarchy or permission matrix. Session-scoped token storage; bearer
interceptor sends credentials only to the configured API origin. Route guards are convenience,
not enforcement. Missing production issuer configuration fails closed. Development/test
identities are isolated from production. Real issuer/client values are deployment prerequisites.

### Angular Feature

Standalone lazy tickets routes provide list, create and detail/edit pages. Feature-local
models, reactive forms and data access own ticket behavior and editVersion/expectedVersion. Details include paged
history; separate operations handle editing, assignment and status. Refresh after success;
no global state library initially. On 409 ticket_changed preserve unsaved values and explain
the conflict. Allow reload before explicit resubmission; never silently replay mutations.
Keep a retained inactive category/agent visible while offering only active new choices;
compare selected IDs, not labels or active flags.

Distinguish loading, empty, validation, unauthenticated, forbidden, missing and failure states.
Core owns auth/common HTTP; shared contains only actual reuse. Accessible controls, keyboard
operation and narrow layouts support mobile-friendly use. Unicode, message keys, logical CSS
and branding variables preserve Arabic/English and future branding compatibility without
implementing translation management or organizational administration.

### History Availability by Story

The history endpoint and baseline history component ship in US1 and remain extensible:

| Checkpoint | Required readable entries |
| --- | --- |
| US1 | Created snapshot, actor/time and initial values |
| US3 | US1 plus DetailsChanged and AssignmentChanged, exact field/null/value shapes |
| US4 | All earlier entries plus StatusChanged, including close/reopen |
| US5 | Comprehensive mixed-history review, pagination/presentation verification and snapshot regressions |

Each mutation story owns its serializer/query projection, feature model/rendering and API/browser
assertions for the entry kinds it emits. Persisting an unreadable entry does not pass that story's
checkpoint. US5 verifies the complete account and improves presentation; it is not the first
implementation of US3/US4 read support. No payload/schema or product behavior changes.

### Testing and Completion Gates

Domain tests cover all state pairs, default/priority rules, required content, closed guards,
assignment, reopen preservation and history/no-ops. Application tests cover validation,
reference eligibility, trusted actor use, port orchestration, failures and cancellation.

SQL Server integration tests apply committed migrations and exercise WebApplicationFactory:
401/403, shared access, references, Unicode/literal substring search, paging tie-breaks,
edit-version roundtrips, malformed/missing/stale versions, simultaneous real/no-op writes, rollback and no orphan
history. Test inactive reference retention in edits/status/no-ops, clearing inactive agents,
rejected new inactive choices, fallback protection and provisioning atomicity. Include genuine
JWT validation tests; fake test authentication alone is insufficient.
EF InMemory/SQLite cannot prove SQL rowversion, locking or collation behavior.

Architecture tests reject forbidden project and framework dependencies and direct persistence
access from controllers. Angular tests cover forms, routing, HTTP contracts and error/conflict
behavior. Focused browser tests cover create-to-reopen against the real backend. SC-005 needs
five representative staff; automated checks cannot claim that evaluation passed. Browser tests
use Playwright-managed servers, isolated SQL data and ephemeral signed identities as specified
in quickstart.md. No pre-running servers or external login service is required for this harness;
production authentication remains external OIDC. Every phase uses the same protected readiness
URL https://localhost:7043/api/ticket-reference-data/categories; unauthenticated 401 proves HTTP
availability, followed by an authorized 200/fallback probe for database readiness. Never switch
to a later story endpoint or infer SQL readiness from 401 alone.

SC-005 follows the repeatable protocol in quickstart.md: five non-implementer support staff,
a standard five-minute introduction, fixed issue/agent inputs and task sequence, no procedural
help during the scored attempt, and per-participant outcome/assistance records. The original
four-of-five success threshold remains unchanged; the protocol is evaluation guidance only.

Build/test relevant backend and frontend projects and verify migrations before completion.
The [quickstart](quickstart.md) defines prospective commands and expected validation outcomes.

## Delivery Boundaries

Later tasks may initialize the solution/projects and Angular app, then implement tested
vertical slices. This phase creates design documents only. No customer profile linking,
communications, escalation, SLA, automatic assignment, notifications, portal, AI, reporting,
full administration or future modules. No constitutional complexity exceptions are needed.
