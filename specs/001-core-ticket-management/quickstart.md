# Quickstart Validation: Core Ticket Management

Revised validation guide, 2026-10-09. Development startup migrations are implemented and verified
in validation/migration-startup.md. The one-command launcher/proxy is implemented;
launcher verification is recorded in validation/local-development.md. Real
external-provider browser sign-in and SC-005 human acceptance remain separately pending.

## Prerequisites

- .NET 10 SDK; pinned dotnet-ef/EF Core 10; Node 24.20.0 or compatible Angular 22 runtime.
- Local SQL Server 2022, Windows-hosted API/tools and the same existing Windows account for all DB access; development database plus distinct disposable integration/E2E databases.
  EF InMemory/SQLite cannot prove SQL rowversion, locks or collations.
- CRM_TEST_SQL_CONNECTION for integration tests; CRM_E2E_SQL_CONNECTION for browser tests,
  supplied as credential-free integrated-security configuration/environment. Database names must respectively start CrmTicketsIntegration_
  and CrmTicketsE2E_, and be dedicated to this run. The harness checks actual target server/
  database against the approved test configuration before any reset. Never infer approval from
  a name alone, operate on production/shared databases, or log connection strings.
- Backend/frontend browsers use HTTPS development certificates. Run dotnet dev-certs https
  before tests to create the API development certificate; Angular's e2e --ssl server uses its
  own local development certificate. Automated tests allow self-signed certificates only for
  their explicitly configured local endpoints; production does not disable TLS validation.
- For manual usage only: external issuer, audience, registered SPA client/redirect/scopes,
  queue-authorized staff identities, and approved reference input. Browser automation uses
  ephemeral signed test identities instead, with no external login service dependency.

## Shared Windows Connection Setup and Verification

Configure ConnectionStrings:Crm once using local SQL Server and Integrated Security=True
(or Trusted_Connection=True), for example:
`Server=localhost;Database=CustomerSupportCrm;Integrated Security=True;Encrypt=True;TrustServerCertificate=True`.
No User ID/Password. The local certificate exception is only for the local self-signed SQL
instance; do not carry it into an environment with normal trusted-certificate validation.

Run API, dotnet ef and provisioning CLI under the same Windows account and identical connection
value. Keep the one key in credential-free local configuration or ConnectionStrings__Crm;
no per-process DB connection credentials. The design-time factory loads that same key without
starting HTTP. Missing Windows-account SQL access is an environment prerequisite; no automatic
SQL-password fallback, new principal/grant script or identity-switching setup is introduced.

[database-principals.md](contracts/database-principals.md) describes the simplified shared
configuration and intentional trade-off: database least-privilege separation is not implemented.
History and reference restrictions are enforced through supported application operations, not
by expecting the same SQL login to have different permissions in different processes.

Validate integrated-security/no-user/no-password configuration in every entry point and compare
Windows host identity with SQL ORIGINAL_LOGIN()/SUSER_SNAME()/catalog across API, EF and CLI.
Exercise real migrations, bounded upserts and normal ticket/history journeys through that shared
identity. Keep application-level unknown/delete/admin/fallback rejection tests; remove three-user
mapping/grant/effective-denial tests. Do not expect a direct SQL write to be blocked by this design.

For isolated integration/E2E runs, change only the catalog to the guarded test database; every
fixture and child process in the run uses that same connection/account. Validate the actual local
server/catalog and owned database before reset, never reset the development catalog, and retain
fail-fast missing-SQL behavior. No separate administrative/runtime/provisioning test credential.

## Build and Unit/Integration Checks After Implementation

From repository root:

```powershell
dotnet tool restore
dotnet restore Crm.slnx
dotnet build Crm.slnx --no-restore
dotnet test Crm.slnx --no-build
Push-Location src/frontend
npm ci
npm run build
npm test -- --watch=false
npx playwright install chromium
Pop-Location
```

Expected: domain/application/real-SQL integration/architecture tests and frontend build/unit
tests pass. Integration fixtures apply reviewed migrations and synthetic data only to their
approved isolated database. Missing SQL configuration/service fails the suite, not a passing skip.
Include genuine JWT validation tests in addition to use-case fakes. Secrets stay outside source.

## Reproducible Browser Test Startup

From repository root, after the build commands:

```powershell
Push-Location src/frontend
npm run e2e
Pop-Location
```

Planned e2e script invokes a small test runner then Playwright; no pre-running servers needed:

1. Validate CRM_E2E_SQL_CONNECTION and exclusive ownership of the configured disposable DB.
   Fail on missing config/service, unsafe target or occupied server ports. Use one worker
   initially; do not attach to another run's queue/database.
2. Generate an ephemeral RSA signing key and short-lived JWTs for staff-a/staff-b with
   crm_support_queue=shared, and staff-denied without it. Include issuer=https://example.test,
   audience=crm-tickets-e2e, stable subject and display name. Store private keys/tokens only
   in restricted temporary fixture storage; no URL/query parameter, source or trace output.
3. Prepare the isolated database before starting servers: reset only its authorized test data,
   apply migrations using the test connection, and invoke the reference provisioner with a
   generated version-1 synthetic input. Seed available and inactive non-default categories/
   agents with stable fixture IDs; fallback is migration-owned and never disabled. The runner
   forwards the same integrated-security test connection and inherited Windows identity to EF/provisioning/API children; JWT fixture keys remain private and are not put on command lines. No DB credentials or identity changes are generated.
4. Playwright webServer has two entries with explicit cwd and reuseExistingServer=false:
   - API cwd=repository root; command `dotnet run --no-build --no-launch-profile --project
     src/backend/Crm.Api`; ASPNETCORE_URLS=https://localhost:7043. Set both DOTNET_ENVIRONMENT and
     ASPNETCORE_ENVIRONMENT to Testing, plus test issuer/audience/public signing key and
     the isolated DB connection. Readiness URL=https://localhost:7043/api/ticket-reference-data/categories, timeout
     120 seconds; 401 proves the HTTP/auth pipeline is listening without a health route.
   - Angular cwd=src/frontend; command `npm run start:e2e`, defined as Angular serve with
     configuration=e2e, port=4200, ssl=true and a test-only `/api/**` proxy to the API above.
     Readiness URL=https://localhost:4200/, timeout 120 seconds. Proxy secure=false only for
     the local self-signed upstream. No production proxy/TLS relaxation.
5. Set webServer.ignoreHTTPSErrors=true for local readiness and Playwright use.ignoreHTTPSErrors
   for these browser/request contexts. These settings do not modify application TLS policies.
   Server entries start concurrently; do not rely on array order. Before ticket scenarios,
   an authorized request must retrieve fallback/reference data and prove SQL/seed readiness.
6. Testing backend uses real JWT signature/issuer/audience/expiry validation against the
   ephemeral key, not an always-successful auth scheme. Test-key configuration is accepted
   only in exact Testing environment; production rejects it and requires its external issuer.
   Angular e2e build replaces only the core auth adapter: a fixture initialization script
   supplies the context token before bootstrap, stores it in memory, and sends it only to
   same-origin /api requests. Production build excludes this adapter and uses OIDC/PKCE.
7. Use separate browser contexts for both authorized staff, denied staff and unauthenticated
   requests. Test valid/invalid JWTs and the same production SupportQueue policy. Protect token
   material from screenshots/traces; persist no storage state. External IdP login itself is
   not claimed as tested by this harness and remains a separate manual deployment check.
8. Playwright owns server shutdown on success/failure. Runner finally removes ephemeral
   credentials/temp inputs, disposes owned SQL fixture resources, and returns the failing
   exit code on setup/server/test failures. Leave no background server or committed test key.

All database child processes receive the same ConnectionStrings__Crm test value with integrated security and run under the same Windows account;
Authentication__TestIssuer=https://example.test; Authentication__TestAudience=crm-tickets-e2e;
Authentication__TestPublicKeyPath points to a generated public-key file. The private signing
key stays in the runner/fixture, never the API/frontend. Production rejects these Test-prefixed
authentication settings; normal issuer/audience/client settings remain separate. Fixture paths
are passed through private environment/config, not URLs. Explicit server URLs and proxy settings
must agree; use /api/ticket-reference-data/categories for readiness in every story phase. Its unauthenticated 401 proves HTTP readiness only; authorized 200 containing the available fallback proves SQL/seed readiness. Never change this URL to a later-story endpoint.

## Story Checkpoints for Readable History

- US1 must expose/render Created snapshot, actor/time and initial values.
- US3 must also project/serialize/render DetailsChanged and AssignmentChanged, including actual
  before/after text/priority/category/agent values and null clearing, before US3 is complete.
- US4 must also project/serialize/render StatusChanged for resolve/close/reopen before its checkpoint.
- US5 verifies comprehensive mixed-history review, pagination and presentation/snapshot regressions.
  It must not be the first implementation of any earlier story's history readability.

At each mutation checkpoint, verify new entries through both GET history and the actual detail
page, not solely database row inspection. The shared component/query evolves with the producing
story; the REST payload shape remains unchanged. Failure/no-op attempts still add no history.

## One-Command Local Development

Supply external issuer/client configuration through normal development settings, secrets through
user-secrets/environment, and approved reference input following
[reference-provisioning.md](contracts/reference-provisioning.md). Perform one-time setup using
the restore/certificate/OIDC prerequisites in docs/development/core-ticket-management.md:
PowerShell 7, pinned SDK/Node/npm, restored tools/backend/frontend dependencies, local SQL access,
trusted browser certificates, backend HTTPS Authority/Audience and registered frontend public
issuer/client/scopes. NODE_EXTRA_CA_CERTS must name the exported public API certificate if Node
does not otherwise trust it; do not store private certificates or secrets in the proxy.
No eligible accounts/issuer/client were supplied for this training project. The launcher does
not create them or replace normal authentication with test identities. These are prerequisites
for manual authenticated use, not additional commands to run on every normal startup.

Configure the existing ConnectionStrings:Crm via normal environment/user-secrets/configuration
(the example shows a credential-free environment value). Remove Authentication__Test* settings.
Complete the private PEM export setup in the development guide once; the launcher uses
.local/dev-certs/localhost.pem and localhost.key for Angular HTTPS. Alternative PEM paths use
CRM_DEV_SSL_CERT/CRM_DEV_SSL_KEY. The public certificate supplies child-only NODE_EXTRA_CA_CERTS
unless already configured; no key is bundled or committed. The single repeatable start command is:

```powershell
$env:ConnectionStrings__Crm = 'Server=localhost;Database=CustomerSupportCrm;Integrated Security=True;Encrypt=True;TrustServerCertificate=True'
# Once the normal configuration is set, each startup requires only:
./scripts/run-dev.ps1
```

Expected: before HTTP listens, Development startup awaits MigrateAsync against the configured local
catalog and applies only pending migrations already compiled from reviewed source-controlled files.
This includes schema/fallback and SQL-only migrations, not just model changes. A repeat start applies
nothing already recorded in migration history. Failed/cancelled migration or pending model changes
without a generated migration prevents startup; do not bypass the failure with EnsureCreated.

Verified launcher behavior: reuse scripts/environment.ps1; start dotnet watch in Development on
https://localhost:7043 and normal Angular development serve on https://localhost:4200 in the same
console, with both live log streams. Browse to the frontend URL. Requests use relative /api and
the development proxy forwards them unchanged to 7043 with API certificate validation enabled.
No backend URL is added to ticket feature code. The e2e proxy/auth configuration is not used.
Initial HTTP readiness requires categories 401 and Angular HTML 200 within 120 seconds; this is
server availability, not authenticated DB acceptance. After sign-in, categories must return 200
with the fallback and the normal ticket journey must work through the frontend origin.

Ctrl+C gives both owned services one shared five-second grace budget from the shutdown request,
then force-stops any remaining owned descendants. Report graceful exits and forced cleanup
separately; cooperative and uncooperative fixtures must exercise both outcomes. Partial start,
timeout or child exit requests owned-child shutdown where safely supported with the same budget;
waiting alone is not evidence of a graceful stop. Partial start, startup timeout or
terminal child exit also cleans up the other child. Ports already in use fail before launch;
never attach to or stop their owners. A failed API build may leave dotnet watch alive, so a watch
banner alone is not startup success. Logs remain visible through rebuilds/restarts. Restarting
after Ctrl+C must reuse the same ports with no owned process left. Abrupt terminal closure is
outside the graceful-stop guarantee. No launcher action removes the development database.
See [local-development.md](contracts/local-development.md) for the lifecycle boundary.

Use the normal external OIDC settings, not Testing authentication. Once migration startup has completed, an operator may run
the provisioning CLI in a separate shell using the same ConnectionStrings__Crm/Windows account.
Alternatively, explicitly apply existing migrations before provisioning if the API has not started:

```powershell
# Same validated ConnectionStrings:Crm and Windows account in this shell.
# Optional explicit setup when provisioning before the first Development API start:
dotnet ef database update --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api
dotnet run --project src/backend/Crm.Api --no-launch-profile -- provision-ticket-references --input $env:CRM_REFERENCE_INPUT
```

CLI mode always exits before normal HTTP startup and never automatically migrates, even in Development.
Its applied-schema/fallback prerequisite remains; failures leave reference upserts unchanged. Other
environments require explicit migration application before normal usage. Testing fixtures and browser
preparation retain their existing explicit, guarded migration step; do not switch them to Development.

### Explicit Model Changes

After deliberately changing the model, generate and review a migration before committing/building:

```powershell
$taskMigrationName = Read-Host 'Name for the deliberate model change'
dotnet ef migrations add $taskMigrationName --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --output-dir Persistence/Migrations
dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api
```

Version the migration, designer and updated snapshot as applicable. Rebuild to include them, then
Development startup applies the pending migrations. These are explicit developer commands; the
API never runs migration-generation tools, rewrites migration files or infers schema from the model.

### Startup Migration Validation After Implementation

Use exclusively owned disposable local SQL catalogs and the existing isolation rules. Do not reset
the development catalog or infer cleanup permission from a name prefix.

| Scenario | Required observation |
| --- | --- |
| Development, fresh catalog | Existing compiled migrations create schema/fallback/search function before protected readiness |
| Development, catalog at an older migration | Pending compiled migrations apply; existing ticket/history data remains readable |
| Repeated Development start | No duplicate migrations, reference provisioning or new ticket activity |
| Model changed without a generated migration | Startup fails; no inferred schema or new migration/source files |
| Migration/connection failure or cancellation | Startup fails before HTTP readiness; scope is disposed; sanitized setup diagnostics |
| Production, Staging, Testing and another non-Development environment | No automatic migration invocation or migration-history/schema change, even with pending migrations |
| CLI/design-time operation under Development | No HTTP startup hook; CLI still requires applied migrations; EF generation/update occurs only when explicitly invoked |

Inspect applied migration history and necessary schema objects rather than interpreting a 401 as
database readiness. After successful Development migration, use the same categories readiness URL
as the browser harness, including an authorized 200/fallback probe. Keep authentication configuration
valid during environment-isolation checks so an unrelated auth failure cannot falsely prove the guard.
Inspect migration/source files before/after startup to verify no generation; no runtime Git checkout is
required. Existing migration verification is recorded in validation/migration-startup.md;
retain these checks when validating the launcher rather than rewriting the earlier evidence.

### Launcher Validation After Implementation

Use owned disposable local SQL catalogs for automated checks and preserve the existing
Testing harness for business regressions. Do not provision test keys into normal Development.

| Scenario | Required observation |
| --- | --- |
| One root command with valid prerequisites | Both URLs reachable; API categories 401 and frontend HTML 200; both live log streams visible |
| Automated authenticated proxy/API journey | Private external HTTPS discovery/JWKS fixture supplies signed tokens directly to HTTP test client; normal Development Authority/Audience/JWT middleware validates them; frontend-origin /api categories and ticket lifecycle/history calls succeed |
| Authenticated manual ticket journey | Frontend /api requests reach backend; authorized categories 200/fallback and create/view/edit/lifecycle/history work; missing/denied access still 401/403 |
| API source edit requiring restart; frontend source edit | Watch rebuild/restart and Angular reload occur; existing compiled migrations are not reapplied |
| Model change lacking explicit migration | Development API fails as before; launcher does not generate migrations or use EnsureCreated |
| Occupied port or missing tool/dependencies | Actionable setup failure without spawning partial services or affecting an unrelated process |
| Proxy certificate not trusted | TLS failure reported with trust setup guidance; no insecure automatic fallback |
| API migration/build failure | No false-ready claim; bounded startup timeout/child exit cleans owned children |
| Ctrl+C, partial launch, terminal child exit | One shared five-second grace budget; separate graceful/forced outcomes; cooperative exit and uncooperative owned-tree fallback; ports free, no orphan watcher/API/npm/Angular children; unrelated process survives |
| Start again after cleanup | Both services start on the same fixed ports; caller environment/location unaffected if invoked in-process |
| Production and Testing regressions | No new proxy/auth substitutions in production; no non-Development auto-migration; existing explicit E2E preparation unchanged |

Observe descendant process ownership, not just parent PID exit. Do not reset the developer's
catalog or kill processes by name/port. Record failures and final results in later launcher
validation evidence; launcher results are in validation/local-development.md. External OIDC
manual evidence remains a deployment prerequisite; SC-005/T079 and provisional T081 are unchanged.

The automated authenticated journey uses HTTP calls through Angular's proxy, not browser PKCE
sign-in. Discovery/JWKS alone does not implement login. Do not inject fixture tokens into SPA
storage, replace the normal auth adapter or set API Authentication__Test* values. Separately
check normal frontend HTML/routing/unauthenticated guard/reload. Genuine browser sign-in/callback
and authenticated UI journeys require external registered client/accounts; record them as pending
if unavailable. Existing Testing Playwright runs retain their separate scope and evidence.

## Required End-to-End Scenarios

Use [tickets-rest.md](contracts/tickets-rest.md) and [data-model.md](data-model.md) as contracts.

1. **Create/view (SC-001)**: Valid create yields unique stable reference and Open/Normal/
   Uncategorised/unassigned; blank required input creates nothing. Detail contains current
   values, editVersion and accessible history. Default creation succeeds when other categories
   are inactive; fallback delete/deactivate/rename provisioning attempts fail atomically.
2. **Track (SC-002)**: Test all status/priority/category/assignment states, exact reference,
   Arabic/Latin literal case-insensitive subject search, combined filters, empty results,
   page bounds and stable ties. No department/branch/assignee access restriction.
3. **Inactive references (FR-005/006/007/009)**: Provision reference inactivation after saving
   a ticket. Confirm ticket version/time/history are unchanged. Edit subject/description/
   priority and explicitly resubmit unchanged inactive IDs; all succeed as applicable.
   Equal values remain a no-op. Close/reopen while retaining inactive references succeeds.
   Newly selecting an inactive ID fails, including after replacing/removing it earlier.
   Replace with active values or clear an inactive agent; history records only actual changes.
4. **Reference changes and version metadata**: Change category/agent label or active flag;
   GET detail reflects the new reference information with unchanged ticket editVersion.
   No ETag/Last-Modified response is present. This is not a representation validator. New
   selections still check current eligibility under lock, including provisioning races.
5. **Lifecycle (SC-003)**: Six allowed unequal transitions, other unequal pairs rejected,
   four equal-status no-ops. Reopen preserves ticket reference/details/assignment. Closed
   detail/assignment edits reject even if identical. No required assignee/customer approval/note.
6. **History (SC-004)**: Assert the exact creation and field-discriminated payload shapes,
   string revision, ordered sequence, actor/time and before/after snapshots. Multiple changed
   details share one revision. Later reference changes never rewrite history. No history
   edit/delete operation; failed/no-op writes add no entries.
7. **Concurrency/atomicity (FR-011/012)**: Two clients read the same editVersion. First writes;
   second sends stale expectedVersion and gets 409 ticket_changed with unsaved inputs retained.
   Missing/malformed token gets 400 invalid_version. Refresh/review/explicit retry succeeds.
   Stale no-ops also fail. Test simultaneous real/no-op writes, failure rollback/no orphan
   history, cancellation cleanup, lock/deadlock transient failure without replay, and token/
   returned-detail consistency. Lost-response guidance does not claim rollback certainty.
8. **Access (FR-013)**: Staff-a/staff-b handle unassigned/each other's tickets. Missing/invalid/
   expired/wrong-issuer/wrong-audience tokens fail; valid staff-denied gets 403 on every ticket,
   history and reference route. Client guards never bypass server policy; production cannot
   use the test identity adapter/configuration.
9. **Provisioning**: Repeated file is no-op; omissions retain rows; invalid duplicate/protected/
   rebind input changes nothing; no delete or permission input is accepted. No ticket/history
   changes from reference upserts. Failures exit without starting HTTP or partially committing.
10. **Platform**: Unicode input, narrow layouts, keyboard controls and logical CSS remain usable.
    No customer/branch/department management or other future module is introduced.
11. **Staff evaluation (SC-005)**: Follow the repeatable protocol below with five eligible
    participants. At least four must meet the unchanged independent-completion/owner/status
    criterion. No automation or simulated participants can claim this passed.

## Repeatable SC-005 Evaluation Protocol

This protocol defines observation, not new product behavior or an extra performance target.
Apply it only after all stories and automated gates pass; external OIDC/manual deployment
readiness is verified before participants arrive. Use synthetic issues/agents and an evaluation
instance with the same reviewed build, permissions, fixture choices and viewport for all five.

### Participants and Preparation

Select five current or intended ticket-handling support staff who use browser software in
ordinary work. None may have implemented/designed this feature or previously piloted its scored
journey. Recruit before testing without selecting only expert product users. Record each person's
support role and broad prior helpdesk experience, preferred available presentation language and
accessibility/input needs, using anonymous IDs P1-P5. No customer/personnel data in the report.
Existing general CRM experience is allowed; the feature-specific scored journey must be new.

Each participant gets a separate authorized account, a clean synthetic ticket case and the same
available Evaluation Agent/category choices. Clear prior participant case data through fixture
setup, not product deletion functionality. Record build identifier, fixture version and environment.
Use identical task-card wording and introduction material; no repeat coached attempts in the score.

### Standard Five-Minute Introduction

Read the following introduction and demonstrate once using a separate, clearly labelled practice
ticket (never the participant's scored case). Deliver the same five-minute session to everyone;
record its actual duration. Clarify vocabulary before the scored attempt using the same script.

“This tool records and tracks support tickets. Tickets have a reference, subject, description,
category, priority, an optional assigned agent and a status. You can create and retrieve a ticket,
assign an agent, mark an issue resolved, close it and reopen it. Its history records changes.
The upcoming exercise asks you to achieve those outcomes using the application on your own.”

Show where the tickets feature opens, then demonstrate create/retrieve/assign/resolve/close/reopen
and the displayed owner/status/history on the practice ticket. Do not give a personalized walkthrough,
print a click-by-click answer sheet, or permit rehearsing the scored case. All are instruction for
an existing workflow; they create no new validation fields, lifecycle transitions or controls.

### Scored Task Card and Allowed Assistance

Give each participant the same outcome-based card with their unique synthetic issue text and the
known available Evaluation Agent name. The card lists goals, not where to click:

1. Create a ticket with the supplied subject/description and default category/priority.
2. Retrieve that ticket using its recorded reference.
3. Assign it to Evaluation Agent.
4. Mark the issue resolved, then close the ticket.
5. Reopen the same ticket.
6. State its current assigned agent and status.

The expected final agent is Evaluation Agent and status Open; intermediate steps must also be
verified. A participant may use the application's displayed information and correct their own
mistakes without observer guidance. Starting work before resolution is allowed but not required.
No new assignee/note/approval prerequisite or timing target is imposed.

During the scored attempt the observer may repeat the task-card wording verbatim or clarify a
predefined term from the introduction. They must not identify controls, recommend the next action,
operate the application, or answer the final owner/status questions. Record every request and reply.
Any procedural assistance makes that participant's attempt unsuccessful for SC-005, even if the
remaining sequence is completed. Coaching after the score is recorded is allowed for learning,
but a coached retry cannot replace the first valid attempt.

### Recording, Exceptions and Pass Rule

Record anonymous participant ID, eligibility/experience, build/fixture/environment, introduction
version/duration, each step completed independently, any assistance, final owner/status answer,
observed correctness, and overall success. Duration may be observed but is not a pass threshold.
Check the actual saved ticket/reference/status/assignment and history for steps; do not score
solely from the participant's claim. Keep notes/screenshots free of personal/customer data.

Overall success requires every card outcome plus correct final owner/status without procedural
assistance. The run passes SC-005 only if at least four of five eligible participants succeed.
Record all valid failures; do not discard them to improve the denominator. A demonstrable external
setup interruption (e.g. broken test network/fixture before a valid attempt) is recorded separately,
repaired, and replaced with a new eligible participant. Product behavior defects count as valid
unsuccessful outcomes, not setup exclusions. Any later evaluation run keeps the original results
and labels the new build/run separately; no retroactive score editing.

Store actual results during implementation/evaluation in
specs/001-core-ticket-management/validation/staff-evaluation.md. Until five real observations exist,
mark SC-005 pending and do not claim full acceptance. This planning document contains no outcomes.

## Advisory Benchmark (Not a Completion Gate)

Optionally measure synthetic 10,000-ticket/20-active-staff p95 reads under 1 second and
mutations under 2 seconds, excluding external login. Record environment/results for tuning.
Omitting or missing these inferred targets does not fail feature completion unless a later
specification explicitly adopts them. No SLA automation/reporting feature is introduced.

## Completion Evidence

Record required build/tests, applied migrations, reference/fallback checks, SQL races/rollback,
browser journeys and actual staff evaluation. Check all six constitutional gates and FR/SC
traceability. Advisory benchmark results are separate. No setup or implementation verification
has run in this planning phase; design review is not proof of working software.
