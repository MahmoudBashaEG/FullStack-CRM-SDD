# Agent Productivity & Collaboration — Story Intake

## Feature

- **Feature name (display):** Agent Dashboard — Productivity & Collaboration
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker

- **Tracker type:** `none`
- **Work item id:** `DASH-002`
- **Work item type:** `Business Feature Story`
- **Story number:** `07` (subject to checking the repository's current plan index)
- **Status:** `Final intake — all business decisions D1–D5 approved by the user (D3 Option B: Agent-managed invitations; D4 Option B: actual in-app reminders); planned as Story 07`
- **Assignee:** `Unassigned`
- **Labels:** `agent-dashboard, task-management, in-app-reminders, quick-replies, team-collaboration, internal-ticket-discussions, ticket-collaborators, authorization, angular, aspnet-core, cqrs, ef-core, squad-kit`

## Title

Agent Productivity & Collaboration — Tasks, Reminders, Quick Replies & Team Discussions

## Description

### Goal

Complete the three **remaining Agent Dashboard capabilities** in one cohesive Story 07: **Tasks & Reminders**, **Quick Replies**, and **Team Collaboration**, integrated into the already delivered **DASH-001 — Agent Dashboard: Ticket Overview & Work Queue**. Users should be able to manage actionable work, receive persistent in-app task reminders, reuse internal response templates, and collaborate on a ticket without introducing customer-facing messaging or weakening established ticket security.

**Business source:** The original *Customer Support CRM — Core Features* lists five Agent Dashboard areas: assigned tickets, customer information, tasks and reminders, quick replies, and team collaboration. `DASH-001` delivers the first two; `DASH-002` delivers the remaining three. The original source is a **high-level feature list** and does **not** specify ownership, permissions, collaborator access, task reminders, message delivery or template governance. **The user selected D3 Option B (assigned-Agent-controlled collaborator invitations with SuperAdmin oversight) and D4 Option B (real in-app task reminders, including missed reminders on sign-in).** The user subsequently also approved D1 (task ownership), D2 (quick-reply governance) and D5 (discussion retention); the workflow rules below operationalize all five approved decisions.

### 1. Existing application and non-regression boundaries

- Prerequisite stories: `ARCH-001`, `AUTH-001`, `CUST-001`, `TICK-001`, `TICK-002`, `DASH-001`. **Before planning, verify DASH-001 is committed** and the working tree is clean; do not assume its commit merely from an earlier recommendation.
- Extend the same .NET 10 ASP.NET Core Clean Architecture solution (`Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`), source-generated Mediator/CQRS, FluentValidation, SQL Server/EF Core, Identity/JWT authorization, Unit of Work, `/api/v1/` routes, ProblemDetails, Angular 22 standalone/Material, feature-based routing and shared design system.
- Reuse the existing CRM Customer and Ticket IDs and Identity Agent/SuperAdmin accounts. Do **not** duplicate Tickets, Customers, Identity users, workflows or activity history.
- Keep `TICK-001/TICK-002` behavior: only SuperAdmin changes ticket assignment; Agents manage only their **currently assigned** tickets; ticket status/escalation permissions remain unchanged; unassigned tickets are SuperAdmin-controlled. DASH-001's dashboard workload, two-query snapshot, top-10 queue and Agent-only access remain intact.
- The new collaborator capability is a **narrowly scoped exception for a separate collaboration read/post surface**, not a new permission to use the existing Ticket Details, Ticket List, Ticket Activity, edit, transition, escalation or assignment APIs. See §5 and the **approved D3 choice**. The current primary Agent may manage collaborators only on tickets assigned to them; SuperAdmin retains oversight.
- Planner must inspect actual committed paths, domain types, authorizations, migrations, shared API patterns and screens. Technical hints below are illustrative, not permission to assume class names or introduce unrelated infrastructure.

### 2. Roles, permissions and ownership — approved rules

| Operation | SuperAdmin | Agent | Customer |
| --- | --- | --- | --- |
| View Agent's DASH-001 My Dashboard | No (unchanged) | Own dashboard only | No |
| Create personal task | Create for a selected **active Agent** | Create for self only | No |
| Read/edit/complete/reopen tasks | All Agent tasks | **Own assigned tasks** only | No |
| Change existing task owner | Only SuperAdmin, to an active Agent | Never | No |
| Manage shared quick-reply templates | Create/edit/archive | Read/copy only | No |
| Manage personal quick-reply templates | Not another Agent's personal templates | Create/edit/archive **own** | No |
| Read/copy applicable quick replies | Shared | Shared + own personal | No |
| Invite/remove ticket collaborators | Any ticket | **Current primary assigned Agent, on own assigned ticket only** | No |
| Read/post internal ticket discussion | Any ticket | Current primary assignee, or explicitly invited active collaborator **through the limited collaboration API** | No |
| Schedule/change/cancel task reminders | Any assigned Agent task | **Own currently assigned tasks** | No |
| Receive/mark own task reminders as read | No Agent notification inbox | **Task’s current Agent owner only** | No |
| Edit ticket, change status, assign, escalate, read existing Ticket Activity | Existing TICK-001/TICK-002 rules, unchanged | Existing TICK-001/TICK-002 rules, unchanged | No |

- Every Agent ID, task owner, collaborator and template ownership decision must be enforced **on the server**. Never trust client-submitted actor/creator IDs or boolean permissions. Agent self-owned records derive their owner from authenticated claims.
- Disabled users cannot sign in or be selected as new task owners or collaborators. Preserve existing `OnTokenValidated` account checks, role guards, session behavior and staff API boundaries.
- Do not expose personal tasks, private templates or internal discussions through the Customer Portal, Customer API or public endpoints.
- **Decision status:** all five decisions are **Approved** by the user — D1 task ownership, D2 template governance, D3 Agent-managed invitations, D4 actual in-app reminders, D5 message retention. Details in §12.

### 3. Tasks & in-app reminders

#### 3.1 Task model and lifecycle

- A task is **Agent work**, not a Ticket and not a scheduled notification. Persist a stable system-generated task ID (use the repository's current ID convention); `Title` (required, trimmed, max 200), `Description` (optional, max 2,000), `AssignedAgentUserId` (required active Agent), `CreatedByUserId` (server-managed), optional `TicketId`, optional `DueAtUtc`, `Status` (`Pending`, `Completed`), `CreatedAtUtc`, `UpdatedAtUtc`, nullable `CompletedAtUtc`, and a simple archive flag or equivalent reversible archived state.
- Agent creation defaults to **Pending**, owner = authenticated Agent, and no ability to pick another owner. SuperAdmin must choose an **active Agent**; no unassigned tasks in this version. Neither actor can spoof the creator or completion timestamp.
- Agents may list/search/filter their own tasks, create, edit permitted text/due date/link, mark complete, reopen, and archive their own tasks. SuperAdmin may manage any task and reassign its owner to another active Agent. Customer has no access. No hard-delete endpoint.
- Complete sets `Status=Completed` and `CompletedAtUtc` using server UTC; reopen sets `Pending` and clears `CompletedAtUtc`. Repeated complete/reopen or unchanged edit is a no-op (no spurious `UpdatedAtUtc` bump). Archive hides a task from normal lists and Dashboard but keeps its record; allow authorized users to view archived tasks and restore them.
- Required/optional fields use bounded validation with existing 400 conventions; invalid owner/ticket references are rejected without partial changes. All writes use existing atomic Unit of Work conventions.

#### 3.2 Optional linked ticket and reassignment behavior

- A task can exist **without** a ticket. A ticket-linked task references the existing `Tickets.Id`; do not duplicate the Customer or ticket content. At task creation or link change, allow linking only if the task's current owner is the **primary currently assigned Agent** of that ticket. A SuperAdmin creating/reassigning a task must obey that same owner-ticket relationship; this avoids assigning a task whose owner cannot open its linked ticket.
- Task ownership is **independent of ticket ownership**. If the linked ticket is subsequently reassigned, do **not** automatically transfer the task, change its status, or delete it. The former task owner still controls their task; API/UI must **suppress restricted ticket subject/customer details and the ticket link** until the current owner becomes eligible again. Show “Linked ticket no longer accessible” and allow the task to be unlinked, completed or archived. SuperAdmin can see the existing link and resolve the mismatch.
- If SuperAdmin changes a task's owner while the linked ticket is not currently assigned to the proposed owner, reject the change **unless the link is removed in the same operation**. Do not silently move tasks or reveal ticket details.
- An invited collaboration participant is **not** equivalent to the primary ticket assignee for task-link authorization.
- Existing ticket transitions, ticket Activity and `UpdatedAtUtc` are **not changed by task updates**. Task events are not inserted into TICK-002's history as if they were ticket edits.

#### 3.3 Task due dates, reminder scheduling, and notification lifecycle (D4 — in-app notification approved)

**Business distinction:** A task describes **what work** an Agent must do, its optional `DueAtUtc` says **when that work is due**, and an optional reminder `RemindAtUtc` tells the CRM **when to notify the Agent**. Being overdue is a task state, not proof a reminder was scheduled. An Agent can have a task without a reminder.

**Who creates and receives reminders (operational rule):**
- When an **Agent creates a task for themselves**, they may optionally enable **Remind me** and choose a reminder date/time. The Agent can also add, reschedule or cancel a reminder later while they still own the task. Agents cannot schedule reminders for another Agent's tasks.
- When **SuperAdmin creates a task for an active Agent**, they may optionally select a reminder time on that task, or modify/cancel the reminder later. This targets **the assigned Agent**, **not** SuperAdmin and not every collaborator. SuperAdmin can inspect reminder metadata while managing the task but does not automatically receive that Agent's alerts.
- A reminder is **always linked to one task** and addressed to **the task's current assigned Agent**. The task creator, ticket customer, other Agents and invited ticket collaborators receive nothing unless they later become that task's assigned owner.
- For this story, permit **at most one active scheduled reminder per task** (no recurrence or multi-reminder sequences). Reminder is **optional and off by default**, including when a due date is set; do not silently create a reminder just because a task is overdue. The task owner or SuperAdmin explicitly selects a future local date/time and the API stores it as UTC. If the task has a due date, require the scheduled reminder **at or before** the due time; tasks without a due date may still have a reminder. Presets such as “1 hour before” or “1 day before” are optional UI conveniences, not distinct reminder entities.
- Tasks retain optional `DueAtUtc`; display due dates/reminder times in the viewer's local timezone. Never assume Cairo time, a fixed browser timezone, a workplace business calendar, or an external calendar service. Server UTC is the authority for time checks.

**Notification lifecycle — what happens at the chosen time:**
1. **Schedule:** Agent or SuperAdmin saves the pending task with an optional future `RemindAtUtc`. The task page shows the scheduled reminder; it is not a notification yet.
2. **Due:** At or after `RemindAtUtc`, if the task is still Pending, unarchived, and assigned to an active Agent, it becomes eligible for **one persistent in-app reminder notification** for its current owner. The notification includes safe task title, task link, scheduled time and readable state (unread/read), not unrestricted ticket/customer details.
3. **Agent online:** While authenticated and the CRM is open, a central notification check (e.g. approximately every 60 seconds, plus login/page load and manual Refresh) surfaces due reminders in an **in-app bell/inbox with an unread badge**, without requiring the Agent to manually open My Tasks. This is in-app behavior, not an OS/browser push notification; near-scheduled-time display is best-effort within the polling interval, not hard real-time.
4. **Agent offline:** When the CRM is closed or the Agent is signed out, the pending reminder is **not lost**. On the next authenticated app open, the server discovers any past-due unnotified reminder and presents it as **missed/due** to that current task owner. A background job is **not required** solely to generate a notification while nobody is signed in.
5. **Read:** Clicking the bell/inbox entry opens the task if still accessible; the Agent can **Mark as read**. Read notifications remain available in recent history, but leave the unread badge. A notification is **not a task completion action**; completing the task requires the explicit Complete action.
6. **Deduplication:** Each scheduled reminder generation fires at most once for its current task owner, even with two browser tabs, retries or repeated polling. The implementation must use server-side idempotency/concurrency-safe persistence, not only JavaScript timers. A genuine reschedule creates a new pending reminder generation; an ordinary page refresh does not.
7. **Change/cancel:** Rescheduling or canceling a **not-yet-triggered** reminder replaces/cancels the pending generation. Completing or archiving the task **before the reminder time** cancels pending notification delivery; reopening/restoring does not recreate an old scheduled reminder automatically. Completed/archived tasks must not continue producing new alerts. Previously issued notifications may remain in history with an accurate completed/archived state and must not masquerade as actionable pending work.
8. **Ownership change:** When SuperAdmin reassigns a task, the **future reminder follows the task to its new Agent**; the former Agent immediately loses access to that task's reminder inbox item and task context. For an already-past-due but still pending reminder, the new owner receives **at most one** missed reminder on the next check. Do not expose restricted linked-ticket customer data to the old or new owner through a reminder.

**Task urgency and list behavior (independent of notifications):**
- For Pending, unarchived tasks at a given server UTC instant, **Overdue** means `DueAtUtc < now`; **Due soon** means `now <= DueAtUtc <= now + 7 days` (inclusive); without due date neither applies. Completed/archived tasks are never counted as overdue/due soon. These are task dashboard categories, not substitutes for a manually scheduled reminder.
- Task list supports server-side paging, search by title, filter by Pending/Completed, due-state (Overdue, DueSoon, NoDueDate), archived state, and presence of a scheduled reminder, with server-side authorization and deterministic ordering. Default view = unarchived Pending tasks, ordered overdue first, then earliest due time, then no due date last, stable ID tie-breaker.
- Dashboard task preview: Pending, Overdue and Due Soon counts plus up to **5** next tasks, linked to My Tasks. Display scheduled reminder metadata and entry to **My Notifications**. Use the complete authorized task set for counts, not only the top-five preview. DASH-001's existing four ticket counts and top-10 ticket queue remain unchanged.
- Avoid false zero counts, fabricated delivery confirmations and endless alert repetition on errors. If task or notification retrieval fails, show a bounded error/Retry for that section without blanking unrelated DASH-001 metrics.

### 4. Quick Replies (internal reusable text templates)

- Deliver real management and reuse of **plain-text templates** now, without claiming to send messages. Template fields: stable ID; `Title` (required, max 120), `Body` (required, trimmed, max 2,000), `Category` (fixed existing ticket categories `General`, `Technical`, `Billing`, `Other` or equivalent reuse), `Scope` (`Personal` or `Shared`), owner ID if Personal, creator/updater IDs and UTC timestamps, and `IsArchived`.
- **Agent** can create, read, edit and archive their own **Personal** templates; can search/read/copy active **Shared** templates but cannot modify them or another Agent's personal templates. **SuperAdmin** manages Shared templates only and may read/copy them. Customer never accesses templates.
- On creation, an Agent's scope is Personal, and SuperAdmin's scope is Shared; do not permit untrusted scope/owner input to elevate Agent templates to Shared. Template scope/owner are immutable after creation. Shared templates are available to all active Agents and SuperAdmin; personal templates never leak to other Agents.
- Provide server-paged template list/search by title and body, optional category and scope filters, active by default, with an option to view/restore archived own/admin-managed templates. Archived templates do not appear in default selectors but remain stored. Prefer updating a template rather than creating a new duplicate on every edit; changes affect future copying only.
- **Use Quick Reply** means display, select and **copy** its plain-text body to clipboard from the template list or within an eligible ticket's existing details page; show success/failure feedback. The text is *not* auto-sent to a customer, posted into internal discussion, inserted into Ticket Activity, or silently edited into ticket Description. No fake Send/Reply button.
- No automatic variable substitution, AI rewriting, HTML/Markdown rendering, attachments or external communication. If placeholder-looking text is entered, treat it literally; ensure clipboard content matches the saved body. Escape text in UI, never interpret it as HTML.

### 5. Team Collaboration — internal ticket discussion with explicit participants

#### 5.1 Scope and access boundary (Decision D3 — Option B approved)

- Team Collaboration must enable **actual Agent-to-Agent participation** while preserving TICK-001/TICK-002's original ticket security model. Introduce **ticket-specific internal discussion messages** and **collaborator membership managed by the current primary assigned Agent or SuperAdmin**.
- Every ticket always has these implicit discussion participants: **SuperAdmin** and its **current primary assigned Agent** (if any). SuperAdmin **or the ticket’s current primary assigned Agent** may additionally invite active Agent collaborators to that ticket; invited Agents can use **only the collaboration read/post surface** for that ticket.
- A collaborator **who is not the current primary assignee** may read a *minimal collaboration ticket context* (`TicketId`, Subject, Status and Priority) and discussion messages through dedicated collaboration endpoints/pages. They **must not** gain access to existing `/api/v1/tickets/{id}` details, Ticket list, Ticket Activity, Subject/Description edits, transitions, escalations, task linking or assignment changes. Do not change `TicketRepository.Scope`, Agent `AgentScope` behavior or the existing Ticket APIs to treat collaborators as assignees.
- **SuperAdmin**, or **the current primary assigned Agent for that same ticket**, can invite/remove other **active Agent** collaborators. An Agent cannot invite/remove participants on an unassigned ticket, a ticket assigned to someone else, or a ticket they only access as an invited collaborator. SuperAdmin may oversee/revoke all invitations. No self-invites, implicit invitations from writing `@name`, automatic access from task links, or customer invitations. Validate target role/account on every write. Do not create redundant collaborator records for the current primary Agent; primary access already exists.
- If a ticket is **unassigned**, collaborator-only access is **suspended** (existing rule: unassigned tickets are SuperAdmin-only). Membership may remain recorded, but collaborators cannot read/post until a primary Agent is assigned again; this does not elevate them to assignee.
- After ticket reassignment, the former primary Agent immediately loses their regular Ticket Details/Activity/actions as before. They may only retain *collaboration-only* access if separately and explicitly invited as a collaborator. Reassignment does not invite or remove collaborators automatically.
- Removing collaborator membership immediately revokes collaboration read/post permissions, including direct URL access. Existing messages remain attributed to the author; never delete old messages when a collaborator is removed or disabled.
- A new collaborator can read existing internal discussion messages for that ticket. This is a **deliberate staff disclosure** limited to the invited account; do not expose Customer PII beyond the minimal context or serialize Ticket Description/Customer contact data in the collaborator-only endpoint.

#### 5.2 Messages and history

- Support a chronological, bounded/paged **internal discussion** per ticket. Each message has stable ID, ticket ID, authenticated author ID/display name, required trimmed plain-text body (1–2,000 characters), and server UTC `CreatedAtUtc`. For this story messages are **append-only**: no edit, deletion or overwrite, including by SuperAdmin.
- Authorized participants can post messages on tickets in any status, including Resolved/Closed (discussion does **not** reopen or modify ticket status). A message should not update the Ticket's `UpdatedAtUtc`, change attention-queue ordering, or create a misleading TICK-002 `TicketActivity` event. **Internal discussion history and existing Ticket Activity are separate concepts**.
- Show internal discussion directly on the normal Ticket Detail screen **only to its existing authorized viewers** (SuperAdmin/current assignee). Provide a separate **My Collaborations** list and limited `/collaboration/tickets/:id` view for invited Agents who are not primary assignees. Never route such collaborators into an unauthorized Ticket Details page.
- Expose author identity and UTC timestamps, clear **Internal only — not visible to customers** labeling, paging or Load More, loading/error/empty states, and confirmation after posting. Do not claim these are external customer conversations, SMS, email or WhatsApp messages.
- Collaborator additions/removals should have retained created/removed metadata (by actor and UTC) or explicit membership-event records so access decisions can be explained; planner chooses a lightweight persistence strategy. A message's author must be retained even if their account is later disabled. No system-generated chat impersonation.
- Do not implement real-time chat sockets, typing indicators, presence, file uploads, **collaboration-message notifications**, group channels, team inboxes, customer messaging, mentions with notifications, or full-text conversation search. Task reminder notifications are independently in scope under §3.3.

### 6. Persistence, consistency and compatibility

- Add **one reviewed, additive EF Core migration** for the minimum new persisted concepts: Agent Task, Task Reminder schedule/notification delivery state (as columns and/or a separate notification record), Quick Reply Template, Ticket Collaborator membership and Ticket Internal Message (or equivalent well-justified names). Keep existing tables, data and prior migration files intact; do not run `EnsureCreated`, drop/recreate or backfill fake discussion entries.
- Use existing Identity user IDs and existing Ticket IDs as real FKs where appropriate. Use restrictive delete behavior, validated role/ownership checks and useful indexes for task owner/status/due date, scheduled reminder eligibility and unique notification generation, notification owner/read state, template scope/owner/category, active collaborator membership, and message ticket/time pagination. Avoid duplicating Identity or Customer tables.
- Persist task/template/membership/message changes transactionally using existing EF Core/Unit of Work conventions. Post and membership changes must not leave partial writes. Favor append-only messages; archive tasks/templates instead of hard deletion.
- New collaboration authorization queries must validate **current** primary assignment/membership in SQL or equivalent server-side scoped access per operation, including the Agent’s right to invite/remove on their currently assigned ticket; do not rely on client state or reuse a stale authorization snapshot. Reminder discovery and notification reads/writes likewise must be scoped to the **current task owner**.
- Respect any existing rowversion/optimistic concurrency conventions where modifying entities that use them; reject conflicting operations instead of silently generating partial records. Never weaken TICK-002's ticket concurrency semantics. No new background infrastructure or messaging broker.
- Preserve all existing ticket, customer, history, refresh-session, task-independent and dashboard data. Migration verification records pre/post counts of existing entities, reviews generated SQL and confirms ticket behavior continues to work.

### 7. API / application use cases

- Use separate typed Application CQRS commands/queries and `/api/v1/` REST resources for task CRUD-like operations, task completion/reopen/archive, reminder scheduling/rescheduling/canceling and scoped notification inbox/read, quick-reply management, collaborator invitation/removal/listing, and internal discussion paging/posting. Use the existing error mapping, validation responses and authorization policy naming style. Exact route names and verbs are planner decisions after reading committed code.
- Provide read-only task summary and preview for integration with the existing Agent-only dashboard; **do not change the semantics of DASH-001's existing four counts/top-10 ticket queue**. The planner may add a separate Agent productivity endpoint/service to avoid breaking `GET /api/v1/agent-dashboard` consumers.
- An Agent cannot send `assignedAgentUserId`, `scope=Shared`, `createdBy`, `authorId`, `isAdmin`, etc. to escalate permissions. Where such fields are valid for SuperAdmin, independently validate the authenticated role and target active Agent.
- Record ownership/invite changes and handle absent/inaccessible resources consistently: return 404 for out-of-scope Agent records where their existence must not leak, 403 for disallowed role endpoints, 400 for invalid fields, and 409 for documented invalid state/conflict transitions. No partial modification on rejection.
- Paginate potentially growing task/template/message lists and apply filtering, authorization, sorting and limits **in SQL**, not after loading all records into Angular. Use the same page conventions as prior stories.
- Keep frontend DTOs safe and minimal: avoid password hashes, token metadata, full customer profiles, and cross-agent tasks/templates. Do not include message contents in external Customer-facing responses.

### 8. Angular UI / navigation

- Extend **My Dashboard (`DASH-001`)**, not replace its cards or ticket queue. Add Tasks preview (counts Pending/Overdue/Due Soon, next five tasks), plus visible entry points to **My Tasks**, **My Notifications**, **Quick Replies**, and **My Collaborations**. Preserve existing role-based navigation and a meaningful view for SuperAdmin to manage shared templates/assigned tasks/collaborators without granting access to Agent-only My Dashboard.
- **My Tasks:** create/edit form, optional ticket selection limited by backend permission, local due-date entry, explicit **Remind me** toggle with local reminder date/time, task list with search/filter/paging, complete/reopen/archive controls, badges for overdue/due-soon; distinguish task owner from ticket assignee. Show safe “linked ticket no longer accessible” fallback after ticket reassignment.
- **In-app reminder inbox:** Agent-only bell/badge + accessible list of unread/recent reminders, link to current task when authorized, Mark as read, missed reminder state after sign-in, safe errors/retry and no customer visibility. SuperAdmin task editor may schedule reminders for the assignee but does not receive that Agent’s reminder notifications. Use app-open/login refresh plus bounded polling while an Agent session is active; do not suggest push or offline desktop delivery.
- **Quick Replies:** list/search/category/scope controls, create/edit/archive for permitted owners, copy-to-clipboard from list and from authorized ticket details, clear label **Copies text only — does not send a message**. SuperAdmin shared-template management should be reachable from a staff/admin route, not an Agent-only route.
- **Internal ticket discussion:** readable thread and composer for SuperAdmin/current primary Agent on existing Ticket Details. For invited collaborators, provide a dedicated minimal context/thread page, plus **My Collaborations** list. **SuperAdmin and the current primary Agent** see invite/remove collaborator controls on their authorized Ticket Details; collaborator-only Agents cannot manage membership. Show internal-only warning prominently.
- Use existing Angular Material, standalone feature ownership, shared typed API services, route guards, reusable UI, accessible labels, text wrapping and logical/RTL-compatible CSS. Provide loading/empty/error/retry states; avoid duplicate login/session logic and unnecessary packages.
- Refresh dashboard/task data on revisiting the page/user action; **only the Agent notification indicator/inbox** may use a bounded polling interval while the app is open to surface due reminders. No push, email/SMS, WebSocket/chat sockets, or background schedulers. Where a failed productivity read occurs, preserve accurate existing DASH-001 ticket data and show an error only for the failed section.
- No mockups are supplied. AI may choose layout details inside these explicit requirements but must not implement outbound communication or broaden ticket access.

### 9. Testing and verification — established lightweight policy

- Add **exactly one meaningful new backend happy-path unit test** and **one meaningful new frontend happy-path unit test** for Story 07, using existing frameworks and conventions; preserve all existing tests and adjust test fakes only when needed for compilation. Because three capability groups share one story, choose tests that meaningfully exercise a central end-to-end *unit-level* application/UI path; **do not silently introduce an extra unit test per feature**.
- Do **not** create automated negative-path, integration/API/SQL, E2E/browser, load or security-abuse suites, new test frameworks or mocking libraries. Use targeted **manual** API/browser checks for each of the three groups and their role boundaries.
- Build backend/frontend; run all existing + new unit tests. Review/apply the additive migration after inspecting it; confirm existing DB counts, applied migration history and schema/enum compatibility remain intact.
- Manual **Tasks & reminders**: Agent self-task with optional ticket link, due soon/overdue, complete/reopen/archive; Agent schedules a reminder for self; SuperAdmin schedules a reminder for task assignee; Agent cannot target another Agent; verify online due delivery, offline missed delivery on next sign-in, mark-read, dedup across refresh/tabs, cancel/reschedule, complete/archive suppression, reassignment recipient update and absence of customer notification. SuperAdmin creates/reassigns Agent tasks; ticket reassignment safely hides restricted context; count/preview reflect correct state.
- Manual **Templates**: Agent personal template, SuperAdmin shared template, correct search/copy text, archive/restore; one Agent cannot read another Agent's personal templates or edit shared; Customer denied; copied content never sends or changes ticket history.
- Manual **Collaboration**: SuperAdmin + assigned Agent exchange internal messages; **the current primary Agent invites** another active Agent to own ticket, and SuperAdmin may independently invite/remove; a collaborator cannot invite another; invited Agent reads/posts **only in the collaboration view**, gets 404 on original Ticket Details/Activity/actions; removal revokes access; unassignment suspends collaborator-only access; reassignment doesn't implicitly grant the former assignee access; all messages persist after refresh and are never visible to Customer.
- Manual **regression**: DASH-001 ticket counts/top-ten ordering/authorization unchanged; Ticket creation/assignment/workflow/history and Customers/Auth/Admin/Profile unaffected; no unexpected automatic notifications/messages; narrow mobile layout usable.
- Record which checks are executed and which remain for manual browser acceptance. Do not log real passwords, JWT keys, bearer tokens or secret values as test evidence. Avoid altering/deleting existing production-like dev records just to reach expected test counts.

### 10. Documentation

- Add `docs/agent-productivity-and-collaboration.md` (or equivalent) describing task lifecycle/owner/ticket-link behavior/**reminder creator/recipient, due/polling/read/missed/dedup behavior**, template governance and copy-only semantics, **Agent-managed collaborator invitations with SuperAdmin oversight**, internal-only message boundaries, role/route matrix, timezone handling, migration implications, and deferred external/push notifications.
- Update `docs/agent-dashboard.md`, `docs/ticket-management.md`, `docs/identity-and-access.md`, `docs/api-conventions.md`, `docs/persistence-and-transactions.md` if persistence conventions are extended, `docs/frontend-ui-foundation.md` for new navigation, and `README.md` as appropriate. Do not falsely claim that outbound Quick Replies or push reminders exist.
- Clearly distinguish **Ticket Activity** (TICK-002 audit timeline) from **Internal Discussion Messages** (DASH-002) and **Task history/state**. Explain that being a collaborator does not mean becoming a ticket assignee.

## Acceptance criteria

- [ ] Story 07 builds on committed DASH-001/TICK-002 without duplicating Ticket, Customer, Identity, dashboard counts or historical events.
- [ ] Roles and server-side record scoping follow §2; Customer cannot access tasks, templates or internal collaboration APIs.
- [ ] Agent creates a Pending task automatically owned by themselves; SuperAdmin can create/reassign tasks only to active Agents; Agent cannot assign/reassign another Agent's work.
- [ ] Tasks support validated title/description/optional link/optional due UTC, edit, completion/reopen, archive/restore, server paging/filtering/search and stable ordering; no hard deletion.
- [ ] Ticket-linked task creation requires that the task owner be the ticket's current primary assignee; post-reassignment task data never leaks now-inaccessible ticket/customer information and no task is silently transferred.
- [ ] Pending/Overdue/Due Soon task counts and top-five task preview are accurate across the whole Agent-owned set with correct UTC boundaries; completed/archived tasks do not count as overdue.
- [ ] Agent may schedule/change/cancel at most one optional task reminder for their own task; SuperAdmin may do the same for any Agent task; only the current task owner receives its in-app notification, never a Customer or collaborator.
- [ ] Reminders appear in the Agent notification inbox when due while signed in (bounded polling), and missed reminders appear after next sign-in; notifications persist with unread/read status, are server-side idempotent across refreshes/tabs, can be marked read, and are never mistaken for task completion.
- [ ] Completing/archiving before reminder time prevents future alerting, a task transfer moves reminder eligibility to the new Agent without exposing old task data, and rescheduling/canceling respects existing notification history and ownership.
- [ ] DASH-001's existing ticket cards and top-10 queue remain accurate and retain their current access controls; productivity section errors do not substitute fake zeros for missing data.
- [ ] Agents create/edit/archive/copy their own Personal quick replies and read/copy Shared templates; only SuperAdmin creates/edits/archives Shared templates.
- [ ] Quick replies support title, plain-text body, category, search/filter/paging and archived state; copying reproduces the body but never sends customer messages or writes ticket activity.
- [ ] SuperAdmin can invite/remove active Agents on any ticket; the **current primary Agent** can invite/remove active collaborators **only on their assigned ticket**; collaborator-only Agents cannot invite/remove. Invitations/removals are enforced by backend and documented with actor/time metadata.
- [ ] SuperAdmin and current primary Agent can read/post ticket internal messages; invited collaborators can read/post only via a separate minimal, scoped collaboration interface.
- [ ] Collaborator membership never grants existing Ticket Details, Ticket Activity, list/edit, assignment, workflow or escalation permissions; no Agent self-invite.
- [ ] When a ticket is unassigned, collaborator-only access is suspended; removed collaborators lose access immediately; reassigned former primary Agent loses ordinary ticket access unless still independently authorized.
- [ ] Collaboration messages are append-only, author/timestamp-attributed, paged, retained on reassignment, and labeled internal-only; Customer sees none and messages do not change Ticket UpdatedAtUtc or activity history.
- [ ] The new task, quick-reply, collaboration and discussion routes render responsive, accessible, permission-aware Angular UI with error/empty/loading states and existing authentication reused.
- [ ] All new data is stored by a reviewed **additive** migration with foreign keys/indexes, and existing tickets, customers, users, refresh sessions, Ticket Activity and migration history remain intact.
- [ ] No external communication, background scheduler, automatic SLA/escalation, OS/browser push notifications, team chat sockets, or unapproved ticket permissions are introduced. **In-app reminder polling and persistent notifications are intentionally in scope.**
- [ ] Backend/frontend builds and all prior unit tests pass, plus exactly one new backend and one new frontend happy-path unit test; manual role/ownership, task/reminder, quick-reply, collaboration and regression checks pass.
- [ ] Documentation records the proposed business rules as approved decisions after review, describes exact access boundaries and explicitly names deferred functionality.

## Attachments

None required. The original *Customer Support CRM — Core Features* document is already retained with the architecture story. Its Agent Dashboard requirements are restated above; planning also references the committed prior stories and existing code.

## Dependencies

- **Prerequisite story IDs:** `ARCH-001`, `AUTH-001`, `CUST-001`, `TICK-001`, `TICK-002`, `DASH-001`.
- **Next work item:** `DASH-002` / proposed Story 07, in `.squad/stories/agent-dashboard/DASH-002/intake.md`.
- **Implementation planning:** generate one Story 07 plan inside `.squad/plans/agent-dashboard/` after verifying the DASH-001 commit. Treat D1–D5 as approved (the user confirmed D1, D2 and D5 at planning time).
- **Relevant code:** existing `Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`; Agent-only dashboard endpoint/Angular feature; Tickets and Customers APIs; authorization/policies; current Ticket ownership and TICK-002 Activity; Angular navigation and UI foundation; EF migrations; backend/frontend unit test projects. Exact files and symbols to be discovered by planner against current committed repo.

## Extra notes

- A combined three-capability story is significantly larger than DASH-001. Keep all three as **one business story** as requested, but organize its future plan into distinct tasks/workstreams for Tasks, Quick Replies, Collaboration, Dashboard UI integration, docs and verification. Do not split into DASH-003/DASH-004 without user request.
- Do not implement or commit while reviewing this intake. The current primary Agent invitation authority (D3) and real in-app reminder delivery (D4) are expressly approved; D1, D2 and D5 were explicitly approved by the user at planning time.
- Reminders **do** deliver durable in-app notifications (including after missed times); they do **not** imply desktop push, email, SMS or a background delivery service. Quick Replies only copy text; do not advertise outbound sending.
- All test data must remain isolated from existing records; never expose test credentials in terminal output, logs, or source control.

### 12. Business decisions and approval status

| ID | Business rule | Decision status / rationale |
| --- | --- | --- |
| **D1 — Task ownership (APPROVED)** | Agents own/manage personal tasks; SuperAdmin can create/reassign tasks to active Agents; ticket-linked tasks require owner to be the ticket's current primary assignee. | **Approved** by the user — the source does not define ownership or ticket-reassignment behavior, so this rule is now authoritative. |
| **D2 — Quick Reply governance (APPROVED)** | Agents maintain Personal templates; SuperAdmin maintains Shared templates; using a reply copies plain text only. | **Approved** by the user — outbound Communication Channels are not implemented; copy-only use is authoritative. |
| **D3 — Cross-Agent collaboration (APPROVED: Option B)** | **Current primary Agent** may invite/remove active Agent collaborators on their own assigned ticket; **SuperAdmin** retains invite/remove oversight on any ticket. Invited users receive limited discussion-only access, not ordinary ticket rights. | The user explicitly chose Agent-controlled collaboration. |
| **D4 — Reminder semantics (APPROVED: Option B)** | **Actual persistent in-app reminder notifications** at chosen time while signed in, plus missed reminders on next sign-in; Agent can schedule own, SuperAdmin on behalf of current owner. One optional reminder per task; no push/external delivery. | The user explicitly chose in-app notification behavior. Creator, recipient and lifecycle are detailed in §3.3 as implementation defaults. |
| **D5 — Discussion retention (APPROVED)** | Internal messages are append-only, visible to authorized participants, including newly invited collaborators; no edit/delete, no Customer access. | **Approved** by the user — defines auditability and discussion confidentiality. |

## Technical hints (optional, not implementation instructions)

- Likely new persisted concepts: `AgentTask`, `TaskReminderNotification` (or reminder schedule + read/delivery state), `QuickReplyTemplate`, `TicketCollaborator`, `TicketInternalMessage`, with existing FK identities and restrictive deletes. Domain invariants/role restrictions should be enforced in Application/Domain as appropriate, not controller/UI only.
- Separate CQRS features and repositories for Tasks, Quick Replies and Collaboration. New read-only productivity summary endpoint rather than changing `GET /api/v1/agent-dashboard` semantics unless the actual code offers a backward-compatible additive design with clear justification.
- Prefer bounded list retrieval and server-side aggregate queries; indexes should follow the actual WHERE/ORDER BY patterns. Avoid broadening `TicketRepository.Scope` to include collaborators.
- Use server UTC `TimeProvider` and convert dates at the UI edge; base due-reminder eligibility on server time and persist notification issuance/read state idempotently. Persist template/message content as ordinary text; encode output and never render untrusted HTML.
- Preserve existing source-generated Mediator conventions, `IUnitOfWork`, Identity/JWT policies, `ProblemDetails`, typed DTOs and Angular Material lazy-loaded feature pattern.

## Out of scope

- Sending email, SMS, WhatsApp, live-chat, web-form messages or customer-facing replies; Communication Channels belongs to a later module.
- OS/browser push, email and SMS reminder delivery, always-on background reminder scheduler, recurring tasks or multiple simultaneous reminders per task, calendar synchronization, notification subscriptions, SLA due dates, auto-escalation or auto-routing. **In-app notification inbox and bounded polling are in scope.**
- Team group chat channels, video/audio, live typing/presence, attachments, broadcast notifications, mentions that grant permission, rich-text editors, full-text search and message moderation.
- New arbitrary ticket permissions, Agent self-claim/transfer, collaborator ability to edit/manage tickets, Customer Portal access to staff tasks/templates/messages, Ticket Activity fabrication or rewriting.
- AI replies, automatic template personalization, knowledge-base integration, business reporting/agent performance analytics, multi-department/branch ownership and configurable permission designer.
- Automated API/integration/E2E/negative-path/security-abuse/performance suites, new test frameworks, packages or infrastructure not needed for Story 07.
