# agent-dashboard — plan overview

Entry point for the **agent-dashboard** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 06 | [06-story-agent-dashboard-ticket-overview-work-queue.md](06-story-agent-dashboard-ticket-overview-work-queue.md) | Agent Dashboard — Ticket Overview & Work Queue | DASH-001 (tracker: none) | Story 05 ([../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md)), Story 04 ([../ticket-management/04-story-ticket-creation-management.md](../ticket-management/04-story-ticket-creation-management.md)), Story 03 ([../customer-management/03-story-customer-profiles-contact-management.md](../customer-management/03-story-customer-profiles-contact-management.md)), Story 02 ([../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md)) |
| 07 | [07-story-agent-productivity-collaboration.md](07-story-agent-productivity-collaboration.md) | Agent Productivity & Collaboration — Tasks, Reminders, Quick Replies & Team Discussions | DASH-002 (tracker: none) | Story 06 ([06-story-agent-dashboard-ticket-overview-work-queue.md](06-story-agent-dashboard-ticket-overview-work-queue.md)), Story 05 ([../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md)), Story 04, Story 02 |

## Dependency notes

- Story 06 is a **read-only consumer** of ticket-management (Stories 04–05) and customer-management (Story 03): it reuses `TicketRepository.Scope` (the Agent ownership filter) and links to the existing `/tickets/:id` and `/customers/:id` screens. It adds no ticket/customer operations, no tables and no migration.
- Shared contracts added: `AuthorizationPolicies.Agent` (Agent role only) and the `/api/v1/agent-dashboard` singleton route. Later stories that change ticket visibility or `TicketStatus` / `TicketPriority` must update `AgentDashboardRepository` metric definitions and `docs/agent-dashboard.md`.
- Story 07 delivers the deferred tasks & reminders, quick replies and team collaboration. It adds one additive migration (`AddAgentProductivityAndCollaboration`: `AgentTasks`, `TaskReminderNotifications`, `QuickReplyTemplates`, `TicketCollaborators`, `TicketInternalMessages`), a separate `GET /api/v1/agent-dashboard/tasks` (the DASH-001 snapshot is unchanged), `StaffActor`, `ForbiddenException` (403), `PreconditionRequiredException` (428) with an ETag / `If-Match` protocol for `/api/v1/tasks`, `ITicketAccessLock` (row-locked write-time authorization) and an allow-listed unique-index ⇒ 409 mapping in `EfUnitOfWork`. Collaborators use only `/api/v1/collaborations/*`; `TicketRepository.Scope` and all ticket APIs are unchanged.
- Shared contracts added by Story 07: later stories that change ticket assignment or visibility must keep `AgentTaskRepository` (`LinkedTicketState`) and `CollaborationRepository.Accessible` consistent and update `docs/agent-productivity-and-collaboration.md`. The bell (`core/notifications/NotificationBell`) is the app's only polling; a future notifications/communication feature should extend it rather than add another poller.
- SuperAdmin-wide metrics belong to a future Reports & Management feature; outbound replies to a future Communication Channels feature.
