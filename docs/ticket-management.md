# Ticket management (TICK-001)

Ticket creation, creation-time assignment and basic management for staff. Post-creation assignment, status workflow, escalation and history belong to TICK-002 (see section 6).

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
| `AssignedAgentUserId` | nullable FK → `AspNetUsers.Id`; set at creation only |
| `CreatedAtUtc` / `UpdatedAtUtc` | UTC |

All FKs are `Restrict`. Customer, creator and agent names are joined at read time, not copied onto the ticket. Tickets are never deleted. Indexes: `(CreatedAtUtc DESC, Id DESC)`, `(AssignedAgentUserId, CreatedAtUtc DESC, Id DESC)`, `(CustomerId, CreatedAtUtc DESC, Id DESC)`, `CreatedByUserId`.

## 2. Fixed sets

- **Category:** `General`, `Technical`, `Billing`, `Other`.
- **Priority:** `Low`, `Medium` (default), `High`, `Urgent` — chosen by staff, never calculated.
- **Status:** `Open` only. Set by the server on creation; there are no transitions in TICK-001.

Stored and serialized by name (a global `JsonStringEnumConverter` rejects integers; query-string filters bind by name). Changing a set is a code change plus a doc update; there is no admin UI.

## 3. Creation-time assignment

| Creator | Customer | Initial assignee |
|---|---|---|
| Agent | existing CRM customer | **the creating Agent**. A submitted `assignedAgentUserId` equal to the caller is harmless; any other value ⇒ 400 |
| SuperAdmin | existing CRM customer | a selected **active Agent**, or none (unassigned). Disabled Agents, SuperAdmin, Customer accounts and unknown ids ⇒ 400 |
| Customer | — | not allowed (403) |

The creator always comes from the token. Agent self-assignment is not workload routing. An unknown `customerId` ⇒ 400.

## 4. Visibility and editing

- SuperAdmin: lists, views and edits every ticket (including unassigned ones).
- Agent: lists, views and edits **only tickets assigned to them**. Unassigned tickets are not visible to Agents.
- Customer: no access.

Enforcement: the `Staff` policy, a `TicketActor` built from the validated JWT (`sub` + role) and an `agentScope` applied first in every SQL query. A ticket outside the caller's scope returns 404 (existence is not revealed). Create/update read-backs use the same scope. Angular guards and menus are usability aids only. Concurrent edits: last write wins.

`PUT` changes only `subject`, `description`, `category`, `priority` (+ `UpdatedAtUtc`).

## 5. API

| Method | Route | Auth | Success / errors |
|---|---|---|---|
| GET | `/api/v1/tickets` | SuperAdmin, Agent | 200 paged |
| POST | `/api/v1/tickets` | SuperAdmin, Agent | 201 + Location; 400 unknown customer / invalid assignee |
| GET | `/api/v1/tickets/{id}` | SuperAdmin, Agent | 200; 404 unknown or not visible |
| PUT | `/api/v1/tickets/{id}` | SuperAdmin, Agent | 200; 404 unknown or not visible |

- **List:** CUST-001 paging (`page` one-based, `pageSize` default 20, clamped to 100) plus `search` (subject contains, or exact ticket GUID), `customerId`, `status`, `category`, `priority`. Order `CreatedAtUtc DESC, Id DESC`. Response `{ items, page, pageSize, totalCount }`.
- **Create request:** `customerId`, `subject`, `description`, `category` (required), `priority` (optional, default Medium), `assignedAgentUserId` (SuperAdmin only).
- **Update request:** `subject`, `description`, `category`, `priority`.
- **Response:** ticket fields plus `customerName`, `customerEmail`, `createdByName`, `assignedAgentName`. No Identity secrets.
- The active-Agent dropdown reuses the SuperAdmin-only `GET /api/v1/agents` (only SuperAdmin sees the selector); customer selection reuses `GET /api/v1/customers?search=`.

## 6. Future stories

- **TICK-002:** post-creation assignment/reassignment (SuperAdmin only), assigning unassigned tickets, status workflow (Resolve/Close/Reopen…), manual escalation, activity history.
- **Customer Portal:** customer self-service submission and tracking.
- **SLA & Automation:** SLA targets, rule/workload-based assignment, notifications.
- **Never:** Agent self-claim or Agent-to-Agent transfer.
