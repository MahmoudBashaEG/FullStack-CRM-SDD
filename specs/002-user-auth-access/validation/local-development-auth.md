# Verified local authentication workflow

Verified 2026-10-10 (Africa/Cairo), T101–T105. Run from the repository root in PowerShell7. The normal command remains `./scripts/run-dev.ps1`, starting dotnet watch/API at https://localhost:7043 and Angular at https://localhost:4200. Relative /api uses the existing secure development proxy. Logs remain visible; owned shutdown has the shared five-second grace budget and forced cleanup if needed.

## Preconditions and isolation

Use local SQL with ConnectionStrings:Crm/Integrated Security and the existing Windows identity. The operator supplies a dedicated3072+bit RS256 signing certificate in CurrentUser/My and its thumbprint/key ID through user-secrets or environment references. It is separate from the already-trusted HTTPS certificate. No JWT key/password is exported or placed in application JSON. The documented PKI command is available; verification uses exclusively owned temporary signer/configuration, not replacement of the developer's permanent certificate, trust, user-secrets or catalog.

Bootstrap is explicit after schema preparation: `dotnet run --no-launch-profile --project src/backend/Crm.Api -- bootstrap-initial-admin`. Interactive input is non-echo; approved automation adds `--input-stdin` and supplies operator-controlled JSON through private stdin, never argv/environment/logs. Actual launcher runs use this CLI with generated synthetic credentials in an account-restricted ignored owned fixture; it returns created. Native CLI verification passes11/11, including the documented dotnet-run invocation, repeats/concurrency/conflicts/invalid input and no schema/signing fallback. The complete backend suite also includes hashing, bootstrap transactional rollback/password boundaries and no elevation/overwrite.

## Actual Development reproduction

Executed both commands, with normal trusted HTTPS and exclusively owned SQL/certificate resources:

```powershell
. ./scripts/environment.ps1
pwsh -NoProfile -File tests/scripts/run-dev.integration.ps1
pwsh -NoProfile -File tests/scripts/run-dev.integration.ps1 -InitialSchema
```

Both PASS. The real root command applies all four existing migrations before authenticated readiness. The older run starts with only InitialTicketStorage; pending compiled migrations are applied by Development startup. The API remains usable after dotnet-watch rebuild, Angular live reload and a repeated full launcher startup. Migration history and Ticket activity are unchanged by repeat startup. The actual proxy rejects an untrusted backend certificate; no TLS bypass is used. Native safeguards also PASS, including cooperative68ms, forced5018ms/orphan5050ms, root-first/preflight/partial-start/122.2s timeout and parent environment/location restoration.

The new auth-development-observations.mjs drives Chromium through the actual Development Angular/API servers, using genuine signin and no token injection. It reproduces:

1. Operator-established Admin signin, current identity, refresh-cookie flags, reload rotation and Admin navigation.
2. Admin Agent creation through UI/API with a new assignment identity; Agent signin and Ticket creation/self-assignment/readable local actor history.
3. Admin rename; subsequent /me and mutation history use the new display name while prior actor snapshots remain identical.
4. Direct Agent Agent-management403; Customer and visitor boundaries are also checked by full API/browser suites.
5. Admin disable; explicitly unexpired old JWT gets401 on the next Ticket request, actual refresh gets401, choices exclude the Agent, and the saved assignment remains readable.
6. Admin re-enable; old JWT still fails, fresh signin/reload succeeds and assignment eligibility returns with the same reference ID.
7. Public Customer signup without automatic login, explicit signin/current Customer identity/reload, Ticket and Agent API403, route denial and signout revocation.
8. Admin signout while an independently signed-in Agent remains authenticated. One active tab per independently signed-in context.
9. Zero localStorage/sessionStorage credentials; JS cannot read the Secure HttpOnly Strict refresh cookie. UI and direct API checks agree.
10. Capture the Agent's actual pre-restart CSRF request token and cookie state only in the private owned manifest; close the browser, restart the API/Angular launcher, reuse the saved pair to refresh successfully, restore through the normal Angular initialization, verify identical Ticket history, and sign out.

The older-schema run additionally compares the encrypted Data Protection key-file hashes across normal restart; files are unchanged, contain encryptedSecret and no plaintext masterKey. No key contents are printed. Independent DataProtectionPersistenceTests pass2/2 in the complete SQL/API suite, using two real processes and retained CSRF/refresh with no token remint before verification.

## Framework keys and refresh coordination

DataProtectionConfiguration explicitly persists framework keys at `%LOCALAPPDATA%/ASP.NET/DataProtection-Keys`, sets discriminator Crm and uses current-user Windows DPAPI. Optional DataProtection:KeyDirectory remains durable and DPAPI-protected; owned test paths do not modify or delete the real shared ring. Missing Windows profile fails closed. This is the approved Windows configuration, not an ephemeral default or speculative production key service.

The complete37-case Playwright suite passes with the real five-minute JWT expiry; concurrent protected reads share exactly one refresh and retry at most once. Successful late old-token expiry uses the already-published JWT. New service/network failure cases delay an expiry response until after the shared refresh fails and verify no second rotation, no replay/loop, cleared role state and an unavailable diagnostic. The complete122-case Angular suite additionally proves signout/new-generation races, malformed envelopes/failed identity restoration and bounded retry without Web Locks/BroadcastChannel. Explicit signout failure stays visible; it is not a false server-revocation claim. Ordinary Ticket network failures retain valid auth for deliberate reload/review without replaying uncertain mutations.

Multiple simultaneously active tabs sharing the same authentication session are outside the supported scope of this feature. Separate profiles/contexts are independent; this is not cross-tab synchronization. Unknown/expired/replayed refresh credentials require fresh signin, with no delivery-loss retry grace. Password reset, credential changes, MFA/email verification and Customer Portal are excluded.

## Migration and recovery expectations

EF pending-model verification passes: no new migration is needed. Complete tests include11 Development migration and8 environment/CLI/design-time boundary cases: fresh, older/populated, current, repeat, model drift, absent artifacts, failure/cancellation, source stability and disposal. Source-controlled migrations/snapshot are unchanged by startup; no EnsureCreated or automatic migration generation/bootstrap. Non-Development and installation CLI schema preparation remain explicit.

Before upgrading a persistent catalog, take an operator-approved backup and test restore/upgrade on an owned copy. The approved transition changes legacy reference eligibility to false and adds null links without changing Ticket/history IDs/bytes or linking accounts. Restore the backup with its matching application version for rollback; an unreviewed migration downgrade dropping new account/session data is not a recovery guarantee. No real development catalog was reset or migrated merely for verification.

All assertions/rate limits/timeouts/lifetimes remain intact. No real participant study was attempted: Feature002 SC-005 is the verified scripted Agent workflow; Feature001 T079 remains pending and T081 provisional. Complete counts, cleanup and requirement results are linked from [final-validation.md](final-validation.md) and [requirements-coverage.md](requirements-coverage.md).
