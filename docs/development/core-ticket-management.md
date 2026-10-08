# Core Ticket Management development and validation

Validated implementation guide, 2026-10-09. Run commands from the repository root in
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
trust it for manual browser usage with `dotnet dev-certs https --trust`. Browser automation
permits the local self-signed API/Angular certificates only in its test configuration.
Production OIDC metadata and HTTP clients use ordinary TLS certificate validation.

## One database connection and Windows identity

API, EF tools and provisioning CLI use this same key and inherited Windows account:

```powershell
$env:ConnectionStrings__Crm = 'Server=localhost;Database=CustomerSupportCrm;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet ef database update --no-build --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api
dotnet ef migrations has-pending-model-changes --no-build --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api
```

This explicitly applies migrations to the chosen development catalog; HTTP startup does not
migrate. Adapt the local instance/catalog before executing. Never supply User ID/Password,
alternate migration/provisioning connection keys, DB access tokens or impersonation.
TrustServerCertificate is the approved local self-signed SQL exception; use false when a
trusted SQL certificate is installed. It does not disable HTTP/OIDC certificate validation.

Database least-privilege separation is intentionally absent. The same Windows identity may
have schema/DML authority while running the API. Supported application operations keep history
immutable, provision references by bounded upserts and expose no delete/admin routes; this
does not protect against privileged direct SQL or bypassed application code. JWT staff actors
and their queue permissions are independent of the process's Windows database identity.
WindowsConnectionTests verifies the shared identity/configuration through actual operations.

## Reference provisioning

Use only approved category/agent input matching schema version 1 in the linked contract.
Keep real input outside version control, for example an ignored .local path. Supply existing
external issuer/subject identities; no passwords, permissions or account creation fields.

```powershell
$env:CRM_REFERENCE_INPUT = Join-Path $pwd '.local/provisioning.local.json'
# Create this operator-approved file using the contract, then:
dotnet run --no-build --no-launch-profile --project src/backend/Crm.Api -- provision-ticket-references --input $env:CRM_REFERENCE_INPUT
```

CLI exits before HTTP startup. Exit 0 reports counts only; 2 is invalid invocation/input;
3 is a database/configuration failure. Repeated identical input is a no-op; omitted records
remain. Agent identity is immutable. Protected Uncategorised cannot be changed/deactivated
and has no supported deletion path. References do not rewrite tickets or history. Runtime
HTTP has no reference provisioning, category/user administration or history edit/delete route.

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
the available fallback. That readiness endpoint works from the MVP onward. Ephemeral RS256
tokens exercise real issuer/audience/signature/expiry validation in exact Testing environment.
The production Angular build excludes the test adapter. Tests use one worker, zero retries
and no saved auth state, traces, screenshots or videos.

Normal fixture disposal/runner finally removes owned catalogs and private temporary inputs.
After a hard process interruption, inspect remaining resources first. An E2E catalog can be
cleaned with `scripts/e2e-database.ps1 -Mode Cleanup -Owner <original-owner-token>` only with
its original CRM_E2E_SQL_CONNECTION and matching owner; mismatches retain the catalog. Never
derive ownership from a prefix or drop all matching databases. Integration ownership is held
by its fixture; absent that evidence, retain the catalog for operator inspection. Stop only
known test processes if ports remain occupied. Do not delete source data or the development DB.

## Manual external OIDC usage (deployment prerequisite)

No actual issuer/client/accounts have been supplied for this training project. These steps
require an external OIDC provider and are not claimed as tested by signed test JWTs.

1. Register a public SPA authorization-code/PKCE client, HTTPS redirect
   `https://localhost:4200/auth/callback`, permitted origin and appropriate API access scopes.
   Configure the API audience and HTTPS authority. Authorized staff must receive stable
   issuer/subject and `crm_support_queue=shared` in API access tokens; the SPA profile must
   also include that claim for its convenience guard. Assignment never grants authorization.
2. Set Authentication__Authority and Authentication__Audience for the API. Remove every
   Authentication__Test* variable; normal hosts reject them. Set DOTNET_ENVIRONMENT and
   ASPNETCORE_ENVIRONMENT to Development for local usage, or Production for deployment.
3. Set public authority/clientId/scope in `src/frontend/src/environments/environment.ts`,
   keeping redirectPath `/auth/callback` and apiBaseUrl `/api`. Use only values from the
   registered provider. No SPA secret or SQL string belongs in this file.
4. Start the API with explicit HTTPS rather than the generated launch profile:

   ```powershell
   $env:ASPNETCORE_URLS = 'https://localhost:7043'
   dotnet run --no-build --no-launch-profile --project src/backend/Crm.Api
   ```

5. Serve the normal frontend on HTTPS 4200 with a same-origin `/api` proxy to HTTPS 7043.
   For local Angular development create ignored `.local/proxy.manual.json` containing
   `{ "/api": { "target": "https://localhost:7043", "secure": true } }`, then in another
   configured repository-root shell run:

   ```powershell
   npm run start --prefix src/frontend -- --port 4200 --ssl --proxy-config ../../.local/proxy.manual.json
   ```

   Trust the local certificates for browser and Node proxy verification (use NODE_EXTRA_CA_CERTS
   with the exported public development certificate if needed). Do not use proxy.e2e.json,
   the e2e adapter or a production certificate-validation bypass for manual OIDC usage.
   Deployment serves the production bundle and trusted HTTPS certificates at its registered
   origin; adjust public redirects accordingly. Cross-origin hosting requires the approved
   API origin and Cors:Origins; tokens are sent only to that configured API origin/path.
6. Verify actual sign-in/callback, an authorized categories read and a ticket journey with
   two authorized accounts, and denied/missing access. Verify issuer/audience claims without
   copying tokens or personal identity data into evidence. Sign-out/session expiry and
   production IdP interoperability are provider deployment checks, not simulated acceptance.

Follow [the canonical SC-005 protocol](../../specs/001-core-ticket-management/quickstart.md#repeatable-sc-005-evaluation-protocol)
only after deployment readiness and automated gates. Preparation and pending status are in
[staff-evaluation.md](../../specs/001-core-ticket-management/validation/staff-evaluation.md).
No real participants are currently available; full acceptance remains pending.
