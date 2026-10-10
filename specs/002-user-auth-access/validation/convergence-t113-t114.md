# T113–T114 convergence remediation

Date: 2026-10-10. Scope: T113 and T114 only. Status: COMPLETE; both markers are [X] after final verification.

## Root causes and changes

T113: generation invalidation discarded the pending refresh promise. Memory-state guards rejected stale JWT/identity data, but could not prevent the browser applying the old response's HttpOnly Set-Cookie after a newer signin. `AuthCoordinator.invalidate()` now increments the generation without forgetting the outstanding flight. Existing AuthService mutation ordering drains that flight before signin/signout; refresh callers still join it until settlement. Old callers reject without publishing identity or replaying requests. A fresh explicit recovery can start after settlement. No backend rotation/replay behavior, retries, token storage or cross-tab infrastructure changed.

T114: JavaScript `trim()` retains U+0085 NEL and removes U+FEFF BOM, unlike the authoritative .NET email trimming policy. Shared Angular email normalization trims surrounding Unicode White_Space instead. Internal characters, case, quoted local parts, UTF-16 bounds, Unicode/control checks and backend syntax authority remain intact. Customer and Agent forms already consume the shared helper; passwords/name handling are unchanged. No backend product changes or migration are needed.

## Failing-first evidence

- T113 coordinator/AuthService focused run: 6 failed, 14 passed. Pending transport was forgotten and newer signin/signout started early.
- T113 real-service/interceptor focused run: both new tests failed on early signin after protected non-expiry401 invalidation.
- T113 browser run against actual local refresh rotation: both new cases failed. The held200 response case returned400 for the racing signin; the held503 case detected early signin. Existing production rate limits and real HTTPS/cookies were retained.
- T114 validation/signup/Agent-page focused run: 4 failed, 44 passed. Surrounding NEL was rejected by the helper and both forms; BOM was incorrectly removed.
- Backend whitespace cases characterize the already-correct canonical policy; no fabricated backend failure is claimed.

## Regression coverage

Coordinator success/failure draining and no overlapping flight; AuthService late200/503/network results, all waiters, signout and explicit recovery; actual interceptor expired401 followed by non-expiry401, no stale replay and new Customer identity. The browser holds an actual refresh response while signin is requested, verifies response/request ordering, current identity, real reload restoration, Secure HttpOnly cookie, memory-only state and signout. Response delivery is observed with Playwright's response event rather than the later route-fulfillment promise resolution.

Cross-layer tests cover surrounding NEL and common Unicode whitespace, preserved quoted/internal characters and internal-control rejection. SQL/API cases retain normalized duplicate, fixed-role, no auto-login, no partial account and authorization assertions. Existing quoted Customer/Agent browser journeys now submit NEL-wrapped addresses and retain their complete signin/reload/role/assignment/history/disable/re-enable assertions. Real 61-second rate-window waits schedule additional journeys without weakening the production limiter.

## Commands and results

Commands source `scripts/environment.ps1`. SQL uses fresh exclusively owned `CrmTicketsIntegration_<GUID>` / `CrmTicketsE2E_<GUID>` catalogs on localhost with Integrated Security and fixture cleanup, never the persistent developer database.

- `dotnet build Crm.slnx --no-restore --verbosity quiet`: PASS, zero warnings/errors.
- `dotnet test Crm.slnx --no-build --verbosity minimal --logger 'trx;LogFileName=t113-t114.trx'`: PASS, Domain43 / Application141 / Architecture7 / SQL-API341 = 532; zero failures/skips.
- Final `npm --prefix src/frontend test -- --watch=false`: PASS, 157 tests in23 files.
- `npm --prefix src/frontend run build`: PASS.
- `dotnet ef migrations has-pending-model-changes --no-build --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api`: PASS, no model changes.
- Focused `npm --prefix src/frontend run e2e -- zz-email-acceptance.spec.ts zzz-refresh-generation.spec.ts`: 3 passed / 1 failed. Both account journeys and the late503 race passed; late200 signin returned400. The new race fixture was adjusted to use the established exact-password input approach and explicitly verify submitted email/password equality without exposing credential values. No product requirement/assertion was weakened.
- Focused rerun `npm --prefix src/frontend run e2e -- zzz-refresh-generation.spec.ts`: PASS, 2/2, zero failures/skips/retries;1.2 minutes.
- First complete browser run: 40 passed / 1 failed, zero retries. All existing account/Ticket cases and the late503 race passed. The late200 fixture's exact-input assertion exposed a missing submitted email, explaining its400 response. SPA navigation can expose the input before Angular NgModel's deferred registration completes. The fixture now awaits the signin URL/heading and completed render before filling, asserts the rendered value, and still holds the old refresh throughout. Product code, security policies, assertions and timeouts are unchanged by this fixture correction.
- Focused rerun after form-readiness correction: PASS,2/2.
- Final complete `npm --prefix src/frontend run e2e`: PASS,41/41, zero failures/skips/retries;8.3 minutes, including real five-minute expiry and rate-window waits. Both held200/503 cases pass with exact-input, transport-order, identity and reload assertions; all established authentication/Ticket regressions pass.

## Changed files and preservation

Production: `src/frontend/src/app/core/auth/auth-coordinator.ts`, `src/frontend/src/app/core/auth/account-validation.ts`.

Tests: coordinator/AuthService/shared validation specs; new `core/http/auth-generation.spec.ts`; signup/Agent page specs; backend AccountValidationTests/EmailAcceptanceTests; email-account-journeys fixture/zz-email-acceptance browser spec; new zzz-refresh-generation browser spec.

Tracking: T113/T114 markers and this new evidence only. Existing uncommitted work is preserved. Specification, approved planning and historical validation are not rewritten. Feature001 T079 remains pending and T081 provisional. No convergence command or commit is run in this step.

Spec SHA256 `BC3ED45BA22710D0CE3E39BEDBE6CE130D914E5975C4E05F7623C0A88413126C` and plan SHA256 `F642C31676CD8220757964904674BBDF866BC7046F952CBDC32B91B90DD7AA51` match the pre-run values. All27 Feature001 baseline hashes match. Reversing only the two completion markers reproduces the pre-run tasks SHA256 `1556B27AD235B7A75A372CF1E301CFBA291B8EC11E323C20124A1460D8AA7509`, proving no unrelated task definitions/status were changed. No migration, approved-design deviation or remaining T113/T114 blocker. Feature002 is ready for another `$speckit-converge` review; that review is not executed here.

Final cleanup checks pass: `git diff --check`; no owned e2e/auth-test temporary directories or listeners on4200/7043 remain. No pending Feature002 task markers remain. `.specify/extensions.yml` is absent, so no pre/post implementation extension hooks are registered.
