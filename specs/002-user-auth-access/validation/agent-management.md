# Agent Management checkpoint

Date: 2026-10-09. Scope: Feature 002 US4, T069–T085. Starting commit `01cfef4`; working tree was clean. Existing completed authentication, bootstrap, signup, SQL, Angular and Playwright infrastructure reused. No business specification or approved design artifact was modified.

## Completed scope and deferred gates

T069–T083 are completed. T084 requires T094 Ticket integration, and T085 requires T084; both remain unchecked. T067–T068 also remain pending T094 as explicitly requested. A scoped browser workflow pass does not claim the later full Ticket/assignment/history integration gate. T086+ implementation and historical Feature 001 evidence were not changed. No blocker prevents proceeding to the separate T086–T097 integration phase.

## API, application and persistence

Shared AdminAgents authorization protects every supported route before binding/resource disclosure: visitor401, live Customer/Agent403, Admin permitted. Application Commands/Queries additionally use a trusted AccountIdentity supplied from the validated per-request authentication snapshot, with centralized enum-based Admin authorization. Controllers retain ISender-only constructors and dispatch through MediatR; no EF/Identity/business persistence calls occur in controllers.

| Route | Behavior |
|---|---|
| GET /api/agents | Agent-only active/disabled list; page>=1, pageSize1–100, defaults1/25; displayName then SQL UserId ordering; no extra/duplicate query keys |
| POST /api/agents | Exact displayName/email/password; creates active Agent plus new linked assignment reference atomically;201 safe DTO, Location detail URL |
| GET /api/agents/{userId} | Safe Agent DTO; canonical Guid form; missing/non-Agent404 |
| PATCH /api/agents/{userId} | Display name and required eight-byte Base64 rowversion only; current user/reference labels change together |
| PUT /api/agents/{userId}/availability | Required boolean and version; atomic disable/re-enable behavior |

DTO exposes only userId/displayName/email/Agent role/isActive/assignmentReferenceId/version. No password/hash/stamp/session/credential data or automatic Agent signin. Strict binding rejects authority fields and unsupported email/password/role updates. DELETE is absent and returns405. Customer/Admin identities cannot be managed or promoted through this surface. Duplicate normalized email gives safe409 agent_conflict; malformed input400; stale writes409 account_changed. No unsupported account administration, public Agent signup, Customer profile or portal was added.

The shared Identity validators/normalizer/hash infrastructure enforces exact 15–128 Unicode scalars, spaces/supplementary Unicode, no mixture/trim/truncation, trimmed valid names and normalized unique emails. SQL tests cover fourteen/fifteen/128/129 using both all spaces and supplementary Unicode. Framework calls without cancellation overloads have checks before/after; EF I/O receives the token.

Account creation owns one EF transaction around UserManager's save and the new reference save. Reference ID is a new Guid distinct from UserId and legacy references; LocalUserId links uniquely to the local Agent with namespace urn:crm:local:user and canonical Guid subject. There is no name/email/subject reconciliation. Admin/Customer creation does not create Agent references.

Writer lock order is account → sessions (disable) → assignment reference. Rowversion is checked under the account lock before no-op decisions. Valid no-op preserves version; stale no-op still conflicts. Rename preserves email/password/role/UserId/reference IDs and immutable history. Disable advances AuthVersion/SecurityStamp, revokes all non-revoked families and removes eligibility in the same transaction. Re-enable restores active/eligible state only; versions/revocations remain advanced and fresh signin is required. No uncertain-commit automatic retry or DB credential redesign.

## Angular behavior

Agent models, relative-/api data access, routing and list/create/edit pages live in features/agents. They reuse the shared bearer interceptor/AuthService and common input validators. Shared Admin guard and navigation predicate expose /agents to Admin only; direct server authorization remains authoritative. Customer/Agent/visitor navigation is denied without feature-owned auth or storage.

The list includes safe name/email/state fields and pagination. Creation clears submitted passwords and reports safe errors. Edit exposes name and availability only; signin email is read-only, no password/role/delete controls. A409 preserves name/availability drafts, blocks automatic overwrite/retry and offers explicit reload/review. Reload updates server DTO/version while retaining the draft. Accessible labels, direction-aware Unicode presentation and responsive layout retain project constraints. Async list/detail results are guarded against obsolete route/request completions.

## Test-first observations and corrections

- Application: all14 new AgentManagementTests failed against throwing handlers before implementation; all14 subsequently passed.
- Real SQL/API: initial23 cases produced22 failures and one pre-existing duplicate-query rejection pass. Failures reflected throwing stores/handlers and absent Agent endpoints, not an omitted SQL fixture. No SQL skips or bypass scheme were used.
- Angular: all9 Agent-management page cases failed against throwing page stubs before implementation; subsequently passed.
- Browser: the real Admin signin succeeded, but the new workflow failed awaiting the absent Agents navigation before UI implementation. No token injection or TLS bypass.
- The first green SQL/API attempt passed22/23 and exposed an EF translation failure on detail lookup. Corrected filtering/ordering/paging to remain on a translatable member projection, with DTO/Base64 construction after materialization. The original detail assertion stayed unchanged; later full suites passed.
- Strengthened rollback injection to throw/cancel after an actual reference SaveChanges, within the owning transaction, proving persisted writes roll back. Added SQL ordering/large-page, unexpired JWT, matching-legacy-name/history and Customer/Admin non-promotion checks.
- Existing app signout test double gained the new navigation predicate; its assertions were preserved. No production authentication lifetime/replay/CSRF behavior or historical assertion was weakened.

## Executed validation

Commands run from the repository root after `. ./scripts/environment.ps1`:

| Command | Result |
|---|---|
| dotnet build Crm.slnx --no-restore --verbosity quiet | PASS, zero warnings/errors |
| dotnet build tests/scripts/fixtures/DevFixture --no-restore --verbosity quiet | PASS, zero warnings/errors |
| dotnet test tests/backend/Crm.Domain.Tests --no-build --verbosity quiet | PASS43 |
| dotnet test tests/backend/Crm.Application.Tests --no-build --verbosity quiet | PASS129, including14 Agent use-case tests |
| dotnet test tests/backend/Crm.ArchitectureTests --no-build --verbosity quiet | PASS7, unchanged layer/controller rules |
| npm --prefix src/frontend test -- --watch=false | PASS118 /22 files |
| npm --prefix src/frontend run build | PASS production build |
| dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build | PASS, no pending model changes |

```powershell
$env:CRM_TEST_SQL_CONNECTION='Server=localhost;Database=CrmTicketsIntegration_'+[guid]::NewGuid().ToString('N')+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
dotnet test tests/backend/Crm.IntegrationTests --no-restore --filter 'FullyQualifiedName~Crm.IntegrationTests.Identity|FullyQualifiedName~Crm.IntegrationTests.Authentication|FullyQualifiedName~LocalJwtTests|FullyQualifiedName~LocalAuthorizationFoundationTests|FullyQualifiedName~AuthConfidentialityTests|FullyQualifiedName~DataProtectionPersistenceTests|FullyQualifiedName~Crm.IntegrationTests.Accounts' --verbosity quiet
```

PASS133 tests, including24 initial Agent SQL/API cases and existing109 Identity/bootstrap/signup/JWT/session/authorization/Data Protection checks. Two further tests were added during final review and independently executed using fresh guarded connections with the same command/filter mechanism: `FullyQualifiedName~Matching_legacy_name_never_links` PASS1 and `FullyQualifiedName~Existing_Customer_or_Admin_cannot_be_promoted` PASS1. Thus135 distinct SQL/API cases passed across the regression run and targeted additions; no claim of a single135-case invocation.

Observed proofs: concurrent normalized creation commits one Agent/reference and one conflict; concurrent Admin updates using one version yield one winner; failure/cancellation after saved writes rolls back account/reference/families; exact role and password boundaries; deterministic active/disabled pagination; safe response/log capture; no authority overrides; non-Agent accounts unchanged. Both previously live Agent JWTs are checked unexpired at the time they fail after disable; all families fail refresh/signin, re-enable never revives them, and fresh signin succeeds. Concurrent disable vs signin/refresh leaves no usable session. Both legacy assignments and saved local-Agent actor/assignment snapshots retain their serialized values and stable IDs across rename/disable/re-enable.

```powershell
$env:CRM_E2E_SQL_CONNECTION='Server=localhost;Database=CrmTicketsE2E_'+[guid]::NewGuid().ToString('N')+';Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
npm --prefix src/frontend run e2e -- agent-management.spec.ts customer-authentication.spec.ts authentication.spec.ts
```

PASS10 browser tests /5.4 minutes: scoped Admin Agent workflow, Customer signup and all eight prior authentication cases. Existing real five-minute expiry/coalescing and strict HTTPS proxy verification pass unchanged. The new workflow uses actual operator-bootstrap Admin signin, creates an Agent through UI/API, renames it, denies Agent Admin access/navigation, disables it from a separate Admin context, observes immediate old JWT401 and reload/refresh failure, re-enables without reviving old JWT, preserves reference ID and requires successful fresh signin. This is available US4 proof, not completion of T084's saved Ticket/history integration checkpoint or T085's final integrated SC-005 claim.

No schema migration required; existing compiled migrations apply only to isolated fixture catalogs. Developer persistent data/configuration and signing/TLS setup are unchanged. No secrets committed. Historical baseline hashes for27 Feature 001 artifacts are unchanged. Ignore files were verified; no extension hooks exist.

Post-run cleanup checks: no listeners4200/7043, no owned e2e/auth-test/restart directories and no owned browser/foundation certificates. Read-only Windows-authenticated sqlcmd inspection of sys.databases confirms zero remaining CrmTicketsIntegration_/CrmTicketsE2E_ fixture catalogs. Final solution build has zero warnings/errors; git diff --check passes.

## Scope and remaining work

No approved design deviation. Final integrated gates T084/T085 and Customer T067/T068 intentionally wait for T094. Existing full Ticket/OIDC fixture/provisioner/current-actor regressions remain assigned to US5; their assertions/evidence were not rewritten or suppressed. Feature 001 T079/SC-005 remains pending and T081 provisional; no participants, observations or usability results were fabricated. The next authorized implementation phase is T086–T097; final integrated Agent/Customer proofs must then be executed before claiming full feature acceptance.

## Integrated completion — 2026-10-10

This later result completes T084–T085 after T094, preserving the earlier US4 checkpoint observations. The complete33-case local-auth Playwright suite passed (6.2m, one worker, zero retries). A subsequent targeted rerun of the same Agent workflow passed1/1 after adding an explicit401 assertion for the disabled browser's actual refresh response.

The real CLI-established Admin signs in, creates and renames an Agent through the application, and that Agent signs in through the normal UI. The Agent creates a Ticket, is assignment eligible, assigns itself and emits its stable local UserId into readable history. Admin disablement rejects the still-unexpired old JWT on the next Ticket request and rejects refresh401; choices exclude the Agent. The saved assignment remains readable and inactive, an authorized Admin's unrelated edit succeeds, and the earlier actor/assignment history prefix is byte-for-value unchanged. Re-enable restores eligibility with the same assignment ID but old authentication remains invalid; a fresh Agent signin reads the saved Ticket successfully. Backend tests additionally verify every revoked refresh family, subsequent signin denial, atomic account/session/reference updates and concurrency/rollback.

The complete SQL/API suite passed328/328 with zero skips. Domain43/Application130/Architecture7, Angular120/22 files, backend/frontend builds and EF model consistency passed. See [Ticket integration](ticket-integration.md) for full commands/results. This completes Feature002 SC-005's repeatable scripted Admin-management sequence and the integrated US1 Admin availability/US4 role boundary; it is independent of Feature001 SC-005's pending real-support-staff usability study. No human participants or usability results were invented. Final-verification tasks remain unimplemented.
