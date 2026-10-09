# Migration startup test-first checkpoint — T084–T085

2026-10-09. The user clarified the task-ID/behavior mismatch by choosing the first option:
T084–T085 fixtures and test-first verification only, leaving the production hook for T087.
This checkpoint does **not** implement automatic Development startup migrations and is not
a passing feature/phase acceptance result. T086–T089 were not attempted. T079 / SC-005 and
the provisional T081 gate are unchanged; no human observations were invented.

## Delivered fixture and test work

MigrationStartupFixture creates a separate fresh CrmTicketsIntegration_ catalog per case,
changing only the catalog from the configured local Windows-authenticated test connection.
It delegates actual server/catalog/ownership verification and cleanup to SqlServerFixture.
Its three explicit modes preserve a fresh unmigrated state, InitialTicketStorage-only state,
or fully migrated state. No fixture secretly migrates a fresh/older startup-under-test catalog.
Failure cleanup verifies ownership before dropping anything. No SQL principal, grant, password,
staff impersonation, development-catalog reset or alternative runtime DB connection was added.

SqlServerFixture retains its one public parameterless xUnit constructor and existing default
behavior. An internal constructor accepts the validated owned connection and migration-selection
flag for the new fixture. Ordinary tests still apply committed migrations exactly as before.

The new API factory uses normal Development authority/audience settings, without Test-prefixed
configuration. Test-only static OIDC metadata supplies the existing ephemeral public key while
the real JWT scheme still validates signatures/issuer/audience/expiry. Authorized categories
readiness passes on a fully migrated fixture; expired and independently signed tokens get 401.
No production auth code or certificate-validation policy changed; no external IdP is needed.

Command interception records migration DDL/history writes and context instances. The fixture also
supplies test-only migration-command failure, host-stop cancellation, absent migration assembly
and model-drift configurations for subsequent production implementation. These are not application
migrations, business fields or bypasses. Backend source/migration fingerprints detect runtime file
creation/rewriting; source checks prohibit EnsureCreated calls and named model-check suppression.

## Eleven new cases and current results

| Case | Current result |
| --- | --- |
| Fixture Fresh state | PASS: no migrations/ticket schema/search function |
| Fixture InitialStorage state | PASS: only initial migration, no search function |
| Fixture Current state | PASS: both existing migrations and required schema |
| Repeated start on Current fixture | PASS: authenticated readiness, expired/wrong-signature rejection, zero migration DDL, unchanged migration/data/source snapshots |
| No EnsureCreated/model-warning suppression in application source | PASS |
| Development start on Fresh fixture | Expected FAIL: applied history stays empty instead of applying both migrations |
| Development start on InitialStorage fixture | Expected FAIL: search migration remains pending |
| Migration-command failure must stop startup | Expected FAIL: no migration call is made, so the current API starts normally |
| Cancellation must stop startup and dispose context | Expected FAIL at startup-failure assertion; no migration hook/token propagation exists yet |
| Missing migration artifacts must stop startup | Expected FAIL: current startup does not inspect migration artifacts |
| Model/snapshot drift must stop startup | Expected FAIL: current startup never invokes migration/model validation |

The Development positive cases also assert the search function and protected fallback, observed
migration DDL, disposed startup context, preserved older ticket/history metadata, and no implicit
agent/reference provisioning. Fault cases require failed HTTP startup, sanitized setup diagnostics,
unchanged migration/source files and appropriate disposal/cancellation evidence. Those assertions
remain in place for T087; they are not claimed to have passed beyond the current first failure.
Successful no-op startup by itself does not prove the positive migration hook exists; the failing
fresh/older-schema cases deliberately expose that missing behavior.

## Executed commands and results

| Check | Result |
| --- | --- |
| `dotnet build Crm.slnx --no-restore` | PASS: zero warnings/errors |
| `dotnet test tests/backend/Crm.IntegrationTests --no-build --filter FullyQualifiedName~DevelopmentMigrationStartupTests --logger 'console;verbosity=minimal'` | 5 passed, 6 expected failures, zero skipped; exit 1 |
| `dotnet test Crm.slnx --no-build --logger 'console;verbosity=minimal'` | 290 passed, 6 expected failures, zero skipped; exit 1. Domain 33, Application 78, Architecture 6 all pass; SQL/API 173 pass / 6 fail |
| Pre-existing regression cases | All 285 pass; the five additional passes are fixture/policy/current-schema cases |
| `dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build` | No pending model changes |
| Source/runtime migration checks | Application files unchanged by this run/startup; no new migration was generated |
| Cleanup probes | Zero disposable integration/E2E catalogs and listeners on 4200/7043 |
| `git diff --check` | PASS, ordinary LF/CRLF notices only |

The new tests were executed before any production implementation and are intentionally red.
No assertion, skip marker, conditional success, test timeout or authentication requirement was
weakened to hide the missing behavior. The full backend suite is currently **not green**.
Frontend/browser runs were not needed for test-only backend fixture changes; application code,
browser harness, model, migrations and business behavior remain untouched.

Existing pinned SDK/EF/SQL Server and Integrated Security environment were reused. Only fixtures
explicitly prepared owned test catalogs; normal API startup still has no automatic migration.
Prior uncommitted planning changes were preserved. Neither spec nor planning text was adjusted
to pass these tests. No application-level migration failure/cancellation behavior is accepted yet.

## Files changed

- Modified `tests/backend/Crm.IntegrationTests/Fixtures/SqlServerFixture.cs` with a test-only initialization option.
- Created `tests/backend/Crm.IntegrationTests/Fixtures/MigrationStartupFixture.cs` with owned modes, signed-auth host and command/fault probes.
- Created `tests/backend/Crm.IntegrationTests/Persistence/DevelopmentMigrationStartupTests.cs` with 11 cases.
- Updated only T084/T085 completion markers in `specs/001-core-ticket-management/tasks.md` for this run.
- Created this checkpoint record.

T084 fixture preparation and T085 test authoring/baseline execution are complete. Production behavior
and making the red tests pass require T087 after the T086 environment/CLI verification prerequisite;
they were outside the clarified scope. No unrelated blocker or business assumption was introduced.
`.specify/extensions.yml` is absent; no pre/post implementation hooks exist. Changes are uncommitted.
