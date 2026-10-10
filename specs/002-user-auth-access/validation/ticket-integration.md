# Ticket Management integration checkpoint

Date: 2026-10-10 (Africa/Cairo). Scope: Feature 002 US5, T086–T097/T111 and the dependent Customer T067–T068 and Agent T084–T085 gates. T092 was already complete and was reused. The existing uncommitted US4 implementation was preserved. No unrelated T098–T107 implementation was undertaken.

## Identity, authorization and preserved behavior

The shared SupportQueue policy remains authoritative on every Ticket action: active Agent/Admin can manage the whole queue regardless of assignment; Customer receives403 and visitors receive401. AdminAgents still permits only Admin. Ticket routes consume shared AuthService, guards and Bearer transport. No Ticket-specific authentication mechanism was added.

CurrentActor now reads only the server-populated TrustedAuthentication snapshot, for any role, and maps it to Actor(urn:crm:local:user,canonical UserId,current display name). Queue permission belongs to endpoint authorization rather than actor resolution. Raw/caller claims and submitted actor/role fields cannot replace the trusted identity. New Agent/Admin history has the local identity; earlier external actors and names are never rewritten. Meaningful Application and actual SQL/API tests verify stable IDs and historical snapshots across name changes.

Assignment choices and saved-reference representations compute eligibility from both the reference and its linked active local Agent account. Admin/Customer roles alone and unlinked legacy references are not eligible, even when an old reference flag is true. Admin creation supplies a new stable reference; name matching never links old references. Disable removes eligibility and invalidates unexpired JWTs and refresh families on subsequent requests. Re-enable restores eligibility but does not revive old authentication; fresh signin is required.

Saved inactive/legacy assignments remain readable and retainable during unrelated edits/status changes, and can be cleared. New selection requires current eligibility. Selected references use transaction-held update locks before the account eligibility read. This prevents a reference/account writer deadlock while retaining the selection-versus-disable serialization guarantees. Existing stale-version, no-op, closed-ticket, category/fallback, transaction rollback, history ordering/pagination and concurrency assertions remain.

## Test-first observations and corrections

- Six new local actor/effective-reference integration cases failed before the actor/eligibility implementation. The initial complete SQL/API run was299 passed/29 failed/328 total, zero skips (`us5-baseline.trx`). The obsolete identity/provisioning/migration assumptions were reviewed rather than suppressed.
- Ticket fixtures now establish real local accounts and obtain access JWTs through actual HTTP csrf/signin. Isolated cryptographic/migration/dependency-negative fixtures use the same real authentication use case; only deliberate invalid-signature/issuer/audience/expiry tests alter JWTs. There is no synthetic success scheme, Angular token injection or external discovery server. Normal HTTP fixture tokens are the actual signin response, not reissued substitutes.
- Agent fixture setup uses genuine creation/rename/availability commands or Admin APIs; provisioning remains categories-only. Existing Agent counts/IDs/actor expectations now correspond to genuine local accounts. Historical actors stay external. Migration tests expect all four existing migrations; old-schema snapshots query only columns present before the Identity upgrade. An unmigrated non-Development account/session store fails protected requests closed with503 and performs no migration.
- Real browser lost-save/lost-lifecycle-response checks exposed a shared interceptor defect: ordinary feature503/network failures cleared an otherwise live JWT, preventing explicit reload/review. Two new interceptor regressions first failed, then passed after clearing was restricted to authoritative protected401. AuthService still clears every failed refresh/restoration, and uncertain mutations are never automatically replayed.
- Browser role checks initially had two ambiguous heading locators; narrowed locators retain exact subject assertions. Repeated full document navigations also exhausted the unchanged refresh limit. Ordinary route changes now use same-instance Angular navigation; explicit reload tests still perform real cookie restoration. Sequential owned contexts preserve the latest HttpOnly cookie only after responses settle. No rate limit, timeout, assertion, retry count or JWT lifetime was relaxed.
- Native console helpers needed the complete SqlClient runtime directory when copied into an owned test workspace. The real launcher negative-TLS case needed its elapsed timer initialized. Both harness errors were corrected without removing shutdown/readiness/timeout/TLS checks. The intentionally missing-database HTTP test prepares its normal authentication outside the deliberately broken target store, then retains the503/no-commit assertions.

## Harness, migration and data preservation

The Ticket-specific T111 extension reuses T092's normal local-auth runner. Private owned manifests contain synthetic test identities and credentials; nothing is injected into Angular. Agents are created through real Admin APIs, categories through the approved CLI. Tracking/history seeds substitute newly created Agent reference IDs only when preparing a fresh owned fixture. Existing production data is not rewritten. A separate unlinked legacy fixture proves saved reference/history compatibility.

The external issuer/JWKS process, old token-injection fixture, migration-fixture authentication override and unused public-key export/raw-claims helper have been removed. Earlier foundation retirement of oidc-client-ts, OIDC callback/environment and sessionStorage token handling is reused. Test* configuration remains only as a deliberate rejection test. Legacy issuer/subject columns and immutable historical actor metadata remain intentionally supported.

No new migration was required. Model verification reports no pending changes. The existing four compiled migrations are InitialTicketStorage, LongTicketSubjectSearch, LocalCrmAuthentication and RefreshSessionLookupIndex. Actual populated upgrade tests preserve Ticket/reference identifiers and history serialization, leaving legacy LocalUserId null. Normal Development startup applies existing pending migrations; non-Development never applies them, and no EnsureCreated or automatic migration generation was introduced.

All 27 Feature 001 artifacts in feature001-artifact-baseline.json still match SHA256. Feature 001 staff T079/SC-005 remains pending and T081 provisional. No participants or observations were invented.

## Executed verification

Commands run from the repository root after `. ./scripts/environment.ps1`. SQL and browser runs use fresh guarded local catalogs with Integrated Security, the existing Windows account and normal HTTPS validation; zero SQL skips.

| Verification | Observed result |
|---|---|
| dotnet build Crm.slnx --no-restore --verbosity quiet | PASS, zero warnings/errors |
| dotnet build tests/scripts/fixtures/DevFixture --no-restore --verbosity quiet | PASS, zero warnings/errors |
| Domain / Application / Architecture, dotnet test --no-build --verbosity quiet | PASS 43 /130 /7 |
| Complete SQL/API, dotnet test tests/backend/Crm.IntegrationTests --no-restore --logger 'trx;LogFileName=us5-final.trx' --verbosity quiet | PASS 328, failed0, skipped0, 4m28s |
| npm --prefix src/frontend test -- --watch=false | PASS 120 in22 files |
| npm --prefix src/frontend run build | PASS production build |
| npm --prefix src/frontend run e2e | PASS 33, retries0, 6.2m; actual five-minute expiry/coalescing and strict proxy included |
| dotnet ef migrations has-pending-model-changes --no-build --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api | PASS, no changes since last migration |
| pwsh -NoProfile -File tests/scripts/run-dev.tests.ps1 | PASS cooperative/forced/orphan/console/root-first/occupied-port/dependency/certificate/partial-start/120s-timeout safeguards and environment restoration |
| pwsh -NoProfile -File tests/scripts/run-dev.integration.ps1 -InitialSchema | PASS real older-schema launcher, four migrations, local signin/refresh/proxy, Ticket lifecycle, watch/reload, repeat-start preservation, negative TLS and cleanup |

Reproduce SQL/API and browser checks with separate owned catalogs:

```powershell
$env:CRM_TEST_SQL_CONNECTION='Server=localhost;Database=CrmTicketsIntegration_'+[guid]::NewGuid().ToString('N')+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test tests/backend/Crm.IntegrationTests --no-build --verbosity quiet
$env:CRM_E2E_SQL_CONNECTION='Server=localhost;Database=CrmTicketsE2E_'+[guid]::NewGuid().ToString('N')+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
npm --prefix src/frontend run e2e
```

Customer integration proves public Customer-only signup without auto-login, explicit signin/me/reload, direct Ticket and Agent-management403, route/nav denial and signout revocation. Agent integration proves actual Admin create/rename, Agent signin/create/assignment/history, unexpired old-token denial after disable, unavailable choices, retained assignment and immutable earlier history, re-enable with the same reference ID, and fresh signin. These are scripted Feature 002 SC-005 checks, independent of Feature 001's pending human usability study.

The fresh-schema launcher also passed the full equivalent checks (pwsh -NoProfile -File tests/scripts/run-dev.integration.ps1). A targeted Agent-management browser rerun passed 1/1 after adding explicit 401 for the disabled refresh response. The final post-retirement complete SQL/API rerun also passed 328/328, zero skips (us5-final-cleanup.trx, 4m28s). No approved specification/planning change or feature-scope deviation was needed. Final Verification remains a separate unimplemented phase.

## Final checkpoint state

Completed T086–T097 and T111; T092 was reused rather than redone. Dependent T067–T068 and T084–T085 are complete after their integrated proofs. T098–T107 remain unchecked. The exact US5 file list is in [ticket-integration-files.md](ticket-integration-files.md); earlier US4 working-tree changes were preserved.

Final solution and DevFixture builds pass with zero warnings/errors after the watch probes restored their exact source bytes. git diff --check passes. Read-only cleanup checks find zero owned SQL fixture catalogs, zero4200/7043 listeners, zero owned temporary fixture directories and zero owned signing certificates. All27 historical Feature001 SHA256 values still match. Approved spec/plan/research/data-model/contracts/quickstart/constitution/architecture artifacts are unchanged. No after_implement extension hooks are registered.

No remaining test failures, approved-design deviation or implementation blocker before the separate Final Verification phase. The approved single-tab limitation remains in force. No work was committed in this run.
