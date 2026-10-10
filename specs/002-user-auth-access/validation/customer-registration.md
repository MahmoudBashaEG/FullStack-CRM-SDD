# Customer registration checkpoint

Date: 2026-10-09. Scope: Feature 002 US3, T060–T068 only. The repository was clean at start, based on commit `1b4ccf6`. Completed authentication/bootstrap foundations were reused. No Agent-management, Ticket-integration or final-verification implementation was pulled forward.

## Delivered behavior

`POST /api/auth/signup` accepts only displayName/email/password. Application validation uses the existing shared name/email/password rules; passwords remain exact strings, including spaces and supplementary Unicode. The registration adapter uses standard Identity UserManager and the existing SQL context/transaction. A server-created Guid, active state, Customer role and initial authentication version are assigned together. No Customer business profile, AgentReference, bootstrap marker, authentication session or refresh credential is created.

Successful registration returns only HTTP 201 and `{message:"Registration complete. Sign in to continue."}`. It does not authenticate, issue JWTs, set a refresh cookie, or replace an already signed-in session. CSRF/Origin checks, authorization-header/query rejection, strict JSON binding and the existing five-attempt/minute signup limiter apply. Safe format errors use `validation_failed`; normalized-email conflicts and account-sensitive Identity rejections use generic `registration_failed` with "Registration could not be completed." No Identity details or supplied credential values are exposed.

The `/auth/signup` page, linked from signin, uses the shared validators and security transport through feature-owned SignupService. It contains no role selector, preserves the exact password, clears submitted passwords, prevents duplicate in-flight submissions and navigates to `/auth/signin` after creation. Direction-aware labels/errors and responsive layout reuse the established Angular approach. Signup neither publishes an access JWT nor changes existing authenticated state.

## Test-first observations

- Application: 14 tests failed against a throwing registration handler stub; after implementation all 14 passed. Added a further malformed-surrogate/overlong-name regression during review.
- Real SQL/API: 18 initial tests failed against the absent public signup endpoint/registration foundation. These were rerun with a guarded Windows-integrated fixture connection; the first attempt without that required fixture configuration was not behavioral evidence. After implementation all 18 passed. Additional tests cover preservation of an existing signed-in session, the actual signup rate limit, and staff-role rejection at the registration port.
- Angular signup page: seven tests failed against the throwing page stub before implementation. The initial sandbox compiler failure and a test mock typing correction were resolved before recording behavioral failures.
- Browser: the real owned SQL/HTTPS harness failed because `/auth/signup` had no signup heading/page. This was observed before implementing the page; no shortened token lifetime, authentication injection or TLS bypass was used.

## Persistence, concurrency and security checks

Concurrent case/whitespace-equivalent registrations must yield exactly one 201 and one generic 400, with one normalized identity. Attempts against existing Customer, Agent and Admin emails preserve the existing account serialization and create no session/reference. The authoritative unique SQL indexes remain intact; no retry or promotion/update path is introduced.

A SaveChanges interceptor throws or cancels after the Identity insert has been saved within the transaction. Separate SQL reads prove the account and related collections remain empty; a clean subsequent registration succeeds. Supported persistence operations remain standard Identity; framework calls lacking cancellation overloads are bounded by checks before/after, and EF transaction calls propagate cancellation. Uncertain commits are not automatically replayed.

SQL/API cases cover 14/15/128/129 scalar passwords using both all spaces and supplementary Unicode. Accepted passwords verify through standard UserManager without trimming/truncation; rejected cases create nothing. Unknown role (including Customer), userId, active and actor inputs fail without an account. Missing CSRF, untrusted Origin, supplied bearer and credential query inputs are rejected. Captured logs and success/error JSON are checked for password/hash disclosure.

## Verification results

Commands run from the repository root after `. ./scripts/environment.ps1`:

| Command | Result |
|---|---|
| `dotnet build Crm.slnx --no-restore --verbosity quiet` | PASS, zero warnings/errors |
| `dotnet build tests/scripts/fixtures/DevFixture --no-restore --verbosity quiet` | PASS, zero warnings/errors |
| `dotnet test tests/backend/Crm.Domain.Tests --no-build --verbosity quiet` | PASS, 43 |
| `dotnet test tests/backend/Crm.Application.Tests --no-restore --verbosity quiet` | PASS, 115, including 15 registration cases |
| `dotnet test tests/backend/Crm.ArchitectureTests --no-build --verbosity quiet` | PASS, 7; unchanged layer/controller assertions |
| `npm --prefix src/frontend test -- --watch=false` | PASS, 107 tests / 20 files |
| `npm --prefix src/frontend run build` | PASS, production build |
| `dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build` | PASS, no model changes |

Real SQL regression command:

```powershell
$env:CRM_TEST_SQL_CONNECTION='Server=localhost;Database=CrmTicketsIntegration_'+[guid]::NewGuid().ToString('N')+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test tests/backend/Crm.IntegrationTests --no-restore --filter 'FullyQualifiedName~Crm.IntegrationTests.Identity|FullyQualifiedName~Crm.IntegrationTests.Authentication|FullyQualifiedName~LocalJwtTests|FullyQualifiedName~LocalAuthorizationFoundationTests|FullyQualifiedName~AuthConfidentialityTests|FullyQualifiedName~DataProtectionPersistenceTests|FullyQualifiedName~CustomerSignupTests' --verbosity quiet
```

PASS: 109 tests, including 22 Customer signup cases; existing Identity/bootstrap, local JWT, session rotation/replay/revocation, privacy, authorization and Data Protection restart assertions retained. Owned fixture catalogs use the existing production migrations; no fixture schema bypass.

Browser command:

```powershell
$env:CRM_E2E_SQL_CONNECTION='Server=localhost;Database=CrmTicketsE2E_'+[guid]::NewGuid().ToString('N')+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
npm --prefix src/frontend run e2e -- customer-authentication.spec.ts authentication.spec.ts
```

PASS: all nine tests in 5.4 minutes, including the new signup flow and all eight pre-existing authentication cases. The original five-minute expiry test passed without shortening the JWT lifetime. Strict HTTPS proxy verification also passed (Origin, Bearer, Secure HttpOnly cookies, rotation and signout). Newly registered Customer signup stays signed out, explicit signin returns Customer through `/me`, direct support-reference API access is 403, the Ticket route redirects to account access, reload restores authentication, and signout makes the earlier JWT fail 401.

This is explicit scoped verification, not a silent reduction of the later full Ticket suite or completion of T067's direct Admin-management gate. The owned runner completed its normal database/certificate/temp-folder cleanup. Post-run checks find no listeners on 4200/7043, no owned browser/foundation certificates and no owned e2e/auth-test/restart directories. A read-only Windows-authenticated `sqlcmd` query against sys.databases confirms zero remaining CrmTicketsIntegration_/CrmTicketsE2E_ fixture catalogs. The final solution rebuild also passes with zero warnings/errors.

Historical manifest verification: all 27 Feature 001 artifacts have unchanged SHA-256 hashes. `git diff --check` passes. No extension hook file is present; ignore rules for generated output/secrets remain intact. Spec, plan, research, data model, contracts and architecture guidance are unchanged.

No migration was generated: this uses the already-versioned LocalCrmAuthentication schema and existing unique normalized indexes. EF model verification reports no pending changes. All SQL validation uses fresh owned catalogs and the same local Windows identity, never the developer's persistent CRM catalog.

## Requirement trace and remaining gates

| Requirement | Checkpoint evidence | Remaining integrated evidence |
|---|---|---|
| FR-001 / FR-002 | Shared local account, fixed Customer creation, public role/authority rejection | Full integrated endpoint matrix |
| FR-003 | Existing account preservation; no staff reference or role promotion | Agent creation story |
| FR-004 | Newly registered Customer explicitly signs in through existing real authentication | Complete role integration |
| FR-005 | Generic sensitive failures, safe field errors, credential-free response/log assertions | Final feature-wide privacy suite |
| FR-006 | Standard password hashing and exact 15–128 scalar boundaries | Agent creation path |
| FR-009 / FR-016 | Existing support policy/route denial, shared AuthService and same-origin HTTPS transport | Direct implemented Admin Agent-management endpoint denial |
| SC-001–003 | Signup/explicit signin/no escalation and current Customer support denial | T094/T067 final integrated Ticket/Admin browser proof |

T067 depends on uncompleted T094, and T068 depends on T067. Both remain unchecked: a scoped signup browser pass is not the final integrated acceptance gate. Agent-management endpoints are not implemented in this run; an unmatched route's 404 is not reported as proof of Admin authorization. The existing AdminAgents policy tests remain part of the backend regression selection. Complete real direct Admin-operation denial after US4/US5, without claiming it now.

Known legacy OIDC/Ticket migration/provisioning regressions remain assigned to US5. Their assertions were not changed or suppressed; the full obsolete harness was not run as a substitute for current local-auth acceptance. Feature 001 historical evidence, pending T079/SC-005 and provisional T081 are retained. No participants or usability evidence were created. This US3 integration gate does not block the explicitly independent T069–T085 Agent-management branch.

## Integrated completion — 2026-10-10

This later result completes the previously pending T067–T068 gate without rewriting the earlier checkpoint observations. T094's real SQL/API and Development proxy/launcher verification established the actual Ticket and Agent-management endpoints first.

The complete33-case Playwright suite passed (6.2m, one worker, zero retries), including public Customer signup with no automatic login, explicit signin with Customer identity, reload restoration and signout revocation. The actual implemented Ticket/reference and Agent-management GET/POST endpoints return403 for the registered Customer; Ticket/Agents routes redirect to access and privileged navigation is absent. This is a role-boundary result, not an unmatched-route404. No Customer Portal behavior was introduced.

The full SQL/API suite passed328/328, zero skips. Domain43/Application130/Architecture7, Angular120/22 files, backend/frontend builds and EF model consistency also passed. See [Ticket integration](ticket-integration.md) for commands, corrected legacy assumptions and preservation evidence. FR-001–006/009/016 and SC-001–003 now have their intended integrated signup/signin/no-escalation/role-boundary evidence. Unrelated final-verification work remains separate; Feature001 human evaluation is still pending.
