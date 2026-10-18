# Story 14 — Security & Administration — Business Feature Intake

## Feature

- **Display name:** Security & Administration — User Management, Roles & Permissions, Administrative Audit Logs & Organization Settings
- **Feature slug:** `security-administration`
- **Work item ID:** `ADMIN-001`
- **Work item type:** `Business Feature Story`
- **Story number:** `14` (verify against the repository's current Squad-Kit index)
- **Tracker:** `none` (local training/assessment CRM)
- **Status:** `Business decisions AD1–AD4 approved — ready for Squad-Kit planning`
- **Labels:** `administration, security, users, agents, identity, deactivation, jwt, roles, permissions, audit, configuration, aspnet-core, angular, squad-kit, training`

## Title

Security & Administration — SuperAdmin User Provisioning, Effective Account Deactivation, Read-Only Permission Matrix, Append-Only Audit Trail and Organization Settings

## 1. Business goal and authoritative scope

Implement **all four** areas named in the original two-page *Customer Support CRM — Core Features* document under **Security & Administration**:

1. **Users and roles:** SuperAdmin views/searches user accounts, securely creates Agent accounts, and deactivates/reactivates accounts while keeping their fixed roles and history.
2. **Permissions:** SuperAdmin views an accurate, **read-only** matrix of the three existing roles, permitted actions, and important record-level scope restrictions.
3. **Audit logs:** the backend records successful administrative changes, and SuperAdmin can search/filter/page the resulting append-only audit trail.
4. **System configuration:** SuperAdmin maintains a minimal set of **non-sensitive informational organization settings**, with actual changes audited.

Deliver working .NET/Angular/API/database behavior, not decorative UI or static fake audit records. The user explicitly approved **AD1–AD4** (§3); those decisions are binding. This remains a **training/assessment project**: complete the four basic business workflows without enterprise IAM, multi-factor identity providers, flexible RBAC infrastructure, event sourcing or a security-information-and-event-management platform.

**Core security objective:** disabling an account must actually prevent its bearer tokens from accessing protected backend endpoints, **including a JWT issued before deactivation**. Hiding the account in the UI or only blocking future logins is insufficient.

## 2. Existing application, dependencies and non-regression

- Extend the existing CRM monorepo and its established .NET 10 Clean Architecture: `Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`; ASP.NET Core Identity + JWT; Mediator/CQRS, FluentValidation, EF Core 10 and SQL Server LocalDB (`CrmDev`). Existing APIs use `/api/v1` conventions. Frontend is Angular 22 standalone/Angular Material with typed API clients and role guards.
- Stories **01–13 have been accepted**; the user confirmed that **Story 13 `REPORT-001` was committed**. The planner/executor must still check the actual `git log`, current `.squad` index and `git status` before implementation; do **not** assume a particular commit hash or clean tree without checking.
- **AUTH-001** already implements identity, JWT issuance/validation, three roles (`SuperAdmin`, `Agent`, `Customer`), login/registration/bootstrap, and an account active-status field according to the repository findings in prior stories. **Inspect actual code:** existing service methods, activation semantics, password provisioning, security stamp, token-validation hooks, Identity and CRM profiles. Do not assume currently issued JWTs are revoked by the `IsActive` flag without verification.
- **CUST-001/CUST-002** use CRM `Customer` records; registered Customer profile linkage is `Customers.UserId` (verified in Story 12 planning), and contact-only profiles remain separate. Do not create/claim CRM Customer records or alter customer registration when provisioning Agents.
- **TICK-001/TICK-002** enforce staff Ticket ownership: SuperAdmin any; Agent only currently assigned; Customer only separately authorized portal tickets. Existing TicketActivity records handle ticket lifecycle; **do not replace or duplicate TicketActivity** with this administrative audit feature.
- **SLA-001**, Knowledge Base configuration, AI-001 Gemini backend configuration, Customer Portal, and **REPORT-001** must not regress. In particular, REPORT-001 Agent historical resolution attribution assumes roles remain fixed: **no post-creation role changes**.
- Configuration ownership stays separate: this story does not edit SLA policies/business calendars, automation rules, Knowledge Base categories, Gemini keys/models, JWT secrets, database connection strings, ticket defaults, or Communication Channels settings.
- **Communication Channels stay the final module.** Agent provisioning must not require implementing email/SMS delivery; support email/phone settings are informational only. Platform branding/languages/multi-branch and external Integrations remain later modules.
- Verify existing DbContext/Identity storage arrangement, EF migrations, UnitOfWork/transaction patterns and startup bootstrap before deciding how admin mutations and audit records can be committed together. Add only the minimum schema needed for actual audit entries and informational settings; use additive migrations; never reset `CrmDev` or rewrite prior migrations.

## 3. Approved business decisions — AD1–AD4

All four rows were **explicitly approved by the user**. Do not reopen them without a material incompatibility discovered in the actual repository.

| ID | Approved choice | Binding requirements |
| --- | --- | --- |
| **AD1 — User Management** | **Option A: Standard User Administration.** | SuperAdmin lists/searches accounts, views name/email/fixed role/active status, securely provisions Agent accounts, and deactivates/reactivates existing accounts. Account history is retained. Customer registration and SuperAdmin bootstrap remain unchanged; **no role changes after creation**. Disabled accounts must lose access to protected APIs even with previously issued JWTs. |
| **AD2 — Roles & Permissions** | **Option A: Read-only matrix.** | SuperAdmin can inspect existing `SuperAdmin`, `Agent`, `Customer` module/action access and assignment-/ownership-based restrictions. Existing backend authorization is authoritative. **No editable permissions, new authorization engine or new permission tables.** |
| **AD3 — Audit Logs** | **Option A: Focused administrative audit.** | Record successful Agent creation, account activation/deactivation, and AD4 actual setting changes, with actor, action, target, timestamp and safe details. Append-only via CRM; SuperAdmin-only searchable/filterable/paginated history. No secrets. Persist audit with business change atomically **where supported**. |
| **AD4 — System Configuration** | **Option A: Basic informational settings.** | SuperAdmin can read/edit and persist organization display name, support contact email, support contact phone and optional support information. Validate server-side. Audit **actual changes only**. No secret management, sending communications, ticket workflow changes or duplication of existing SLA/automation/KB settings. |

**Approval gate:** AD1–AD4 complete. Proposed implementation defaults (endpoints, audit column names, initial values, pagination, admin self-deactivation safeguards, password-provisioning UI) below are **not** separately user-approved design decisions. Choose the smallest secure solution after inspecting real code; surface material conflicts in the plan.

## 4. Roles and access boundaries

| Operation | SuperAdmin | Agent | Customer | Anonymous |
| --- | --- | --- | --- | --- |
| List/search/view user accounts | Yes | No | No | No |
| Provision an Agent account | Yes | No | No | No |
| Activate/deactivate an account | Yes, with anti-lockout safeguards | No | No | No |
| View read-only roles/permissions matrix | Yes | No | No | No |
| View/search/filter/page administrative audit logs | Yes | No | No | No |
| Read/update organization/support settings | Yes | No | No | No |
| Change an existing account's role | **No** | No | No | No |
| Edit/delete administrative audit records | **No** | No | No | No |

- Every **new Administration API** enforces the existing backend `SuperAdmin` policy. Anonymous ⇒ 401, Agent/Customer ⇒ 403, following the API's current conventions. Angular route guards/nav hiding are additional UX only.
- Existing role-specific endpoints and row-level scopes remain unchanged: Agents do not become SuperAdmins; Customers cannot access staff records; admin account listing does not reveal passwords, hashes, refresh tokens, secret values or internal authentication material.
- An inactive account must be refused when trying to authenticate and must also fail authorization on subsequent requests using **previously minted access tokens**. Apply the check in the shared authenticated request pipeline so it is effective on existing Tickets, Portal, KB, SLA, AI, Reports and other protected endpoints, not only newly added Admin APIs.
- Choose appropriate 401/403 behavior aligned with existing authentication conventions; avoid disclosing sensitive details about the disabled account. Confirm the application does not accidentally let an inactive user's long-lived existing token bypass the check because a JWT is cryptographically valid.
- Avoid global behavior that blocks legitimate anonymous login/registration or necessary public health checks. No new token blacklist/distributed session store unless proven essential to the minimal requirement.
- Keep authorization tests and browser verification focused on effective backend checks, not only visual control hiding.

## 5. AD1 — Standard User Administration

### Required SuperAdmin UI and APIs

- **Accounts list:** paged, with search (e.g. name/email), role and active-status indicators; safe, consistent ordering. Show existing Agents, Customers and SuperAdmins as accounts; do not conflate an Identity account with a CRM Customer profile.
- **Account detail:** account ID, display name, email, fixed role and active/inactive status, plus other already-safe fields only if useful. No password/hash/token display.
- **Create Agent:** SuperAdmin enters minimal identity details and uses a secure account-provisioning approach compatible with current ASP.NET Identity. Role is **always** `Agent` on this endpoint, chosen server-side; user-controlled request data cannot create a SuperAdmin or Customer instead. Respect existing email/username uniqueness, password policy and validation.
- **Secure initial access:** planner inspects the existing account-creation/password flow and specifies the **smallest safe approach** (for example, a one-time initial secret if an invitation mechanism does not exist). Never invent outbound invitation email/SMS, hardcode shared credentials, expose hashes, record credentials in audit/HTTP logs, or save a clear-text password in new CRM tables. If provisioning requires a material new password/recovery workflow, surface it explicitly rather than silently building unrelated infrastructure.
- **Deactivate/reactivate:** authorized SuperAdmin can change an account's active status with clear confirmation UI, understandable success/error handling and no deletion of its historical records. Reactivation restores normal eligibility to sign in; it does not automatically recreate/restore expired tokens.
- **Already-assigned tickets:** disabling an Agent must preserve ticket assignment/history; do not auto-reassign tickets or rewrite historic actors as a hidden side effect. Existing SuperAdmin reassignment remains the way to handle abandoned workloads. UI may warn about existing workload if easily derived from existing data, but is not required to implement new routing logic.
- **Prevent lockout:** as a recommended safety default, do not allow the current SuperAdmin to disable their own session/account or disable the last active SuperAdmin. Prefer simple explicit backend checks; no enterprise policy engine. If the repo architecture suggests another minimal robust anti-lockout pattern, document the choice in planning.
- **Idempotent no-ops:** if an account is already in the requested active state, return a sensible result without emitting a misleading `AccountActivated`/`AccountDeactivated` audit record. Concurrent account-status modifications should not lead to false audit entries or inconsistent state; leverage existing concurrency conventions rather than introducing distributed locks.
- **Role immutability:** no `ChangeRole`, promote/demote or bulk user-role editor. Existing SuperAdmin bootstrap or other approved provisioning is preserved; creating an Agent must not accidentally grant extra roles.

### Effective JWT revocation condition

- The new account-status enforcement must apply to **all protected backend requests** under the existing JWT authentication scheme, including tokens issued **before** deactivation. Inspect whether existing `IsActive` checks occur only during login, whether JWT carries a stale status claim, and whether the JWT pipeline supports a small fresh account-status lookup. Do not rely on the SPA reading an `isActive` flag.
- Verify: login as Agent; retain a token; SuperAdmin disables the Agent; use retained token directly against an already-existing protected Agent endpoint ⇒ denied; attempting new login ⇒ denied; SuperAdmin reactivates; fresh login works. Include analogous Customer role coverage because AD1 permits account deactivation generally.
- Any performance/caching choice must not create a window where deactivated users keep authorized access contrary to AD1. Simplicity and correctness outweigh speculative optimization in this project.

## 6. AD2 — Read-only Roles & Permissions matrix

- Provide a **SuperAdmin-only** Administration page/section showing the three fixed roles as columns and human-readable abilities as rows, grouped by CRM module. At minimum cover authentication/account access; Customer Management; Ticket creation/tracking/edit/workflow; Agent Dashboard; SLA/Automation; Knowledge Base drafting/publication; staff AI; Customer Portal; Reports/Management; and Security/Administration.
- Correctly communicate **record-level scope**, not simply `Allowed = true`: Agent staff-ticket access is restricted by **current assignment** (collaboration alone is insufficient); Customer Portal ticket access is restricted by the **linked CRM Customer profile**; Staff Knowledge Base visibility and `Published + Public` Help Center are different; only SuperAdmin publishes KB and accesses Reports/Admin.
- Treat matrix content as a maintained presentation of the existing policies and route-level permissions, not a new runtime permission store. Verify each important matrix claim against actual controllers, authorization policies and established app use cases at planning time. If a feature has exceptions, label them instead of silently marking every cell Yes.
- No edit/toggle/save operation, authorization-policy migration or database permission table. No change in actual authorization should be caused by visiting the matrix.
- Keep responsive, accessible and readable at narrow widths (horizontal scroll/table layout is acceptable). Optional module descriptions can explain why permissions differ.

## 7. AD3 — Administrative audit logs

### Events to capture

1. Successful **Agent account creation** (`AgentCreated`, or consistent existing naming).
2. Successful **account deactivation** (`AccountDeactivated`).
3. Successful **account reactivation** (`AccountReactivated`).
4. Actual **organization-setting changes** (`OrganizationSettingsUpdated`), recording which informational fields changed.

- Other existing modules' ordinary ticket activities, KB publishing operations, SLA edits, AI usage, account logins and failed logins are **not** retroactively covered. Do not present the new audit page as a complete historic audit of all prior CRM actions. Audit begins when these ADMIN-001 operations are implemented; no fictitious backfill of administrative changes.
- Each event needs a durable stable record with **actor Identity User ID**, event/action type, affected target kind and stable target ID, UTC timestamp, and minimal safe action details. Optional labels may be resolved for display, but retain IDs even if user names change. Avoid recording whole entity snapshots or unnecessary personal/customer data.
- On organization settings changes, record **which keys changed and the previous/new safe informational values** (or a minimal redacted/safe diff). Never include secrets. If values include incidental sensitive free text, the planner should choose bounded/sanitized audit details without introducing a generic PII detection engine.
- Append-only through application operations. **No public/admin edit/delete audit API**. The existing database administration tools are outside the CRM permission model; do not claim the logs are physically immutable against DB operators.
- **Audit integrity:** a successful administrative mutation must never silently succeed without its required audit entry. Prefer writing both on the **same EF DbContext/transaction/SaveChanges** if current Identity and application data share storage; when ASP.NET Identity uses `UserManager` saves, inspect whether an existing transaction can enclose both writes. Do not write the audit record in Angular or use an unawaited fire-and-forget task. If true atomicity is not achievable without material redesign, the planner must explain the limitation and a safe minimal alternative before implementation.
- No log for validation failures, forbidden operations or unchanged settings/account state falsely labeled as a successful mutation. Any safety-critical failure should return an appropriate API error, not forge a successful audit entry.

### Read-only Audit Logs UI

- SuperAdmin-only list, newest first with deterministic tie-breaker; bounded paging; useful filters for event/action type and date range, plus simple search by actor/target ID or safely indexed label if available. Follow current CRM paging/date validation conventions; SQL filters before pagination.
- Display timestamp, actor, action, target and safe details. Loading/empty/error/retry states and clear messaging for older periods with no ADMIN-001 events.
- Do not expose event payloads with credentials, user password/reset tokens, JWTs, API keys, database details, or sensitive authentication internals in API response or page.
- No log deletion UI, retention/archival service, advanced full-text index, export feature or external SIEM integration in this story.

## 8. AD4 — Informational organization settings

Provide exactly these user-editable fields, stored durably in SQL Server:

| Field | Intended behavior | Suggested validation default (planner to align with existing patterns) |
| --- | --- | --- |
| **Organization display name** | Friendly organization name shown on Administration settings; may be reused by later Platform branding work | Required non-whitespace, bounded (e.g. ≤ 150 characters) |
| **Support contact email** | Informational support contact address only | Optional or required as UX convention dictates; when supplied, validate email and bound length |
| **Support contact phone** | Informational contact number only, **not** an SMS destination configured here | Optional, bounded (e.g. ≤ 40 characters), not overly restrictive for international numbers |
| **Support information** | Optional brief explanatory text customers/support staff may eventually use | Optional, bounded (e.g. ≤ 1,000 characters), safe text rendering |

- SuperAdmin can read the current saved values, edit, save, reload and see accurate validation errors. An initial-settings/default/empty-state strategy is needed for newly migrated databases; avoid inventing organization details from synthetic demo data as universal defaults.
- One authoritative settings record or equivalently simple persistence design; avoid duplicate conflicting sources or multi-tenant configuration infrastructure. Keep informational values separate from backend secrets and operational config (`appsettings`, environment/user secrets, SLA and KB configuration).
- Ensure settings are persisted and available on subsequent requests without redeployment, app restart or cache confusion. Use straightforward optimistic concurrency/version checks where existing conventions support them, avoiding lost updates and duplicate audit entries from simultaneous saves.
- When values change, record a single safe administrative audit event as part of the same successful database change; when there is **no change**, do not write a misleading audit event. The audit must not capture secret configuration.
- Any additional exposure of these values in the Customer Portal, public pages, navbar/branding or outbound communications is **not required** in Story 14; those integrations can be considered in later Platform/Communication work. Do not silently implement them as scope expansion.

## 9. API and data architecture expectations

- Follow existing `/api/v1`, Identity, Mediator/CQRS, FluentValidation, `ProblemDetails`, EF Core and DI patterns. Illustrative routes (the planner checks existing collision/naming conventions):
  - `GET /api/v1/admin/users` and `GET /api/v1/admin/users/{id}`;
  - `POST /api/v1/admin/agents`;
  - scoped `PUT` or `PATCH /api/v1/admin/users/{id}/status`;
  - `GET /api/v1/admin/permissions` (or a maintained matrix in SuperAdmin UI with no arbitrary permission-evaluation endpoint);
  - `GET /api/v1/admin/audit-logs`;
  - `GET` / `PUT /api/v1/admin/organization-settings`.
- Each new admin endpoint must independently enforce **SuperAdmin**, validate user input, avoid unbounded user/audit listing and avoid exposing secrets. No arbitrary caller-supplied admin actor or role; derive actor from validated JWT server-side.
- Implement the minimum **additive schema** for audit entries and persisted organization settings; reuse existing Identity user active status if present. Do not modify or drop existing Account/Ticket/Feedback/KB/SLA data, reset LocalDB, or alter migrations already applied. Inspect migration before running `dotnet ef database update`.
- Domain and Application should remain independent of EF/ASP.NET Core details; infrastructure can handle Identity and persistence. Use clear, minimal interfaces instead of copying authorization code into each handler.
- **Transactional coupling** is a key risk: account creation/deactivation is often committed by `UserManager`, while the audit record may be saved through EF Core. Inspect actual DbContext and transaction scope and choose the least complex reliable approach. Do not double-save audit entries or have two independent writes falsely described as atomic.
- No external services, message broker, event sourcing, background auditing worker, token blacklist cache, user-role administration engine, configurable permission model or customer communication provider.

## 10. Angular UX and navigation

- Provide a SuperAdmin-only **Administration** route group and navigation, using the existing shell and responsive Angular Material patterns. Suggested sections: **Users**, **Roles & Permissions**, **Audit Logs**, **Organization Settings**.
- **Users:** search, paging, roles/status indicators, safe account detail, Agent creation form and an explicit confirmation before deactivate/reactivate. Reflect success/errors accurately and refresh account state. Show a message when deactivation is disallowed for a protected SuperAdmin account.
- **Permissions:** read-only informative role matrix with accurate scope explanations. No edit or Save buttons.
- **Audit Logs:** filter by action/date, search and paging, readable safe action details; loading/empty/error/retry states; timestamps labeled consistently with app conventions.
- **Organization Settings:** reactive or equivalent validated form, save and reload, distinguish unchanged vs saved; read-only/saving state handling and visible backend validation messages.
- Non-SuperAdmin roles must neither see Administration navigation nor open the protected routes; most importantly the backend must still deny direct requests. Do not redesign existing layouts or introduce a second Angular application.
- Responsive at approximately **400px** and keyboard-usable forms. Show user-controlled text with escaped/interpolated rendering, not unsafe `[innerHTML]`. Preserve logout, token expiry, active-session handling and existing module navigation.

## 11. Manual acceptance and testing policy

- **Exactly one new backend happy-path unit test and one new frontend happy-path unit test**, using existing xUnit and Vitest patterns. Planner chooses high-value seams, e.g. a successful audited account-status change or Agent creation at the Application layer, and an Administration frontend formatter/view helper. Avoid adding additional automated tests or test frameworks; manual API, SQL and browser tests are needed for cross-cutting JWT authorization and transaction behaviors.
- Story 13 baseline was reported as **13 backend and 13 frontend passing tests**; the expected total after this story is **14 backend and 14 frontend** if the baseline is confirmed unchanged. Run both existing test suites and both builds. Record precisely what was run.
- Check any new additive migration(s), generated SQL, `has-pending-model-changes` and affected row counts before/after, without wiping test data. No hardcoded secrets or checked-in provisioning credentials.
- Manual API/SQL/browser acceptance matrix (may be reorganized during planning, but **do not drop** key coverage):

1. **Baseline:** Story 13 committed; no unrelated application changes; builds/tests at expected baseline or deviations documented.
2. **SuperAdmin-only Admin routes:** SuperAdmin succeeds; Agent/Customer ⇒ 403, anonymous ⇒ 401 for every new Admin API. Agent/Customer cannot open Administration UI or see its navigation.
3. **User list/details:** safe names/email/role/status; search, filter/paging and account-detail reads match Identity SQL data. No passwords, hashes, security stamps, tokens or secret claims in responses.
4. **Agent creation:** creates one genuine Identity user with exactly Agent role and active state using a non-hardcoded secure onboarding path. Reject duplicate email, invalid identity fields and weak passwords as applicable. The new Agent can use the existing login and staff UI.
5. **Role immutability:** no Admin endpoint allows changing Agent to SuperAdmin or Customer to Agent. Forged `role`, `isAdmin`, etc. cannot grant extra authority. Customer registration and bootstrap behavior remain intact.
6. **Live-token deactivation:** sign in as Agent, retain a real JWT, deactivate from SuperAdmin, then call an **existing protected staff endpoint** with the retained token ⇒ denied. New login also denied. Reactivate, obtain fresh JWT, and verify access restored. Repeat/spot-check with a Customer's preexisting JWT and Customer Portal endpoint.
7. **Protection against admin lockout:** self-deactivation and disabling the last active SuperAdmin cannot bypass backend safeguards; ordinary permitted account toggles still work. Verify error reporting and no stray audit on rejected actions.
8. **Deactivation history:** old ticket assignments, activity actors, Agent performance history, Customer profile linkage and other historical references survive deactivation. No silent reassignment or deletion; SuperAdmin can still reassign through existing Ticket Management.
9. **No-op/concurrency:** repeated activation/deactivation to an already requested state returns a consistent no-op or clear conflict, without false duplicate audit. Concurrent status edits do not produce contradictory audit rows.
10. **Permissions matrix:** displays all three fixed roles and accurate module/action restrictions, especially current Agent assignment, KB publication, Customer Portal ownership, Reports and Admin. No editable toggles and no new permissions backdoor.
11. **Audit on successful operations:** one corresponding append-only audit entry for each successful Agent creation, actual account toggle and settings update. Correct persisted actor ID, action, target, timestamp and safe details.
12. **Audit integrity/failure:** no successful business change without its audit record in the designed transaction boundary; failed/forbidden changes and no-ops leave no false success entry. Simulate a safe DB error or inspect transaction behavior without corrupting data. No edit/delete audit API.
13. **Audit search:** SuperAdmin can view newest-first paged events, search/filter action and date ranges, verify stable ordering, paging and empty/validation/error cases. Agent/Customer cannot fetch logs.
14. **Organization settings:** initial/default state, edit all four fields, save, refresh, restart the API if convenient and confirm durable DB values. Invalid email/overlong/blank required fields return appropriate 400 with friendly Angular errors. No secret/operational settings exposed.
15. **Settings audit:** an actual setting change generates one safe audit diff; saving identical values generates no change event. Values remain informational; SLA/ticket creation logic and Gemini config are unaffected.
16. **Existing-auth regression:** login/registration, SuperAdmin bootstrap, JWT expiry, Customer↔CRM profile linkage, Agent current-assignment ticket access, Customer Portal ownership, and staff role policies still behave correctly for active accounts.
17. **Existing-module smoke:** ticket create/workflow, Agent dashboard, SLA automation, KB publication, staff AI (explicit user-initiated), Customer Portal/CSAT and management Reports still function. Admin navigation creates no Gemini calls or outbound messages.
18. **Browser UX:** four Admin sections, create/toggle confirmations, search/date filters, loading/empty/error/Retry, sign-out and account switching, at ~400px and normal desktop widths; existing nav unaffected.
19. **SQL migration and privacy:** inspect actual migration, schema and account/audit/settings data, no unexpected data loss. Audit/API/HTTP output contains no credentials, passwords, password hashes, token values, Gemini key or DB secrets.
20. **Final check:** both builds and unit suites pass with exactly one added test each; docs match behavior; actual browser/API/SQL results distinguished from Unverified; no unrelated changes; **do not commit or push until independent acceptance**.

## 12. Explicitly out of scope

- Changing roles after creation, dynamic permission editing, per-user custom permissions, roles/claims builder, tenant-specific authorization or enterprise identity providers.
- MFA/SSO/LDAP/OAuth federations, password-reset-by-email workflows, outbound invitations or any newly built email/SMS delivery.
- Deleting user accounts or rewriting/deleting historical tickets/activities for disabled accounts; silent bulk reassignment or automatic cleanup of their workload.
- Blanket audit of ticket lifecycle, KB publishing, SLA policy edits, login events, failed logins or past system history; generic event bus, tamper-proof external append-only storage, audit archiving/retention/exports/SIEM.
- AI key/model/JWT/DB secret configuration, SLA policy/automation configuration, Knowledge Base settings, ticket priority/category defaults or any operational preference beyond AD4's four informational fields.
- New communication channels, ticket conversations, support mailbox integration, sent email/SMS/WhatsApp, customer chatbot; **Communication Channels LAST**.
- Platform localization/branding/multi-branch/multi-department or external ERP/API integrations (later modules).
- Additional automated unit, HTTP integration, E2E, security-scan or load-test suites beyond the single backend + frontend happy-path tests and existing suites.

## 13. Planning handoff and delivery gates

- **Story:** `ADMIN-001`, Story **14**; feature slug `security-administration`.
- **Prerequisite:** committed `REPORT-001`, plus existing `AUTH-001`, `CUST-001`, Tickets/SLA/KB/AI/Portal/Reports. Verify actual git history and repo state.
- **Intake location:** `.squad/stories/security-administration/ADMIN-001/intake.md`.
- **Suggested plan:** `.squad/plans/security-administration/14-story-security-administration.md`; update `.squad/plans/security-administration/00-overview.md` and `.squad/plans/00-index.md`.
- **Recommended planning sequence:** verify baseline → inspect account active/JWT validation across existing APIs → examine Identity provisioning, bootstrap, DbContext/UoW transactions → design secure Agent creation/deactivation with lockout protection → define audit entity and atomic writes → define organization settings and audit diff → create accurate read-only permissions matrix → minimal SuperAdmin APIs and Angular pages → additive migration(s)/docs → 1 backend + 1 frontend new tests → manual authorization, SQL and browser verification.
- **Stop-and-report triggers:** an unresolved inability to block **already-issued JWTs** on protected endpoints; transactional behavior that could write a successful account change without required audit; any conflict requiring changing existing roles/Agent attribution or new email delivery; migration drift that changes existing data/schema unexpectedly. Raise the conflict during planning rather than silently weakening AD1/AD3.
- **Planning only:** create Squad-Kit plan/index files, no application code, migrations, commits or pushes. Independent review is required before implementation authorization.
- **After implementation:** build backend/frontend, run existing tests plus exactly one new xUnit and one new Vitest happy-path test; perform available API/SQL/security/browser checks; record Unverified separately; seek independent acceptance before the user commits.

## Source and approval note

- Original source: *Customer Support CRM — Core Features* (two pages), Section **10 — Security & Administration**: **Users and roles; Permissions; Audit logs; System configuration**. That source specifies feature categories but **does not prescribe** role immutability, account-deactivation enforcement, audit events, UI, settings fields or storage designs.
- Explicit user approvals: **AD1 Option A**, **AD2 Option A**, **AD3 Option A**, **AD4 Option A**. Preserve them exactly.
- Engineering defaults/safeguards (e.g. SuperAdmin anti-lockout, proposed audit fields, secure initial credential delivery, API names, indexing, validation lengths, and transaction approach) are planning recommendations, **not separately approved user decisions**. Inspect actual checked-out repository, identify genuine constraints, keep designs small, and flag material conflicts before implementation.
- Current repository code was not supplied for direct inspection when this intake was drafted. Do **not** treat suggested C# types/method signatures, exact Identity behavior or migration names as verified facts until the Squad-Kit planner checks the local working tree.
