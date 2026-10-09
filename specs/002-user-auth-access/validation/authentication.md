# Authentication implementation — US2

Validated 2026-10-09 (Africa/Cairo). Scope: T036–T059 plus explicit prerequisites T092/T108/T109/T110. T001–T035 were reused. Customer signup, Agent management, Ticket-specific fixture expansion/T111 and later integration work remain unimplemented. No real developer account was seeded/reset and no developer database was reset.

## Implemented behavior

- MediatR signin/refresh/signout commands and safe current-identity query use inward Application ports. Infrastructure owns Identity verification/rehash/dummy verification, session transactions and standard cryptographic primitives. API controllers retain the existing sole-ISender constructor rule; transport filters own anonymous-bound antiforgery and secure cookie emission/deletion.
- Signin trims/normalizes email only, preserves the exact 15–128-scalar password, verifies through Identity, rejects inactive users, and produces identical `invalid_credentials` responses for wrong/nonexistent/disabled accounts. A precomputed framework dummy hash covers nonexistent-account verification. Failed signin leaves a previous valid server context unchanged.
- Actual five-minute RS256 JWTs retain the existing strict issuer/audience/algorithm/type/claim/lifetime validation and live account/session/version read on every protected request. `/me` returns only userId/displayName/role. Visitors/invalid tokens receive401, valid Customers receive403 for the queue, and Agent/Admin queue access is verified.
- Seven-day absolute families hold only random32-byte refresh digests. Account locks precede session/credential locks; replacing contexts locks multiple account IDs in deterministic order. Rotation consumes before successor insertion inside one transaction. Known consumed replay commits family revocation and returns401; other families survive. Lost responses are not retried with consumed input. No uncertain-commit/autoretry policy was added.
- Secure/HttpOnly/Strict host-only refresh and antiforgery cookies use Path=/ and no Domain. Cookie lifecycle operations require an anonymous-bound framework CSRF pair and exact configured Origin. Bearer is rejected on csrf/signin/refresh; signout permits only the trusted server-validated optional context and rejects mismatched live contexts409. Signout revokes the current family, clears the cookie and rejects its unexpired JWT afterwards.
- Strict JSON/duplicate/unknown-field/query/media checks remain. Signin10/min/IP, unused future signup5/min/IP policy, and refresh30/min/trusted-family/IP return safe429/Retry-After. Forwarded headers are disabled unless explicitly configured `ReverseProxy:KnownProxies` IPs are provided; spoofed X-Forwarded-For cannot bypass the default limiter. SQL dependency faults remain fail-closed503; unexpected uniqueness exceptions are not indiscriminately recast as dependencies.
- Angular keeps JWT/identity/CSRF state in memory. Initializing startup performs CSRF→refresh→me before navigation. One shared refresh runs through identity restoration/publication; waiting requests replay at most once, late old-token expiry uses the renewed token, and all failures clear authorization state without loops. Signin/signout serialize against pending cookie responses and against each other because generation checks alone cannot discard browser-applied HttpOnly Set-Cookie. Stale completions cannot publish state. No Web Locks/BroadcastChannel/cross-tab support exists.
- Signin preserves exact password input, clears the submitted password, displays generic errors and lands Agent/Admin on tickets and Customers on safe identity/access. Protected guards/navigation consume shared state; the server remains authority. Failed signout is visibly unconfirmed with explicit retry, not a false revocation claim. Malformed envelopes are rejected before sending any invalid bearer request. No signup/portal/Agent-management UI was added.

Multiple simultaneously active tabs sharing the same authentication session are outside the supported scope of this feature.

## Shared browser / local HTTPS

T092 replaces the active OIDC/token-injection substitution with the normal AuthService and discovers auth specs outside the former tickets-only directory. The runner retains guarded fresh SQL creation/ownership cleanup, explicit Testing migrations, private synthetic credentials and exclusively owned signing certificate cleanup. It bootstraps Admin through the actual CLI with private stdin; Customer/Agent/disabled identities are prepared through standard Identity in test-only helpers, never public seed APIs or invented product signup/Agent creation.

Angular serves trusted HTTPS4200 and proxies relative `/api` to trusted HTTPS7043 with secure verification enabled. E2E explicitly reuses the existing approved development PEM/key for both hosts and NODE_EXTRA_CA_CERTS for Node; no new TLS certificate/export/trust-store change occurred. An added Node system-CA mode initially failed strict readiness; removing that extra mode restored the existing PEM trust approach without `ignoreHTTPSErrors`/secure:false. Proxy verification observes forwarded Origin/Bearer/Set-Cookie, private flags, rotation, revocation and `/api-docs` remaining frontend HTML.

Full Playwright discovery remains the default; this checkpoint explicitly invokes `authentication.spec.ts`. Old Ticket-specific browser fixtures remain T111 and are not silently removed from eventual full discovery. The helper observes actual application bearer publication rather than injecting tokens. Chromium did not retain a navigated response body for inspection, so private test token metadata comes from the application's actual `/me` Authorization request instead; lifetime is decoded only for test measurement, never application authentication authority.

## Test-first and corrections

- New command contracts compiled against throwing declarations: **6 application tests failed**, then passed after handlers.
- Initial real auth API/SQL tests: **8 failed** before endpoints/transaction implementation, then passed.
- Angular coordinator: **3 red**; AuthService: **4 red**; interceptor: **4 red**; root role navigation: **1 red** before their respective behavior. Core transport/privacy assertions were retained.
- The framework Data Protection probe genuinely passed before wiring; no fabricated red result. See data-protection.md for restart evidence.
- Some supplementary SQL/rollback/limiter/privacy/race tests were added after the initial implementation and are not retrospectively claimed as red-first. Signin/access component tests were also added after their first implementation; this is a test-first sequencing deviation, not a changed requirement/assertion.
- Initial Angular runs required normal filesystem access after compiler sandbox denial; tests/configuration were not weakened. Fixture restore required network access for vulnerability metadata; audit checks stayed enabled.
- Async test transport expectations were advanced through their promise microtasks, with CSRF-after-transition requests explicitly verified. No assertion/timeout workaround.
- Existing controller architecture assertion caught extra adapter constructor parameters. Transport responsibilities moved to API filters, retaining the original architecture test unchanged; **7/7** now pass.
- Existing ticket unit-test doubles gained only the new shared AuthService methods/state needed by their harness. Their substantive assertions and ticket implementations remain unchanged; no backend/full Ticket E2E migration occurred.
- Explicit malformed-envelope and failed-signout-retry checks first failed (**2 red**), then passed after the corrections.
- The in-flight cookie response race was corrected with within-instance mutation serialization, while keeping generation and final signed-out-state assertions and adding the no-overlapping-signout-request assertion.

## Commands / results

All SQL runs use fresh owned CrmTicketsIntegration_GUID/CrmTicketsE2E_GUID catalogs and the same configured local Windows Integrated Security identity. Test credentials/keys/cookies/token values are never published here. Builds are run after loading `scripts/environment.ps1`.

| Verification | Result |
|---|---|
| dotnet build Crm.slnx --no-restore --verbosity quiet | PASS, 0 warnings/errors |
| dotnet build tests/scripts/fixtures/DevFixture --no-restore --verbosity quiet | PASS, 0 warnings/errors |
| Domain suite | PASS, 43/43 |
| Application suite | PASS, 100/100 |
| Architecture suite | PASS, 7/7; assertions unchanged |
| Focused real SQL/API/Identity/auth/JWT/privacy/restart regression suite | PASS, 87/87 |
| npm test --prefix src/frontend -- --watch=false | PASS, 97/97 across18 files |
| npm run build --prefix src/frontend | PASS |
| EF has-pending-model-changes | PASS, no model changes/new migrations |
| npm run e2e --prefix src/frontend -- authentication.spec.ts | PASS, 8/8, 5.3 minutes; real expiry, HTTPS, cookie flags, roles, independent sessions, restoration and error recovery |
| tests/scripts/local-auth-proxy.tests.mjs inside owned running harness | PASS, strict HTTPS/Origin/Bearer/cookie/rotation/revocation checks |

Focused integration command:

```powershell
dotnet test tests/backend/Crm.IntegrationTests --no-build --filter 'FullyQualifiedName~Crm.IntegrationTests.Identity|FullyQualifiedName~Crm.IntegrationTests.Authentication|FullyQualifiedName~LocalJwtTests|FullyQualifiedName~LocalAuthorizationFoundationTests|FullyQualifiedName~AuthConfidentialityTests|FullyQualifiedName~DataProtectionPersistenceTests' --logger 'console;verbosity=minimal'
```

Browser reproduction after the builds/approved HTTPS prerequisites: provide a fresh `CRM_E2E_SQL_CONNECTION` with a guarded CrmTicketsE2E_GUID name, then:

```powershell
npm run e2e --prefix src/frontend -- authentication.spec.ts
```

The earlier complete six-case run passed twice, including actual five-minute server expiry with multiple expired protected requests and exactly one shared refresh. The expiry case has a new six-minute test budget to exercise the approved five-minute token; no token duration is shortened, existing timeout/assertion is increased or retry/bypass enabled. Additional malformed-envelope and unconfirmed-signout recovery cases expand the final scope to eight cases. The standalone proxy script is executed inside this running owned harness and prints only safe pass metadata.

## Boundaries and remaining work

Existing Feature001 historical artifact hashes are unchanged (27 checked). No spec/plan/constitution/approved architecture requirement was altered, and no new schema migration was needed. Feature001 T079 remains pending and T081 provisional; no staff study was attempted. Known external-OIDC/Ticket fixture regressions remain deferred to US5/T091/T111/T093, with historical evidence unchanged; this scoped validation is not a claim that the full old backend/Ticket-browser acceptance suite is green. No signup, Agent CRUD or new Ticket workflow was implemented.

Developer workflow: use the existing dedicated signer references/HTTPS setup, explicitly bootstrap the initial Admin if necessary, then run `./scripts/run-dev.ps1`, open https://localhost:4200/auth/signin, sign in using operator-owned credentials, reload and sign out. Only owned fixture databases/accounts were used for verification. No committed/default Admin password exists. Customer signup and Agent management links/endpoints remain later tasks.

Detailed changed-file inventory: authentication-files.md. T036–T059 and T092/T108–T110 are marked complete; no later story tasks were changed. The obsolete unused secure:false E2E proxy file was removed; E2E shares the tested secure:true development proxy. Final checks found zero owned integration/E2E catalogs, zero owned temporary signing certificates and zero private e2e/dp-test/auth-test directories. No extension hooks are registered. No blocker before T060–T068; previously documented later Ticket integration work remains pending. Changes are uncommitted.
