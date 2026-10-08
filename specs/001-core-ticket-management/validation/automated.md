# Final automated validation — T073–T077

Validated 2026-10-09 (Africa/Cairo), resuming committed T001–T072 at `5e4bd83`.
Initial git status/diff were clean. All 72 completed tasks had source and prior checkpoint
evidence; T073–T081 were unchecked and had no partial implementation/evidence. No completed
story was reimplemented. The requirements checklist had 16 checked / 0 unchecked items.
Spec, plan, research, data model, contracts, quickstart and constitution are unchanged.

## Environment and results

Windows PowerShell 7; .NET SDK 10.0.400/runtime 10.0.11; EF tooling 10.0.12;
SQL Server 17.0.1000.7 on localhost; Node 24.20.0/npm 11.19.0; Angular 22.2.1,
CLI/build 22.2.2; TypeScript 6.0.3; Vitest 5; Playwright 1.64.0/Chromium.
SQL Server 2022 is the design baseline; this installed later local version successfully
exercised the committed schema, collation, rowversion and transaction behavior.

| Executed check | Result |
| --- | --- |
| `dotnet tool restore` | Passed, pinned dotnet-ef 10.0.12 |
| `dotnet restore Crm.slnx --locked-mode` | Passed, all eight projects; no dependency changes |
| `dotnet build Crm.slnx --no-restore` | Passed, zero warnings/errors |
| `dotnet test Crm.slnx --no-build --logger 'console;verbosity=minimal'` | 278 passed: Domain 33, Application 78, Architecture 6, SQL/API 161; zero failures/skips |
| Focused architecture suite | 6 passed |
| Focused security suite before final route-data-source assertion | 11 passed; the final full SQL/API run also passes the expanded inventory check |
| `npm ci --prefix src/frontend` | Passed; 291 installed / 292 audited; zero vulnerabilities; lockfile unchanged |
| `npx playwright install chromium` from src/frontend | Passed |
| `npm test --prefix src/frontend -- --watch=false` | 68 passed in 11 files |
| `npm run build --prefix src/frontend` | Passed, production bundle; initial 341.54 kB, within existing budgets |
| Final `npm run e2e --prefix src/frontend` after clean install | 18 passed, 48.2 seconds, zero retries; all earlier stories plus three platform cases |
| `dotnet dev-certs https --check` in the Windows harness environment | Existing certificate valid; no certificate was created or TLS policy relaxed |
| `dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build` | No changes since last migration |
| Migration application | Existing `20261008164804_InitialTicketStorage` applied to each owned fresh integration/E2E database; no new migration or development-catalog changes |
| Production bundle scan | No `__CRM_TEST_AUTH`, `crm-tickets-e2e` or `TestPublicKeyPath` markers in dist/crm/browser |
| Reviewed source/evidence secret scan | No literal private signing-key blocks or JWTs; configuration review found no database credentials |
| `git diff --check` | Passed; only Git's normal LF-to-CRLF notices |
| Final read-only cleanup probes | Zero disposable integration/E2E catalogs, .local/e2e-* directories and listeners on 4200/7043 |

Restore reported npm installation-script policy warnings for existing dependencies, and browser
processes reported NO_COLOR/FORCE_COLOR warnings. Commands and required tests passed without
changing these policies, warning/error settings, test assertions, timeouts or retry counts.
A certificate probe from the restricted process could not see the certificate store; the same
check in the actual Windows harness environment confirmed the existing valid certificate.

## T073 architecture evidence

[DependencyBoundaryTests](../../../tests/backend/Crm.ArchitectureTests/DependencyBoundaryTests.cs)
checks both the direct project graph and compiled references. Domain has no layer/EF/ASP.NET/HTTP
dependency; Application has no Infrastructure/API/EF/HTTP dependency; Infrastructure has no API
dependency. All controllers accept ISender and dispatch through MediatR. IL inspection includes
async state machines and rejects Domain, Infrastructure, EF, SqlClient and System.Data calls.
A negative detector test proves a domain-member access is found. Source review confirms controllers
perform binding, dispatch and response mapping; application/domain own business rules.

These checks cover structural access, not every possible business rule written using primitive
expressions inside a controller; that semantic boundary still needs code review. No architecture
exception, generic repository framework, event sourcing or new dependency was introduced.

## T074 security evidence

[AuthorizationBoundaryTests](../../../tests/backend/Crm.IntegrationTests/Security/AuthorizationBoundaryTests.cs)
asserts the exact nine method/route pairs against both MVC actions and routing endpoint metadata.
Every action requires SupportQueue and lacks AllowAnonymous. Every route rejects missing, expired,
wrong-issuer and wrong-audience tokens with 401, and valid denied/blank-subject identities with 403,
before payload/existence decisions. An untrusted signature also rejects; authorized staff A/B can
read and mutate the same unassigned ticket. Earlier assignment suites retain the rule that being
an assignee grants no access. No delete/admin/escalation/communication routes exist.

Production rejects Test-prefixed authentication configuration, and missing/insecure authority or
missing audience fails closed when authentication options are resolved. Production options require
HTTPS metadata, signature/issuer/audience/lifetime/expiration validation, suppress diagnostic detail
and install no certificate-bypassing backchannel handler. Injected private exception text and raw
unknown-field content are absent from sanitized errors; no-store remains applied. Source/configuration
review found no sensitive EF logging, PII diagnostic opt-in or production HTTP TLS bypass. The approved
local SQL TrustServerCertificate setting is distinct and remains an explicit local SQL trade-off.

## T075 platform evidence and correction

[platform.spec.ts](../../../src/frontend/e2e/tickets/platform.spec.ts) creates and retrieves mixed
Arabic/English/plain-text markup using only keyboard navigation and native inputs, checks visible
focus, verifies native edit/assignment/status controls are focusable, and reads creation history.
Create/detail/list pages are checked at 320/375/768px in LTR and RTL with an unusually long subject.
The page must not overflow horizontally; the queue table may scroll inside its existing container.
Changing the existing --brand-color variable updates the actual header. Logical layout CSS and
dir=auto text remain in use; no translation, organization or branding administration was added.

Tests were written and run before the production correction. The initial browser run passed 17
and failed 1: at 320px RTL the long detail subject produced 5726px document overflow. The detail
heading lacked wrapping. Extending the existing h1 wrapping rule to h2 fixed it; the unchanged
assertion then passed in two complete 18-test runs, including the final clean-install run.

The initial architecture build was stopped by xUnit2029 because a collection assertion used
Assert.Empty over a filter. The equivalent Assert.DoesNotContain predicate satisfies the enabled
analyzer; no analyzer or warning gate was disabled. All final builds pass.

## T076/T077 reproduction and limits

The runnable [development guide](../../../docs/development/core-ticket-management.md) documents
restore/build/test commands, one shared Windows connection, migrations, bounded CLI input, HTTPS
ports/readiness, fixture ownership/cleanup and actual external OIDC prerequisites. The automated
quickstart restore/build/unit/SQL/browser/migration scenarios were exercised using fresh owned
catalogs. All API/EF/CLI/fixture children inherit one Windows identity/connection per run; only the
test catalog changes. The development database was not modified. Synthetic references and real
signed Testing-only JWTs are used; external sign-in is not simulated as production evidence.

Candidate output SHA-256 fingerprints (ignored generated binaries, not committed artifacts):

- Crm.Api.dll: `03077C68388FB17EB8BC46002CC2F92667643DF72B5D6C60D5F7684D585C5831`
- Production main-UBEILS5H.js: `04861AF3B098D5D11EE3E6ABDDE53373925DD7424168584D30A4AA2E0FFD4AC5`

No real external OIDC issuer/client/accounts were supplied; manual provider interoperability remains
unverified. SC-005 requires real eligible staff and is pending by user instruction. Automated
validation is not full feature acceptance. Secret scanning is a heuristic supplemented by diff/config
review, not a proof about arbitrary untracked external files. `.specify/extensions.yml` is absent;
no pre/post hooks exist. See [staff preparation](staff-evaluation.md), [benchmark disposition](advisory-benchmark.md)
and [provisional completion audit](completion.md) for remaining acceptance dependencies.
