# US2 tracking evidence — T032–T040

Date: 2026-10-08 (Africa/Cairo). Scope: read-only ticket listing, tracking, search, filters and paging, continuing from committed T001–T031. T041 and later were not started.

## Delivered

- MediatR list query and application validation: trimmed/empty search, exact enum syntax, valid GUID syntax, all/unassigned/agent assignment filters, page >= 1 and page size 1–100 (default 25).
- Protected GET `/api/tickets`; strict single-value query binding, duplicate/unknown keys and malformed paging rejected with consistent 400 responses. Valid unknown reference IDs yield empty 200 pages.
- Parameterized SQL Server no-tracking read: exact binary-collation reference OR case-insensitive, accent-sensitive subject substring, AND every selected filter. `CHARINDEX` treats percent/underscore/brackets/backslashes literally without LIKE patterns or a full-text subsystem. Paging uses a long offset and at most pageSize+1 rows; order is UpdatedAtUtc descending, Reference ascending.
- Summary projections omit description, creation time and editVersion; current category/agent labels and inactive flags remain readable. Reads leave rowversions and history unchanged.
- Standalone Angular list/filter components, typed transport, URL-preserved criteria, paging, clearing filters, detail links, explicit retries, and distinct loading/empty/error/access states. No global state library or additional business routes.

## Test-first and verification results

| Check | Result |
| --- | --- |
| T032 application tests before implementation | 22 expected failures against the compiling handler stub |
| T033 real SQL/API tests before list endpoint implementation | 40 expected failures from missing GET support; 3 existing duplicate-query checks passed |
| `dotnet build Crm.slnx --no-restore` | Passed, zero warnings/errors |
| `dotnet test Crm.slnx --no-restore` | Domain 6, Application 39, real SQL/API 66 passed; zero failures/skips in these suites |
| New tracking SQL suite | All 43 passed, including Unicode, Latin casing/accent sensitivity, literal characters, exact-reference OR subject matching, combined filters, inactive/unknown IDs, ordering ties, default paging/hasMore, maximum integer page, authorization and dependency failure |
| `npm run build --prefix src/frontend` | Production build passed |
| `npm test --prefix src/frontend -- --watch=false` | 29 passed, including filter/page/transport/navigation/error/race cases |
| `npm run e2e --prefix src/frontend` | All 6 passed: 3 US1 regressions and 3 tracking scenarios |
| Architecture suite | Existing scaffold only; T073 not executed. Inward dependencies and thin HTTP dispatch preserved |
| Scope/whitespace review | Passed; no placeholder handlers or future mutation implementations; specification/design/constitution unchanged |

SC-002 uses 32 explicitly prepared records spanning every status/priority, assigned/unassigned tickets, active/inactive references and tied update times. An independent expected-record predicate checks exact ordered results in 26 SQL search/filter cases and 21 browser/API cases. Browser cases also include tickets legitimately created by US1 in the same shared queue; fixture membership is asserted, extras captured once, then expected filtering and ordering calculated independently. Assertions compare full ordered reference arrays, including zero matches. Clear-filter, 25/7 paging, detail navigation, denial and explicit failure/retry behavior also pass.

Existing initial migration `20261008164804_InitialTicketStorage` was applied by the owned integration/browser fixtures. No new migration or development database changes were required. All hosts used the existing local SQL Server connection and Windows identity; no SQL credentials/principals/grants were introduced. Readiness remains `/api/ticket-reference-data/categories` with unauthenticated 401 and authorized 200. Servers, temporary authentication files and owned test catalogs were cleaned up.

## Problems found and corrected

1. UI tests exposed an obsolete request remaining active after duplicate URL criteria; the invalid route now cancels it rather than allowing stale results to replace the error state.
2. Hexadecimal/non-integer page syntax was silently normalized by JavaScript Number conversion. Invalid raw syntax is now transported unchanged for the strict API rejection. Valid paging and all filter criteria remain preserved.
3. SQLCMD defaults left QUOTED_IDENTIFIER off, incompatible with the existing computed identity index. The synthetic script explicitly enables the required option, and SQLCMD receives `-I` and UTF-8 input (`-f 65001`). No index/constraint was weakened.
4. The first browser oracle omitted a matching Arabic ticket from US1. The oracle now includes legitimate extra records in the shared queue while retaining complete ordered equality assertions.
5. The task-completion script's T031 guard was extended only to T040. The prior application test fake gained the new read-port member; no prior behavior was reimplemented.

## Assumptions and limits

- Prepared SQL data deliberately represents future lifecycle/assignment states without adding production mutations. Only already-readable Created history is seeded; these fixtures do not certify later lifecycle or full history behavior.
- Read-only filter choices combine available/eligible lookup choices with inactive references already seen in queue results; deep-linked unknown IDs retain their identifier label. This adds no category/user administration or new reference endpoint.
- CHARINDEX is an equivalent literal-search implementation detail. It retains the planned collation, parameterization and exact-reference semantics.
- Existing OIDC production configuration remains an external deployment prerequisite. Automated tests use genuine signed Testing-only JWTs and the unchanged shared-queue policy.
- No new packages, schema, business fields, permissions, benchmark requirements or product scope were introduced. No outstanding blocker or requirement deviation remains for this checkpoint. Full feature acceptance and SC-005 remain pending later tasks.
- `.specify/extensions.yml` is absent; no implementation hooks were registered.

## Created files

- `src/backend/Crm.Api/Binding/TicketListRequest.cs`
- `src/backend/Crm.Api/Controllers/TicketListController.cs`
- `src/backend/Crm.Application/Tickets/Queries/ListTicketsQuery.cs`
- `src/backend/Crm.Application/Tickets/Validation/ListTicketsValidator.cs`
- `tests/backend/Crm.Application.Tests/Tickets/ListTicketsTests.cs`
- `tests/backend/Crm.IntegrationTests/Fixtures/TrackingTickets.sql`
- `tests/backend/Crm.IntegrationTests/Tickets/ListTicketsTests.cs`
- `src/frontend/src/app/features/tickets/components/ticket-filters.component.ts`
- `src/frontend/src/app/features/tickets/components/ticket-filters.component.spec.ts`
- `src/frontend/src/app/features/tickets/pages/ticket-list.page.ts`
- `src/frontend/src/app/features/tickets/pages/ticket-list.page.spec.ts`
- `src/frontend/e2e/tickets/tracking.spec.ts`
- `specs/001-core-ticket-management/validation/us2.md`

## Modified files

- `src/backend/Crm.Application/Tickets/Contracts/TicketDtos.cs`
- `src/backend/Crm.Application/Tickets/Ports/ITicketReadService.cs`
- `src/backend/Crm.Infrastructure/Tickets/TicketReadService.cs`
- `tests/backend/Crm.Application.Tests/Tickets/CreateAndGetTicketTests.cs`
- `src/frontend/src/app/features/tickets/models/ticket.models.ts`
- `src/frontend/src/app/features/tickets/data-access/tickets.service.ts`
- `src/frontend/src/app/features/tickets/data-access/tickets.service.spec.ts`
- `src/frontend/src/app/features/tickets/tickets.routes.ts`
- `src/frontend/src/app/app.component.html`
- `src/frontend/e2e/run-e2e.mjs`
- `scripts/e2e-database.ps1`
- `scripts/complete-task.ps1`
- `specs/001-core-ticket-management/tasks.md`

## Reproduce

Use the [US1 environment/owned-database commands](us1.md#reproduce-this-checkpoint) with fresh integration/E2E catalog names and the same Windows identity. Run the full solution test suite, Angular build/unit tests, then `npm run e2e --prefix src/frontend`. The existing runner now loads the ownership-verified shared tracking SQL fixture before starting Playwright servers. Missing SQL configuration/access or any failing scenario remains a failure, not a skip.
