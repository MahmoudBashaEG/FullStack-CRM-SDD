# US5 comprehensive ticket history checkpoint: T066–T072

Validated 2026-10-08, continuing committed T001–T065 (`0d9cfc4`). T066–T072 are complete. T073–T081 were not started. The specification, plan, research, data model, contracts, quickstart and constitution remain unchanged.

## Delivered behavior and FR-010/011/SC-004 evidence

History continues to use the existing protected GET endpoint and immutable entry model. Creation, every editable detail field, assignment/reassignment/clearing, and lifecycle changes are readable together, with trusted actor/time and exact before/after values. No history editing/deletion, duplicate endpoint, live actor/reference-directory join, event replay, new audit subsystem or future module was introduced.

The prepared 33-entry history covers Created; DetailsChanged subject/description/category/priority; AssignmentChanged null-to-agent, agent-to-agent and agent-to-null; and StatusChanged progress/resolve/close/reopen. Every timestamp is identical. Insertion is deliberately reversed, while expected reads follow numeric revision then sequence. Revisions include 9/10 and consecutive strings `9007199254740992`/`9007199254740993`, proving ordering and lossless rendering beyond JavaScript's safe integer range. Default pages contain 25 then 8 entries; page 3 and extreme valid offsets return empty success.

API tests assert exact property sets, discriminator/value shapes, string revisions, positive sequences, IDs, actors and UTC times. SQL constraints reject duplicate revision/sequence pairs and nonpositive order values without partially inserting entries. Reads use no-tracking projections and leave ticket metadata unchanged. Current reference labels/inactive flags may differ from historical labels; supported CLI renaming changes current detail presentation without changing editVersion/update time or any historical payload.

A separate real-command journey creates a ticket, changes four details together, assigns/reassigns/clears and performs all six allowed lifecycle transitions. Its 14 entries retain the actual original/replacement labels and responsible signed identities. Fresh detail/assignment/status no-ops and rejected blank content, assignment, transition and stale requests add zero entries and preserve saved metadata. Existing US3/US4 rollback, cancellation, stale/no-op, timeout and deadlock suites also pass. This provides SC-004 evidence for the tested successful operations and zero activity from failed/no-op operations.

The existing query/DTO/serializer already satisfied mixed-history shapes, numeric ordering, snapshots and bounded reads; those components were verified instead of rewritten. The history HTTP binding now parses decimal paging explicitly, matching the established ticket-list convention. The feature component preserves every prior kind/field/null representation and treats revisions as opaque strings. The detail page now cancels obsolete history requests on paging, mutation refresh or route change, and retries the actual failed page rather than the last displayed page. Successful mutations refresh authoritative page-one history; failed writes do not fabricate local entries.

## Test-first and final validation

| Check | Result |
| --- | --- |
| T066 application tests before production changes | All 8 passed against existing behavior |
| T066 SQL tests after fixture-loader correction, before HTTP correction | 19 passed; hexadecimal page syntax unexpectedly produced 200 rather than 400 |
| T067/history-page tests before frontend changes | 3 failed, 62 passed: missing attempted-page retry input, obsolete requests not cancelled, retry selecting the last displayed page |
| `dotnet build Crm.slnx --no-restore` | Passed, zero warnings/errors |
| Backend tests | Domain 33, Application 78, SQL/API 150 passed: 261 tests, zero failures/skips |
| New history SQL/API suite | All 23 passed: shapes/order/paging, snapshots, authorization/absent mutation routes, sanitized read failure, real mutation history and storage order constraints |
| `npm test --prefix src/frontend -- --watch=false` | All 68 passed, including every history kind/field/null, safe text, actor/time, large revisions, paging bounds/loading/empty/error, refresh/no-op behavior and cross-ticket cancellation |
| `npm run build --prefix src/frontend` | Production build passed |
| `npm run e2e --prefix src/frontend` | All 15 passed: 13 earlier-story regressions and 2 comprehensive history journeys |
| Migration consistency check | `dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build`: no changes since the last migration |
| Migration execution | Existing InitialTicketStorage applied by owned integration/browser fixtures; no new migration or development-catalog schema change |
| Architecture scope | Inward dependencies and thin MediatR dispatch remain. ArchitectureTests is the existing scaffold with no discovered tests; T073 is not implemented or claimed as automated architecture coverage |
| Final cleanup | Zero owned integration/E2E catalogs, temporary e2e directories and listeners on 4200/7043 |

The full backend suite ran before the final three storage-constraint cases were added; the complete SQL suite then ran again with those cases and passed all 150. Domain/application source was unchanged. Tests were written before paired production corrections; existing correct behavior was not replaced with artificial stubs. No assertions, constraints, retry counts or browser timeouts were weakened.

## Reproduction

From the repository root, use a fresh catalog for each run. Existing test catalogs are rejected rather than reset.

```powershell
. ./scripts/environment.ps1
dotnet build Crm.slnx --no-restore
$taskCatalog = 'CrmTicketsIntegration_' + [guid]::NewGuid().ToString('N')
$env:CRM_TEST_SQL_CONNECTION = "Server=localhost;Database=$taskCatalog;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
dotnet test Crm.slnx --no-build --logger 'console;verbosity=minimal'
npm test --prefix src/frontend -- --watch=false
npm run build --prefix src/frontend
$taskCatalog = 'CrmTicketsE2E_' + [guid]::NewGuid().ToString('N')
$env:CRM_E2E_SQL_CONNECTION = "Server=localhost;Database=$taskCatalog;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
npm run e2e --prefix src/frontend
dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build
```

All fixture/API/EF/provisioning processes retain the same local SQL Server connection and Windows account within a run. Only the owned catalog changes. No SQL credentials/principals/grants or identity switching were added. The history fixture uses inactive reference rows, preserving earlier selectable-reference tests. Browser startup uses the existing protected categories readiness URL, real signed Testing-only JWTs, HTTPS/proxy setup, guarded SQL ownership and zero retries.

## Problems discovered and corrections

1. EF raw SQL formatting interpreted literal JSON braces in the synthetic fixture as format placeholders. The test loader now executes the fixed fixture text through the owned context's database command; production persistence was not changed. A nullable tuple annotation in the new test was also corrected before running its assertions.
2. ASP.NET integer query binding accepted hexadecimal history-page input. A small HTTP binding helper now uses the same explicit decimal parsing convention as the existing list endpoint. Application still owns page bounds, and authorization still precedes binding/existence decisions.
3. Multiple history requests for one ticket could finish out of order and overwrite a newer page. A feature-local switchMap stream now cancels obsolete reads, including on route change and successful mutation refresh.
4. A failed page-two request retained displayed page one, and Retry history requested page one. The parent tracks the attempted page and passes it to the component; retry preserves page two. Existing loaded entries remain visible during failure, distinct from empty success.

## Assumptions, deviations and limits

- No new business assumption, product behavior outside the approved history scope, or dependency-order deviation was introduced. Immutable history remains an application guarantee under the project's shared Windows database identity, not a claim that privileged direct SQL updates are denied.
- Prepared fixture data represents a complete prior account solely for isolated read tests. Actual-command and earlier mutation suites separately prove emitted history and atomicity; no production fixture/admin endpoint was added.
- Existing external OIDC deployment configuration remains a prerequisite. Signed Testing JWT validation is exercised; external IdP sign-in and SC-005 human usability evaluation are not claimed as complete.
- The API still promises no snapshot across separate offset-page requests; obsolete client-response cancellation does not create such a guarantee.
- No new package, schema, migration, business field, global state library or future CRM capability was added. No remaining blocker was found. Final architecture/security/platform checks and human evaluation remain T073+ work.
- `.specify/extensions.yml` is absent; no pre/post implementation hooks are registered.
- Changes remain uncommitted at checkpoint completion.

## Created files

- `tests/backend/Crm.Application.Tests/Tickets/GetTicketHistoryTests.cs`
- `tests/backend/Crm.IntegrationTests/Tickets/TicketHistoryTests.cs`
- `tests/backend/Crm.IntegrationTests/Fixtures/FullTicketHistory.sql`
- `src/backend/Crm.Api/Binding/TicketHistoryRequest.cs`
- `src/frontend/src/app/features/tickets/components/ticket-history.component.spec.ts`
- `src/frontend/e2e/tickets/history.spec.ts`
- `specs/001-core-ticket-management/validation/us5.md`

## Modified files

- `src/backend/Crm.Api/Controllers/TicketHistoryController.cs`
- `src/frontend/src/app/features/tickets/components/ticket-history.component.ts`
- `src/frontend/src/app/features/tickets/pages/ticket-detail.page.ts`
- `src/frontend/src/app/features/tickets/pages/ticket-detail.page.spec.ts`
- `src/frontend/e2e/run-e2e.mjs`
- `scripts/e2e-database.ps1`
- `scripts/complete-task.ps1`
- `specs/001-core-ticket-management/tasks.md`
