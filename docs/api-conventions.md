# API conventions

- **Routes:** business resources use `/api/v1/{resource}` — plural, lowercase (`ApiRoutes.V1`, `LowercaseUrls = true`). `GET /api/health` is the only unversioned route. No `v2`.
- **DTOs:** PascalCase C# records ending in `Request` / `Response`, in `Crm.Api/Contracts/<Feature>/`. JSON is camelCase (ASP.NET Core web defaults). Never expose Domain entities or EF models.
- **Success:** return the DTO directly, no envelope. `200 OK` for reads/updates, `201 Created` + `Location` (`CreatedAtAction`) on create, `204 No Content` when there is no body.
- **Errors:** `application/problem+json` only, produced centrally by `GlobalExceptionHandler` and `[ApiController]` model validation.

  | Exception | Status | Body |
  |---|---|---|
  | FluentValidation `ValidationException` | 400 | `ValidationProblemDetails` (camelCase error keys) |
  | `NotFoundException` | 404 | `ProblemDetails` |
  | `DomainException` | 422 | `ProblemDetails` |
  | anything else | 500 | `ProblemDetails` (detail only in Development) |

  Every problem includes `traceId`.
- **Controllers are thin:** map request -> command/query, `ISender.Send`, map result -> response. No business or persistence logic.
- **Pagination / sorting / filtering:** deferred to the first list-endpoint feature intake.
- **Angular:** all URLs are built through `ApiUrl` (`versioned()` / `unversioned()`); typed transport interfaces live next to the feature's API service.
