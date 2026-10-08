# Convergence corrections — T082 and T083

Validated 2026-10-09 (Africa/Cairo). This run implemented only T082–T083 on top of the current
implementation, preserving the pre-existing uncommitted T073–T078/T080 work. Spec, plan,
research, data model, contracts, quickstart, constitution and checklist markers remain unchanged.
The requirements checklist was 16 checked / 0 unchecked. No completed story was reimplemented.
T079 / SC-005 was not attempted; T081 remains provisional. No usability evidence was fabricated.

## T082 — obsolete ticket retries

Root cause: the route detail subscription used switchMap, but explicit Retry ticket opened an
independent subscription whose lifetime ended only when the component was destroyed. Angular
can reuse that detail component for another reference. A retry for A could therefore accept A's
late response after the route stream had accepted B, replacing B's detail/edit version/seed and
refreshing history for the wrong combination of displayed ticket and current route. An obsolete
error could also replace B's current error state. Separate same-ticket retries could race.

Route loads and explicit retries now feed one feature-local detailRequests stream with switchMap.
Navigation or a newer retry cancels the previous read. Component destruction disposes the stream
and its current inner request. Success still accepts authoritative detail and refreshes history/
references; conflict reload/review remains separate and retains its existing draft behavior.
No mutation replay, new global state or business workflow was added.

Three new Angular regressions prove:

- A late retry success for A cannot alter loaded B, B's version/seed, history, error or unsaved draft.
- A late retry failure for A cannot change B's state.
- An older same-ticket retry is cancelled while the current retry succeeds normally.

They were written and executed before the correction: 3 failed / 68 existing tests passed.
After the correction, all 71 Angular tests pass, including detail/session/history regressions.
Cancelled requests cannot be delivered by Angular's testing backend; the tests assert cancellation
and preservation of B. Before the fix they delivered the stale responses to reproduce the behavior.

## T083 — complete long literal searches

Root cause: the query passed the entire nvarchar search term to CHARINDEX. On the installed SQL
Server, a read-only probe returned a match for 4,000 UTF-16 units but error 8152 for 4,001, 7,999,
8,000 and 8,001. The accepted terms were valid under the existing search contract. DatabaseOperation
correctly sanitized the SQL exception as a dependency failure, but that meant valid searches
incorrectly returned 503 instead of matching/empty 200 pages.

The query retains the original CHARINDEX fast path for terms of at most 8,000 bytes. A CASE inside
its needle argument prevents the oversized term from reaching CHARINDEX even if SQL evaluates
predicate branches eagerly. Longer terms use migration-managed dbo.TicketSubjectContainsLiteral.
Its bounded 2,000-character SC prefix only finds candidate positions; full-term SUBSTRING equality
under the existing CI/AS/SC collation decides each match. It advances one character through candidate
positions to retain overlapping matches. Supplementary characters use consistent SC character
counting; the prefix occupies at most 4,000 UTF-16 units, keeping the underlying lookup safe.

This is an implementation branch, not a new search limit. The full term is preserved and checked;
no prefix-only result, input truncation, LIKE wildcard interpretation, client-side filtering, new
validation cap, full-text engine or new database compatibility setting was introduced. Exact binary
reference matching remains ORed with subject search, and all filters, ordering and bounded paging
remain in the parameterized SQL query. The function is a persistence detail, with no Domain/HTTP
dependency or new business field. Existing no-tracking projections remain unchanged.

Added version-controlled migration `20261008224935_LongTicketSubjectSearch` and its generated
designer. Up creates the function; Down drops it. Entity mappings and the model snapshot are unchanged.
Both the initial storage migration and this migration were applied to fresh owned integration/E2E
databases. Apply this new migration using the existing shared ConnectionStrings:Crm before running
the updated API against an existing installation; the development catalog was not changed by this run.

Seven new SQL/API regressions cover matching/nonmatching 8,000/8,001-character ASCII terms,
case-insensitive full matching and literal percent/underscore/brackets/backslash, overlapping
candidates, supplementary Unicode, and combined status/category/priority/unassigned filters with
tied-time/reference-order paging. Nonmatches differ after the prefix to detect truncation. All seven
failed with 503 before the fix. Afterwards all 50 list SQL/API cases pass, including earlier exact
reference, Unicode, literal search, authorization, invalid syntax and failure-state regressions.

A new Playwright case submits the complete 8,001-character term through the real Angular search
form, HTTPS proxy and API. It asserts 200/matching reference, an unchanged URL term, then 200/empty
for a term differing at its end. Existing browser journeys also pass with the new migration.

## Executed validation

Existing pinned toolchain: .NET SDK 10.0.400, EF 10.0.12, local SQL Server 17.0.1000.7,
Node 24.20.0/npm 11.19.0, Angular 22.2.1 and Playwright 1.64.0. All database hosts/fixtures
use one inherited Windows identity and the same Integrated Security connection within each run.
Only fresh fixture-owned catalogs were used; no SQL principals, grants or passwords were added.

| Command / check | Result |
| --- | --- |
| Angular tests before T082 production change | 3 new failures, 68 existing passes |
| Focused Long_ SQL/API tests before T083 production change | 7 failures, all valid searches returned 503 |
| `dotnet ef migrations add LongTicketSubjectSearch --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --output-dir Persistence/Migrations` | Generated migration/designer; reviewed function SQL added afterwards |
| `dotnet build Crm.slnx --no-restore` | Passed, zero warnings/errors |
| `dotnet test tests/backend/Crm.IntegrationTests --no-build --filter FullyQualifiedName~ListTicketsTests` | 50 passed |
| `dotnet test Crm.slnx --no-build --logger 'console;verbosity=minimal'` | 285 passed: Domain 33, Application 78, Architecture 6, SQL/API 168; zero failures/skips |
| `npm test --prefix src/frontend -- --watch=false` | 71 passed across 11 files |
| `npm run build --prefix src/frontend` | Production build passed, existing budgets satisfied |
| `npm run e2e --prefix src/frontend` | 19 passed, 57.9 seconds, zero retries; both migrations applied |
| `dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build` | No pending model changes |
| Planning/spec/constitution comparison | Unchanged |
| `git diff --check` | Passed, only ordinary LF/CRLF notices |
| Final cleanup probes | Zero disposable integration/E2E databases, private e2e directories and listeners on 4200/7043 |

No test assertion, retry policy, timeout, validation rule or quality gate was weakened. No runtime
package/configuration dependency or database schema field was added. The optional load benchmark
was not run; long repetitive text can require multiple candidate checks and its runtime is not
claimed as benchmark performance. External OIDC sign-in and actual staff evaluation remain outside
these automated claims. No additional blocker or business assumption was introduced.

## Files changed by this run

Modified:

- `src/frontend/src/app/features/tickets/pages/ticket-detail.page.ts`
- `src/frontend/src/app/features/tickets/pages/ticket-detail.page.spec.ts` — three new regressions
- `src/backend/Crm.Infrastructure/Tickets/TicketReadService.cs`
- `tests/backend/Crm.IntegrationTests/Tickets/ListTicketsTests.cs` — seven new SQL/API cases
- `src/frontend/e2e/tickets/tracking.spec.ts` — one new browser journey
- `specs/001-core-ticket-management/tasks.md` — T082/T083 completion markers only for this run

Created:

- `src/backend/Crm.Infrastructure/Persistence/Migrations/20261008224935_LongTicketSubjectSearch.cs`
- `src/backend/Crm.Infrastructure/Persistence/Migrations/20261008224935_LongTicketSubjectSearch.Designer.cs`
- `specs/001-core-ticket-management/validation/convergence.md`

Pre-existing uncommitted cross-cutting files were preserved. Changes remain uncommitted.
`.specify/extensions.yml` is absent; no pre/post implementation hooks are registered.
