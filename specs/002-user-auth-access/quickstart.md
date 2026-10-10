# Quickstart: Local Accounts and Access Control

Implemented and verified 2026-10-10. This guide reproduces Feature002's local account, authentication and Ticket integration behavior. See [local development verification](validation/local-development-auth.md), [final results](validation/final-validation.md) and [requirement coverage](validation/requirements-coverage.md). Feature001 historical OIDC evidence remains historical.

## 1. Safe local prerequisites and signing setup

Use existing PowerShell7/.NET/Node dependencies and local SQL Server. Restore with the existing environment cache script, dotnet restore --locked-mode and npm ci --prefix src/frontend. Keep existing trusted HTTPS development PEM/key prerequisites from docs/development/core-ticket-management.md; do not export or change trust automatically as part of signin/setup. SQL uses existing ConnectionStrings:Crm with Integrated Security; no username/password or three-principal setup.

Signing requires a dedicated protected certificate, distinct from HTTPS. An authorized operator can create a local certificate explicitly in CurrentUser/My (this is a one-time optional setup command, never API startup behavior):

```powershell
cd C:\Users\mbasg\Desktop\CRM
. ./scripts/environment.ps1
$crmSigningCertificate = New-SelfSignedCertificate -Subject 'CN=CRM Local JWT Signing' -CertStoreLocation Cert:\CurrentUser\My -Type Custom -KeyAlgorithm RSA -KeyLength 3072 -KeyExportPolicy NonExportable -KeyUsage DigitalSignature -NotAfter (Get-Date).AddYears(1)
dotnet user-secrets set 'Jwt:SigningCertificateThumbprint' $crmSigningCertificate.Thumbprint --project src/backend/Crm.Api
dotnet user-secrets set 'Jwt:SigningKeyId' $crmSigningCertificate.Thumbprint --project src/backend/Crm.Api
dotnet user-secrets set 'Jwt:CertificateStoreLocation' 'CurrentUser' --project src/backend/Crm.Api
dotnet user-secrets set 'Jwt:Issuer' 'urn:crm:local' --project src/backend/Crm.Api
dotnet user-secrets set 'Jwt:Audience' 'crm-api' --project src/backend/Crm.Api
dotnet user-secrets set 'Auth:AllowedOrigins:0' 'https://localhost:4200' --project src/backend/Crm.Api
```

If a suitable dedicated signing certificate already exists, configure its thumbprint instead of creating another. The private key is not exported or printed. User-secrets hold references/config, not a password/private-key copy; private-key ACL must allow only the intended host/operator. Do not alter machine root trust for this signing certificate. Production supplies protected signing material accessible to its host account and validates HTTPS origin/key rollover separately. Data-protection keys for CSRF must be durable/OS-protected; runtime config validates the signing certificate and origins, failing closed if missing/expired/inaccessible.

Runtime external OIDC is retired. Remove obsolete Authentication:Authority/Audience and frontend environment.oidc settings from your local configuration. Old Authentication__Test* values remain forbidden in normal Development. Current fixtures use isolated local accounts/certificates and real signin, with no external discovery or injected Angular token.

The API persists ASP.NET Data Protection keys at `%LOCALAPPDATA%/ASP.NET/DataProtection-Keys`, uses stable application name `Crm` and encrypts keys with current-user Windows DPAPI. An unavailable Windows profile fails setup. Optional `DataProtection:KeyDirectory` selects an operator-managed durable directory with the same protection; restrict its permissions to the host/operator and keep it out of Git. Normal framework key lifetime/rollover is retained. Keep the repository, Windows identity, signer and SQL catalog consistent across restart; actual retained CSRF/refresh restart checks are linked above. Never print/copy key XML into evidence.

## 2. Apply explicit schema and bootstrap

Migration files are generated/reviewed/versioned during implementation, not in startup. Development API applies existing pending compiled migrations using the existing hook. For explicit initial setup (also the non-Development/CLI path):

```powershell
dotnet build Crm.slnx
dotnet ef database update --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build
dotnet run --no-launch-profile --project src/backend/Crm.Api -- bootstrap-initial-admin
```

Use the installation command's non-echo password prompt; do not supply a password in argv, committed JSON or shell history. Choose an operator-owned email and unique password meeting 15–128 Unicode-scalar policy. No example/default Admin credential is provided. Expected result created; repeating command returns already_complete and changes no credentials/roles. Missing schema/invalid input fails safely. Commands/config details are defined in contracts/bootstrap.md.

The bootstrap CLI increment (T028–T035) is implemented and verified; see [Admin bootstrap evidence](validation/admin-bootstrap.md). Run the command in an interactive terminal for hidden password entry. Automation must explicitly add `--input-stdin` and send the single credential JSON object from an operator-controlled secret source over private stdin; redirected input without that flag is rejected. No password argument or environment/config password is accepted. Exit0 means created/already_complete, exit2 invalid/missing input or identity conflict, exit3 cancellation/database/dependency failure. Stdout contains only status and userId; prompts/errors use stderr. Completed setup is checked before reading either input mode. This command neither starts HTTP nor requires JWT signing configuration nor applies migrations. Admin signin and Agent-management integration are now verified by the final workflow.

For future model changes developers explicitly generate a named migration, inspect its operations and model snapshot, build/test and commit it. Startup never generates one or uses EnsureCreated. Feature002 transition makes legacy AgentReferences ineligible without removing saved assignments/history. Inspect a populated disposable migration fixture before touching a real development catalog; never reset the developer's data for verification.

Before migrating persistent data, take an operator-approved backup and verify restore/upgrade on an owned copy. Rollback means restoring the backup with its matching application version; do not assume dropping new account/session tables through migration downgrade preserves newly created data.

## 3. Start and exercise roles

```powershell
./scripts/run-dev.ps1
```

Frontend https://localhost:4200, API https://localhost:7043; existing /api proxy and visible dotnet-watch/Angular logs remain. Categories401 plus frontend HTML200 remains unauthenticated readiness. Start applies only existing pending Development migrations; it does not create Admin credentials or Agent references. Ctrl+C stops only owned process trees with existing grace/cleanup behavior.

1. Sign in at /auth/signin with the bootstrapped Admin. Inspect current identity; no external redirect/discovery. Admin can open tickets and /agents.
2. Create an Agent using an unused email and Admin-supplied initial password; record its stable UserId/reference ID privately for the test. Update its display name; no email/password/role/delete control is available.
3. Sign out; prior context's access JWT and refresh cannot authorize subsequent protected requests. Sign in as Agent; ticket create/view/list/edit/assign/status/history work across the shared queue and Agent administration is denied.
4. Reload the Agent page: memory starts empty, protected refresh cookie restores authentication through rotation and /me. No access/refresh credentials appear in localStorage/sessionStorage. Cookie is Secure/HttpOnly/Strict; JS cannot read refresh value.
5. In a separate browser profile/context sign in as Admin. Disable that Agent. In its still-open context, the next protected ticket request fails, refresh fails and new signin fails; unexpired JWT must not extend access. Its existing ticket assignments/history stay readable to Admin and clearable according to ticket rules.
6. Re-enable the Agent. Old JWT/refresh still fail; fresh signin succeeds and assignment eligibility returns.
7. Sign out and register a Customer at /auth/signup with display name/email/password. Signup creates only an active Customer and routes to explicit signin, never automatic login. Sign in: identity/access page is available, tickets and Agent administration are denied, and no portal business UI is added.
8. Verify public signup role/actor overrides and Customer/Agent direct Agent-management requests are denied. Try wrong/nonexistent/disabled credentials; failure text must not disclose which condition occurred.
9. Use separate browser profiles for independent sessions, keeping one active tab per session: signing out one must not affect the other. Within the Agent application instance, trigger concurrent protected requests when its JWT has expired and verify one /api/auth/refresh operation, followed by at most one replay per waiting request with the new JWT. Force refresh401,503/network and failed restoration separately: JWT/current-user state clears, all waiters fail consistently and no refresh/retry loop occurs. Verify a late old-JWT401 does not cause redundant rotation after another request already renewed authentication, or after a failed refresh invalidated that generation. Multiple simultaneously active tabs sharing the same authentication session are outside the supported scope of this feature.
10. Verify legacy references remain on saved tickets and readable in history but cannot be selected for a new assignment. New Agent accounts use new IDs, never a legacy link. Category CLI rejects nonempty agents input and still supports category-only upserts.

See contracts/authentication.md and agent-management.md for exact request/response/version/error assertions. Do not paste tokens/passwords into screenshots, reports or console logs. Browser network inspection can verify status/cookie flags without publishing sensitive values.

## 4. Automated verification

Use existing guarded disposable SQL fixture setup and same Integrated Security identity; supply a newly owned CrmTicketsIntegration_GUID connection through CRM_TEST_SQL_CONNECTION rather than reusing/resetting CustomerSupportCrm. Existing tests refuse missing/unsafe SQL configuration. E2E runner must create owned catalogs, explicitly migrate/bootstrap private local accounts and use real signin rather than injected tokens; updated launcher integration tests use the same local auth.

```powershell
dotnet build Crm.slnx --no-restore
dotnet build tests/scripts/fixtures/DevFixture --no-restore
$env:CRM_TEST_SQL_CONNECTION='Server=localhost;Database=CrmTicketsIntegration_'+[guid]::NewGuid().ToString('N')+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test Crm.slnx --no-build --logger 'console;verbosity=minimal'
npm test --prefix src/frontend -- --watch=false
npm run build --prefix src/frontend
$env:CRM_E2E_SQL_CONNECTION='Server=localhost;Database=CrmTicketsE2E_'+[guid]::NewGuid().ToString('N')+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
npm run e2e --prefix src/frontend
pwsh -NoProfile -File tests/scripts/run-dev.tests.ps1
pwsh -NoProfile -File tests/scripts/run-dev.integration.ps1
pwsh -NoProfile -File tests/scripts/run-dev.integration.ps1 -InitialSchema
pwsh -NoProfile -File tests/scripts/bootstrap-admin.tests.ps1
dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build
```

Serialize fixed-port browser/launcher runs; retain existing ownership and cleanup safeguards. New tests must initially fail before corresponding implementation, then pass without weaker assertions. Required cases include password14/15/128/129 scalars, spaces-only and supplementary Unicode, concurrency/rollback, JWT invalid signatures/claims/expiry, CSRF/Origin, refresh replay/lost response, signout/disable/re-enable with old JWT, role matrix/direct API bypass, bootstrap races, legacy bytes preserved and credential-free logs. Complete existing ticket regressions under local Agent and Admin identities.

Final results: Domain43/Application130/Architecture7/SQL-API333, Angular122 and Playwright37 all pass with zero failures/skips; builds, migration consistency, native bootstrap, fresh/older/repeat launcher, strict proxy and protected-key restart checks pass. Detailed commands, corrections and cleanup are in the linked validation artifacts. This feature's SC-005 is a scripted Admin workflow, not the five-staff evaluation from Feature001. Do not attempt or fabricate Feature001 T079; T081 remains provisional.
