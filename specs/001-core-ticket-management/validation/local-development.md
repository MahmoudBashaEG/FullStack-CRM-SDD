# One-command local Development validation — T090–T096

Validated 2026-10-09, Africa/Cairo. Scope: newly added launcher tasks only. Existing
T001–T089 implementation and evidence were retained. No business specification, domain
model, migration, HTTP contract or constitutional requirement changed. T079/SC-005 remains
pending and unexecuted; T081 remains provisional. No participant evidence was fabricated.

## Command and runtime behavior

From a configured repository-root PowerShell 7 terminal:

```powershell
./scripts/run-dev.ps1
```

Frontend: https://localhost:4200. Backend: https://localhost:7043.
The actual integration/manual terminal runs invoked that same command through a child
PowerShell command `& ./scripts/run-dev.ps1`; both real services and their logs were observed.

The launcher reuses environment.ps1, starts dotnet watch and existing npm start concurrently,
and explicitly selects Development/no launch profile and normal Angular development serve.
The relative frontend /api configuration remains unchanged. Development-only
proxy.development.json forwards /api/** unchanged to 7043 with secure:true. Existing Testing
proxy/auth/explicit migration preparation and the production bundle remain unchanged.

Both stdout and stderr stay live in the caller console. A small scripts/DevProcess.cs helper
assigns each suspended child to a Windows job before it can spawn descendants. Owned console
groups receive CTRL_BREAK on shutdown; one shared five-second grace budget precedes forced
job termination where necessary. Job ownership covers root-first exit and avoids killing
unrelated dotnet/node processes. Root handles are retained to avoid signaling recycled IDs.
Caller location and all modified environment variables are restored, including null/unset
versus empty values. Native npm CLI execution avoids an extra launcher-owned cmd wrapper;
npm's own script descendants remain inside its job.

## Certificate setup and approval

The existing valid, trusted Windows HTTPS development certificate was reused. Angular receives
explicit PEM certificate/key paths rather than generating a separate untrusted certificate.
Default paths are ignored .local/dev-certs/localhost.pem and localhost.key; optional
CRM_DEV_SSL_CERT/CRM_DEV_SSL_KEY supply existing alternatives. Child NODE_EXTRA_CA_CERTS
defaults to the public certificate unless already configured. No global TLS bypass or
certificate-store trust change was made.

Automatic approval review initially rejected unencrypted private-key export as not expressly
authorized. The user then explicitly approved export into that ignored directory restricted
to the existing Windows account. Only after approval were the directory ACL and existing
certificate export performed. The private key was never printed, bundled or committed.
The launcher itself only checks/uses existing certificate files, never exports or generates them.

## Test-first outcomes and corrections

Before product launcher/proxy implementation, run-dev.tests.ps1 failed with “Missing root
launcher,” and dev-proxy.tests.mjs failed with ENOENT for proxy.development.json. No missing
artifact was treated as success or skipped. Subsequent behavior tests exposed these issues:

- Multiple executable matches from Get-Command were incorrectly combined; select the first
  resolved executable. Paths containing spaces are exercised by isolated native tool stand-ins.
- The proposed `dotnet watch ... run -- --no-launch-profile` syntax loaded the generated
  launch profile and bound HTTP 5277. Actual SDK verification corrected it to
  `dotnet watch ... run --no-launch-profile`. Only that syntax example was corrected in plan.md
  and the new T092 definition; intended behavior and historical tasks were not rewritten.
- First interactive Angular startup waited for telemetry consent. Child-only
  NG_CLI_ANALYTICS=false removes the prompt and is restored on exit.
- Restoring an originally absent environment variable through the PowerShell/.NET overload
  produced an empty value. Explicit Env-provider removal now preserves unset state; the
  exact comparison assertion remained unchanged and passes.
- Test fixture corrections included discovering portable PowerShell rather than assuming
  Program Files, isolating generated Ctrl+C from the outer test runner, proper bearer-property
  interpolation and Windows file-URL module imports. One overlapping fixed-port test attempt
  correctly refused the occupied port; subsequent runs were serialized. An attempted fixture
  rebuild while its issuer executable was active failed with a file lock and was rerun only
  after owned cleanup. These were recorded failures, not bypasses.

The quota interruption prevented an automatic approval review from completing; that command
was not executed. The user requested continuation, and the final checks ran through normal
approval review after resumption. No sandbox/approval workaround was used.

## Executed verification

| Check | Final result |
| --- | --- |
| pwsh -NoProfile -File tests/scripts/run-dev.tests.ps1 | PASS: 14 scenarios — three ownership cases, ten console/startup cases and exact in-process restoration |
| Native cooperative ownership | Graceful exit, 79 ms, zero remaining owned descendants |
| Native uncooperative ownership | Forced cleanup after 5,052 ms; zero remaining descendants; unrelated control survives |
| Native root-first orphan | Forced cleanup after 5,045 ms; zero remaining descendants; unrelated control survives |
| Console cases | Two clean starts/Ctrl+C stops; root-first, occupied port, forbidden test auth, missing dependency/certificate/tool, partial launch and actual 120-second timeout all pass |
| pwsh -NoProfile -File tests/scripts/run-dev.integration.ps1 | PASS: real root launcher, fresh owned SQL, normal JWT/proxy journey, actual watch restart, normal browser/live reload, repeated start and negative TLS |
| Same integration command with -InitialSchema | PASS: explicit fixture preparation stops at InitialTicketStorage, launcher applies only pending search migration, then all checks pass |
| node tests/scripts/dev-proxy.tests.mjs | PASS in integrated actual-server runs; configuration, nested routing and /api-docs exclusion |
| Actual proxy negative trust | With only Angular child's CA trust removed, backend certificate is rejected and proxy returns 5xx; no insecure fallback |
| dotnet build Crm.slnx --no-restore | PASS; zero warnings/errors |
| dotnet test Crm.slnx --no-build --logger 'console;verbosity=minimal' | PASS; 304 tests, zero failures/skips: Domain 33, Application 78, Architecture 6, Integration 187 |
| npm test --prefix src/frontend -- --watch=false | PASS; 71 tests in 11 files |
| npm run build --prefix src/frontend | PASS; production build |
| npm run e2e --prefix src/frontend | PASS; 19 tests, 46.2 seconds, one worker, zero retries |
| EF migrations has-pending-model-changes, Infrastructure/API, --no-build | PASS; no pending model changes |
| Source/migration/spec checks | Migration/snapshot, business spec, constitution, Testing runner/configuration, and temporary source/project probes unchanged |
| Certificate Git ignore checks | Both local PEM/key paths ignored |

Fixed-port tests ran sequentially; backend/unit/production-build checks that do not bind those
ports could run independently. No assertion, timeout, auth validation or TLS safeguard was weakened.
The initial startup timeout remained 120 seconds; shutdown grace remained five seconds.

## Migrations and identity

Every database operation used the inherited Windows identity and the same validated local
Integrated Security ConnectionStrings:Crm within each run. Only exclusively owned disposable
catalogs were used; the developer's CustomerSupportCrm catalog was not migrated/reset for tests.
No SQL principals, passwords, grants or alternate runtime/migration credentials were introduced.

Fresh Development API startup applied 20261008164804_InitialTicketStorage and
20261008224935_LongTicketSubjectSearch before readiness, producing the protected fallback
and dbo.TicketSubjectContainsLiteral. The older-schema fixture deliberately pre-applied only
InitialTicketStorage; normal launcher startup applied only LongTicketSubjectSearch.
Repeated launch and actual dotnet-watch restart preserved migration IDs, saved ticket and
history counts. A temporary project edit was proven to change the actual API listener PID,
not merely touch a source timestamp; exact project bytes were restored. Existing T085/T086
regressions continue to prove model drift/failure/cancellation/disposal, no re-executed DDL,
no generation/EnsureCreated and no non-Development startup migration.

The launcher never calls EF update/generation, EnsureCreated or reference provisioning.
Explicit synthetic reference setup for assignment was performed by the integration test
operator after migration startup, using the existing bounded CLI outside the launcher.

## Authentication and frontend observations

A private external HTTPS discovery/JWKS fixture issued signed synthetic tokens directly to
the HTTP test client. The actual Development API used normal Authority/Audience and normal
issuer/audience/signature/lifetime middleware; no Authentication__Test* settings or product
auth bypass were used. Through the real Angular /api proxy, expired/wrong-audience tokens
received 401, denied queue access received 403 and authorized categories returned 200/fallback.
Create/view/edit/assign/resolve/close/reopen and all history kinds succeeded through frontend-origin
HTTP calls, including preserved assignment/reference on reopening and exact query forwarding.

The normal browser separately loaded trusted HTTPS, routed unauthenticated ticket access to
the Support queue access/Sign in page and observed Angular live reload after a temporary
template edit and its exact restoration. No fixture tokens were injected into SPA storage,
and the normal frontend auth adapter was not replaced. Existing Testing Playwright journeys
remain separate evidence. These checks do not simulate or claim real authorization-code/PKCE
sign-in, an external registered client or actual support-staff usability.

Console terminal runs observed both real server logs and graceful API/watch, Angular/npm and
issuer shutdown. Non-console automation also exercised the explicitly labeled five-second
forced cleanup where console signaling was unavailable. No graceful result is claimed for
that fallback or abrupt terminal closure.

## Files changed in this implementation checkpoint

Created:

- scripts/run-dev.ps1 and scripts/DevProcess.cs.
- src/frontend/proxy.development.json.
- tests/scripts/run-dev.tests.ps1, dev-proxy.tests.mjs and run-dev.integration.ps1.
- tests/scripts/fixtures/DevFixture/DevFixture.csproj, Program.cs and generated packages.lock.json (no package dependencies).
- tests/scripts/fixtures/proxy-observations.mjs and browser-observations.mjs.
- This validation record.

Modified:

- src/frontend/angular.json: normal development serve proxy only.
- docs/development/core-ticket-management.md and quickstart.md: verified command/setup/reproduction.
- plan.md: corrected watch argument forwarding syntax only during this implementation.
- tasks.md: corrected new T092 syntax and completed T090–T096 markers only.

Pre-existing planning and T084–T089 uncommitted files were preserved. No unrelated ticket
implementation was redone. All six constitutional gates remain satisfied: source fidelity,
feature scope, inward backend dependencies, feature-based frontend ownership, automated quality/
versioned migrations/no committed secrets, and existing platform compatibility.

Final cleanup probes found zero disposable catalogs, zero listeners on 4200/7043/7443, zero
DevFixture processes and zero disposable dev-integration/dev-tests/e2e directories. The approved
private local development certificate export intentionally remains for future normal startup.
No pre/post hooks are configured. Changes are uncommitted.

T090–T096 are complete. Real external OIDC browser sign-in remains a documented setup/manual
interoperability dependency because no registered client/accounts were supplied. T079/SC-005
was not attempted and T081 remains provisional; no full human acceptance is claimed.
