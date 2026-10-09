# Quickstart: Local Accounts and Access Control

This is a proposed reproduction guide for Feature002 after implementation. New routes/CLI/local-account behavior do not exist merely because this plan was written. No commands in this guide were executed during planning; results must be recorded later in this feature's validation evidence. Feature001 historical OIDC evidence remains historical.

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

Remove current runtime Authentication:Authority/Audience and frontend environment.oidc requirements during implementation; do not leave two schemes. Old Authentication__Test* values remain forbidden in normal Development. Update prior test fixtures to use isolated local accounts/certificates, not external discovery or hardcoded shipped credentials.

## 2. Apply explicit schema and bootstrap

Migration files are generated/reviewed/versioned during implementation, not in startup. Development API applies existing pending compiled migrations using the existing hook. For explicit initial setup (also the non-Development/CLI path):

```powershell
dotnet build Crm.slnx
dotnet ef database update --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build
dotnet run --no-launch-profile --project src/backend/Crm.Api -- bootstrap-initial-admin
```

Use the installation command's non-echo password prompt; do not supply a password in argv, committed JSON or shell history. Choose an operator-owned email and unique password meeting 15–128 Unicode-scalar policy. No example/default Admin credential is provided. Expected result created; repeating command returns already_complete and changes no credentials/roles. Missing schema/invalid input fails safely. Commands/config details are defined in contracts/bootstrap.md.

For future model changes developers explicitly generate a named migration, inspect its operations and model snapshot, build/test and commit it. Startup never generates one or uses EnsureCreated. Feature002 transition makes legacy AgentReferences ineligible without removing saved assignments/history. Inspect a populated disposable migration fixture before touching a real development catalog; never reset the developer's data for verification.

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
9. Use separate browser profiles for independent sessions, keeping one active tab per session: signing out one must not affect the other. Within the Agent application instance, trigger concurrent protected requests when its JWT has expired and verify one /api/auth/refresh operation, followed by at most one replay per waiting request with the new JWT. Force refresh401,503/network and failed restoration separately: JWT/current-user state clears, all waiters fail consistently and no refresh/retry loop occurs. Verify a late old-JWT401 does not cause redundant rotation after another request already renewed authentication. Multiple simultaneously active tabs sharing the same authentication session are outside the supported scope of this feature.
10. Verify legacy references remain on saved tickets and readable in history but cannot be selected for a new assignment. New Agent accounts use new IDs, never a legacy link. Category CLI rejects nonempty agents input and still supports category-only upserts.

See contracts/authentication.md and agent-management.md for exact request/response/version/error assertions. Do not paste tokens/passwords into screenshots, reports or console logs. Browser network inspection can verify status/cookie flags without publishing sensitive values.

## 4. Automated verification

Use existing guarded disposable SQL fixture setup and same Integrated Security identity; supply a newly owned CrmTicketsIntegration_GUID connection through CRM_TEST_SQL_CONNECTION rather than reusing/resetting CustomerSupportCrm. Existing tests refuse missing/unsafe SQL configuration. E2E runner must create owned catalogs, explicitly migrate/bootstrap private local accounts and use real signin rather than injected tokens; updated launcher integration tests use the same local auth.

```powershell
dotnet build Crm.slnx --no-restore
dotnet test Crm.slnx --no-build --logger 'console;verbosity=minimal'
npm test --prefix src/frontend -- --watch=false
npm run build --prefix src/frontend
npm run e2e --prefix src/frontend
pwsh -NoProfile -File tests/scripts/run-dev.tests.ps1
pwsh -NoProfile -File tests/scripts/run-dev.integration.ps1
dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build
```

Serialize fixed-port browser/launcher runs; retain existing ownership and cleanup safeguards. New tests must initially fail before corresponding implementation, then pass without weaker assertions. Required cases include password14/15/128/129 scalars, spaces-only and supplementary Unicode, concurrency/rollback, JWT invalid signatures/claims/expiry, CSRF/Origin, refresh replay/lost response, signout/disable/re-enable with old JWT, role matrix/direct API bypass, bootstrap races, legacy bytes preserved and credential-free logs. Complete existing ticket regressions under local Agent and Admin identities.

Record new commands/results, failing-first evidence, migration IDs/data invariants and cleanup in Feature002 validation artifacts during implementation. No pass result is claimed now. This feature's SC-005 is a scripted Admin workflow, not the five-staff evaluation from Feature001. Do not attempt or fabricate Feature001 T079; T081 remains provisional.
