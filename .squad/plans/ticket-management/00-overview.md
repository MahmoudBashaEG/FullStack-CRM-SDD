# ticket-management — plan overview

Entry point for the **ticket-management** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 04 | [04-story-ticket-creation-management.md](04-story-ticket-creation-management.md) | Ticket Creation & Management | TICK-001 (tracker: none) | Story 01 ([../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md)), Story 02 ([../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md)), Story 03 ([../customer-management/03-story-customer-profiles-contact-management.md](../customer-management/03-story-customer-profiles-contact-management.md)) |

## Dependency notes

- **Story 04** reuses without modifying: `IIdentityService.FindByIdAsync` (active-Agent check), `ICustomerRepository.FindDetailsByIdAsync` (customer existence), `AuthorizationPolicies.Staff`, `PagedResult<T>` / `Paging` / `PagedResponse<T>`, `CustomersApi.list` (customer autocomplete) and the SuperAdmin-only `GET /api/v1/agents` (active-Agent dropdown, called only for SuperAdmin). It adds a global `JsonStringEnumConverter` (no earlier contract uses enums).
- Shared contracts established by Story 04 for later stories: `Crm.Domain.Tickets.Ticket` / `Tickets.Id`, enums `TicketCategory` / `TicketPriority` / `TicketStatus` (stored by name, so TICK-002 can append statuses), `ITicketRepository` with `agentScope`, `TicketActor` (token-derived actor), `TicketDetails`, and `docs/ticket-management.md`.
- **TICK-002 — Ticket Workflow, Assignment & History** (needs its own intake) builds on Story 04: SuperAdmin-only post-creation assignment/reassignment, status transitions, manual escalation and activity history. Customer Portal and SLA & Automation are separate later features.
