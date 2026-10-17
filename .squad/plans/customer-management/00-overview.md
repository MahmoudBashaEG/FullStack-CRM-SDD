# customer-management — plan overview

Entry point for the **customer-management** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 03 | [03-story-customer-profiles-contact-management.md](03-story-customer-profiles-contact-management.md) | Customer Profiles & Contact Management | CUST-001 (tracker: none) | Story 01 ([../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md)), Story 02 ([../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md)) |

## Dependency notes

- **Story 03** extends AUTH-001 deliberately: `RegisterCustomerCommand` now also creates the linked CRM profile, `UpdateMyProfileCommand` becomes transactional and mirrors name/phone to it, and two policies (`Staff`, `Customer`) join `AuthorizationPolicies`. `IIdentityService` is unchanged.
- Shared contracts established by Story 03 for later stories: `Crm.Domain.Customers.Customer` / `Customers.Id` (the key future notes, attachments, interaction history and tickets reference — not `AspNetUsers.Id`), `ICustomerRepository`, `PagedResult<T>` / `Paging` (Application) and `PagedResponse<T>` (API) as the list contract, `AuthorizationPolicies.Staff` / `.Customer`, and `docs/customer-management.md`.
- Future stories CUST-002 (notes), CUST-003 (attachments), CUST-004 (interaction history) and Ticket Management each need their own intake; verified contact claiming/merging and customer deletion are deferred.
