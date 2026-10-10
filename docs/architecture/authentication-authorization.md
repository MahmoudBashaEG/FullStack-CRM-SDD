# Shared CRM Authentication and Authorization Architecture

Status: implemented and verified for Feature002, 2026-10-10. This is the reusable contract for future CRM features; Feature002 sequencing and detailed endpoints belong in its [plan](../../specs/002-user-auth-access/plan.md) and contracts. Runtime external OIDC is retired; historical evidence is retained. See [final verification](../../specs/002-user-auth-access/validation/final-validation.md) and [requirement coverage](../../specs/002-user-auth-access/validation/requirements-coverage.md).

## Shared account identity

One shared CRM account store uses ASP.NET Core Identity UserManager/stores/standard password hashing, adapted in Infrastructure over the existing SQL context. Domain/Application remain framework-independent. CRM UserId is a stable Guid used across modules; it is not an email, external subject, Customer profile ID or Agent assignment ID. Credentials never leave account infrastructure or appear in logs/responses. Passwords follow the confirmed Unicode-scalar 15–128 policy without trim/normalization/truncation; use the common validator in every account creation path.

Exactly Customer, Agent and Admin exist with one required immutable scalar role per account. SQL constrains the vocabulary; framework authorization role/policy machinery consumes the authoritative role. Public registration creates Customers only; Admin-only Agent creation creates Agents only; protected installation bootstrap establishes the initial Admin. No public staff creation, role edits, second user store or Customer profile is implied.

## Authentication and token lifecycle

The backend is the only first-party JWT issuer/validator. Standard IdentityModel signing and JwtBearer middleware use dedicated protected RS256 signing credentials, exact issuer/audience/algorithm/lifetime and zero skew. Access JWTs are short-lived (five minutes) and contain only stable UserId, one role, session/version and standard token metadata. Keep them only in Angular memory; never feature storage, browser storage, URLs or cookies used as bearer credentials.

Opaque refresh credentials use standard random generation and digest storage in the same database. HttpOnly/Secure/Strict host-only cookies carry them; JS cannot read them. Refresh rotates credentials transactionally in a seven-day absolute family. Consumed-token reuse revokes that family; no unbounded tokens/reuse grace. A lost refresh response can require fresh signin. Cookie lifecycle endpoints use framework antiforgery with consistent anonymous binding and exact trusted Origin; same-origin /api proxy deployment remains the supported shape.

Every protected bearer request checks current active account, role/authentication version and live session after signature/lifetime verification. No positive cache or expiry-only revocation. Signout revokes the current family and invalidates its access JWTs; other independent cookie jars remain unaffected. Agent disablement atomically increments auth version/security stamp, revokes all its sessions and removes new assignment eligibility. Re-enable permits fresh signin, never revives old credentials. SQL dependency failure denies access with service-unavailable, not a successful cached fallback. Already-authorized operations may finish; next protected operations must check state.

## Durable security state on Windows

DataProtectionConfiguration pins the framework application discriminator to `Crm` and uses current-user Windows DPAPI encryption. The default durable repository is `%LOCALAPPDATA%/ASP.NET/DataProtection-Keys`; a missing Windows profile fails setup rather than selecting ephemeral keys. Optional `DataProtection:KeyDirectory` selects an operator-managed directory with the same framework persistence/protection. Default framework key lifetime/rollover is unchanged. Keep repository permissions limited to the host/operator; do not commit or print key XML. This feature supports Windows hosting under the configured identity and does not introduce distributed key infrastructure.

Data Protection protects antiforgery state, independently of the dedicated JWT signing certificate and SQL refresh digests. Actual API and Development-launcher restart tests retain the pre-restart CSRF request-token/cookie pair and refresh credential, then successfully rotate/restore/sign out. Normal restart preserves the encrypted key ring. JWT signer configuration and the SQL catalog must also remain consistent across restart; key persistence does not revive expired/revoked sessions.

## Authorization and backend integration

Shared named policies: AuthenticatedCrmUser requires validated local identity, SupportQueue admits active Agents/Admins, AdminAgents admits Admins only. API applies a fallback authenticated-user policy and explicit AllowAnonymous for defined authentication transport/public flows. Middleware owns authentication; API policy checks precede payload/resource disclosure. 401 means no valid authentication; 403 means valid identity without permission. Application handlers operate on trusted identity/ports rather than HTTP claims or scattered role-string comparisons.

Future capabilities define named policies using this registry and current validated CRM identity; they do not invent a parallel role model. Angular guards/menu visibility improve navigation only, never enforce security. Customers have identity/signout but no internal queue or Agent management until separately specified portal capabilities. Agents/Admins retain all approved shared-queue lifecycle permissions irrespective of assignee; Admin role alone is not assignable Agent identity.

ICurrentActor is the single Application-facing source of trusted actor identity for any authenticated role, not a SupportQueue-only adapter. Feature policies separately govern access; using CurrentActor never grants queue permission. Its compatible Actor shape emits constant urn:crm:local:user namespace, canonical UserId subject and current display name; historical external namespaces remain saved data. Future features reuse stable UserIds and this actor source, never caller-supplied identity or email as a key. Signing key rotation cannot change actor identity. Domain must not depend on ASP.NET Identity/EF; Application declares cohesive use-case ports; Infrastructure implements Identity/transactions/token creation; API is composition/HTTP/policies.

## Angular shared state and feature ownership

Core AuthService/state owns initialization, current identity, memory JWT, one shared in-flight refresh and within-instance session generation. Core interceptor attaches bearer only to same-origin protected /api, coalesces expiry refresh and permits at most one successful replay per eligible rejected request, never loops/retries forbidden, failed refresh or uncertain mutations. Signout failures stay visible without false server-revocation claims. A new page restores through CSRF+refresh+/me before guards resolve; any restoration/refresh failure clears JWT/current identity. Dependency failure is unavailable diagnostic state, never authenticated state.

Core role guards use the shared identity/policy model. Signup/signin pages belong to features/auth, Agent management to features/agents and ticket UI to features/tickets. No auth copies within features. Shared is only genuinely reusable presentation code. Arabic/English-compatible Unicode/direction-aware labels/forms, accessible errors and responsive layouts remain constraints; no branch/department/branding module is prebuilt.

The application supports one active browser tab per signed-in session. Multiple simultaneously active tabs sharing the same authentication session are outside the supported scope of this feature. Cross-tab synchronization is excluded; no Web Locks dependency, BroadcastChannel requirement or cross-tab locking infrastructure. Within one Angular instance, concurrent API requests share one in-flight AuthService refresh through parsing/identity restoration; publish the JWT before successful waiters replay, each at most once. Reuse an already-renewed JWT for late old-token401 responses. Any refresh failure invalidates its generation, clears memory JWT/current identity and rejects every waiter; delayed expiry responses cannot start another automatic rotation. Keep503/network authentication diagnostics distinct from401 without retaining authenticated state. Ordinary feature503/network errors alone preserve an otherwise valid JWT for explicit recovery; uncertain feature mutations are never automatically replayed. Generation checks prevent signout/new signin from being overwritten by stale refresh completions or queued requests. Separate browser profiles/devices retain independent sessions; ticket response/version safeguards stay in the tickets feature.

## Installation and Agent identity integration

EF migrations evolve schema and explicitly preserve existing ticket/history data; no plaintext credentials or default Admin seeds. Development applies existing pending compiled migrations through the current hook only; other startup/CLI paths migrate explicitly. Operator-only bootstrap CLI supplies credentials via hidden input/private stdin and atomically records completion with one Admin. Idempotent rerun never resets credentials/promotes users. Runtime startup/run-dev does not bootstrap or generate signing keys.

Admin Agent management is limited to list/create/display-name update/disable/re-enable; immutable signin email/password/role and no deletion. Account/reference updates share one context transaction and expected-version concurrency. Every new Agent has its own new assignment reference linked uniquely to UserId. Admin/Customer do not get one. Existing legacy AgentReferences keep saved IDs/external metadata and no local links; they remain readable on old tickets/history but unavailable for new assignment. No automatic linking/reconciliation, deletion or history rewriting. Category provisioning remains, but old Agent upsert provisioning is retired so it cannot bypass account authority.

## Rules for future features

- Reuse the existing CRM User/account model and stable UserIds; keep Customer business/profile entities distinct.
- Reuse the existing JWT issuer/validation, refresh mechanism and session revocation; do not create another authentication system or User store.
- Reuse shared authorization policies and ICurrentActor; extend named policies deliberately instead of redefining Customer/Agent/Admin independently or trusting caller identity.
- Enforce authorization server-side before resource/payload decisions; do not treat route guards as protection.
- Reuse core AuthService/guards/interceptor; keep feature pages/models/data access within their feature.
- Preserve immutable history and legacy reference compatibility; account names/availability changes cannot rewrite prior snapshots.
- Never commit signing/private credentials or log auth payloads. New account-creation paths, if later authorized, must reuse the standard framework and common validators/transactions.
- Add architecture, direct API, SQL concurrency and auth-transition regression tests; do not reinstate external provider/test-only auth schemes as shortcuts.

This document records stable implemented rules. Technical detailed limits/contracts and feature-specific test sequencing stay in Feature002 artifacts; no separate ADR is added because the repository has no ADR convention and a duplicate decision document adds no value.
