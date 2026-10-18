# Story 14 — Security & Administration — User Management, Effective Deactivation, Read-Only Permission Matrix, Administrative Audit Logs & Organization Settings

## Prerequisites

- **Story 13 completed and committed:** [../reports-management/13-story-reports-management.md](../reports-management/13-story-reports-management.md) (`REPORT-001`).
  - At planning time `git log --oneline -3` showed `3023f1e feat(reports): … (REPORT-001)` on top of `master`.
  - `git status` showed only the untracked `.squad/stories/security-administration/` and `.squad/plans/security-administration/` folders.
  - That confirms intake §2: **the REPORT-001 commit exists.**
  - **Before implementing, run `git status` and `git log --oneline -3`. Stop and report if any application file is modified or `REPORT-001` is missing from `git log`.** Do not depend on the exact commit id.
- **Story 02 completed:** [../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md) (`AUTH-001`). It already delivered most of the account machinery this story builds on. These facts were verified in code during planning:
  - **Revocation of tokens issued before deactivation already exists.** `JwtBearerEvents.OnTokenValidated` (`backend/src/Crm.Api/Authentication/SecurityRegistration.cs` lines 38–58) re-reads the account on **every** authenticated request through `IIdentityService.FindByIdAsync`. It fails the token when the account is missing, when `!user.IsActive`, or when the `sstamp` claim differs from the stored `SecurityStamp` (line 53). This runs in the shared JWT scheme, so it covers every protected controller (Tickets, Portal, KB, SLA, AI, Reports, Agents, …). Anonymous routes (`AuthController`, `HealthController`, both `[AllowAnonymous]`) are not affected.
  - `IdentityService.SetActiveAsync` (`backend/src/Crm.Infrastructure/Identity/IdentityService.cs` lines 132–139) sets `IsActive` **and** rotates the security stamp (`UpdateSecurityStampAsync`).
  - Login refuses inactive accounts (`CheckCredentialsAsync` line 47 ⇒ the uniform 401 "Invalid email or password.", `LoginCommand.cs` line 24). Refresh refuses them and revokes the family (`RenewSessionCommand.cs` lines 92–102).
  - **Agent provisioning already exists:** SuperAdmin-only `POST /api/v1/agents` (`AgentsController.cs` lines 33–42). `CreateAgentCommandHandler` (`backend/src/Crm.Application/Agents/CreateAgent/CreateAgentCommand.cs` lines 13–29) always assigns `UserRoles.Agent` **server-side** (line 27). The request has no role field (`CreateAgentRequest.cs` line 3). The SuperAdmin sets the initial password, which is validated by `AccountRules.NewPassword` (8–128) and the Identity password options (`Infrastructure/DependencyInjection.cs` lines 63–78). There is no outbound email.
  - **Agent status toggle already exists:** `PUT /api/v1/agents/{id}/status` → `SetAgentStatusCommand` (`backend/src/Crm.Application/Agents/SetAgentStatus/SetAgentStatusCommand.cs` lines 9–28). It is `ITransactionalCommand`, calls `SetActiveAsync` and revokes refresh sessions on disable. It is restricted to Agent accounts by `AgentLookup.GetAgentOrThrowAsync` (`AgentLookup.cs` lines 10–14). **It has no audit, no no-op handling and no row lock.**
  - **Roles are fixed after creation:** the only role writes are `AddToRoleAsync` at creation (`IdentityService.cs` line 78) and at bootstrap (`IdentityBootstrapper.cs` line 73). There is no `RemoveFromRoleAsync` anywhere. **This story keeps it that way.**
  - **Identity and CRM data share one `CrmDbContext`** (`CrmDbContext.cs` lines 17–18: `IdentityDbContext<ApplicationUser, …>`). `UserManager` saves on the request-scoped context, so inside an `ITransactionalCommand` its writes join the transaction (`IIdentityService.cs` lines 3–7; `docs/persistence-and-transactions.md` line 12). **An audit row and an Identity change can therefore be committed atomically.** No redesign is needed, so the AD3 "stop and report" trigger does not fire.
- **Stories 04–05, 09–13** must not regress. Ticket assignment and history live in `Tickets` / `TicketActivities` and are **not touched**. `TicketActivity` stays the ticket audit (`TicketActivity.cs` lines 3–8). REPORT-001 relies on "roles never change", and this story preserves that.
- Applied migrations end with `20261018124705_AddTicketFeedback`. **This story adds one additive migration, `AddAdministrationAuditAndSettings`, with two new tables.** No existing table or column changes.
- Tooling: .NET SDK 10, EF Core 10 (SQL Server), Mediator (source-generated), FluentValidation, xUnit 2.9.3 (`Crm.UnitTests.csproj` references `Crm.Application` only), Angular / Material 22, Vitest via `ng test`.
  - **Baseline tests at `3023f1e`: backend 13 `[Fact]`, frontend 13 `it(`** (both counted during planning). That matches intake §11.
  - **No new NuGet or npm package.**
- Intake: `.squad/stories/security-administration/ADMIN-001/intake.md` (`attachments/` is empty). Business decisions **AD1–AD4 are approved and not reopened.**
- Shared contracts touched (additive unless noted):
  - Domain: new folder `Crm.Domain/Administration/`.
  - Application:
    - new folder `Crm.Application/Administration/`;
    - new ports `IAccountAdministration`, `IAdminAuditRepository`, `IOrganizationSettingsRepository`;
    - `AdministrationRules`;
    - **changed:** `CreateAgentCommand` gains `ActorUserId` and writes the audit;
    - **removed:** `SetAgentStatusCommand` (replaced by the audited `SetAccountStatusCommand`; the `/agents/{id}/status` route is kept).
  - Infrastructure:
    - two configurations, two repositories, one Identity adapter, 2 `DbSet`s, 3 DI lines, 1 migration;
    - `IdentityService.ThrowIfFailed` maps `ConcurrencyFailure` to 409.
  - API:
    - new `AdministrationController` under `/api/v1/admin`, plus `Contracts/Administration/AdministrationContracts.cs`;
    - `AgentsController` passes the actor;
    - `EntityTags` gets a generic 428 message.
  - Angular:
    - `app.routes.ts` (+1 route), `NAV_ITEMS` (+1 SuperAdmin entry);
    - a new `features/admin/` folder.
  - Docs.
- **No change** to: `SecurityRegistration` (the revocation check is already correct), `JwtTokenService`, `IIdentityService` (so the four existing `FakeIdentityService` test doubles stay untouched), the Ticket/SLA/KB/AI/Portal/Reports code, or any existing test.

---

## Story Goal

Deliver the four **Security & Administration** areas for **SuperAdmin only**, using the existing identity stack:

1. **Users (AD1):**
   - `GET /api/v1/admin/users` returns a paged, searchable list of **all** accounts (SuperAdmin, Agent, Customer): name, email, fixed role, active status, created date. It can be filtered by role and status.
   - `GET /api/v1/admin/users/{id}` returns one account.
   - `PUT /api/v1/admin/users/{id}/status` deactivates or reactivates any account. It has anti-lockout rules (not yourself, not the last active SuperAdmin), an idempotent no-op, a row lock against concurrent toggles, and an atomic audit entry.
   - Agent creation stays on the existing `POST /api/v1/agents` (role forced to Agent server-side), which now writes an `AgentCreated` audit entry atomically.
   - A deactivated account's **previously issued JWT is refused on every protected endpoint**. This is the existing `OnTokenValidated` check: verified, documented and tested manually, not rebuilt.
2. **Roles & Permissions (AD2):** a read-only Angular page. The rows are CRM modules and abilities; the columns are the 3 fixed roles. Each cell carries an explicit scope label ("Assigned tickets only", "Own linked profile", "Published + Public only", …). It is a maintained static presentation of the policies that exist. There is no endpoint, no permission table and no toggle.
3. **Audit Logs (AD3):**
   - A new append-only table `AdminAuditLogs` with exactly 4 actions: `AgentCreated`, `AccountDeactivated`, `AccountReactivated`, `OrganizationSettingsUpdated`.
   - Each entry stores the actor id, the target type, id and label, the UTC time, and a bounded list of safe field changes.
   - Each entry is written in the **same database transaction** as its business change.
   - `GET /api/v1/admin/audit-logs` lists the entries newest first, filtered by action, UTC date range and search (actor/target id, name, email or label), and paged.
   - There is no edit or delete API.
4. **Organization Settings (AD4):**
   - A new singleton table `OrganizationSettings` (row Id 1, inserted by the migration with all values null, so it starts as "not configured").
   - The fields are display name (required, ≤ 150), support email (optional, valid, ≤ 256), support phone (optional, ≤ 40) and support information (optional, ≤ 1000).
   - `GET`/`PUT /api/v1/admin/organization-settings` use ETag / `If-Match` optimistic concurrency.
   - A save writes exactly one audit entry listing the changed fields with their old and new values. Saving identical values changes nothing and writes no audit.
5. **Angular:** one SuperAdmin-only **Administration** route group (`/admin/users`, `/admin/users/:id`, `/admin/permissions`, `/admin/audit-logs`, `/admin/settings`) with a section nav. It has loading, empty, error and Retry states, confirmations before status changes, and works at 400 px.
6. Exactly **one** new backend unit test and **one** new frontend unit test (13 → 14 each), a new `docs/security-administration.md`, small doc updates, and a manual API/SQL/browser matrix mapped to intake §11 checks 1–20.

**Not in scope** (do **not** build):
- Role changes after creation, promote/demote, editable permissions or a permission table.
- Account deletion, invitation email/SMS, forgot-password or MFA.
- Auditing of ticket/KB/SLA/AI actions, logins, failed logins, Agent profile edits or Agent password resets (see C6).
- Audit export, retention or SIEM.
- Exposing the organization settings in the portal, navbar, branding or messages.
- Automatic reassignment of a deactivated Agent's tickets.
- A new `/admin/agents` creation endpoint (C3).
- Communication Channels (stays last).

### Conflicts and decisions (surface these in the implementation report)

| # | Intake point | Resolution in this plan |
|---|---|---|
| C1 | §5 "Effective JWT revocation": inspect whether `IsActive` is checked only at login. | **It is already checked on every request.** `SecurityRegistration.cs` lines 41–56 load the account through `IIdentityService.FindByIdAsync` (`IdentityService.cs` lines 32–33, one indexed PK read) and fail the token when `!user.IsActive` or the stamp differs. Deactivation also rotates the stamp (`IdentityService.cs` line 138) and revokes all refresh sessions. **No change is made to the pipeline.** There is no cache, so there is no window where a deactivated user keeps access. The failure is a plain 401 with no detail about the account ("Account state changed." is only the internal `context.Fail` reason, never sent to the client). Verification steps 7–8 prove it with retained real tokens for an Agent **and** a Customer. |
| C2 | §5 "Secure initial access": smallest safe approach. | **Keep the existing approach:** the SuperAdmin types the initial password in the existing `/agents/new` form. It is sent once over HTTPS, hashed by Identity, never stored in clear and never echoed back (`AgentResponse` has no password field). The SuperAdmin hands it over out of band, and the Agent changes it in Profile → Change password (`POST /api/v1/me/password`). `LoggingBehavior` logs only the message type name (`LoggingBehavior.cs` lines 16–25), and there is no HTTP body logging (`Program.cs` has none), so the password never reaches the logs. **The audit entry never contains it** (C7). No invitation or reset workflow is built. **Documented limitation:** no forced change on first login (same as `docs/identity-and-access.md` line 125). |
| C3 | §9 illustrative `POST /api/v1/admin/agents`. | **Not added.** `POST /api/v1/agents` already is the SuperAdmin-only, role-forced provisioning endpoint. A second route would duplicate the same use case and give two places to keep audited. The Admin Users page links to the existing `/agents/new` form, and the existing endpoint gains the audit (task 6). One endpoint means one code path, so the audit cannot be bypassed. |
| C4 | AD1: deactivate **any** account, while the existing `/agents/{id}/status` is unaudited. | **One audited command for every status change.** The new `SetAccountStatusCommand` (task 5) serves `PUT /api/v1/admin/users/{id}/status` (any role). It also serves the existing `PUT /api/v1/agents/{id}/status` with `RequiredRole = Agent`, which keeps that route's 404 for non-Agent ids and its 204 response. `SetAgentStatusCommand` is deleted. **No status change can succeed without its audit entry.** |
| C5 | §5 anti-lockout and concurrency. | Inside the transaction, the handler first locks the target `AspNetUsers` row (`UPDLOCK, HOLDLOCK, ROWLOCK`, the `TicketAccessLock` pattern, `TicketAccessLock.cs` lines 14–25). Then it re-reads the account. **Deactivation is refused with 409** when the target is the caller ("You cannot deactivate your own account.") or when the target is a SuperAdmin and no **other** active SuperAdmin remains after locking all SuperAdmin rows ("At least one other active SuperAdmin is required."). Two concurrent toggles of the same account serialize on the row lock: the second one sees the committed state and becomes a **no-op** (no stamp rotation, no audit). Two SuperAdmins deactivating each other at the same moment can deadlock; SQL Server picks a victim (1205), `EnableRetryOnFailure` re-runs the victim's whole handler (`EfUnitOfWork.cs` lines 47–92), and the victim now sees the committed deactivation and is refused. **There is always ≥ 1 active SuperAdmin.** |
| C6 | AD3 lists exactly 4 event kinds. | **Exactly those 4.** Existing Agent profile edits (`PUT /agents/{id}`) and SuperAdmin password resets (`PUT /agents/{id}/password`) are **not** audited. They are outside the approved AD3 list. Documented as a follow-up in `docs/security-administration.md`, not silently added. |
| C7 | AD3 "safe details", no secrets, bounded. | Details are a list of `{ field, oldValue, newValue }` (`AdminAuditChange`):<br>• `AgentCreated`: `role: null→"Agent"`, `isActive: null→"true"`;<br>• status events: `isActive: "true"→"false"` (or the reverse);<br>• settings: one entry per changed field.<br>Every value is truncated to 200 characters with "…" (`AdministrationRules.AuditValueMaxLength`). It is serialized as JSON with `JavaScriptEncoder.UnsafeRelaxedJsonEscaping`. The values are rendered by Angular interpolation only, so relaxed escaping is safe, and the stored size stays ≤ 4000 (4 fields × 2 × 200 × ≤ 2 escape factor + keys). **No password, hash, stamp, token, key or connection string is ever an audit field.** The target label is the account email (the one identifying attribute) or "Organization settings". |
| C8 | AD3 atomicity "where supported". | **Supported, and fully atomic:**<br>• `CreateAgentCommand` and `SetAccountStatusCommand` are `ITransactionalCommand`s. The `UserManager` saves, the refresh-session revocation and the audit insert all commit together in `ExecuteInTransactionAsync` (`EfUnitOfWork.cs` lines 56–90); any exception rolls back all of them.<br>• `UpdateOrganizationSettingsCommand` is a **single `SaveChanges`** (settings update + audit insert) and is atomic by EF's implicit transaction (`docs/persistence-and-transactions.md` line 5).<br>Audit is never written from Angular and never fire-and-forget. |
| C9 | AD4 initial state; "avoid inventing organization details". | The migration inserts row `Id = 1` with **all values NULL** (`migrationBuilder.Sql`, not `HasData`, so the model has no seed data and `has-pending-model-changes` stays clean). `IsConfigured = DisplayName != null`. The UI shows "Not configured yet" and empty fields. No demo name is invented. A missing row (DB edited by hand) ⇒ `InvalidOperationException` ⇒ logged 500 "Organization settings row is missing; apply the migrations." This is a deployment defect, not a user flow. |
| C10 | AD4 concurrency and duplicate audit on simultaneous saves. | A shadow `rowversion` `Version` is exposed as `version` plus a strong ETag (the AgentTasks precedent: `AgentTasksController.cs` lines 142–147, `EntityTags.cs` lines 18–44). `PUT` requires `If-Match` (missing ⇒ 428, malformed ⇒ 400, stale ⇒ 409). The handler compares versions before anything else (`AgentTaskGuards.EnsureVersion` pattern, lines 25–31), and EF's rowversion check is the backstop (`EfUnitOfWork.cs` lines 26–30). Two simultaneous saves of the same version: one wins, the other gets 409 and writes **no** audit. A no-op save does not change the version. |
| C11 | AD2 matrix accuracy. | Static Angular data in `permission-matrix.ts`, transcribed from the **verified** `docs/identity-and-access.md` §1 (lines 7–43) and the controller policies (grep in Context item 16). Exceptions are shown as scoped text, never a bare ✔. The page footer says: "Display only. The server enforces every rule." Adding a backend permission later means editing this file and the doc together (stated in `docs/security-administration.md`). **No endpoint**: a `GET /admin/permissions` returning the same constants would add an API without adding truth. |
| C12 | §5 already-assigned tickets of a deactivated Agent. | **Nothing is changed:**<br>• `Tickets.AssignedAgentUserId`, `TicketActivities` actors and REPORT-001 attribution stay as they are;<br>• the account row is never deleted (FKs are `Restrict`, e.g. `TicketActivityConfiguration.cs` lines 22–25);<br>• new assignments already require an **active** Agent (`AgentTaskGuards.cs` lines 15–22; ticket creation and assignment per `docs/identity-and-access.md` line 17);<br>• the SuperAdmin reassigns through the existing `PUT /api/v1/tickets/{id}/assignee`.<br>The account detail page shows a hint with a link to `/tickets`. No workload query is added. |
| C13 | Deactivating the bootstrap SuperAdmin (`admin@admin.com`). | Allowed when another active SuperAdmin exists (impossible today through the API, which creates no SuperAdmins, so in practice it is always refused as the last one). `IdentityBootstrapper` never reactivates an existing account (`IdentityBootstrapper.cs` lines 41–51). Documented. |
| C14 | Identity optimistic concurrency. | `UserManager.UpdateAsync` checks `ConcurrencyStamp` and returns `ConcurrencyFailure`. Today `ThrowIfFailed` would turn that into a 400 with an empty key (`IdentityService.cs` lines 141–159). Add one mapping: `ConcurrencyFailure` ⇒ `ConflictException` (409). It is not expected to fire inside the locked status command, but it makes concurrent profile edits honest. |

### Recorded decisions

| Concern | Decision |
|---|---|
| Naming | Domain `Crm.Domain.Administration` (`AdminAuditEntry`, `AdminAuditAction`, `AdminAuditTargetType`, `AdminAuditChange`, `OrganizationSettings`). Application `Crm.Application.Administration.<UseCase>`. Ports: `Crm.Application.Abstractions.Identity.IAccountAdministration`; `Crm.Application.Abstractions.Persistence.IAdminAuditRepository` / `IOrganizationSettingsRepository`. Infrastructure: `Identity/AccountAdministration.cs`, `Persistence/Repositories/AdminAuditRepository.cs`, `OrganizationSettingsRepository.cs`. API: `Crm.Api.Contracts.Administration`, `AdministrationController`. Angular: `frontend/src/app/features/admin/`. |
| Routes | `GET /api/v1/admin/users?search&role&isActive&page&pageSize` · `GET /api/v1/admin/users/{id}` · `PUT /api/v1/admin/users/{id}/status` `{ isActive }` → 200 `{ account, changed }` · `GET /api/v1/admin/audit-logs?action&from&to&search&page&pageSize` · `GET` / `PUT /api/v1/admin/organization-settings` (ETag / If-Match). `/api/v1/admin/*` is a module prefix, documented as an exception like `/knowledge-base/*` (`docs/api-conventions.md` line 3). |
| Tables | `AdminAuditLogs` (bigint identity Id, `OccurredAtUtc`, `ActorUserId` FK Restrict, `Action` nvarchar(40), `TargetType` nvarchar(30), `TargetId` nvarchar(36), `TargetLabel` nvarchar(256), `Changes` nvarchar(4000) JSON; index `IX_AdminAuditLogs_OccurredAtUtc_Id` DESC, DESC). `OrganizationSettings` (int Id = 1 with check constraint, 4 nullable value columns, `UpdatedAtUtc` / `UpdatedByUserId` nullable, FK Restrict, `Version` rowversion). |
| Limits (`AdministrationRules`) | `OrganizationNameMaxLength` 150 · `SupportEmailMaxLength` = `AccountRules.EmailMaxLength` (256) · `SupportPhoneMaxLength` 40 · `SupportInformationMaxLength` 1000 · `SearchMaxLength` 100 · `AuditValueMaxLength` 200 · `EarliestAuditDate` 2000-01-01. Paging uses `Paging.DefaultPageSize` 20 / `MaxPageSize` 100 (clamped) / `MaxPage` (`PagedResult.cs` lines 8–10). |
| Ordering | Users: `LastName, FirstName, Email, Id`. Audit: `OccurredAtUtc DESC, Id DESC` (identity Id = insertion order, the `TicketActivities` precedent, `TicketActivityConfiguration.cs` line 14). |
| Dates | Audit `from`/`to` are UTC calendar days, inclusive, as `yyyy-MM-dd` ⇒ SQL `[from 00:00Z, to+1 00:00Z)`. The UI labels timestamps "(UTC)", like REPORT-001. |
| Errors | Validation ⇒ 400 `ValidationProblemDetails` (`isActive`, `displayName`, `supportEmail`, `supportPhone`, `supportInformation`, `search`, `role`, `from`, `to`, `page` keys). Unknown account ⇒ 404. Self / last SuperAdmin ⇒ 409. Stale settings version ⇒ 409, missing ⇒ 428. Anonymous ⇒ 401, Agent/Customer ⇒ 403. |
| Text safety | Every user-controlled string (names, emails, support information, audit values) is rendered with interpolation. **Never `[innerHTML]`** under `features/admin/`. Support information uses `.user-text` (`productivity-page.scss` lines 50–54). |
| UI placement | One nav entry "Administration" (`admin_panel_settings`, `/admin`, `exact: false`, `roles: ['SuperAdmin']`) after "Reports". The existing "Agents" entry stays. No Home card. |

---

## Context — Read These Files First

1. `.squad/stories/security-administration/ADMIN-001/intake.md` — the contract: §3 (AD1–AD4), §4 role table, §5 accounts and JWT revocation, §6 matrix, §7 audit, §8 settings, §9 API, §10 Angular, §11 the 1+1 test policy and the 20-step acceptance matrix, §12 out of scope, §13 stop-and-report triggers.
2. [../reports-management/13-story-reports-management.md](../reports-management/13-story-reports-management.md) — tone, the conflicts and decisions tables, and the Verification and Done Criteria style.
3. `backend/src/Crm.Api/Authentication/SecurityRegistration.cs`:
   - lines 38–58: `OnTokenValidated`, **the revocation check (C1); do not modify**;
   - lines 61–66: policies; line 62: authenticated fallback;
   - line 63: `SuperAdmin` = `RequireRole(UserRoles.SuperAdmin)`.

   `AuthorizationPolicies.cs` lines 3–9. `ClaimsPrincipalExtensions.cs` lines 9–12 (`GetUserId`, the actor source).
4. `backend/src/Crm.Infrastructure/Identity/IdentityService.cs`:
   - lines 22–30: `Project`, the role subquery to copy;
   - lines 32–33: `FindByIdAsync`;
   - line 47: login refuses inactive accounts;
   - lines 62–80: `CreateUserAsync` (two saves);
   - lines 82–88: `ListByRoleAsync`, the role filter to copy;
   - lines 132–139: `SetActiveAsync` (rotates the stamp);
   - lines 141–159: `ThrowIfFailed` (add C14 after line 146).

   `ApplicationUser.cs` lines 6–12. `IIdentityService.cs` lines 3–27 (**not changed**). `UserAccount.cs` lines 4–13 (`SecurityStamp` is internal: never map it to a response). `UserRoles.cs` lines 4–11.
5. `backend/src/Crm.Application/Agents/`:
   - `CreateAgent/CreateAgentCommand.cs` lines 10–29 (**changed**, task 6);
   - `CreateAgent/CreateAgentValidator.cs` lines 6–15;
   - `SetAgentStatus/SetAgentStatusCommand.cs` lines 9–28 (**deleted**, its logic moves to task 5);
   - `AgentLookup.cs` lines 10–14 (Agent-only 404 message to keep).

   `backend/src/Crm.Api/Controllers/AgentsController.cs` lines 33–42 (Create) and 53–60 (SetStatus).
6. `backend/src/Crm.Application/Auth/RenewSession/RenewSessionCommand.cs` lines 92–102 and `Auth/Login/LoginCommand.cs` lines 8–30 (inactive accounts cannot renew or log in). `Abstractions/Persistence/IRefreshSessionRepository.cs` lines 5–22 (`RevokeAllForUserAsync`). `Crm.Domain/Identity/RefreshSessionRevocationReason.cs` line 10 (`AccountDisabled`).
7. `backend/src/Crm.Infrastructure/Persistence/EfUnitOfWork.cs`:
   - lines 12–18: `KnownUniqueIndexes`;
   - lines 20–35: the 409 mapping;
   - lines 37–93: transaction + retry + final save (line 61) + commit.

   `Common/Behaviors/TransactionBehavior.cs` lines 21–38. `Abstractions/Messaging/ITransactionalCommand.cs`. `docs/persistence-and-transactions.md` lines 5, 7, 8, 10, 11, 12.
8. `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketAccessLock.cs` lines 8–44 — **the raw `SqlQuery` + `UPDLOCK, HOLDLOCK, ROWLOCK` + `EnsureTransaction` pattern** for `AccountAdministration`.
9. Singleton + versioned precedents:
   - `Crm.Domain/Sla/SlaPolicy.cs` lines 10–45 (singleton Id, `UpdatedByUserId`);
   - `SlaPolicyConfiguration.cs` lines 14 and 25 (`ValueGeneratedNever`, user FK `Restrict`);
   - `SlaConfigurationRepository.cs` lines 20–24 (`FindUserNameAsync`, copy it);
   - `TicketFeedbackConfiguration.cs` line 17 (shadow rowversion);
   - `TicketFeedback.cs` lines 33–46 (`Update` returns false on no-op).
10. Append-only precedent: `Crm.Domain/Tickets/TicketActivity.cs` lines 3–8 and 52–68 (private ctor, factory methods only, identity Id). `TicketActivityConfiguration.cs` lines 12–27 (enum as string, user FK `Restrict`, descending index).
11. ETag precedent:
    - `backend/src/Crm.Api/Http/EntityTags.cs` lines 7–44 (line 24: the message to make generic);
    - `AgentTasksController.cs` lines 31, 79–84, 142–147;
    - `Application/AgentTasks/AgentTaskGuards.cs` lines 24–31;
    - `UpdateAgentTask/UpdateAgentTaskCommand.cs` lines 20–43 (version check first; save only when changed).
12. Paging/search precedent:
    - `Infrastructure/Persistence/Repositories/CustomerRepository.cs` lines 25–49 (`Contains` search, count, order + `Id` tie-breaker, `Skip`/`Take`);
    - `Application/Customers/ListCustomers/ListCustomersQuery.cs` lines 15–17 (trim, clamp);
    - `ListCustomersValidator.cs` lines 11–13;
    - `Common/Paging/PagedResult.cs`; `Api/Contracts/Common/PagedResponse.cs`;
    - `AgentTasksController.cs` lines 33–50 (`[FromQuery]` + `PagedResponse` mapping).
13. `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` lines 22–42 (add 2 `DbSet`s after line 42). `backend/src/Crm.Infrastructure/DependencyInjection.cs` lines 41–61 (add after line 61) and line 80. `Common/Validation/AccountRules.cs` lines 5–28. `backend/src/Crm.Api/Errors/GlobalExceptionHandler.cs` lines 23–30 (camelCase keys), 43–48 (428), 67–72 (409). `Crm.Api/DependencyInjection.cs` lines 16–19 (enums as names).
14. `backend/src/Crm.Infrastructure/Persistence/Migrations/20261018124705_AddTicketFeedback.cs` (generated migration style, `rowversion` column). Latest snapshot: `CrmDbContextModelSnapshot.cs`.
15. Tests: `backend/tests/Crm.UnitTests/Auth/LoginCommandHandlerTests.cs` lines 9–74 — **the template for the single new backend test** (fakes with `NotSupportedException` for unused members, `FakeUnitOfWork.SaveCount`).
16. AD2 sources:
    - `docs/identity-and-access.md` lines 7–43 (the verified role matrix) and 97 (`/agents/{id}` Agent-only);
    - grep `\[Authorize\(Policy = AuthorizationPolicies\.` in `backend/src/Crm.Api/Controllers/`:
      - Staff: Tickets, Customers, CustomerRecords, Tasks, QuickReplies, Collaborations, KB articles/categories, TicketKnowledgeArticles, TicketAi, AiAssistant, SlaAlerts;
      - SuperAdmin: Agents, Sla, AutomationRules, Reports, `TicketsController.cs` lines 84 and 105, `KnowledgeArticlesController.cs` lines 73/79/85/91, `KnowledgeCategoriesController.cs` lines 27/37, `AgentTasksController.cs` line 136;
      - Agent: AgentDashboard, Notifications, `CollaborationsController.cs` line 30;
      - Customer: PortalTickets, PortalHelpCenter, `MeController.cs` lines 47/53.
17. Angular:
    - `frontend/src/app/app.routes.ts` lines 85–90 (insert after the `reports` route, before the comment on line 90);
    - `core/layout/navigation.ts` lines 13–31 (insert after line 29);
    - `core/layout/shell/shell.ts` line 38 (role filtering);
    - `core/auth/auth-guards.ts` lines 14–19;
    - `core/auth/auth-service.ts` line 19 (`currentUser`).
18. Angular patterns:
    - `features/quick-replies/quick-reply-list-page.ts` lines 52–118 (filters form + `debounceTime(300)` + `Subject`/`switchMap` + `MatPaginator` + reload);
    - `features/tasks/tasks-api.ts` lines 22–41 (`HttpParams`, empty values omitted) and 111–113 (`If-Match`);
    - `features/sla/sla-errors.ts` lines 6–10 (`describeSaveError`: field messages);
    - `core/api/describe-http-error.ts`;
    - `features/agents/agent-detail-page.ts` lines 125–153 (status toggle with re-bind on error);
    - `features/collaboration/ticket-collaborators.ts` line 52 (`window.confirm` precedent);
    - `shared/ui/productivity-page.scss`; `shared/ui/status-badge/status-badge.ts` (tones); `shared/ui/page-header/page-header.ts`;
    - `features/customers/customer-models.ts` line 15 (`PagedResponse<T>`).
19. `frontend/src/app/features/reports/report-format.spec.ts` (one pure-function Vitest `it`, no TestBed) — **the pattern for the single new frontend test.**
20. Docs:
    - `docs/identity-and-access.md` lines 13, 42–43 (matrix), 55–58 and 94–95 (routes), 112 (revocation);
    - `docs/api-conventions.md` lines 3 and 22;
    - `docs/persistence-and-transactions.md` line 12;
    - `README.md` line 3.

---

## Product rules (from story)

### Role matrix (server-enforced)

| Operation | SuperAdmin | Agent | Customer | Anonymous |
|---|---|---|---|---|
| `GET /api/v1/admin/users`, `GET /admin/users/{id}` | ✔ all accounts | 403 | 403 | 401 |
| `PUT /api/v1/admin/users/{id}/status` | ✔ (not self; not the last active SuperAdmin) | 403 | 403 | 401 |
| `POST /api/v1/agents` (existing; now audited) | ✔ role forced to Agent | 403 | 403 | 401 |
| `PUT /api/v1/agents/{id}/status` (existing; now audited, same rules) | ✔ Agent ids only | 403 | 403 | 401 |
| `GET /api/v1/admin/audit-logs` | ✔ | 403 | 403 | 401 |
| `GET` / `PUT /api/v1/admin/organization-settings` | ✔ | 403 | 403 | 401 |
| Change an account's role | **nobody** (no endpoint exists) | — | — | — |
| Edit / delete an audit entry | **nobody** (no endpoint exists) | — | — | — |
| Angular `/admin/**` + nav entry | ✔ | hidden, redirected to `/` | hidden, redirected to `/` | sign-in |

### Account status rules

| Situation | Result | Audit |
|---|---|---|
| Deactivate an active Agent/Customer/other SuperAdmin | 200 `changed: true`; `IsActive = false`, stamp rotated, refresh sessions revoked (`AccountDisabled`); the next request with any old JWT ⇒ 401 | `AccountDeactivated` |
| Reactivate an inactive account | 200 `changed: true`; `IsActive = true`, stamp rotated; **no** token is restored (fresh login required) | `AccountReactivated` |
| Requested state = current state | 200 `changed: false`; nothing written | none |
| Deactivate yourself | 409 "You cannot deactivate your own account." | none |
| Deactivate the last active SuperAdmin | 409 "At least one other active SuperAdmin is required." | none |
| Unknown id (or non-Agent id on `/agents/{id}/status`) | 404 | none |
| Missing `isActive` | 400 `isActive` | none |

### Audit events

| Action | Written by | TargetType / TargetId / TargetLabel | Changes |
|---|---|---|---|
| `AgentCreated` | `CreateAgentCommand` (`POST /agents`) | `UserAccount` / new user id / email | `role: null → Agent`, `isActive: null → true` |
| `AccountDeactivated` | `SetAccountStatusCommand` | `UserAccount` / user id / email | `isActive: true → false` |
| `AccountReactivated` | `SetAccountStatusCommand` | `UserAccount` / user id / email | `isActive: false → true` |
| `OrganizationSettingsUpdated` | `UpdateOrganizationSettingsCommand` | `OrganizationSettings` / `1` / "Organization settings" | one per changed field among `displayName`, `supportEmail`, `supportPhone`, `supportInformation` (values ≤ 200 chars) |

---

## Backend Tasks

### 1 — Domain: audit entry and organization settings

**Create file: `backend/src/Crm.Domain/Administration/AdminAuditAction.cs`**

```csharp
namespace Crm.Domain.Administration;

/// <summary>ADMIN-001 AD3: the only audited administrative actions. Stored by name.</summary>
public enum AdminAuditAction
{
    AgentCreated,
    AccountDeactivated,
    AccountReactivated,
    OrganizationSettingsUpdated,
}

public enum AdminAuditTargetType
{
    UserAccount,
    OrganizationSettings,
}
```

**Create file: `backend/src/Crm.Domain/Administration/AdminAuditChange.cs`**

```csharp
namespace Crm.Domain.Administration;

/// <summary>One safe field change. Values are display text, truncated; never secrets.</summary>
public sealed record AdminAuditChange(string Field, string? OldValue, string? NewValue)
{
    public const int ValueMaxLength = 200;

    public static AdminAuditChange Of(string field, string? oldValue, string? newValue) =>
        new(field, Truncate(oldValue), Truncate(newValue));

    private static string? Truncate(string? value) =>
        value is { Length: > ValueMaxLength } ? value[..(ValueMaxLength - 1)] + "…" : value;
}
```

**Create file: `backend/src/Crm.Domain/Administration/AdminAuditEntry.cs`** (style of `TicketActivity.cs` lines 3–68):

```csharp
using Crm.Domain.Common;

namespace Crm.Domain.Administration;

/// <summary>
/// ADMIN-001 AD3: append-only administrative audit record. Created only by the factories below and inserted in the same
/// transaction as the change it describes. Never updated or deleted by the application (no API exists). Id is a store-generated
/// identity (insertion order = deterministic tie-breaker). ActorUserId / TargetId reference accounts by id; names are joined at read time.
/// </summary>
public sealed class AdminAuditEntry
{
    public const string OrganizationSettingsTargetId = "1";
    public const string OrganizationSettingsLabel = "Organization settings";

    private AdminAuditEntry() { } // EF Core

    public long Id { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public Guid ActorUserId { get; private set; }
    public AdminAuditAction Action { get; private set; }
    public AdminAuditTargetType TargetType { get; private set; }
    public string TargetId { get; private set; } = string.Empty;
    public string TargetLabel { get; private set; } = string.Empty;
    public IReadOnlyList<AdminAuditChange> Changes { get; private set; } = [];

    public static AdminAuditEntry AgentCreated(Guid actorUserId, Guid agentUserId, string email, DateTimeOffset now) =>
        New(actorUserId, AdminAuditAction.AgentCreated, AdminAuditTargetType.UserAccount, agentUserId.ToString(), email, now,
            [AdminAuditChange.Of("role", null, "Agent"), AdminAuditChange.Of("isActive", null, "true")]);

    public static AdminAuditEntry AccountStatusChanged(Guid actorUserId, Guid userId, string email, bool isActive, DateTimeOffset now) =>
        New(actorUserId, isActive ? AdminAuditAction.AccountReactivated : AdminAuditAction.AccountDeactivated,
            AdminAuditTargetType.UserAccount, userId.ToString(), email, now,
            [AdminAuditChange.Of("isActive", isActive ? "false" : "true", isActive ? "true" : "false")]);

    public static AdminAuditEntry OrganizationSettingsUpdated(Guid actorUserId, IReadOnlyList<AdminAuditChange> changes, DateTimeOffset now)
    {
        if (changes.Count == 0)
        {
            throw new DomainException("An unchanged save is not an audited change."); // backstop: the handler skips no-ops
        }

        return New(actorUserId, AdminAuditAction.OrganizationSettingsUpdated, AdminAuditTargetType.OrganizationSettings,
            OrganizationSettingsTargetId, OrganizationSettingsLabel, now, changes);
    }

    private static AdminAuditEntry New(
        Guid actorUserId, AdminAuditAction action, AdminAuditTargetType targetType, string targetId, string targetLabel,
        DateTimeOffset now, IReadOnlyList<AdminAuditChange> changes) =>
        new()
        {
            ActorUserId = actorUserId,
            Action = action,
            TargetType = targetType,
            TargetId = targetId,
            TargetLabel = targetLabel,
            OccurredAtUtc = now,
            Changes = changes,
        };
}
```

**Create file: `backend/src/Crm.Domain/Administration/OrganizationSettings.cs`** (singleton like `SlaPolicy.cs` lines 10–45; no-op like `TicketFeedback.Update` lines 33–46):

```csharp
using Crm.Domain.Common;

namespace Crm.Domain.Administration;

/// <summary>
/// ADMIN-001 AD4: the one informational settings row (Id 1, inserted by the migration with all values null = not configured).
/// Informational only: never used by tickets, SLA, KB, AI or messaging. No secrets.
/// </summary>
public sealed class OrganizationSettings
{
    public const int SingletonId = 1;

    private OrganizationSettings() { } // EF Core

    public int Id { get; private set; }
    public string? DisplayName { get; private set; }
    public string? SupportEmail { get; private set; }
    public string? SupportPhone { get; private set; }
    public string? SupportInformation { get; private set; }
    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    public bool IsConfigured => DisplayName is not null;

    /// <summary>Values must already be trimmed / normalized (null = empty). Returns the changes; empty = nothing was modified.</summary>
    public IReadOnlyList<AdminAuditChange> Update(
        string displayName, string? supportEmail, string? supportPhone, string? supportInformation, Guid actorUserId, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new DomainException("The organization display name is required."); // backstop: the validator returns 400 first
        }

        var changes = new List<AdminAuditChange>();
        Track(changes, "displayName", DisplayName, displayName);
        Track(changes, "supportEmail", SupportEmail, supportEmail);
        Track(changes, "supportPhone", SupportPhone, supportPhone);
        Track(changes, "supportInformation", SupportInformation, supportInformation);
        if (changes.Count == 0)
        {
            return changes;
        }

        DisplayName = displayName;
        SupportEmail = supportEmail;
        SupportPhone = supportPhone;
        SupportInformation = supportInformation;
        UpdatedAtUtc = now;
        UpdatedByUserId = actorUserId;
        return changes;
    }

    private static void Track(List<AdminAuditChange> changes, string field, string? oldValue, string? newValue)
    {
        if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
        {
            changes.Add(AdminAuditChange.Of(field, oldValue, newValue));
        }
    }
}
```

### 2 — Application: rules, ports, read models

**Create file: `backend/src/Crm.Application/Common/Validation/AdministrationRules.cs`**

```csharp
using FluentValidation;

namespace Crm.Application.Common.Validation;

public static class AdministrationRules
{
    public const int OrganizationNameMaxLength = 150;
    public const int SupportEmailMaxLength = AccountRules.EmailMaxLength;
    public const int SupportPhoneMaxLength = 40;
    public const int SupportInformationMaxLength = 1000;
    public const int SearchMaxLength = 100;
    public static readonly DateOnly EarliestAuditDate = new(2000, 1, 1);

    /// <summary>Trimmed value, or null when empty (same rule as AccountRules.NormalizeOptional).</summary>
    public static string? Normalize(string? value) => AccountRules.NormalizeOptional(value);
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Identity/IAccountAdministration.cs`**

```csharp
using Crm.Application.Common.Paging;

namespace Crm.Application.Abstractions.Identity;

/// <summary>Role must be one of UserRoles.All or null; Search is trimmed or null.</summary>
public sealed record AccountListFilter(string? Search, string? Role, bool? IsActive);

public sealed record LockedAccountRow(Guid UserId, bool IsActive);

/// <summary>ADMIN-001 account administration reads and write-time locks. IIdentityService stays the write boundary.</summary>
public interface IAccountAdministration
{
    /// <summary>All roles. Order: LastName, FirstName, Email, Id. Filters apply before paging.</summary>
    Task<PagedResult<UserAccount>> ListAsync(AccountListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Call only inside an ITransactionalCommand. UPDLOCK + HOLDLOCK on the AspNetUsers row until commit; reads the latest committed
    /// state. null = no such account.
    /// </summary>
    Task<LockedAccountRow?> LockAccountAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Call only inside an ITransactionalCommand. Locks every SuperAdmin account row until commit and returns them.</summary>
    Task<IReadOnlyList<LockedAccountRow>> LockSuperAdminsAsync(CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IAdminAuditRepository.cs`**

```csharp
using Crm.Application.Administration;
using Crm.Application.Common.Paging;
using Crm.Domain.Administration;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>Append-only: Add stages (the caller saves in the same transaction as the audited change); there is no update or delete.</summary>
public interface IAdminAuditRepository
{
    void Add(AdminAuditEntry entry);

    /// <summary>Newest first (OccurredAtUtc DESC, Id DESC). Filters before paging. fromUtc inclusive, toUtcExclusive exclusive.</summary>
    Task<PagedResult<AdminAuditEntryDetails>> ListAsync(
        AdminAuditAction? action, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, string? search,
        int page, int pageSize, CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IOrganizationSettingsRepository.cs`**

```csharp
using Crm.Domain.Administration;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>The singleton row (Id 1, created by the migration). Tracked. null only when the migration was not applied.</summary>
public interface IOrganizationSettingsRepository
{
    Task<OrganizationSettings?> FindAsync(CancellationToken cancellationToken = default);

    /// <summary>Current rowversion of the tracked entity (shadow property "Version").</summary>
    byte[] GetVersion(OrganizationSettings settings);

    /// <summary>Display name of a user id; null when unknown (same as ISlaConfigurationRepository.FindUserNameAsync).</summary>
    Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Administration/AdministrationDetails.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Domain.Administration;

namespace Crm.Application.Administration;

public sealed record AccountStatusChange(UserAccount Account, bool Changed);

public sealed record AdminAuditEntryDetails(
    long Id, DateTimeOffset OccurredAtUtc, AdminAuditAction Action, Guid ActorUserId, string? ActorName, string? ActorEmail,
    AdminAuditTargetType TargetType, string TargetId, string TargetLabel, IReadOnlyList<AdminAuditChange> Changes);

public sealed record OrganizationSettingsDetails(
    bool IsConfigured, string? DisplayName, string? SupportEmail, string? SupportPhone, string? SupportInformation,
    DateTimeOffset? UpdatedAtUtc, string? UpdatedByName, byte[] Version);
```

### 3 — Application: account queries

**Create file: `backend/src/Crm.Application/Administration/ListAccounts/ListAccountsQuery.cs`** (handler + validator in one file, like `UpdateSlaPolicyCommand.cs`):

```csharp
public sealed record ListAccountsQuery(string? Search, string? Role, bool? IsActive, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<UserAccount>>;

public sealed class ListAccountsQueryHandler(IAccountAdministration accounts) : IQueryHandler<ListAccountsQuery, PagedResult<UserAccount>>
{
    public async ValueTask<PagedResult<UserAccount>> Handle(ListAccountsQuery query, CancellationToken cancellationToken)
    {
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize); // documented cap, as ListCustomersQuery
        return await accounts.ListAsync(new AccountListFilter(search, query.Role, query.IsActive), query.Page, pageSize, cancellationToken);
    }
}

public sealed class ListAccountsValidator : AbstractValidator<ListAccountsQuery>
{
    public ListAccountsValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Search).MaximumLength(AdministrationRules.SearchMaxLength);
        RuleFor(q => q.Role).Must(r => r is null || UserRoles.All.Contains(r)).WithMessage("Unknown role.");
    }
}
```

**Create file: `backend/src/Crm.Application/Administration/GetAccount/GetAccountQuery.cs`**

```csharp
public sealed record GetAccountQuery(Guid Id) : IQuery<UserAccount>;

public sealed class GetAccountQueryHandler(IIdentityService identity) : IQueryHandler<GetAccountQuery, UserAccount>
{
    public async ValueTask<UserAccount> Handle(GetAccountQuery query, CancellationToken cancellationToken) =>
        await identity.FindByIdAsync(query.Id, cancellationToken) ?? throw new NotFoundException($"Account '{query.Id}' was not found.");
}
```

### 4 — Infrastructure: `AccountAdministration`

**Create file: `backend/src/Crm.Infrastructure/Identity/AccountAdministration.cs`**

- `internal sealed class AccountAdministration(CrmDbContext dbContext) : IAccountAdministration`.
- Copy the projection from `IdentityService.cs` lines 22–30 into a private `Project` (same comment: filter and order **before** projecting). Do **not** make `IdentityService.Project` shared; the copy keeps `IdentityService` unchanged.
- `ListAsync`:
  - `var users = dbContext.Users.AsNoTracking();`
  - **search** (non-null):
    `users = users.Where(u => u.FirstName.Contains(s) || u.LastName.Contains(s) || (u.FirstName + " " + u.LastName).Contains(s) || u.Email!.Contains(s));`
    This is parameterized, as `CustomerRepository.cs` lines 29–38.
  - **role** (non-null): copy the predicate of `IdentityService.cs` lines 84–85.
  - **isActive** (non-null): `users = users.Where(u => u.IsActive == isActive)`.
  - `totalCount = await users.CountAsync(ct)`.
  - Then `Project(users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).ThenBy(u => u.Email).ThenBy(u => u.Id).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct)`.
  - Return `new PagedResult<UserAccount>(items, page, pageSize, totalCount)`.
- `LockAccountAsync` (pattern `TicketAccessLock.cs` lines 8–25, with a private `sealed class StatusRow { public Guid UserId { get; init; } public bool IsActive { get; init; } }`):

  ```csharp
  EnsureTransaction();
  var rows = await dbContext.Database.SqlQuery<StatusRow>($"""
      SELECT [Id] AS [UserId], [IsActive]
      FROM [AspNetUsers] WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
      WHERE [Id] = {userId}
      """).ToListAsync(cancellationToken);
  return rows.SingleOrDefault() is { } row ? new LockedAccountRow(row.UserId, row.IsActive) : null;
  ```
- `LockSuperAdminsAsync`:

  ```csharp
  EnsureTransaction();
  var rows = await dbContext.Database.SqlQuery<StatusRow>($"""
      SELECT u.[Id] AS [UserId], u.[IsActive]
      FROM [AspNetUsers] u WITH (UPDLOCK, HOLDLOCK, ROWLOCK)
      WHERE u.[Id] IN (SELECT ur.[UserId] FROM [AspNetUserRoles] ur
                       INNER JOIN [AspNetRoles] r ON r.[Id] = ur.[RoleId]
                       WHERE r.[Name] = {UserRoles.SuperAdmin})
      """).ToListAsync(cancellationToken);
  return rows.Select(r => new LockedAccountRow(r.UserId, r.IsActive)).ToList();
  ```
- `EnsureTransaction()` is copied from `TicketAccessLock.cs` lines 38–44, with the message "IAccountAdministration locks must be used inside an ITransactionalCommand."

**File: `backend/src/Crm.Infrastructure/Identity/IdentityService.cs`** — in `ThrowIfFailed`, insert after line 146 (C14):

```csharp
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.ConcurrencyFailure)))
        {
            throw new ConflictException("The account was changed by another request. Reload it and try again.");
        }
```

### 5 — Application: `SetAccountStatusCommand` (replaces `SetAgentStatusCommand`)

**Create file: `backend/src/Crm.Application/Administration/SetAccountStatus/SetAccountStatusCommand.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Administration;
using Crm.Domain.Identity;
using FluentValidation;
using Mediator;

namespace Crm.Application.Administration.SetAccountStatus;

/// <summary>
/// ADMIN-001 AD1/AD3: activate or deactivate an account, with its audit entry in the same transaction.
/// RequiredRole = UserRoles.Agent for the legacy /agents/{id}/status route (other ids 404 there).
/// IsActive is nullable only so a missing JSON field fails validation instead of defaulting to false.
/// </summary>
public sealed record SetAccountStatusCommand(Guid ActorUserId, Guid UserId, bool? IsActive, string? RequiredRole = null)
    : ICommand<AccountStatusChange>, ITransactionalCommand; // lock + Identity save + session revocation + audit = one transaction

public sealed class SetAccountStatusCommandHandler(
    IAccountAdministration accounts,
    IIdentityService identity,
    IRefreshSessionRepository sessions,
    IAdminAuditRepository audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SetAccountStatusCommand, AccountStatusChange>
{
    public async ValueTask<AccountStatusChange> Handle(SetAccountStatusCommand command, CancellationToken cancellationToken)
    {
        var isActive = command.IsActive!.Value;

        // Serializes concurrent toggles of this account: the loser re-reads the committed state and becomes a no-op.
        var locked = await accounts.LockAccountAsync(command.UserId, cancellationToken);
        var account = locked is null ? null : await identity.FindByIdAsync(command.UserId, cancellationToken);
        if (account is null || (command.RequiredRole is { } role && account.Role != role))
        {
            throw new NotFoundException(command.RequiredRole == UserRoles.Agent
                ? $"Agent '{command.UserId}' was not found."
                : $"Account '{command.UserId}' was not found.");
        }

        if (account.IsActive == isActive)
        {
            return new AccountStatusChange(account, Changed: false); // idempotent: no stamp rotation, no revocation, no audit
        }

        if (!isActive)
        {
            if (account.Id == command.ActorUserId)
            {
                throw new ConflictException("You cannot deactivate your own account.");
            }

            if (account.Role == UserRoles.SuperAdmin)
            {
                var admins = await accounts.LockSuperAdminsAsync(cancellationToken);
                if (!admins.Any(a => a.IsActive && a.UserId != account.Id))
                {
                    throw new ConflictException("At least one other active SuperAdmin is required.");
                }
            }
        }

        var now = timeProvider.GetUtcNow();
        await identity.SetActiveAsync(account.Id, isActive, cancellationToken); // also rotates the security stamp ⇒ old JWTs fail
        if (!isActive)
        {
            await sessions.RevokeAllForUserAsync(account.Id, RefreshSessionRevocationReason.AccountDisabled, now, cancellationToken);
        }

        audit.Add(AdminAuditEntry.AccountStatusChanged(command.ActorUserId, account.Id, account.Email, isActive, now));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccountStatusChange((await identity.FindByIdAsync(account.Id, cancellationToken))!, Changed: true);
    }
}

public sealed class SetAccountStatusValidator : AbstractValidator<SetAccountStatusCommand>
{
    public SetAccountStatusValidator()
    {
        RuleFor(c => c.UserId).NotEmpty();
        RuleFor(c => c.IsActive).NotNull();
    }
}
```

**Delete folder: `backend/src/Crm.Application/Agents/SetAgentStatus/`** (`SetAgentStatusCommand.cs`). Grep `SetAgentStatusCommand` afterwards ⇒ no reference remains.

The handler is replay-safe (no instance state, no external call): a deadlock retry re-runs it from the lock (C5).

### 6 — Application: audit Agent creation

**File: `backend/src/Crm.Application/Agents/CreateAgent/CreateAgentCommand.cs`** — replace lines 10–29:

```csharp
public sealed record CreateAgentCommand(
    Guid ActorUserId, string FirstName, string LastName, string Email, string? PhoneNumber, string Password)
    : ICommand<UserAccount>, ITransactionalCommand; // create user + add role + audit = one transaction

public sealed class CreateAgentCommandHandler(
    IIdentityService identity,
    IAdminAuditRepository audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<CreateAgentCommand, UserAccount>
{
    public async ValueTask<UserAccount> Handle(CreateAgentCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim();
        if (await identity.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var agent = await identity.CreateUserAsync(
            new NewUserAccount(email, command.FirstName.Trim(), command.LastName.Trim(), AccountRules.NormalizeOptional(command.PhoneNumber)),
            command.Password,
            UserRoles.Agent, // always server-side; the request has no role field
            cancellationToken);

        // Never the password: role and active state only (C7).
        audit.Add(AdminAuditEntry.AgentCreated(command.ActorUserId, agent.Id, agent.Email, timeProvider.GetUtcNow()));
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return agent;
    }
}
```

Add `using Crm.Application.Abstractions.Persistence;` and `using Crm.Domain.Administration;`. `CreateAgentValidator.cs` is unchanged (it validates by member name). **No test references `CreateAgentCommand`** (grep `backend/tests`).

### 7 — Application: audit log query

**Create file: `backend/src/Crm.Application/Administration/ListAuditLogs/ListAuditLogsQuery.cs`**

```csharp
public sealed record ListAuditLogsQuery(
    AdminAuditAction? Action, DateOnly? From, DateOnly? To, string? Search, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<AdminAuditEntryDetails>>;

public sealed class ListAuditLogsQueryHandler(IAdminAuditRepository audit)
    : IQueryHandler<ListAuditLogsQuery, PagedResult<AdminAuditEntryDetails>>
{
    public async ValueTask<PagedResult<AdminAuditEntryDetails>> Handle(ListAuditLogsQuery query, CancellationToken cancellationToken)
    {
        // UTC calendar days, inclusive: [from 00:00Z, to+1 00:00Z).
        DateTimeOffset? fromUtc = query.From is { } f ? new DateTimeOffset(f.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;
        DateTimeOffset? toExclusive = query.To is { } t ? new DateTimeOffset(t.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;
        var search = string.IsNullOrWhiteSpace(query.Search) ? null : query.Search.Trim();
        var pageSize = Math.Min(query.PageSize, Paging.MaxPageSize);
        return await audit.ListAsync(query.Action, fromUtc, toExclusive, search, query.Page, pageSize, cancellationToken);
    }
}

public sealed class ListAuditLogsValidator : AbstractValidator<ListAuditLogsQuery>
{
    public ListAuditLogsValidator()
    {
        RuleFor(q => q.Page).InclusiveBetween(1, Paging.MaxPage);
        RuleFor(q => q.PageSize).GreaterThanOrEqualTo(1);
        RuleFor(q => q.Action).IsInEnum();
        RuleFor(q => q.Search).MaximumLength(AdministrationRules.SearchMaxLength);
        RuleFor(q => q.From).GreaterThanOrEqualTo(AdministrationRules.EarliestAuditDate).When(q => q.From is not null);
        RuleFor(q => q.To).GreaterThanOrEqualTo(q => q.From!.Value).WithMessage("'To' must be on or after 'From'.")
            .When(q => q.From is not null && q.To is not null);
    }
}
```

### 8 — Application: organization settings

**Create file: `backend/src/Crm.Application/Administration/GetOrganizationSettings/GetOrganizationSettingsQuery.cs`**

```csharp
public sealed record GetOrganizationSettingsQuery : IQuery<OrganizationSettingsDetails>;

public sealed class GetOrganizationSettingsQueryHandler(IOrganizationSettingsRepository settings)
    : IQueryHandler<GetOrganizationSettingsQuery, OrganizationSettingsDetails>
{
    public async ValueTask<OrganizationSettingsDetails> Handle(GetOrganizationSettingsQuery query, CancellationToken cancellationToken) =>
        await OrganizationSettingsMapper.ToDetailsAsync(await settings.RequireAsync(cancellationToken), settings, cancellationToken);
}

internal static class OrganizationSettingsMapper
{
    public static async Task<OrganizationSettings> RequireAsync(this IOrganizationSettingsRepository repository, CancellationToken ct) =>
        await repository.FindAsync(ct)
            ?? throw new InvalidOperationException("Organization settings row is missing; apply the migrations (dotnet ef database update).");

    public static async Task<OrganizationSettingsDetails> ToDetailsAsync(
        OrganizationSettings s, IOrganizationSettingsRepository repository, CancellationToken ct) =>
        new(s.IsConfigured, s.DisplayName, s.SupportEmail, s.SupportPhone, s.SupportInformation, s.UpdatedAtUtc,
            s.UpdatedByUserId is { } by ? await repository.FindUserNameAsync(by, ct) : null,
            repository.GetVersion(s));
}
```

**Create file: `backend/src/Crm.Application/Administration/UpdateOrganizationSettings/UpdateOrganizationSettingsCommand.cs`**

```csharp
/// <summary>Single save (settings + audit) — atomic without ITransactionalCommand. Requires the current version (If-Match).</summary>
public sealed record UpdateOrganizationSettingsCommand(
    Guid ActorUserId, byte[] ExpectedVersion, string? DisplayName, string? SupportEmail, string? SupportPhone, string? SupportInformation)
    : ICommand<OrganizationSettingsDetails>;

public sealed class UpdateOrganizationSettingsCommandHandler(
    IOrganizationSettingsRepository settings,
    IAdminAuditRepository audit,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<UpdateOrganizationSettingsCommand, OrganizationSettingsDetails>
{
    public async ValueTask<OrganizationSettingsDetails> Handle(UpdateOrganizationSettingsCommand command, CancellationToken cancellationToken)
    {
        var current = await settings.RequireAsync(cancellationToken);
        if (!settings.GetVersion(current).AsSpan().SequenceEqual(command.ExpectedVersion))
        {
            throw new ConflictException("The organization settings were changed by someone else. Reload them and try again.");
        }

        var now = timeProvider.GetUtcNow();
        var changes = current.Update(
            command.DisplayName!.Trim(),
            AdministrationRules.Normalize(command.SupportEmail),
            AdministrationRules.Normalize(command.SupportPhone),
            AdministrationRules.Normalize(command.SupportInformation),
            command.ActorUserId,
            now);

        if (changes.Count > 0)
        {
            audit.Add(AdminAuditEntry.OrganizationSettingsUpdated(command.ActorUserId, changes, now));
            await unitOfWork.SaveChangesAsync(cancellationToken); // one statement batch: UPDATE (rowversion-checked) + INSERT audit
        }

        return await OrganizationSettingsMapper.ToDetailsAsync(current, settings, cancellationToken);
    }
}

public sealed class UpdateOrganizationSettingsValidator : AbstractValidator<UpdateOrganizationSettingsCommand>
{
    public UpdateOrganizationSettingsValidator()
    {
        RuleFor(c => c.ExpectedVersion).NotEmpty();
        RuleFor(c => c.DisplayName).NotEmpty().Must(v => !string.IsNullOrWhiteSpace(v)).WithMessage("Enter the organization name.")
            .MaximumLength(AdministrationRules.OrganizationNameMaxLength);
        RuleFor(c => c.SupportEmail).EmailAddress().MaximumLength(AdministrationRules.SupportEmailMaxLength)
            .When(c => !string.IsNullOrWhiteSpace(c.SupportEmail));
        RuleFor(c => c.SupportPhone).MaximumLength(AdministrationRules.SupportPhoneMaxLength);
        RuleFor(c => c.SupportInformation).MaximumLength(AdministrationRules.SupportInformationMaxLength);
    }
}
```

The max lengths are checked on the raw value, so leading or trailing spaces count. That is accepted and matches the other validators.

### 9 — Infrastructure: configurations, repositories, DbContext, DI

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/AdminAuditEntryConfiguration.cs`**

```csharp
internal sealed class AdminAuditEntryConfiguration : IEntityTypeConfiguration<AdminAuditEntry>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // rendered by Angular interpolation only; keeps non-ASCII compact (C7)
    };

    public void Configure(EntityTypeBuilder<AdminAuditEntry> builder)
    {
        builder.ToTable("AdminAuditLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd(); // bigint IDENTITY: append order breaks timestamp ties
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.TargetType).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.TargetId).IsRequired().HasMaxLength(36);
        builder.Property(e => e.TargetLabel).IsRequired().HasMaxLength(256);
        builder.Property(e => e.Changes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<AdminAuditChange>>(v, Json) ?? new List<AdminAuditChange>(),
                new ValueComparer<IReadOnlyList<AdminAuditChange>>(
                    (a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, c) => HashCode.Combine(h, c.GetHashCode())), v => v.ToList()))
            .IsRequired()
            .HasMaxLength(4000);

        // References only; nothing cascades (accounts are never deleted).
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.OccurredAtUtc, e.Id }).IsDescending(true, true); // newest-first audit page
    }
}
```

The usings are `System.Text.Encodings.Web`, `System.Text.Json`, `Microsoft.EntityFrameworkCore.ChangeTracking`, `Crm.Domain.Administration` and `Crm.Infrastructure.Identity`.

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/OrganizationSettingsConfiguration.cs`**

```csharp
internal sealed class OrganizationSettingsConfiguration : IEntityTypeConfiguration<OrganizationSettings>
{
    public void Configure(EntityTypeBuilder<OrganizationSettings> builder)
    {
        builder.ToTable("OrganizationSettings", t => t.HasCheckConstraint("CK_OrganizationSettings_Singleton", "[Id] = 1"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever(); // singleton: Id 1 (row inserted by the migration)
        builder.Property(s => s.DisplayName).HasMaxLength(150);
        builder.Property(s => s.SupportEmail).HasMaxLength(256);
        builder.Property(s => s.SupportPhone).HasMaxLength(40);
        builder.Property(s => s.SupportInformation).HasMaxLength(1000);
        builder.Ignore(s => s.IsConfigured);
        builder.Property<byte[]>("Version").IsRowVersion(); // ETag; overlapping saves ⇒ 409 (EfUnitOfWork)
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/AdminAuditRepository.cs`**

- `Add(entry) => dbContext.AdminAuditLogs.Add(entry)`.
- `ListAsync`:
  - `var q = dbContext.AdminAuditLogs.AsNoTracking();`
  - `action` ⇒ `q.Where(e => e.Action == action)`; `fromUtc` ⇒ `e.OccurredAtUtc >= fromUtc`; `toUtcExclusive` ⇒ `e.OccurredAtUtc < toUtcExclusive`.
  - `search`:
    - if `Guid.TryParse(search, out var id)`, then `var text = id.ToString(); q = q.Where(e => e.ActorUserId == id || e.TargetId == text);`
    - otherwise `q = q.Where(e => e.TargetLabel.Contains(search) || dbContext.Users.Any(u => u.Id == e.ActorUserId && (u.Email!.Contains(search) || (u.FirstName + " " + u.LastName).Contains(search))));`
  - `totalCount = await q.CountAsync(ct)`.
  - Page: `q.OrderByDescending(e => e.OccurredAtUtc).ThenByDescending(e => e.Id).Skip(...).Take(...)`.
  - Then project with a left join to `dbContext.Users` on `ActorUserId`: `ActorName = u.FirstName + " " + u.LastName`, `ActorEmail = u.Email`. Materialize to `AdminAuditEntryDetails`, with `Changes` read through the converter.
  - If EF cannot translate the converted `Changes` inside the join projection, select the page of entities plus the actor columns into an anonymous type first, then map in memory. **Record which path was taken.**
- **There are no Update or Remove members.**

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/OrganizationSettingsRepository.cs`**

```csharp
internal sealed class OrganizationSettingsRepository(CrmDbContext dbContext) : IOrganizationSettingsRepository
{
    public Task<OrganizationSettings?> FindAsync(CancellationToken cancellationToken = default) =>
        dbContext.OrganizationSettings.SingleOrDefaultAsync(s => s.Id == OrganizationSettings.SingletonId, cancellationToken);

    public byte[] GetVersion(OrganizationSettings settings) =>
        dbContext.Entry(settings).Property<byte[]>("Version").CurrentValue ?? [];

    public Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => (string?)(u.FirstName + " " + u.LastName))
            .SingleOrDefaultAsync(cancellationToken);
}
```

After `SaveChangesAsync`, EF refreshes the store-generated `Version`, so `GetVersion` returns the **new** value for the response ETag.

**File: `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`** — after line 42 add:

```csharp
    public DbSet<AdminAuditEntry> AdminAuditLogs => Set<AdminAuditEntry>();
    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();
```

Add `using Crm.Domain.Administration;`.

**File: `backend/src/Crm.Infrastructure/DependencyInjection.cs`** — after line 61 add:

```csharp
        services.AddScoped<IAdminAuditRepository, AdminAuditRepository>();
        services.AddScoped<IOrganizationSettingsRepository, OrganizationSettingsRepository>();
```

After line 80 add:

```csharp
        services.AddScoped<IAccountAdministration, AccountAdministration>();
```

### 10 — Migration

In `backend/`:

```powershell
dotnet ef migrations add AddAdministrationAuditAndSettings --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations
```

**Inspect the generated file before applying it.** It must contain only:
- `CreateTable("AdminAuditLogs")` with an FK to `AspNetUsers` (Restrict), `IX_AdminAuditLogs_ActorUserId` and the descending `(OccurredAtUtc, Id)` index;
- `CreateTable("OrganizationSettings")` with `CK_OrganizationSettings_Singleton`, an FK to `AspNetUsers` (Restrict) and `IX_OrganizationSettings_UpdatedByUserId`;
- **no** `AlterColumn`, `DropColumn` or `RenameColumn` on any existing table.

Then edit `Up` by hand. After the `OrganizationSettings` `CreateTable`, append:

```csharp
            // C9: the one settings row, not configured yet (all values NULL). Not HasData: keeps the model seed-free.
            migrationBuilder.Sql("INSERT INTO [OrganizationSettings] ([Id]) VALUES (1);");
```

`Down` drops both tables (generated). Apply it with `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api`. **Never reset `CrmDbContext`/`CrmDev` and never edit an earlier migration.**

### 11 — API: contracts, `AdministrationController`, `AgentsController`, `EntityTags`

**Create file: `backend/src/Crm.Api/Contracts/Administration/AdministrationContracts.cs`**

```csharp
using Crm.Application.Abstractions.Identity;
using Crm.Application.Administration;
using Crm.Domain.Administration;

namespace Crm.Api.Contracts.Administration;

/// <summary>Safe account fields only: never the security stamp, hash, lockout data or tokens.</summary>
public sealed record AdminAccountResponse(
    Guid Id, string Email, string FirstName, string LastName, string? PhoneNumber, string Role, bool IsActive, DateTimeOffset CreatedAtUtc)
{
    public static AdminAccountResponse From(UserAccount a) =>
        new(a.Id, a.Email, a.FirstName, a.LastName, a.PhoneNumber, a.Role, a.IsActive, a.CreatedAtUtc);
}

/// <summary>IsActive nullable so a missing field is 400, not "deactivate".</summary>
public sealed record SetAccountStatusRequest(bool? IsActive);

public sealed record AccountStatusResponse(AdminAccountResponse Account, bool Changed)
{
    public static AccountStatusResponse From(AccountStatusChange c) => new(AdminAccountResponse.From(c.Account), c.Changed);
}

public sealed record AdminAuditChangeResponse(string Field, string? OldValue, string? NewValue);

public sealed record AdminAuditLogResponse(
    long Id, DateTimeOffset OccurredAtUtc, AdminAuditAction Action, Guid ActorUserId, string? ActorName, string? ActorEmail,
    AdminAuditTargetType TargetType, string TargetId, string TargetLabel, IReadOnlyList<AdminAuditChangeResponse> Changes)
{
    public static AdminAuditLogResponse From(AdminAuditEntryDetails d) =>
        new(d.Id, d.OccurredAtUtc, d.Action, d.ActorUserId, d.ActorName, d.ActorEmail, d.TargetType, d.TargetId, d.TargetLabel,
            d.Changes.Select(c => new AdminAuditChangeResponse(c.Field, c.OldValue, c.NewValue)).ToList());
}

/// <summary>Version = base64 rowversion (the ETag value without quotes).</summary>
public sealed record OrganizationSettingsResponse(
    bool IsConfigured, string? DisplayName, string? SupportEmail, string? SupportPhone, string? SupportInformation,
    DateTimeOffset? UpdatedAtUtc, string? UpdatedByName, string Version)
{
    public static OrganizationSettingsResponse From(OrganizationSettingsDetails d) =>
        new(d.IsConfigured, d.DisplayName, d.SupportEmail, d.SupportPhone, d.SupportInformation, d.UpdatedAtUtc, d.UpdatedByName,
            Convert.ToBase64String(d.Version));
}

public sealed record UpdateOrganizationSettingsRequest(string? DisplayName, string? SupportEmail, string? SupportPhone, string? SupportInformation);
```

**Create file: `backend/src/Crm.Api/Controllers/AdministrationController.cs`** (class-level policy like `SlaController.cs` lines 15–18):

```csharp
[ApiController]
[Route(ApiRoutes.V1 + "/admin")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class AdministrationController(ISender sender) : ControllerBase
{
    private const string Concurrency = " Requires If-Match: the settings ETag (428 if missing, 400 if malformed, 409 if stale).";

    [EndpointDescription("Requires role: SuperAdmin. All accounts (SuperAdmin, Agent, Customer). search on name/email; role (SuperAdmin|Agent|Customer); isActive; one-based page (default 1), pageSize (default 20, clamped to 100). Order: last name, first name, email, id.")]
    [HttpGet("users")]
    public async Task<ActionResult<PagedResponse<AdminAccountResponse>>> ListUsers(
        [FromQuery] string? search, [FromQuery] string? role, [FromQuery] bool? isActive,
        [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListAccountsQuery(search, role, isActive, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<AdminAccountResponse>(
            result.Items.Select(AdminAccountResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin. One account (safe fields only).")]
    [HttpGet("users/{id:guid}")]
    public async Task<ActionResult<AdminAccountResponse>> GetUser(Guid id, CancellationToken cancellationToken) =>
        Ok(AdminAccountResponse.From(await sender.Send(new GetAccountQuery(id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin. Activates or deactivates an account; the role never changes. Deactivation revokes every session and makes existing access tokens fail immediately. Same state ⇒ 200 changed=false (no audit). Own account or the last active SuperAdmin ⇒ 409. Audited (AccountDeactivated / AccountReactivated).")]
    [HttpPut("users/{id:guid}/status")]
    public async Task<ActionResult<AccountStatusResponse>> SetUserStatus(Guid id, SetAccountStatusRequest request, CancellationToken cancellationToken) =>
        Ok(AccountStatusResponse.From(await sender.Send(new SetAccountStatusCommand(User.GetUserId(), id, request.IsActive), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin. Append-only administrative audit (Agent creation, account (de)activation, organization settings changes) since ADMIN-001. action; from/to (yyyy-MM-dd, UTC days, inclusive); search (actor/target id, actor name/email, target label); paging as /users. Newest first.")]
    [HttpGet("audit-logs")]
    public async Task<ActionResult<PagedResponse<AdminAuditLogResponse>>> ListAuditLogs(
        [FromQuery] AdminAuditAction? action, [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] string? search,
        [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListAuditLogsQuery(action, from, to, search, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<AdminAuditLogResponse>(
            result.Items.Select(AdminAuditLogResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin. Informational organization/support settings. isConfigured=false until first saved. Returns the ETag header.")]
    [HttpGet("organization-settings")]
    public async Task<ActionResult<OrganizationSettingsResponse>> GetSettings(CancellationToken cancellationToken) =>
        SettingsResult(await sender.Send(new GetOrganizationSettingsQuery(), cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin. displayName required (≤ 150); supportEmail optional valid email (≤ 256); supportPhone optional (≤ 40); supportInformation optional (≤ 1000). Identical values ⇒ nothing saved, same version, no audit. Changes ⇒ one OrganizationSettingsUpdated audit entry." + Concurrency)]
    [HttpPut("organization-settings")]
    public async Task<ActionResult<OrganizationSettingsResponse>> UpdateSettings(UpdateOrganizationSettingsRequest request, CancellationToken cancellationToken) =>
        SettingsResult(await sender.Send(
            new UpdateOrganizationSettingsCommand(
                User.GetUserId(), EntityTags.RequireIfMatch(Request), request.DisplayName, request.SupportEmail, request.SupportPhone, request.SupportInformation),
            cancellationToken));

    private ActionResult<OrganizationSettingsResponse> SettingsResult(OrganizationSettingsDetails details)
    {
        Response.Headers.ETag = EntityTags.Format(details.Version);
        return Ok(OrganizationSettingsResponse.From(details));
    }
}
```

**File: `backend/src/Crm.Api/Controllers/AgentsController.cs`:**

- lines 38–40 ⇒ `new CreateAgentCommand(User.GetUserId(), request.FirstName, request.LastName, request.Email, request.PhoneNumber, request.Password)`;
- lines 56–60 ⇒

  ```csharp
      public async Task<IActionResult> SetStatus(Guid id, SetAgentStatusRequest request, CancellationToken cancellationToken)
      {
          // Same audited command as /admin/users/{id}/status, restricted to Agent accounts (others 404). Response stays 204.
          await sender.Send(new SetAccountStatusCommand(User.GetUserId(), id, request.IsActive, UserRoles.Agent), cancellationToken);
          return NoContent();
      }
  ```
- update the `using`s: remove `Crm.Application.Agents.SetAgentStatus`; add `Crm.Application.Administration.SetAccountStatus` and `Crm.Domain.Identity`;
- update the `EndpointDescription` on line 53: "Requires role: SuperAdmin. Agent accounts only. Same rules and audit as PUT /api/v1/admin/users/{id}/status."

`SetAgentStatusRequest(bool IsActive)` is **unchanged** (the existing Angular client always sends it).

**File: `backend/src/Crm.Api/Http/EntityTags.cs`** line 24 — change the message to `"Send If-Match with the version you last loaded."` (it is now shared by tasks and settings). Change line 7's summary only if needed. Behaviour is unchanged.

### 12 — Backend unit test (exactly one)

See Test Plan item 1.

## Frontend Tasks

### 13 — Models, API, formatter, matrix data

**Create file: `frontend/src/app/features/admin/admin-models.ts`**

```ts
import { UserRole } from '../../core/auth/auth-models';

/** Matches Crm.Api AdminAccountResponse. */
export interface AdminAccount {
  id: string;
  email: string;
  firstName: string;
  lastName: string;
  phoneNumber: string | null;
  role: UserRole;
  isActive: boolean;
  createdAtUtc: string;
}

export interface AccountStatusResponse {
  account: AdminAccount;
  changed: boolean;
}

export const AUDIT_ACTIONS = ['AgentCreated', 'AccountDeactivated', 'AccountReactivated', 'OrganizationSettingsUpdated'] as const;
export type AuditAction = (typeof AUDIT_ACTIONS)[number];

export interface AuditChange {
  field: string;
  oldValue: string | null;
  newValue: string | null;
}

/** Matches Crm.Api AdminAuditLogResponse. */
export interface AuditLogEntry {
  id: number;
  occurredAtUtc: string;
  action: AuditAction;
  actorUserId: string;
  actorName: string | null;
  actorEmail: string | null;
  targetType: 'UserAccount' | 'OrganizationSettings';
  targetId: string;
  targetLabel: string;
  changes: AuditChange[];
}

/** Matches Crm.Api OrganizationSettingsResponse. version is sent back as If-Match. */
export interface OrganizationSettings {
  isConfigured: boolean;
  displayName: string | null;
  supportEmail: string | null;
  supportPhone: string | null;
  supportInformation: string | null;
  updatedAtUtc: string | null;
  updatedByName: string | null;
  version: string;
}

export interface UpdateOrganizationSettingsRequest {
  displayName: string;
  supportEmail: string | null;
  supportPhone: string | null;
  supportInformation: string | null;
}

export interface AccountListParams { page: number; pageSize: number; search: string; role: UserRole | ''; status: 'active' | 'inactive' | ''; }
export interface AuditListParams { page: number; pageSize: number; action: AuditAction | ''; from: string; to: string; search: string; }
```

**Create file: `frontend/src/app/features/admin/admin-api.ts`** (style of `tasks-api.ts` lines 16–41 and 111–113):
- `listUsers(p: AccountListParams): Observable<PagedResponse<AdminAccount>>` — `HttpParams` with `page`/`pageSize`. It sets `search` (trimmed, if non-empty), `role` (if set) and `isActive` (`'true'`/`'false'` from `status`, if set). `GET versioned('admin/users')`.
- `getUser(id)`: `GET admin/users/${id}`.
- `setUserStatus(id, isActive): Observable<AccountStatusResponse>`: `PUT admin/users/${id}/status` with `{ isActive }`.
- `listAuditLogs(p: AuditListParams)`: `GET admin/audit-logs`; empty values are omitted.
- `getSettings()`: `GET admin/organization-settings`.
- `updateSettings(version, body)`: `PUT admin/organization-settings`, `{ headers: { 'If-Match': `"${version}"` } }`.
- Import `PagedResponse` from `../customers/customer-models` (line 15).

**Create file: `frontend/src/app/features/admin/admin-format.ts`** (pure functions, no Angular imports except types):

```ts
export function auditActionLabel(action: AuditAction): string; // 'Agent created' | 'Account deactivated' | 'Account reactivated' | 'Organization settings updated'
export function auditFieldLabel(field: string): string;         // displayName→'Display name', supportEmail→'Support email', supportPhone→'Support phone', supportInformation→'Support information', isActive→'Active', role→'Role'; unknown → field
export function describeAuditChange(change: AuditChange): string;
// null/'' shown as '(empty)'; booleans 'true'/'false' → 'Yes'/'No'; e.g. "Display name: (empty) → Acme Support", "Active: Yes → No", "Role: (empty) → Agent"
export function statusConfirmation(account: { firstName: string; lastName: string; email: string; role: string }, activate: boolean): string;
// deactivate: "Deactivate Ada Lovelace (ada@example.com)? They are signed out everywhere immediately and cannot sign in until reactivated. Their tickets and history are kept."
// activate:   "Reactivate Ada Lovelace (ada@example.com)? They can sign in again with their existing password."
export function canDeactivate(account: { id: string; isActive: boolean }, currentUserId: string | null): boolean; // isActive && id !== currentUserId (UX only)
export function formatUtcTimestamp(iso: string): string; // 'Oct 18, 2026, 14:05 UTC' via Intl.DateTimeFormat('en-US', { timeZone: 'UTC', dateStyle: 'medium', timeStyle: 'short' }) + ' UTC'
```

**Create file: `frontend/src/app/features/admin/permission-matrix.ts`** — a typed static constant. **Transcribe exactly** (C11; sources: `docs/identity-and-access.md` lines 7–43 + Context item 16):

```ts
export type Access = { level: 'full' | 'scoped' | 'none'; note?: string };
export interface PermissionRow { ability: string; superAdmin: Access; agent: Access; customer: Access }
export interface PermissionModule { module: string; rows: PermissionRow[] }
export const PERMISSION_MATRIX: readonly PermissionModule[] = [ /* below */ ];
```

| Module | Ability | SuperAdmin | Agent | Customer |
|---|---|---|---|---|
| Account & sign-in | Sign in; view/update own profile; change own password | full | full | full |
| | Public sign-up | none | none | scoped "Creates a Customer account" |
| Customer Management | List/view/create contact-only/edit customers; notes, interactions, attachments (append-only) | full | full | none |
| | Own CRM customer profile | none | none | scoped "Own linked profile" |
| Tickets | Create tickets | scoped "Assign to an active Agent or leave unassigned" | scoped "Assigned to self" | none |
| | List/view/edit, change status, view activity | full | scoped "Currently assigned to them only (collaboration does not grant access)" | none |
| | Assign/reassign/unassign; clear escalation | full | none | none |
| | Escalate | full | scoped "Assigned to them" | none |
| Agent Dashboard & productivity | Own Agent Dashboard and task reminder inbox | none | full | none |
| | Tasks | scoped "Any active Agent; reassign" | scoped "Own tasks" | none |
| | Quick replies | scoped "Manage Shared" | scoped "Own Personal; read/copy Shared" | none |
| | Internal discussion / collaborators | full | scoped "Primary assignee or active collaborator; invite/remove: primary assignee only" | none |
| SLA & Automation | SLA policy, business calendar, automation rules, SLA overview | full | none | none |
| | SLA alerts (notification bell) | full | full | none |
| Knowledge Base | Read/search Published articles | full | full | none |
| | Create/edit Drafts, submit | scoped "Any Draft" | scoped "Own Drafts" | none |
| | Return/publish/archive/restore; manage categories | full | none | none |
| | Link articles to tickets | full | scoped "Assigned tickets" | none |
| AI assistance (staff) | Ticket summary/reply draft/category/solutions; AI chat | full | scoped "Assigned tickets only (chat: ticket context assigned only)" | none |
| Customer Portal | Submit/list/view own tickets and status history; CSAT feedback | none | none | scoped "Own linked profile; feedback on own Resolved/Closed tickets" |
| | Help Center | none | none | scoped "Published + Public articles only" |
| Reports & Management | Management dashboard and reports | full | none | none |
| Security & Administration | Users, account (de)activation, audit logs, organization settings, this matrix | full | none | none |
| | Create Agent accounts | full | none | none |
| | Create SuperAdmin / change any role | none "Bootstrap only; roles never change" | none | none |

### 14 — Pages, routes, navigation

**Create file: `frontend/src/app/features/admin/admin.routes.ts`**

```ts
export const ADMIN_ROUTES: Routes = [
  { path: '', pathMatch: 'full', redirectTo: 'users' },
  { path: 'users', title: 'Users | Support CRM', loadComponent: () => import('./admin-users-page').then(m => m.AdminUsersPage) },
  { path: 'users/:id', title: 'Account | Support CRM', loadComponent: () => import('./admin-user-detail-page').then(m => m.AdminUserDetailPage) },
  { path: 'permissions', title: 'Roles & Permissions | Support CRM', loadComponent: () => import('./permissions-page').then(m => m.PermissionsPage) },
  { path: 'audit-logs', title: 'Audit Logs | Support CRM', loadComponent: () => import('./audit-logs-page').then(m => m.AuditLogsPage) },
  { path: 'settings', title: 'Organization Settings | Support CRM', loadComponent: () => import('./organization-settings-page').then(m => m.OrganizationSettingsPage) },
];
```

**Create file: `frontend/src/app/features/admin/admin-section-nav.ts`** — standalone `crm-admin-section-nav`, inline template. It is a `<nav aria-label="Administration sections">` with 4 `mat-button` links (`/admin/users`, `/admin/permissions`, `/admin/audit-logs`, `/admin/settings`) and `routerLinkActive="active"` (`aria-current` set by `ariaCurrentWhenActive="page"`). It uses a flex-wrap layout so it wraps at 400 px. Every page renders it under its `crm-page-header`.

All pages: standalone, `ChangeDetectionStrategy.OnPush`, signals, `takeUntilDestroyed`, `styleUrls: ['../../shared/ui/productivity-page.scss', './admin-page.scss']`. **Create file: `admin-page.scss`** (≤ 2 kB): `.table-scroll { overflow-x: auto; }`, `.matrix th, .matrix td { padding; vertical-align: top; }`, `.section-nav { display: flex; flex-wrap: wrap; gap }`, `.scope { font: var(--mat-sys-body-small); }`.

**Create file: `admin-users-page.ts` / `.html`** — follow `quick-reply-list-page.ts` lines 52–118:
- filters form: `search` (debounced 300 ms), `role` (All / SuperAdmin / Agent / Customer), `status` (All / Active / Inactive);
- `Subject<AccountListParams>` + `switchMap` + per-request `catchError` ⇒ `error` signal;
- `MatPaginator` (`pageSize` 20, `[pageSizeOptions]="[20, 50, 100]"`);
- the toolbar has the "New Agent" link (`routerLink="/agents/new"`, icon `person_add`; C3);
- rows (`.rows` / `.row`): name, email, role chip text, `crm-status-badge` "Active"/"Inactive" (tone `success`/`neutral`), "Manage" link to `/admin/users/{id}`;
- states: progress bar while loading; error with **Retry** (`reload()`); empty "No accounts match these filters.".

**Create file: `admin-user-detail-page.ts` / `.html`**:
- Loads `getUser(id)`. It shows a `dl` with Account ID, Name, Email, Phone, Role ("fixed at creation"), Status badge and Created (`formatUtcTimestamp`). There is **no** password, stamp or token field (the API has none).
- Action button:
  - `canDeactivate(account, auth.currentUser()?.id ?? null)` ⇒ "Deactivate account" (`mat-stroked-button`, warn);
  - inactive ⇒ "Reactivate account";
  - own account ⇒ hint "You cannot deactivate your own account.", no button.
- Click ⇒ `window.confirm(statusConfirmation(account, activate))` (precedent `ticket-collaborators.ts` line 52); cancel ⇒ nothing. Then call `setUserStatus`:
  - success sets the account from `response.account`, with a snackbar "Account deactivated. All sessions were signed out." / "Account reactivated." / (when `changed === false`) "No change: the account was already active/inactive.";
  - error ⇒ `describeHttpError(error, 'Could not change the account status.')` shown with `role="alert"`; the 409 detail text (self / last SuperAdmin) is shown verbatim.
- `account.role === 'Agent'` ⇒ a note: "Assigned tickets stay assigned to this Agent. Reassign them from Tickets." (link `/tickets`), plus a link "Edit profile or set password" to `/agents/{id}` (existing page).
- `account.role === 'Customer'` ⇒ a note: "The CRM customer profile and ticket history are kept."
- Load error ⇒ message + Retry. A 404 shows the server detail.

**Create file: `permissions-page.ts` / `.html`**:
- One `<table class="matrix">` per module inside `.table-scroll`, with `<caption>` = module name. Columns: Ability / SuperAdmin / Agent / Customer.
- Cells: full ⇒ "✔ Allowed"; scoped ⇒ "◐ Limited" + `<span class="scope">note</span>`; none ⇒ "— Not allowed".
- The symbol is `aria-hidden="true"` and the text is always printed.
- Intro: "Roles are fixed at account creation. This page describes the access the server enforces; it does not change it."
- **There are no inputs, toggles or Save buttons.**

**Create file: `audit-logs-page.ts` / `.html`**:
- filters: Action select (All + `AUDIT_ACTIONS` with `auditActionLabel`), From / To (`<input matInput type="date">`, labelled "From (UTC)" / "To (UTC)"), Search (debounced, hint "Actor or target ID, name, email");
- client-side guard: To < From ⇒ inline error and no request; the server 400 is still authoritative (`describeSaveError` from `features/sla/sla-errors.ts`);
- list newest first: `formatUtcTimestamp(occurredAtUtc)`, `auditActionLabel(action)`, "by {{ actorName ?? actorUserId }} ({{ actorEmail }})", target "{{ targetLabel }}" + `targetId` in `.meta`, then `@for (c of entry.changes)` ⇒ `<li class="user-text">{{ describeAuditChange(c) }}</li>`;
- paginator; loading; error + Retry;
- empty: "No administrative changes match these filters. The audit log starts with ADMIN-001; earlier actions were not recorded."

**Create file: `organization-settings-page.ts` / `.html`**:
- Reactive form:
  - `displayName` (`required`, `maxLength(150)`);
  - `supportEmail` (`email`, `maxLength(256)`);
  - `supportPhone` (`maxLength(40)`);
  - `supportInformation` (`textarea`, `maxLength(1000)`, character counter `mat-hint align="end"`).
- On load: `form.reset(values ?? '')`; keep `version` in a signal. Show "Not configured yet" when `!isConfigured`, otherwise "Last updated {{ formatUtcTimestamp }} by {{ updatedByName }}".
- Save (disabled while `form.pristine`, invalid or saving):
  - success ⇒ if `response.version === previousVersion`, show snackbar "No changes to save."; else "Organization settings saved.". Reset the form to the response (pristine again).
  - 409 ⇒ message "These settings were changed by someone else. Reload to see the latest values." + a Reload button;
  - 400 ⇒ `describeSaveError(error, 'Could not save the settings.')`;
  - other ⇒ `describeHttpError`.
- Reload button re-fetches; the form disables while saving.
- Hint under the form: "Informational only. These values are not used for tickets, SLA or messages yet."

**File: `frontend/src/app/app.routes.ts`** — after line 89 (the end of the `reports` route), insert:

```ts
      {
        path: 'admin',
        canActivate: [roleGuard('SuperAdmin')],
        loadChildren: () => import('./features/admin/admin.routes').then(m => m.ADMIN_ROUTES),
      },
```

**File: `frontend/src/app/core/layout/navigation.ts`** — after line 29 (`Reports`), insert:

```ts
  { label: 'Administration', icon: 'admin_panel_settings', path: '/admin', exact: false, roles: ['SuperAdmin'] },
```

**No change** to `features/agents/*`. The existing toggle on `/agents/{id}` keeps calling `PUT /agents/{id}/status`, which is now audited server-side (C4).

### 15 — Frontend unit test (exactly one)

See Test Plan item 2.

### 16 — Docs

- **Create file: `docs/security-administration.md`** with these sections:
  1. Scope (AD1–AD4) and the role table from this plan.
  2. Accounts: the status rules table; revocation mechanics (C1: per-request `IsActive` + stamp check, sessions revoked, 401 with no account details); initial password handling (C2); fixed roles.
  3. Permission matrix maintenance (C11: edit `permission-matrix.ts` and `identity-and-access.md` together).
  4. Audit: the 4 actions, fields, atomicity (C8), append-only via the CRM (DB operators are outside the CRM permission model, so the log is **not** physically immutable), "starts with ADMIN-001", not audited: C6.
  5. Organization settings: fields, validation, initial state (C9), ETag (C10), informational only.
  6. Routes table.
  7. Follow-ups: audit Agent profile edits and password resets; audit retention/export; using the settings in the portal or branding.
- `docs/identity-and-access.md`:
  - line 13 ⇒ append " (status changes and creation are audited; see [security-administration.md](security-administration.md))";
  - after line 42 add the row `| Administration: users, account (de)activation (any role), audit logs, organization settings, permission matrix | ✔ | — | — |`;
  - after line 94 add the 4 `/api/v1/admin/*` route rows;
  - line 112 ⇒ append " Verified for ADMIN-001 with retained Agent and Customer tokens."
- `docs/api-conventions.md`:
  - line 3 ⇒ add "`/api/v1/admin/*` (ADMIN-001)" to the module-prefixed exceptions;
  - line 22 ⇒ "(versioned resources: `/api/v1/tasks`, `/api/v1/admin/organization-settings`)".
- `docs/persistence-and-transactions.md`: after line 12 add the bullet: "**Administration (ADMIN-001):** `CreateAgentCommand` and `SetAccountStatusCommand` are `ITransactionalCommand`s. Identity saves, session revocation and the `AdminAuditLogs` insert commit together. `SetAccountStatusCommand` locks the target `AspNetUsers` row (and all SuperAdmin rows when deactivating a SuperAdmin) with `UPDLOCK, HOLDLOCK` through `IAccountAdministration`. Settings + audit is one save; `OrganizationSettings.Version` is a rowversion ETag."
- `README.md` line 3 ⇒ insert ", Reports & Management (`REPORT-001`) and Security & Administration (`ADMIN-001`)" in place of " and Reports & Management (`REPORT-001`)".

---

## Edge Cases & Failure Modes

- **Agent/Customer token on any `/admin/*` route:** 403 (class-level `SuperAdmin` policy). No token ⇒ 401 (fallback, `SecurityRegistration.cs` line 62). A forged `role` claim fails the signature check.
- **Agent or Customer opening `/admin/...` in Angular:** `roleGuard('SuperAdmin')` redirects to `/`. The nav entry is filtered out (`shell.ts` line 38).
- **Retained JWT after deactivation** (the core objective):
  - the next request with that token ⇒ 401 from `OnTokenValidated` (`!user.IsActive`, and the stamp differs), on **every** protected controller;
  - the refresh cookie ⇒ rejected and the family revoked (`RenewSessionCommand.cs` lines 92–102; sessions are already revoked with `AccountDisabled`);
  - a new login ⇒ the uniform 401 "Invalid email or password." (`IdentityService.cs` line 47). No account state is disclosed.
- **Reactivation:** the old tokens stay invalid (the stamp was rotated again on reactivation and the sessions were revoked on deactivation). Only a fresh login works.
- **Deactivated SuperAdmin with the Admin page open:** their next API call is 401. The existing interceptor handles 401 as a session end. **Do not** add special handling.
- **Self-deactivation:** 409 (`SetAccountStatusCommand`), plus the button is hidden (`canDeactivate`). A crafted request is still refused server-side.
- **Last active SuperAdmin:** 409, after locking all SuperAdmin rows. Concurrent mutual deactivation is safe through the lock, a deadlock victim and a retry (C5). A 1205 deadlock is retried by `EnableRetryOnFailure` (`Infrastructure/DependencyInjection.cs` line 37); the retried handler sees the committed state.
- **Repeated or concurrent same-state requests:** the second request waits on the row lock, sees the committed state and returns `changed: false`. **No second audit row, no second stamp rotation.**
- **Concurrent opposite requests (deactivate vs reactivate):** they serialize. Both apply in commit order, and there are two audit rows in the same order as the final state. No contradictory pair is possible, because each entry's `oldValue` equals the committed state it read under the lock.
- **Account id that does not exist / non-Guid:** `{id:guid}` route constraint ⇒ 404 for a non-Guid; unknown Guid ⇒ 404 "Account '…' was not found."
- **`/agents/{id}/status` with a Customer or SuperAdmin id:** 404 "Agent '…' was not found." (as before, `RequiredRole`).
- **Missing `isActive` in the body:** 400 `isActive`. `{ "isActive": "yes" }` ⇒ 400 from model binding.
- **Forged role fields** (`{ "role": "SuperAdmin" }` on `POST /agents`, `{ "role": …, "isActive": true }` on status): unknown JSON members are ignored by `System.Text.Json`. The role is always Agent at creation and is never written afterwards. **No endpoint accepts a role.**
- **Duplicate email on Agent creation:** 409 (`CreateAgentCommand` pre-check or `ThrowIfFailed` line 148). The transaction rolls back and there is **no audit row**.
- **Weak password:** 400 from the validator (`AccountRules.NewPassword`) or Identity (`password` key). No user and no audit.
- **Audit insert fails** (e.g. a DB error during `SaveChanges`): the whole transaction rolls back. The user is not created and the status is not changed (`EfUnitOfWork.cs` lines 63–76). An unknown commit outcome ⇒ 503 `CommitOutcomeUnknownException` (lines 83–88), and the client reloads.
- **Organization settings without If-Match:** 428. Malformed ⇒ 400 `ifMatch`. Stale ⇒ 409 and **no audit**.
- **Settings identical values** (including only whitespace differences that normalize to the same text): `Update` returns no changes ⇒ no save, no audit, same version.
- **Settings blank optional fields:** normalized to `null`. Clearing a value records `oldValue → null` ("(empty)" in the UI).
- **Display name of only spaces:** 400 "Enter the organization name.".
- **Invalid support email:** 400 `supportEmail`. An empty string is allowed (optional).
- **Overlong values:** 400 per field. The Angular form blocks them first (`maxLength` + counter).
- **Long or unicode support information in the audit:** truncated to 200 characters with "…" (`AdminAuditChange.Of`). Arabic and emoji are stored as-is (relaxed encoder). It is rendered by interpolation in `.user-text`.
- **`<script>` in names, settings or labels:** shown literally (interpolation). Grep check: no `innerHTML` under `features/admin/`.
- **Missing settings row** (manual DB edit): 500 logged with "apply the migrations" (C9). It is not a user path.
- **Audit filters:** `to < from` ⇒ 400 `to`; `from < 2000-01-01` ⇒ 400 `from`; `from=2026-13-01` ⇒ 400 (model binding); `action=Deleted` ⇒ 400 (enum binding). A search > 100 characters ⇒ 400. `pageSize=500` ⇒ clamped to 100.
- **Audit search with a Guid:** exact actor or target id match. Text search: target label (email / "Organization settings") or the actor's current name/email.
- **Renamed actor:** the audit keeps `ActorUserId`. The displayed name is the current one. The target label is the email at the time of the event (snapshot).
- **Audit before ADMIN-001:** none. The empty state says so. No backfill.
- **Deactivated Agent's tickets:** unchanged. Reports keep their attribution (REPORT-001 R5 relies on fixed roles, which is still true). The SuperAdmin reassigns through Tickets.
- **Deactivated Customer:** the portal is inaccessible. The CRM `Customers` row and `Customers.UserId` link are unchanged. Staff still see and serve the customer.
- **Concurrent signup while a SuperAdmin-deactivation transaction holds `HOLDLOCK` on SuperAdmin rows:** the signup inserts other keys and is not blocked by row locks. If the optimizer scans `AspNetUsers` for the `IN` subquery, the range lock can make a concurrent signup wait until the status change commits (milliseconds). Accepted.
- **Narrow screens (400 px):** the section nav wraps; the user rows wrap; the matrix tables scroll inside `.table-scroll` (no page scroll); the forms are single-column.
- **No AI or messaging:** no admin handler references `IAiTextGenerator`, an email service or a notification sender (grep in Verification).

---

## Test Plan

1. **Backend unit (the only new backend test) — `backend/tests/Crm.UnitTests/Administration/SetAccountStatusCommandHandlerTests.cs`:**
   - `[Fact] Handle_DeactivateActiveAgent_RotatesAccessRevokesSessionsAndWritesOneAuditEntryInTheSameSave`.
   - Arrange:
     - an active Agent `UserAccount`;
     - a `FakeAccountAdministration`, where `LockAccountAsync` returns `LockedAccountRow(id, true)` and `LockSuperAdminsAsync` throws `NotSupportedException` (it must not be called for an Agent);
     - a `FakeIdentityService`: `FindByIdAsync` returns the account, then the updated copy after `SetActiveAsync`; it records `SetActiveAsync(id, false)`; all other members throw `NotSupportedException` (pattern `LoginCommandHandlerTests.cs` lines 34–47);
     - a `FakeRefreshSessionRepository` recording `RevokeAllForUserAsync(id, AccountDisabled, now)`;
     - a `FakeAdminAuditRepository` recording `Add`;
     - a `FakeUnitOfWork` counting saves (lines 68–74);
     - a fixed `TimeProvider` (a nested `FixedTimeProvider : TimeProvider` overriding `GetUtcNow`).
   - Act: `Handle(new SetAccountStatusCommand(actorId, agentId, IsActive: false), CancellationToken.None)`.
   - Assert:
     - `Changed == true` and `Account.IsActive == false`;
     - `SetActiveAsync` was called once with `false`;
     - one revocation with `AccountDisabled` at `now`;
     - exactly one audit entry: `Action == AccountDeactivated`, `ActorUserId == actorId`, `TargetType == UserAccount`, `TargetId == agentId.ToString()`, `TargetLabel == email`, `OccurredAtUtc == now`, and a single change `("isActive", "true", "false")`;
     - `SaveCount == 1`.
   - No other new test (intake §11). The existing 13 tests are unchanged ⇒ **14 total**.
2. **Frontend unit (the only new frontend test) — `frontend/src/app/features/admin/admin-format.spec.ts`:**
   - one `describe` with one `it('labels audit actions, describes safe changes, builds confirmations and never offers self-deactivation', …)` (pattern `report-format.spec.ts`). It asserts:
     - `auditActionLabel('AccountDeactivated')` = 'Account deactivated';
     - `describeAuditChange({ field: 'isActive', oldValue: 'true', newValue: 'false' })` = 'Active: Yes → No';
     - `describeAuditChange({ field: 'displayName', oldValue: null, newValue: 'Acme Support' })` = 'Display name: (empty) → Acme Support';
     - `statusConfirmation(…, false)` contains 'signed out everywhere';
     - `canDeactivate({ id: 'a', isActive: true }, 'a') === false`, `canDeactivate({ id: 'b', isActive: true }, 'a') === true`, `canDeactivate({ id: 'b', isActive: false }, 'a') === false`;
     - `formatUtcTimestamp('2026-10-18T14:05:00Z')` = 'Oct 18, 2026, 2:05 PM UTC' (record the exact `Intl` output once and assert it).
   - **14 total.**
3. **Existing suites:** all 13 backend + 13 frontend tests pass unchanged. No existing test file is edited (`IIdentityService` is unchanged; `CreateAgentCommand` and `SetAgentStatusCommand` have no tests).
4. **Manual (no new automated suites):** Verification steps 5–19 cover JWT revocation, transactions, SQL and the browser, as intake §11 requires.

---

## Migration / Rollback

- **Forward:** `AddAdministrationAuditAndSettings` creates `AdminAuditLogs` and `OrganizationSettings`, plus one `INSERT` of the settings row. Purely additive: no existing table, column, index or row is touched.
- **Before applying:** record `SELECT COUNT(*)` for `AspNetUsers`, `Tickets`, `TicketActivities`, `TicketFeedback`, `Customers`, `RefreshSessions`. Re-check them after applying ⇒ identical.
- **Rollback (before any audit data matters):** `dotnet ef database update AddTicketFeedback --project src/Crm.Infrastructure --startup-project src/Crm.Api` drops both new tables (loses audit rows and settings, nothing else). Then `dotnet ef migrations remove` and revert the code. Reverting the code without the DB rollback is harmless (unused tables).
- **Half-applied state:** the migration runs in one EF migration transaction. On failure nothing is created; re-run after fixing. With the migration pending, the bootstrapper logs "skipped: pending migration" (`IdentityBootstrapper.cs` lines 23–30) and the settings endpoints return 500 (missing table). Apply the migration first.
- **Code/DB order:** apply the migration, then start the new API. The old API ignores the new tables.

---

## Verification Steps

1. **Baseline:** in the repo root, `git status` ⇒ only the untracked security-administration intake/plan folders; `git log --oneline -3` ⇒ `REPORT-001` present. In `backend/`: `dotnet test` ⇒ 13 passed. In `frontend/`: `npm test -- --watch=false` ⇒ 13 passed. Record `npx ng build` warnings (budget baseline).
2. **Backend builds and tests:** in `backend/`: `dotnet build` (0 errors; list pre-existing warnings separately), then `dotnet test` ⇒ **14 passed**.
3. **Migration and layering:** in `backend/`:
   - review the generated migration (task 10 checklist), then apply it;
   - `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api` ⇒ no changes;
   - SQL `SELECT * FROM OrganizationSettings` ⇒ one row, Id 1, all values NULL; row counts from Migration / Rollback unchanged;
   - `git grep -n "EntityFrameworkCore\|Microsoft.AspNetCore" -- src/Crm.Application/Administration src/Crm.Domain/Administration` ⇒ nothing;
   - `git grep -n "SetAgentStatusCommand\|RemoveFromRole" -- src` ⇒ nothing;
   - `git grep -n "IAiTextGenerator\|Gemini\|Email.*Send" -- src/Crm.Application/Administration src/Crm.Api/Controllers/AdministrationController.cs` ⇒ nothing.
4. **Frontend builds and tests:** in `frontend/`: `npx ng build` (no errors; compare budget warnings; each admin component style ≤ 4 kB), then `npm test -- --watch=false` ⇒ **14 passed**. `git grep -n "innerHTML" -- frontend/src/app/features/admin` ⇒ nothing.
5. **Setup:** `.\run-dev.ps1`.
   - Tokens via `POST /api/v1/auth/login`: `$admin` (SuperAdmin), an existing Customer `$cust`. Never paste token values into the report.
   - As `$admin`: `POST /api/v1/agents` creating "Admin Test Agent" (`admin.test.agent@example.test`, a fresh password that is not committed anywhere) ⇒ 201. Log in as that Agent ⇒ `$agent`.
6. **Authorization (§11.2):**
   - every `/api/v1/admin/*` route (6 operations): no token ⇒ 401, `$agent` ⇒ 403, `$cust` ⇒ 403, `$admin` ⇒ 200 (PUTs with valid bodies);
   - Angular: Agent and Customer see no "Administration" entry, and typing `/admin/users` redirects to `/`.
7. **Live-token deactivation, Agent (§11.6):**
   - `GET /api/v1/tickets` with `$agent` ⇒ 200;
   - as `$admin`: `PUT /api/v1/admin/users/{agentId}/status {"isActive":false}` ⇒ 200 `changed:true`;
   - immediately repeat `GET /api/v1/tickets`, `GET /api/v1/agent-dashboard` and `GET /api/v1/knowledge-base/articles` with the **retained** `$agent` ⇒ 401 each, and the body has no account detail;
   - `POST /auth/login` as the Agent ⇒ 401 "Invalid email or password.";
   - SQL `SELECT COUNT(*) FROM RefreshSessions WHERE UserId='<agentId>' AND RevokedAtUtc IS NULL` ⇒ 0;
   - reactivate ⇒ 200 `changed:true`; the old `$agent` still ⇒ 401; a fresh login ⇒ 200 and `GET /tickets` ⇒ 200.
8. **Live-token deactivation, Customer:** the same with `$cust` against `GET /api/v1/portal/tickets` and `GET /api/v1/me`. Then reactivate and log in fresh.
9. **Users list/detail (§11.3):**
   - `GET /admin/users` ⇒ all roles; `?role=Agent`, `?isActive=false`, `?search=admin.test` filter correctly; `?pageSize=1&page=2` pages; totals match SQL `SELECT COUNT(*) FROM AspNetUsers` (with the role join for the role filter);
   - inspect the JSON: no `securityStamp`, `passwordHash`, `concurrencyStamp`, lockout or token fields;
   - `GET /admin/users/{randomGuid}` ⇒ 404.
10. **Agent creation and role immutability (§11.4–§11.5):**
    - `POST /agents` with an extra `"role":"SuperAdmin"` ⇒ 201 and role Agent (SQL `AspNetUserRoles` ⇒ exactly one row, Agent);
    - duplicate email ⇒ 409; password `short` ⇒ 400; neither leaves an audit row;
    - the new Agent can log in and use the staff UI;
    - confirm no endpoint changes roles (`git grep -n "RemoveFromRoleAsync\|AddToRoleAsync" -- backend/src` ⇒ only `IdentityService.cs` line 78 and `IdentityBootstrapper.cs` line 73);
    - Customer self-registration (`/sign-up`) and restart bootstrap still work.
11. **Anti-lockout (§11.7):**
    - as `$admin`: `PUT /admin/users/{ownId}/status {"isActive":false}` ⇒ 409 "You cannot deactivate your own account.";
    - with a single SuperAdmin, there is no other SuperAdmin to target, so the last-SuperAdmin path is exercised by the self check. Record "last-SuperAdmin rule verified by code review + unit-level reasoning; not reproducible without a second SuperAdmin (none can be created via the API)". **Do not** insert a SuperAdmin by SQL unless the user agrees;
    - `{}` ⇒ 400 `isActive`;
    - SQL ⇒ no audit rows for these attempts.
12. **Deactivation history (§11.8):** before deactivating the Agent, assign one ticket to them (as `$admin`). After deactivating:
    - `Tickets.AssignedAgentUserId` is unchanged and `TicketActivities` is unchanged (SQL counts);
    - `/reports/agents` still lists them;
    - the ticket can be reassigned to another active Agent through Tickets;
    - customer linkage is unchanged for the Customer (`Customers.UserId`).
13. **No-op and concurrency (§11.9):**
    - deactivate twice ⇒ second response `changed:false`; SQL `AdminAuditLogs` shows one `AccountDeactivated` for the pair; `SecurityStamp` unchanged by the second call;
    - optionally fire two deactivations in parallel (two PowerShell jobs) ⇒ one `changed:true`, one `changed:false`, one audit row.
14. **Audit (§11.11–§11.13):**
    - SQL `SELECT Action, ActorUserId, TargetId, TargetLabel, Changes, OccurredAtUtc FROM AdminAuditLogs ORDER BY Id` ⇒ exactly one row per successful creation, toggle and settings save from steps 5–15, with the correct actor, target and changes, and **no password, stamp or token** anywhere (`Changes LIKE '%password%'` ⇒ none);
    - `GET /admin/audit-logs` ⇒ newest first; `?action=AgentCreated` filters; `?from=<today>&to=<today>` includes them; `?from=2000-01-01&to=2000-01-02` ⇒ empty; `?to=2026-01-01&from=2026-02-01` ⇒ 400; `?search=<agentId>` and `?search=admin.test` match;
    - there is no PUT/DELETE route (`OPTIONS`/OpenAPI shows GET only).
15. **Audit integrity (§11.12):** prove rollback without corrupting data. In a SQL session run `BEGIN TRAN; SELECT * FROM AdminAuditLogs WITH (TABLOCKX);` and keep it open. Send a status change with a short client timeout, then `ROLLBACK` the blocking session. Alternatively, inspect the `SetAccountStatusCommand` transaction boundary by code review. Expected: the business change and the audit row commit together or not at all. **Record which method was used.**
16. **Organization settings (§11.14–§11.15):**
    - `GET` ⇒ `isConfigured:false`, all null, an `ETag` header;
    - PUT without If-Match ⇒ 428; with `"bad"` ⇒ 400; valid ⇒ 200, the new version, one `OrganizationSettingsUpdated` row listing the 4 fields;
    - PUT with the same values and the new version ⇒ 200, same version, **no** new audit row;
    - PUT with the old version ⇒ 409, no audit;
    - change only `supportPhone` ⇒ one audit row with one change;
    - blank `displayName` / `supportEmail = "nope"` / 151-character name ⇒ 400 with field keys, and the Angular form shows the messages;
    - restart the API ⇒ values persist (SQL);
    - SLA policy, ticket creation and the AI config are unaffected (no code reads `OrganizationSettings`: `git grep -n "OrganizationSettings" -- backend/src` ⇒ only administration files, configuration, DbContext and migration).
17. **Permissions page (§11.10):** compare every row with `docs/identity-and-access.md` lines 7–43. There are no inputs or buttons. It works at 400 px (horizontal scroll inside the table box only).
18. **Browser UX (§11.18):** as SuperAdmin, at desktop width and at ~400 px:
    - all four sections load; the section nav works;
    - users: search, filters and paging work; detail; deactivate → confirm dialog → badge changes; cancel does nothing; own account has no button;
    - audit: filters, date validation, empty state text, Retry after stopping the API;
    - settings: save / no-change / 409 Reload / validation;
    - sign-out and sign-in as another role ⇒ no Administration entry;
    - existing nav entries unchanged.
19. **Regression smoke (§11.16–§11.17):**
    - login/registration for each role; SuperAdmin bootstrap restart; token refresh (wait > 10 minutes or check the refresh call after reload);
    - Agent current-assignment ticket access;
    - Customer portal ownership and CSAT;
    - ticket create/workflow; Agent Dashboard; SLA overview and alerts; KB publish; staff AI only on click; Reports;
    - DevTools Network on Admin pages ⇒ no `/ai/` calls.
20. **OpenAPI:** `/openapi/v1.json` ⇒ 6 admin operations, each "Requires role: SuperAdmin", `action` enum as strings, `from`/`to` as `date`; `/agents/{id}/status` description updated.
21. **Record honestly:**
    - list which of steps 5–20 were run and the observed results;
    - anything not run (especially 11 last-SuperAdmin, 13 parallel, 15 rollback simulation, 18 browser) is **Unverified**, not passed;
    - report `dotnet build`, `dotnet test`, `ng build`, `npm test`, `has-pending-model-changes`, the migration review, which `AdminAuditRepository` projection path was taken (task 9), C1–C14 and the remaining limitations;
    - keep pre-existing warnings separate;
    - list the demo data created (the Agent from step 5, settings values, the ticket assignment from step 12).

    **Do not commit or push before the user accepts ADMIN-001.**

---

## Done Criteria

- [ ] Every new `/api/v1/admin/*` endpoint is SuperAdmin-only (Agent/Customer 403, anonymous 401). The actor always comes from the JWT. No response exposes a password, hash, security stamp, token or secret.
- [ ] Users: a paged, searchable, role- and status-filterable list of all accounts plus account detail, both matching SQL. Agent creation goes through the existing role-forced `POST /api/v1/agents` (C2, C3).
- [ ] Deactivate/reactivate works for any account through one audited command (also used by `/agents/{id}/status`). It refuses self-deactivation and the last active SuperAdmin with 409, returns `changed:false` with no audit for same-state requests, and serializes concurrent toggles (C4, C5).
- [ ] A JWT issued before deactivation is refused on existing protected endpoints (verified for Agent and Customer), new login and refresh are refused, and reactivation requires a fresh login (C1).
- [ ] No role can change after creation; no role-changing code path exists (`RemoveFromRoleAsync` absent; `AddToRoleAsync` only at creation and bootstrap).
- [ ] The read-only permissions page shows all three roles, every listed module and the scoped restrictions (assignment-only Agent tickets, own linked profile, Published + Public Help Center, SuperAdmin-only publish, Reports and Admin), with no editable controls and no permission storage (C11).
- [ ] `AdminAuditLogs` records exactly `AgentCreated`, `AccountDeactivated`, `AccountReactivated` and `OrganizationSettingsUpdated`, with actor id, target type, id and label, UTC time and bounded safe changes. Each entry commits atomically with its change (C7, C8). No-ops, validation failures and refusals write nothing. No edit or delete API exists.
- [ ] The audit list is newest first with an Id tie-breaker, has action, UTC date-range and search filters with bounded paging, and validates its input.
- [ ] Organization settings: the 4 fields persist in SQL across restarts. Validation is server-side with friendly Angular errors. Initial state is "not configured". ETag/If-Match prevents lost updates. Exactly one audit entry is written per real change and none per identical save (C9, C10).
- [ ] One additive migration (2 tables + 1 row). `has-pending-model-changes` reports none. Existing row counts are unchanged.
- [ ] Angular: the SuperAdmin-only Administration route group with 4 sections, confirmations, loading/empty/error/Retry, works at 400 px, no `[innerHTML]`, existing navigation unaffected.
- [ ] Exactly one new backend test and one new frontend test (14/14). All existing tests pass unchanged. Both builds succeed.
- [ ] `docs/security-administration.md` created. Identity, API conventions, persistence docs and README updated.
- [ ] Manual matrix (Verification 5–20) recorded honestly. Unrun steps are marked Unverified. Nothing is committed or pushed until the user reviews and accepts ADMIN-001.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
