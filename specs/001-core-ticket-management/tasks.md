---
description: "Implementation tasks for Core Ticket Management"
---

# Tasks: Core Ticket Management

**Input**: Latest `specs/001-core-ticket-management/` design artifacts; synchronized 2026-10-08 with the single local SQL Server/Windows Authentication decision. Existing 81 task IDs, ordering and all other approved design decisions are preserved. Implementation is verified through T078, with the T080 optional benchmark disposition recorded. T079 / SC-005 is pending and not executed; the T081 audit is provisional and its final acceptance gate remains pending.
**Prerequisites**: [spec](spec.md), [plan](plan.md), [research](research.md),
[data model](data-model.md), [REST](contracts/tickets-rest.md),
[provisioning](contracts/reference-provisioning.md), [single Windows DB connection](contracts/database-principals.md), [quickstart](quickstart.md), constitution v1.0.0.
**Tests**: Required by the constitution and the user's planning instructions. T001-T078 and T080 are complete and verified. T079 and T081 remain unchecked because actual SC-005 participants are unavailable. See validation/us1.md through validation/us5.md, validation/automated.md, validation/staff-evaluation.md, validation/advisory-benchmark.md and validation/completion.md.
**Organization**: US1 create/view P1; US2 tracking P1; US3 edit/assign P2; US4 lifecycle P2;
US5 history review P2. US1 includes creation history; US3/US4 must read the kinds they emit at their own checkpoints. US5 verifies comprehensive mixed-history review/presentation.

## Format: `[ID] [P?] [Story] Description`

[P] denotes file-separated work in the dependency waves below, after prerequisites. It does
not authorize agents or skipping dependencies. Story labels apply only to story phases.
Write meaningful behavior tests before their paired implementation; demonstrate missing
behavior once scaffolds compile, then make them pass. Do not leave suites permanently skipped.

## Path Conventions

Paths are repository-relative planned files. Backend: `src/backend/Crm.*`; tests:
`tests/backend/Crm.*Tests`; frontend: `src/frontend/src/app/{core,shared,features/tickets}`;
browser fixtures/tests: `src/frontend/e2e/`. Keep implementation within the approved scope.

## Shared Database Decision

API, EF migration tools and reference CLI use the identical ConnectionStrings:Crm value and
Windows identity in an environment/run. Use Integrated Security/Trusted_Connection, no SQL
username/password and no SqlCredential, AccessToken or impersonation for DB access. An existing
Windows account with required local SQL access is a prerequisite, not a principal-creation task.
Tests may change only the catalog to their approved disposable DB, sharing that test connection
across all entry points. JWT staff authentication/queue permissions are unchanged and separate
from Windows database authentication.

No SQL-user creation/mapping, GRANT/DENY scripts, database permission matrix or three-principal
verification tasks remain. Test supported application behavior instead of expecting direct SQL
DML to be denied. Supported history is immutable, reference CLI is upsert-only, and unsupported
admin/delete operations remain absent. The shared identity's broader DB authority is the documented
project trade-off; no database least-privilege separation is claimed.

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Initialize the approved projects and test workspaces only.

- [X] T001 Create `Crm.slnx`, `global.json`, and the four `src/backend/Crm.Domain/Crm.Domain.csproj`, `Crm.Application/Crm.Application.csproj`, `Crm.Infrastructure/Crm.Infrastructure.csproj`, `Crm.Api/Crm.Api.csproj` projects targeting net10.0; enforce Domain with no project dependencies, Application -> Domain, Infrastructure -> Application/Domain, API -> Application/Infrastructure.
- [X] T002 Pin compatible ASP.NET/EF Core 10 and MediatR 12.5.0 after compatibility/security review in `Directory.Packages.props`, backend `packages.lock.json` files and `.config/dotnet-tools.json`; register MediatR in `src/backend/Crm.Application/DependencyInjection.cs` without EF/HTTP coupling.
- [X] T003 [P] Initialize xUnit projects under `tests/backend/Crm.Domain.Tests/`, `Crm.Application.Tests/`, `Crm.IntegrationTests/`, `Crm.ArchitectureTests/` with exact corresponding `.csproj` names and add them to `Crm.slnx` (after T001/T002).
- [X] T004 [P] Initialize Angular/CLI 22 standalone workspace with TypeScript 6.0.x in `src/frontend/package.json`, `package-lock.json`, `angular.json`, `src/app/app.config.ts`, `src/app/app.routes.ts`; keep shared limited to actual reuse (after T001/T002).
- [X] T005 Configure `.gitignore`, `.editorconfig`, `src/backend/Crm.Api/appsettings.json`, `src/frontend/src/environments/environment.ts` for formatting, public config and ignored secrets/test outputs; the backend may store a credential-free ConnectionStrings:Crm Integrated Security value, never a DB username/password or per-process connection key; frontend receives no SQL connection string, and real provisioning input remains uncommitted (after T003/T004).
- [X] T006 Restore/build scaffolds and record tool versions/results in `specs/001-core-ticket-management/validation/setup.md`; empty-project compilation is not feature acceptance.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Minimal authorization, reference ports and shared persistence/test plumbing.
**Gate**: Complete foundation code scaffolding before story implementation. Database-backed scenarios require T023 migrations and the validated shared Integrated Security connection; browser scenarios additionally require T026 API routes. All hosts in a run use the same Windows account and catalog; no principal/grant-verification gate remains.

- [X] T007 [P] Define outcomes, trusted actor port and opaque version parser in `src/backend/Crm.Application/Common/OperationResult.cs`, `ICurrentActor.cs`, `Tickets/Contracts/EditVersion.cs`; require canonical base64 of 8 bytes, missing/malformed -> 400 invalid_version; use TimeProvider and no HTTP/EF types.
- [X] T008 [P] Implement `src/backend/Crm.Api/Authorization/SupportQueuePolicy.cs`, `CurrentActor.cs`, `AuthenticationConfiguration.cs`; real JWT issuer/audience/signature/expiry, stable issuer/subject and crm_support_queue=shared; assignment grants no rights; production fails closed and rejects test-auth configuration.
- [X] T009 [P] Define `src/backend/Crm.Application/Tickets/Ports/ITicketReferenceData.cs`, `Contracts/TicketReferenceDtos.cs` and reference records/configurations under `src/backend/Crm.Infrastructure/Tickets/` and `Persistence/Configurations/`; enforce "Stable GUID label identity", nonblank Unicode labels, unique ordinal issuer/subject, boolean availability/eligibility, no accounts/credentials/roles.
- [X] T010 Create `src/backend/Crm.Infrastructure/Persistence/CrmDbContext.cs` and `DependencyInjection.cs`; one scoped context shared by feature/reference ports, DI disposal, SQL Server, async CancellationToken, no EnableRetryOnFailure or implicit execution retry; add `src/backend/Crm.Infrastructure/Persistence/CrmDatabaseSettings.cs` using SqlConnectionStringBuilder to require local Integrated Security/shared catalog, reject User ID/Password keys and alternative authentication, and load only ConnectionStrings:Crm for API, design-time EF and CLI with identical normalized settings and Windows identity; prohibit SqlCredential/AccessToken/impersonation DB authentication and alternative process connection keys (after T007/T009).
- [X] T011 Configure `src/backend/Crm.Api/Errors/ProblemDetailsMapping.cs`, `HttpConfiguration.cs`, `Program.cs` for sanitized typed errors, strict unknown/duplicate-field rejection, 415 media types, approved CORS/no-store and no HTTP validators/If-Match substitution (after T007/T008/T010).
- [X] T012 Implement `src/backend/Crm.Application/Tickets/Queries/GetCategoriesQuery.cs`, `GetEligibleAgentsQuery.cs`, Infrastructure `src/backend/Crm.Infrastructure/Tickets/TicketReferenceData.cs`, API `src/backend/Crm.Api/Controllers/TicketReferenceDataController.cs`; protected selectable-only reference reads, no administration endpoint (after T009-T011).
- [X] T013 Implement `src/backend/Crm.Infrastructure/Tickets/ReferenceProvisioner.cs`, `ReferenceProvisioningInput.cs` and CLI dispatch in `src/backend/Crm.Api/Program.cs` per provisioning contract: use the shared ConnectionStrings:Crm with Integrated Security and the same Windows identity, validate no SQL username/password, and exit without HTTP startup; version-1 strict schema, immutable agent identity, omissions retained, atomic upsert, protected fallback, no delete/reconciliation, sanitized counts and exit 0/2/3 before HTTP startup (after T012; execution awaits T023 schema/shared-connection verification).
- [X] T014 [P] Build real SQL/WebApplicationFactory/JWT fixtures in `tests/backend/Crm.IntegrationTests/Fixtures/SqlServerFixture.cs`, `CrmApiFactory.cs`, `TestJwtFactory.cs`; require CRM_TEST_SQL_CONNECTION, approved actual server/database ownership before reset, isolated CrmTicketsIntegration_ DB, synthetic data and signed tokens; fixture setup, EF, API and CLI use the same local Windows account and identical Integrated Security test connection, changing only InitialCatalog through the shared builder and supplying that same test value as ConnectionStrings:Crm to every host; fail on SQL absence or invalid authentication, with no generated DB credentials/principals or identity switching (after T013).
- [X] T015 [P] Implement production OIDC/PKCE adapter, queue guard and safe token/error interceptors in `src/frontend/src/app/core/auth/auth.service.ts`, `support-queue.guard.ts`, `core/http/auth.interceptor.ts`, `error.interceptor.ts`; oidc-client-ts, origin-restricted tokens, no SPA secret/token server/role UI (after T013).
- [X] T016 Build `src/frontend/e2e/run-e2e.mjs`, `fixtures/auth.ts`, `playwright.config.ts`, `proxy.e2e.json`, core `auth/auth.e2e.ts`, package/angular e2e configuration and Testing-only backend key settings in `src/backend/Crm.Api/Authorization/AuthenticationConfiguration.cs`; isolated SQL preparation before servers with one shared Integrated Security test connection and inherited Windows identity for EF/provisioning/API children, ephemeral signed identities, both .NET Testing envs/--no-launch-profile, HTTPS 7043/4200, reuseExistingServer=false, authorized SQL readiness probe, 401 HTTP readiness at /api/ticket-reference-data/categories for every phase, with a separate authorized 200/fallback SQL probe, one worker and cleanup; production excludes test adapter (after T014/T015; run after T023/T026).

**Checkpoint**: Protected shared queue infrastructure/reference dependencies ready; no future admin.

---

## Phase 3: User Story 1 - Create and View a Ticket (Priority: P1) MVP

**Goal**: Record and retrieve a ticket with readable creation history.
**Independent Test**: Create/retrieve without assignment/status mutations; verify unique reference,
Open/Normal/Uncategorised/unassigned, initial actor/time/snapshot and denied access.

### Tests for User Story 1

- [X] T017 [P] [US1] Write `tests/backend/Crm.Domain.Tests/Tickets/TicketCreationTests.cs`: defaults, "nonblank after trimming", Unicode/interior whitespace, immutable identity/created time and revision 1.
- [X] T018 [P] [US1] Write `tests/backend/Crm.Application.Tests/Tickets/CreateAndGetTicketTests.cs`: category/fallback validation, rejected creation leaves no ticket/history, trusted actor/time and cancellation using feature-port fakes.
- [X] T019 [P] [US1] Write `tests/backend/Crm.IntegrationTests/Tickets/CreateAndViewTests.cs`, `ReferenceProvisioningTests.cs`: 201/Location/200/400/401/403/404, stable references, readable initial history, no validators, reference changes preserve editVersion, migration/fallback protection, atomic provisioning/identity-rebind rejection; verify shared normalized server/catalog, IntegratedSecurity=true, rejected User ID/Password keys and matching Windows/SQL identity across API/EF/CLI in `tests/backend/Crm.IntegrationTests/Persistence/WindowsConnectionTests.cs`; this verifies one shared identity, not distinct principal permissions; assert application-level unsupported/history/delete/upsert rejections, not SQL permission-denied DML.

### Implementation for User Story 1

- [X] T020 [US1] Implement `src/backend/Crm.Domain/Tickets/Ticket.cs`, `TicketPriority.cs`, `TicketStatus.cs`: "generated once" ID, "Stable public reference", "nonblank after trimming" subject/description, "Low, Normal, High, Urgent", "Open, InProgress, Resolved, Closed", optional agent, immutable created time and "creation 1, increment once per changed operation" revision; creation behavior only (after T017-T019).
- [X] T021 [US1] Implement immutable `src/backend/Crm.Domain/Tickets/TicketHistoryEntry.cs`, `TicketChangeValue.cs` and creation snapshot: "unique together per ticket" revision/sequence, "null for Created" field, "null represents unassigned/no previous value", trusted actor/time/display fallback; contract creation revision 1/sequence 1 (after T020).
- [X] T022 [US1] Add `src/backend/Crm.Infrastructure/Persistence/Configurations/TicketConfiguration.cs`, `TicketHistoryConfiguration.cs` and context sets: "uniqueidentifier primary key", "nvarchar(36), unique exact-match index; TKT- plus 32 uppercase GUID digits", "Unicode nvarchar(max)", "Required stable category key, foreign key", "Nullable foreign key", "Stable string code with check constraint", "datetimeoffset", "bigint", "SQL rowversion shadow property; never a Domain dependency"; restrict deletes, unique history order and required subject collation; explicitly map dbo.Tickets, dbo.TicketHistoryEntries, dbo.CategoryReferences, dbo.AgentReferences and dbo.__EFMigrationsHistory to the stable mappings in data-model.md (after T021).
- [X] T023 [US1] Generate/review/version initial migrations and model snapshot in `src/backend/Crm.Infrastructure/Persistence/Migrations/`; seed protected Uncategorised ID 00000000-0000-0000-0000-000000000001/available with SQL CHECK; add `src/backend/Crm.Infrastructure/Persistence/DesignTimeCrmDbContextFactory.cs` so EF tooling loads the identical shared Windows-authenticated ConnectionStrings:Crm without HTTP startup; apply/verify forward migrations and fallback seeding in the approved isolated database using the same Windows account and identical ConnectionStrings:Crm as API/CLI; report missing existing-account SQL access as an environment prerequisite rather than generating a login or grant script (after T022).
- [X] T024 [US1] Define Application `src/backend/Crm.Application/Tickets/Ports/ITicketMutationStore.cs`, `ITicketReadService.cs` and implement Infrastructure `src/backend/Crm.Infrastructure/Tickets/TicketMutationStore.cs`, `TicketReadService.cs`: atomic creation/category lock, one SaveChanges/commit for ticket/history, committed DTO/token consistency, shadow token -> editVersion, detail/initial-history projection, no old-history loading/replay (after T023).
- [X] T025 [US1] Implement Application `src/backend/Crm.Application/Tickets/Commands/CreateTicketCommand.cs`, `Queries/GetTicketQuery.cs`, `GetTicketHistoryQuery.cs`, `Contracts/TicketDtos.cs`, `Validation/CreateTicketValidator.cs`; current inactive values readable, history page >=1/default 25/max 100, defaults/choices/actor/cancellation and all current detail fields (after T024).
- [X] T026 [US1] Implement thin protected API `src/backend/Crm.Api/Controllers/TicketsController.cs`, `TicketHistoryController.cs` for create/detail/history; dispatch handlers, correct statuses/Location/no-store, no validators/direct EF/business logic (after T025).
- [X] T027 [US1] Define `src/frontend/src/app/features/tickets/models/ticket.models.ts`, `data-access/tickets.service.ts`, `tickets.routes.ts`: typed create/detail/history/reference reads, opaque editVersion, decimal-string history revision and no writable server metadata; connect guarded lazy standalone tickets routes in `src/frontend/src/app/app.routes.ts` (after T026).
- [X] T028 [US1] Implement feature `src/frontend/src/app/features/tickets/pages/ticket-create.page.ts`, `components/ticket-create-form.component.ts`: default category/priority, required text, server field errors, accessible Unicode controls, navigate to returned reference and no automatic replay (after T027).
- [X] T029 [US1] Implement feature `src/frontend/src/app/features/tickets/pages/ticket-detail.page.ts`, `components/ticket-history.component.ts`: current fields/inactive labels plus readable initial actor/time/snapshot, loading/empty/403/404/failure states and logical narrow-layout CSS; initial history must not wait for US5 (after T028).
- [X] T030 [US1] Write/run feature `src/frontend/src/app/features/tickets/pages/ticket-create.page.spec.ts`, `ticket-detail.page.spec.ts`, `data-access/tickets.service.spec.ts` for form/defaults/HTTP/errors/guarded navigation/safe text/inactive detail rendering (after T029).
- [X] T031 [US1] Add/run `src/frontend/e2e/tickets/create-view.spec.ts`, paired T017-T019/T030 suites and record US1 evidence in `specs/001-core-ticket-management/validation/us1.md`; prove the same reference-category readiness URL used in every phase, fresh startup, authorized/denied identities, defaults/initial history and successful positive API creation/reads under the same shared Windows identity/connection (after T030).

**Checkpoint**: Independently usable create/view/history MVP passes its acceptance cases.

---

## Phase 4: User Story 2 - List and Track Tickets (Priority: P1)

**Goal**: Find current work through literal search, combined filters and stable bounded pages.
**Independent Test**: Prepared records spanning all statuses/priorities/categories/assignments;
results match exactly without ticket mutations.

### Tests for User Story 2

- [X] T032 [P] [US2] Write `tests/backend/Crm.Application.Tests/Tickets/ListTicketsTests.cs`: exact reference OR case-insensitive subject substring AND filters, empty q, single values, page >=1/default 25/max 100, syntax rejection and well-formed unknown IDs.
- [X] T033 [P] [US2] Write `tests/backend/Crm.IntegrationTests/Tickets/ListTicketsTests.cs`: real SQL Arabic/Latin/literal wildcards, inactive-ID filters, all/unassigned/agent, UpdatedAtUtc DESC/Reference ASC ties, hasMore/empty/failure and protected access.

### Implementation for User Story 2

- [X] T034 [US2] Implement Application `src/backend/Crm.Application/Tickets/Queries/ListTicketsQuery.cs`, `Validation/ListTicketsValidator.cs`: bounded criteria/summary-page result, duplicate query keys rejected at HTTP boundary (after T032/T033).
- [X] T035 [US2] Extend Infrastructure `src/backend/Crm.Infrastructure/Tickets/TicketReadService.cs`: parameterized no-tracking literal search/AND filtering, exact reference, collation, ordering, bounded hasMore; no full-text/client-only filtering/total-count requirement (after T034).
- [X] T036 [US2] Implement protected API `src/backend/Crm.Api/Controllers/TicketListController.cs`: strict binding, handler dispatch, empty 200 versus failures and no extra business routes (after T035).
- [X] T037 [US2] Extend feature `src/frontend/src/app/features/tickets/models/ticket.models.ts`, `data-access/tickets.service.ts` with list query/page types and transport; preserve criteria and distinguish [] from error (after T036).
- [X] T038 [US2] Implement feature `src/frontend/src/app/features/tickets/pages/ticket-list.page.ts`, `components/ticket-filters.component.ts`, `tickets.routes.ts`: search/clear/filter/agent/unassigned/paging/current summaries and accessible detail navigation (after T037).
- [X] T039 [US2] Write/run feature `src/frontend/src/app/features/tickets/pages/ticket-list.page.spec.ts`, `components/ticket-filters.component.spec.ts` for query combinations/page/empty/errors and protected access (after T038).
- [X] T040 [US2] Add/run `src/frontend/e2e/tickets/tracking.spec.ts` and T032/T033/T039; record exact SC-002 matching evidence in `specs/001-core-ticket-management/validation/us2.md` (after T039).

**Checkpoint**: Prepared-data tracking is usable independently of mutation stories.

---

## Phase 5: User Story 3 - Edit and Assign a Ticket (Priority: P2)

**Goal**: Correct details and manually assign/reassign/unassign with inactive retention and atomic history.
**Independent Test**: Non-closed fixtures; verify actual edits/no-ops, inactive retain/replace/clear,
invalid new selections and stale rejection without lifecycle controls.

### Tests for User Story 3

- [X] T041 [P] [US3] Write `tests/backend/Crm.Domain.Tests/Tickets/TicketEditAssignmentTests.cs`: normalization, closed guards, at-most-one assignment, no status side effects, actual-field history and no-op preserved time/revision.
- [X] T042 [P] [US3] Write `tests/backend/Crm.Application.Tests/Tickets/EditAndAssignTests.cs`: compare persisted/requested IDs before availability, retain inactive IDs, reject new/former inactive IDs, clear without replacement check, omitted PATCH fields unchanged, stale check before no-op.
- [X] T043 [P] [US3] Write `tests/backend/Crm.IntegrationTests/Tickets/EditAssignmentConcurrencyTests.cs`: 400 invalid_version/409 ticket_changed, fresh/stale no-ops, simultaneous writers, provisioning selection races, cancellation/rollback/no orphan history, deadlock/timeout without replay and returned DTO/token consistency.

### Implementation for User Story 3

- [X] T044 [US3] Add Domain `src/backend/Crm.Domain/Tickets/Ticket.cs` detail/assignment methods: "Closed details/assignment edits are rejected even if supplied values are identical", optional single assignment, no status side effect; deterministic actual-field history sequence subject/description/category/priority and no-op preservation (after T041-T043).
- [X] T045 [US3] Define Application `src/backend/Crm.Application/Tickets/Contracts/TicketMutationDtos.cs`, `Validation/TicketMutationValidators.cs`: required expectedVersion, PATCH at least one editable field/omitted unchanged/null detail invalid, agentId required but "Nullable foreign key" clearing allowed, reject server-owned fields (after T044).
- [X] T046 [US3] Extend Infrastructure `src/backend/Crm.Infrastructure/Tickets/TicketMutationStore.cs`: sole transaction owner, ticket UPDLOCK/HOLDLOCK/version compare, category-then-agent HOLDLOCK for new IDs only, original EF token, ticket/pending-history single save/commit, no-op no save, cancellation-safe rollback/disposal and no retries (after T045).
- [X] T047 [US3] Implement Application `src/backend/Crm.Application/Tickets/Commands/UpdateTicketDetailsCommand.cs`: merge omissions, compare category ID including explicit retained inactive ID, validate only new category, domain rules/trusted actor/time/committed editVersion via T046; extend existing history DTO/projection/serializer in `Contracts/TicketDtos.cs` and Infrastructure `Tickets/TicketReadService.cs` for readable DetailsChanged before this story finishes; prove DetailsChanged through GET history and detail UI in the story tests (after T046).
- [X] T048 [US3] Implement Application `src/backend/Crm.Application/Tickets/Commands/AssignTicketCommand.cs`: retain same inactive ID, eligible new non-null ID, null clears, no status/access grant, atomic history/no-op/stale handling; extend that same DTO/projection/serializer for AssignmentChanged, including null clearing, before US3 completes; prove AssignmentChanged/null clearing through GET history and detail UI, without waiting for US5 (after T047).
- [X] T049 [US3] Implement protected API `src/backend/Crm.Api/Controllers/TicketDetailsController.cs`, `TicketAssignmentController.cs`: PATCH custom application/json/Accept-Patch and PUT assignment, omitted/null/unknown/duplicate semantics, 400/409/415/503 outcomes without validators (after T048).
- [X] T050 [US3] Extend feature `src/frontend/src/app/features/tickets/models/ticket.models.ts`, `data-access/tickets.service.ts`: expectedVersion bodies, no If-Match, no-op token unchanged, distinguish ticket_changed from other 409 errors (after T049).
- [X] T051 [US3] Implement feature `src/frontend/src/app/features/tickets/components/ticket-edit-form.component.ts`, `ticket-assignment.component.ts` and detail integration: current inactive options retained, active new choices only, unassign, ID comparison, refresh committed detail/history/list after success; extend feature `components/ticket-history.component.ts` and `models/ticket-history.models.ts` to render all DetailsChanged/AssignmentChanged actor/time/before-after/null shapes at the US3 checkpoint (after T050).
- [X] T052 [US3] Implement feature `src/frontend/src/app/features/tickets/services/ticket-edit-session.service.ts`, `components/ticket-conflict.component.ts`: preserve unsaved input on ticket_changed, explicit reload/review/resubmit, lost-response uncertainty, never automatic merge/replay (after T051).
- [X] T053 [US3] Write/run feature `src/frontend/src/app/features/tickets/components/ticket-edit-form.component.spec.ts`, `ticket-assignment.component.spec.ts`, `services/ticket-edit-session.service.spec.ts` for inactive retention/rejection/clear/no-op and preserved input on stale/transport failure (after T052).
- [X] T054 [US3] Add/run `src/frontend/e2e/tickets/edit-assignment.spec.ts`: provisioning inactivation, unrelated edits, invalid new choices, manual ownership, concurrent contexts/explicit retry and actual DetailsChanged/AssignmentChanged readable through both history endpoint and detail UI (after T053).
- [X] T055 [US3] Run T041-T043/T053/T054 and record FR-005/006/007/010/011/012 evidence in `specs/001-core-ticket-management/validation/us3.md`; verify reference changes never mutate saved ticket/history.

**Checkpoint**: US3 changes are saved atomically and DetailsChanged/AssignmentChanged are readable through GET history and the detail UI now; current inactive references and version conflicts pass. US5 is not a prerequisite for that readability.

---

## Phase 6: User Story 4 - Progress, Close, and Reopen a Ticket (Priority: P2)

**Goal**: Exact four-state lifecycle, closed restrictions and preserved reopened ticket.
**Independent Test**: Prepared tickets in each state; all 16 state pairs and retained inactive
references, without any mandatory assignment/customer approval/resolution note.

### Tests for User Story 4

- [X] T056 [P] [US4] Write `tests/backend/Crm.Domain.Tests/Tickets/TicketLifecycleTests.cs`: "Open, InProgress, Resolved, Closed", six allowed unequal transitions, four equal no-ops, remaining six pairs rejected; reopen preservation and status history.
- [X] T057 [P] [US4] Write `tests/backend/Crm.Application.Tests/Tickets/ChangeTicketStatusTests.cs`, `tests/backend/Crm.IntegrationTests/Tickets/TicketStatusTests.cs`: no retained-reference eligibility check or assignment prerequisite, stale no-op conflict, closed restrictions and failure atomicity.

### Implementation for User Story 4

- [X] T058 [US4] Add Domain `src/backend/Crm.Domain/Tickets/Ticket.cs` lifecycle method: Open -> InProgress/Resolved, InProgress -> Resolved, Resolved -> Closed/Open, Closed -> Open; equal no-op, preserve inactive references, actual status history/revision/time only (after T056/T057; merge T044 first).
- [X] T059 [US4] Implement Application `src/backend/Crm.Application/Tickets/Commands/ChangeTicketStatusCommand.cs`, `Validation/ChangeTicketStatusValidator.cs`: required status/expectedVersion, T046 scope/trusted actor/time, no unchanged category/agent revalidation; extend existing history DTO/projection/serializer to make StatusChanged readable through GET history before the US4 checkpoint; include actor/time/before-after projection assertions in the US4 API cases (after T058).
- [X] T060 [US4] Implement protected API `src/backend/Crm.Api/Controllers/TicketStatusController.cs`: PUT status, consistent invalid-version/stale/lifecycle errors, no escalation status/automation (after T059).
- [X] T061 [US4] Extend feature `src/frontend/src/app/features/tickets/data-access/tickets.service.ts`, `models/ticket-lifecycle.ts`: canonical state codes/localizable labels, allowed choices, expectedVersion and no automatic replay (after T060).
- [X] T062 [US4] Implement feature `src/frontend/src/app/features/tickets/components/ticket-status.component.ts` and detail integration: allowed changes/close/reopen, read-only closed details/assignment, reuse conflict behavior and retain inactive references; extend feature history models/component to render StatusChanged actor/time/before-after values now, not first in US5 (after T061).
- [X] T063 [US4] Write/run feature `src/frontend/src/app/features/tickets/components/ticket-status.component.spec.ts`: choices, guards, reopen preservation, 409 distinction and no implicit state changes (after T062).
- [X] T064 [US4] Add/run `src/frontend/e2e/tickets/lifecycle.spec.ts`: full create-to-reopen, retained inactive references, unassigned ticket, direct-close rejection and StatusChanged readable through history endpoint and detail UI before completing US4 (after T063).
- [X] T065 [US4] Run T056/T057/T063/T064 and US3 regressions; record FR-008/009 and SC-003 in `specs/001-core-ticket-management/validation/us4.md`.

**Checkpoint**: Every lifecycle rule passes and emitted StatusChanged entries are readable through GET history and the detail UI at US4 completion, with earlier history kinds preserved.

---

## Phase 7: User Story 5 - Review Ticket History (Priority: P2)

**Goal**: Comprehensively verify/present mixed history, paging and immutable snapshots; all entry kinds already became readable in their producing stories.
**Independent Test**: Prepared complete history, equal timestamps, large revisions and renamed/
inactive references; no live edits needed for the review journey.

### Tests for User Story 5

- [X] T066 [P] [US5] Write `tests/backend/Crm.Application.Tests/Tickets/GetTicketHistoryTests.cs`, `tests/backend/Crm.IntegrationTests/Tickets/TicketHistoryTests.cs`: exact discriminated shapes, "Revision bigint serialized as decimal string; Sequence positive int; unique together per ticket", numeric ordering, immutable snapshots/paging/auth/missing behavior.
- [X] T067 [P] [US5] Write feature `src/frontend/src/app/features/tickets/components/ticket-history.component.spec.ts`: every kind/field/null value, large revisions without Number conversion, actors/times, safe text, pages/empty/loading/error states.

### Implementation for User Story 5

- [X] T068 [US5] Verify/refine comprehensive mixed-history ordering/paging/snapshot behavior in existing Application `src/backend/Crm.Application/Tickets/Queries/GetTicketHistoryQuery.cs`, DTOs and Infrastructure `src/backend/Crm.Infrastructure/Tickets/TicketReadService.cs`; all emitted kinds already have serialization/projection support from their producing stories; no live directory join, history mutation or duplicate US1 endpoint (after T066/T067).
- [X] T069 [US5] Refine/verify comprehensive presentation in feature `src/frontend/src/app/features/tickets/components/ticket-history.component.ts`, `models/ticket-history.models.ts`: creation/all before-after kinds already render at earlier checkpoints; verify readable mixed entries, null semantics, stable pages/error states and no edit/delete controls (after T068).
- [X] T070 [US5] Integrate full history refresh/rendering in feature `src/frontend/src/app/features/tickets/pages/ticket-detail.page.ts`, `data-access/tickets.service.ts`; preserve initial history from US1 and preserve already-readable US3/US4 entries and verify comprehensive review/refresh behavior (after T069).
- [X] T071 [US5] Add/run `src/frontend/e2e/tickets/history.spec.ts` with prepared full history: who/what/when, pages/equal timestamps and snapshots unchanged after reference-label updates (after T070).
- [X] T072 [US5] Run T066/T067/T071 plus US3/US4 actual-change/no-op/rollback regressions; record FR-010/011 and SC-004 in `specs/001-core-ticket-management/validation/us5.md`.

**Checkpoint**: Complete immutable history review passes; all earlier stories remain usable.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Architecture, platform and honest end-to-end completion checks.

- [X] T073 [P] Implement/run `tests/backend/Crm.ArchitectureTests/DependencyBoundaryTests.cs`: Domain cannot reference other layers/EF/ASP.NET, Application cannot reference Infrastructure/API/EF/HTTP, Infrastructure cannot reference API, controllers cannot access persistence or implement domain rules.
- [X] T074 [P] Add/run `tests/backend/Crm.IntegrationTests/Security/AuthorizationBoundaryTests.cs`: every route protected, production rejects test keys/config, no unwanted admin/delete/escalation/communication endpoints, no sensitive diagnostics or production TLS exceptions.
- [X] T075 [P] Verify/fix actual feature Unicode/keyboard/narrow-layout/direction-aware CSS and branding variables under `src/frontend/src/app/features/tickets/` with `src/frontend/e2e/tickets/platform.spec.ts`; no new translation/organizational/branding administration.
- [X] T076 Document runnable config/provisioning/cleanup/manual OIDC steps in `docs/development/core-ticket-management.md`; align ports/scripts/envs/contracts, shared Windows connection/no DB credentials, intentional lack of database least-privilege separation, application-level immutable history/upsert-only CLI/absent admin-delete routes, test DB ownership and no real secrets/input (after T073-T075).
- [X] T077 Run complete backend/Angular/browser/quickstart required validation; record environment/results/migrations/failure evidence in `specs/001-core-ticket-management/validation/automated.md`; advisory benchmarks are not gates (after T076).
- [X] T078 Prepare the SC-005 run materials and blank anonymous observation sheet in `specs/001-core-ticket-management/validation/staff-evaluation.md`, using the canonical quickstart protocol without duplicating/changing it: fixed reviewed build/environment/synthetic fixtures, separate scored/practice cases and Evaluation Agent, five eligible support staff with no implementation/design/prior-pilot involvement, standard five-minute introduction, exact outcome-based card, permitted wording/term clarifications but no procedural help, and per-step/answer/assistance recording; record recruitment/setup as pending if human participation is unavailable (after T077).
- [ ] T079 Evaluate SC-005 with five actual representative staff and record anonymous outcomes in `specs/001-core-ticket-management/validation/staff-evaluation.md`; follow the quickstart protocol: five eligible non-implementer staff, standard five-minute introduction, fixed card/fixture sequence, no procedural assistance, anonymous step/owner/status records, and at least four successful first valid attempts; record all valid failures and product defects, exclude only documented external setup-invalid attempts, retain original run/build results and impose no time-to-completion gate; never fabricate/coached-retry acceptance (after T078; five actual participants required).
- [X] T080 Optionally execute the advisory benchmark and record results or explicit not-run disposition in `specs/001-core-ticket-management/validation/advisory-benchmark.md`; omitted/missed targets do not block completion unless adopted in spec; use a separate benchmark instance or run after staff scoring so load cannot disturb the fixed evaluation environment (after T077).
- [ ] T081 Audit FR/SC evidence, six constitutional gates, migrations/secrets/scope and completed checkboxes; record final acceptance/human dependencies in `specs/001-core-ticket-management/validation/completion.md`; no full acceptance without actual SC-005 evidence (after T079 and T080 disposition).

---

## Dependencies & Execution Order

### Phase Dependencies

- Setup: T001 -> T002 -> {T003,T004} -> T005 -> T006.
- Foundation: {T007,T008,T009} after T006; T010 after T007/T009; T011 after T007/T008/T010;
  T012 -> T013 -> {T014,T015} -> T016. Harness executes only after migrations/API exist.
- US1: {T017,T018,T019} -> T020 -> T021 -> T022 -> T023 -> T024 -> T025 -> T026 ->
  T027 -> T028 -> T029 -> T030 -> T031.
- US2: {T032,T033} -> T034 -> T035 -> T036 -> T037 -> T038 -> T039 -> T040.
- US3: {T041,T042,T043} -> T044 -> T045 -> T046 -> T047 -> T048 -> T049 -> T050 ->
  T051 -> T052 -> T053 -> T054 -> T055.
- US4: {T056,T057} -> T058 -> T059 -> T060 -> T061 -> T062 -> T063 -> T064 -> T065.
- US5: {T066,T067} -> T068 -> T069 -> T070 -> T071 -> T072.
- Final: {T073,T074,T075} after stories -> T076 -> T077 -> T078 -> T079 and optional T080
  disposition -> T081. Human evaluation is explicit, not replaceable by synthetic results.

### User Story Dependencies

Recommended serial integration graph:

```text
Setup -> Foundation -> US1 -> US2 -> US3 -> US4 -> US5 -> Final verification
```

US2/US3 use US1's aggregate/projections; US3 does not require tracking business behavior.
US4 requires US3's mutation store. US5 can be tested from prepared histories after US1,
but integrate after US3/US4 to prove real emitted payloads. Independently testable stories
share infrastructure; do not pretend shared-code dependencies are absent.

### Within Each User Story

Tests -> model/rules -> persistence/handlers -> thin endpoints -> feature UI -> integration
and browser validation. Do not concurrently modify Ticket.cs, TicketReadService.cs, feature
models/data access/routes or ticket-detail.page.ts across stories. No commit/deployment is
performed by task generation; follow the user's authorization when implementing later.

### Parallel Opportunities

Safe waves after explicit prerequisites: T003/T004; T007/T008/T009; T014/T015; each story's
test-authoring wave; T073/T074/T075. Merge API/fixture foundations before running dependent
tests. Shared implementation files remain sequential; [P] is not permission to spawn agents.

---

## Parallel Example: User Story 1

After foundation, author T017 domain tests, T018 handler tests and T019 SQL/API cases in
separate files. Merge before implementing T020-T029 and executing the suites.

## Parallel Example: User Story 2

After US1, author T032 criteria tests and T033 SQL cases independently; integrate before
T034-T038. Never parallelize shared query/service implementations across stories.

## Parallel Example: User Story 3

After US1/foundation, author T041 rules, T042 handler and T043 concurrency/reference cases
independently. T044-T052 implementation stays serial due to shared files.

## Parallel Example: User Story 4

After US3, author T056 state matrix and T057 use-case/API cases independently; T058 starts
only once US3's aggregate/mutation-store changes are merged.

## Parallel Example: User Story 5

After US1 history scaffolding, author T066 query/SQL and T067 component cases independently
using prepared histories. Integrate T068-T070 after emitted histories from US3/US4 exist.

---

## Requirement Traceability

| Requirement | Primary coverage |
| --- | --- |
| FR-001 | T017-T028/T031 |
| FR-002 | T017/T018/T020/T023/T025/T028/T031 |
| FR-003 | T024-T031/T066-T072 |
| FR-004 | T032-T040 |
| FR-005 | T041-T047/T049-T055 |
| FR-006 | T009/T012/T013/T019/T023/T025/T028/T043/T047/T051 |
| FR-007 | T041-T044/T046/T048-T055 |
| FR-008 | T056-T065 |
| FR-009 | T041/T044/T056-T065 |
| FR-010 | T021/T024/T025/T029/T043/T044/T046-T048/T058/T066-T072 |
| FR-011 | T018/T024/T041-T048/T055/T057-T059/T072/T077 |
| FR-012 | T007/T043/T045-T054/T057/T059/T064 |
| FR-013 | T008/T011/T012/T015/T016/T019/T026/T031/T033/T036/T049/T060/T074 |
| FR-014 | T013/T026/T049/T060/T074/T081: absent routes/bounded provisioning |
| SC-001 | T019/T031 |
| SC-002 | T033/T040 |
| SC-003 | T056/T057/T064/T065 |
| SC-004 | T043/T066/T067/T071/T072 |
| SC-005 | T078 preparation; T079 actual protocol-based staff evaluation |
| A-011 | T042/T043/T047/T048/T051/T054/T057/T059/T064 |
| A-012 | T013/T019/T023/T031/T043/T077 |

## Latest Decision Coverage

| Preserved decision | Concrete task/checkpoint |
| --- | --- |
| Unified /api/ticket-reference-data/categories readiness | T012 endpoint; T016 harness; T031 MVP proof; no phase-dependent endpoint switch |
| Single Windows-authenticated DB identity | T010 shared connection validation, T013 same-account CLI, T014/T016 same-account test hosts, T023 same-configuration EF factory |
| Shared connection verification and application boundaries | T019 WindowsConnectionTests, T023 schema validation, T031 and mutation-story journeys; no SQL principal creation/grants/denied-DML assertions |
| US3 history readable immediately | T047/T048 projection/serialization, T051 rendering, T053/T054 tests before T055 checkpoint |
| US4 history readable immediately | T059 projection, T062 rendering, T063/T064 tests before T065 checkpoint |
| US5 comprehensive review only | T066-T072 mixed history/paging/presentation and snapshot regressions, not first read support |
| Repeatable SC-005 protocol | T078 prepared/frozen run, T079 real observations and unchanged four-of-five threshold |

## Implementation Strategy

### MVP First (User Story 1 Only)

Complete setup/foundation and T017-T031, then demonstrate protected creation/detail/initial
history. Bootstrap/reference/test infrastructure is necessary, not an administration module.
MVP completion is distinct from full feature acceptance.

### Incremental Delivery

Add tracking, edits/ownership, lifecycle and complete history review, testing each prepared-data
journey and prior regressions. Full acceptance includes actual SC-005 participants. No SLA,
automation, escalation, communications, AI, reports, customer linking/portal, organizational
management, retention policy or advanced administration is added.

### Parallel Team Strategy

If later authorized by the user, contributors may use file-separated waves. Share interfaces/
fixtures first and keep overlapping aggregate/config/service work sequential. No agents are
launched by this task list.

## Notes

- Spec/constitution/designs are authoritative; document/clarify high-impact deviations.
- Quoted model constraints are source rules; do not invent business limits.
- Mark tasks complete only with implementation/verification evidence.
- Benchmark execution is optional; its disposition is recorded without making it a gate.
- External OIDC sign-in remains a manual deployment check, separate from signed-identity tests.

## Explicit Shared-Identity Trade-off

Runtime, EF migrations and provisioning use the same Windows identity and validated
ConnectionStrings:Crm within an environment/run. Tests change only the catalog to an owned
local SQL database and use the same account for all hosts. No three-principal mapping/grant/
effective-denial work remains. History immutability and bounded reference upserts are enforced
by supported application paths, not separate SQL credentials. The shared identity may have
schema/direct-DML rights; bypassed code/direct SQL can alter data. Do not claim database
least-privilege separation or add SQL username/password configuration to compensate.

## Phase 9: Convergence

Assessment 2026-10-09. Two verified implementation gaps remain. These corrections depend on
existing implemented story code and can proceed independently of pending T079 / SC-005 and
the provisional T081 acceptance gate. No human participants or usability outcomes are required
or fabricated by this phase. Preserve the existing specification, plan and task definitions;
write failing regressions before correcting production behavior.

- [X] T082 [US1] HIGH — Prevent obsolete explicit ticket-detail retry responses from replacing the current route's ticket or altering its history, error state, edit version or draft in `src/frontend/src/app/features/tickets/pages/ticket-detail.page.ts`; write regression tests in `ticket-detail.page.spec.ts` that start Retry ticket for A, navigate to B and load B, then deliver A's late success or failure. Verify B remains authoritative and normal same-ticket retry still works. Reuse the feature's route/request cancellation or generation approach without automatic mutation replay; run relevant detail/session/history tests and browser regressions and record correction evidence in `validation/convergence.md` per FR-003, US1/AC2, plan: Angular detail retrieval/refresh, T029/T070 (partial). Evidence F1: the current independent reload subscription accepts A after the route stream has accepted B.
- [X] T083 [US2] HIGH — Correct literal subject-substring search in `src/backend/Crm.Infrastructure/Tickets/TicketReadService.cs` for valid terms beyond CHARINDEX's supported needle length; first add real SQL/API regressions in `tests/backend/Crm.IntegrationTests/Tickets/ListTicketsTests.cs` for matching and nonmatching ASCII terms at 8,000/8,001 characters, preserving case-insensitive Unicode/literal wildcard behavior, exact-reference OR semantics, combined filters and bounded deterministic paging. Keep filtering server-side and parameterized; do not truncate input, introduce an unapproved business length limit or weaken assertions. Run list/application/API/browser regressions and record correction evidence in `validation/convergence.md` per FR-004, US2/AC3, plan: literal search and SQL-backed query projections, T035 (partial). Evidence F2: an accepted 8,001-character needle raises SQL error 8152 in CHARINDEX and becomes service_unavailable instead of a matching/empty 200 page.

## Phase 10: Development Startup Migrations (Shared Infrastructure Amendment)

**Input**: The 2026-10-09 [startup design](plan.md#local-database-migration-startup),
research decision 11, [startup validation](quickstart.md#startup-migration-validation-after-implementation)
and the shared-connection/provisioning contracts. This phase adds no business user story, data
field, REST route or frontend workflow. It supports the five existing stories without redoing them.
T001-T083 retain their existing descriptions, IDs, ordering and completion history. This phase
can execute independently of pending T079 / SC-005 and provisional T081; no real staff recruitment
or fabricated acceptance evidence is involved.

**Goal**: Development normal HTTP startup applies only existing pending compiled migrations before
serving requests. Other environments, provisioning CLI and design-time construction retain explicit
migration behavior. New model changes still require a deliberately generated/reviewed/versioned
migration; no EnsureCreated, automatic generation or migration-source rewriting is permitted.

**Independent test**: On exclusively owned local SQL catalogs, start a valid Development host with
fresh/older schema and inspect migration history plus fallback/search objects before authorized
categories readiness. Repeat start and prove previously applied migrations/data are unchanged.
Start valid non-Development hosts against pending schema and prove zero migration invocation/schema
changes. Authentication errors must not masquerade as proof that migration was skipped.

### Test Infrastructure and Test-First Verification

- [X] T084 Create `tests/backend/Crm.IntegrationTests/Fixtures/MigrationStartupFixture.cs` for exclusively owned fresh/older-schema catalogs by reusing the local server, Integrated Security, actual server/catalog/Windows identity and ownership checks in `Fixtures/SqlServerFixture.cs`; extract only minimal shared ownership helpers there if necessary, preserving ordinary fixture behavior and the development-catalog exclusion. Support a fresh catalog with no migrations, a catalog explicitly at InitialTicketStorage, and a fully migrated catalog; fixtures must not silently pre-apply pending migrations in startup-under-test scenarios. Reuse existing signed JWT/auth fixtures with valid normal host authority/audience and explicit test-only options injection for authenticated readiness; do not add Test-prefixed settings to Development/Production configuration, disable JWT validation, require a real external IdP, or create SQL principals/grants. Ensure ownership-verified cleanup on success/failure and serial SQL isolation. This task prepares tests, not production migration behavior.
- [X] T085 [P] Write `tests/backend/Crm.IntegrationTests/Persistence/DevelopmentMigrationStartupTests.cs` using T084: prove normal Development API startup applies the compiled pending migration set before HTTP readiness on a fresh catalog and a catalog at InitialTicketStorage, including the LongTicketSubjectSearch function and protected fallback, while preserving existing ticket/history data. Capture applied migration IDs, necessary objects and data before/after a second start; assert no already-applied migration is executed again (including command interception/DDL-execution evidence, not only unchanged row counts) and no runtime reference provisioning/new ticket activity occurs. Add failure/cancellation, missing migration artifacts and model/snapshot-drift cases that prevent HTTP readiness, dispose startup scope/context and expose only sanitized setup diagnostics; assert no migration/source file generation or rewriting and no EnsureCreated path. Model drift must require an explicit generated migration, without suppressing EF pending-model checks. Run the tests against current behavior and record meaningful Development failures before T087; test-only model/fault fixtures must not become production migration sources. Depends on T084; author in parallel with T086 only after T084.
- [X] T086 [P] Write `tests/backend/Crm.IntegrationTests/Security/MigrationEnvironmentBoundaryTests.cs` using T084: start valid Production, Staging, Testing and another non-Development HTTP host against schema with pending compiled migrations and assert zero automatic migration calls/DDL plus unchanged migration history/schema. Keep configuration/authentication valid and confirm host readiness so configuration rejection cannot falsely pass the guard test; retain real JWT validation and the Testing-only key boundary. Under Development verify explicit reference-provisioning CLI and `src/backend/Crm.Infrastructure/Persistence/DesignTimeCrmDbContextFactory.cs` construction bypass HTTP migration initialization; CLI keeps its applied-schema prerequisite and bounded upsert behavior, while explicit EF update remains the operation that applies migrations outside normal Development HTTP startup. Demonstrate these paths never generate migrations or use EnsureCreated. Record already-correct negative cases as passing without manufacturing failures. Depends on T084; author in parallel with T085 only after T084.

### Implementation and Completion

- [X] T087 Implement `src/backend/Crm.Infrastructure/Persistence/CrmDatabaseMigration.cs` and the normal HTTP startup hook in `src/backend/Crm.Api/Program.cs` after T085/T086 have been written/run. The API composition root must guard with IsDevelopment(), after provisioning CLI dispatch and application build but before HTTP listening/RunAsync/readiness; other environments must not invoke the helper. Await scoped CrmDbContext.Database.MigrateAsync (or equivalent) with startup cancellation and async scope/context disposal, using the same validated ConnectionStrings:Crm/local SQL Server/Windows identity. Apply only reviewed source-controlled migrations compiled into Infrastructure; require valid migration artifacts/model snapshot and retain pending-model-change failure. Never generate migrations, invoke generation tools, use EnsureCreated/EnsureCreatedAsync, inspect Git at runtime, migrate in shared AddInfrastructure registration, or launch a background migration task. Fail startup on migration failure/cancellation with sanitized diagnostics; no silent continuation, DB reset/drop, fallback credential, automatic reference provisioning, custom retry/lock framework or outer ticket transaction. Keep EF in Infrastructure/API composition and leave Domain/Application/controllers unchanged. Run T085/T086 until they pass; any necessary helper-specific tests remain in the backend test projects, not in product flows.
- [X] T088 Update `docs/development/core-ticket-management.md` after T087 passes to replace the pending-design note with verified runnable Development startup instructions, explicit model-change generation/review/versioning, optional explicit pre-start provisioning setup, and explicit migration application for non-Development/Testing paths. Preserve the shared Windows connection, migration ownership/cleanup guidance and normal external OIDC prerequisites. Keep `src/frontend/e2e/run-e2e.mjs` and the existing Testing fixture's explicit guarded migration preparation unchanged unless a verified compatibility correction is required; do not change the specification/planning requirements to make implementation pass. Clearly separate applying compiled migrations from generating new ones and never imply a runtime Git checkout or source mutation is needed. Depends on T087.
- [X] T089 Run `dotnet build Crm.slnx`, complete backend tests including T085/T086, the existing Playwright regression harness and EF migration/model-consistency checks after T088; apply no migration to a shared/development catalog merely for verification. Record test-first failures, final pass/fail results, migration IDs/objects/DDL execution evidence, repeat-start data invariants, environment/CLI/design-time exclusions, no-generation/EnsureCreated/model-drift checks, failure/cancellation/readiness/disposal results, Windows connection identity and owned cleanup in `specs/001-core-ticket-management/validation/migration-startup.md`. Verify source-controlled migration files remain unchanged by runtime execution, no secrets/schema fields/new business behavior were introduced, and tests/assertions/timeouts/auth safeguards were not weakened. Link this new evidence without rewriting prior checkpoint outcomes; mark only actually verified phase tasks complete, retaining T079 / SC-005 and T081 pending status. Depends on T088.

### Dependency Wave and Checkpoint

Existing setup, mutation/history implementation and T082/T083 are prerequisites already completed.
Execution: T084 -> {T085,T086 test authoring} -> T087 -> T088 -> T089. Parallelism is limited to
file-separated test authoring after shared fixtures are ready; retain serial/owned SQL execution
and no implicit agent authorization. Run new positive startup regressions before paired production
changes; keep currently correct negative paths passing. No user-story implementation phase, entity
constraint, original task completion marker or human-evaluation gate is rewritten by this amendment.

**Checkpoint**: Development-only startup migration behavior and all negative boundaries are
verified; original US1-US5 remain usable. Original independent story criteria and US1 MVP scope
remain unchanged. Startup applies existing migrations only, never schema inference/generation.

## Phase 11: One-Command Local Development (Shared Infrastructure Amendment)

**Input**: plan.md "One-Command Local Development", research decision 12,
contracts/local-development.md and quickstart.md launcher scenarios, 2026-10-09.
This phase supports US1–US5 without adding a business story, entity, REST route or authentication
workflow. T001–T089 retain their exact definitions, ordering and completion history. Completed
T084–T089 supply the migration implementation; verify it through the launcher rather than redoing it.
T079 / SC-005 remains pending and T081 provisional; neither blocks this tooling phase.

**Goal**: After documented one-time prerequisites, one repository-root command starts the normal
Development API and Angular frontend with live logs, a trusted same-origin /api proxy and bounded
owned-process cleanup. No Docker Compose, Aspire or new orchestration dependency.

**Independent test**: Run `pwsh -NoProfile -File ./scripts/run-dev.ps1` with an owned disposable
local SQL catalog and valid normal authentication/certificate configuration. Observe frontend HTML
200, API categories 401 and nested proxied API responses; verify authorized categories 200/fallback
and a complete ticket API journey through the frontend-origin /api proxy using the private external HTTPS issuer fixture and normal JWT validation. Real browser OIDC sign-in/UI interoperability is a separate manual check, pending if actual registered client/accounts are unavailable.
Exercise actual Ctrl+C, child/partial-start failure and restart; prove all owned descendants stop,
ports are released and an unrelated control process survives. HTTP 401 alone is not SQL/auth acceptance.

### Test-First Preparation

- [X] T090 [P] Write `tests/scripts/run-dev.tests.ps1` with test-only process fixtures under `tests/scripts/fixtures/` for `scripts/run-dev.ps1` before implementation: assert simultaneous owned child launch, inherited visible stdout/stderr, correctly forwarded paths with spaces, Development variables/API URL/no launch profile, non-interactive dotnet watch without --no-build, existing environment.ps1 reuse, caller environment/location restoration, preflight tool/dependency/occupied-port failures without affecting owners, Authentication__Test* rejection, initial 120-second readiness/timeout, watcher-alive-but-API-failed handling, partial launch and terminal child exit. Include actual console Ctrl+C plus one shared five-second shutdown grace budget from the shutdown request, cooperative exit within grace and uncooperative fallback with separately recorded graceful/forced cleanup assertions for watcher/API/npm wrapper/Angular descendants, including root-first exit, unrelated control-process survival and a second clean launch. Use standalone PowerShell assertions/native test fixtures if sufficient; no new test framework dependency merely for this script. Keep deliberate fixture faults/synthetic configuration outside application code; never kill by process name/port or modify developer data. Run meaningful failing cases against the missing launcher before T092 and record expected failures; absent production launcher is failure, not a skipped success. Depends on completed T089; test authoring may parallel T091, but process/port cases execute serially.
- [X] T091 [P] Write `tests/scripts/dev-proxy.tests.mjs` for planned `src/frontend/proxy.development.json` and `src/frontend/angular.json` before T093: cover development-only wiring, relative environment.apiBaseUrl '/api', nested /api/** forwarding with unchanged path/query and bearer headers, exclusion of /api-docs, HTTPS target 7043/secure:true, failure for untrusted backend TLS without insecure fallback, and unchanged production/e2e configuration. Use the existing Node/Angular tooling and disposable trusted HTTPS fixture or actual servers for observable forwarding; configuration assertions alone do not prove request routing. Reuse existing certificate setup without changing machine trust or exposing private material. Missing proxy/wiring must produce meaningful failures before implementation; record them. Depends on completed T089; author separately from T090 and serialize actual fixed-port server runs.

### Implementation

- [X] T092 [P] Implement `scripts/run-dev.ps1` after T090 is written/run, per contracts/local-development.md: resolve repository paths from script location; reuse scripts/environment.ps1; preflight tools/restored dependencies/certificates/free 4200 and 7043; preserve existing API configuration sources without duplicating user-secret parsing, reject inherited Authentication__Test* and never echo credentials. Start `dotnet watch --non-interactive --project src/backend/Crm.Api run --no-launch-profile` and existing `npm run start --prefix src/frontend -- --configuration development --port 4200 --ssl --host localhost` concurrently, using structured safe arguments/working directories and owned native npm wrapper if needed. Set both .NET host environments Development, ASPNETCORE_URLS https://localhost:7043 and DOTNET_WATCH_SUPPRESS_LAUNCH_BROWSER for children; allow builds/restarts and inherit live console stdout/stderr without detached windows/log-only output. Retain handles and descendant ownership, interruptible monitoring, 120-second initial categories-401/frontend-HTML-200 availability checks and both URL messages. On Ctrl+C, partial launch, timeout or terminal child exit, request owned-child shutdown where safely supported, use one shared five-second grace budget from the shutdown request, then terminate remaining owned trees; waiting alone is not a graceful request and evidence must distinguish graceful exit from forced cleanup, including descendants after root exit, without touching unrelated processes; restore caller environment/location and distinguish normal stop from nonzero setup/start/child failure. API alone applies migrations through the existing hook; no EF update/generation, EnsureCreated, SQL principals, provisioning, dependency install, certificate trust changes, test identities or custom orchestration. Run T090 until passing. Depends on T090; may implement parallel T093 in separate files, but execute fixed-port validations serially.
- [X] T093 [P] Create `src/frontend/proxy.development.json` with `/api/**` forwarding unchanged to https://localhost:7043 and secure:true; wire only `serve.configurations.development.proxyConfig` in `src/frontend/angular.json`. Reuse existing npm start, normal development build/auth and `src/frontend/src/environments/environment.ts` apiBaseUrl '/api'; retain the current origin-restricted bearer interceptor and ticket-local data access. No absolute backend URL in feature code, new environment copy, path rewrite, secure:false/global TLS bypass, e2e auth substitution or production change. Node trusts the public API certificate through the existing normal trust/NODE_EXTRA_CA_CERTS prerequisite. Run `tests/scripts/dev-proxy.tests.mjs` until passing. Depends on T091; may implement parallel T092, without concurrent fixed-port runs.

### Integrated Verification and Reproduction

- [X] T094 Exercise the actual root command through `tests/scripts/run-dev.integration.ps1`, reusing T090/T091 fixtures and existing owned SQL/JWT infrastructure where applicable: both real Development API/dotnet watch and normal Angular serve run together, log output stays visible, availability uses categories 401 plus frontend HTML 200, nested /api proxy preserves requests and normal JWT denials, and authorized categories 200/fallback plus create/view/edit/assign/resolve/close/reopen/history HTTP calls through frontend-origin /api prove usable API/proxy communication. Provide a private ephemeral test-only HTTPS discovery/JWKS issuer fixture under tests/scripts/fixtures/ that signs tokens directly for the HTTP test client; configure normal API Authority/Audience and preserve issuer/audience/signature/lifetime validation. Verify normal frontend HTML, routing/unauthenticated guard and reload separately. Discovery/JWKS does not implement authorization-code/PKCE login: do not inject tokens into SPA storage or replace the normal frontend auth adapter; never set API Authentication__Test* values, switch the launcher to Testing, inject a product auth bypass or claim synthetic issuer evidence as real external-provider sign-in. Real browser sign-in/callback and authenticated UI journeys use actual registered external OIDC client/accounts and are separate manual interoperability checks. If unavailable, record them as pending without blocking completed automated server/proxy/JWT verification or fabricating success; the existing Testing Playwright harness retains separate evidence. Verify fresh/older-schema pending migration application before API readiness and repeat start with unchanged applied IDs/data; reuse T085/T086 for model drift, failure/cancellation, no-generation/EnsureCreated and non-Development exclusions rather than changing startup code. Observe a real source edit/rebuild/restart and Angular reload with exact restoration of temporary edits, actual Ctrl+C, cooperative shutdown within the shared five-second grace budget and uncooperative forced fallback with separate outcomes, partial launch/child failure/root-first descendant cleanup, occupied-port owner survival, TLS failure and clean second launch. Use exclusively owned disposable catalogs; no development DB reset or automated reference provisioning in the launcher. Record failures and corrections without weakening assertions. Depends on T092/T093 and executes serially after fixture server tests.
- [X] T095 Update `docs/development/core-ticket-management.md` and the launcher sections of `specs/001-core-ticket-management/quickstart.md` after T094 verification: document the single root `pwsh -NoProfile -File ./scripts/run-dev.ps1` command, one-time restored SDK/Node/dependencies, local Windows SQL connection, browser/Node HTTPS trust including public-certificate NODE_EXTRA_CA_CERTS where needed, normal backend/frontend OIDC prerequisites, 4200/7043 URLs, relative /api development proxy, visible logs/watch behavior, the shared five-second shutdown grace budget, separate graceful/forced outcomes and Ctrl+C/owned cleanup. Document automated HTTP proxy/JWT journeys separately from real browser OIDC sign-in/UI prerequisites. Explain automatic application of compiled pending Development migrations versus explicit model-change generation/review/commit/rebuild and explicit setup for provisioning/non-Development. Preserve Testing harness preparation and original migration evidence; mark launcher instructions verified only to the extent actually exercised, document real-provider prerequisites honestly and abrupt-terminal-close limitations. No new product behavior, credential samples or fabricated SC-005 result. Depends on T094.
- [X] T096 Run final launcher/script/proxy tests plus `dotnet build Crm.slnx`, complete backend tests (including T085/T086), Angular unit tests/production build, existing Playwright harness and EF model-consistency checks; record commands, test-first failures, final outcomes, proxy/TLS/JWT observations, real-watch/reload, migration IDs/data invariants, live logs, the shared five-second shutdown grace budget with measured cooperative/graceful and uncooperative/forced outcomes, owned descendant cleanup/unrelated-process survival and exact one-command reproduction in `specs/001-core-ticket-management/validation/local-development.md`. Verify scripts/run-dev.ps1 does not generate migrations/use EnsureCreated/reset databases/install dependencies/provision references and src/frontend/e2e/run-e2e.mjs remains on explicit guarded Testing preparation; preserve migration/snapshot/source except reviewed launcher/proxy changes and exactly restore temporary verification edits. Record normal external OIDC manual checks as pending if unavailable, separately from automated fixture results; do not falsely claim participant evaluation or use absent participants as an implementation defect. Check secrets/scope/architecture, owned SQL/temp/process cleanup and unchanged assertions/timeouts/auth/TLS safeguards. Mark only actually verified tasks complete; T079 remains pending and T081 provisional. Depends on T095; prior evidence stays unchanged.

### Dependencies and Parallel Opportunities

Completed feature and startup phases -> {T090, T091 test authoring} ->
{T092 after T090, T093 after T091} -> T094 -> T095 -> T096.

T090/T091 use distinct test files; T092/T093 use distinct product/configuration files. These are
parallel authoring opportunities only: serialize fixed-port, console-signal and SQL verification.
No agents are implicitly authorized. Keep source-edit probes isolated and restore exact originals.
US1–US5 retain their existing independent tests and MVP scope: US1 creation/detail is still the
business MVP; this tooling checkpoint supports the existing complete feature without new stories.
No entity constraints or REST payloads changed, so no data-model reimplementation is required.

**Checkpoint**: One documented command starts both configured normal Development services with
usable /api communication and visible logs. Pending migrations apply only through the existing
Development API hook, source generation remains explicit, and owned-process cleanup is verified.
Availability, automated normal-JWT fixture evidence, real external OIDC sign-in and human SC-005
acceptance are reported separately. Original T079/T081 gates and historical results are preserved.
