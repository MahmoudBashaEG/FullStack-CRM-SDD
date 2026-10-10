# Feature 002 final verification

Date: 2026-10-10, Africa/Cairo. Scope: T098–T107 only, following completed T001–T097/T108–T111. Existing uncommitted US4/US5 work was preserved. No new business capability, password recovery, portal, cross-tab infrastructure or account-administration expansion was introduced. Specification, plan, research, data model and contracts remain authoritative and unchanged.

## Corrections and new verification

T098 adds five actual SQL/API cases in AuthenticationSecurityTests: configured/unconfigured prior signing-key acceptance at the API boundary; expired absolute session rejecting a cryptographically unexpired JWT and refresh; a lost successful refresh response causing replay rejection/family revocation; and successful/failed transport redaction with hashed passwords and digest-only refresh persistence. All five initially pass existing backend behavior. Existing meaningful security, signup, bootstrap, Agent, concurrency and Ticket tests are retained rather than duplicated or weakened.

Final review exposed a genuine Angular defect. After a503/network refresh failure cleared JWT/identity, its generation remained current. A delayed old-JWT expiry response could therefore start another automatic refresh after the first flight failed. Two new AuthService regressions failed (unexpected second refresh, one request rather than zero) before production was changed. AuthService now invalidates the failed generation before clearing state; delayed requests are rejected, all current waiters fail, and explicit recovery starts a new generation. The original nine state tests and the two new cases pass11/11. Auth-platform browser cases deliberately delay one expiry response until after503/network failure and verify exactly one refresh, no replay/loop, cleared role navigation and unavailable diagnostics. Ordinary feature network failures still preserve valid auth for explicit Ticket recovery; uncertain mutations are never automatically replayed.

The new auth-platform browser checks cover public forms and Admin pages at320/375/768 pixels in LTR/RTL, Unicode/exact-space inputs, keyboard focus, labels/password description, no incorrect UTF-16 maxlength, no credential storage and no Web Locks/BroadcastChannel. Initial harness issues were corrected: wait for NgModel initialization before entering text, include the actual Admin HttpOnly cookie in the private fixture manifest, and scope a repeated Agents link to the Main navigation. Behavioral assertions, rate limits, timeouts, token lifetime and retry policy remain unchanged. Final targeted platform result:4/4 PASS.

## Current results

| Verification | Result |
|---|---|
| Crm.slnx and DevFixture builds, --no-restore --verbosity quiet | PASS, zero warnings/errors |
| Complete Angular suite, npm --prefix src/frontend test -- --watch=false | PASS122, failed0, skipped0,22 files |
| Angular production build, npm --prefix src/frontend run build | PASS |
| New security cases | PASS5, failed0, skipped0 |
| Architecture checks | PASS7, failed0, skipped0 |
| EF pending-model verification | PASS, no model changes since last migration |
| Real Development launcher: fresh and older schema | PASS, full role workflow, pending migrations, restart/security-state preservation, watch/reload, secure proxy and cleanup |
| Complete backend solution suite | PASS513: Domain43/Application130/Architecture7/SQL-API333; failed0/skipped0 |
| Complete Playwright suite | PASS37, failed0/skipped0, one worker/zero retries,6.2m |
| Native launcher safeguards | PASS: cooperative68ms, forced5018ms/orphan5050ms, all console/preflight cases including122.2s timeout and environment restoration |

## Reproduction commands

From the repository root in PowerShell7, using the existing dependency setup:

```powershell
. ./scripts/environment.ps1
dotnet build Crm.slnx --no-restore --verbosity quiet
dotnet build tests/scripts/fixtures/DevFixture --no-restore --verbosity quiet
$env:CRM_TEST_SQL_CONNECTION='Server=localhost;Database=CrmTicketsIntegration_'+[guid]::NewGuid().ToString('N')+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test Crm.slnx --no-build --logger 'trx;LogFileName=final-verification.trx' --verbosity minimal
npm --prefix src/frontend test -- --watch=false
npm --prefix src/frontend run build
$env:CRM_E2E_SQL_CONNECTION='Server=localhost;Database=CrmTicketsE2E_'+[guid]::NewGuid().ToString('N')+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
npm --prefix src/frontend run e2e
pwsh -NoProfile -File tests/scripts/run-dev.tests.ps1
pwsh -NoProfile -File tests/scripts/run-dev.integration.ps1
pwsh -NoProfile -File tests/scripts/run-dev.integration.ps1 -InitialSchema
pwsh -NoProfile -File tests/scripts/bootstrap-admin.tests.ps1
dotnet ef migrations has-pending-model-changes --no-build --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api
```

Fixed-port browser/launcher runs are serialized. Launcher watch probes finish and restore source bytes before SQL migration source-fingerprint assertions run. Native safeguards copy an isolated workspace; no developer data is reset. The default development catalog is never a verification target. Normal HTTPS certificate validation remains enabled throughout.

## Security and role review

Identity uses its standard password hash/rehash over the shared LocalUser context, with the common15–128 Unicode-scalar validator in signup/Agent/bootstrap. Spaces and supplementary Unicode are exact; no trimming, normalization or truncation. Normalized-email unique indexes resolve races. Generic failures, rollback, duplicate/escalation rejection and forbidden Agent mutations are checked through real persistence/API tests.

RS256 uses the dedicated3072+bit configured certificate, exact issuer/audience/type/signature/expiry, five-minute capped access lifetime, zero skew and minimal claims without names/emails/secrets. Every protected request checks the active account, role/version and live session; SQL outage fails closed503. Prior public-key configuration permits only still-valid signed JWTs; removing the key rejects them. Private signing material is never exported by runtime or committed.

Opaque refresh secrets have only SHA256 digests in SQL and a Secure/HttpOnly/Strict host-only cookie. Rotation has one successor, consumed replay revokes only that family, seven-day absolute expiry does not slide, and lost delivery has no retry grace. Signout revokes the current family, independently signed-in contexts survive, and mismatched live contexts are rejected. Signup/signin/refresh/signout use framework anonymous-bound antiforgery plus exact Origin; bearer does not bypass signout CSRF. Strict DTO/media/query and rate-limit rules remain.

| Identity | Public signup/signin | Current identity/signout | Ticket queue/operations | Agent management |
|---|---|---|---|---|
| Visitor | Available with CSRF/input/Origin rules | No protected identity; repeated signout safe |401 |401 |
| Customer | Customer signup only; own signin | Allowed with live authentication |403 |403 |
| Agent | No public Agent creation; own signin | Allowed while active | Allowed across shared queue |403 |
| Admin | No public Admin creation; operator bootstrap | Allowed | Allowed across shared queue | Allowed, Agents only |

Admin alone never receives an assignment identity. Active linked Agents are selectable; disabled/unlinked legacy references are not. Saved assignments remain readable, retainable and clearable; unrelated edits/status mutations stay available to authorized staff. Disable rejects old unexpired JWTs, refresh and new signin; re-enable requires fresh signin and does not resurrect old sessions. Historical actor/reference snapshots and Ticket concurrency/lifecycle/history behavior remain intact.

## Architecture, data and operational review

Seven compiled/project/controller architecture checks pass: inward dependencies, Identity/EF ownership in Infrastructure and MediatR-only controllers. Roles and account identity are shared; features consume core AuthService/transport/policies and ICurrentActor. Async SQL/saves/transactions propagate cancellation; framework calls without cancellation overloads retain boundary checks. Existing transactions have no uncertain-write automatic retry or event-sourcing addition.

Data Protection uses application name Crm, explicit durable Windows user-profile repository and current-user DPAPI protection; an operator/owned directory uses the same protection. Normal restart accepts the retained pre-restart CSRF/refresh pair and preserves the encrypted key files; signing keys and SQL refresh digests are separate concerns. No real key ring is deleted or copied into Git.

Four existing versioned migrations remain:20261008164804_InitialTicketStorage,20261008224935_LongTicketSubjectSearch,20261009124742_LocalCrmAuthentication,20261009131155_RefreshSessionLookupIndex. No new migration is needed. Fresh/older/current/repeated startup and populated transitions retain saved IDs/history and leave old references unlinked. Only Development uses MigrateAsync; non-Development/CLI schema setup is explicit. There is no EnsureCreated or automatic migration generation/bootstrap. Before a persistent upgrade, take an operator-approved backup and verify restoration in an owned copy; restoring backup with the matching application version is the rollback approach rather than dropping newly created account/session data through an unreviewed downgrade.

OIDC retirement review finds no active external Authority/discovery/JWKS/client/callback/token-injection dependency. Historical external issuer/subject values remain as immutable compatibility data. Deliberate legacy Test* rejection cases and an innocuous test-variable name authority are not runtime providers. All27 recorded Feature001 artifact hashes match. Feature001 T079 remains pending for unavailable real staff and T081 provisional; Feature002 SC-005 is the scripted Agent-management sequence and requires no fabricated participants.

Source-control review includes tracked and nonignored working-tree files: zero private-key/PFX/DP-key artifacts, zero literal private-key/JWT payloads and no shipped source/config password. Synthetic test password inputs are fixtures/boundary probes, not defaults or public privileged seeds. Production configuration uses Integrated Security and references operator-managed signer material; ignored owned credential/cookie files are private and cleaned after runs. All required suites pass. Final cleanup and traceability are recorded below.

## Final task and cleanup status

T098–T107 are complete. Feature002 has111/111 completed tasks, including previously completed T001–T097/T108–T111; earlier Customer/Agent integration gates remain complete. The complete backend solution invocation passes513/513 (43 Domain,130 Application,7 Architecture,333 SQL/API), zero failures/skips. The complete Angular suite passes122/122 in22 files, and complete Playwright37/37, zero failures/skips/retries. Native bootstrap independently passes11/11; these cases are also part of the333 SQL/API total, not additional distinct coverage. DataProtection2/2, Development-migration11/11, environment-boundary8/8 and new-security5/5 are likewise subsets of that total. Builds/EF/proxy/root fresh-older-repeat/shutdown checks pass. No required check remains blocked.

Final read-only cleanup: zero CrmTicketsIntegration_/CrmTicketsE2E_ catalogs, zero4200/7043 listeners, zero owned e2e/auth-test/dp-test/dev-integration temporary directories and zero owned browser/Foundation signing certificates. The existing trusted HTTPS files and durable real user-profile Data Protection ring are intentionally retained. All27 historical Feature001 SHA256 hashes still match. git diff --check passes; final solution/DevFixture builds have zero warnings/errors and EF reports no pending model changes. No pre/post implementation extension hooks exist.

FR-001–FR-016 and SC-001–SC-006 PASS with scenario/component/test/reproduction mapping in [requirements-coverage.md](requirements-coverage.md); all six constitutional checks PASS. Current shared architecture and quickstart describe the implemented protected key repository, local role workflow and failed-generation behavior. No approved-design deviation or outstanding implementation defect is known. Only the genuine late-expiry-after-failed-refresh defect required production correction; fixture readiness/cookie/selector improvements affect verification only. No assertion/lifetime/rate-limit/timeout was weakened.

Known limitations: local Windows/SQL/HTTPS and operator-managed signing/key access; same-origin deployment; one active tab per session; no password reset/recovery/change/MFA/email verification, broader account administration or portal. Feature001 T079 remains pending for unavailable real support staff and T081 provisional, independent of Feature002's scripted SC-005. No participant evidence was invented. Feature002 is ready for a separate speckit-converge run; it was not invoked here. Existing working-tree changes are preserved and no Git commit was created.

## Files changed in this final phase

- src/frontend/src/app/core/auth/auth.service.ts — invalidate failed generation.
- src/frontend/src/app/core/auth/auth.service.spec.ts — two failing-first late-expiry regressions.
- tests/backend/Crm.IntegrationTests/Security/AuthenticationSecurityTests.cs — five final SQL/API security cases (new).
- src/frontend/e2e/auth-platform.spec.ts — four final platform/failure-coordination cases (new).
- src/frontend/e2e/fixtures/ticket-local.fixture.ts — actual Admin cookie restoration support.
- tests/scripts/fixtures/DevFixture/Program.cs — capture the real Admin refresh cookie in the private owned manifest.
- tests/scripts/fixtures/auth-development-observations.mjs — actual Development role and retained-security-state browser workflow (new).
- tests/scripts/run-dev.integration.ps1 — run that workflow and verify encrypted key-ring stability on restart.
- specs/002-user-auth-access/quickstart.md — verified workflow, isolated test configuration, durable keys and evidence links.
- docs/architecture/authentication-authorization.md — current implementation status, protected repository/restart and generation behavior.
- docs/development/core-ticket-management.md — live final-auth evidence/recovery links.
- specs/002-user-auth-access/tasks.md — verified T098–T107 checkboxes only.
- specs/002-user-auth-access/validation/final-validation.md (new).
- specs/002-user-auth-access/validation/local-development-auth.md (new).
- specs/002-user-auth-access/validation/requirements-coverage.md (new).

No migration/package/configuration-secret/spec/plan/research/data-model/contract or historical Feature001 artifact changes. Native watch probes restore source/project bytes exactly. The working tree also contains earlier US4/US5 work, indexed in their existing validation file lists; it was not redone or discarded.
