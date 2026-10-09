# Foundation validation — T001–T027

Validated 2026-10-09, Africa/Cairo. Feature002 foundation only. No bootstrap, public signin/refresh/signout/me/signup endpoints, authentication UI, Agent-management handlers/UI, cross-tab infrastructure or later Ticket integration was implemented. Only explicitly included T020/T021/T023/T024 authentication/reference prerequisites touch existing integration. All other story tasks remain pending.

## Baseline and test-first evidence

- Original backend build: PASS, zero warnings/errors.
- Original backend suite: 304 PASS (Domain33, Application78, Architecture6, Integration187), no skips; SQL fixture took1m48s.
- Original Angular suite:71 PASS/11files. Initial sandbox compiler run failed with filesystem access denial; approved normal-access rerun passed. No assertion/config workaround.
- Identity10.0.12 package restored; Jwt8.19.2 was made a direct Infrastructure dependency matching the version already resolved by API JwtBearer. No stack upgrade.
- Domain scalar/rule tests compiled against failing contract stubs before implementation: four expected failures for accepted boundaries/Unicode/name behavior. Application email test: one expected failure. Frontend scalar tests: four failed/two passed before implementation.
- Schema/JWT tests: eight expected failures for absent LocalUser/tables/constraints/unregistered issuer/reader before implementation.
- New architecture assertion was run against the existing compiled Infrastructure before rebuilding model implementation (BuildProjectReferences=false): one expected missing-LocalUser failure; after rebuild all seven architecture tests pass.
- Provisioning restrictions: two failed/one passed before adapter/parser changes. Restriction and category idempotence now pass.
- Additional SQL/crypto/role/failure tests were expanded as the model became available, not retrospectively claimed as failing-first.

Baseline validation was started before setup-helper additions to capture unchanged Feature001 behavior; no production model/adapters were compiled until their relevant red tests had been observed. This small setup scheduling adjustment is recorded rather than pretending every baseline operation followed numerical order.

## Implemented foundation

One Infrastructure LocalUser/IdentityUserContext store with required scalar Customer/Agent/Admin, unique normalized email/username and role/version CHECKs. Shared framework-free DTOs/ports and scalar validators remain inward. Standard UserManager/Identity V3 hashing verifies/rehashes exact spaces/Unicode passwords; default UTF16/whitespace validator replaced, no custom hashing. Explicit required fields/rowversions/FK restrictions and new nullable unique AgentReference link.

Session/refresh/bootstrap tables are schema only: no product account/bootstrap/session flows. Opaque credential digest binary32 and one-current-generation constraint plus all-generation session lookup index are ready for later transaction code. No raw refresh/password seed or Customer profile. Reference CLI now rejects nonempty agents through parser and adapter before staging categories; category-only upserts/fallback protections remain.

RS256 certificate-store issuer, five-minute integer NumericDate lifetime capped by session expiry, exact local issuer/audience/type/algorithm and minimal claims. Local bearer middleware validates required claims and current indexed active user/role/version/live sid on each request, installs trusted snapshot, applies reusable role policies and safe401/403/503. No external Authority/Testing scheme. CurrentActor's later snapshot/name/namespace integration is not implemented; legacy ticket assertions remain unchanged.

## Executed final checks

| Check | Result |
|---|---|
| dotnet build Crm.slnx --no-restore | PASS, zero warnings/errors |
| Domain suite |43 PASS, zero failures/skips |
| Application suite |85 PASS, zero failures/skips |
| Architecture suite |7 PASS, zero failures/skips |
| Focused real SQL/API foundation filter (Identity, LocalJwtTests, LocalAuthorizationFoundationTests, AuthConfidentialityTests) |49 PASS, zero failures/skips; final duration38s |
| Complete affected Integration suite |207 PASS,29 FAIL,0 skipped,236 total;2m10s; see regression summary |
| npm test --prefix src/frontend -- --watch=false |77 PASS/12files; no failures |
| npm run build --prefix src/frontend |PASS |
| EF has-pending-model-changes Infrastructure/API --no-build |PASS, no model changes |
| Fresh/older/current Development startup |PASS: existing pending migrations apply before readiness; repeat stable |
| Production/Staging/Testing/Preview fresh startup |PASS: no implicit schema migration |
| Populated legacy migration |PASS: ticket/history JSON snapshots unchanged, reference IDs/labels/external identities preserved, links null/eligibility false |
| Framework password/rehash/unique-email race/role CHECK/rowversion/FK/refresh uniqueness |PASS |
| JWT role matrix, required-claim rejection, expiry/issuer/audience/signature, disabled/version/revoked state, independent session |PASS |
| EF-wrapped SQL outage |Initial FAIL500, corrected PASS503 with unchanged assertion |
| Owned integration catalogs/owned test certificates after tests |0/0 |
| Historical Feature001 artifacts |27 hashes unchanged |

Focused suites prove foundation behavior; the broad suite is deliberately not called green. No browser/Playwright task beyond T027 was attempted. No participant evaluation performed.

## Corrections discovered

Identity's non-null AllowedUserNameCharacters setting uses empty string to disable its conflicting character whitelist; default PasswordValidator registration is removed rather than supplemented. Missing namespace imports in new test files were corrected after compiler failures; no requirements changed. JWT NumericDate test tightened to exact five-minute claim interval/DTO expiry rather than a subsecond flaky wall-clock assertion. CurrentAuthenticationReader recognizes EF's wrapped transient SqlException; API challenges map that dependency failure503, not500/401. Required JWT claim forms/lifetime were validated explicitly. A separate explicit lookup-index migration corrected the all-generation refresh lookup requirement without editing the original generated migration after validation.

## Local configuration and secrets

Crm.Api now has UserSecretsId=crm-local-development. A dedicated non-exportable RSA3072 signing certificate in CurrentUser/My is referenced by Jwt user-secrets (thumbprint/key ID/store/issuer/audience). No private export, HTTPS trust change, database password or account password was configured. Signing configuration is validated before HTTP/Development migration readiness; installation category CLI/design-time persistence remains signer-independent. Tests create/remove only their own separate temporary certificates; zero retained owned test certificates verified. Normal signing credential intentionally remains for future developer startup. Data Protection/CSRF story tasks T108–T110 were not executed.

SQL operations inherited the Windows identity and validated Integrated Security. All verification migrations ran only on owned disposable catalogs; no developer catalog reset or direct shared-catalog verification migration command. Normal Development startup retains its approved migration behavior.

## Remaining acceptance and blockers

T001–T027 foundation gate passes its focused checks. No blocker to T028 bootstrap work. Full feature/manual authentication is not available until later stories. Broad legacy tests need planned integration adaptation for external actor identities, old Agent CLI, old OIDC configuration/policy layout, two-migration assumptions and old pre-migration seeding of the new LocalUserId column. Assertions were not relaxed. The current baseline is not a release-ready whole-system regression checkpoint. Feature001 T079 remains pending and T081 provisional. See integration-regressions.md for all29 failures; later US5 tasks own adaptation, not this scope.

No post/pre hooks are configured. Changes remain uncommitted.
