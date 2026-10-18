# customer-management — plan overview

Entry point for the **customer-management** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 03 | [03-story-customer-profiles-contact-management.md](03-story-customer-profiles-contact-management.md) | Customer Profiles & Contact Management | CUST-001 (tracker: none) | Story 01 ([../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md)), Story 02 ([../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md)) |
| 08 | [08-story-customer-notes-interactions-attachments.md](08-story-customer-notes-interactions-attachments.md) | Customer Notes, Interaction History & Attachments | CUST-002 (tracker: none) | Story 03 ([03-story-customer-profiles-contact-management.md](03-story-customer-profiles-contact-management.md)), Story 04–05 ([../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md)), Story 07 ([../agent-dashboard/07-story-agent-productivity-collaboration.md](../agent-dashboard/07-story-agent-productivity-collaboration.md)) |

## Dependency notes

- **Story 03** extends AUTH-001 deliberately: `RegisterCustomerCommand` now also creates the linked CRM profile, `UpdateMyProfileCommand` becomes transactional and mirrors name/phone to it, and two policies (`Staff`, `Customer`) join `AuthorizationPolicies`. `IIdentityService` is unchanged.
- Shared contracts established by Story 03 for later stories: `Crm.Domain.Customers.Customer` / `Customers.Id` (the key future notes, attachments, interaction history and tickets reference — not `AspNetUsers.Id`), `ICustomerRepository`, `PagedResult<T>` / `Paging` (Application) and `PagedResponse<T>` (API) as the list contract, `AuthorizationPolicies.Staff` / `.Customer`, and `docs/customer-management.md`.
- Future stories CUST-002 (notes), CUST-003 (attachments), CUST-004 (interaction history) and Ticket Management each need their own intake; verified contact claiming/merging and customer deletion are deferred.
- **Story 08** (`CUST-002`) delivers notes, manual interaction history and attachments in one story (the earlier CUST-003/CUST-004 split is superseded). It adds one additive migration (`AddCustomerNotesInteractionsAttachments`: `CustomerNotes`, `CustomerInteractions`, `CustomerAttachments` with `varbinary(max)` content), a new `ICustomerRecordRepository` (`ICustomerRepository` unchanged) and `CustomerRecordsController` under `/api/v1/customers/{customerId}/…` (Staff policy; notes/interactions append-only, attachments not deletable). Related tickets reuse the scoped `GET /api/v1/tickets?customerId=` with no ticket code change. Later stories that touch ticket visibility must keep that section consistent; the Communication Channels module must not reuse `CustomerInteractions` for real channel messages without its own intake.
