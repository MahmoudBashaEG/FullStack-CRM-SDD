# Core Ticket Management development and validation

Validated implementation guide, 2026-10-10. Run commands from the repository root in
PowerShell 7. The authoritative behavior is in [the specification](../../specs/001-core-ticket-management/spec.md);
see [REST](../../specs/001-core-ticket-management/contracts/tickets-rest.md),
[reference provisioning](../../specs/001-core-ticket-management/contracts/reference-provisioning.md)
and [the shared database connection](../../specs/001-core-ticket-management/contracts/database-principals.md).

## Prerequisites and restore

Use the pinned .NET SDK in global.json, Node 24.20.0/npm 11.19.0, local Windows SQL Server,
sqlcmd and Chromium. The existing Windows account must already have access to the local
instance and be able to create the disposable test databases. No login/grant creation script
is supplied. SQL Server 17 was used for validation; the planning baseline is SQL Server 2022.

```powershell
. ./scripts/environment.ps1
dotnet tool restore
dotnet restore Crm.slnx --locked-mode
dotnet build Crm.slnx --no-restore
dotnet dev-certs https --check
npm ci --prefix src/frontend
Push-Location src/frontend
npx playwright install chromium
Pop-Location
```

If a development HTTPS certificate is missing, create it with `dotnet dev-certs https`;
trust it for manual browser usage with `dotnet dev-certs https --trust`.
Browser HTTPS, API HTTPS and the development proxy use ordinary TLS certificate validation.

## One database connection and Windows identity

API, EF tools and provisioning CLI use this same key and inherited Windows account:

```powershell
$env:ConnectionStrings__Crm = 'Server=localhost;Database=CustomerSupportCrm;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet ef database update --no-build --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api
dotnet ef migrations has-pending-model-changes --no-build --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api
```

The update command explicitly applies existing migrations to the chosen catalog. It is
optional before normal Development API startup, but required if you want to provision
references before that first startup. Production, Staging, Testing and other non-Development
hosts require explicit migration preparation; they never migrate during normal API startup.
Testing fixtures and the browser runner keep their explicit, ownership-checked preparation.

## One-command local startup

Configure the shared local authentication prerequisites below and remove all Authentication__Test* settings.
One-time HTTPS setup: reuse the existing trusted API development certificate for Angular,
so both browser HTTPS and the secure:true proxy validate it. Exporting a certificate's private
key is sensitive; perform this deliberate setup only on your own development machine. The
launcher never exports/generates certificates or changes machine trust.

```powershell
. ./scripts/environment.ps1
$devCertDirectory = Join-Path $pwd '.local/dev-certs'
New-Item -ItemType Directory -Path $devCertDirectory -Force | Out-Null
$devCertAccount = [Security.Principal.WindowsIdentity]::GetCurrent().Name
icacls $devCertDirectory /inheritance:r /grant:r "${devCertAccount}:(OI)(CI)F"
dotnet dev-certs https --check --trust
# If not valid/trusted, deliberately complete dotnet dev-certs https --trust first.
dotnet dev-certs https --export-path (Join-Path $devCertDirectory 'localhost.pem') --format PEM --no-password
```

Keep localhost.key private and ignored; do not copy it into source, logs or the frontend
bundle. Re-export deliberately when the certificate changes. You may instead provide existing
PEM paths through CRM_DEV_SSL_CERT and CRM_DEV_SSL_KEY. The default paths are the two files
under .local/dev-certs; NODE_EXTRA_CA_CERTS defaults to the public PEM in the children and is
restored on exit. Set it explicitly to the API's public CA if using a different frontend cert.

After that setup, the exact PowerShell 7 command from the repository root is:

```powershell
./scripts/run-dev.ps1
```

The launcher reuses environment.ps1 and starts dotnet watch plus the normal Angular development
server concurrently in the same terminal. Backend https://localhost:7043, frontend
https://localhost:4200. Angular uses relative /api and proxy.development.json (secure:true).
Child-only Development variables and no launch profile fix the API URL; telemetry consent
is disabled for the Angular child so first startup does not wait for a prompt. Logs stay live.
Both HTTP endpoints must become available in 120 seconds. A categories 401 and frontend HTML
200 prove availability; sign-in and authorized categories 200/fallback prove actual access.

Ctrl+C requests shutdown. Both services share a five-second grace period; remaining owned
trees are force-cleaned and the outcomes are reported distinctly. Windows job ownership also
cleans descendants when a wrapper exits first. The launcher never kills by name/port, reuses
occupied ports or removes databases. A forced terminal close is outside the graceful guarantee.
In a non-console automation host, graceful console signaling may be unavailable; the same
five-second owned-tree fallback still applies. Restart after cleanup uses the same two ports.

The API awaits existing pending compiled EF migrations before listening. A fresh or older
schema receives only those migrations; an already-current schema skips migration execution.
Migration failure, cancellation, missing artifacts or model/snapshot drift stops startup.
The protected fallback is migration-seeded; startup does not provision agents/categories.
The CLI exits before this HTTP hook and still requires an explicitly prepared schema.
Applying migrations requires deployed compiled artifacts, not a runtime Git checkout.

After an intentional model change, generate a migration explicitly:

```powershell
$migrationName = 'DescribeYourModelChange' # Replace with the reviewed change's name.
dotnet ef migrations add $migrationName --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --output-dir Persistence/Migrations
dotnet build Crm.slnx
dotnet ef migrations has-pending-model-changes --no-build --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api
```

Choose an appropriate migration name. Review the generated migration,
designer and snapshot, verify it against owned disposable catalogs, and version-control
all artifacts. Rebuild before using --no-build. Startup never generates migrations,
infers a schema with EnsureCreated, rewrites source or suppresses pending-model checks.
For non-Development deployment apply reviewed migrations explicitly before starting the API
using the same configured connection and Windows identity.
Adapt the local instance/catalog before executing. Never supply User ID/Password,
alternate migration/provisioning connection keys, DB access tokens or impersonation.
TrustServerCertificate is the approved local self-signed SQL exception; use false when a
trusted SQL certificate is installed. It does not disable HTTP certificate validation.

Database least-privilege separation is intentionally absent. The same Windows identity may
have schema/DML authority while running the API. Supported application operations keep history
immutable, provision references by bounded upserts and expose no delete/admin routes; this
does not protect against privileged direct SQL or bypassed application code. JWT staff actors
and their queue permissions are independent of the process's Windows database identity.
WindowsConnectionTests verifies the shared identity/configuration through actual operations.

## Reference provisioning

Use only approved category input matching schema version 1. Nonempty agents input is rejected.
Keep real input outside version control, for example an ignored .local path. Supply existing
category IDs and availability; no passwords, permissions or account creation fields.

```powershell
$env:CRM_REFERENCE_INPUT = Join-Path $pwd '.local/provisioning.local.json'
# Create this operator-approved file using the contract, then:
dotnet run --no-build --no-launch-profile --project src/backend/Crm.Api -- provision-ticket-references --input $env:CRM_REFERENCE_INPUT
```

CLI exits before HTTP startup. Exit 0 reports counts only; 2 is invalid invocation/input;
3 is a database/configuration failure. Repeated identical input is a no-op; omitted records
remain. Agent identity is immutable. Protected Uncategorised cannot be changed/deactivated
and has no supported deletion path. References do not rewrite tickets or history. Runtime
HTTP has no category provisioning or history edit/delete route. Admin-only Agent management creates and maintains local assignment identities; it cannot modify legacy references.

## Automated checks and safe cleanup

Use fresh disposable names for each run. Both fixture systems reject existing catalogs;
they verify the actual local server, catalog and exclusive ownership before removal. The
development catalog is never a test target. All child processes share the same run connection.

```powershell
$taskCatalog = 'CrmTicketsIntegration_' + [guid]::NewGuid().ToString('N')
$env:CRM_TEST_SQL_CONNECTION = "Server=localhost;Database=$taskCatalog;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
dotnet test Crm.slnx --no-build --logger 'console;verbosity=minimal'
npm test --prefix src/frontend -- --watch=false
npm run build --prefix src/frontend
$taskCatalog = 'CrmTicketsE2E_' + [guid]::NewGuid().ToString('N')
$env:CRM_E2E_SQL_CONNECTION = "Server=localhost;Database=$taskCatalog;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
npm run e2e --prefix src/frontend
```

The browser runner owns API HTTPS 7043 and Angular HTTPS 4200; occupied ports fail rather
than reusing hosts. It migrates, provisions synthetic data, starts both servers and checks
`/api/ticket-reference-data/categories`: unauthenticated 401, then signed authorized 200 with
the available fallback. That readiness endpoint works from the MVP onward. Actual local
signin and isolated dedicated signing configuration exercise normal JWT/session validation.
Browser tests use actual signin and HttpOnly cookie restoration, with no injected Angular JWT.
They use one worker, zero retries, private owned temporary cookie state and no traces/screenshots/videos.

Normal fixture disposal/runner finally removes owned catalogs and private temporary inputs.
After a hard process interruption, inspect remaining resources first. An E2E catalog can be
cleaned with `scripts/e2e-database.ps1 -Mode Cleanup -Owner <original-owner-token>` only with
its original CRM_E2E_SQL_CONNECTION and matching owner; mismatches retain the catalog. Never
derive ownership from a prefix or drop all matching databases. Integration ownership is held
by its fixture; absent that evidence, retain the catalog for operator inspection. Stop only
known test processes if ports remain occupied. Do not delete source data or the development DB.

## Shared local CRM authentication

Use the dedicated protected RS256 signing-certificate setup in [Feature 002 quickstart](../../specs/002-user-auth-access/quickstart.md), distinct from the HTTPS certificate. Configuration contains Jwt issuer/audience/thumbprint/key-ID references and the trusted frontend Origin, never a private key or password. Windows Data Protection keys remain durable and protected for normal restarts. Remove obsolete external authority/client/discovery settings; the application uses one shared local account/session infrastructure.

After schema preparation, an installation operator establishes the initial Admin with hidden input:

```powershell
dotnet run --no-build --no-launch-profile --project src/backend/Crm.Api -- bootstrap-initial-admin
./scripts/run-dev.ps1
```

Bootstrap is separate from startup. It accepts no default password, password argument or config password; repeated completed setup changes nothing. Automation must explicitly use --input-stdin and an operator-controlled private input pipe. Passwords follow the exact 15–128 Unicode-scalar policy, including spaces.

The frontend runs at https://localhost:4200 and API at https://localhost:7043 with the shared relative /api development proxy. Sign in at /auth/signin. Admins manage Agents at /agents; Agents/Admins share the whole Ticket queue regardless of assignment. Public /auth/signup creates only Customers and requires subsequent explicit signin. Customers and visitors cannot access internal Tickets; only Admins manage Agents. No Customer Portal or external redirect/callback is present.

Angular holds the five-minute access JWT only in memory. Secure HttpOnly refresh cookies rotate through the same-origin HTTPS transport and restore authentication on reload. Server-side account/session checks apply to every protected request. Disablement invalidates existing Agent sessions immediately; re-enable requires fresh signin. One active browser tab per session is supported; concurrent same-instance requests share one refresh.

Active locally managed Agents are assignment choices. Admin/Customer roles alone are not assignable. Saved legacy/inactive references remain readable, retainable and clearable, while new selection requires current eligibility. Historical actor snapshots retain their original identifiers/names; future mutations use the trusted local UserId. Ordinary Ticket network/503 errors require explicit recovery/review without automatic mutation replay; invalid sessions and failed refresh/restoration clear shared authentication state.

See [shared architecture](../architecture/authentication-authorization.md), [Ticket integration evidence](../../specs/002-user-auth-access/validation/ticket-integration.md), [final authentication verification](../../specs/002-user-auth-access/validation/final-validation.md) and [local role/restart reproduction](../../specs/002-user-auth-access/validation/local-development-auth.md) for current behavior. Failed refresh invalidates its generation so delayed expiry responses cannot rotate again; ordinary feature failures still require deliberate recovery without replaying uncertain writes. Historical Feature 001 validation documents describe the earlier implementation and remain unchanged.

Follow [the canonical SC-005 protocol](../../specs/001-core-ticket-management/quickstart.md#repeatable-sc-005-evaluation-protocol)
only after deployment readiness and automated gates. Preparation and pending status are in
[staff-evaluation.md](../../specs/001-core-ticket-management/validation/staff-evaluation.md).
No real participants are currently available; full acceptance remains pending.

Startup migration verification is recorded in
[migration-startup.md](../../specs/001-core-ticket-management/validation/migration-startup.md).

Launcher, proxy, watch/reload, shutdown and normal JWT fixture results are recorded in
[local-development.md](../../specs/001-core-ticket-management/validation/local-development.md).
Run `pwsh -NoProfile -File tests/scripts/run-dev.tests.ps1` for native lifecycle cases and
`pwsh -NoProfile -File tests/scripts/run-dev.integration.ps1` (optionally `-InitialSchema`)
for real launcher/owned SQL/proxy/local-auth checks. The latter performs operator-only Admin
bootstrap with private stdin credentials, actual local HTTP signin and rotating-cookie proxy
verification. It creates Agents through real Admin APIs and exercises the complete Ticket
lifecycle. Both fresh-schema and older-schema runs retain migration, watch/reload, secure TLS,
repeat-start and owned-process cleanup checks; no external issuer or token-injection adapter remains.
