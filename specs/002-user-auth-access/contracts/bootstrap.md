# Initial Admin Installation Contract

## Invocation

Planned command from repository root after explicit migration or successful Development migration startup:

`dotnet run --no-launch-profile --project src/backend/Crm.Api -- bootstrap-initial-admin`

The CLI prompts for display name and email, then uses a non-echoing password prompt; no password command-line argument or default config. Automation may use `bootstrap-initial-admin --input-stdin` with one JSON object {displayName,email,password} on private stdin from an operator-controlled secret source, not committed files, shell history or command substitutions. Never echo input, captured stdin or credential values. Interactive secure input is the default local setup path; no plaintext on disk. Values obey the same validators as signup/Agent creation.

Operator authorization is control of host execution and database connection; there is no public bootstrap route. Bootstrap does not issue JWT, set refresh cookie, start HTTP, migrate schema or generate signing keys. It shares validated ConnectionStrings:Crm/Windows identity and only required Infrastructure services, bypassing normal HTTP auth/migration startup. Missing schema is explicit setup failure, not EnsureCreated fallback. Signing configuration is needed for subsequent HTTP signin, not for writing the initial Identity password hash.

## Idempotence, atomicity and results

Begin transaction with serializable locking of singleton InitialAdminSetup Id1; if complete, return already_complete before requesting/using credentials. Otherwise validate new credentials, ensure normalized email is unused, create exactly one Admin through UserManager and write completion record; commit together. Concurrent invocations cannot create two initial Admins. Duplicate email cannot elevate/overwrite a Customer/Agent. Any validation/storage failure rolls back account and marker. No supported later Admin creation or credential-reset mode.

stdout contains only {status:"created"|"already_complete",userId:Guid}; no email/password/hash/security stamp. Prompts go to interactive terminal/stderr, never machine-readable stdout. Exit0 created/already_complete, exit2 invalid/missing input or identity conflict, exit3 database/schema/dependency failure; errors are safe and disclose no credentials. CLI cancellation fails without partial setup. Credential recovery is outside this feature; manual unsupported SQL edits are not a documented product recovery flow.

## Migration/setup integration

EF migrations contain schema/constraints only, never an Admin password or default Admin record. Development HTTP startup continues applying existing pending compiled migrations only; operator runs bootstrap separately once. Non-Development and CLI installations apply migrations explicitly before bootstrap. run-dev.ps1 does not provision/bootstrap/generate credentials. Repeated startup never overwrites Admin credentials or creates an additional Admin.

Verify blank/malformed/email-conflict/password boundary input, fresh/repeated/concurrent execution, injected save failures, cancellation, absent schema, unchanged existing Customer/Agent, absence of credential logs/argv and no new public route.
