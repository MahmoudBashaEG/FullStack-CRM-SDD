# Ticket Creation & Management — Story Intake

## Feature

- **Feature name (display):** Ticket Management — Creation & Management
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker

- **Tracker type:** `none`
- **Work item id:** `TICK-001`
- **Work item type:** `Business Feature Story`
- **Status:** `Draft`
- **Assignee:** `Unassigned`
- **Labels:** `ticket-management, ticket-creation, ticket-list, customer-association, agent-assignment, authorization, aspnet-core, angular, cqrs, ef-core, happy-path-unit-tests, squad-kit`

## Title

Ticket Creation & Management

## Description

### Goal

Implement the first of **two** Ticket Management stories in the existing Customer Support CRM. Deliver ticket creation, an association with an existing CRM customer, essential ticket details, initial status/priority/category, role-controlled **assignment at creation**, list/search/filter/pagination, details, and basic editing. A ticket created by an Agent is automatically assigned to that Agent. When SuperAdmin creates a ticket, they may assign it to an active Agent or leave it unassigned. Follow-up changes to ticket assignment, status transitions, escalation and history belong to **TICK-002 — Ticket Workflow, Assignment & History**.

### 1. Existing codebase and boundaries

- Build on the **already implemented ARCH-001, AUTH-001 and CUST-001** stories. Preserve their established contracts, database, migration history, roles and existing behavior. Do not scaffold a new app or redefine user/customer identity.
- Reuse the current Clean Architecture projects (`Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`), CQRS/source-generated Mediator, FluentValidation, existing `CrmDbContext`, transaction/Unit of Work conventions, `/api/v1/` REST/API contracts and ProblemDetails responses.
- Reuse AUTH-001's ASP.NET Core Identity users, roles (`SuperAdmin`, `Agent`, `Customer`), enabled/disabled account checks and server-side authorization. Do not create a second Agent or authentication model.
- Reuse the existing CUST-001 **CRM Customers.Id** for ticket ownership (not `AspNetUsers.Id`). Both registered and contact-only customers can have tickets.
- Reuse the existing Angular standalone, feature-based architecture, Angular Material theme, navigation, guards, typed API services, shared UI and direction-neutral responsive styling.
- Planner must inspect the **actual current repository** (especially the CUST-001 customer model/lookup and AUTH-001 Agent services) rather than guessing class names, routes, migration state or filename line numbers.

### 2. Ticket information and relationships

- Each ticket has its own immutable system-generated **Ticket ID**, independent of Customer and Identity user IDs; a human-readable sequential ticket number is not required for TICK-001.
- Required fields: **CustomerId**, **Subject**, **Description**, **Category**, **Priority** and system-managed **Status**. System-managed audit metadata: **CreatedByUserId**, **CreatedAtUtc**, **UpdatedAtUtc** and nullable **AssignedAgentUserId**.
- On creation, the status is always **Open**. Neither staff creator may choose another initial status. TICK-001 supports viewing/filtering the status but **not status transitions**; TICK-002 adds transitions and the other lifecycle states.
- Use a small **predefined, consistent category set** for the first story: `General`, `Technical`, `Billing`, `Other`. Do not create category-administration screens or tables unless the actual architecture makes a simpler approach preferable and the planner explains why.
- Use a small **predefined priority set**: `Low`, `Medium` (default), `High`, `Urgent`. Priority is user-selected on ticket creation and can be edited by authorized staff. Do not calculate priority automatically from SLA rules.
- Validate that the selected CRM customer exists. Do not require the customer to have an Identity account or password. The CustomerId is chosen when the ticket is created and **is not editable** within TICK-001.
- Ticket Subject and Description are required, trimmed and length-validated with reasonable documented limits; validation errors follow existing API conventions. Creation/update timestamps are UTC.
- Preserve references to the actual registered/contact-only Customer and existing Identity/Agent users using suitable foreign keys and indexes; do not duplicate the customer or agent data on the ticket as a second source of truth.
- Ticket creation metadata (`CreatedByUserId`, `CreatedAtUtc`) cannot be spoofed or edited by clients. The creator and assigned Agent may be different when SuperAdmin creates a ticket.
- No hard deletion of tickets under this story.

### 3. Ticket creation and initial assignment — approved business rules

| Creating role | Customer selection | Initial assigned Agent | Assignment input |
| --- | --- | --- | --- |
| **Agent** | Must select an existing CRM customer | **Automatically assigned to the creating Agent** | No Agent selector; any submitted assignment ID must be ignored or rejected, never honored. Derive the Agent ID from the validated authenticated user. |
| **SuperAdmin** | Must select an existing CRM customer | **Selected active Agent, or `null` (unassigned)** | Optional Agent selector; selecting an Agent is allowed only if the account exists, has the Agent role, and is active/enabled. |
| **Customer** | Not allowed to create tickets in TICK-001 | N/A | Future Customer Portal story. |

- The **backend** must enforce the creator/assignment rules. Hiding controls in Angular is not sufficient.
- `CreatedByUserId` always comes from the validated authenticated user, not an untrusted client field.
- Agent creation is automatic **self-assignment only**; it is not workload-based automatic routing and cannot assign someone else.
- SuperAdmin can leave the new ticket unassigned for later allocation. A disabled Agent cannot be newly assigned a ticket.
- **TICK-001 supports assignment only during ticket creation.** Nobody—including SuperAdmin—can edit an existing ticket's assignment in this story. **TICK-002** introduces post-creation assignment and reassignment, and **only SuperAdmin** may perform these changes. Agents never transfer/reassign tickets to other Agents.
- No automatic routing based on workloads, schedules or rules; those belong to future SLA & Automation capabilities.

### 4. Permissions, visibility and editing

| Operation | SuperAdmin | Agent | Customer |
| --- | --- | --- | --- |
| Create ticket for an existing customer | Yes | Yes | No |
| Choose initial assigned Agent | Active Agent or leave blank | No; always assigned to self | No |
| List/search/filter tickets | All tickets | **Only tickets assigned to themselves** | No |
| View ticket details | Any ticket | Only tickets assigned to themselves | No |
| Edit subject/description/category/priority | Any ticket | Only tickets assigned to themselves | No |
| Change existing ticket assignment | Deferred to TICK-002 (SuperAdmin only) | Never | No |
| Change status / escalate / view full activity history | Deferred to TICK-002 | Deferred | Deferred |

- Enforce all list filtering and record-level access in the **backend** using authenticated identity/role information. An Agent must not bypass assigned-ticket visibility by requesting a ticket ID directly, changing query parameters, or posting another Agent ID.
- All SuperAdmin-created unassigned tickets are visible to SuperAdmin. They are not listed for Agents until subsequently assigned in TICK-002.
- Ticket creators and assigned Agents are conceptually separate: an Agent-created ticket has the same ID for both; a SuperAdmin-created ticket can have a different assigned Agent or be unassigned.
- Existing disabled-account/session checks from AUTH-001 continue to apply. Do not weaken existing authorization or allow an unassigned ticket to be claimed by Agents in this story.
- Registered Customer self-service ticket submission, tracking and visibility are deferred to **Customer Portal**, not added here.

### 5. Ticket management use cases and API

- Expose staff-protected, versioned REST endpoints for **create**, **list/search/filter/paginate**, **view details** and **update the permitted basic fields**. Use established `...Request`/`...Response` typed DTOs and camelCase JSON. Document role and ownership rules in OpenAPI.
- Suggested resource route: `/api/v1/tickets`. Follow project conventions for `GET`, `POST`, `GET /{id}` and `PUT /{id}`; no assignment-change endpoint and no status-change endpoint in TICK-001.
- Return enough read-only information to show selected Customer, ticket creator and assigned Agent clearly, without exposing password hashes, refresh-token metadata or unrelated sensitive Identity fields.
- Reuse the existing customer list/search API in the creation form for finding registered or contact-only customers. Reuse existing Agent-management/Identity abstractions where suitable for active-Agent validation and admin selection. The planner must inspect the actual existing endpoints and access restrictions; do not assume the Agents admin API is safe to reuse directly from an Agent UI.
- Follow CUST-001's existing pagination shape (`items`, `page`, `pageSize`, `totalCount`), one-based pages and sensible page-size limits, consistent ordering (newest first with a stable ID tie-breaker).
- Provide ticket search (at minimum by subject and ticket identifier if supported by the chosen ID presentation) and simple filters for customer, status, category and priority; the backend must always apply authorization filtering in addition to requested filters. Avoid advanced full-text search and configurable saved views.
- Basic ticket edits update **only Subject, Description, Category, Priority**. Customer, creator, created timestamp, initial assignment and status are unchanged in TICK-001.
- Use EF Core and a new **additive migration**; preserve existing identity, customer, refresh-session and migration data. Index ticket-to-customer relation, assigned-Agent relation and default list ordering as appropriate for actual queries. Never use `EnsureCreated` or drop/recreate the database.

### 6. Angular UI/UX

- Add a lazy-loaded **Tickets** feature and corresponding navigation for `SuperAdmin` and `Agent` only. `Customer` does not see Tickets staff UI.
- Provide a **Ticket List** with subject, customer, category, priority, Open status, assigned Agent or Unassigned, created date, search/filter controls and server-side paginator. Display only records permitted by backend policy.
- Provide a **Create Ticket** page where staff select an existing CRM customer, enter Subject/Description, choose Category and Priority; initial Status is displayed as Open/read-only.
- For an Agent creator, show **Assigned to you automatically**, with no Agent dropdown. For SuperAdmin, show an optional dropdown of **active Agents**, plus an **Unassigned** choice.
- Provide a **Ticket Details/Edit** view with customer information, creator, assigned Agent, created/updated dates, and editing for allowed basic fields. Show assignment and status as read-only in TICK-001; do not add transfer or change-status controls yet.
- Use current Angular Material responsive design, role-aware navigation, route guards, loading/empty/error states and recoverable request errors. Reuse existing API handling and shared components; do not introduce duplicate auth logic or credentials in browser storage.
- No mockups are provided. AI can select sensible layout/interaction patterns based on approved design conventions, but cannot add unrequested Ticket workflows.

### 7. Verification, documentation and later-story boundaries

- Add exactly **one meaningful backend happy-path unit test** and **one meaningful Angular happy-path unit test** for the new Ticket behavior using the existing testing frameworks. Do not introduce automated negative/edge/API/SQL integration/E2E/browser/performance suites. Manual role/ownership and successful-flow verification remains appropriate.
- Build both projects, run existing and new happy-path unit tests, inspect and apply migration without deleting existing data; verify Ticket creation by Agent (self-assignment), creation by SuperAdmin (selected Agent and unassigned), staff list/detail/edit/search/pagination and preservation of existing Customer/Auth behavior.
- Document the Ticket entity fields, category/priority choices, initial Open status, ID relationships, creation-time assignment rules, role/record visibility rules, endpoints and future-story boundaries in a new Ticket Management document and appropriate existing docs.
- Do **not** implement post-creation reassignment, advanced workflow/status transitions, activity timeline, manual escalation or automatic SLA/workload routing in TICK-001.

## Acceptance criteria

- [ ] TICK-001 extends ARCH-001, AUTH-001 and CUST-001 in place, reusing their architecture, Identity users/roles, CRM Customer IDs, EF Core transactions, API standards and Angular Material design; no unrelated module is recreated.
- [ ] A ticket has a stable Ticket ID, valid CRM CustomerId, required Subject/Description/Category/Priority, initial Open status, server-controlled creator/assignment fields and UTC timestamps.
- [ ] Both registered and contact-only CRM customers are valid ticket owners; the ticket never requires a Customer Identity login.
- [ ] Authenticated Agent creates a ticket for a selected customer and is automatically assigned to themselves. Backend derives creator/assignee from the authenticated account and never permits Agent to pick another assignee.
- [ ] Authenticated SuperAdmin creates a ticket for a selected customer, either selecting an existing **active Agent** or leaving assignment null. Disabled/non-Agent accounts cannot be assigned.
- [ ] On creation, status is always Open; Category and Priority come from documented fixed sets and Medium is the default priority.
- [ ] SuperAdmin can view/search/filter/paginate all tickets and update permitted basic fields; Agent can perform these actions only for tickets assigned to themselves; Customer role cannot access staff ticket APIs.
- [ ] Ticket list uses existing paging conventions, searches relevant ticket attributes, filters by customer/status/category/priority and displays a deterministic stable order.
- [ ] Ticket details and edit endpoints expose only safe DTO data; updates cannot alter ticket ownership, creator, existing assignment or status.
- [ ] Backend policies/record checks independently enforce permissions, without trusting Angular route guards, client-provided user IDs or forged assignment values.
- [ ] Angular Tickets navigation, list/search/filters/pagination, create, details and basic-edit flows work for SuperAdmin and Agent; role-specific creation assignment is clearly explained in UI.
- [ ] Additive EF Core migration creates Ticket persistence, relationships and indexes without dropping, recreating or changing existing Identity/Customer tables or data.
- [ ] Existing AUTH-001 authentication and Agent administration and CUST-001 customer self-registration/profiles/contact records continue functioning unchanged.
- [ ] One new backend and one new frontend happy-path unit test pass; backend and frontend builds pass; planned manual flows, migration and critical role/assignment checks are verified without new test frameworks.
- [ ] Documentation distinguishes initial assignment (TICK-001) from assignment/reassignment, status workflows, manual escalation and history (TICK-002), and future Customer Portal and SLA & Automation features.

## Attachments

None required: the original core-feature document is already retained in the ARCH-001 story at `.squad/stories/project-architecture/project-init-architecture/attachments/azm_squad_customer_support_crm 1.pdf`. The relevant Ticket Management requirements are fully reproduced in this intake, so planning does not depend on that attachment being available.

## Dependencies

- **Blocked by / related ids:** `ARCH-001`, `AUTH-001`, `CUST-001` (implemented and accepted). `TICK-002` follows this story and adds reassignment, workflow transitions, manual escalation and history.
- **Depends on code areas or other stories:** Existing `backend/src/Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`, `backend/tests/Crm.UnitTests`, `frontend/src/app/core`, `frontend/src/app/shared`, `frontend/src/app/features/customers`, and `docs/api-conventions.md`, `docs/identity-and-access.md`, `docs/customer-management.md`, `docs/persistence-and-transactions.md`, `docs/frontend-ui-foundation.md`. Inspect real code to establish exact names and service boundaries.

## Extra notes

- This file defines **business requirements and acceptance criteria**, not the implementation plan. Produce the implementation plan separately through Squad-Kit after approval; do not implement code during intake review.
- Assignment rules are user-approved: **Agent creator → self-assigned; SuperAdmin creator → select active Agent or leave unassigned; only SuperAdmin can later assign/reassign existing tickets (in TICK-002).**
- Initial TICK-001 scope intentionally separates ticket CRUD and initial assignment from the later workflow. Do not introduce Agent self-claim, Agent-to-Agent transfers, or auto-routing based on workload.
- The chosen small category and priority sets are intentionally simple starting values for this training CRM; revise the intake before planning if the business wants different labels.
- SuperAdmin is an administrator, not an Agent account: selecting SuperAdmin as the assigned Agent is not allowed.
- Prefer straightforward, maintainable implementation over speculative abstractions.

## Technical hints

- Stack: .NET 10 / ASP.NET Core, EF Core 10 SQL Server LocalDB, Clean Architecture + source-generated Mediator/CQRS + FluentValidation + Unit of Work, Identity/JWT, Angular 22 standalone/Material, single repository.
- Consider a Ticket domain model with immutable Ticket ID and CustomerId, subject/description/category/priority, initial Open status, CreatedByUserId, nullable AssignedAgentUserId, and UTC timestamps; use domain methods to protect invariants and keep persistence detail in Infrastructure.
- A Creator/Assignee is an existing Identity user, whereas a ticket's CustomerId points to a CRM Customer record. Avoid polymorphic or duplicate user/customer IDs.
- Derive the current user ID and role from validated server authentication. Expose only required fields in create/update requests; use explicit server-side rules for optional SuperAdmin assignee validation and Agent self-assignment.
- For Agent ticket reads/edits, scope at the server query/handler level to the validated Agent ID. Do not return all tickets and filter them in Angular.
- Reuse CUST-001 paged responses and existing customer-selection search. Provide a safe staff-usable read path for selectable **active** Agents if the existing Agent management API is SuperAdmin-only; do not relax the security of admin-only endpoints simply to populate a dropdown.
- Add a new migration after existing ARCH/AUTH/CUST migrations; do not modify applied migrations. Plan verification of valid customer/agent references and preserving prior database contents.
- Add only the feature-specific approved happy-path unit tests; keep manual API and UI smoke checks concise.

## Out of scope

- TICK-002 capabilities: changing an existing ticket's assignee, assigning unassigned tickets, Agent self-claim, workflow/status transitions, Resolve/Close/Reopen actions, manual escalation, and detailed event/activity history.
- Later CRM modules: Customer Portal self-service ticket submission/tracking, SLA targets, rule-based/automatic assignment, notifications, dashboards, reports, multi-channel communications and AI.
- Category/priority administration, ticket attachments, comments/internal notes, email/WhatsApp/SMS integration, configurable taxonomies, ticket deletion, bulk import/export, complex audit/event sourcing, multi-branch/department ownership and advanced permission systems.
- Additional automated negative/edge/API/SQL integration/E2E/browser/performance/security-abuse tests, new test frameworks or unnecessary infrastructure.
