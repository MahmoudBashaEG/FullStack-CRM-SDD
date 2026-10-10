# T112 — Email acceptance alignment

Date: 2026-10-10. Scope: T112 only. Status: COMPLETE; required final regressions pass and T112 is marked [X].

## Canonical behavior and correction

The approved plan and contracts require a trimmed, syntactically valid single email address, at most 256 UTF-16 units, without control characters. The existing Application validator uses `MailAddress.TryCreate` and exact parsed-address equality; Identity owns NFC/invariant-uppercase normalization and SQL-enforced uniqueness. That backend policy remains unchanged.

The Angular shared regex excluded quoted local-part spaces and embedded `@`, preventing valid account requests before submission. Replace that independent syntax definition with conservative UX checks: surrounding trim, UTF-16 bound, valid Unicode, no C0/C1 controls, and a nonempty local/domain around the last `@`. Preserve internal characters and case. The server remains authoritative for complete syntax: a display-name wrapper or another malformed candidate may pass preliminary UX checks but must fail server validation. No custom Angular email parser, library, authentication redesign, schema change or migration was introduced. Existing forms already use this shared function; no form production changes were needed. Password/name rules and generic server-error presentation are unchanged.

## Test-first observations

- Initial focused Angular run: 6 failed / 29 passed. Both quoted form submissions were blocked; quoted-space and embedded-`@` validator cases failed; C1 controls and malformed Unicode were incorrectly accepted by the prior client check.
- Initial quoted browser run: 2 failed. Customer and Agent forms emitted no creation request; response waits timed out. Real local signin, HTTPS, owned SQL and normal application security were used.
- Backend canonical-policy tests passed before the frontend correction. No artificial backend red result is claimed.
- Four new real SQL/API cases verify ordinary/quoted Customer and Agent creation, surrounding trim and internal-space preservation, normalized duplicate rejection with unchanged safe codes, malformed-address rejection without partial accounts, exactly Customer/Agent roles, no new session or signin cookie, Agent-reference creation, subsequent actual signin and non-Admin creation denial.
- Frontend tests verify common accepted quoted-space, embedded-`@`, quoted-angle and Arabic addresses, controls/Unicode/length/basic invalid checks, and actual Customer/Agent page submission of unchanged quoted email and password. Existing password/name/duplicate/error/role tests remain intact.
- Browser journeys were extracted into `e2e/fixtures/email-account-journeys.ts` with all existing ordinary Customer/Agent assertions retained. `zz-email-acceptance.spec.ts` reuses those complete journeys for quoted emails: explicit signin/reload/signout and Customer denial; Admin-only Agent creation, assignment identity, rename, saved history, disable/re-enable and fresh signin. Extra journeys run after the established suite and wait one real 61-second rate window rather than relaxing production rate limits or bypassing clocks. The beforeAll hook timeout accommodates that explicit wait; product/test assertion timeouts are unchanged.

## Commands and results

All tool commands use `. ./scripts/environment.ps1`. SQL tests use fresh owned `CrmTicketsIntegration_<GUID>` / `CrmTicketsE2E_<GUID>` catalogs on localhost with Integrated Security; existing fixture guards and cleanup remain.

- `dotnet build Crm.slnx --no-restore --verbosity quiet`: PASS, zero warnings/errors.
- `dotnet test tests/backend/Crm.Application.Tests --filter FullyQualifiedName~AccountValidationTests --verbosity quiet`: initial canonical cases PASS, 10 tests; final complete Application suite includes the additional quoted-angle case.
- `dotnet test tests/backend/Crm.IntegrationTests --no-build --filter FullyQualifiedName~EmailAcceptanceTests --verbosity minimal`: PASS, 4/4.
- Final `dotnet test Crm.slnx --no-build --verbosity minimal --logger 'trx;LogFileName=t112-sequential.trx'`: PASS, Domain43 / Application134 / Architecture7 / SQL-API337 = 521; zero failures/skips.
- Final `npm --prefix src/frontend test -- --watch=false`: PASS, 136 tests in 22 files.
- `npm --prefix src/frontend run build`: PASS.
- `dotnet ef migrations has-pending-model-changes --no-build --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api`: PASS, no model changes.
- Final `npm --prefix src/frontend run e2e`: PASS, 39/39, zero failures/skips/retries; 7.3 minutes including real five-minute expiry and 61-second rate-window wait.

A first concurrent broad validation attempt was not green: SQL337 had one existing migration-fixture timeout (336 passed); Angular136 had two timeout failures; browser39 had seven failures after extra early signins reached the unchanged ten-per-minute limiter and a source edit triggered HMR during an assertion. Final Angular and complete backend reruns passed without assertion, timeout, rate-limit or product changes. The browser rerun follows backend completion, with source frozen and additional cases scheduled last. These failures are retained here rather than hidden.

The extraction initially required correcting Playwright callback fixture destructuring (discovery failure, no application defect). The next sequential browser run passed all 37 established cases plus the quoted Customer case, but the final additional Agent signin exhausted the remaining per-IP budget (38 passed / 1 failed). A real rate-window wait in the additional-journey beforeAll hook addresses this fixture scheduling constraint without retrying failed signin or weakening the limiter. Complete rerun result is recorded above when finished.

## Changed files and preservation

Production: `src/frontend/src/app/core/auth/account-validation.ts` only.

Tests: shared `account-validation.spec.ts`; `features/auth/pages/signup.page.spec.ts`; `features/agents/pages/agent-management.spec.ts`; backend `Accounts/AccountValidationTests.cs`; new SQL/API `Accounts/EmailAcceptanceTests.cs`; existing `e2e/customer-authentication.spec.ts` and `e2e/agent-management.spec.ts`; new `e2e/fixtures/email-account-journeys.ts` and `e2e/zz-email-acceptance.spec.ts`.

Tracking/evidence: this file and T112 completion marker only. No other task or prior validation document is rewritten. Spec SHA256 `BC3ED45BA22710D0CE3E39BEDBE6CE130D914E5975C4E05F7623C0A88413126C` and plan SHA256 `F642C31676CD8220757964904674BBDF866BC7046F952CBDC32B91B90DD7AA51` remain unchanged; all 27 Feature001 historical baseline hashes match. Feature001 T079 remains pending and T081 provisional. No convergence command or Git commit was run. Extension hooks are absent.

Final preservation/cleanup checks: `git diff --check` passes; no pending Feature002 task markers remain; no owned `auth-test-`/`e2e-` temporary directories or listeners on4200/7043 remain. Spec/plan and all27 historical hashes were rechecked after completion. No approved-design deviation or remaining T112 blocker. Feature002 is ready for a new `$speckit-converge` review; that review was not run in this implementation step.
