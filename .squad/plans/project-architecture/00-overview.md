# project-architecture — plan overview

Entry point for the **project-architecture** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 01 | [01-story-project-init-architecture.md](01-story-project-init-architecture.md) | Project Init Architecture | ARCH-001 (tracker: none) | — |

## Dependency notes

- **Story 01** is foundational: it creates `backend/` (Clean Architecture, CQRS via Mediator, EF Core + SQL Server, transactional command boundary) and `frontend/` (Angular shell, centralized `ApiUrl`). Every later CRM feature story depends on it.
- Shared contracts established by Story 01: `docs/api-conventions.md` (routes, DTO naming, ProblemDetails) and `docs/persistence-and-transactions.md` (`IUnitOfWork`, `ITransactionalCommand`). Feature stories must follow them.
