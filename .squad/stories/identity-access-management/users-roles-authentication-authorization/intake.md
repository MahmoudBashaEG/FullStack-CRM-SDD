# Identity & Access Management — Story Intake

## Feature

- **Feature name (display):** Users, Roles, Authentication & Authorization
- **Feature slug (folder under `plans/`):** `identity-access-management`

## Tracker

- **Tracker type:** `none`
- **Work item id:** `AUTH-001`
- **Work item type:** `Security / Business Foundation Story`
- **Status:** `Draft`
- **Labels:** `identity, users, roles, authentication, authorization, jwt, refresh-tokens, aspnet-core-identity, angular, ef-core, customer-signup, agent-management, happy-path-unit-tests`

## Title

```
Users, Roles, Authentication & Authorization
```

---

## Description

```
Goal
Implement the identity and access-control foundation for the existing Customer Support CRM application built in ARCH-001. Support three predefined roles (SuperAdmin, Agent, Customer), SuperAdmin-controlled agent accounts, public Customer registration, login/logout, short-lived JWT access tokens with rotating refresh-token sessions, role-based API authorization, and role-aware Angular navigation. Do not implement unrelated CRM business capabilities.

1. Existing architecture and story boundaries
- Extend the ALREADY IMPLEMENTED ARCH-001 monorepo, backend, frontend, and database. Do not scaffold a replacement application or restructure the Clean Architecture layers.
- Reuse the existing ASP.NET Core API, Clean Architecture dependency rules, CQRS/Mediator pipeline, EF Core SQL Server DbContext/migrations, reusable transactional Unit of Work, route/DTO/ProblemDetails conventions, Angular core/shared/features layout, Angular Material shell, and central API URL configuration.
- All new business API routes follow /api/v1/{resource}; health stays at /api/health. Request/Response DTOs use PascalCase names, JSON camelCase, direct success payloads, and standard HTTP codes/ProblemDetails errors.
- Keep identity/infrastructure implementation out of the Domain and Application dependency boundaries. Choose appropriate Application abstractions and Infrastructure implementations rather than making handlers depend directly on EF Core/ASP.NET Core Identity framework types.

2. Roles and permissions
- Exactly three built-in roles: SuperAdmin, Agent, Customer. Seed roles idempotently; do not allow public creation of roles or arbitrary role selection.
- SuperAdmin: log in; view/create/manage Agent accounts (at minimum view/list, create, update basic details, enable/disable, and set/reset agent passwords); view own identity; change own password. SuperAdmin is the only role allowed to create Agent accounts in this initial story.
- Agent: log in; view own identity; update allowed own contact/profile details; change own password. No agent creation, role changes, platform administration, or access to other users' private data. Ticket and customer-management permissions will be defined in their future feature intakes.
- Customer: publicly self-register, log in, view/update own basic profile, change own password. No access to Agent/SuperAdmin management or other customers' data. Later Customer Portal and CRM interaction history belong to separate stories.
- SuperAdmin accounts must never be creatable through public registration or the Agent-management API. Role membership is server-controlled; clients cannot supply or override assigned roles.
- Enforce roles and resource ownership in ASP.NET Core authorization policies/handlers on the server; Angular route guards/navigation are usability only and never the security boundary.
- Require authentication by default for application business endpoints; explicitly allow anonymous access only to signup, login, and other documented public endpoints such as the existing health endpoint.

3. Customer registration and basic user data
- Public Customer signup collects first name, last name, email, password, and optional phone number. Do not collect unnecessary personal details by default. Allow an optional company/organization name only if it fits the existing customer-domain boundaries.
- Assign Customer role automatically on signup. No role selector and no way for a request body to create Agent/SuperAdmin accounts.
- Emails uniquely identify accounts (case-insensitive normalization); users authenticate with email and password.
- Model identity separately from future CRM business/customer records. Preserve a stable user identifier and an extensible association for later Customer Management stories; avoid prematurely implementing tickets, interactions, notes, or attachments.
- SuperAdmin-created Agents have first name, last name, unique email, optional phone, an initial password set through the admin UI/API, and an enabled/disabled state. Agent role is assigned by the server.
- Only an authenticated user can read/update their own account profile; privileged agent-management endpoints require SuperAdmin.

4. Password and account security
- Password minimum length: 8 characters for all password creation/change/reset flows. Do not impose extra complexity requirements (uppercase, punctuation, digits) unless separately agreed; allow longer passphrases.
- Use ASP.NET Core Identity for secure user/role management and built-in password hashing/verification, unique users, security-stamp/account controls, and safe password operations. Never store plaintext/reversibly encrypted passwords or return passwords/password hashes through APIs.
- Initial passwords assigned by SuperAdmin (including Agent password resets) can be used for normal sign-in without a forced first-login password change. Users may voluntarily change their own passwords later.
- Provide authenticated self-service change-password for all roles and SuperAdmin-initiated reset/set-password for Agents; do not expose existing passwords, including to SuperAdmin.
- Disabled Agent accounts cannot obtain new login sessions and must not continue using existing access tokens to reach protected endpoints. Design server-side active-state or token-version/security-stamp validation as needed.
- Add a basic lockout / rate-limit strategy for repeated login attempts. Do not expose whether an account exists through differing login error messages; never log credentials or tokens.
- Email confirmation and email-based forgot-password are deferred because no outbound email provider is yet configured; document that public signup currently does not verify email ownership.

5. JWT authentication, refresh tokens, and browser sessions
- Authenticate API requests using signed, short-lived JWT access tokens. Validate signature, issuer, audience, expiry, and identity/role claims. Token lifetimes and signing settings must be configurable; JWT payloads must not contain passwords, sensitive profile data, or secrets.
- Issue a cryptographically random refresh token on successful login, with its own configurable expiration. Persist only a non-reversible hash of the refresh token and required session metadata (user, issuance, expiration, revocation/replacement state) in SQL Server; never store a plaintext refresh token in the database or logs.
- Provide an authenticated-session renewal endpoint (for example POST /api/v1/auth/refresh) that validates the refresh token and active user account, issues a new access token, and rotates the refresh token. Make refresh tokens single-use: revoke the previous token on successful rotation and reject reused, expired or revoked tokens; specify refresh-token family invalidation on detected reuse. Implement rotation/revocation updates atomically using the existing transactional EF Core approach.
- Deliver refresh tokens to Angular via Secure, HttpOnly cookies with appropriate SameSite and Path attributes; keep access tokens in application memory rather than localStorage/sessionStorage. Configure local HTTPS/development proxy behavior so cookies function correctly without weakening production security. Protect cookie-authenticated session-changing endpoints (including refresh/logout) from CSRF.
- The Angular app must have login, Customer signup, logout, current-user/session loading, protected routes, and role-aware navigation. On a normal page reload, it should silently regain a valid access token using the refresh cookie, as long as the refresh session remains valid; when both tokens are unusable, return to the sign-in flow. SuperAdmin has an Agent-management view; Customer and Agent receive minimal signed-in landing/profile views.
- Provide refresh and logout/revocation API flows. Logout must revoke the current refresh session and clear its cookie. Password change/reset and Agent disable must invalidate affected refresh sessions and deny further protected API access, including with previously issued JWTs, using a server-side account/security-state check; do not assume JWT expiry alone provides immediate revocation.
- Avoid unnecessary token/session management screens and advanced device management in this story. Reuse the central Angular API client/proxy; keep JWT signing keys, bootstrap credentials, and other secrets outside source control using user secrets/environment settings.

6. Initial SuperAdmin bootstrap (migration for schema; secure, idempotent account provisioning)
- EF Core migration: add a NEW migration that creates the ASP.NET Core Identity user, role and supporting tables in the existing SQL Server database. The migration must NOT seed the SuperAdmin password or a hardcoded password hash, and must not drop/recreate existing data.
- Automatic bootstrap: after the Identity migration has been applied, provision exactly the three predefined roles (SuperAdmin, Agent, Customer) and create the initial SuperAdmin account with email admin@admin.com only if it does not already exist. Bootstrap must run automatically on a newly initialized environment and be idempotent. Do not silently promote an unrelated existing account with this email; report a configuration issue instead.
- Password storage: create the administrator through ASP.NET Core Identity's UserManager/password APIs, which use the framework's PasswordHasher to generate and persist a salted, one-way password hash. Never store plaintext, reversible encryption, or a hardcoded/precomputed password hash in the database, migration, source code, logs, or API responses.
- Password configuration: supply the initial password at setup through .NET User Secrets (configuration key Bootstrap:SuperAdmin:Password) or a secure environment/configuration secret outside source control. Never embed the password value in the intake, migration, repository, or documentation. Document setup with a placeholder, not a real password.
- Subsequent startup: when the SuperAdmin account already exists, do not recreate it, create duplicates, or overwrite/reset its password. Subsequent startup must not require the initial password again merely to use the existing account; missing bootstrap credentials must be actionable on first provisioning.
- The seeded SuperAdmin can sign in normally using the configured initial password without a mandatory first-login password change. Voluntary change-password remains available.
- Apply migrations to existing databases without data loss. Do not use EnsureCreated or drop/recreate the database to initialize Identity.

7. Angular UI and user workflows
- Extend the already approved Angular Material theme, shell, shared UI components, routing and responsive layout; the AI may design the actual forms/agent list based on these requirements and available data. No Figma or mockups are supplied.
- Public: Sign In and Customer Sign Up forms with client-side validation and clear, non-revealing errors.
- Authenticated: profile/current-user page, change password, logout, role-specific shell/navigation.
- SuperAdmin: Agent list, Agent create form, Agent details/edit, enable/disable, and set/reset password. Never display password values except the one being entered, and never reveal stored hashes.
- Agent and Customer: own profile and a simple authenticated landing page only. Do not build Tickets, Customer Portal, reports, dashboard metrics, or other unimplemented CRM features.
- Prefer direction-neutral layout/CSS so that later Arabic/RTL can be introduced without redesign; actual localization remains a later story.

8. Database, API, and verification
- Add EF Core identity/user/role and minimum profile/account-state persistence to the EXISTING SQL Server database using a new migration. Reuse existing transactional boundaries for workflows that change several related records.
- Implement authentication, self-profile, and SuperAdmin Agent-management API endpoints under the established /api/v1/ prefix, with documented role requirements in OpenAPI.
- Use server-side ownership checks, typed API contracts, validation and existing ProblemDetails/ValidationProblemDetails conventions. Do not expose Identity/EF persistence entities directly in API responses.
- Add only basic, happy-path unit tests: one meaningful backend unit test and one meaningful Angular unit test for the new behavior. No negative, failure, security-abuse, SQL/API integration, E2E, browser automation, performance or heavy test suites.
- Required manual verification is limited to successful Customer signup/login, seeded SuperAdmin login without mandatory password change, SuperAdmin creates Agent and Agent logs in, successful refresh-token rotation/session renewal (including after browser reload), normal logout, authorized landing page, builds, unit tests, and applying the migration without recreating the database. Do not add negative-path manual or automated test suites.
- Document role matrix, seed/bootstrap instructions (without password), API routes, session/expiry behavior, local run/setup steps, and decisions deferred to later feature intakes.
```

---

## Acceptance criteria

```
- [ ] AUTH-001 extends the existing ARCH-001 monorepo and honors its backend Clean Architecture, CQRS/Mediator, transactional persistence, API conventions, and Angular feature-based architecture; no new application scaffold or unrelated CRM features.
- [ ] Exactly three predefined roles (SuperAdmin, Agent, Customer) exist and are provisioned idempotently.
- [ ] Public Customer signup records first name, last name, unique email, optional phone, and a securely hashed password, and assigns Customer role server-side. Public signup cannot select elevated roles.
- [ ] A Customer can sign in and view/update only their own basic profile; Agent can sign in and view/update only their own basic profile.
- [ ] SuperAdmin can sign in and list, create, edit, enable/disable, and reset/set initial passwords for Agent accounts; only SuperAdmin can manage Agents.
- [ ] Backend enforces authentication and role/resource-ownership authorization independently of Angular route guards. Existing GET /api/health remains anonymous.
- [ ] All password creation/change/reset paths enforce minimum length of 8, with secure hashing. Neither seeded SuperAdmin nor newly provisioned/reset Agent accounts are required to change their password on first login.
- [ ] New EF Core migration adds identity/persistence schema to the existing database without dropping or recreating it.
- [ ] A new EF Core migration creates the ASP.NET Core Identity schema without embedding or seeding administrator credentials; a separate automatic, post-migration, idempotent bootstrap creates the three roles and admin@admin.com only when absent, using an initial password supplied through .NET User Secrets or secure environment configuration and salted one-way hashing through ASP.NET Core Identity UserManager/PasswordHasher. The database stores only the password hash, and subsequent startup neither recreates the account nor overwrites/resets its password.
- [ ] Signed, short-lived JWT access tokens validate signature, issuer, audience, and expiry and contain safe identity/role claims; token and signing secrets never appear in source or logs.
- [ ] Successful login issues an expiring refresh token, stored only as a hash in SQL Server and delivered to Angular in a Secure HttpOnly cookie. Refresh API issues a new JWT and rotates the single-use refresh token atomically; reused/expired/revoked tokens are not accepted and reuse invalidates the relevant token family.
- [ ] Angular refreshes an authenticated session after normal page reload without persisting JWTs/refresh tokens to localStorage/sessionStorage. CSRF protections cover cookie-based refresh/logout, and local-development cookie/HTTPS configuration is documented.
- [ ] Logout revokes the current refresh session and clears its cookie. Account disable and password change/reset revoke affected renewable sessions and invalidate protected access according to server-side account/security-state checks, with the remaining JWT tradeoffs documented.
- [ ] Angular has functioning sign-in/signup, profile/change-password and role-appropriate navigation, plus SuperAdmin Agent management, using the existing shared UI theme and API client.
- [ ] All APIs follow /api/v1/ route, ...Request/...Response DTO, camelCase JSON, success HTTP status and ProblemDetails conventions, and document required roles.
- [ ] Backend build and Angular build pass; one happy-path backend and one happy-path frontend unit test for this story pass; the database migration and key successful manual flows are verified. No extra automated test types are introduced.
- [ ] Existing ARCH-001 files/behavior remain intact except for deliberate security integration changes; unrelated CRM features, email systems, localization, multi-department, and multi-branch behavior are not implemented.
```

---

## Attachments

| File (relative to this folder) | What it is |
| --- | --- |
| `attachments/azm_squad_customer_support_crm 1(2).pdf` | Original CRM feature list; Security & Administration and Customer Management provide business context, not permission to implement unrelated feature stories. |

---

## Dependencies

- **Blocked by / related ids:** `ARCH-001` (implemented, verified, and accepted before AUTH-001 planning/implementation).
- **Depends on code areas or other stories:** Existing `backend/src/Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`, `backend/tests/Crm.UnitTests`, `frontend/src/app/core`, `frontend/src/app/shared`, `frontend/src/app/features`, and the current `docs/api-conventions.md`, `docs/persistence-and-transactions.md`, `docs/frontend-ui-foundation.md`. The planner must inspect the real current code rather than relying only on the old ARCH-001 plan.

## Extra notes

- This intake intentionally contains requirements and acceptance criteria, not a step-by-step implementation plan; generate the plan separately with Squad-Kit after approval.
- **Assumption to confirm:** Agent accounts are created **only by SuperAdmin**. The phrase “agents can be created by users” is interpreted as authorized admin users, not arbitrary Customers/Agents or public self-registration. Do not permit Agent self-signup without an explicit new requirement.
- Initial SuperAdmin password was supplied in discussion. Treat it as a setup secret; do not copy it into generated files or commit it. Since it has been shared in chat, use a new strong password before any non-local deployment.
- Suggested minimum customer data for identity signup is limited to name, email, and optional phone; additional CRM customer fields, interactions and attachments belong to the future Customer Management intake.
- Deliberate simplified scope: no SMTP/email provider, self-service forgot-password/email verification, custom permission administration, multi-tenant/branch/department roles, SSO/OAuth federation, MFA, or new test frameworks. Security-critical controls (hashed passwords, authorization, account disable, safe JWT/session handling, basic brute-force protection) remain mandatory despite the happy-path-only automated testing policy.

## Technical hints

- Existing stack: .NET 10 ASP.NET Core REST API, source-generated Mediator, EF Core 10 SQL Server LocalDB, Angular standalone/Material, single monorepo. Use the versions/packages already implemented in ARCH-001 rather than respecifying the whole architecture.
- Prefer `Microsoft.AspNetCore.Identity` and EF Core stores for credentials, password hashes, unique user identifiers, roles and password/security-stamp operations; use `Microsoft.AspNetCore.Authentication.JwtBearer` or an appropriate compatible JWT validation path. Preserve Clean Architecture dependency direction.
- Avoid public endpoints that take a requested role; only authorization-checked application commands can create Agents or assign roles.
- Plan short-lived JWT access tokens plus rotating, single-use refresh tokens with SQL Server hashed-at-rest persistence, configurable lifetimes, secure HttpOnly cookie delivery, CSRF protection, and revocation on logout/password changes/Agent disable. Use the existing EF Core transaction boundary for token rotation. Keep access tokens in memory; avoid browser web storage for authentication credentials. Do not assume JWT expiry alone is immediate revocation.
- Password bootstrap: use the placeholder configuration key `Bootstrap:SuperAdmin:Password` via .NET User Secrets or an environment secret (never its actual value in files). After applying the Identity schema migration, use ASP.NET Core Identity UserManager.CreateAsync(user, password) and RoleManager for idempotent provisioning; rely on the built-in salted PasswordHasher and never seed a credential through migration HasData/raw SQL. Do not reset an existing administrator password on startup.
- Preserve transaction atomicity when creating the account and profile/assigning roles, while using the identity framework's supported persistence operations safely.
- Add only the minimal feature-specific happy-path unit tests; do not create new API/DB/E2E suites.

## Out of scope

- Any other CRM business feature: complete Customer Management (interactions, notes, attachments), tickets, communications, dashboards, SLA, knowledge base, portal, reports, AI, integrations.
- Customer record management by Agent/SuperAdmin beyond basic self-profile and account provisioning described here; specify business ownership rules in future intakes.
- Customer email ownership verification, outbound email/SMS providers and self-service email-based password recovery (deferred until an email integration story exists).
- User-customizable roles, new role names, configurable permissions matrix, custom policy administration, self-registration as Agent/SuperAdmin.
- Localization, Arabic translations, complete RTL, departments, branches, multi-tenancy, SSO, MFA, production deployment, Docker, CI/CD.
- Automated negative/edge/abuse-path tests, automated API/SQL integration tests, E2E/browser testing, snapshot/performance suites, and any unnecessary testing infrastructure.
