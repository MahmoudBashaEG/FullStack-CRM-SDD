# Agent productivity and collaboration (DASH-002)

Tasks and in-app reminders, quick replies and team collaboration on top of the Agent Dashboard ([agent-dashboard.md](agent-dashboard.md)). No outbound communication, push, email, SMS, background scheduler or WebSocket exists.

## 1. Decisions (all approved)

| # | Decision | Status |
|---|---|---|
| D1 | **Task ownership:** one active Agent owns a task. Agents create and manage their own; SuperAdmin creates for, manages and reassigns to any active Agent. | **Approved** |
| D2 | **Quick-reply governance:** Agents own *Personal* templates; SuperAdmin owns *Shared* templates (Agents read/copy only). Scope is derived from the role and never changes. | **Approved** |
| D3 | **Collaborators (Option B):** the ticket's current primary Agent manages invitations on their own assigned ticket; SuperAdmin oversees any ticket. A collaborator can only use the discussion surface. | **Approved** |
| D4 | **Reminders (Option B):** persistent in-app reminders, including *missed* ones delivered on the next sign-in. | **Approved** |
| D5 | **Retention:** internal discussion messages are append-only (no edit, no delete). | **Approved** |

## 2. Role and route matrix

| Operation | SuperAdmin | Agent | Customer |
|---|---|---|---|
| Create task | for a required active Agent | for self only | 403 |
| Read / edit / complete / reopen / archive / restore task | all | own (others 404) | 403 |
| Change task owner | to an active Agent | policy 403 | 403 |
| Schedule / cancel reminder | any task | own tasks | 403 |
| Reminder inbox | 403 | items of tasks they currently own | 403 |
| Shared templates | create / edit / archive / restore / read / copy | read / copy active only (edit 403) | 403 |
| Personal templates | never visible (404) | own only | 403 |
| Read / post discussion | any ticket | primary assignee, or active collaborator on an **assigned** ticket | 403 |
| Invite / remove collaborators | any ticket | primary assignee of that ticket (collaborator-only 403, others 404) | 403 |

| Method | Route | Policy | Success |
|---|---|---|---|
| GET / POST | `/api/v1/tasks` | Staff | 200 / 201 |
| GET | `/api/v1/tasks/{id}`, `/api/v1/tasks/linkable-tickets` | Staff | 200 |
| PUT | `/api/v1/tasks/{id}` | Staff | 200 |
| PUT / DELETE | `/api/v1/tasks/{id}/ticket-link`, `/reminder` | Staff | 200 |
| POST / DELETE | `/api/v1/tasks/{id}/completion`, `/archive` | Staff | 200 |
| PUT | `/api/v1/tasks/{id}/assignee` | SuperAdmin | 200 |
| POST | `/api/v1/notifications/check` | Agent | 200 |
| GET | `/api/v1/notifications` | Agent | 200 |
| PUT | `/api/v1/notifications/{id}/read` | Agent | 204 |
| GET / POST | `/api/v1/quick-replies` | Staff | 200 / 201 |
| GET / PUT | `/api/v1/quick-replies/{id}` | Staff | 200 |
| POST / DELETE | `/api/v1/quick-replies/{id}/archive` | Staff | 200 |
| GET | `/api/v1/collaborations` | Agent | 200 |
| GET | `/api/v1/collaborations/{ticketId}` , `/messages`, `/collaborators`, `/eligible-agents` | Staff (access evaluated per ticket) | 200 |
| POST | `/api/v1/collaborations/{ticketId}/messages`, `/collaborators` | Staff | 201 |
| DELETE | `/api/v1/collaborations/{ticketId}/collaborators/{agentUserId}` | Staff | 204 |
| GET | `/api/v1/agent-dashboard/tasks` | Agent | 200 |

Status rules: out-of-scope records whose existence must not leak → **404**; visible but not modifiable → **403** (`ForbiddenException`); invalid field → **400**; state conflict, stale version or duplicate active membership → **409**; missing `If-Match` → **428**; commit outcome unknown → **503**.

## 3. Tasks

Fields: title (≤ 200), description (≤ 2000), status `Pending`/`Completed`, owner, creator, optional due time, optional ticket link, optional single reminder, archive flag. Tasks are never hard-deleted. Repeating complete / reopen / archive / restore or re-saving identical values is a no-op (200, `updatedAtUtc` and version unchanged). Editing an archived task is 409.

**Ticket link.** Optional; the ticket must be **currently assigned to the task owner**, checked inside the transaction from the row-locked ticket (`ITicketAccessLock`). Collaborator membership never satisfies it. After the ticket is reassigned the link is never auto-changed: `linkedTicketState` is computed in SQL as `None`, `Accessible` or `Inaccessible`. For an Agent an `Inaccessible` link returns no ticket id or subject ("Linked ticket no longer accessible"); SuperAdmin always gets both plus the state. The link only changes through `PUT`/`DELETE /tasks/{id}/ticket-link`. `GET /tasks/linkable-tickets` is a display aid (≤ 10 tickets of the owner); the locked check stays authoritative.

**Reassignment (SuperAdmin).** Target must be an active Agent. If the link is kept the ticket must be assigned to the new owner, otherwise 409 unless `removeTicketLink: true`.

**Urgency** (server UTC, Pending and unarchived only): *Overdue* `due < now`; *Due soon* `now ≤ due ≤ now + 7 days`; no due date is neither. Default order: no due date last, due ascending, id. Default list view: Pending, unarchived. The dashboard section (`GET /agent-dashboard/tasks`) returns counts over all such tasks plus the next 5.

## 4. Reminders

> SLA-001: the bell and `/notifications` inbox now also show SLA alerts (own table and endpoints, all staff); the reminder rules below are unchanged.

* One optional reminder per task, scheduled by the owner or SuperAdmin, delivered **in-app to the task's current owner only**. It must be in the future and at or before the due time. Only Pending, unarchived tasks can be scheduled; cancel is always allowed.
* `ReminderGeneration` increases whenever a *different* time is scheduled; the same time is a no-op. Complete and archive cancel a pending reminder; reopen and restore never recreate it.
* **Delivery:** `POST /notifications/check` (called on app open and every 60 s by the Agent UI bell) runs one idempotent `INSERT … SELECT` over the caller's own due tasks. It reads `AgentTasks WITH (UPDLOCK, ROWLOCK)` (so it waits for uncommitted reassignment/completion/archive and uses the committed row, required with `READ_COMMITTED_SNAPSHOT ON`) and is guarded by `NOT EXISTS (… WITH (UPDLOCK, HOLDLOCK))` plus the unique index `UX_TaskReminderNotifications_TaskGeneration (TaskId, ReminderGeneration)`. Two tabs or five parallel calls produce one row.
* **Missed:** `wasMissed = issuedAt > scheduledFor + 5 min`. It records **delayed issuance** only (no check reached the server in time: signed out, CRM closed, tab suspended, API down). It is not proof that the Agent was offline. A missed reminder is issued by the first check after the next sign-in.
* **Reassignment:** a not-yet-issued reminder follows the task (the recipient is resolved at issuance). An issued **unread** notification is transferred in the same transaction as the reassignment (same row, still unread, `TransferredFromUserId/AtUtc` recorded). A **read** one stays as the reader's history and is hidden from every inbox while the reader does not own the task.
* Racing issuance vs reassignment/completion: whichever commits first wins; no stranded or duplicated notification.
* Mark-as-read only sets `ReadAtUtc`; it never completes the task. Time: the server `TimeProvider` is the authority; the UI shows the viewer's local zone.
* **Deferred:** push, email, SMS, background delivery, recurrence, multiple reminders.

## 5. Quick replies

Plain-text templates (title ≤ 120, body ≤ 2000, category = `TicketCategory`). Agents manage their own **Personal** templates; only SuperAdmin manages **Shared** ones (Agent edit/archive → 403). Scope and owner are derived from the role at creation and immutable; the request has no scope field. SuperAdmin never sees Personal templates (404). Archived Shared templates disappear for Agents. Copy is a client action that copies exactly the stored body: **"Copies text only — does not send a message."** Text is stored and rendered verbatim (interpolation only, no HTML or variables).

## 6. Team collaboration

Per-ticket internal discussion and collaborator membership. Access is evaluated from current rows in SQL on every call: SuperAdmin → any ticket; Agent → the ticket is assigned to them (**primary**) or the ticket is assigned to someone **and** the Agent has an active membership (**collaborator**). An unassigned ticket suspends collaborator access (memberships are kept). Manage = SuperAdmin or primary.

* Collaborators use only `/api/v1/collaborations/*` and `/collaboration/tickets/:id` (subject, status, priority, assignee name). They get **404** on every `/api/v1/tickets*` endpoint, exactly as before. A collaborator is **not** an assignee.
* Invite: not yourself, not the assigned Agent, target must be an active Agent; duplicate active membership → 409. Removal records who/when; messages are never touched; re-inviting adds a new row.
* Messages are append-only, 1–2000 characters, attributed and paged newest first. Posting works on any ticket status and never changes `Tickets.UpdatedAtUtc/Version` or Ticket Activity.
* If the ticket is reassigned the former primary loses collaboration access unless separately invited; the new primary can manage existing collaborators.

| | Ticket Activity | Internal discussion | Task state |
|---|---|---|---|
| Content | system-recorded ticket changes | free-text staff messages | an Agent's own work item |
| Mutable | append-only | append-only | editable |
| Visible to | SuperAdmin, assigned Agent | SuperAdmin, primary, collaborators | owner, SuperAdmin |

## 7. Persistence

Five tables, all FKs `Restrict`: `AgentTasks` (with `Version rowversion`), `TaskReminderNotifications` (bigint identity), `QuickReplyTemplates` (check constraint `CK_QuickReplyTemplates_Owner`), `TicketCollaborators`, `TicketInternalMessages` (bigint identity). Named indexes: `UX_TaskReminderNotifications_TaskGeneration` (dedup), filtered `UX_TicketCollaborators_ActiveMembership` (`WHERE RemovedAtUtc IS NULL`), filtered `(AssignedAgentUserId, RemindAtUtc) WHERE RemindAtUtc IS NOT NULL`.

* **Optimistic concurrency (ETag / If-Match):** every task response carries `version` and a strong `ETag`; every task mutation requires `If-Match` with that value (missing 428; `*`, weak, several values, non-base64 or not 8 bytes → 400; stale → 409). A write between load and save is caught by EF's `WHERE [Version] = @original` → 409.
* **Unique violations:** `EfUnitOfWork.KnownUniqueIndexes` maps only `UX_TicketCollaborators_ActiveMembership` to 409; every other unique violation is a logged 500.
* **Write-time authorization:** post message, invite, remove, create task with link, set link and reassign-with-link are `ITransactionalCommand`s. `ITicketAccessLock` reads the ticket row (and a membership key) `WITH (UPDLOCK, HOLDLOCK, ROWLOCK)` and the handler decides from those values before writing. Plain reads never block under `READ_COMMITTED_SNAPSHOT`, so the hints are mandatory. Lock order: `Tickets` → `TicketCollaborators` → `AgentTasks`/new rows.
* **Retries:** each transactional attempt starts from a cleared change tracker and re-runs the whole handler (deadlock 1205 is retried and verified to give a single transfer). The commit is never replayed: a failure during commit becomes `CommitOutcomeUnknownException` → **503** "The change may have been saved. Reload before trying again." Raw SQL / `ExecuteUpdate` are not assumed to be retried by EF; the two idempotent autocommit statements (`IssueDueAsync`, `MarkReadAsync`) are wrapped in an explicit execution strategy, and the next 60-second check is the final retry. `TransferUnreadAsync` and the lock queries retry only as part of a whole attempt.
* Manual SQL that touches filtered-index tables needs `sqlcmd -I` (`QUOTED_IDENTIFIER ON`).

## 8. Frontend

Routes: `/tasks`, `/tasks/new`, `/tasks/:id` (SuperAdmin, Agent); `/notifications` (Agent); `/quick-replies`, `/quick-replies/new`, `/quick-replies/:id` (SuperAdmin, Agent); `/collaboration`, `/collaboration/tickets/:id` (Agent). Navigation: Tasks, Quick Replies, My Collaborations; the notification bell sits in the toolbar. The bell is the **only** interval in the app (60 s, `exhaustMap`, Agent session only). Ticket Details gains Internal discussion, Collaborators and Quick replies cards. Task pages send the displayed `version` as `If-Match`, reload on 409/503 and keep typed input. `datetime-local` values are converted to ISO UTC at the UI edge.

## 9. Migration impact

One additive migration `AddAgentProductivityAndCollaboration` (five `CREATE TABLE`s, no change to existing tables or rows). Rollback: `dotnet ef database update AddTicketWorkflowAndActivity` drops the five tables (all tasks, reminders, templates, memberships and messages are lost), then `dotnet ef migrations remove`.
