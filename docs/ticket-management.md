# Ticket management (TICK-001, TICK-002)

Ticket creation and basic management (TICK-001) plus post-creation assignment, status workflow, manual escalation and append-only activity history (TICK-002) for staff. Customer Portal and SLA/automation remain future work (section 12).

## 1. Ticket entity

Table `Tickets` (`Crm.Domain.Tickets.Ticket`).

| Field | Notes |
|---|---|
| `Id` | GUID v7 generated in code; immutable; the ticket identifier (no sequential number yet) |
| `CustomerId` | FK → `Customers.Id` (registered **or** contact-only; never `AspNetUsers.Id`); chosen at creation, not editable |
| `Subject` | required, trimmed, ≤ 200 |
| `Description` | required, trimmed, ≤ 4000 |
| `Category` / `Priority` / `Status` | fixed sets (section 2), stored as `nvarchar(20)` names |
| `CreatedByUserId` | FK → `AspNetUsers.Id`; always the authenticated caller |
| `AssignedAgentUserId` | nullable FK → `AspNetUsers.Id`; set at creation, changed later only by SuperAdmin (section 4) |
| `IsEscalated` | `bit`, default `0` (existing rows are not escalated). A flag, **not** a status |
| `EscalationReason` / `EscalatedAtUtc` / `EscalatedByUserId` | set while escalated (reason ≤ 500, `EscalatedByUserId` FK → `AspNetUsers.Id`); all null/false when not escalated |
| `Version` | SQL Server `rowversion` (shadow property). Server-side only; never sent to or accepted from the client (section 8) |
| `CreatedAtUtc` / `UpdatedAtUtc` | UTC |

All FKs are `Restrict`. Customer, creator, agent and escalating-user names are joined at read time, not copied onto the ticket. Tickets are never deleted. Indexes: `(CreatedAtUtc DESC, Id DESC)`, `(AssignedAgentUserId, CreatedAtUtc DESC, Id DESC)`, `(CustomerId, CreatedAtUtc DESC, Id DESC)`, `CreatedByUserId`, `EscalatedByUserId`.

## 2. Fixed sets and workflow

- **Category:** `General`, `Technical`, `Billing`, `Other`.
- **Priority:** `Low`, `Medium` (default), `High`, `Urgent` — chosen by staff, never calculated.
- **Status:** `Open`, `InProgress` (shown as "In Progress"), `Resolved`, `Closed`. New tickets are always `Open`. Status never changes through the basic edit; only through the transition endpoint.

Exactly five transitions are legal (`Crm.Domain.Tickets.TicketWorkflow`, mirrored for display only in `features/tickets/ticket-workflow.ts`); everything else, including the same status, is rejected with 409:

| From | To | UI action |
|---|---|---|
| `Open` | `InProgress` | Start progress |
| `InProgress` | `Resolved` | Resolve |
| `Resolved` | `Closed` | Close |
| `Resolved` | `InProgress` | Resume |
| `Closed` | `Open` | Reopen |

SuperAdmin may run a transition on any ticket (including unassigned ones — this does **not** assign it). An Agent may run transitions only on tickets **currently assigned to them** (others return 404). A request carries `fromStatus` (the status the user saw) and `toStatus`; a `fromStatus` that differs from the stored status is a stale request ⇒ 409.

Stored and serialized by name (a global `JsonStringEnumConverter` rejects integers; query-string filters bind by name). Changing a set is a code change plus a doc update; there is no admin UI.

## 3. Creation-time assignment (unchanged from TICK-001)

| Creator | Customer | Initial assignee |
|---|---|---|
| Agent | existing CRM customer | **the creating Agent**. A submitted `assignedAgentUserId` equal to the caller is harmless; any other value ⇒ 400 |
| SuperAdmin | existing CRM customer | a selected **active Agent**, or none (unassigned). Disabled Agents, SuperAdmin, Customer accounts and unknown ids ⇒ 400 |
| Customer | — | not allowed (403) |

The creator always comes from the token. Agent self-assignment is not workload routing. An unknown `customerId` ⇒ 400.

## 4. Assignment of existing tickets

`PUT /api/v1/tickets/{id}/assignee` with `{ "assignedAgentUserId": "<guid>" | null }` — **SuperAdmin only** (Agents and Customers get 403).

- Assign an unassigned ticket, reassign to another Agent, or unassign (`null`). The target must be an **active Agent** (disabled Agents, SuperAdmin, Customer accounts, unknown ids and an empty GUID ⇒ 400).
- The same assignee as now is a **no-op**: 200, no event, `UpdatedAtUtc` unchanged. (This is checked before validating the target, so re-submitting a since-disabled current assignee is not an error.)
- Status, escalation, customer and creator are untouched.
- Visibility follows the **current** assignee, immediately: the former Agent gets 404 on detail, edit, workflow actions and activity and loses the list entry; the new Agent gains them. Unassigned tickets are visible to SuperAdmin only.
- There is no Agent claim, transfer, routing or workload balancing, and none is planned.

## 5. Escalation

A manual attention flag, separate from `Status`.

- **Escalate** — `POST /api/v1/tickets/{id}/escalation` `{ "reason": "…" }`. SuperAdmin (any ticket) or the currently assigned Agent. Only `Open`/`InProgress` tickets. Reason required, trimmed, ≤ 500 (blank/missing/too long ⇒ 400). Already escalated, or `Resolved`/`Closed` ⇒ 409.
- **Clear** — `DELETE /api/v1/tickets/{id}/escalation`. **SuperAdmin only** (Agents ⇒ 403). Not escalated ⇒ 409. Clearing nulls the reason/by/at fields; history keeps the events.
- **Automatic clearance:** transitioning an escalated ticket to `Resolved` clears the flag in the same save and records `EscalationAutoCleared` (actor = the user who resolved). Resume/Reopen never re-escalate; escalation survives assignment changes and `Open → InProgress`.
- **Automatic escalation (SLA-001):** an enabled `SlaBreached` + `EscalateTicket` rule escalates an Open/In Progress ticket once; `EscalatedByUserId` is null = Automation. Already escalated: no second event. See [sla-and-automation.md](sla-and-automation.md).
- The list filter `escalated=true|false` narrows within the caller's scope.

## 6. Activity history

`ActorUserId` is nullable since SLA-001: null = **Automation** (shown as "Automation", `isAutomatic: true`); an automatic `AssigneeChanged` carries the rule name in its reason.

Table `TicketActivities` (`Crm.Domain.Tickets.TicketActivity`), **append-only**: there is no update/delete method, endpoint or UI.

| Event type | Recorded when | Data |
|---|---|---|
| `Created` | a ticket is created (after TICK-002 only) | initial assignee in `ToAgentUserId` |
| `FieldChanged` | a basic edit changes a field — **one row per changed field** | `Field` (`Subject`/`Description`/`Category`/`Priority`), `OldValue`/`NewValue`. **Description values are not stored** (only that it changed) |
| `AssigneeChanged` | assignment actually changes | `FromAgentUserId` → `ToAgentUserId` |
| `StatusChanged` | a transition | status names in `OldValue`/`NewValue` |
| `Escalated` | escalation raised | `Reason` |
| `EscalationCleared` | SuperAdmin clears | — |
| `EscalationAutoCleared` | ticket resolved while escalated | — |
| `FirstResponseRecorded` | Mark as Responded (SLA-001) | — |

Every event stores the authenticated actor and the UTC time. Actor/agent **names are joined at read time** from `AspNetUsers` (accounts are never deleted, so disabled users still display; a later rename changes how old events display). No passwords, stamps or profiles are copied.

- **Atomic:** events are appended by the `Ticket` aggregate's own methods and inserted by the same single `SaveChanges` as the ticket change, so a change cannot be saved without its event and a failed save leaves neither. Rejected requests (validation, 404, 409) and no-ops create no events.
- **Read:** `GET /api/v1/tickets/{id}/activity?page=1&pageSize=20` — CUST-001 paging rules (page one-based, default size 20, clamped to 100), ordered `OccurredAtUtc DESC, Id DESC` (events of one save share a timestamp; the identity `Id` breaks ties). Visibility equals ticket visibility (Agent: only currently assigned tickets, otherwise 404; Customer 403). Responses carry names, never user ids.
- **Legacy tickets:** tickets created under TICK-001 have **no events** and none are fabricated (no backfill). The ticket's creation data stays in the Summary card. The UI shows "History tracking started with TICK-002; earlier activity was not recorded." only after the oldest page is loaded and it contains no `Created` event.

## 7. Visibility and editing

- SuperAdmin: lists, views and edits every ticket (including unassigned ones).
- Agent: lists, views and edits **only tickets currently assigned to them**. Unassigned tickets are not visible to Agents.
- Customer: no access.

Enforcement: the `Staff` policy (plus `SuperAdmin` on assignment and clearing escalation), a `TicketActor` built from the validated JWT (`sub` + role) and an `agentScope` applied first in every SQL query. A ticket outside the caller's scope returns 404 (existence is not revealed). Read-backs use the same scope. Angular guards and buttons are usability aids only; the API re-checks everything.

`PUT /tickets/{id}` changes only `subject`, `description`, `category`, `priority` (+ `UpdatedAtUtc`), records a `FieldChanged` per changed field, and when nothing changed returns 200 without saving (no event, `UpdatedAtUtc` unchanged).

`PUT /tickets/{id}/category` (AI-001) changes only the category with the same scope, permissions and `FieldChanged` event; subject, description and priority are taken from the stored row. `expectedCategory` must equal the current category (409 otherwise).

## 8. Concurrency

Two separate mechanisms:

1. **Overlapping requests — row version.** `Tickets.Version` is a server-side `rowversion`. The version checked is the one EF loaded in the **same request**; it is never exposed to the client. If another request commits to the ticket between this request's load and save, the `UPDATE … WHERE Version = @loaded` affects 0 rows, `EfUnitOfWork.SaveChangesAsync` raises `ConflictException` ⇒ **409** "The record was changed by another request. Reload it and try again.", and nothing (ticket or events) is saved. This applies to every ticket command, including `PUT`.
2. **Stale browser pages — request preconditions.** The row version cannot detect a page that was merely open for a while. Instead: a transition's `fromStatus` must equal the stored status (409); escalate requires "not already escalated" and Open/InProgress (409); clear requires "currently escalated" (409); assigning the current assignee is a no-op.

**Last-write-wins (unchanged from TICK-001):** a basic edit (`PUT`) and an assignment choice submitted from a stale page are applied as submitted. The history records both changes in order. A client-supplied version/ETag would change the TICK-001 `PUT` contract and is deferred. The category-only edit carries `expectedCategory`, so an AI suggestion applied from a stale page returns 409 instead of overwriting.

After a 409 the Angular detail page shows the server message, refreshes the ticket and its activity (so buttons match the real status), keeps unsaved edit-form values and a typed escalation reason, and lets the user retry. Buttons are disabled while a request is in flight. After a 404 on an action (e.g. the ticket was reassigned) the page drops the stale record and shows "This ticket is no longer available to you. It may have been reassigned."

## 9. API

KB-001 additions: `GET/POST /tickets/{id}/knowledge-articles` and `DELETE /tickets/{id}/knowledge-articles/{articleId}` link Published Knowledge Base articles to a ticket. They use the same ticket scope as every other ticket endpoint and never change the ticket (status, assignment, SLA, `UpdatedAtUtc`, activity). `GET /tickets/{id}` is unchanged.

SLA-001 additions: `POST /tickets/{id}/first-response`, `GET /tickets?slaBreached=true|false`, an `sla` object on every ticket response (null = not tracked) and `isAutomatic` on activity entries.

| Method | Route | Auth | Success / errors |
|---|---|---|---|
| GET | `/api/v1/tickets` | SuperAdmin, Agent | 200 paged |
| POST | `/api/v1/tickets` | SuperAdmin, Agent | 201 + Location; 400 unknown customer / invalid assignee |
| GET | `/api/v1/tickets/{id}` | SuperAdmin, Agent | 200; 404 unknown or not visible |
| PUT | `/api/v1/tickets/{id}` | SuperAdmin, Agent | 200; 400; 404; 409 (overlapping write) |
| PUT | `/api/v1/tickets/{id}/category` | SuperAdmin, Agent | 200; 400; 404; 409 (category changed / overlapping write) |
| PUT | `/api/v1/tickets/{id}/assignee` | **SuperAdmin** | 200; 400 invalid assignee; 403; 404; 409 |
| POST | `/api/v1/tickets/{id}/transitions` | SuperAdmin, Agent (assigned) | 200; 400 missing/unknown status; 404; 409 stale/illegal |
| POST | `/api/v1/tickets/{id}/escalation` | SuperAdmin, Agent (assigned) | 200; 400 reason; 404; 409 already escalated / Resolved / Closed |
| DELETE | `/api/v1/tickets/{id}/escalation` | **SuperAdmin** | 200; 403; 404; 409 not escalated |
| GET | `/api/v1/tickets/{id}/activity` | SuperAdmin, Agent (assigned) | 200 paged; 400 paging; 403 Customer; 404 |

- **List:** CUST-001 paging (`page` one-based, `pageSize` default 20, clamped to 100) plus `search` (subject contains, or exact ticket GUID), `customerId`, `status`, `category`, `priority`, `escalated`. Order `CreatedAtUtc DESC, Id DESC`. Response `{ items, page, pageSize, totalCount }`.
- **Create request:** `customerId`, `subject`, `description`, `category` (required), `priority` (optional, default Medium), `assignedAgentUserId` (SuperAdmin only).
- **Update request:** `subject`, `description`, `category`, `priority`.
- **Transition request:** `fromStatus`, `toStatus`. **Escalate request:** `reason`. **Assignee request:** `assignedAgentUserId` (an empty body binds to `null` = unassign; the Angular control always sends the field).
- **Response:** ticket fields plus `customerName`, `customerEmail`, `createdByName`, `assignedAgentName`, and (TICK-002, additive) `isEscalated`, `escalationReason`, `escalatedAtUtc`, `escalatedByUserId`, `escalatedByName`. No Identity secrets.
- The active-Agent dropdown reuses the SuperAdmin-only `GET /api/v1/agents` (only SuperAdmin sees the selector); customer selection reuses `GET /api/v1/customers?search=`.

## 10. Frontend

`features/tickets/` was extended in place. The detail page shows role-aware **Workflow** actions (Start progress / Resolve / Close / Resume / Reopen, Escalate with reason, Clear escalation for SuperAdmin), a SuperAdmin-only **Assignment** card, the unchanged **Edit** card, and a paged **Activity** section (loading, Load more, error with Retry). The list has In Progress/Resolved/Closed statuses with tones, an "Escalated" badge and an Escalation filter.

## 11. Migration impact

`AddTicketWorkflowAndActivity` is additive: five new `Tickets` columns (`IsEscalated` default `0`, three nullable escalation columns, `Version rowversion`), one FK + index for `EscalatedByUserId`, and the new `TicketActivities` table with `Restrict` FKs and indexes. No existing column is altered, no row is inserted or updated, and the `Status` column needs no change (names in `nvarchar(20)`). Existing tickets keep every value and start with no history. Rolling back drops `TicketActivities` (all history is lost) and the new columns; first reset any `InProgress`/`Resolved`/`Closed` tickets to `Open` or the TICK-001 code cannot read them.

## 12. Future stories

Knowledge Base shipped in `KB-001`: see [knowledge-base.md](knowledge-base.md).

SLA & automation shipped in `SLA-001`: see [sla-and-automation.md](sla-and-automation.md).

- **Customer Portal:** customer self-service submission and tracking.
- **SLA & Automation:** SLA targets, rule/workload-based assignment, notifications.
- **Deferred:** attachments, escalation levels and notifications, auto-close, configurable workflows/custom statuses, ticket deletion/merge/bulk actions.
- **Never:** Agent self-claim or Agent-to-Agent transfer.
- **DASH-002:** internal discussion and collaborators are a separate surface ([agent-productivity-and-collaboration.md](agent-productivity-and-collaboration.md)); they never change tickets, Ticket Activity or ticket visibility.
- **Agent Dashboard (DASH-001):** read-only consumer of this module — see [agent-dashboard.md](agent-dashboard.md). It adds no ticket operations.
