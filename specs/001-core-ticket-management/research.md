# Research: Core Ticket Management

**Date**: 2026-10-08. Decisions below are engineering choices derived from the active spec,
constitution and user stack; they are not additional business requirements. All technical
unknowns are resolved for planning. Operator-specific connection/issuer values are configuration.

## 1. Supported Toolchain

**Decision**: .NET/ASP.NET Core/EF Core 10, Angular/CLI 22, TypeScript 6.0.x, Node 24.20.0.
SQL Server 2022 is a conservative target engine, not a claim about the newest version.
Pin supported patches in SDK/package manifests and lockfiles during implementation.

**Rationale**: .NET 10 is LTS. Official Angular tables permit Node 24.15+ and TypeScript
6.0.x for Angular 22. Installed SDKs include 10.0.400 and Node is 24.20.0.

**Alternatives considered**: .NET 8/9 offer less remaining support; Angular 21 requires
an earlier major upgrade. Do not scaffold with prerelease packages or an unbounded latest tag.

Sources: [Microsoft support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core),
[Angular versions](https://angular.dev/reference/versions),
[Angular releases](https://angular.dev/reference/releases).

## 2. MediatR and Layer Boundaries

**Decision**: Use MediatR 12.5.0 as the explicit planning baseline, with compatibility and
vulnerability review when restoring dependencies. It provides the requested command/query
orchestration without assuming commercial-license eligibility. Future upgrades require the
appropriate license and support review, not a custom replacement dispatcher.

**Rationale**: Its tagged license is Apache-2.0; newer MediatR versions introduce a different
licensing model. Application needs no dependency on EF to use MediatR handlers.

**Alternatives considered**: A current licensed release is valid if entitlement is supplied;
custom mediation adds unnecessary maintenance and departs from the selected stack.

Sources: [Official MediatR information](https://mediatr.io/),
[tagged 12.5.0 license](https://github.com/LuckyPennySoftware/MediatR/blob/v12.5.0/LICENSE).

## 3. Feature Persistence Ports

**Decision**: Application owns ITicketMutationStore, ITicketReadService and ITicketReferenceData;
Infrastructure implements them with one scoped DbContext. Query DTOs and opaque version tokens
cross the boundary; DbSet/IQueryable/EF exceptions do not. A mutation operation holds a short
transaction across loading, rule evaluation and commit/no-op result.

**Rationale**: These ports add atomic aggregate persistence, transaction/concurrency control
and test seams. No generic repository per entity and no repeated EF unit-of-work abstraction.
This is our design inference from the dependency constraints.

**Alternatives considered**: Direct EF in Application breaks the chosen inward separation;
per-DbSet repositories add ceremony; separate CQRS stores/event sourcing add unsupported scope.

## 4. Edit Versions, Transactions and Atomic History

**Decision**: Return opaque editVersion in ticket detail; require expectedVersion in existing
mutation bodies. Both encode the SQL shadow rowversion as base64, without quotes. Use 400 for
missing/malformed versions and 409 ticket_changed for stale values. No HTTP representation
validators or conditional-request headers are used. Responses are Cache-Control: no-store.
PATCH details has a documented partial JSON schema; PUT assignment/status replaces complete
writable subresource state, with no validator returned after normalization.

**Rationale**: Category/agent labels and active flags may change independently of ticket state.
A raw ticket rowversion cannot validate that entire representation. Separating the aggregate
edit token avoids composite validators and avoids the transformed-PUT validator restriction.
It still implements stale edit rejection exactly as required by FR-012.

**Alternatives considered**: Composite ETags need reference revisions and coordinated reads;
weak ETags do not provide strong If-Match comparison. Neither is necessary here. Keeping raw
rowversion as a strong validator or returning validators after transformed PUT is rejected.

**Transaction decision**: DI supplies one request-scoped DbContext shared by feature ports and disposes it; ITicketMutationStore owns the sole transaction/commit/rollback. The handler
uses its mutation operation and the same transaction-backed reference port, with no nested
transaction or MediatR transaction pipeline. Parameterized ticket UPDLOCK/HOLDLOCK followed
by newly selected category/agent HOLDLOCK reads in fixed order protects eligibility and no-ops.
Real saves also use the original EF token; ticket/history save once and commit together.
No-op returns unchanged token/history/time. Cancel/failure paths roll back and dispose.

**Retry decision**: No EnableRetryOnFailure or implicit retrying strategy, handler replay or
HTTP mutation retry. A known stale version is 409; deadlock/timeout is 503. Unknown commit
outcome requires refresh, not blind replay. Cleanup uses a non-cancelled bounded path where
needed. This is version-based concurrency with short writer serialization, not purely optimistic.

**Alternatives considered**: Unlocked no-op checking leaves a race; broad serializable read
transactions restrict unrelated queries; a separate no-op persistence algorithm adds complexity.

Sources: [HTTP validators](https://www.rfc-editor.org/rfc/rfc9110.html#section-8.8.1),
[PUT semantics](https://www.rfc-editor.org/rfc/rfc9110.html#section-9.3.4),
[PATCH](https://www.rfc-editor.org/rfc/rfc5789.html),
[EF concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency),
[EF transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions),
[connection resiliency](https://learn.microsoft.com/en-us/ef/core/miscellaneous/connection-resiliency),
[SQL table hints](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table).

## 5. Minimal Authorization and Reference Provisioning

**Decision**: Validate external JWTs and enforce SupportQueue with an explicit
crm_support_queue=shared claim plus issuer/subject identity. Frontend uses oidc-client-ts
authorization-code/PKCE against an external SPA registration. Production fails closed if
identity configuration is missing. Test credentials/configurations are isolated from production and still validate signed JWTs. Categories
and eligible agents are provisioned reference records, read-only through HTTP.

**Rationale**: Delivers the confirmed shared queue without registration, credential storage,
role management or a permission matrix. Stable issuer/subject keys distinguish identities;
agent eligibility is separate from the authority to access the queue. OIDC library avoids
hand-written authentication protocol code. No administrator UI is implied by provisioning.

**Alternatives considered**: Anonymous access violates the spec; building an identity server
or category/user administration expands scope. A future administration feature may replace
the claim adapter/reference source without changing the domain lifecycle.

Sources: [JWT bearer](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0),
[authorization policies](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/policies?view=aspnetcore-10.0),
[oidc-client-ts project](https://github.com/authts/oidc-client-ts).

### Confirmed Reference Rules and Bounded Provisioning

**Decision**: Compare submitted IDs with persisted IDs before checking availability. Retain
unchanged inactive references in edits/no-ops/status changes; clear an assignment without a
replacement eligibility check. Newly selected IDs, including formerly used inactive IDs,
require current availability/eligibility. Protect Uncategorised at its fixed ID; never delete,
rename or deactivate it while default. Seed and SQL CHECK plus supported-tool validation
preserve the invariant without adding an administration workflow.

**Rationale**: These are confirmed spec A-011/A-012, not optional technical defaults.
[reference-provisioning.md](contracts/reference-provisioning.md) defines one installation-only
JSON upsert input. Omitted rows remain unchanged; no reconciliation/delete mode. Validate
all records before any write, use one transaction, reject changed agent identity keys,
and leave tickets/history untouched when reference availability changes.

**Alternatives considered**: Rechecking unchanged references blocks approved unrelated edits;
a generic importer, sync engine or runtime administration UI is unnecessary scope.

## 6. Search, Reference Identity and Paging

**Decision**: Ticket reference is TKT- plus the uppercase hexadecimal GUID without separators,
with a unique exact-match index. Subject is Unicode and uses explicit case-insensitive,
accent-sensitive SQL collation; escape literal substring wildcard characters. Queries are
parameterized. List defaults to 25 results, max 100, ordered UpdatedAtUtc descending then
Reference ascending; history orders by Revision/Sequence ascending with the same page bounds.

**Rationale**: Stable references do not need a centralized sequence. Bounded pages keep the
first queue useful with increasing volume. Search must not treat percent/underscore/brackets
as user-authored query operators. Test Arabic and Latin text against SQL Server itself.

**Alternatives considered**: Client-only filtering and unbounded lists do not scale; full-text
search changes substring behavior; per-query lowercasing can inhibit indexed comparisons.
No case-insensitive customer-specific normalization is invented.

Source: [EF collations](https://learn.microsoft.com/en-us/ef/core/miscellaneous/collations-and-case-sensitivity).

## 7. HTTP and Frontend

**Decision**: Resource-oriented REST with typed DTOs, ProblemDetails, explicit body-version conflicts,
standalone lazy Angular routes and reactive forms. Functional interceptors handle token
transport and common failures; ticket-specific conflict behavior stays in tickets. No global
state library. CORS uses approved frontend origins, allows Authorization/Content-Type, and exposes Location. No validator or conditional-header transport is required.

**Rationale**: Clear ownership and consistent errors support all specified journeys. Server
policies remain authoritative; guards cannot establish authorization. Keep unsaved edits on
conflict and require explicit reload/resubmission.

**Alternatives considered**: Generic catch-all mutation endpoints obscure lifecycle validation;
NgModules/global state are unnecessary for this bounded first feature.

Sources: [ASP.NET errors](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/error-handling-api?view=aspnetcore-10.0),
[Angular lazy routes](https://angular.dev/best-practices/performance/lazy-loaded-routes),
[interceptors](https://angular.dev/guide/http/interceptors),
[route guards](https://angular.dev/guide/routing/route-guards).

## 8. Verification and Platform Compatibility

**Decision**: xUnit domain/application tests, WebApplicationFactory with real SQL Server,
architecture checks, Angular TestBed/Vitest and focused browser lifecycle tests. Unicode
storage, direction-aware styles, message keys and presentation variables maintain platform
compatibility. Provisioning is external to the ticket use cases; no organizational fields
are guessed now. Performance numbers in plan.md are advisory benchmarks, not completion gates unless adopted in the spec.

**Rationale**: SQL Server rowversion/locks/collations need provider-real tests. Frontend tests
cover transport and state handling; staff evaluation separately proves SC-005. All 16 status
pairs are tested, with equal pairs as no-ops and six allowed unequal transitions.

**Alternatives considered**: EF InMemory/SQLite do not prove provider-specific correctness;
automated tests alone cannot establish representative staff usability.

Sources: [ASP.NET integration testing](https://learn.microsoft.com/en-us/aspnet/core/test/integration-tests?view=aspnetcore-10.0),
[Angular testing](https://angular.dev/guide/testing),
[Angular HTTP testing](https://angular.dev/guide/http/testing).

### Reproducible Browser Harness

**Decision**: npm run e2e invokes a test runner that prepares an isolated SQL database,
creates an ephemeral signing key/test tokens, then invokes Playwright. Two webServer entries
start API and Angular e2e configuration, with fixed local ports, explicit working directories,
reuseExistingServer=false and bounded readiness timeouts. Both .NET environment variables are
Testing; --no-launch-profile prevents accidental launch settings. Authorized setup requests
prove database readiness after unauthenticated HTTP readiness at /api/ticket-reference-data/categories in every story phase. One worker initially; no endpoint switch when tracking ships.

**Rationale**: Test-only signed identity fixtures exercise JWT validation and the real queue
policy without depending on an external sign-in UI. Production stays on external OIDC;
frontend e2e adapter is a build replacement unavailable in production. Fixture keys/tokens
remain ephemeral, outside source and URL parameters. HTTPS uses separate local development
certificates; certificate-validation exceptions apply only to test readiness/browser contexts.
The Angular test proxy targets the test API and enables local self-signed upstreams only.

**Alternatives considered**: Manually started servers and unspecified issuer login cannot make
npm run e2e reproducible. Fake authentication alone would not prove JWT validation.

Sources: [Playwright servers](https://playwright.dev/docs/test-webserver),
[Playwright authentication](https://playwright.dev/docs/auth),
[Angular configuration replacements](https://angular.dev/tools/cli/environments).

## 9. Single Windows Identity for Local SQL Server

**Decision**: ConnectionStrings:Crm uses local SQL Server and Integrated Security=True /
Trusted_Connection=True. API, EF migration tooling and provisioning CLI run as the same Windows
account using the same connection. No separate SQL/database principals, database username/password,
grant/deny scripts or per-process connection secrets. Tests change only the catalog in that shared
connection, remaining on the same Windows identity. Keep ordinary application/JWT auth separate.

**Rationale**: This is the user's explicit project decision. SqlClient integrated security uses
Windows authentication; reject User ID/Password keys instead of relying on their being ignored.
SqlConnectionStringBuilder validation is shared in Infrastructure across all entry points. The
EF design-time factory uses the same key, avoiding HTTP startup but not changing DB authority.
[database-principals.md](contracts/database-principals.md) is simplified to this contract.

**Trade-off**: Database least-privilege separation is intentionally not implemented. Broad rights
needed for migrations may be available to HTTP and CLI through that identity. Application pathways
protect history and limit provisioning; they do not stop privileged direct SQL writes. No database
permission-denial proof is claimed. Verify unsupported operations at the application/CLI boundary.

**Alternatives considered**: Separate runtime/migration/provisioning identities were superseded by
this project decision and are removed. SQL-authentication passwords, impersonating each staff user,
a new role administration module and an identity-switching credential framework are rejected.

Sources: [SqlClient Windows authentication and syntax](https://learn.microsoft.com/en-us/sql/connect/ado-net/connection-string-syntax),
[IntegratedSecurity builder](https://learn.microsoft.com/en-us/dotnet/api/microsoft.data.sqlclient.sqlconnectionstringbuilder.integratedsecurity),
[Windows host identity](https://learn.microsoft.com/en-us/dotnet/api/system.security.principal.windowsidentity.getcurrent),
[SQL connecting login](https://learn.microsoft.com/en-us/sql/t-sql/functions/original-login-transact-sql).

## 10. Story-Level History and Repeatable Staff Evaluation

**Decision**: Deliver read/projection/rendering support alongside each emitted history kind:
Created in US1, details/assignment in US3, status in US4. US5 covers comprehensive mixed-history
presentation and regression proof, without delaying earlier readability. Use unchanged REST shapes.

**Rationale**: Mutation checkpoint evidence must show who changed what and when. Writing rows
alone cannot prove the specified history behavior. A common endpoint/component avoids duplicate
story-specific history systems.

**Evaluation decision**: SC-005 is measured with five actual support staff who did not implement
or previously pilot this feature, identical training/test data and a scripted sequence, no
procedural assistance in the scored attempt, recorded task outcomes/assistance and final owner/
status answers. See quickstart.md. No extra speed target or new product requirement is introduced.

**Alternatives considered**: Deferring projection until US5 hides earlier emitted changes;
uncontrolled coaching or simulated users cannot make the four-of-five evaluation repeatable.
