# identity-access-management — plan overview

Entry point for the **identity-access-management** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 02 | [02-story-users-roles-authentication-authorization.md](02-story-users-roles-authentication-authorization.md) | Users, Roles, Authentication & Authorization | AUTH-001 (tracker: none) | Story 01 ([../project-architecture/01-story-project-init-architecture.md](../project-architecture/01-story-project-init-architecture.md)) |

## Dependency notes

- **Story 02** builds on the ARCH-001 foundation (Story 01) and changes its shared contracts deliberately: authentication is required by default (fallback policy), `GlobalExceptionHandler` gains 401/409 mappings, and `docs/api-conventions.md` documents the `/api/v1/auth/*` and `/api/v1/me` singleton routes.
- Shared contracts established by Story 02 for later features: `UserRoles` (`Crm.Domain/Identity`), `IIdentityService` / `ITokenService` (`Crm.Application/Abstractions/Identity`), the `SuperAdmin` authorization policy, `ClaimsPrincipalExtensions.GetUserId()`, the Angular `AuthService` / `authGuard` / `roleGuard` and `NavItem.roles`, and `docs/identity-and-access.md`.
- Future Customer Management, Tickets and Customer Portal stories link their records to the stable `AspNetUsers.Id` (`Guid`) and define their own Agent/Customer permissions in their intakes.
