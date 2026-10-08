# Quickstart Validation: Core Ticket Management

Revised prospective guide, 2026-10-08. Referenced implementation/scripts do not exist yet.
No setup, build, migration, provisioning, server or test commands were executed during planning.
After tasks and implementation, the steps below must run as documented.

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

## Manual Development Usage (Separate from Browser Harness)

Supply external issuer/client configuration through normal development settings, secrets through
user-secrets/environment, and approved reference input following
[reference-provisioning.md](contracts/reference-provisioning.md). From repository root:

```powershell
# Use the shared ConnectionStrings:Crm and current Windows account for both commands.
dotnet ef database update --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api
dotnet run --project src/backend/Crm.Api --no-launch-profile -- provision-ticket-references --input $env:CRM_REFERENCE_INPUT
```

Start API under that same Windows account and connection with normal development configuration, and Angular via npm start in separate terminals.
Use production-style OIDC sign-in. No Testing identity fallback or administration UI is required.
The provisioning command exits without starting HTTP, and failure leaves all references unchanged.

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
