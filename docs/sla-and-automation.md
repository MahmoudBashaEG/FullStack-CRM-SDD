# SLA & basic automation (SLA-001)

Measures, routes, watches and escalates support work. Configured by SuperAdmin without code changes. Basic by design: one global policy, one global calendar, fixed triggers/conditions/actions, in-app alerts only.

## 1. Decisions and conflicts

Business decisions S1–S12 were approved in the intake (S1 snapshot at creation, S2 manual first response, S4 stored deadlines never change, S5 automatic assignment of unassigned tickets, S6 escalation once, S7 alert recipients, S8 exactly-once effects, S9 fixed rule model, S10 first resolution only, S11 one alert per rule, S12 no external delivery). Corrections F1 (each warning rule uses its own lead), F2 (rule conditions in SQL before the batch limit) and F3 (one shared remaining-time calculation) are implemented.

Conflicts with existing code and their resolutions:

| # | Conflict | Resolution |
|---|---|---|
| C1 | `TicketActivity.ActorUserId` was required | now nullable; `null` = **Automation**; history joins the actor with a left join |
| C2 | the DASH-002 inbox is Agent-only and task-specific | new `SlaAlerts` table + `/api/v1/sla-alerts` (Staff); the same bell and `/notifications` page now serve all staff; reminder endpoints unchanged |
| C3 | Agents always self-assign; there is no customer submission yet | rules run only when the ticket is still unassigned after the existing assignee resolution (SuperAdmin-created unassigned tickets today); future channels reuse `ITicketCreationAutomation` |
| C4 | resolving clears escalation | kept; SLA breach state and alerts are independent of the flag |
| C5 | `Resolved → InProgress` and `Closed → Open` are allowed | `SlaResolvedAtUtc` stamped on the first `Resolved` only; no new cycle on reopen |
| C6 | `Tickets.Version` can race the worker | one short transaction per (rule, ticket, kind); a 409 rolls that item back and the next poll retries |
| C7 | no background infrastructure | `SlaAutomationWorker : BackgroundService` in `Crm.Api`, single instance |
| C8 | `CreateTicketCommandHandler` gained a dependency | the existing unit test only received the extra constructor argument |

## 2. Roles and routes

| Route | SuperAdmin | Agent | Customer |
|---|---|---|---|
| `GET/PUT /api/v1/sla/policy`, `GET/PUT /sla/calendar`, `GET /sla/calendar/preview`, `GET /sla/overview` | yes | 403 | 403 |
| `GET/POST /automation-rules`, `GET/PUT /automation-rules/{id}` | yes | 403 | 403 |
| `GET /sla-alerts`, `GET /sla-alerts/unread-count`, `PUT /sla-alerts/{id}/read` | own alerts | own alerts, only for tickets currently assigned to them | 403 |
| `POST /tickets/{id}/first-response` | any tracked active ticket | own assigned ticket (others 404) | 403 |
| `GET /tickets?slaBreached=` | any | own tickets | 403 |

Angular: `/sla` (SuperAdmin; tabs Policy, Calendar, Rules, Overview); `/notifications` for SuperAdmin and Agent (SuperAdmin sees only SLA alerts).

## 3. Policy and ticket snapshot

`SlaPolicies` (Id 1) with `SlaPolicyTargets` (one row per priority): first-response and resolution targets in business minutes, plus an enabled flag. Nothing is seeded. A ticket is tracked only if, **at creation**, the policy exists and is enabled and a calendar exists; then the targets, both due instants (UTC) and the calendar time zone are written to eight nullable `Tickets` columns. Otherwise all stay `NULL` = "Not tracked". Existing tickets are never back-filled. Priority changes after creation do not move deadlines.

## 4. Business time

`BusinessCalendars` (Id 1): time zone id (IANA, resolved with `TimeZoneInfo`), working weekdays, one opening/closing local time, explicit holiday dates. Deadlines are computed by `BusinessTimeCalculator.AddBusinessMinutes` (start outside hours begins at the next open period; closed days and holidays are skipped; a local time inside a DST gap moves to the first valid minute; an ambiguous time uses standard time) and stored as UTC.

**S4:** stored due instants never change after policy or calendar edits. Remaining and elapsed business minutes, and warning thresholds, are computed at read time with the **current** calendar between now and the **stored** due instant (negative = overdue). Display (`SlaEvaluator.Evaluate`) and the worker call the same `RemainingBusinessMinutes` and `IsWarningThresholdReached`, so a ticket shown as Warning is the one the worker alerts on. Without a calendar there is no remaining time and no warning. The UI states this.

## 5. First response and resolution

First response is recorded manually (**Mark as Responded**, S2): server time and actor, once; a second call is 409; not tracked is 409; only Open/In Progress. Nothing is sent to the customer. Resolution is the first transition to `Resolved` (S10). States per target: Pending, Met (completed on time), Breached (late, or open past due). Ticket indicator: Not tracked, Breached, Met, Warning, On track.

## 6. Rules

Fields: name, enabled, trigger, action, sort order, optional conditions (category, priority, assigned/unassigned, SLA kind), target Agent, warning lead. Valid combinations only: `TicketCreated`→`AssignToAgent` (active Agent), `SlaWarningDue`→`CreateInAppNotification` (lead 1–10080), `SlaBreached`→`CreateInAppNotification` or `EscalateTicket`. Notify and escalate on breach are two rules. Rules run by sort order, then creation time, then id; at most one assignment per ticket (first match with an active Agent target wins; inactive targets are skipped). Rules are never deleted. `EnabledAtUtc` (set on create-enabled and on re-enable) means only SLA events due after it fire. Several matching warning rules each fire at their own lead (F1).

## 7. Worker

`SlaAutomationWorker` runs once at startup (catch-up for deadlines missed while stopped) and every `SlaAutomation:PollInterval` (default 00:01:00, allowed 00:00:10–01:00:00; validated at startup). `SlaAutomation:Enabled=false` disables it. Each pass is `RunSlaAutomationCommand`; each (rule, ticket, kind) runs in its own `ExecuteInTransactionAsync`, reloads the ticket, re-checks eligibility and writes the execution row, alerts or escalation together. `UX_AutomationRuleExecutions_RuleTicketKind (RuleId, TicketId, SlaKind)` (no filter; NULL kinds dedupe assignment) and `UX_SlaAlerts_ExecutionRecipient` make duplicates impossible; they are deliberately not in `KnownUniqueIndexes`. Candidate queries apply the rule's category/priority/assignment conditions in SQL before `TOP`, ordered by due then id, with in-pass exclusion paging so a failing ticket never blocks the others (F2). Limits: 200 per page, 10 pages per (rule, kind) per pass. **Single API instance only**; several replicas would duplicate polling work (the unique keys still prevent duplicate effects).

## 8. Alerts

Warning: assigned active Agent, or all active SuperAdmins when unassigned. Breach: assigned active Agent and all active SuperAdmins. Alerts are persistent and read/unread. Visibility is evaluated on every call: recipient is the caller and (SuperAdmin, or the ticket is currently assigned to the caller). A reassigned ticket's alerts disappear for the former Agent and are not inherited by the new one; collaborator membership grants nothing. Nothing leaves the CRM.

## 9. UI

Ticket details SLA card; SLA column and "Breached" filter on the ticket list; SLA badge on the Agent Dashboard queue (counts and order unchanged); bell for all staff polling every 60 s; `/sla` admin tabs with loading/empty/error states.

## 10. Migration

`AddSlaAndAutomation`: 7 new tables, 8 nullable `Tickets` columns, FK `FirstRespondedByUserId`, two filtered worker indexes, `TicketActivities.ActorUserId` made nullable. No data is rewritten.

## 11. Limitations

Overview counts the first 500 active tracked tickets; no SLA pause/resume, per-department calendars, recurring holidays or retroactive recalculation; no customer alerts; no new cycle on reopen; if a configured time zone later disappears from the OS, ticket creation fails until the calendar is re-saved.
