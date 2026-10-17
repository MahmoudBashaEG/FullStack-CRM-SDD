# agent-dashboard — plan overview

Entry point for the **agent-dashboard** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 06 | [06-story-agent-dashboard-ticket-overview-work-queue.md](06-story-agent-dashboard-ticket-overview-work-queue.md) | Agent Dashboard — Ticket Overview & Work Queue | DASH-001 (tracker: none) | Story 05 ([../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md)), Story 04 ([../ticket-management/04-story-ticket-creation-management.md](../ticket-management/04-story-ticket-creation-management.md)), Story 03 ([../customer-management/03-story-customer-profiles-contact-management.md](../customer-management/03-story-customer-profiles-contact-management.md)), Story 02 ([../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md)) |

## Dependency notes

- Story 06 is a **read-only consumer** of ticket-management (Stories 04–05) and customer-management (Story 03): it reuses `TicketRepository.Scope` (the Agent ownership filter) and links to the existing `/tickets/:id` and `/customers/:id` screens. It adds no ticket/customer operations, no tables and no migration.
- Shared contracts added: `AuthorizationPolicies.Agent` (Agent role only) and the `/api/v1/agent-dashboard` singleton route. Later stories that change ticket visibility or `TicketStatus` / `TicketPriority` must update `AgentDashboardRepository` metric definitions and `docs/agent-dashboard.md`.
- Deferred to later agent-dashboard stories: tasks & reminders, quick replies, team collaboration. SuperAdmin-wide metrics belong to a future Reports & Management feature.
