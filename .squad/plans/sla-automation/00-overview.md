# sla-automation — plan overview

Entry point for the **sla-automation** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 09 | [09-story-sla-basic-automation.md](09-story-sla-basic-automation.md) | SLA & Basic Automation — Targets, Calendars, Workflows, Background Jobs, Assignment, Escalation and Notifications | SLA-001 (tracker: none) | Story 08 ([../customer-management/08-story-customer-notes-interactions-attachments.md](../customer-management/08-story-customer-notes-interactions-attachments.md)), Story 07 ([../agent-dashboard/07-story-agent-productivity-collaboration.md](../agent-dashboard/07-story-agent-productivity-collaboration.md)), Story 06 ([../agent-dashboard/06-story-agent-dashboard-ticket-overview-work-queue.md](../agent-dashboard/06-story-agent-dashboard-ticket-overview-work-queue.md)), Story 05 ([../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md)), Story 04, Story 02 |

## Dependency notes

- **Story 09** (`SLA-001`) implements all nine SLA & Automation capabilities in one story, with business decisions S1–S12 approved in the intake. It adds one migration (`AddSlaAndAutomation`: `SlaPolicies`/`SlaPolicyTargets`, `BusinessCalendars`/`BusinessHolidays`, `AutomationRules`, `AutomationRuleExecutions`, `SlaAlerts`, 8 nullable SLA columns on `Tickets`, `TicketActivities.ActorUserId` made nullable for **Automation** attribution).
- Shared contracts it changes: `Ticket` / `TicketActivity` (system actor), `TicketDetails` / `TicketResponse` (+`sla`), `TicketActivityResponse` (+`isAutomatic`), `AttentionTicketResponse` (+`slaIndicator`), `CreateTicketCommandHandler` (+`ITicketCreationAutomation` hook), the DASH-002 bell and `/notifications` page (now all staff; SLA alerts live in their own `SlaAlerts` table and `/api/v1/sla-alerts` — the reminder endpoints are unchanged).
- First background process in the system: `SlaAutomationWorker` (hosted `BackgroundService` in `Crm.Api`), single API instance only.
- Later stories: the **Communication Channels** module (kept last) should replace the manual "Mark as Responded" with real first-response detection and reuse `ITicketCreationAutomation` for channel-created tickets; **Reports & Management** builds SLA KPIs/charts on the persisted due instants and `AutomationRuleExecutions`.

## Implementation progress

| NN | Story | Status |
|----|-------|--------|
| 09 | SLA-001 SLA & Basic Automation | **Implemented, uncommitted — awaiting independent review.** Migration `AddSlaAndAutomation` applied to local `CrmDev`. Backend 9/9 and frontend 9/9 unit tests pass (baseline 8/8 + one new each); both builds succeed. API/SQL acceptance matrix run by the implementing session. Browser checks (11 items: /sla tabs, calendar preview, auto-assignment, SLA card, list/dashboard, warning/escalation, bell/inbox, role authorization, error states, responsive layout, regression smoke) reported complete by the user after manual testing. |
