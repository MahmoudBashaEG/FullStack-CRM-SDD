# Customer Profiles & Contact Management — Story Intake

## Feature

- **Feature name (display):** Customer Management — Profiles & Contacts
- **Feature slug (folder under `plans/`):** `customer-management`

## Tracker

- **Tracker type:** `none`
- **Work item id:** `CUST-001`
- **Work item type:** `Business Feature Story`
- **Status:** `Draft`
- **Labels:** `customer-management, customer-profiles, contacts, aspnet-core, angular, cqrs, ef-core, identity-integration, role-authorization, happy-path-unit-tests, squad-kit`

## Title

Customer Profiles & Contact Management

## Description

### Goal

Implement the first Customer Management business capability in the **existing** Customer Support CRM application. Support both (a) customers who self-register through the AUTH-001 identity flow and can log in, and (b) customer contact records created by a SuperAdmin or Agent **without** creating a login account. Allow authorized users to maintain customer profile and contact details. This story is limited to profiles and contacts; other Customer Management capabilities belong to later stories.

### 1. Existing codebase and architectural constraints

- Extend the already implemented **ARCH-001** and **AUTH-001** application, database, and Angular frontend. Do not create a new solution, application, login system, or identity roles.
- Reuse the actual existing Clean Architecture layers (`Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`), CQRS/Mediator handlers, validation, EF Core SQL Server DbContext and migrations, Unit of Work/transaction policy, and `/api/v1/` API conventions.
- Reuse AUTH-001's ASP.NET Core Identity user IDs and the roles `SuperAdmin`, `Agent`, and `Customer`. Authentication, JWT access tokens, refresh-token rotation, session handling, and Agent administration remain owned by AUTH-001.
- Reuse Angular standalone feature-based organization, existing Material shell/design tokens, shared components, typed API access, auth/session state, and route protection.
- The planner **must inspect current implemented code** and existing architecture documents. Do not guess concrete AUTH-001 entity names, controller routes, folder names, or interfaces from this intake.

### 2. Customer records and relationship to login accounts

- Introduce a persistent **CRM customer profile/contact record** with its own stable Customer ID, separate from an ASP.NET Core Identity account.
- Support two customer types in one CRM customer list:
  - **Registered customer:** has an existing Identity user with the `Customer` role and an associated CRM customer profile. The account authenticates as implemented by AUTH-001.
  - **Contact-only customer:** created manually by SuperAdmin or Agent; has a CRM customer profile but **no Identity user**, password, role membership, JWT, or ability to sign in.
- The association between a CRM profile and an Identity user must be **optional** for contact-only records and **at most one-to-one** for registered customer accounts. Do not change or duplicate the existing Identity account model merely to support contacts.
- An authenticated Customer's self-registration must create (or ensure the existence of) a linked CRM customer profile. Integrate with the **existing AUTH-001 signup workflow** rather than creating a second public registration route. Ensure new account/profile creation is coordinated atomically where required by the existing persistence model.
- Existing Customer-role Identity users created *before* CUST-001 must gain their CRM profiles via a safe, idempotent backfill/initialization path; preserve existing identities, passwords, tokens, and login behavior. Do not create profiles for Agents or SuperAdmin.
- **Do not automatically link/merge** a contact-only record to a newly registered account merely because its contact email matches the account email: AUTH-001 does not verify email ownership. An explicit verified claim/linking/merge workflow is **out of scope**. Two distinct CRM records may temporarily have the same contact email; document the trade-off rather than risking unauthorized profile access.

### 3. Customer data and edit rules

- Minimal CRM customer fields:
  - **First name** — required.
  - **Last name** — required.
  - **Contact email** — required, validated as an email address.
  - **Phone number** — optional.
  - **Company / organization** — optional (CRM-specific; previously deferred from AUTH-001).
  - **Customer ID, created-at UTC, updated-at UTC**, and optional Identity-user association — system-managed; never editable by clients.
- Trim text input, validate required fields and reasonable lengths, and use consistent API validation/ProblemDetails errors. Do not add unnecessary demographic, financial, or sensitive personal fields.
- For a **linked** registered customer, preserve AUTH-001's restriction against changing the login email. Display the linked customer's email as read-only in customer-edit workflows. Coordinate permitted updates to names/phone with the existing AUTH-001 profile behavior so the Identity self-profile and CRM customer profile do not silently diverge. The planner should choose and document the authoritative fields/synchronization boundary after inspecting the codebase.
- For a **contact-only** customer, authorized staff can edit contact email, name, phone, and company. Changing a contact email must **never** create, change, claim, or reassign an Identity login.
- Preserve Customer ID across changes. Do not implement hard deletion or account deletion in this story; future tickets/communications will refer to customer records.

### 4. Roles, authorization and ownership

| Action | SuperAdmin | Agent | Customer |
| --- | --- | --- | --- |
| List/search CRM customers | Yes | Yes | No |
| View any CRM customer profile | Yes | Yes | No |
| Create contact-only CRM customer | Yes | Yes | No |
| Edit a contact-only CRM customer | Yes | Yes | No |
| Edit a registered customer's allowed CRM fields | Yes | Yes | No; only own |
| View own linked CRM customer profile | N/A (admin identity profile remains in AUTH-001) | N/A (agent identity profile remains in AUTH-001) | Yes |
| Edit own linked profile (allowed fields only) | N/A | N/A | Yes |
| Create/login/manage Customer Identity accounts | Existing AUTH-001 signup only | No | Existing public signup/self-service only |

- The backend must enforce role and *record ownership* for each endpoint. Angular menus/guards are UX only, never authorization enforcement.
- An authenticated Customer may access only their own linked CRM customer profile using **their validated Identity user ID**, not a client-supplied `userId` or arbitrary customer ID. Contact-only records are not exposed to public users.
- Staff permissions are intentionally simple and global for now. Multi-department/branch ownership, per-agent assignment of customer contacts, granular permission configuration, and customer impersonation belong to later intakes.

### 5. Backend use cases, APIs and data access

- Add CQRS commands/queries and REST endpoints for staff to **create**, **list/search**, **view details**, and **update** CRM customer records.
- Provide the signed-in Customer with **view/update own CRM profile** through authenticated, ownership-checked endpoints and/or by extending AUTH-001's current profile route in a backward-compatible way. Do not build competing profile systems.
- Use `/api/v1/customers` for staff customer-resource endpoints; conform to existing DTO `...Request`/`...Response`, camelCase JSON, direct success responses, correct HTTP status codes, OpenAPI, and centralized `ProblemDetails` conventions.
- Define the first minimal customer-list contract (ARCH-001 deferred list conventions until the first list feature): server-side **one-based** `page` (default 1) and `pageSize` (default 20, capped at 100), an optional single `search` term matching name/email/phone, and a stable deterministic default ordering (newest first with ID tie-breaker). Return a typed page result containing `items`, `page`, `pageSize`, and `totalCount` rather than a universal envelope around every endpoint.
- Use EF Core persistence in Infrastructure; queries should use efficient projection and no-tracking where appropriate. Create a new **additive migration** for CRM profiles and indexes; preserve all existing Identity and migration data. Do not use `EnsureCreated`, drop/recreate the database, or insert fake customers through migrations.
- Use the ARCH-001 transactional Unit of Work for writes. The signup/profile creation integration and any write affecting multiple records must be atomic; avoid partial Identity/profile state when an operation fails.

### 6. Angular UI/UX

- Add a lazy-loaded **Customer Management** Angular feature for authorized `SuperAdmin` and `Agent` users, integrated into the existing shell/navigation.
- Implement a **Customers list** with search, pagination, customer display name, contact email, phone/company summaries, and a visual indication of whether the record is a registered customer or contact-only.
- Implement **Create Customer** (contact-only, explicitly labeled **No login account created**), **Customer Details**, and **Edit Customer** workflows for staff. Do not ask staff to set a password when creating a contact-only record.
- For registered Customer users, extend/reuse the **existing own-profile** experience from AUTH-001 to display and allow editing appropriate CRM contact fields; do not expose staff list/create/edit screens to Customer users.
- Allow AI to choose sensible layouts/form ergonomics from current data while preserving the approved Angular Material theme, responsive design, shared components, and direction-neutral CSS conventions. No new Figma/mockups required.
- Use existing Angular auth session, guards, and centralized API URL handling; do not create duplicate authentication logic or store credentials in browser storage.

### 7. Verification, documentation, and future-story boundaries

- Add **one meaningful backend happy-path unit test** and **one meaningful frontend happy-path unit test** for CUST-001. Continue using existing test frameworks. No automated negative/edge/authorization-abuse/DB integration/API/E2E/performance test suites or additional test frameworks.
- Verify backend and frontend builds, successful application of the additive migration, and the key manual happy-path flows: staff create/edit/list/search contact-only customer; a new Customer signup produces a linked CRM profile; a pre-existing Customer account gets a profile; logged-in Customer views/edits their own permitted profile fields.
- Document the distinction between CRM contacts and Identity accounts, field ownership/sync, the chosen endpoints/list contract, duplicate-contact-email trade-off, and dependencies for future Customer Management and Ticket Management stories.
- Do **not** plan or implement customer notes, attachments, interaction/ticket history, ticket assignment, email invitations, verified email claiming, bulk import/export, advanced reporting, multi-department/branch ownership, or deleting customers here. Those need separate intakes.

## Acceptance criteria

- [ ] CUST-001 extends the existing ARCH-001 + AUTH-001 application/database and preserves current authentication, roles, transaction policy, API conventions, and Angular theme.
- [ ] A CRM customer record has a stable Customer ID, required first/last name and contact email, optional phone/company, UTC timestamps, and an optional Identity user association.
- [ ] SuperAdmin and Agent can create a **contact-only** Customer without an Identity account, password, token, or login privileges.
- [ ] SuperAdmin and Agent can list/search/paginate customer profiles, view a customer, and edit allowed contact data through protected APIs and Angular screens.
- [ ] Customer list provides one-based page/pageSize with documented defaults/cap, name/email/phone search, stable ordering, and typed paginated response.
- [ ] AUTH-001 self-registration creates a linked CRM customer profile using the existing Identity registration flow, with atomic persistence as needed; no second signup mechanism is introduced.
- [ ] Pre-existing AUTH-001 Customer accounts receive exactly one linked CRM profile through an idempotent data backfill; other roles do not receive customer profiles.
- [ ] Contact-only customers remain unlinked, and no association is inferred or performed solely from matching email addresses; any duplicate contact emails are documented rather than automatically merged.
- [ ] Only the authenticated Customer can view/edit their own linked CRM profile using server-validated Identity ownership; the Customer role cannot browse or modify other customer records.
- [ ] For linked accounts, login email remains uneditable under AUTH-001 rules; allowed contact changes stay consistent with AUTH-001's identity/profile data. Staff edits to contact-only email never affect login credentials.
- [ ] Existing Identity accounts, role assignments, passwords, tokens, refresh sessions and SuperAdmin bootstrap behavior continue to function without regression.
- [ ] New EF Core migration adds CRM customer persistence without dropping/recreating databases or existing user data; future stories can reference stable Customer IDs.
- [ ] All endpoints follow established `/api/v1/`, DTO, JSON, status code, validation, and `ProblemDetails` standards; ownership and role restrictions are enforced on the server.
- [ ] Angular Customer Management feature uses existing guards, navigation, responsive Material styling and typed API services; existing Customer self-profile is reused/extended rather than duplicated.
- [ ] Backend/frontend builds pass and one backend + one frontend happy-path CUST-001 unit test pass; only successful manual user flows/migration checks are required.
- [ ] Customer notes, attachments, interactions/ticket history, Customer Portal, verified linking/merging, and unrelated CRM modules remain out of scope.

## Attachments

| File (relative to this folder) | What it is |
| --- | --- |
| `attachments/azm_squad_customer_support_crm 1(2).pdf` | Original Customer Support CRM feature list. CUST-001 covers the profile/contact-details subset of Customer Management only. |

## Dependencies

- **Blocked by / related ids:** `ARCH-001` and `AUTH-001` (implemented and accepted before CUST-001 implementation); related future stories `CUST-002` (notes), `CUST-003` (attachments), `CUST-004` (interaction history) and Ticket Management — **not part of this story**.
- **Depends on code areas or other stories:** Existing `backend/src/Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`, `backend/tests/Crm.UnitTests`, `frontend/src/app/core`, `frontend/src/app/shared`, `frontend/src/app/features`, and `docs/api-conventions.md`, `docs/persistence-and-transactions.md`, `docs/frontend-ui-foundation.md`, `docs/identity-and-access.md`. Inspect the repository's **current** implementations (especially Customer signup, Identity data fields, EF Core DbContext, and profile endpoints), not just earlier plans.

## Extra notes

- This intake is a requirements and acceptance-criteria input for Squad-Kit planning, **not** an implementation prompt. No plan or code should be generated from this file until the separate planning step.
- Existing AUTH-001 uses email/password registration with no email-ownership verification. Therefore auto-linking a manually created contact to a newly registered Identity account by matching email would be unsafe; verified linking/merge is deliberately deferred.
- Treat Identity email as authentication data and CRM contact email as business data with carefully documented sync rules. The planner should inspect AUTH-001 to avoid duplicating first/last/phone editing behavior or introducing conflicting sources of truth.
- No UI mockups are supplied. Use the implemented Angular Material design system and real feature data to guide UI decisions.
- For a training CRM, prefer understandable, maintainable code over speculative abstractions or production-scale infrastructure.

## Technical hints

- Existing stack: .NET 10, ASP.NET Core REST API, Clean Architecture, source-generated Mediator/CQRS, EF Core 10 + SQL Server LocalDB, ASP.NET Core Identity/JWT/refresh sessions, Angular standalone + Material, single monorepo.
- Plan a nullable foreign-key association from CRM customer record to the existing Identity user key (with uniqueness for linked accounts). Do not require every customer contact to be a registered user.
- Prefer feature-focused application handlers, narrowly scoped persistence interfaces if useful, and efficient server-side paging. Keep EF/Identity implementations within Infrastructure and HTTP contracts within API.
- Handle initial backfill of Customer-role accounts and subsequent signup linkage idempotently. Coordinate Identity and CRM profile writes through the already established transactional boundaries where supported by the real persistence model.
- Maintain role checks for staff operations and Identity-subject ownership checks for customer self-profile operations. Never let an untrusted request supply a user association.
- Only simple happy-path unit tests for the feature; continue existing testing conventions.

## Out of scope

- Creating passwords, Identity users, JWTs, refresh tokens, role assignments, or invitations when staff create contact-only customers; changes to AUTH-001's fundamental login, role, or session model.
- Public signup redesign or a second Customer signup endpoint; arbitrary customer-account claiming or merging by matching email; email confirmation/invites.
- Customer notes, file attachments, interaction history, tickets, communications, SLAs, dashboards, reports, Knowledge Base, AI features, and Customer Portal capabilities.
- Customer deletion, bulk import/export, mass edits, advanced sorting/filtering, complex deduplication/merge, approval queues, and external CRM integrations.
- Multi-branch/multi-department permissions, customizable roles, localization/RTL implementation, production hosting or additional infrastructure.
- Automated negative-path, integration, SQL/API, browser/E2E, performance, or security-abuse test suites beyond the established happy-path unit testing policy.
