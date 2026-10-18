# Story 09 — SLA & Basic Automation — Business Feature Intake

## Feature

- **Display name:** SLA & Automation — Policies, Calendars, Rules, Scheduling and Alerts
- **Feature slug:** `sla-automation`
- **Work item ID:** `SLA-001`
- **Work item type:** `Business Feature Story`
- **Story number:** `09` (confirm against current Squad-Kit index)
- **Tracker:** `none` (training project)
- **Status:** `Business scope and decisions S1–S12 approved — ready for Squad-Kit planning`
- **Labels:** `sla, service-level, business-hours, holidays, workflow-rules, background-service, auto-assignment, escalation, notifications, aspnet-core, angular, squad-kit, training`

## Title

SLA & Basic Automation — Targets, Calendars, Workflows, Background Jobs, Assignment, Escalation and Notifications

## 1. Business goal and source

Turn the existing ticket-management CRM into a system that can **measure first-response and resolution performance**, **automatically route new tickets**, **detect overdue work**, **escalate tickets**, and **notify staff**. Give SuperAdmin straightforward screens for changing basic SLA and automation settings without modifying code.

The original *Customer Support CRM — Core Features* document requires under **SLA & Automation**: (1) response and resolution targets, (2) automatic assignment, (3) escalation rules, (4) alerts and notifications. The user additionally **explicitly requested that basic versions of SLA calendars, a configurable workflow/rule engine, and background job processing be included now**, not postponed. These supporting components are therefore **required**, not optional technical extras.

**Scope principle:** All nine capabilities below must work end-to-end; *basic* means limited configuration options, simple workflows, a single-process hosted worker, and reuse of existing CRM abstractions — **not** omitted capabilities or placeholder UI.

### Required capabilities (all nine)

1. Configurable first-response and resolution **SLA targets** by existing ticket priority.
2. **SLA business-hours calendar**, configurable weekdays, opening/closing hours, timezone and excluded holiday dates.
3. A **basic configurable workflow/rules engine** (supported trigger → condition → action types).
4. A **background scheduler/worker** that checks deadlines and executes due automations.
5. **Automatic assignment** of newly created tickets by configurable rules.
6. **Automatic SLA escalation** based on configured breach rules.
7. Persistent **in-app alerts/notifications**, including deadline warnings and breaches.
8. **SLA monitoring** in the ticket UI (deadlines, remaining business time, met/breached/awaiting response).
9. **SuperAdmin administration screens** to configure policy, calendar, supported automation rules, and view rules/status.

## 2. Existing application and non-regression requirements

- Work **inside** the implemented monorepo and reuse `ARCH-001`, `AUTH-001`, `CUST-001/CUST-002`, `TICK-001/TICK-002`, `DASH-001/DASH-002`; do not build a separate application, ticket model, auth system or notification stack.
- Backend: existing .NET 10 ASP.NET Core Clean Architecture (`Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`), source-generated Mediator/CQRS, FluentValidation, EF Core + SQL Server/LocalDB, JWT/Identity roles and established `/api/v1/` conventions. Frontend: Angular 22 standalone/Material, existing navigation, auth guard, typed APIs and styling.
- Inspect the **actual committed implementation** before planning: current priority/category/status enums, ticket creation and assignment/escalation authorization, timestamps/activity history, `Agent Dashboard`, existing **task-reminder notifications and inbox**, persistence/migration conventions, and customer visibility. Do not guess API/class names.
- Preserve existing ticket assignment and action boundaries. Per current stories, only SuperAdmin may **manually** assign/reassign tickets; Agents can work on tickets assigned to them; collaborator-only access is not ordinary ticket management access. Automated assignment is a **server-owned system action**, not an elevation of Agent manual permissions.
- Keep the future **Communication Channels module last**. This story must not send emails, WhatsApp, SMS or live-chat messages, or falsely report a customer received a response.
- Existing CUST-002 customer notes/interactions are not proof of a response and are not altered by this feature.

## 3. Roles and authorizations

| Operation | SuperAdmin | Agent | Customer |
| --- | --- | --- | --- |
| View SLA metrics on any ticket | Yes | Only already-accessible assigned tickets | No new access |
| Mark a ticket's **first customer response** (manual tracking) | Yes | Only their currently assigned ticket | No |
| View personal SLA notifications | Own staff notifications | Own assigned-ticket notifications | No |
| View all breached/at-risk tickets for management | Yes | Only tickets already authorized | No |
| Configure SLA policies/calendars | Yes | No | No |
| Create/edit/disable supported automation rules | Yes | No | No |
| Manually reassign a ticket | Existing TICK-002 rights | Existing TICK-002 rights | No |
| Cause automated assignment/escalation | Via enabled system rules | No rule-management permission | No |

Authorization is enforced by the backend on every query/command. Staff notifications must not leak details of tickets the recipient cannot access. Customer-role users cannot call the new staff endpoints. Preserve existing ticket activity/history, including correct attribution of **system** changes rather than impersonating an Agent.

## 4. A — Configurable SLA targets

- SuperAdmin can configure one **active global SLA policy**, with first-response and resolution targets for **each priority actually supported by the existing Ticket entity**. Each target is a positive whole number of *business minutes* (UI may present convenient hours/minutes).
- When a **new ticket** is created, use its priority and active policy/calendar to calculate and **persist** response and resolution deadlines and the policy/calendar information needed for historical interpretation. Show an explicit "No SLA configured" state if settings are unavailable; do not fabricate deadlines.
- Both targets begin at the ticket creation time. **First response** is measured when an authorized staff member explicitly clicks **Mark as Responded**; record the authenticated actor and server UTC timestamp. The action is internal tracking only and **does not send a message**. Only the first recorded response counts; later clicks must not reset the response metric.
- **Resolution** is measured using the existing successful ticket transition to its resolved terminal state (inspect actual statuses). Reuse the existing workflow and timestamps; do not introduce a second conflicting status machine.
- For each tracked target show at least: target duration, due-at, elapsed/remaining business time, `Pending / Met / Breached` (and `Not configured` if appropriate), and actual first-response/resolution time when present. A recorded response or resolution after the deadline is still marked **Breached**, with the actual time retained.
- Staff may continue managing breached tickets; SLA breach is a measurement, not a prohibition on resolution.
- Policy edits must not silently rewrite historical measurements: **approved S4** = policy changes affect **new tickets only**; tickets already tracked retain their originally calculated due timestamps. Existing tickets predating this story remain intact and may show "Not tracked" unless a separate explicitly approved initialization workflow is chosen.
- No manual user-provided response timestamp or retroactive first-response backdating. For this story, no multi-stage response confirmations or message-provider integration.

## 5. B — Business-hours calendar and holidays

- SuperAdmin manages one global **business calendar** with: named timezone (time-zone-aware, no hardcoded UTC offset); enabled working weekdays; one common opening and closing local clock time on working days; explicit **holiday dates** that are excluded entirely.
- SLA due times are calculated by adding **business minutes**, counting only intervals that fall inside working hours and outside holidays; nights/weekends do not count. Tickets created outside working hours begin consuming SLA time at the next open period.
- Example **for explanation, not a mandated default**: Sunday–Thursday 09:00–17:00 in `Africa/Cairo`, Friday/Saturday closed. A ticket created Thursday 16:00 with a 4-business-hour target is due Sunday 12:00 if Sunday is not a holiday.
- Show/edit weekly schedule and a simple add/remove holiday-date list in the SuperAdmin UI. The calendar must support at least an example holiday and an actual cross-weekend deadline. Validate that some business time exists (at least one working day and a positive open interval).
- Persist deadlines as **UTC instants** and display date/times clearly in the user's UI; never assume the server's clock is Cairo time. Handle timezone transitions using supported platform timezone facilities, with no self-built timezone database.
- **Approved S3/S10 boundary:** no SLA pausing for Pending Customer/on-hold states, no per-department calendars, no per-priority calendars, no split shifts or multi-interval workdays. Historical ticket due times remain stable after calendar changes; new tickets use the updated calendar.

## 6. C — Basic configurable workflow engine

- Build a **small rule evaluator** whose rule definitions are managed by SuperAdmin and persisted in SQL Server. No scripts, JSON expression language, graphical workflow designer or arbitrary nested conditions.
- A rule consists of: **Name**, **Enabled**, **Trigger**, up to a small fixed set of **supported conditions**, **Action**, minimal **parameters** (such as a target Agent or warning threshold), and **evaluation order/priority** where multiple assignment rules exist.
- Supported triggers for this first version:
  - `TicketCreated` — fired during ticket creation, used for assignment.
  - `SlaWarningDue` — detected by background worker when warning threshold is reached.
  - `SlaBreached` — detected by background worker once an unresolved/unresponded target has crossed its deadline.
- Supported conditions should be restricted to relevant existing ticket data: category, priority, whether assigned, and SLA kind (`FirstResponse` or `Resolution`). No universal arbitrary-expression editor.
- Supported actions: `AssignToAgent` for ticket creation; `CreateInAppNotification` for warning/breach; `EscalateTicket` for breach, using the **existing** ticket escalation semantics. If one trigger needs both escalation and notification, support a small explicit action combination or **two simple rules**, whichever better fits the current code.
- SuperAdmin can **create, list, change and disable** rules and set supported parameters. Validate configuration when saved (valid trigger/action combinations, active target Agent, valid category/priority values, positive warning lead time), so the worker does not constantly retry an invalid rule.
- Evaluate deterministic rule order and **execute at most one automatic assignment action per new ticket**. Once a rule has assigned the ticket, subsequent assignment rules must not overwrite it in the same event. Existing user-chosen valid assignments, if currently supported on creation, should not be overridden.
- When no assignment rule matches, preserve the existing unassigned flow so SuperAdmin can assign manually. If configured Agent becomes inactive, skip that rule and leave the ticket eligible for other valid rules or manual assignment; do not assign to a disabled account.
- Rule execution must not recursively trigger itself or create repeated actions/notifications. This is a basic bounded rule engine, not a multi-step BPMN/event-bus framework.

## 7. D — Background processing

- Include a real, functional **ASP.NET Core hosted `BackgroundService`** within the existing API host (or the smallest equivalent native .NET scheduler), with a configurable polling interval; **approved S8 default approximately 60 seconds**.
- On each run, query eligible unresolved/unresponded tickets for approaching or breached response/resolution deadlines; evaluate the applicable configured rules; perform due escalations and persist notifications/results.
- Handle worker startup/restart and short outages: if the deadline passed while the API was stopped, the next run must process it. Do not claim processing happens while the API host is shut down.
- Store minimal **per-ticket, per-SLA-type, per-event notification/escalation state** (or equivalent unique processing key) so repeated polling does not duplicate alerts, history, or escalations. Use ordinary database uniqueness/idempotency as appropriate; avoid distributed locks, queues and elaborate retry frameworks for a single local training host.
- Failures should be logged and should not terminate the entire application/worker; next scans may retry eligible work. Expensive full-table scanning should be avoided by simple indexed eligibility queries, but no scalability infrastructure is needed.
- **Training deployment boundary:** a single locally running API instance. Document that multiple backend replicas would require additional coordination and are outside scope.

## 8. E — Automatic assignment

- Provide rule-based **category-to-Agent assignment** with an optional priority condition. SuperAdmin chooses a current active Agent from an existing Agent list and controls rule order/enabled state.
- When a new unassigned ticket is created, evaluate `TicketCreated` assignment rules in configured order and apply the first match. Show assigned Agent on the ticket and dashboard with the same authorization and customer visibility as a normal valid ticket assignment.
- Record the system assignment in existing ticket activity/history with an understandable attribution such as "Assigned automatically" rather than pretending SuperAdmin clicked Assign.
- No round-robin distribution, load-balancing algorithms, skill matrices, departmental routing, or complex fallback chains in this story.

## 9. F — Escalation rules

- An SLA is **breached** when its due instant passes and the required first response/resolution hasn't happened on time (or its actual completion was late). Preserve the timestamp and distinction between first-response and resolution breach.
- Allow SuperAdmin to configure whether response breaches and/or resolution breaches **automatically escalate**. A matching enabled `SlaBreached` rule marks the ticket escalated using the **existing** escalation behavior and adds the appropriate system activity/history event.
- If a ticket was already manually escalated, do not generate a second identical escalation. Escalation does **not** silently reassign the Agent or change priority unless the existing workflow explicitly requires it; surface any incompatible existing behavior for review.
- Escalation does not rewrite the original SLA deadline, restart the SLA clock, or hide the breach.

## 10. G — Alerts and notifications

- Support **persistent in-app** alerts for SLA events using the **existing DASH-002 notification/inbox behavior** where compatible. Inspect whether that code is reminder-specific; extend it minimally or use the smallest shared notification abstraction rather than duplicating a competing bell/inbox.
- **Warning alert** when a pending response/resolution SLA enters a configured lead-time window (e.g. 30 business minutes before due). **Breach alert** when the target is missed. SuperAdmin may configure warning lead times, enable/disable applicable alert rules and choose recipients within the supported options.
- **Approved S7 recipients:** warning → current primary assigned Agent; breach → current Agent **and** SuperAdmin's staff inbox/management view. An unassigned ticket's alert still reaches SuperAdmin. No Customer alerts in this story.
- Alerts include minimal safe ticket context (ticket ID/subject where authorized), type (first response or resolution), due time, status and a link that honors **current** ticket authorization. Collaborator-only membership must not give access to the full ticket.
- Provide unread/read state and a badge/list when the app is open; overdue events remain visible on next login after downtime. No email, SMS, WhatsApp, browser push, WebSockets, or real-time delivery guarantees.
- Ensure reassignment doesn't expose a now-inaccessible ticket through an old Agent's alert; use current access checks when listing/launching notifications.
- An SLA alert is different from a DASH-002 task reminder; neither completes a task or changes a ticket on its own.

## 11. H — Ticket-level SLA monitoring

- On existing authorized Ticket Details, display response and resolution targets, due times, status, and time remaining/overdue in **business time**. Include **Mark as Responded** only while permitted and applicable, with clear text that this is a manual acknowledgement and sends nothing externally.
- On existing Agent ticket list/dashboard, show a concise SLA indicator (`On track`, `Warning`, `Breached`, `Met`, or `Not tracked`) without changing existing counts or queue ownership. Provide basic sorting/filtering by SLA attention state where practical.
- SuperAdmin has a simple **SLA attention/overdue overview** of currently at-risk and breached tickets with links to permitted ticket details. Full historical charts and Agent-performance reporting belong to later Reports & Management module.
- Use one shared backend SLA calculation/evaluation service for deadline computation and state; the frontend displays backend-authoritative states rather than inventing a contradictory SLA clock.

## 12. I — SuperAdmin administration UI

Add a protected **SLA & Automation** feature in the existing Angular shell for SuperAdmin:

1. **Policies:** edit response/resolution target durations for the existing ticket priorities; enable/disable an active policy as supported by the backend.
2. **Business Calendar:** edit timezone, weekdays, a single daily business-hours interval, and holiday dates.
3. **Automation Rules:** list/create/edit/enable/disable supported simple trigger/condition/action rules, including target Agent and warning threshold parameters.
4. **SLA Overview:** basic counts and lists of warning/breached tickets and recent automations/results; no analytics dashboard or charts required.

Controls must make supported rule combinations clear. Do not build a generic visual drag/drop workflow designer. Agent users may see personal ticket SLA states and notifications but cannot access these configuration routes or endpoints; Customers gain no new access.

## 13. Data, API, correctness and migration constraints

- Add the **minimum** required persisted concepts for SLA policy, calendar/holidays, rule definitions, per-ticket SLA snapshots/status/response attribution and processed action/notification deduplication. Reuse existing ticket, ticket-history, auth and notification tables where appropriate. Planner chooses the smallest coherent schema after inspecting current code.
- Add **reviewed additive EF Core migration(s)**; preserve all existing migrations and customer/ticket/task/notification data. Do not reset `CrmDev`, create fake customers via migrations, or overwrite historical tickets.
- Reuse existing CQRS/validation/Unit of Work, auth, status-change and auditing patterns. Coordinate ticket-creation + SLA snapshot + auto-assignment so a successful create request cannot leave an inconsistent half-configured ticket. Avoid speculative ETags, row locks, transaction retry loops and distributed processing architecture beyond what is actually necessary.
- Use clearly named API endpoints under current `/api/v1/` conventions; prefer extending existing ticket read models/backward-compatibly over duplicating ticket data. Use server-generated action times and identities, validated role/ownership, readable `ProblemDetails`, and documented DTOs.
- Preserve existing notification behavior and the previously implemented Customer, Agent Dashboard, Ticket Activity and Team Collaboration workflows.
- Existing tickets from before SLA-001 must remain accessible even if no historical SLA exists; mark them `Not tracked` by default rather than inventing a past first response.
- Document time calculation rules, timezone mapping, holiday handling, automation ordering, hosted worker behavior, and limitations.

## 14. Verification and training test policy

- Exactly **one new backend happy-path unit test** and **one new frontend happy-path unit test** for `SLA-001`, consistent with previous stories. Run all existing unit tests; do not add a Playwright/E2E suite or automated SQL/API integration tests without explicit request.
- **Because the module includes nine capabilities, the manual acceptance matrix below is mandatory.** One unit test per side is not a substitute for functional verification.
- Build backend/frontend; verify migrations against development `CrmDev` without removing existing data; manually exercise APIs, SQL rows and the real browser. If browser automation is unavailable, document browser checks as `Unverified`, not passed.
- Use a controllable local/test clock or short demo SLA targets to verify worker behavior **without waiting real business hours**. Do not ship special hardcoded demo rules in production logic.
- Verify at least: 
  1. SuperAdmin saves a policy with response/resolution targets for every existing priority; Agent cannot edit policy.
  2. Calendar excludes closed weekday/weekend and a chosen holiday; due times correctly traverse both boundaries, with stored UTC deadlines.
  3. New unassigned ticket matches a category-based rule and is assigned once; ticket created with no match remains unassigned; rule enable/disable changes only subsequent events.
  4. Staff manually marks first response once; it records server timestamp/actor, does not send anything, and distinguishes met from late response.
  5. A ticket is resolved on time vs after the resolution deadline; reporting shows correct states; existing ticket workflow remains functional.
  6. A near-due ticket gets one warning alert and the configured Agent sees it in-app; reminder notification UX still works.
  7. A breached response/resolution SLA gets exactly one escalation/history event and one intended breach alert per configured rule, including after two worker polls or an API restart.
  8. A disabled rule does not run; an invalid/inactive target Agent cannot be automatically assigned; unassigned tickets remain actionable by SuperAdmin.
  9. Agent sees SLA data only for assigned tickets; SuperAdmin has full oversight; Customer gets no new access; removed collaborator cannot bypass existing ticket visibility.
  10. Admin forms, Agent Ticket Details, dashboards/inbox, error/empty states and basic mobile-width layout render and work in-browser.
  11. Existing story features (ticket assignment/status/history, tasks/reminders, Agent Dashboard, customer data) pass a quick regression smoke test.
- Report `dotnet build`, `dotnet test`, `ng build`, frontend unit test results, migration status, manual verification and any remaining blockers. Keep unrelated pre-existing warnings clearly separate.
- **Do not commit or push** until implementation is independently reviewed and user explicitly accepts it.

## 15. Out of scope

- Real customer communications, provider-based first-response detection, Email/WhatsApp/SMS/Live Chat/Web Form integrations or sending.
- Multi-calendar organizations/departments/branches, different hours per day, multiple daily shifts, complex holiday recurrence, SLA pause/resume, sophisticated breach waivers, retroactive SLA recalculation, multiple resolution cycles and comprehensive reopening policy changes.
- Visual workflow designers, arbitrary user-authored code, nested boolean expressions, loops and multi-stage approval workflows; load-balancing, round-robin or ML assignment.
- Distributed/multi-instance job orchestration, external queues, Hangfire/Quartz requirements, multi-region deployment, high-availability scheduling, websocket push.
- KPI/report charting and customer satisfaction analytics (future Reports & Management); Customer Portal integration and AI features.
- Expanding normal ticket visibility to collaboration-only Agents; changes to customer-profile notes/attachments/interaction records.
- Automated API/integration/E2E/performance suites and speculative infrastructure hardening beyond the training test policy.

## 16. Approved business decisions — S1–S12

**Approval recorded: all nine capabilities and all twelve operating decisions S1–S12 were explicitly approved by the user.** The following rules are the binding Story 09 business baseline for Squad-Kit planning:

| ID | Business question | Approved rule |
| --- | --- | --- |
| **S1 — SLA scope** | Which tickets receive SLA? | New tickets only; one global policy with per-priority response/resolution business-minute targets; previously created tickets show `Not tracked`. |
| **S2 — First response** | How do we measure it before Communications? | Explicit `Mark as Responded` by assigned Agent or SuperAdmin, server time, first mark only; does not send a customer message. |
| **S3 — Calendar** | How many calendars and working shifts? | One global timezone-aware calendar; working weekdays, one daily opening/closing interval, explicit holidays. Seed/demo hours are not fixed business promises. |
| **S4 — History of settings** | What if targets/calendar change? | Changes affect newly created tickets; existing ticket SLA deadlines remain unchanged. |
| **S5 — Assignment** | Which automatic strategy? | First matching enabled category→active-Agent rule; optional priority condition; already-assigned tickets unchanged; fallback unassigned. |
| **S6 — Escalation** | What happens on breach? | Use existing escalated state/flag once; don't automatically reassign or change priority. |
| **S7 — Alerts** | Who gets warnings/breaches? | Warning → assigned Agent; breach → assigned Agent + SuperAdmin; unassigned ticket → SuperAdmin. In-app only; reuse existing inbox. |
| **S8 — Scheduling** | When is automation executed? | Native in-process `BackgroundService` poll about every 60 seconds; recover missed deadlines when app restarts; deduplicate database effects. |
| **S9 — Rule editor** | What can SuperAdmin configure? | Fixed types: TicketCreated/SlaWarningDue/SlaBreached; category/priority/assignment/SLA kind conditions; Assign/Notify/Escalate actions; enable/disable/order. |
| **S10 — Reopen and pauses** | Pause clock or reset on reopen? | No pause; first-response time stays fixed, original SLA cycle remains historical on reopen. Reopening does not silently reset or create another SLA cycle. |
| **S11 — Warnings** | How to interpret lead time? | Configured lead time measured in **business minutes** before the persisted deadline; at most one warning per ticket/SLA kind per configured rule. |
| **S12 — Notifications** | Delivery guarantee? | Persistent in-app inbox/polling; best effort while the API runs, due events caught up after restart, never claim a customer-facing delivery. |

**Planning gate passed:** S1–S12 are **approved**; proceed with `/squad-plan`. The planner may decide code organization and endpoint paths after inspecting the repo, but must not silently change agreed business behavior. Only surface material conflicts discovered in the actual implementation.

## 17. Dependencies and handoff

- **Prerequisite for implementation:** CUST-002 (Story 08) accepted and committed, with a clean baseline. Planning may proceed now; the planner must verify actual Git/repository state and flag any uncommitted prior-story changes before implementation.
- **Inputs:** this intake, original *Customer Support CRM — Core Features* document (if available to the planner), current repository code, current ticket/status/priority/notification designs and relevant architecture docs.
- **Sequencing inside implementation plan:** inspect baseline → SLA policy/calendar → ticket SLA snapshot and first-response tracking → basic rule engine and automatic assignment → worker/escalation/notifications → ticket/Admin UI → migration/API/browser/manual verification. The planner may refine exact ordering to avoid incomplete functionality.
- **Expected planning artifact:** `.squad/plans/sla-automation/09-story-sla-basic-automation.md` (suggested filename; planner picks actual path and updates overview/index).
- **Delivery gate:** plan only first, then independent review; implement later in a fresh session; no commit until accepted.

## Attachments

- Original *Customer Support CRM — Core Features* (two-page business feature list) — supplied in earlier project work. The user has not required embedding/copying the original into the repository for this story.

## Notes for Squad-Kit planner

This intake defines **what must work** and the **approved** business boundaries, **not the class-by-class implementation plan**. Inspect the repository before proposing persistence tables or altering ticket status, notifications, identity, routes, or tests. Complete all nine named capabilities with simple, coherent implementations. Flag genuine conflicts with the existing codebase for review; do not reopen S1–S12 without a material contradiction. Avoid turning this training assessment into an enterprise workflow suite.
