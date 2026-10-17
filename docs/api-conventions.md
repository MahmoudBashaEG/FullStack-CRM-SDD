# API conventions

- **Routes:** business resources use `/api/v1/{resource}` — plural, lowercase (`ApiRoutes.V1`, `LowercaseUrls = true`). `GET /api/health` is the only unversioned route. No `v2`. Singleton resources `/api/v1/auth/*` and `/api/v1/me` are the documented exceptions to plural naming.
- **DTOs:** PascalCase C# records ending in `Request` / `Response`, in `Crm.Api/Contracts/<Feature>/`. JSON is camelCase (ASP.NET Core web defaults). Never expose Domain entities or EF models.
- **Success:** return the DTO directly, no envelope. `200 OK` for reads/updates, `201 Created` + `Location` (`CreatedAtAction`) on create, `204 No Content` when there is no body.
- **Errors:** `application/problem+json` only, produced centrally by `GlobalExceptionHandler` and `[ApiController]` model validation.

  | Exception | Status | Body |
  |---|---|---|
  | FluentValidation `ValidationException` | 400 | `ValidationProblemDetails` (camelCase error keys) |
  | `NotFoundException` | 404 | `ProblemDetails` |
  | `UnauthorizedException` | 401 | `ProblemDetails` |
  | `ConflictException` | 409 | `ProblemDetails` |
  | `DomainException` | 422 | `ProblemDetails` |
  | anything else | 500 | `ProblemDetails` (detail only in Development) |

  Every problem includes `traceId`.
- **Controllers are thin:** map request -> command/query, `ISender.Send`, map result -> response. No business or persistence logic.
- **Auth:** every endpoint requires a Bearer JWT by default (fallback policy); anonymous endpoints carry `[AllowAnonymous]` and say so in `EndpointDescription`. Role-restricted endpoints use named policies (`AuthorizationPolicies`). 401/403/429 responses are ProblemDetails via `UseStatusCodePages`. Staff endpoints use `AuthorizationPolicies.Staff` (SuperAdmin or Agent); Customer self-service endpoints use `AuthorizationPolicies.Customer` and take the user id from the token only. See [identity-and-access.md](identity-and-access.md).
- **Pagination:** list endpoints take one-based `page` (default 1) and `pageSize` (default 20, values above 100 are clamped to 100) plus feature-specific filters (e.g. `search`), use a stable default order with an `Id` tie-breaker, and return `PagedResponse<T>` (`Crm.Api/Contracts/Common`) `{ items, page, pageSize, totalCount }`. Non-list endpoints keep returning the DTO directly. First used by `GET /api/v1/customers` ([customer-management.md](customer-management.md)).
- **Angular:** all URLs are built through `ApiUrl` (`versioned()` / `unversioned()`); typed transport interfaces live next to the feature's API service.
