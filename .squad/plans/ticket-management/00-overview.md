# ticket-management — plan overview

Entry point for the **ticket-management** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 04 | [04-story-ticket-creation-management.md](04-story-ticket-creation-management.md) | Ticket Creation & Management | TICK-001 (tracker: none) | Story 01 ([../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md)), Story 02 ([../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md)), Story 03 ([../customer-management/03-story-customer-profiles-contact-management.md](../customer-management/03-story-customer-profiles-contact-management.md)) |
| 05 | [05-story-ticket-workflow-assignment-history.md](05-story-ticket-workflow-assignment-history.md) | Ticket Workflow, Assignment & History | TICK-002 (tracker: none) | Story 04 ([04-story-ticket-creation-management.md](04-story-ticket-creation-management.md)); transitively Stories 01–03 |

## Dependency notes

- **Story 04** reuses without modifying: `IIdentityService.FindByIdAsync` (active-Agent check), `ICustomerRepository.FindDetailsByIdAsync` (customer existence), `AuthorizationPolicies.Staff`, `PagedResult<T>` / `Paging` / `PagedResponse<T>`, `CustomersApi.list` (customer autocomplete) and the SuperAdmin-only `GET /api/v1/agents` (active-Agent dropdown, called only for SuperAdmin). It adds a global `JsonStringEnumConverter` (no earlier contract uses enums).
- Shared contracts established by Story 04 for later stories: `Crm.Domain.Tickets.Ticket` / `Tickets.Id`, enums `TicketCategory` / `TicketPriority` / `TicketStatus` (stored by name, so TICK-002 can append statuses), `ITicketRepository` with `agentScope`, `TicketActor` (token-derived actor), `TicketDetails`, and `docs/ticket-management.md`.
- **Story 05 (TICK-002)** extends Story 04 in place: appends `InProgress`/`Resolved`/`Closed` to `TicketStatus`, adds escalation columns + `Version` rowversion to `Tickets`, the append-only `TicketActivities` table (aggregate-owned, saved in the same `SaveChanges`), `ITicketRepository.ListActivityAsync`, five fields on `TicketDetails`/`TicketResponse`, the `escalated` list filter, and `EfUnitOfWork` mapping `DbUpdateConcurrencyException` ⇒ 409. Reuses the SuperAdmin-only `GET /api/v1/agents` for the reassignment selector. Customer Portal and SLA & Automation remain separate later features.
