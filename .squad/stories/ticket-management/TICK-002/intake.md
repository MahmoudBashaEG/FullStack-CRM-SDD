# Ticket Workflow, Assignment & History — Story Intake

## Feature

- **Feature name (display):** Ticket Management — Workflow, Assignment & History
- **Feature slug (folder under `plans/`):** `ticket-management`

## Tracker

- **Tracker type:** `none`
- **Work item id:** `TICK-002`
- **Work item type:** `Business Feature Story`
- **Status:** `Draft`
- **Assignee:** `Unassigned`
- **Labels:** `ticket-management, ticket-workflow, ticket-assignment, ticket-history, escalation, authorization, aspnet-core, angular, cqrs, ef-core, happy-path-unit-tests, squad-kit`

## Title

Ticket Workflow, Assignment & History

## Description

### Goal

Complete the **second and final core Ticket Management story** in the existing Customer Support CRM. Extend the committed `TICK-001` implementation so SuperAdmin can assign, unassign and reassign **existing** tickets; authorized staff can advance tickets through a controlled lifecycle; assigned Agents can request manual escalation; and the system records a trustworthy, readable history of ticket activity. Preserve all `TICK-001` ticket creation, customer, search, priority/category and permission behavior. This is an extension of the existing module, **not** a new ticket implementation.

### 1. Existing system and compatibility requirements

- Prerequisites: committed `ARCH-001`, `AUTH-001`, `CUST-001` and `TICK-001` (Story 04). Inspect the **actual repository and current code** before planning; do not assume an older plan's line numbers or symbols remain accurate.
- Reuse .NET 10 ASP.NET Core, existing Clean Architecture projects (`Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`), source-generated Mediator/CQRS, FluentValidation, EF Core SQL Server, `IUnitOfWork`/transaction conventions, `/api/v1/` routes, validation/ProblemDetails and JWT-based authorization.
- Reuse the existing `Ticket` entity, `Tickets` table, `ITicketRepository`, ticket queries/commands/API, fixed string-enum conventions, Identity roles/users, CRM Customers, Angular 22 Material tickets feature, guards, navigation, typed API services, and customer/agent lookup services. Extend in place instead of duplicating any of them.
- Existing data matters: retain all tickets created under `TICK-001`, including their stable IDs, customer relationships, creator, assignee, subject, description, category, priority, timestamps, and `Open` status. Preserve Identity users, sessions, Customers and prior migrations.
- Preserve `TICK-001` creation-time assignment: **Agent creates => assigned to that Agent**; **SuperAdmin creates => optionally assign an active Agent or leave null**. `CreatedByUserId` is immutable and comes from authenticated identity. Only an existing `Agent` with `IsActive = true` may be chosen as a new assignee.
- Preserve staff visibility: **SuperAdmin sees all tickets**; **Agent can list/view/edit only tickets currently assigned to that Agent**; Customer cannot use staff ticket APIs/UI. A record that moves out of an Agent's assignment immediately moves out of their view, including direct-link and later edit attempts (404, not content leakage).
- Preserve the preexisting four basic edit fields (`Subject`, `Description`, `Category`, `Priority`). They still follow the established `TICK-001` permissions; add historical recording of successful edits. Do not accidentally change `PUT /api/v1/tickets/{id}` semantics or let that endpoint edit assignment/status.

### 2. Existing-ticket assignment and reassignment — **approved rules**

- **Only SuperAdmin** can assign, reassign, or unassign an **existing** ticket. Agents cannot assign a ticket to themselves, claim an unassigned ticket, transfer a ticket, or select another Agent. Customers have no access.
- SuperAdmin may:
  - Assign an existing unassigned ticket to an **active Agent**.
  - Reassign an existing ticket from Agent A to another **active Agent**.
  - Explicitly set the current assignee to **Unassigned** (`null`) to return it to the admin-controlled queue.
- Reject unknown, inactive/disabled, non-Agent (SuperAdmin/Customer), or malformed target IDs using established validation behavior. No valid ticket may be assigned to SuperAdmin as the *assignee*.
- Perform assignment changes against the server-side authenticated SuperAdmin role. Never trust `createdByUserId`, `actorUserId`, or permission flags from request data. Reassigning does **not** change ticket creator or CustomerId.
- Record each actual assignment change in ticket history: prior assignee, new assignee, authenticated actor and UTC time. A no-op assignment to the current assignee should not produce a duplicate event or misleading timestamp update.
- Once reassigned, Agent A loses visibility immediately and Agent B gains visibility through the existing SQL-level assigned-Agent scope. Unassigned tickets remain visible to SuperAdmin only. Existing status/priority/category values are preserved when changing assignment; no automatic status reset or notification.
- **No** workload balancing, round-robin assignment, Agent self-claim, transfer requests, assignment recommendations, or SLA-based routing.

### 3. Ticket lifecycle — **proposed simple defaults; confirm during intake review**

- Extend the `TicketStatus` enum stored and serialized **by name** with `InProgress`, `Resolved`, and `Closed`. Preserve existing `Open` and keep existing persisted rows valid. In the Angular UI, display `InProgress` as **In Progress**. Newly created tickets still begin as `Open`, with no user-selectable initial status.
- Use this **explicit transition matrix** for TICK-002; every other transition is invalid:

| Current status | Allowed next status | Meaning |
| --- | --- | --- |
| `Open` | `InProgress` | Begin handling |
| `InProgress` | `Resolved` | Work completed; awaiting closure |
| `Resolved` | `Closed` | Finish ticket |
| `Resolved` | `InProgress` | Resolution unsuccessful; resume work |
| `Closed` | `Open` | Reopen ticket for a new work cycle |

- **SuperAdmin** may execute permitted transitions for any ticket. An **Agent** may execute permitted transitions only on a ticket **currently assigned to them**. No Customer transitions yet. The client submits the desired target status, but the **backend** validates its authorization and the current-to-next transition; invalid/stale transitions must return a consistent validation/conflict response and must not mutate the ticket or history.
- Allow SuperAdmin to initiate a workflow action even on an unassigned ticket (as permitted by the matrix); do not silently assign the ticket. Assignments and status are independent properties.
- Do **not** add freeform status editing through the existing basic-edit request; expose an explicit transition use case/action. Status is read-only on the basic edit form and presented as a separate workflow action.
- Each valid transition records old status, new status, actor, and UTC timestamp. A status transition updates the ticket's `UpdatedAtUtc` as an actual change. New statuses must be usable in existing ticket-list filters and shown with suitable badge labels/tones.
- Do not implement configurable workflows, SLAs, timers, scheduled auto-closing, reminders, status approvals, or status-based ownership changes.

### 4. Manual escalation — **proposed simple defaults; confirm during intake review**

- Escalation is a **separate indicator**, not a fifth ticket status. A ticket may be marked **Escalated** while retaining its current `Open` or `InProgress` status. This is a manual attention flag, **not** an automatic assignment, notification, or priority calculation.
- An **Agent assigned to the ticket** or **SuperAdmin** may mark an `Open`/`InProgress` ticket as escalated, entering a **required short reason** (trimmed; maximum 500 characters). Capture who escalated it and when (UTC). Reject a duplicate request if it is already escalated without creating another event.
- **Only SuperAdmin** may manually clear an active escalation. Record the clear action and actor/time in history. Assigned Agents cannot clear it themselves.
- **Proposed lifecycle interaction:** when an escalated ticket transitions to `Resolved`, automatically clear its active escalation **as part of the same successful operation** and record the clearance in history. This avoids leaving resolved/closed tickets flagged as needing escalation. Reopening a ticket does not reinstate a prior escalation.
- Show an escalation badge/flag and current escalation reason on ticket details; expose a simple **Escalated** filter on the staff ticket list (subject to existing assigned-Agent visibility). Prevent escalation of `Resolved` or `Closed` tickets.
- Preserve full past escalation events in activity history even after the indicator is cleared. No escalation levels, escalation recipient/team, email alert, SLA breach detection, auto-routing or management approval chain.

### 5. Ticket activity history — **required**

- Introduce **append-only** ticket activity records with their own stable IDs and FK to the existing ticket. For each event, retain: event type, related Ticket ID, authenticated actor (or explicit system actor for system-generated changes), UTC occurrence time, and enough structured old/new data or concise safe details to explain what changed. Do not store password/session/secret data or duplicate entire user/customer profiles.
- New tickets created **after TICK-002 is deployed** get a `Created` activity event. Record successful changes for:
  - Basic updates to subject, description, category and priority (identify the changed fields and meaningful before/after values where suitable; do not log unchanged fields).
  - Assignment from unassigned, reassignment, and unassignment.
  - Workflow status transitions and reopen actions.
  - Manual escalation and escalation clearance (including automatic clearance on resolution, clearly attributed to the triggering action/system).
- Ticket modification and its corresponding history event(s) must succeed or fail **atomically**, using existing EF Core/Unit of Work transaction facilities. No successful ticket change may silently omit its activity record. Never create events for rejected requests or no-op updates.
- **Legacy tickets from TICK-001 have no historical event records.** Do **not** fabricate `Created`/`Assigned`/`Updated` events or timestamps for actions that were never logged. Show a clear empty-state note such as **“History tracking started with TICK-002; earlier activity was not recorded.”** on an older ticket until the first tracked change; creation metadata already present on the ticket remains visible separately. No destructive backfill or historical guesswork.
- Show the ticket's events on its detail page in chronological **newest-first** order with a deterministic tie-breaker, actor display name, event label, timestamp, and readable change details. Use bounded/paged retrieval rather than an unbounded table dump.
- Apply existing authorization at the history endpoint: SuperAdmin sees all ticket activity; an Agent only sees the history of tickets **currently assigned** to them; Customer sees none. On reassignment, the former Agent immediately loses history access along with the ticket.
- Historical records are immutable from UI/API: no event edit/delete, no full event-sourcing system, no background event bus or external auditing service.

### 6. API / application behavior

- Keep the existing `GET/POST/PUT /api/v1/tickets` contracts backwards compatible and extend ticket response data only as necessary for status/escalation UI. Continue returning enums as PascalCase **strings** (`Open`, `InProgress`, `Resolved`, `Closed`), never numbers.
- Provide clearly separated versioned staff API operations for:
  - **Change assignee** (SuperAdmin only; nullable target permitted).
  - **Transition status** (SuperAdmin or currently assigned Agent).
  - **Escalate** and **clear escalation** with the permissions above.
  - **List paged ticket activity** (scoped to current ticket visibility).
- Suggested resource organization: subresources/actions under `/api/v1/tickets/{id}/...`; the planner may choose exact HTTP verbs/routes consistent with the existing code and describe the request/response shape. Avoid a general-purpose mutable patch endpoint that could bypass role rules.
- Invalid or inaccessible ticket IDs: preserve existing `404` scoping for Agents; Customer requests to staff-only endpoints get `403`; invalid assignees, missing escalation reasons and invalid transitions yield documented `400` or `409` outcomes consistent with project conventions.
- Reuse current `GET /api/v1/agents` (SuperAdmin-only) for the reassignment selector rather than exposing admin-only Agent data to Agents. Preserve server-side active-Agent validation regardless of dropdown filtering.
- Ensure assignment, workflow and history writes are safe under ordinary concurrent requests. Re-check current state/visibility when applying commands and document the chosen concurrency behavior; avoid silently overwriting an invalid transition due to stale UI state. Keep implementation lightweight; do not introduce distributed locking or complicated job systems.
- Keep optional ticket-list filters/paging consistent with TICK-001. Support all new status values, and add the simple escalation filter. No new search engine or globally configurable workflow rules.

### 7. Angular UI/UX

- Extend the **existing** `features/tickets/` pages and API service; do not add a separate Ticket Management application or duplicate core ticket screens.
- On ticket details, show current status with context-specific actions (for example **Start Progress**, **Resolve**, **Close**, **Resume**, **Reopen**) limited to the transition matrix and the logged-in actor's permissions.
- SuperAdmin gets an **assignment control** for existing tickets: select an active Agent, replace the current assignee, or choose Unassigned. Assigned Agents see current assignee read-only and cannot transfer a ticket.
- Show manual **Escalate** (with required reason) to SuperAdmin or the assigned Agent for `Open`/`InProgress` tickets; show **Clear escalation** only to SuperAdmin when escalated. Reflect the badge/filter and clear outcome in the UI.
- Show a readable **Activity / History** section on the existing ticket-detail page, with loading, pagination or Load More, clear empty/error states, and retry. Actor identity and before/after changes should be understandable without exposing internal DB identifiers unnecessarily.
- Preserve Ticket list/create/detail/edit, customer picker, filters, existing global nav, responsive Angular Material styling, route guards, session management and existing error handling. Refresh ticket details and activity after successful actions; when reassignment removes the current Agent's access, show a recoverable access-lost/not-found message rather than continuing to show stale ticket content.
- Accessible controls and user-friendly labels are preferred; no new packages or visual redesign unless strictly necessary.

### 8. Verification, documentation and scope boundaries

- Add **exactly one meaningful backend happy-path unit test** and **one meaningful frontend happy-path unit test** for TICK-002, retaining existing tests. Use existing frameworks and conventions; no new automated integration, API, SQL, E2E/browser, negative-path, load or security-abuse suites. Important role and transition checks may be verified manually.
- Run all existing + new unit tests and both builds; review and apply **one additive migration** as needed for new status-independent escalation data and activity storage. Never edit TICK-001's already-applied `AddTickets` migration or drop/recreate existing tables/data.
- Verify the existing tickets still load and retain owner/assignment and category/priority, new statuses serialize and persist as strings, new events remain durable after reload, and activity is correctly scoped. Manually verify Agent/SuperAdmin behaviors, transitions, assignment, escalation, and regression of the original TICK-001 creation/list/edit flows.
- Inspect migration SQL before applying, compare pre/post counts for users, customers and existing tickets, and document schema/data changes. If a legacy history empty-state is used, verify it honestly describes pre-tracking activity.
- Update `docs/ticket-management.md` and related API, authorization, persistence, and frontend documentation, explaining workflows, permissions, event semantics, escalation behavior, migration impact and future deferred features.
- Do not expand into Customer Portal ticket submission/tracking, comments/internal notes, communication channels, email/WhatsApp/SMS, attachments, SLAs/notifications, automatic routing or dashboards.

## Acceptance criteria

- [ ] TICK-002 extends the committed TICK-001 module and preserves existing `CustomerId`, creator, assignee, ticket IDs, tickets, roles, API conventions and current data; no duplicate Ticket system is introduced.
- [ ] Agent-created tickets remain self-assigned; SuperAdmin-created tickets still allow an active Agent or Unassigned; original ticket creation and basic management continue working.
- [ ] Only SuperAdmin can assign, reassign or unassign an **existing** ticket; the assignee must be an active Agent or null, and Agent self-claim/transfer is impossible server-side.
- [ ] Reassignment updates current visibility immediately: old Agent loses ticket/list/history access (404 for scoped direct reads) and new Agent gains access; unassigned tickets remain SuperAdmin-only.
- [ ] Status supports `Open`, `InProgress`, `Resolved`, `Closed` as string names, while every new ticket initially starts `Open`.
- [ ] Only the defined workflow transitions are accepted and are authorized server-side; Agent transitions require current assignment, while SuperAdmin may transition any ticket.
- [ ] Invalid or stale transitions do not mutate the ticket or create misleading history; permitted transitions update `UpdatedAtUtc` and add an audit event.
- [ ] Assigned Agent or SuperAdmin can manually escalate eligible active tickets with a required reason; only SuperAdmin can manually clear, and resolution clears active escalation with an explicit recorded event (proposed policy).
- [ ] Escalation state, reason and history are visible where authorized, and an escalation-only ticket-list filter honors assigned-Agent scope.
- [ ] New tickets and successful post-deployment field edits, assignments, transitions and escalation actions generate immutable activity events with actor/time/meaningful change details; event persistence is atomic with the ticket change.
- [ ] Legacy TICK-001 tickets remain intact and do not receive invented historical actions; Activity clearly states when pre-tracking events are unavailable.
- [ ] Activity retrieval is paged/bounded, ordered deterministically, and secured by the same ticket-level record access as details; Customer cannot access staff history.
- [ ] Ticket details UI provides role-aware assignment, workflow and escalation actions plus readable, paged Activity; existing tickets UI and regression behavior are preserved.
- [ ] A safe additive EF Core migration preserves prior tickets, customers, users, sessions and applied migration history; enum string mapping remains intact.
- [ ] Backend and frontend builds succeed; all previous unit tests and exactly one new backend + one new frontend happy-path unit test pass; manual acceptance covers critical role, workflow, escalation and history flows.
- [ ] Documentation accurately describes workflow, assignment, escalation and activity, including pre-TICK-002 history limitations, and all remaining out-of-scope features.

## Attachments

None required. Business requirements and relationships relevant to TICK-002 are fully reproduced above. The existing TICK-001 intake and implementation plan in the repository are referenced below for context; they are not external attachments.

## Dependencies

- **Blocked by / related ids:** `ARCH-001`, `AUTH-001`, `CUST-001`, `TICK-001` (all implemented and committed). TICK-002 is Story 05 and the second core Ticket Management story.
- **Depends on code areas or other stories:** `.squad/stories/ticket-management/TICK-001/intake.md`, `.squad/plans/ticket-management/04-story-ticket-creation-management.md`, existing backend Ticket domain/application/infrastructure/API, `backend/tests/Crm.UnitTests`, `frontend/src/app/features/tickets`, existing Identity/Agent lookup and Customer repositories, `docs/ticket-management.md`, `docs/api-conventions.md`, `docs/identity-and-access.md`, `docs/persistence-and-transactions.md` and `docs/frontend-ui-foundation.md`. Planner must inspect the **currently committed code**, not rely on old line references.

## Extra notes

- **Already agreed with the user:** only SuperAdmin can change an existing assignment; Agents cannot claim/transfer tickets; assignment during creation stays exactly as TICK-001; Agent ticket visibility is based on **current** assignment.
- **Proposed defaults to approve before running `/squad-plan`:** the five lifecycle transitions in §3; Agent + SuperAdmin ability to raise escalation, SuperAdmin-only manual clearance, and automatic escalation clearance on Resolve in §4. These are specific design proposals for this intake, **not previously approved business decisions**. Adjust this file if different rules are desired.
- No work has started on TICK-002 implementation. This intake supplies requirements/acceptance criteria only; Squad-Kit planning happens separately after review.
- Favor maintainable, small feature-specific changes. Protect existing TICK-001 behavior instead of rewriting it. Keep server-side permission rules authoritative, even if the Angular UI hides forbidden actions.
- Preserve Git history and applied migrations; do not commit automatically during implementation.

## Technical hints (optional)

- Extend `Crm.Domain.Tickets.Ticket` with explicit domain operations for assignment, workflow transition and escalation; keep basic update separate. Add `InProgress`, `Resolved`, `Closed` to the existing `TicketStatus` enum **without renumbering or changing existing values**; the existing EF string conversion should support additive names.
- Add a small dedicated activity entity/table (for example `TicketActivity`, if appropriate after inspecting actual naming conventions), foreign-keyed to Ticket with `Restrict` on delete and an index for newest-first history queries. Consider minimal escalation fields on `Ticket`; choose a simple durable model rather than event sourcing or an external queue.
- For each state-changing handler, derive actor/role server-side and verify the live ticket's scoped access and current state. Stage all ticket + audit changes then persist atomically via the established Unit of Work/transaction conventions. Do not allow an Agent's previous assignment to authorize access after reassignment.
- For ticket-created/edited events, extend the **existing** create/basic-update handlers to append activity going forward. Keep `CreatedByUserId` immutable and all newly added audit actor values derived from validated Identity claims.
- Reuse ticket detail/list projection strategy and the existing SuperAdmin-only `GET /api/v1/agents` selector; avoid changes to Identity and Customer contracts unless clearly required.
- When reading history, order by occurrence timestamp descending plus ID descending and page at the database level. Display only safe field labels and changes; think through actor-name display if accounts are later disabled or removed (do not duplicate credentials).
- Proposed API action design could be `PUT /api/v1/tickets/{id}/assignee`, `POST /api/v1/tickets/{id}/transitions`, `POST /api/v1/tickets/{id}/escalation`, `DELETE /api/v1/tickets/{id}/escalation`, and `GET /api/v1/tickets/{id}/activity`, but preserve existing project API conventions and let the planner choose consistent contracts.
- If concurrent state changes need a guard, use a lightweight existing DB approach and document 409 behavior; do not add a distributed lock/message broker.
- Keep frontend interaction simple and consistent with existing `TicketDetailPage`, status badges, snackbars, errors, paginator and route navigation. Reuse current data models, do not introduce new UI/auth frameworks.

## Out of scope

- Customer Portal self-service ticket submission, customer ticket tracking, external users accessing staff history.
- Agent assignment/transfer/claim/reassignment actions, except **Agent auto-self-assignment on creation already present in TICK-001**; no automatic workload/rule-based routing.
- Automatic escalations, escalation levels/queues/approval workflows, SLA deadlines, notifications, reminders, email/SMS/WhatsApp/chat integrations.
- Comments, internal notes, attachments, ticket deletion, merging, bulk updates/import/export, custom ticket numbers or category/priority administration.
- Configurable workflow designers, arbitrary custom statuses, status automation/background jobs, event sourcing, message brokers, advanced reporting or analytics.
- New negative-path, integration, SQL/API, E2E/browser, performance, penetration or abuse test suites and additional test frameworks beyond the approved happy-path unit tests.
