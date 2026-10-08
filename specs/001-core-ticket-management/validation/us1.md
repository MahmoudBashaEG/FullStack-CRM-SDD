# US1 implementation evidence — T001–T031

Date: 2026-10-08. This is the create/view MVP checkpoint, not full Core Ticket Management acceptance. T032–T081 remain outside the authorized run.

## Delivered

- Four .NET projects with inward dependencies, CQRS/MediatR and feature-specific persistence ports.
- Application-owned creation validation/defaults, trusted actor/time, immutable Domain creation history and SQL Server atomic persistence.
- Protected POST ticket, GET detail/history and selectable category/agent endpoints; strict JSON, consistent errors, no HTTP validators, no delete/admin operations.
- Installation-only strict version-1 reference upserts with fallback protection, immutable agent identities and transactional rollback.
- Angular standalone create/detail/history pages inside the tickets feature; production OIDC/PKCE, origin-restricted bearer transport and guarded navigation.
- Test-only signed JWT fixtures and reproducible Playwright-owned HTTPS servers. Readiness always uses `/api/ticket-reference-data/categories`: 401 for HTTP, authorized 200/fallback for SQL readiness.

## Executed validation

| Command/check | Result |
| --- | --- |
| `dotnet tool restore` and `dotnet restore Crm.slnx` | Passed; exact tool/package lockfiles |
| `dotnet build Crm.slnx --no-restore` | Passed, zero warnings/errors |
| `dotnet test Crm.slnx --no-build --no-restore` | Domain 6, Application 17, real SQL/API 23 passed; zero failed/skipped in these suites |
| Architecture test project | Scaffold only; no tests discovered; T073 not executed |
| `npm install --prefix src/frontend` | Passed without force/legacy peer dependency bypass |
| `npm run build --prefix src/frontend` | Production build passed |
| `npm test --prefix src/frontend -- --watch=false` | 16 passed |
| `playwright install chromium` | Installed; transient download resets recovered |
| `npm run e2e --prefix src/frontend` | 3 passed from fresh servers/SQL fixtures; repeated after corrections |
| NuGet transitive vulnerability audit and full npm audit | Zero known vulnerabilities reported |
| Layer/project-reference and controller dependency inspection | Domain has no project references; Application -> Domain; Infrastructure -> Application/Domain; API -> Application/Infrastructure. No EF/HTTP/outer-layer source dependencies in Domain/Application; no persistence access in controllers |

The reviewed migration `20261008164804_InitialTicketStorage` creates the four documented dbo tables, rowversion, history/reference constraints and protected fallback. It was applied to fresh owned catalogs using Integrated Security; a second update reported no pending migrations. Integration and browser fixtures applied the same migration independently. No production/development catalog was reset or migrated by this run.

Tests prove all four priorities, stable unique references, Arabic/plain-text preservation, defaults, shared access, denied/invalid JWTs, strict malformed/unknown/duplicate input rejection, null optional-input rejection, initial readable snapshots, inactive current labels with unchanged version/history, fallback protection, upsert/no-op/omission behavior, identity rebinding rollback, and forced persistence rollback without orphan ticket/history rows.

## Test-first evidence and corrections

- Domain creation tests: 6 expected failures against a compiling creation stub before implementation, then green.
- Application use-case tests: 11 expected failures against compiling handler stubs before implementation, then green. All stubs were replaced.
- API contract tests ran against real SQL before US1 controllers: missing routes produced expected failures. Fixture/configuration problems were corrected separately; no passing skips were introduced.
- Corrected unpublished Angular framework pin, computed-column filtered-index incompatibility, migration assembly rebuild ordering, PowerShell connection-builder parsing, empty SQL credential-key rejection, late test-host configuration resolution and Windows account alias comparisons.
- Corrected frontend test fixture setup, CLI configuration error containment, stale history-response handling and application ownership of category availability decisions. Unsupported DELETE uses HTTP 405 on the existing read route, rather than an assumed 404; deletion is unavailable.

## Configuration, assumptions and limitations

- Actual tools: SDK 10.0.400/runtime 10.0.11; EF/ASP.NET packages 10.0.12, MediatR 12.5.0; Node 24.20.0/npm 11.19.0; Angular framework 22.2.1/CLI-build 22.2.2, TypeScript 6.0.3.
- The supplied local default instance (`localhost`) is reachable with the existing Windows account and reports SQL Server 17.0.1000.7, newer than the planning baseline. No SQL principals, grants, passwords or identity switches were created.
- API, design-time factory and CLI consume one `ConnectionStrings:Crm`. Test runs override only the catalog to a newly named owned fixture; normalized application-name metadata is `Crm`. OS names and SQL original/current-login names may use linked-account aliases; comparisons preserve each representation across hosts rather than assuming identical display strings.
- Agent identity uniqueness uses an internal persisted SHA-256 key of length-prefixed Unicode issuer/subject. This avoids SQL string-index length limits and trailing-space equality without adding business field limits. Identity comparisons remain ordinal; this storage detail is absent from REST/UI.
- Windows-authenticated database access is intentionally shared. History immutability and supported upsert/delete restrictions are application guarantees; separate database privilege isolation is not claimed.
- Disposable catalogs require an explicit integrated-security test connection and a fresh name. Fixtures reject existing catalogs; cleanup checks actual local server, catalog and ownership marker. Browser token files use restricted temporary-directory ACLs; private signing keys remain in memory. Fixtures/servers were cleaned up after successful runs.
- Production/manual OIDC remains external and requires a real authority/audience/SPA registration and authorized identities. The automated harness validates genuine signatures/policy with Testing-only keys; it does not claim a real external-issuer login was tested.
- No list/edit/assignment/status mutations, other CRM modules, SC-005 evaluation or advisory benchmarks were implemented/executed. Full feature acceptance remains pending later tasks.

## Reproduce this checkpoint

From repository root, load `scripts/environment.ps1`. Supply fresh local test catalog names (do not reuse an existing database):

```powershell
. ./scripts/environment.ps1
$testCatalog = 'CrmTicketsIntegration_' + [guid]::NewGuid().ToString('N')
$env:CRM_TEST_SQL_CONNECTION = "Server=localhost;Database=$testCatalog;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
dotnet tool restore
dotnet restore Crm.slnx
dotnet build Crm.slnx --no-restore
dotnet test Crm.slnx --no-build --no-restore
$browserCatalog = 'CrmTicketsE2E_' + [guid]::NewGuid().ToString('N')
$env:CRM_E2E_SQL_CONNECTION = "Server=localhost;Database=$browserCatalog;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
npm ci --prefix src/frontend
npm run build --prefix src/frontend
npm test --prefix src/frontend -- --watch=false
npm exec --prefix src/frontend -- playwright install chromium
npm run e2e --prefix src/frontend
```

Use the existing Windows identity for every command. Sandbox execution may require escalation for Windows SQL authentication, HTTPS package restoration and build subprocesses. Do not substitute SQL credentials or disable test failures.

For manual use, first apply the migration explicitly to the selected development catalog, provision approved references through the documented CLI, and supply external authentication settings. Configure the SPA API URL and API CORS origins to agree with the selected HTTPS hosts; the default relative `/api` assumes same-origin hosting. No issuer or production staff identity was invented.

The [file inventory](files.md) enumerates implementation artifacts. Specification, constitution and planning designs were not edited. `.specify/extensions.yml` is absent, so no before/after implementation hooks were registered.
