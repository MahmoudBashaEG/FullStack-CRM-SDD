# Local Development Launcher Contract

Prospective technical contract, 2026-10-09. No business requirement or REST shape changes.

## Invocation and prerequisites

From the repository root: `pwsh -NoProfile -File ./scripts/run-dev.ps1`.
Windows PowerShell 7 host, pinned SDK/Node/npm, restored dependencies, accessible local SQL,
normal OIDC configuration and local HTTPS trust are prerequisites. Resolve paths from the
script location and reuse scripts/environment.ps1; do not depend on generated launch profiles.
No credentials are passed as launcher arguments or printed in setup diagnostics.

ConnectionStrings:Crm comes from the existing API configuration sources and uses local SQL
Integrated Security and the inherited Windows identity. Frontend public OIDC settings stay
in its existing environment approach. Reject Authentication__Test* environment values; normal
API validation also rejects test settings outside Testing. No auth bypass/automatic identities,
reference provisioning, dependency installation or certificate trust changes during launch.

## Services and communication

- API: dotnet watch, non-interactive, normal run/build, --no-launch-profile; both host environment
  variables Development; HTTPS localhost:7043; suppress backend browser auto-launch.
- Frontend: existing npm start/Angular development configuration; HTTPS localhost:4200.
- New proxy.development.json is wired only into Angular development serve. `/api/**` forwards
  unchanged to https://localhost:7043, with secure:true and bearer headers preserved.
- environment.apiBaseUrl remains `/api`; no feature-specific backend URLs or duplicate HTTP client.
- Trust the API certificate in Node, using existing NODE_EXTRA_CA_CERTS public PEM setup where
  needed. Do not reuse the e2e secure:false proxy or change production TLS/Testing authentication.

API alone applies compiled pending EF migrations before Development HTTP listening; existing
startup scope/cancellation/model checks remain. No EnsureCreated, generation, explicit EF update
or alternate migration connection inside the launcher. Generation/review/versioning is deliberate
developer work; model changes require rebuild/restart with an explicitly generated migration.

## Lifetime and observable outcome

Run owned children concurrently in the same console, inheriting live stdout/stderr. Preserve
process handles and descendant ownership, including a possible native npm command wrapper.
Use structured arguments and explicit working directories; no secrets in interpolated shell code.
No detached child windows or logs hidden exclusively in files.

Check tools/dependencies/ports before spawning; preserve normal configuration validation rather
than duplicating its parsing. Initial availability: API categories 401 and frontend HTML 200
within a bounded 120 seconds. Report both URLs, but do not call a 401 proof of SQL or sign-in.
An authorized categories 200/fallback and normal ticket journey supply manual acceptance evidence.
No token generation or external OIDC request is required just to perform availability probes.

Ctrl+C reaches the console children; cleanup uses one five-second grace budget for both
services from the shutdown request, then terminates remaining owned trees. On partial launch,
timeout or child exit, request owned-child shutdown where safely supported and use the same
budget; waiting alone is not a graceful shutdown request. Report graceful exits separately
from forced cleanup. Test both cooperative exit within grace and an uncooperative fallback.
Partial launch, readiness timeout and terminal child exit unwind both
services. A watcher can remain alive after an API build/start failure; do not equate that with
readiness. Subsequent hot-reload/rebuild interruptions remain visible without a custom health
or restart platform. Abrupt window/process termination is outside graceful shutdown guarantees.

Never stop an unrelated process, attach to occupied ports, or delete databases/files. Cleanup
must cover children when their wrapper/root exits first; verify actual descendants and port
release, not merely root exit. Restore location/environment when invoked in-process. Setup or
unexpected child/startup failure returns nonzero; normal operator stop is reported distinctly.
Do not invent SQL privileges, alter global certificate settings or echo sensitive configuration.

## Verification boundary

See quickstart.md launcher scenarios and plan.md. Test ownership/failure/cleanup mechanics and
real startup/proxy/watch behavior; retain existing backend, Angular and Playwright regressions.
Automated SQL verification uses owned disposable catalogs. Ordinary manual launch intentionally
uses the developer's configured catalog without cleanup/reset. External OIDC interoperability and
SC-005 human evaluation remain separate, honestly pending dependencies when unavailable.

Authenticated automation sends HTTP requests through the real Angular /api proxy using
directly issued signed fixture tokens from a private, ephemeral HTTPS discovery/JWKS issuer
outside product code. Normal API Authority/Audience and JWT validation remain enabled in
Development. No API test-key configuration, frontend token/session injection or auth adapter
replacement is allowed. This proves categories and ticket API journeys through the frontend
origin, not SPA sign-in. Verify normal HTML/routing/unauthenticated guard/reload separately.
Discovery/JWKS is not a PKCE login service. Real browser sign-in/callback and authenticated
UI journeys require registered external OIDC client/accounts and are recorded separately,
pending when unavailable. Existing Testing Playwright results do not substitute for them.
