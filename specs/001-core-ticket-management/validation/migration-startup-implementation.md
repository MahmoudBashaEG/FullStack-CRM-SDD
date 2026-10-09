# T086–T087 startup migration checkpoint

Date: 2026-10-09. Scope: T086 and T087 only, continuing completed T084/T085.

## Implementation

Normal Development HTTP startup awaits a scoped Infrastructure migration helper before
listening. It uses the existing validated ConnectionStrings:Crm and Windows identity.
The helper requires compiled migrations and a model snapshot, rejects pending model
changes, checks pending migrations and calls MigrateAsync only when needed. Startup
cancellation propagates and the scope/context are asynchronously disposed. Failure or
cancellation stops startup with a sanitized diagnostic.

Production, Staging, Testing and Preview hosts do not invoke the helper. Provisioning
dispatch returns before the HTTP hook; design-time construction remains explicit.
No model, migration, configuration credential, domain behavior or frontend was changed.
No EnsureCreated, automatic migration generation, runtime Git scan or custom retry
framework was introduced. Existing assertions and authentication safeguards are intact.

## Test-first findings and corrections

The eight T086 boundary cases were written and executed before adding the startup hook.
Two successive runs each passed seven cases and exposed a Development CLI issue:
HTTP application handlers required an actor during service-provider validation, then
Development EF logging contaminated the CLI's counts-only JSON stdout. CLI registration
now includes persistence only and disables logging providers for that command. The
third pre-hook run passed all eight cases. These are necessary boundary corrections.

After the hook was added, the paired startup/boundary suite passed 18 of 19 cases.
The unchanged repeat-start assertion detected EF's conditional migration-history CREATE
TABLE command on an already-current database. Checking pending migrations before
MigrateAsync avoids that command without relaxing the assertion. Final full-suite
execution includes all 19 startup/boundary cases and passes.

## Validation results

| Validation | Result |
| --- | --- |
| dotnet build Crm.slnx --no-restore | PASS; zero warnings/errors |
| dotnet test Crm.slnx --no-build --logger console;verbosity=minimal | PASS; 304 passed, zero failed/skipped |
| Domain / Application / Architecture / Integration | 33 / 78 / 6 / 187 passed |
| EF migrations has-pending-model-changes, Infrastructure/API, --no-build | PASS; no pending model changes |
| Owned SQL cleanup and ports 4200/7043 | Zero disposable catalogs; zero listeners |

SQL verification used isolated, ownership-checked local catalogs with Integrated
Security. Startup applies InitialTicketStorage and LongTicketSubjectSearch as needed;
tests check migration IDs, fallback/search objects, preserved data, repeat-start command
evidence, valid authenticated non-Development readiness, explicit CLI/design-time
boundaries, failure/cancellation/disposal, model drift and unchanged source fingerprints.
The pre-implementation T084/T085 red-test evidence remains unchanged.

## Files changed in this checkpoint

- Created tests/backend/Crm.IntegrationTests/Security/MigrationEnvironmentBoundaryTests.cs.
- Modified tests/backend/Crm.IntegrationTests/Tickets/ReferenceProvisioningTests.cs with an optional CLI environment argument.
- Created src/backend/Crm.Infrastructure/Persistence/CrmDatabaseMigration.cs.
- Modified src/backend/Crm.Api/Program.cs with the Development startup hook and required CLI boundary corrections.
- Updated only T086/T087 completion markers in tasks.md and created this checkpoint record.

No blockers or new business assumptions. Prior uncommitted planning/fixture changes
were preserved. T088/T089 documentation and final browser-validation tasks were not
started. T079/SC-005 remains unexecuted and T081 remains provisional; no usability
evidence was fabricated. No implementation hooks are configured. Changes are uncommitted.
