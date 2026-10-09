# Authentication HTTP Contract

Proposed Feature002 contract. Same-origin HTTPS, relative /api, JSON camelCase and strict unknown/duplicate-property rejection; no-store on every response. No external auth/cookie login scheme. All passwords are exact strings validated as 15–128 Unicode scalars, with spaces allowed, no mixture requirement and no trim/normalization/truncation. Display names trim to nonblank, <=200 scalars; email trims, validates <=256 UTF-16 units and normalizes through Identity.

## Shared transport and errors

ProblemDetails uses existing type/title/status/code/traceId and optional errors map. No supplied values, Identity errors, credential/account status, exception or token internals in messages. 400 validation_failed for malformed/unknown/duplicate JSON; 415 wrong media; 401 authentication_invalid or token_expired for missing/invalid/expired bearer; 403 forbidden for a valid account lacking policy; 403 csrf_failed for auth-cookie CSRF/Origin rejection; 429 rate_limited with Retry-After; 503 service_unavailable for dependencies. JWT expiry is not a license to retry an uncertain mutation: the interceptor retries only a request rejected before its endpoint executed.

Auth cookie lifecycle POSTs require Content-Type application/json, X-CSRF-TOKEN from GET /api/auth/csrf, and exact configured Origin (development https://localhost:4200). Bodyless operations send {}. Validate built-in antiforgery with an anonymous principal consistently for token issuance and validation, restoring any validated principal afterwards. CSRF endpoints/signup/signin/refresh reject supplied Authorization headers; signout is the one optional-bearer exception for session revocation fallback. No credentials allowed in query strings. No credential CORS; reverse proxy is same-origin. Bearer alone never bypasses CSRF on signout.

Refresh cookie: __Host-crm-refresh, Secure, HttpOnly, SameSite=Strict, Path=/, no Domain; Expires/Max-Age <=family expiry (seven days absolute). Antiforgery cookie: __Host-crm-csrf, Secure, HttpOnly, SameSite=Strict, Path=/; request token is returned to JS in JSON/in memory, not authentication. Clear cookies with identical path/name/security attributes. Refresh cookie intentionally covers host to use __Host prefix; only auth endpoints consume it, ticket endpoints authorize bearer only.

## Endpoint matrix

| Method/path | Input | Success | Protection |
|---|---|---|---|
| GET /api/auth/csrf | No body/query/bearer | 200 {requestToken:string}; antiforgery cookie | Public transport support; no account data |
| POST /api/auth/signup | {displayName,email,password} | 201 {message:"Registration complete. Sign in to continue."}; no user/token/refresh cookie | CSRF+Origin; Customer only |
| POST /api/auth/signin | {email,password} | 200 token envelope +refresh cookie | CSRF+Origin; local credentials |
| POST /api/auth/refresh | {} +refresh cookie | 200 token envelope +rotated cookie | CSRF+Origin and live refresh family |
| POST /api/auth/signout | {} +refresh cookie; optional current bearer | 204 +clear refresh cookie | CSRF+Origin; idempotent, no auth required when already signed out |
| GET /api/auth/me | No body/query | 200 {userId:Guid,displayName:string,role:"Customer"|"Agent"|"Admin"} | Current live bearer account/session |

Token envelope: {accessToken:string,tokenType:"Bearer",expiresAtUtc:ISO-8601 UTC}. No refresh secret in response JSON, URL or JS storage. After envelope, client loads /me to establish trusted identity; never trust decoded claims as sole authority. Signup never signs in. Role, userId, active flags and actor fields are unknown and rejected, including Customer role input; public role is server assigned only.

Signup formatting failures may report safe field errors. Duplicate normalized email and account-sensitive Identity failure return 400 registration_failed with a generic "Registration could not be completed." No existing-account indication; all creations must roll back on failure. Valid signup vs rejected signup is not claimed indistinguishable; failure messages expose no reason/account information. Concurrent duplicate submissions yield one success and generic rejection of others.

Signin malformed input400; invalid credential/nonexistent/disabled account401 invalid_credentials with identical "Email or password is incorrect." No newly created authentication/cookie on failure and no change to existing valid context. Standard/dummy Identity verification prevents a trivial nonexistent-account shortcut. Successful signin replaces/revokes the previous valid cookie family atomically; other cookie jars unaffected.

Refresh malformed JSON400; missing/unknown/expired/revoked credential401 authentication_invalid, clear cookie; known consumed replay401 authentication_invalid after committing family revocation. Dependency failure503 never becomes401. Issue successor only after current user/session validation under the same transaction; consume one predecessor and create one valid successor atomically. If delivery is lost, never automatically retry a consumed credential; require fresh signin. No grace/reuse success. Concurrent requests inside the supported single Angular instance join one refresh; deliberate server replay/racing refresh tests must still prove single-consume and family revocation. Multiple simultaneously active tabs sharing the same authentication session are outside the supported scope of this feature. No Web Locks or cross-tab coordination requirement.

Signout revokes valid refresh-cookie family and/or validated current bearer sid, if only one is supplied. If both are valid they must identify the same sid; mismatched live contexts return409 authentication_context_changed without revoking another context. Known consumed cookie identifies its family for revocation too. Missing/expired/unknown credentials still yield204 and cookie clear; invalid bearer must not make repeated signout fail. Session-aware validation must retain enough trusted sid information for the optional-bearer fallback, without trusting raw claims from an unvalidated JWT. Signout never increments global account version or revokes independent families. Browser clears its in-instance memory/identity and invalidates pending auth generations after successful204; network503 remains a visible failed signout, not claimed server revocation. Access token expiry may prevent bearer fallback, but normal refresh cookie remains usable until the same family absolute expiry.

401 challenge/403 forbid are mapped to the same safe ProblemDetails contract explicitly. Protected APIs validate account/session after crypto on each request; old JWT after signout/disable returns401 even if cryptographically unexpired. Authenticated Customer support request is403. Re-enabling never makes revoked credentials valid.

## JWT and configuration

RS256 signed using dedicated protected 3072+ bit RSA X509 certificate. iss=urn:crm:local, aud=crm-api, sub=canonical UserId, role=one scalar role, sid=session Guid, av=positive version decimal, jti=unique token Guid, iat/nbf/exp; five-minute expiry capped by session expiry, zero skew, exact RS256/typ JWT checks, MapInboundClaims=false. No email/name/password/hash/refresh data. Refresh does not revoke earlier access JWTs of the same live sid; signout/disable does.

Jwt:Issuer, Jwt:Audience, Jwt:SigningCertificateThumbprint, Jwt:SigningKeyId, Jwt:CertificateStoreLocation (CurrentUser for local), Jwt:PreviousValidationCertificateThumbprints (public rollover keys) are validated config. Private key and certificate access are operator-managed; config contains references, not private key contents. Public issuer/audience config is not secret. Auth:AllowedOrigins contains exact origin; Auth durations/limits have documented defaults in plan. Tests use isolated ephemeral certificates with the same real mechanism, never external TestIssuer/TestPublicKeyPath or a bypass scheme.

## Required observable tests

Each role signin/me/signout; missing/invalid/expired/wrongissuer/wrongaudience/wrongalgorithm/signature JWT; old JWT after signout/disable/re-enable; own family vs other context; reload refresh and concurrent single-instance API requests sharing one refresh; rotation replay/lost-response failure; CSRF absent/invalid/wrong-Origin even with cookie; malformed Unicode/password boundaries; safe failures/logs; cookie flags; no local/sessionStorage credentials; server dependency failure; strict DTOs/rate limits. Policy failures precede protected payload parsing and resource existence disclosure.

## Single-instance client refresh contract

AuthService owns one shared in-flight refresh through response parsing and current-identity restoration. Waiting protected requests neither send a second refresh nor replay before the JWT is published. Each eligible waiter replays at most once, only for the same account/session generation. Late old-token401 responses use an already-renewed current JWT rather than redundantly rotating. All refresh failures clear JWT/current user/roles and fail every waiter with a safe error;503/network retains an unavailable diagnostic only, never authenticated state or an automatic retry loop. Signout/new signin invalidates stale completions and queued replays. Authentication transport bypasses the refresh interceptor. Refresh failure does not claim server revocation if communication failed. Separate cookie jars remain independent; no cross-tab support, locking infrastructure or browser Web Locks requirement is introduced.
