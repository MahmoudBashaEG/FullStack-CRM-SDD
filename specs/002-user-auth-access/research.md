# Research: Local CRM Authentication and Access Control

Date: 2026-10-09. All decisions are proposed implementation choices within the clarified specification. Existing code inspected: API AuthenticationConfiguration/SupportQueuePolicy/CurrentActor/HttpConfiguration, shared CrmDbContext/AgentReference/ReferenceProvisioner, Angular core authentication/interceptors/routes/environments, existing Testing/E2E runner and launcher. No production code changed.

## 1. Account management and exactly one role

**Decision**: ASP.NET Core Identity with LocalUser : IdentityUser<Guid>, existing context based on IdentityUserContext, AddIdentityCore/UserManager, no AddRoles and no Identity login-cookie scheme. Required AccountRole scalar with SQL CHECK; standard role claims and authorization policies consume it. Domain owns role vocabulary; all framework types/stores stay Infrastructure.
**Rationale**: Standard account/password facilities without parallel stores; a required scalar directly enforces one role, unlike many-to-many Identity roles. This is the project's role representation, not a custom password/security framework.
**Alternatives considered**: Default role joins need additional enforcement of exactly-one and invite unnecessary role management; separate stores/handmade password hashes rejected; MapIdentityApi issues a proprietary token format and extra excluded flows rather than this project's explicit JWT contract.
**Sources**: [Identity custom models](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/customize-identity-model?view=aspnetcore-10.0), [Identity API token guidance](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-api-authorization?view=aspnetcore-10.0).

## 2. Password validation and normalization

**Decision**: Preserve standard Identity V3 PasswordHasher and rehash behavior; replace the default PasswordValidator with valid Unicode-scalar counting, inclusive 15–128, no mixtures/trim/normalization. Configure UserManager registration once for signup/Agent/bootstrap. Frontend uses the same scalar semantics and rejects malformed surrogate sequences.
**Rationale**: Default validator counts UTF-16 units and rejects whitespace-only passwords; merely disabling mixture flags does not implement the confirmed policy. Normalizing passwords would change accepted secrets. Email, unlike password, is trimmed and normalized using Identity's UpperInvariantLookupNormalizer (NFC/invariant uppercase); normalized unique email has a database constraint.
**Alternatives considered**: HTML maxlength/string.Length alone reject supplementary Unicode incorrectly; app-only unique checks race; custom password hashing rejected.
**Sources**: [PasswordValidator implementation](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.0/src/Identity/Extensions.Core/src/PasswordValidator.cs), [PasswordHasher implementation](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.0/src/Identity/Extensions.Core/src/PasswordHasher.cs), [Identity normalizer](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.0/src/Identity/Extensions.Core/src/UpperInvariantLookupNormalizer.cs), [Rune decoding](https://learn.microsoft.com/en-us/dotnet/api/system.text.rune.decodefromutf16?view=net-10.0).

## 3. JWT signing and lifetime

**Decision**: RS256 with dedicated protected RSA certificate, 3072+ bits; configured issuer urn:crm:local/audience crm-api, access lifetime five minutes, zero clock skew and explicit algorithm/type/signature/lifetime checks. Minimum sub/role/sid/av plus standard token metadata. No name/email/secrets in JWT. Angular memory only.
**Rationale**: User explicitly chose backend-issued JWT. Standard signing/validation libraries implement cryptography; certificate-store custody avoids committing/exporting private keys. Short tokens reduce exposure but do not substitute for revocation.
**Alternatives considered**: Identity proprietary tokens and external OIDC do not meet the chosen contract; indefinite JWTs rejected; shared HS256 secret is possible but not chosen to avoid distributing a signing secret; existing HTTPS certificate must not double as JWT signer.
**Source**: [ASP.NET JWT Bearer configuration](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/configure-jwt-bearer-authentication?view=aspnetcore-10.0). This project deliberately provides a closed first-party local issuer under the explicit user decision, not a general OAuth/OIDC authorization server.

## 4. Refresh/session model and races

**Decision**: Opaque random 256-bit refresh secrets, SHA-256 digest storage, seven-day absolute session family, rotation with consumed predecessors retained for replay detection. Consumed-token reuse revokes that family; no grace window. Signout revokes sid, not the user. The project supports one active browser tab per signed-in session. Multiple simultaneously active tabs sharing the same authentication session are outside the supported scope of this feature. AuthService shares exactly one in-flight refresh among concurrent requests inside that Angular instance, publishes the new memory JWT before successful waiters retry once, and clears JWT/identity on any failure. Preserve within-instance generation checks; no Web Locks, BroadcastChannel or cross-tab coordination infrastructure.
**Rationale**: Preserves reload without JS-readable persistent credentials. Single-instance request coalescing avoids competing legitimate rotation while atomic server consume/replay protection stays intact. Lost refresh responses can require fresh signin; no encrypted successor cache or retry grace. Separate profiles/devices retain independent sessions, with one active tab per session. PG-001 is resolved by the explicit project scope decision rather than a browser-support dependency or unapproved locking fallback.
**Alternatives considered**: local/sessionStorage credential persistence rejected; nonrotating refresh and retry grace weaken the approved rotation/replay behavior; global signout breaks independent sessions. Cross-tab locking, browser capability gating and multi-tab synchronization are excluded by the single-tab project decision. Per-request independent refresh within one instance is rejected because it races rotating credentials.
**Sources**: [RFC 9700 refresh protection](https://www.rfc-editor.org/rfc/rfc9700.html#section-4.14.2), [.NET cryptographic random bytes](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.randomnumbergenerator.getbytes?view=net-10.0).

## 5. Disabled-account and context invalidation

**Decision**: OnTokenValidated reads account+session on every authenticated request, compares authoritative role/AuthVersion, checks active/revoked/expiry and installs trusted current identity. Disable transaction increments version/security stamp, revokes sessions and makes reference ineligible. Re-enable changes active state only, never restores earlier authentication. No positive cache.
**Rationale**: Existing JWTs must stop working on the next request after disable/signout; expiry-only or periodic cookie-stamp validation is insufficient. One indexed SQL lookup trades throughput/database availability for the required correctness. Already-authorized requests may finish. Store failure is 503/fail-closed.
**Alternatives considered**: JWT-only revocation waits for expiry; user-wide stamp updates at signout invalidate unrelated contexts; distributed revocation caches add invalidation complexity for this local project.
**Source**: [JwtBearer OnTokenValidated](https://learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.authentication.jwtbearer.jwtbearerevents.ontokenvalidated?view=aspnetcore-10.0).

## 6. CSRF and browser boundaries

**Decision**: Same-origin HTTPS application/API via existing /api proxy. Secure/HttpOnly/Strict host-only refresh cookie; built-in antiforgery request-token header and exact configured Origin on signup/signin/refresh/signout. Anonymous CSRF principal during antiforgery issuance/validation, restoring any validated identity afterwards. No bearer on signup/signin/refresh; signout may carry the current bearer as a revocation fallback without bypassing CSRF. Access-protected routes use bearer only. Required public CSRF token endpoint is transport support, not public account administration.
**Rationale**: Cookies are ambient credentials; SameSite/CORS alone are not enough. Stable anonymous binding avoids an expired/missing bearer causing anti-CSRF principal mismatches. Tokens stay in memory and never appear in logs.
**Alternatives considered**: refresh in localStorage and wildcard credential CORS rejected; treating all POSTs as bearer-only misses login/refresh/signout CSRF; implementing a homegrown CSRF protocol rejected.
**Source**: [ASP.NET antiforgery](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).

## 7. Bootstrap and transaction ownership

**Decision**: Explicit bootstrap-initial-admin API-executable CLI using operator input, hidden password prompt or private stdin JSON; no password argv/environment/default configuration. Migrations first; CLI bypasses HTTP/migration hook. Serializable singleton setup record created with Admin; rerun checks completion before credential prompting and makes no changes. Identity saves enclosed by the owning context transaction; no automatic retries.
**Rationale**: Keeps schema evolution separate from credential creation and prevents races/partial setup. Local Windows operator authority is the installation boundary. No public endpoint and no plaintext migration seed.
**Alternatives considered**: Startup seeding with persistent config/password risks credential persistence and surprise resets; public first-user-wins bootstrap unsafe; migration password seeds violate spec/constitution.
**Sources**: [Identity EF store](https://raw.githubusercontent.com/dotnet/aspnetcore/v10.0.0/src/Identity/EntityFrameworkCore/src/UserOnlyStore.cs), [EF transactions](https://learn.microsoft.com/en-us/ef/core/saving/transactions).

## 8. References, provisioning and actor compatibility

**Decision**: New AgentReference per new Agent with unique nullable local UserId FK; legacy links remain null and ineligible after explicit migration. No auto-link/reconciliation. Existing issuer/subject history and reference identifiers retained. New CurrentActor uses constant local namespace plus stable UserId. Category CLI persists, but agent upserts become unsupported and rejected.
**Rationale**: Clarification explicitly forbids linking; leaving legacy CLI eligibility mutation enabled would bypass the required local account model. Current actor's compatible shape avoids rewriting immutable historical snapshots. SQL transactions synchronize new account/reference updates.
**Alternatives considered**: Reuse/link legacy identities contradicts clarification; new separate assignable-account store duplicates authority; deleting old references breaks tickets; rewriting old actor IDs breaks immutability.
**Source**: [EF optimistic concurrency](https://learn.microsoft.com/en-us/ef/core/saving/concurrency). Existing Feature001 code/data contracts provide the concrete compatibility constraints.

## 9. Retirement and operational assumptions

**Decision**: Replace every runtime external OIDC/Testing token shortcut; update harnesses to actual local accounts/signin sessions and ephemeral signing configuration. Retain Feature001 evidence as historical and write new results in Feature002. Reuse launcher/readiness/Development migrations. Verify reload and concurrent requests within one Angular instance, with no Web Locks feature detection or unsupported-locking authentication dependency.
**Rationale**: One authentication infrastructure must serve current/future modules. Test-only direct JWT injection cannot prove signin/refresh; keep cryptographic negative fixtures only inside tests.
**Alternatives considered**: Parallel legacy scheme or feature-owned user stores rejected. No ADR convention or existing docs/architecture files were found; one shared architecture document is enough.

All research unknowns are resolved. Durations, limits and operational bounds are technical choices, not new business success targets. Signing-store access, HTTPS trust and operator credentials are deployment prerequisites. No requirement/spec change was needed.
