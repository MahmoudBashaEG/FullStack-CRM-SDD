# reports-management — plan overview

Entry point for the **reports-management** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 13 | [13-story-reports-management.md](13-story-reports-management.md) | Reports & Management — Ticket Reports, SLA Performance, Agent Performance, Customer Satisfaction & Management Dashboard | REPORT-001 (tracker: none) | Story 12 ([../customer-portal/12-story-customer-portal.md](../customer-portal/12-story-customer-portal.md), committed baseline, `TicketFeedback`), Story 09 ([../sla-automation/09-story-sla-basic-automation.md](../sla-automation/09-story-sla-basic-automation.md)), Story 06 ([../agent-dashboard/06-story-agent-dashboard-ticket-overview-work-queue.md](../agent-dashboard/06-story-agent-dashboard-ticket-overview-work-queue.md)), Stories 04–05, Story 02 |

## Dependency notes

- **Story 13** (`REPORT-001`) delivers all five Reports & Management areas in one story under approved decisions RM1–RM6. Access is SuperAdmin only, and the story is read-only.
  - **Reads:** a new read-only port `IReportingRepository` / `ReportingRepository` with grouped SQL aggregates.
  - **Schema:** no new table, column, index or migration. No new package.
  - **SLA:** the cohort outcome is a SQL mirror of `SlaEvaluator.Target`. At risk is the existing SLA **Warning**, read through `SlaEvaluator` + `ISlaMonitoringRepository.ListActiveTrackedAsync`. Overdue reuses `TicketRepository.IsBreached`.
  - **Agent credit:** resolution actions are credited to the recorded `StatusChanged → Resolved` actor, never to the current assignee.
  - **CSAT:** cohort by `TicketFeedback.CreatedAtUtc`, using the current rating.
  - **Dashboard:** composed in Angular from the four report endpoints. There is no separate overview endpoint.
- Shared contracts it touches (additive only):
  - New routes: `/api/v1/reports/{tickets|sla|agents|customer-satisfaction}`, all with the `SuperAdmin` policy.
  - Infrastructure DI gets one repository.
  - `TicketRepository.IsBreached` changes from `private` to `internal`.
  - Angular: the `/reports` route group and 1 SuperAdmin `NAV_ITEMS` entry.
- Unchanged: the Agent Dashboard (Agent-only), SLA worker and automation, the portal, AI-001, KB, ticket APIs, and every existing test.
- Later stories:
  - **Communication Channels** stays last. A recorded first response ("Mark as Responded") is not a sent message, and reports label it that way.
  - Indexes for larger datasets (`TicketFeedback.CreatedAtUtc`, `TicketActivities (Type, OccurredAtUtc)`) need separate review.
