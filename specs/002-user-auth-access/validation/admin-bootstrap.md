# Initial Admin bootstrap validation — T028–T035

Validated 2026-10-09 (Africa/Cairo). This checkpoint implements the operator CLI increment only. T001–T027 remain complete; T036 and later are not implemented. Full US1 signin/Agent-management acceptance still depends on T058/T085. No real installation Admin was created during verification.

## Behavior and installation workflow

From the repository root, after existing migrations have been applied explicitly or by successful Development HTTP startup:

```powershell
. ./scripts/environment.ps1
dotnet build Crm.slnx --no-restore
dotnet ef database update --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build
dotnet run --no-launch-profile --project src/backend/Crm.Api -- bootstrap-initial-admin
```

The operator enters display name/email and an exact password at the hidden terminal prompt (`Console.ReadKey(intercept:true)`). Prompts use stderr; no password argument/environment/config default exists. Passwords retain spaces/Unicode, use the shared 15–128-scalar rules and are hashed by Identity. Overlong input fails rather than truncating. Automation explicitly adds `--input-stdin` and delivers exactly one {displayName,email,password} JSON object from its protected secret source over private stdin, never a committed file or shell-history literal. Unknown/duplicate/non-string fields fail safely. The native automated tests use generated synthetic private stdin; the first-run physical-terminal prompt was reviewed in code, not manually entered with real operator credentials.

Stdout is only {status,userId}. Exit0: created/already_complete; exit2: invalid/missing input or identity conflict; exit3: dependency/schema/cancellation failure, with no exception/credential disclosure. Cancellation/uncertain commit is never automatically replayed; an operator can rerun to check the durable completion marker. Repeated completed bootstrap requests no credentials, including with redirected interactive stdin or malformed private JSON, and changes no existing account.

Operator authorization is local execution plus inherited Windows SQL authority. CLI uses the same validated ConnectionStrings:Crm/Integrated Security as the application, opens no HTTP listener, requires no signing key, issues no token/cookie, generates no keys and does not migrate/create schema. There is no public Admin endpoint or production seed/default password. Category provisioning is retained unchanged by this checkpoint.

## Atomicity and idempotence

Application owns input invariants/fixed Admin role through a MediatR command and an installation transaction port. Infrastructure begins SERIALIZABLE and reads singleton Id1 with UPDLOCK/HOLDLOCK before invoking input. The absent-key range lock serializes competing installations. Identity CreateAsync and completion SaveChanges share one scoped CrmDbContext/transaction. An active Admin is committed with normalized unique identity, framework hash/stamps, authentication version and rowversion; no AgentReference or session is created. Existing Customer/Agent/Admin email conflicts fail without elevation, reset or overwrite.

SQL verification ran three concurrent service calls and three concurrent native processes. Both produced exactly one created result, matching user IDs for all already_complete results, one Admin and one marker. Repeated runs preserved the serialized account, including password hash, role, stamps and rowversion. Injected storage failure and cancellation after Identity's user save rolled back both records; a clean retry then succeeded. Actual cancellation during credential acquisition also left both tables empty. No retry strategy/uncertain-commit replay was added.

No schema/model/migration change was necessary: the existing InitialAdminSetup/User schema is used. All four existing migration IDs remained applied in current-schema fixtures. A fresh-schema Development CLI attempt returned exit3 and left the migration table/User schema absent, rather than running the HTTP migration hook.

## Test-first and correction evidence

- T028–T030 tests were authored before implementation. Initial missing command/port compile errors were resolved with compile-only declarations and a throwing handler; these compile failures are not counted as behavior-test evidence.
- Application red run: 9 failed, 0 passed (NotImplementedException).
- SQL/native red run: 20 failed, 0 passed, with absent bootstrap handling/composition.
- Application green: 9 passed. Original SQL green: 11 passed; original native green: 9 passed. Additional actual-token cancellation, native identity conflict and documented dotnet-run tests expanded coverage afterwards without claiming a prior red execution for each added case.
- New tests required the repository's xUnit Assert.Single predicate overload; the assertions were preserved.
- The documented Development dotnet-run test initially failed (exit3): full Application DI registration tried to validate ticket handlers without an HTTP actor. Corrected the installation composition to register only BootstrapInitialAdminHandler with MediatR's type filter. Development DI validation stays enabled; HTTP composition and existing ticket code are unchanged. The same assertion then passed.

## Final commands and results

Real SQL uses new exclusively owned CrmTicketsIntegration_GUID catalogs, the existing guarded fixtures, Integrated Security and inherited Windows identity. Native children receive only connection/environment/invocation in arguments; credential JSON is delivered on their private stdin and never printed/persisted. The script creates no credential files. Normal tests exercise the real compiled API executable, including two exact documented dotnet-run invocations in Development with a deliberately unavailable signer.

| Command | Final result |
|---|---|
| dotnet build Crm.slnx --no-restore --verbosity quiet | PASS, 0 warnings/errors |
| dotnet test tests/backend/Crm.Domain.Tests --no-build | PASS, 43/43 |
| dotnet test tests/backend/Crm.Application.Tests --no-build | PASS, 94/94 (9 bootstrap) |
| dotnet test tests/backend/Crm.ArchitectureTests --no-build | PASS, 7/7 |
| dotnet test tests/backend/Crm.IntegrationTests --no-build --filter 'FullyQualifiedName~AdminBootstrapTests' | PASS, 12/12 |
| pwsh -NoProfile -File tests/scripts/bootstrap-admin.tests.ps1 | PASS, 11/11 native process tests |
| dotnet test tests/backend/Crm.IntegrationTests --no-build --filter 'FullyQualifiedName~Crm.IntegrationTests.Identity\|FullyQualifiedName~LocalJwtTests\|FullyQualifiedName~LocalAuthorizationFoundationTests\|FullyQualifiedName~AuthConfidentialityTests' | PASS, 72/72; 49 existing foundation +23 bootstrap |
| dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build | PASS, no pending model changes |

No skips, weakened assertions, timeout increases or auth bypass were introduced. Password boundaries tested at14/15/128/129 scalars with all spaces and supplementary Unicode; successful passwords verified through the standard UserManager hash path. Native tests also cover absent input/schema, strict JSON, forbidden password arguments, safe conflict exits, repeated setup without prompting, and credential-free result/error streams.

Known historical external-OIDC/Ticket regression failures were not repaired or rerun as a full acceptance suite; they remain explicitly deferred to their planned integration work. No frontend change required an Angular/Playwright rerun. No Feature001 usability evaluation was attempted. The 27 recorded Feature001 artifact hashes remain unchanged. No spec/plan/constitution/architecture decision was changed. Extension hooks are absent. No blocker before T036; actual signin/UI/Agent management remains later work.

Final cleanup verification found zero CrmTicketsIntegration_* databases and zero owned foundation-test certificates. Native process handles are disposed and timed-out process trees are terminated by their fixture. Existing operator signing/HTTPS certificates and the developer catalog were not modified. Local red-run output remains ignored under .local; no secret/generated runtime artifact is included in the changes.

## Files changed

Source:

- src/backend/Crm.Application/Accounts/Commands/BootstrapInitialAdminCommand.cs
- src/backend/Crm.Application/Accounts/Ports/IInitialAdminBootstrapStore.cs
- src/backend/Crm.Infrastructure/Identity/InitialAdminBootstrapper.cs
- src/backend/Crm.Infrastructure/Identity/InitialAdminSetup.cs (completion-marker comment)
- src/backend/Crm.Infrastructure/DependencyInjection.cs
- src/backend/Crm.Api/Installation/AdminBootstrapInput.cs
- src/backend/Crm.Api/Program.cs

Tests and reproducible driver:

- tests/backend/Crm.Application.Tests/Accounts/BootstrapAdminTests.cs
- tests/backend/Crm.IntegrationTests/Identity/AdminBootstrapTests.cs
- tests/backend/Crm.IntegrationTests/Identity/AdminBootstrapCliTests.cs
- tests/backend/Crm.IntegrationTests/Fixtures/BootstrapCliFixture.cs
- tests/scripts/bootstrap-admin.tests.ps1

Documentation/status: this evidence, quickstart.md's bootstrap checkpoint note, and tasks.md checkboxes T028–T035 only. Existing historical evidence is not rewritten.
