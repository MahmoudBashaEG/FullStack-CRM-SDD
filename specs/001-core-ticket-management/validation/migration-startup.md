# Development migration startup: final automated validation

Date: 2026-10-09. Checkpoint: T088–T089, following completed T084–T087.
T079/SC-005 remains pending and unexecuted; T081 remains provisional.

## Scope and documentation

Updated the development guide with runnable Development environment/start commands,
the shared ConnectionStrings:Crm Windows connection, optional explicit migration setup
before provisioning, explicit preparation for non-Development/Testing, and deliberate
model-change migration generation/review/build/versioning. Runtime application of compiled
migrations requires neither generation tools nor a Git checkout. External OIDC deployment
prerequisites, test ownership/cleanup and the existing Testing browser preparation remain.

No product source, test assertion, timeout, authentication safeguard, model, migration,
specification or planning requirement was changed during T088–T089. Existing uncommitted
planning and T084–T087 implementation changes were preserved.

## Test-first history

See [T084–T085 baseline](migration-startup-test-first.md): eleven new cases initially
yielded five passes and six meaningful Development startup failures; the full suite had
290 passes and six failures. That original evidence is unchanged.

See [T086–T087 checkpoint](migration-startup-implementation.md): boundary tests exposed
Development CLI handler registration and logging/JSON issues, corrected before adding
the startup hook. After implementation, the unchanged repeat-start assertion caught
conditional migration-history DDL. A pending-migration check now avoids invoking
MigrateAsync for an already-current catalog. Existing assertions were preserved.

## Commands and results

Commands ran from the repository root after scripts/environment.ps1. SQL tests and
the browser harness used different fresh, exclusively owned CrmTicketsIntegration_ /
CrmTicketsE2E_ catalogs on localhost with Integrated Security, encrypted transport and
the approved local TrustServerCertificate exception. No shared development catalog was
migrated for verification and no SQL principal or password was introduced.

| Command | Final result |
| --- | --- |
| dotnet build Crm.slnx | PASS; zero warnings/errors |
| dotnet test Crm.slnx --no-build --logger 'console;verbosity=minimal' | PASS; 304 passed, zero failed/skipped |
| Backend breakdown | Domain 33, Application 78, Architecture 6, Integration 187 |
| npm test --prefix src/frontend -- --watch=false | PASS; 71 tests across 11 files |
| npm run build --prefix src/frontend | PASS; production Angular bundle |
| npm run e2e --prefix src/frontend | PASS; 19 tests, one worker, zero retries; 56.6 seconds |
| dotnet ef migrations list --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build --no-connect | Lists the two existing compiled migrations; offline mode intentionally cannot report database-applied status |
| dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build | PASS; no pending model changes |
| Git comparison of migrations, browser runner and Playwright configuration | Unchanged |
| SQL catalog and listener cleanup probes | Zero disposable integration/E2E catalogs; zero listeners on 4200/7043 |

Browser results include create/view, edit/assignment, lifecycle, comprehensive history,
concurrency/recovery, long literal search, tracking/filtering and Arabic/English narrow
layout/keyboard regressions. Runner preparation explicitly applied both migrations and
provisioned four synthetic references before the Testing HTTP host; it was not converted
to Development startup behavior. Ephemeral keys/input were disposed by existing cleanup.
Ordinary Node color-environment warnings did not affect results.

## Startup evidence and invariants

The passing DevelopmentMigrationStartupTests and MigrationEnvironmentBoundaryTests
provide 19 real SQL/host cases, including the following evidence:

- Fresh Development startup applies 20261008164804_InitialTicketStorage and
  20261008224935_LongTicketSubjectSearch before authorized categories readiness. Tables,
  the protected Uncategorised fallback and dbo.TicketSubjectContainsLiteral exist.
- Older-schema startup applies only pending compiled migrations and preserves saved
  Unicode ticket/history data. Repeat current-schema startup has no migration-write
  commands, duplicate migrations, reference provisioning or ticket activity. Before/after
  snapshots include ticket row versions, history and reference data.
- Production, Staging, Testing and Preview start with valid JWT configuration against
  pending schema and perform no startup migration commands/context activity or DDL.
  Authorized readiness succeeds; missing/denied credentials remain 401/403. Migration
  IDs and the missing search function stay unchanged.
- Development provisioning CLI bypasses the HTTP hook: fresh schema fails with its
  documented database exit code; prepared schema supports bounded upserts. Design-time
  context construction does not migrate; explicit migration APIs apply the pending set.
- Command failure, cancellation, missing compiled migration artifacts and model drift
  prevent HTTP readiness. Diagnostics exclude connection strings/private fault markers;
  startup scope/context disposal and observed cancellation are asserted.
- Backend source fingerprints remain unchanged by execution. Policy assertions reject
  EnsureCreated calls and suppressed pending-model warnings. No migration was generated
  or existing migration/snapshot rewritten during validation.
- Passing WindowsConnectionTests compare API/design-time connection and actual SQL login
  values with the owning fixture, and compare OS Windows identity across process usage.
  SQL ORIGINAL_LOGIN and SUSER_SNAME aliases are compared separately; literal equality
  between those display names is not an identity requirement.

The hook remains in API composition and EF implementation in Infrastructure. Domain,
Application, controllers, ticket transactions/history and frontend feature boundaries
are unaffected; all six architecture tests pass. No secrets, schema fields, extra modules
or new business behavior were introduced in this checkpoint.

## Disposition

T088 and T089 are complete. No blocker or deviation remains for this scope. The development
guide links this evidence. No extension hooks are configured. Full human acceptance is
not claimed: no eligible participants were recruited or fabricated, no SC-005 observations
were produced, and T079/T081 remain unchecked. Changes are uncommitted.
