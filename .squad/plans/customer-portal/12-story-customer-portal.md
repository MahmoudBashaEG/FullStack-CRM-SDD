# Story 12 — Customer Portal — Ticket Submission, Request Tracking, Customer-Safe History, Help Center & CSAT Feedback

## Prerequisites

- **Story 11 completed and committed:** [../ai-features/11-story-ai-features-gemini-assistance.md](../ai-features/11-story-ai-features-gemini-assistance.md) (`AI-001`). At planning time `git log` showed `1fdb7e8 feat(ai): … (AI-001)` on top of `master`, and the working tree had only the untracked `.squad/stories/customer-portal/` and `.squad/plans/customer-portal/` folders. That resolves intake §2's open question: **the AI-001 commit exists.** **Run `git status` and `git log --oneline -3` before implementing. Stop and report if any application file is modified or if `AI-001` is not in `git log`.** Do not depend on the exact commit id.
- **Stories 02–03 completed:** [../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md) and [../customer-management/03-story-customer-profiles-contact-management.md](../customer-management/03-story-customer-profiles-contact-management.md). The **authoritative Identity ↔ CRM link** is `Customers.UserId`. It is a nullable FK to `AspNetUsers` with a **unique filtered index** (`CustomerConfiguration.cs` lines 22–24). Only the server sets it: at signup in the same transaction as the account (`RegisterCustomerCommand.cs` lines 33–36), or by the idempotent startup backfill (`CustomerProfileInitializer.cs` lines 28–45). Contact-only profiles have `UserId == null` and no login, so they cannot be claimed. `GET /me/customer-profile` already resolves the profile through `ICustomerRepository.FindDetailsByUserIdAsync(User.GetUserId())`. When none exists it returns 404 "Customer profile not found." (`GetMyCustomerProfileQuery.cs` lines 14–16). **This story resolves the profile the same way. It never matches by email.**
- **Stories 04–05 completed:** [../ticket-management/04-story-ticket-creation-management.md](../ticket-management/04-story-ticket-creation-management.md), [../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md). `Ticket.Create` always writes `Open` plus one `Created` activity (`Ticket.cs` lines 52–79). `TicketActivity` is append-only (`TicketActivity.cs`). `TicketRepository.Scope` is the **staff** boundary (`TicketRepository.cs` lines 82–84). **It is not changed.** Customer ownership is a separate filter, `Ticket.CustomerId == <resolved CRM customer id>`.
- **Story 09 completed:** [../sla-automation/09-story-sla-basic-automation.md](../sla-automation/09-story-sla-basic-automation.md). `ITicketCreationAutomation` ("Future ticket channels (web form, email) reuse this hook", `ITicketCreationAutomation.cs` lines 5–9) stages the SLA snapshot and at most one rule-based assignment on the same unit of work **before the single save** (`TicketCreationAutomation.cs` lines 16–55).
- **Story 10 completed:** [../knowledge-base/10-story-knowledge-base-management.md](../knowledge-base/10-story-knowledge-base-management.md). `KnowledgeArticleVisibility.Public` is documented as "eligible for the future Customer Portal (Published + Public only)" (`KnowledgeArticleVisibility.cs` lines 3–6). The safe renderer is `renderMarkdown` / `MarkdownView` (`markdown.ts`, `markdown-view.ts`). `docs/knowledge-base.md` line 8 (K2) requires the portal to use **its own contract**.
- **Stories 01–02 completed:** Mediator/CQRS pipeline, `GlobalExceptionHandler` (404/403/409/400 mapping), `AuthorizationPolicies.Customer` (`AuthorizationPolicies.cs` line 8, registered in `SecurityRegistration.cs` line 66), the global authenticated fallback policy (`SecurityRegistration.cs` line 62), `User.GetUserId()`, `roleGuard`, `NAV_ITEMS`.
- Applied migrations end with `20261018090955_AddKnowledgeBase`. **This story adds exactly one additive migration, `AddTicketFeedback`** (one new table, no change to existing tables).
- Tooling: .NET SDK 10 (`global.json` `10.0.400`), EF Core 10, Mediator (source-generated), FluentValidation, xUnit 2.9.3 (`Crm.UnitTests.csproj` line 22 references `Crm.Application` only), Angular / Material 22.2 (`@angular/material` already includes `MatButtonToggleModule`), Vitest via `ng test`. Baseline tests at `1fdb7e8`: **backend 11, frontend 11** (11 `[Fact]` files under `backend/tests`, 11 `it(` blocks under `frontend/src`). **No new NuGet or npm package.**
- Intake: `.squad/stories/customer-portal/PORTAL-001/intake.md` (`attachments/` is empty). Business decisions **CP1–CP5 are approved and not reopened.**
- Shared contracts touched (additive only):
  - Domain: +1 entity `TicketFeedback`.
  - `CrmDbContext`: +1 `DbSet`. +1 EF configuration. +1 migration.
  - `EfUnitOfWork.KnownUniqueIndexes`: +1 entry.
  - `Infrastructure/DependencyInjection.cs`: +2 repositories.
  - 2 new controllers under `/api/v1/portal/…`.
  - Doc-string edits in `KnowledgeArticleVisibility.cs` and in the `KnowledgeArticlesController` `Visibility` constant.
  - Angular: `app.routes.ts` (+1 route), `NAV_ITEMS` (+4 entries), `HomePage` (+1 Customer-only card), `PUBLIC_VISIBILITY_HINT` text, a new `features/portal/` folder.
  - Docs.
- **No change** to: `Ticket`, `TicketActivity`, `ITicketRepository`, `TicketRepository`, `CreateTicketCommand`, `TicketsController`, `IKnowledgeBaseRepository`, `KnowledgeBaseRepository`, SLA, automation, AI or collaboration code, or any existing test.

---

## Story Goal

Deliver all five **Customer Portal** capabilities from the core-features document (**Submit tickets, Track requests, View history, Access FAQs, Submit feedback**) for signed-in, linked Customers, under approved decisions CP1–CP5:

1. **Portal access (CP1):** a Customer-only area at `/portal` (Angular `roleGuard('Customer')`). Every `/api/v1/portal/**` endpoint requires `AuthorizationPolicies.Customer` (Agent/SuperAdmin ⇒ 403, anonymous ⇒ 401). Each request resolves the CRM `CustomerId` on the server from the JWT `sub` through `Customers.UserId`. No resolvable profile ⇒ 404 "Customer profile not found." No request carries `customerId`, `userId`, role or ownership fields.
2. **Submit tickets (CP2):** `POST /api/v1/portal/tickets` `{ subject, description, category }` → `SubmitPortalTicketCommand`. The server sets `CustomerId` (resolved), `CreatedByUserId` (the customer's own Identity id), `Open`, `Medium`, no assignee, and server timestamps. It then runs the **same** `ITicketCreationAutomation.ApplyAsync` hook (SLA snapshot plus optional rule-based assignment) and saves **once**. The domain writes the `Created` activity. Extra JSON fields (priority, status, assignee, customerId) are not on the DTO and are ignored.
3. **Track requests (CP3):** `GET /api/v1/portal/tickets` lists **every** ticket whose `CustomerId` is the caller's profile, whoever created it. It is filtered in SQL, newest first, one-based paging, with optional subject/exact-id search and status filter. `GET /api/v1/portal/tickets/{id}` returns customer-safe details: subject, description, category, status, created time, feedback state. Another customer's id ⇒ **404** (the same text as a missing id).
4. **View history (CP3):** `GET /api/v1/portal/tickets/{id}/timeline` projects **only** `Created` and `StatusChanged` activity into `{ kind, fromStatus, status, occurredAtUtc, isRecorded }`, oldest first. Legacy tickets without a `Created` event get one synthesized "submitted" entry from `Tickets.CreatedAtUtc` (`isRecorded: false`). No actor, assignee, reason, field, SLA or escalation data is ever selected.
5. **Access FAQs / Help Center (CP4):** `GET /api/v1/portal/help-center/articles` (search on title/summary/body, `type`, `categoryId`, paging), `GET …/articles/{id}`, and `GET …/categories`. **Every query has `Status == Published && Visibility == Public` in SQL**, including detail by guessed id (⇒ 404). Responses carry no author, publisher, status, visibility or internal metadata. The body renders through the Story 10 `MarkdownView`.
6. **Submit feedback (CP5):** `PUT /api/v1/portal/tickets/{id}/feedback` `{ rating: 1–5, comment?: ≤ 1000 }` creates or updates the **single** `TicketFeedback` row of an owned ticket whose **current** status is `Resolved` or `Closed`. The row has a unique index on `TicketId` (`UX_TicketFeedback_TicketId` ⇒ 409 on a racing duplicate insert), a check constraint on `Rating`, and rowversion (`Version`) ⇒ 409 on overlapping edits. Reopened tickets keep their feedback **read-only** until they are Resolved/Closed again. Feedback never touches the ticket, its activity, SLA or assignment.
7. Exactly **one** new backend unit test and **one** new frontend unit test, a new `docs/customer-portal.md`, small doc updates, and a manual API/SQL/browser matrix mapped to intake §13 checks 1–20.

**Not in scope** (do **not** build): anonymous or guest portal or Help Center; contact-only profile claiming or email matching; customer replies, comments, messages, chat, email/SMS/WhatsApp (**Communication Channels stays last**); customer edit, close, reopen or escalate; priority, assignee or SLA shown to the customer; customer-facing Gemini or any AI call; related-article suggestions on the ticket form; customer article ratings or comments; CSAT reports, aggregates or staff feedback screens (future **Reports & Management**); rate limiting of submissions; new identity verification; E2E, integration or SQL test projects.

### Conflicts and decisions (surface these in the implementation report)

| # | Intake point | Resolution in this plan |
|---|---|---|
| P1 | §2/§4: find the authoritative Identity ↔ CRM linkage; never claim by email. | **Verified:** `Customers.UserId`, a unique filtered FK to `AspNetUsers` set only by signup (transactional) and the startup backfill. Resolve it with `ICustomerRepository.FindDetailsByUserIdAsync(userId)`. The new static helper `PortalCustomer.ResolveIdAsync` throws `NotFoundException("Customer profile not found.")` when it is null, the same text and status as `GetMyCustomerProfileQuery.cs` line 16. In practice a Customer account always has a profile (signup and backfill), so this is a defensive path. The UI shows "Your account is not linked to a customer profile. Contact support." No `ForbiddenException`: its doc comment (`ForbiddenException.cs` line 3) reserves 403 for "records the caller can already see but may not change". |
| P2 | §6/§11: reuse the ticket-created pathway; do not invent a staff actor or duplicate logic. | `CreateTicketCommand` requires a **staff** `TicketActor` (`TicketActor.cs` lines 3–7). Its `ResolveInitialAssigneeAsync` would **self-assign** any non-SuperAdmin actor (`CreateTicketCommand.cs` lines 70–77), so a Customer actor would end up as the ticket's "assigned agent". Wrong. **New `SubmitPortalTicketCommand`** in `Crm.Application/Portal/SubmitTicket/`. It performs exactly the core sequence of `CreateTicketCommandHandler` lines 44–57: `Ticket.Create(…, TicketPriority.Medium, createdByUserId: <customer user id>, assignedAgentUserId: null, now)` → `ITicketCreationAutomation.ApplyAsync(ticket)` → `tickets.Add` → **one** `SaveChangesAsync`. SLA, assignment and the activity rules stay in their single existing homes (`Ticket.Create`, `TicketCreationAutomation`), so nothing is duplicated or run twice. `ITicketRepository` and `CreateTicketCommand` are **unchanged**. `CreatedByUserId` is the customer's own `AspNetUsers.Id`: the FK accepts any account (`TicketConfiguration.cs` line 26), and staff screens show the customer's name as creator and as actor of the `Created` event, which is truthful. |
| P3 | §6: "Automatic assignment might run asynchronously under the existing background service." | **Code reality:** rule-based assignment runs **synchronously inside creation**, before the single save (`TicketCreationAutomation.cs` lines 32–55; `CreateTicketCommand.cs` lines 12–15). `SlaAutomationWorker` only evaluates breaches and alerts later. A portal ticket can therefore come back already assigned. The customer never sees it either way, because no assignee is in any portal DTO. The confirmation text does not promise an Agent ("Our team will review it."). |
| P4 | §4/§11: customer-specific projections; do not reuse staff DTOs or weaken staff scope. | **New read ports**: `IPortalTicketRepository` and `IHelpCenterRepository` (precedent: `IAiKnowledgeSearch`, AI-001 D7). Adding methods to `ITicketRepository` / `IKnowledgeBaseRepository` would break the hand-written fakes in 5 existing tests (`CreateTicketCommandHandlerTests`, `TransitionTicketStatusCommandHandlerTests`, `SuggestTicketCategoryQueryHandlerTests`, `ChangeKnowledgeArticleStatusCommandHandlerTests`, `CreateCustomerCommandHandlerTests`). Also, the staff `Project` joins agent, escalator and responder names (`TicketRepository.cs` lines 133–170). The portal queries **select only the safe columns**, so sensitive data never leaves SQL. Ownership `t.CustomerId == customerId` is in **every** statement, never a broad read filtered later. |
| P5 | §8: project safe events only; legacy tickets; no invented transitions. | Only `TicketActivityType.Created` and `StatusChanged` rows are selected, with columns `Id, Type, OccurredAtUtc, OldValue, NewValue`. `ActorUserId`, `Field`, `From/ToAgentUserId` and `Reason` are **not selected**. `StatusChanged` stores `OldValue`/`NewValue` as status names (`TicketActivity.cs` lines 34–35). Parse them with `Enum.TryParse<TicketStatus>` and skip unparseable rows. If no `Created` row exists (pre-TICK-002 tickets, `ticket-activity-list.ts` line 67), prepend one `Submitted` entry at `Tickets.CreatedAtUtc` with `isRecorded: false`. A real `Created` row ⇒ no synthesized duplicate. The read is bounded to the **newest 200** events (`PortalRules.MaxTimelineEvents`), re-sorted oldest first with `Id` as the tie-breaker. |
| P6 | §10/§11: one editable record per ticket, uniqueness in the DB, sensible conflict. | `TicketFeedback` (`long Id` identity, `TicketId` FK `Restrict`, `Rating int`, `Comment nvarchar(1000) NULL`, `CreatedAtUtc`, `UpdatedAtUtc`, `Version rowversion`). It has a unique index `UX_TicketFeedback_TicketId`, mapped in `EfUnitOfWork.KnownUniqueIndexes` to 409 "Feedback for this request was already submitted. Reload to see it.", and a check constraint `CK_TicketFeedback_Rating` `[Rating] BETWEEN 1 AND 5` (precedent `QuickReplyTemplateConfiguration.cs` lines 12–14). **No `CustomerId` column**: ownership comes through `Tickets.CustomerId` (§10 "only if actually necessary"). **One upsert endpoint, `PUT …/{id}/feedback`**, returns the refreshed `PortalTicketResponse`. The saved feedback travels inside `GET /portal/tickets/{id}` (`feedback`, `canSubmitFeedback`), so no separate GET exists. |
| P7 | §10: reopened tickets with feedback. | The record is **kept** (never deleted), `canSubmitFeedback` is `false` while the status is `Open`/`InProgress`, and the UI shows the saved rating read-only with "You can update your feedback after this request is resolved again." `PUT` ⇒ 409 "Feedback can be given once the request is resolved or closed." Editing resumes automatically once the ticket is Resolved/Closed again. No ticket is reopened or changed by feedback. |
| P8 | §9: type/category filters; expose only safe category names. | The staff `GET /knowledge-base/categories` is `Staff`-only and returns `isActive`/`updatedAtUtc`. **`GET /portal/help-center/categories`** returns `{ id, name }` only for categories that have **at least one** `Published` + `Public` article (inactive categories included when they still hold public articles; empty categories never listed). Ordered by `Name, Id`. |
| P9 | §12: route names to verify. | Angular: `/portal` (landing), `/portal/tickets`, `/portal/new-ticket`, `/portal/tickets/:id`, `/portal/help-center`, `/portal/help-center/:id`. **`/portal/new-ticket`** instead of `/portal/tickets/new`: `NAV_ITEMS` uses `exact: false` prefix matching (`navigation.ts` lines 13–26 / shell `routerLinkActive`), so "My Tickets" would also highlight on the New Ticket page. API: `/api/v1/portal/tickets[/{id}[/timeline|/feedback]]` and `/api/v1/portal/help-center/{articles[/{id}]|categories}`. `LowercaseUrls` is on (`Crm.Api/DependencyInjection.cs` line 19). |
| P10 | §7: list fields; hide internal data. | Summary: `id, subject, category, status, createdAtUtc`. Detail adds `description`, `canSubmitFeedback`, `feedback`. **Not exposed:** `priority`, `updatedAtUtc` (it moves on internal edits and assignment), customer name/email, creator, assignee, escalation, SLA. |
| P11 | §9: Public visibility text in staff UI. | `PUBLIC_VISIBILITY_HINT` (`knowledge-base-models.ts` line 55) says "Nothing is visible to customers today." After this story that is false. Change it to "Published + Public articles appear in the Customer Help Center for signed-in customers." Update the `KnowledgeArticleVisibility` doc comment (lines 3–6) and the `KnowledgeArticlesController` `Visibility` constant (line 24, "Customer Portal is a future story") to match. Text only, no behaviour change. |

### Recorded decisions

| Concern | Decision |
|---|---|
| Naming | Application `Crm.Application.Portal` (use cases under `Crm.Application/Portal/<UseCase>/`), ports `IPortalTicketRepository` / `IHelpCenterRepository` in `Crm.Application.Abstractions.Persistence`, Infrastructure `PortalTicketRepository` / `HelpCenterRepository` in `Persistence/Repositories/`. API `Crm.Api.Contracts.Portal`, `PortalTicketsController`, `PortalHelpCenterController`. Angular `frontend/src/app/features/portal/`. Domain entity `Crm.Domain.Tickets.TicketFeedback`, table `TicketFeedback`. |
| Identity input | Every portal message carries `Guid UserId` = `User.GetUserId()` (JWT `sub`), like `GetMyCustomerProfileQuery(Guid UserId)`. The handler calls `PortalCustomer.ResolveIdAsync(customers, userId)` first, on **every** request. No portal actor record, no role flag. |
| CQRS shape | Queries: `ListPortalTicketsQuery`, `GetPortalTicketQuery`, `GetPortalTicketTimelineQuery`, `ListHelpCenterArticlesQuery`, `GetHelpCenterArticleQuery`, `ListHelpCenterCategoriesQuery` (no `IUnitOfWork`). Commands: `SubmitPortalTicketCommand`, `SavePortalTicketFeedbackCommand`. Each saves **once** ⇒ **not** `ITransactionalCommand` (`ITransactionalCommand.cs` lines 3–8). |
| Errors | Not owned / missing ticket ⇒ `NotFoundException($"Ticket '{id}' was not found.")` (`GetTicketQuery.cs` line 13 text). Missing or unpublished/internal article ⇒ `NotFoundException($"Help article '{id}' was not found.")`. Feedback on an ineligible status ⇒ `ConflictException`. Validation ⇒ 400 `ValidationProblemDetails`. Race on first insert ⇒ 409 (unique index). Overlapping edit ⇒ 409 (rowversion, `EfUnitOfWork.cs` lines 25–29). |
| Limits (`PortalRules`) | `FeedbackCommentMaxLength` 1000 · `MaxTimelineEvents` 200 · `MinRating` 1 · `MaxRating` 5 (aliases of `TicketFeedback.MinRating/MaxRating`). Subject/description/search reuse `TicketRules` (200 / 4000 / 100). Help search reuses `KnowledgeBaseRules.SearchMaxLength` (100). Paging reuses `Paging` (default 20, clamp 100). |
| Ordering | Tickets: `CreatedAtUtc DESC, Id DESC` (existing index `(CustomerId, CreatedAtUtc, Id)`, `TicketConfiguration.cs` line 47). Articles: `PublishedAtUtc DESC, Id DESC` (`PublishedAtUtc ?? UpdatedAtUtc` in the projection, always set for Published). Timeline: `OccurredAtUtc ASC, Id ASC` for display. |
| Text safety | Ticket subject/description and feedback comment render with text interpolation and `white-space: pre-wrap` (`.user-text`, `productivity-page.scss` lines 50–54), **never** `[innerHTML]`. Only article bodies use `MarkdownView` (`markdown.ts`: raw HTML escaped, `javascript:` links dropped, images reduced to alt text). |
| Caching | No root store and no `localStorage`. Portal data lives only in page-component signals, which are destroyed on navigation and on `endSession()` (`auth-service.ts` lines 71–75 navigates to `/sign-in`). The detail page **clears** its ticket/timeline signals before loading each new `:id` (`paramMap` + `switchMap`), so a reused component never shows the previous ticket. |
| UI placement | Nav (Customer only, after Home): "Support Portal" (`support_agent`, `/portal`, exact), "My Tickets" (`confirmation_number`, `/portal/tickets`), "New Ticket" (`add_circle`, `/portal/new-ticket`, exact), "Help Center" (`help_center`, `/portal/help-center`). `HomePage` gets one Customer-only card linking to the portal. The staff navigation is unchanged. |

---

## Context — Read These Files First

1. `.squad/stories/customer-portal/PORTAL-001/intake.md` — the contract: §3 (CP1–CP5), §4 role table and identity rules, §6–§10 the five capabilities, §11 API/persistence, §12 Angular UX, §13 the 20 manual checks and the 1+1 test policy, §14 out of scope.
2. [../ai-features/11-story-ai-features-gemini-assistance.md](../ai-features/11-story-ai-features-gemini-assistance.md) and [../knowledge-base/10-story-knowledge-base-management.md](../knowledge-base/10-story-knowledge-base-management.md) — tone, "Conflicts" / "Recorded decisions" tables, Verification and Done Criteria style. Story 10 Verification step 3 holds the exact `dotnet ef` commands.
3. `backend/src/Crm.Domain/Customers/Customer.cs` lines 5–25 (`UserId`, `IsRegistered`). `backend/src/Crm.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs` lines 22–24 (1:1 FK + unique filtered index). `backend/src/Crm.Application/Auth/RegisterCustomer/RegisterCustomerCommand.cs` lines 12–38. `backend/src/Crm.Application/Profile/GetMyCustomerProfile/GetMyCustomerProfileQuery.cs` lines 8–16 (**the identity-resolution template**). `backend/src/Crm.Application/Abstractions/Persistence/ICustomerRepository.cs` line 19 (`FindDetailsByUserIdAsync`). `backend/src/Crm.Application/Customers/CustomerAccountSync.cs` lines 6–20 (static-extension helper style).
4. `backend/src/Crm.Api/Controllers/MeController.cs` lines 46–50 — `[Authorize(Policy = AuthorizationPolicies.Customer)]` + `EndpointDescription("Requires role: Customer (…)")` + `User.GetUserId()`. **Mirror this in both portal controllers.** `backend/src/Crm.Api/Authentication/AuthorizationPolicies.cs` line 8 and `SecurityRegistration.cs` lines 61–66.
5. `backend/src/Crm.Application/Tickets/CreateTicket/CreateTicketCommand.cs` lines 12–62 — **the creation sequence to mirror** (lines 44–57) and why it cannot be reused (lines 64–92). `CreateTicketValidator.cs` lines 8–16 (rules to reuse). `backend/src/Crm.Application/Common/Validation/TicketRules.cs` lines 5–7.
6. `backend/src/Crm.Domain/Tickets/Ticket.cs` lines 52–79 (`Create` ⇒ `Open` + `Created` activity), 203–224 (`TransitionTo` ⇒ `StatusChanged`). `TicketActivity.cs` lines 25–26 and 34–35 (what `Created` and `StatusChanged` store). `TicketActivityType.cs` lines 4–14. `TicketStatus.cs` lines 7–13. `TicketWorkflow.cs` lines 6–14 (`Closed → Open` = reopen, `Resolved → InProgress` = resume). `TicketCategory.cs` lines 4–10.
7. `backend/src/Crm.Application/Sla/ITicketCreationAutomation.cs` lines 5–13 and `TicketCreationAutomation.cs` lines 16–55 (synchronous SLA + assignment, P3).
8. `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs` lines 25–80 (`ListAsync`: filter order, GUID-or-subject search lines 64–71, `CountAsync` then `Skip/Take`), 172–211 (`ListActivityAsync`: ownership in every statement). `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketActivityConfiguration.cs` lines 16 and 27 (`Type` stored by name; index `(TicketId, OccurredAtUtc, Id)`). `TicketConfiguration.cs` line 47 (customer index).
9. `backend/src/Crm.Infrastructure/Persistence/Repositories/KnowledgeBaseRepository.cs` lines 39–86 (`ListArticlesAsync`: filters, LIKE search on title/summary/body line 69, `orderby` before the record projection lines 74–83) and 108–121 (staff `Visible`, **not reused**: it includes Internal). `backend/src/Crm.Infrastructure/Persistence/Repositories/AiKnowledgeSearch.cs` (a separate read-only repository over `KnowledgeArticles`, the precedent for `HelpCenterRepository`). `backend/src/Crm.Domain/KnowledgeBase/KnowledgeArticle.cs` lines 14–30, `KnowledgeArticleVisibility.cs` lines 3–11.
10. `backend/src/Crm.Domain/KnowledgeBase/TicketKnowledgeArticleLink.cs` and `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketKnowledgeArticleLinkConfiguration.cs` lines 9–26 — **the entity/configuration template for `TicketFeedback`** (identity `long Id`, `Restrict` FK to `Ticket`, named unique index constant). `QuickReplyTemplateConfiguration.cs` lines 12–14 (check-constraint syntax). `KnowledgeArticleConfiguration.cs` line 22 (`Version` rowversion).
11. `backend/src/Crm.Infrastructure/Persistence/EfUnitOfWork.cs` lines 10–17 (`KnownUniqueIndexes`), 25–33 (409 mapping). `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` lines 22–41 (`DbSet` list). `backend/src/Crm.Infrastructure/DependencyInjection.cs` lines 41–58 (repository registrations; add after line 58).
12. `backend/src/Crm.Api/Controllers/TicketsController.cs` lines 30–53 (paged list → `PagedResponse`, `CreatedAtAction`, description style) and `KnowledgeArticlesController.cs` lines 24–47. `backend/src/Crm.Api/Contracts/Common/PagedResponse.cs`. `backend/src/Crm.Api/Contracts/KnowledgeBase/KnowledgeBaseContracts.cs` (record + `static From` style). `backend/src/Crm.Api/DependencyInjection.cs` lines 16–18 (enums as strings, `allowIntegerValues: false`).
13. `backend/src/Crm.Application/Common/Exceptions/NotFoundException.cs`, `ConflictException.cs` lines 3–4, `ForbiddenException.cs` line 3. `backend/src/Crm.Application/Common/Paging/PagedResult.cs` lines 4–11. `backend/src/Crm.Application/KnowledgeBase/ListArticles/ListKnowledgeArticlesQuery.cs` lines 11–43 (query + validator in one file style).
14. `backend/tests/Crm.UnitTests/Tickets/CreateTicketCommandHandlerTests.cs` (110 lines) — **the template for the single new backend test**: naming, hand-written fakes with `NotSupportedException` for unused members (lines 51–109), `TimeProvider.System`.
15. `frontend/src/app/app.routes.ts` lines 75–80 (insert after the `ai-assistant` route, before the comment on line 80). `frontend/src/app/core/layout/navigation.ts` lines 13–26 (`NAV_ITEMS`). `frontend/src/app/core/auth/auth-guards.ts` lines 14–19 (`roleGuard`). `frontend/src/app/core/layout/shell/shell.ts` lines 36–39 (role filtering). `frontend/src/app/core/auth/auth-service.ts` lines 19–21, 67–75.
16. `frontend/src/app/features/tickets/ticket-list-page.ts` lines 60–157 (`Subject` + `switchMap` + per-request `catchError`, debounced filters, one-based paging) and `ticket-create-page.ts` lines 43–117 (form, `submitting` guard, snackbar, navigate). `ticket-create-page.html` lines 14–40 (subject/description/category fields and `mat-error` texts). `ticket-models.ts` lines 4–12 (`TICKET_CATEGORIES`, `TICKET_STATUSES`, types), 132–145 (`statusLabel`, `statusTone`).
17. `frontend/src/app/features/knowledge-base/knowledge-base-page.html` lines 1–88 (toolbar, rows list, paginator), `knowledge-article-page.ts` lines 31 and 76–83 (404 ⇒ "not available"), `markdown-view.ts` (reuse as is), `knowledge-base-models.ts` lines 3–18 (`ARTICLE_TYPES`, `articleTypeLabel`) and line 55 (`PUBLIC_VISIBILITY_HINT`, P11).
18. `frontend/src/app/shared/ui/productivity-page.scss` (shared `.toolbar`, `.rows`, `.row`, `.row-title`, `.meta`, `.user-text`, `.hint`, `.error`, `.stack`, `.form`, `.actions`). `frontend/angular.json` lines 44–47 (component style budget 4 kB warning).
19. `frontend/src/app/features/knowledge-base/markdown.spec.ts` (pure-function Vitest spec, no TestBed) — **the pattern for the single new frontend test.**
20. `frontend/src/app/features/home/home-page.ts` lines 34–54 and `home-page.html` lines 1–30 (where the Customer card goes).
21. Docs: `docs/identity-and-access.md` lines 5–36 (matrix) and 41–83 (routes). `docs/knowledge-base.md` line 8 (K2). `docs/ticket-management.md` line 3 and line 161. `README.md` line 3.

---

## Product rules (from story)

### Role matrix (server-enforced)

| Operation | Customer (linked) | Agent | SuperAdmin | Anonymous |
|---|---|---|---|---|
| `GET/POST /portal/tickets` | ✔ own profile only | 403 | 403 | 401 |
| `GET /portal/tickets/{id}`, `/timeline` | ✔ own (others 404) | 403 | 403 | 401 |
| `PUT /portal/tickets/{id}/feedback` | ✔ own **and** Resolved/Closed (else 404 / 409) | 403 | 403 | 401 |
| `GET /portal/help-center/*` | ✔ Published + Public only (else 404) | 403 | 403 | 401 |
| Staff ticket, KB, AI, admin endpoints | 403 (unchanged) | unchanged | unchanged | 401 |
| Customer account without a profile | 404 "Customer profile not found." on every portal route | — | — | — |

### Current vs new behaviour

| Area | Current | New (PORTAL-001) |
|---|---|---|
| Customer navigation | Home, Profile | + Support Portal, My Tickets, New Ticket, Help Center |
| Ticket creation | Staff only (`POST /tickets`) | + Customer `POST /portal/tickets` (Open, Medium, unassigned before automation, same SLA/assignment hook, same `Created` event) |
| Staff ticket screens | — | **Unchanged.** Portal tickets appear like any other. "Created by" shows the customer |
| Ticket activity | — | **Unchanged.** Portal reads a filtered projection only |
| KB `Public` visibility | Staff-only meaning ("future portal") | Published + Public articles are readable by signed-in Customers. Staff KB rules unchanged |
| CSAT | — | New `TicketFeedback` table, one row per ticket, never alters the ticket |
| AI | Staff only | **Unchanged**. No portal AI call |

---

## Backend Tasks

### 1 — Domain: `TicketFeedback`

**Create file: `backend/src/Crm.Domain/Tickets/TicketFeedback.cs`**

```csharp
using Crm.Domain.Common;

namespace Crm.Domain.Tickets;

/// <summary>
/// PORTAL-001 CSAT: at most one record per ticket (unique TicketId), created and edited only by the owning customer while the
/// ticket is Resolved or Closed. Ownership comes from Tickets.CustomerId (no copy). Never deleted; never changes the ticket.
/// Id is a store-generated identity.
/// </summary>
public sealed class TicketFeedback
{
    public const int MinRating = 1;
    public const int MaxRating = 5;

    private TicketFeedback() { } // EF Core

    public long Id { get; private set; }
    public Guid TicketId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>Feedback can be created or edited only while the ticket's current status is Resolved or Closed (CP5).</summary>
    public static bool IsEligible(TicketStatus status) => status is TicketStatus.Resolved or TicketStatus.Closed;

    public static TicketFeedback Create(Guid ticketId, int rating, string? comment, DateTimeOffset now)
    {
        EnsureRating(rating);
        return new() { TicketId = ticketId, Rating = rating, Comment = comment, CreatedAtUtc = now, UpdatedAtUtc = now };
    }

    /// <summary>false = nothing changed (no save, no timestamp change).</summary>
    public bool Update(int rating, string? comment, DateTimeOffset now)
    {
        EnsureRating(rating);
        if (Rating == rating && string.Equals(Comment, comment, StringComparison.Ordinal))
        {
            return false;
        }

        Rating = rating;
        Comment = comment;
        UpdatedAtUtc = now;
        return true;
    }

    // Backstop only: the validator returns 400 first.
    private static void EnsureRating(int rating)
    {
        if (rating is < MinRating or > MaxRating)
        {
            throw new DomainException($"A rating must be between {MinRating} and {MaxRating}.");
        }
    }
}
```

**No change** to `Ticket.cs`: feedback is not part of the ticket aggregate, so saving feedback never bumps `Tickets.Version` or `UpdatedAtUtc`.

### 2 — Application: rules, read models, ports, identity helper

**Create file: `backend/src/Crm.Application/Common/Validation/PortalRules.cs`** (style of `KnowledgeBaseRules.cs`):

```csharp
using Crm.Domain.Tickets;

namespace Crm.Application.Common.Validation;

public static class PortalRules
{
    public const int FeedbackCommentMaxLength = 1000;
    public const int MinRating = TicketFeedback.MinRating;
    public const int MaxRating = TicketFeedback.MaxRating;
    /// <summary>Newest status events kept in one timeline read (CP3 bounded read).</summary>
    public const int MaxTimelineEvents = 200;
}
```

**Create file: `backend/src/Crm.Application/Portal/PortalDetails.cs`**

```csharp
using Crm.Domain.KnowledgeBase;
using Crm.Domain.Tickets;

namespace Crm.Application.Portal;

// Customer-safe read models (P4/P10). No priority, assignee, creator, escalation, SLA, staff names or user ids — by construction.

public sealed record PortalTicketSummary(Guid Id, string Subject, TicketCategory Category, TicketStatus Status, DateTimeOffset CreatedAtUtc);

public sealed record PortalFeedbackDetails(int Rating, string? Comment, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record PortalTicketDetails(
    Guid Id, string Subject, string Description, TicketCategory Category, TicketStatus Status, DateTimeOffset CreatedAtUtc,
    PortalFeedbackDetails? Feedback)
{
    public bool CanSubmitFeedback => TicketFeedback.IsEligible(Status);
}

/// <summary>Search already trimmed (subject, or exact ticket id). Status null = any.</summary>
public sealed record PortalTicketListFilter(string? Search, TicketStatus? Status);

/// <summary>Raw safe columns of one Created/StatusChanged activity row (P5). Never actor, field, agents or reason.</summary>
public sealed record PortalActivityEvent(long Id, TicketActivityType Type, DateTimeOffset OccurredAtUtc, string? OldValue, string? NewValue);

public sealed record PortalTicketHistory(DateTimeOffset TicketCreatedAtUtc, IReadOnlyList<PortalActivityEvent> Events);

public enum PortalTimelineEntryKind
{
    Submitted = 0,
    StatusChanged = 1,
}

/// <summary>IsRecorded false = synthesized from Tickets.CreatedAtUtc for a ticket that pre-dates activity tracking.</summary>
public sealed record PortalTimelineEntry(
    PortalTimelineEntryKind Kind, TicketStatus? FromStatus, TicketStatus Status, DateTimeOffset OccurredAtUtc, bool IsRecorded);

public sealed record HelpCenterCategory(Guid Id, string Name);

/// <summary>List row — never the body, never author/status/visibility.</summary>
public sealed record HelpCenterArticleSummary(
    Guid Id, string Title, string? Summary, KnowledgeArticleType Type, Guid CategoryId, string CategoryName, DateTimeOffset PublishedAtUtc);

public sealed record HelpCenterArticleDetails(
    Guid Id, string Title, string? Summary, string Body, KnowledgeArticleType Type, Guid CategoryId, string CategoryName,
    DateTimeOffset PublishedAtUtc, DateTimeOffset UpdatedAtUtc);

/// <summary>Search already trimmed (title, summary or body).</summary>
public sealed record HelpCenterArticleFilter(string? Search, KnowledgeArticleType? Type, Guid? CategoryId);
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IPortalTicketRepository.cs`**

```csharp
using Crm.Application.Common.Paging;
using Crm.Application.Portal;
using Crm.Domain.Tickets;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// PORTAL-001 customer scope (P4): every read filters Tickets.CustomerId == customerId in SQL, in every statement.
/// Separate from ITicketRepository (staff scope), which is unchanged. Not owned or missing ⇒ null.
/// AddFeedback only stages; the caller saves through IUnitOfWork.
/// </summary>
public interface IPortalTicketRepository
{
    /// <summary>Ordered CreatedAtUtc DESC, Id DESC.</summary>
    Task<PagedResult<PortalTicketSummary>> ListAsync(
        Guid customerId, PortalTicketListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<PortalTicketDetails?> FindAsync(Guid ticketId, Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>Created + StatusChanged events only, the newest maxEvents, returned oldest first (OccurredAtUtc, Id).</summary>
    Task<PortalTicketHistory?> FindHistoryAsync(Guid ticketId, Guid customerId, int maxEvents, CancellationToken cancellationToken = default);

    void AddFeedback(TicketFeedback feedback);

    /// <summary>Tracked, for updates. The caller has already verified ownership.</summary>
    Task<TicketFeedback?> GetFeedbackAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IHelpCenterRepository.cs`**

```csharp
using Crm.Application.Common.Paging;
using Crm.Application.Portal;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// PORTAL-001 CP4: Status == Published AND Visibility == Public in SQL for every query (list, detail, categories).
/// Separate from IKnowledgeBaseRepository (staff visibility includes Internal), which is unchanged. Read-only.
/// </summary>
public interface IHelpCenterRepository
{
    /// <summary>Ordered PublishedAtUtc DESC, Id DESC. Rows never contain the body.</summary>
    Task<PagedResult<HelpCenterArticleSummary>> ListArticlesAsync(
        HelpCenterArticleFilter filter, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Null when missing, not Published or not Public.</summary>
    Task<HelpCenterArticleDetails?> FindArticleAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Only categories holding at least one Published + Public article. Ordered Name, Id.</summary>
    Task<IReadOnlyList<HelpCenterCategory>> ListCategoriesAsync(CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Portal/PortalCustomer.cs`** (static-extension style of `CustomerAccountSync.cs`):

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;

namespace Crm.Application.Portal;

internal static class PortalCustomer
{
    /// <summary>
    /// P1: the caller's CRM customer id from the validated JWT subject through Customers.UserId (unique, server-set).
    /// Never by email, never from the request. Same 404 as GET /me/customer-profile when no profile is linked.
    /// </summary>
    public static async Task<Guid> ResolveIdAsync(this ICustomerRepository customers, Guid userId, CancellationToken cancellationToken) =>
        (await customers.FindDetailsByUserIdAsync(userId, cancellationToken))?.Id
            ?? throw new NotFoundException("Customer profile not found.");
}
```

### 3 — Application: ticket use cases

**Create file: `backend/src/Crm.Application/Portal/SubmitTicket/SubmitPortalTicketCommand.cs`** (command, handler and validator in one file, like `ListKnowledgeArticlesQuery.cs`):

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Crm.Application.Sla;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.Portal.SubmitTicket;

/// <summary>
/// CP2. Single save — not transactional: the same sequence as CreateTicketCommandHandler (Ticket.Create → ITicketCreationAutomation →
/// Add → one save), so the Created activity, SLA snapshot and optional rule-based assignment commit together exactly once (P2/P3).
/// Customer, creator, status, priority and assignee come from the server only. Category is nullable so a missing field fails validation.
/// </summary>
public sealed record SubmitPortalTicketCommand(Guid UserId, string Subject, string Description, TicketCategory? Category)
    : ICommand<PortalTicketDetails>;

public sealed class SubmitPortalTicketCommandHandler(
    ICustomerRepository customers,
    ITicketRepository tickets,
    ITicketCreationAutomation automation,
    IPortalTicketRepository portal,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SubmitPortalTicketCommand, PortalTicketDetails>
{
    public async ValueTask<PortalTicketDetails> Handle(SubmitPortalTicketCommand command, CancellationToken cancellationToken)
    {
        var customerId = await customers.ResolveIdAsync(command.UserId, cancellationToken);

        var ticket = Ticket.Create(
            Guid.CreateVersion7(),
            customerId,
            command.Subject.Trim(),
            command.Description.Trim(),
            command.Category!.Value, // validator: NotNull
            TicketPriority.Medium,   // CP2: never chosen by the customer
            command.UserId,          // the customer's own account; no staff actor is impersonated
            assignedAgentUserId: null,
            timeProvider.GetUtcNow());

        await automation.ApplyAsync(ticket, cancellationToken); // SLA snapshot + at most one rule-based assignment (SLA-001)
        tickets.Add(ticket);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (await portal.FindAsync(ticket.Id, customerId, cancellationToken))!; // always owned: just created for this customer
    }
}

public sealed class SubmitPortalTicketValidator : AbstractValidator<SubmitPortalTicketCommand>
{
    public SubmitPortalTicketValidator()
    {
        RuleFor(c => c.Subject).NotEmpty().MaximumLength(TicketRules.SubjectMaxLength);         // NotEmpty also rejects whitespace-only
        RuleFor(c => c.Description).NotEmpty().MaximumLength(TicketRules.DescriptionMaxLength);
        RuleFor(c => c.Category).NotNull().IsInEnum();
    }
}
```

**Create file: `backend/src/Crm.Application/Portal/ListTickets/ListPortalTicketsQuery.cs`**

```csharp
public sealed record ListPortalTicketsQuery(
    Guid UserId, string? Search, TicketStatus? Status, int Page = 1, int PageSize = Paging.DefaultPageSize)
    : IQuery<PagedResult<PortalTicketSummary>>;
```

Handler (`ListPortalTicketsQueryHandler(ICustomerRepository customers, IPortalTicketRepository portal)`): `customerId = await customers.ResolveIdAsync(query.UserId, …)`. Then trim the search (`string.IsNullOrWhiteSpace ⇒ null`), `pageSize = Math.Min(query.PageSize, Paging.MaxPageSize)`, and `return await portal.ListAsync(customerId, new PortalTicketListFilter(search, query.Status), query.Page, pageSize, …)`. Mirror `ListTicketsQuery.cs` lines 23–31. Validator in the same file, as `ListTicketsValidator.cs` lines 9–17: `Page` `InclusiveBetween(1, Paging.MaxPage)`, `PageSize` `>= 1`, `Search` `MaximumLength(TicketRules.SearchMaxLength)`, `Status` `IsInEnum()`.

**Create file: `backend/src/Crm.Application/Portal/GetTicket/GetPortalTicketQuery.cs`**

```csharp
public sealed record GetPortalTicketQuery(Guid UserId, Guid Id) : IQuery<PortalTicketDetails>;
// Handler: customerId = ResolveIdAsync; portal.FindAsync(query.Id, customerId) ?? throw new NotFoundException($"Ticket '{query.Id}' was not found.");
```

**Create file: `backend/src/Crm.Application/Portal/GetTimeline/GetPortalTicketTimelineQuery.cs`**

```csharp
public sealed record GetPortalTicketTimelineQuery(Guid UserId, Guid Id) : IQuery<IReadOnlyList<PortalTimelineEntry>>;

public sealed class GetPortalTicketTimelineQueryHandler(ICustomerRepository customers, IPortalTicketRepository portal)
    : IQueryHandler<GetPortalTicketTimelineQuery, IReadOnlyList<PortalTimelineEntry>>
{
    public async ValueTask<IReadOnlyList<PortalTimelineEntry>> Handle(GetPortalTicketTimelineQuery query, CancellationToken cancellationToken)
    {
        var customerId = await customers.ResolveIdAsync(query.UserId, cancellationToken);
        var history = await portal.FindHistoryAsync(query.Id, customerId, PortalRules.MaxTimelineEvents, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{query.Id}' was not found.");
        return PortalTimeline.Build(history);
    }
}
```

**Create file: `backend/src/Crm.Application/Portal/PortalTimeline.cs`** (pure, static):

```csharp
using Crm.Domain.Tickets;

namespace Crm.Application.Portal;

internal static class PortalTimeline
{
    /// <summary>
    /// P5: Created ⇒ Submitted (recorded); StatusChanged with parseable names ⇒ StatusChanged. Anything else is ignored.
    /// No Created row (pre-TICK-002 ticket) ⇒ one Submitted entry from Tickets.CreatedAtUtc, IsRecorded false. Never invents transitions.
    /// Input is already oldest first (OccurredAtUtc, Id); the synthesized entry goes first.
    /// </summary>
    public static IReadOnlyList<PortalTimelineEntry> Build(PortalTicketHistory history)
    {
        var entries = new List<PortalTimelineEntry>();
        foreach (var e in history.Events)
        {
            if (e.Type == TicketActivityType.Created)
            {
                entries.Add(new(PortalTimelineEntryKind.Submitted, null, TicketStatus.Open, e.OccurredAtUtc, IsRecorded: true));
            }
            else if (e.Type == TicketActivityType.StatusChanged
                && Enum.TryParse<TicketStatus>(e.OldValue, out var from) && Enum.TryParse<TicketStatus>(e.NewValue, out var to))
            {
                entries.Add(new(PortalTimelineEntryKind.StatusChanged, from, to, e.OccurredAtUtc, IsRecorded: true));
            }
        }

        if (!history.Events.Any(e => e.Type == TicketActivityType.Created))
        {
            entries.Insert(0, new(PortalTimelineEntryKind.Submitted, null, TicketStatus.Open, history.TicketCreatedAtUtc, IsRecorded: false));
        }

        return entries;
    }
}
```

When more than 200 events exist, the oldest (including `Created`) can be cut off. A synthesized `Submitted` entry then appears at `CreatedAtUtc` (still a true fact). Documented in Edge Cases.

### 4 — Application: feedback use case

**Create file: `backend/src/Crm.Application/Portal/SaveFeedback/SavePortalTicketFeedbackCommand.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.Portal.SaveFeedback;

/// <summary>
/// CP5 create-or-update of the single feedback row of an owned Resolved/Closed ticket. Single save. Rating nullable so a missing
/// field fails validation. Never touches the ticket, its activity, SLA or assignment (P6/P7).
/// </summary>
public sealed record SavePortalTicketFeedbackCommand(Guid UserId, Guid TicketId, int? Rating, string? Comment)
    : ICommand<PortalTicketDetails>;

public sealed class SavePortalTicketFeedbackCommandHandler(
    ICustomerRepository customers,
    IPortalTicketRepository portal,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<SavePortalTicketFeedbackCommand, PortalTicketDetails>
{
    public async ValueTask<PortalTicketDetails> Handle(SavePortalTicketFeedbackCommand command, CancellationToken cancellationToken)
    {
        var customerId = await customers.ResolveIdAsync(command.UserId, cancellationToken);
        var ticket = await portal.FindAsync(command.TicketId, customerId, cancellationToken)
            ?? throw new NotFoundException($"Ticket '{command.TicketId}' was not found.");
        if (!ticket.CanSubmitFeedback)
        {
            throw new ConflictException("Feedback can be given once the request is resolved or closed.");
        }

        var rating = command.Rating!.Value; // validator: NotNull + range
        var comment = string.IsNullOrWhiteSpace(command.Comment) ? null : command.Comment.Trim();
        var now = timeProvider.GetUtcNow();
        var existing = await portal.GetFeedbackAsync(command.TicketId, cancellationToken);
        if (existing is null)
        {
            portal.AddFeedback(TicketFeedback.Create(command.TicketId, rating, comment, now));
            await unitOfWork.SaveChangesAsync(cancellationToken); // racing first insert ⇒ unique index ⇒ 409
        }
        else if (existing.Update(rating, comment, now))
        {
            await unitOfWork.SaveChangesAsync(cancellationToken); // overlapping edit ⇒ rowversion ⇒ 409
        }

        return (await portal.FindAsync(command.TicketId, customerId, cancellationToken))!;
    }
}

public sealed class SavePortalTicketFeedbackValidator : AbstractValidator<SavePortalTicketFeedbackCommand>
{
    public SavePortalTicketFeedbackValidator()
    {
        RuleFor(c => c.TicketId).NotEmpty();
        RuleFor(c => c.Rating).NotNull().InclusiveBetween(PortalRules.MinRating, PortalRules.MaxRating);
        RuleFor(c => c.Comment).MaximumLength(PortalRules.FeedbackCommentMaxLength);
    }
}
```

### 5 — Application: Help Center use cases

Create, each with its handler (and validator where listed) in one file:

- **`backend/src/Crm.Application/Portal/ListHelpCenterArticles/ListHelpCenterArticlesQuery.cs`**: `ListHelpCenterArticlesQuery(Guid UserId, string? Search, KnowledgeArticleType? Type, Guid? CategoryId, int Page = 1, int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<HelpCenterArticleSummary>>`. The handler first calls `customers.ResolveIdAsync(query.UserId, …)` (CP1: only linked Customers; the id itself is unused), then trims the search and clamps `pageSize`, then calls `helpCenter.ListArticlesAsync(new HelpCenterArticleFilter(search, query.Type, query.CategoryId), query.Page, pageSize, …)`. Validator: `Page` 1..`Paging.MaxPage`, `PageSize >= 1`, `Search` `MaximumLength(KnowledgeBaseRules.SearchMaxLength)`, `Type` `IsInEnum()`.
- **`backend/src/Crm.Application/Portal/GetHelpCenterArticle/GetHelpCenterArticleQuery.cs`**: `GetHelpCenterArticleQuery(Guid UserId, Guid Id) : IQuery<HelpCenterArticleDetails>`. It resolves the profile, then `helpCenter.FindArticleAsync(query.Id) ?? throw new NotFoundException($"Help article '{query.Id}' was not found.")`.
- **`backend/src/Crm.Application/Portal/ListHelpCenterCategories/ListHelpCenterCategoriesQuery.cs`**: `ListHelpCenterCategoriesQuery(Guid UserId) : IQuery<IReadOnlyList<HelpCenterCategory>>`. It resolves the profile, then `helpCenter.ListCategoriesAsync()`.

No `Application/DependencyInjection.cs` change: handlers and validators are discovered (`DependencyInjection.cs` line 11 and the source-generated Mediator). `PortalCustomer` and `PortalTimeline` are static.

### 6 — Infrastructure: EF configuration, DbSet, unit-of-work mapping

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketFeedbackConfiguration.cs`**

```csharp
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class TicketFeedbackConfiguration : IEntityTypeConfiguration<TicketFeedback>
{
    public const string TicketIndex = "UX_TicketFeedback_TicketId";

    public void Configure(EntityTypeBuilder<TicketFeedback> builder)
    {
        builder.ToTable("TicketFeedback", t => t.HasCheckConstraint("CK_TicketFeedback_Rating", "[Rating] BETWEEN 1 AND 5"));
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedOnAdd(); // bigint IDENTITY
        builder.Property(f => f.Comment).HasMaxLength(1000);
        builder.Property<byte[]>("Version").IsRowVersion(); // overlapping edits ⇒ 409 (EfUnitOfWork), as Tickets.Version

        // Ownership through Tickets.CustomerId (no copy). Nothing cascades: tickets are never deleted.
        builder.HasOne<Ticket>().WithMany().HasForeignKey(f => f.TicketId).OnDelete(DeleteBehavior.Restrict);

        // One record per ticket (CP5). Referenced by EfUnitOfWork.KnownUniqueIndexes.
        builder.HasIndex(f => f.TicketId).IsUnique().HasDatabaseName(TicketIndex);
    }
}
```

**File: `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`**: after line 41 (`TicketKnowledgeArticleLinks`) add:

```csharp
    public DbSet<TicketFeedback> TicketFeedback => Set<TicketFeedback>();
```

(`Crm.Domain.Tickets` is already imported, line 9.)

**File: `backend/src/Crm.Infrastructure/Persistence/EfUnitOfWork.cs`**: in `KnownUniqueIndexes` (lines 12–17) add after line 16:

```csharp
        ["UX_TicketFeedback_TicketId"] = "Feedback for this request was already submitted. Reload to see it.",
```

### 7 — Infrastructure: repositories and registration

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/PortalTicketRepository.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Portal;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

/// <summary>PORTAL-001 (P4): customer ownership in every statement; only customer-safe columns are ever selected.</summary>
internal sealed class PortalTicketRepository(CrmDbContext dbContext) : IPortalTicketRepository
{
    public async Task<PagedResult<PortalTicketSummary>> ListAsync(
        Guid customerId, PortalTicketListFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = Owned(customerId);

        if (filter.Status is { } status)
        {
            query = query.Where(t => t.Status == status);
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            // Parameterized; EF Core escapes LIKE wildcards for string.Contains. A full GUID also matches the ticket id (as TicketRepository).
            var search = filter.Search;
            query = Guid.TryParse(search, out var ticketId)
                ? query.Where(t => t.Id == ticketId || t.Subject.Contains(search))
                : query.Where(t => t.Subject.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc).ThenByDescending(t => t.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(t => new PortalTicketSummary(t.Id, t.Subject, t.Category, t.Status, t.CreatedAtUtc))
            .ToListAsync(cancellationToken);

        return new PagedResult<PortalTicketSummary>(items, page, pageSize, totalCount);
    }

    public Task<PortalTicketDetails?> FindAsync(Guid ticketId, Guid customerId, CancellationToken cancellationToken = default) =>
        (from t in Owned(customerId).Where(t => t.Id == ticketId)
         from f in dbContext.TicketFeedback.Where(f => f.TicketId == t.Id).DefaultIfEmpty()
         select new PortalTicketDetails(
             t.Id, t.Subject, t.Description, t.Category, t.Status, t.CreatedAtUtc,
             f == null ? null : new PortalFeedbackDetails(f.Rating, f.Comment, f.CreatedAtUtc, f.UpdatedAtUtc)))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<PortalTicketHistory?> FindHistoryAsync(
        Guid ticketId, Guid customerId, int maxEvents, CancellationToken cancellationToken = default)
    {
        var ticket = Owned(customerId).Where(t => t.Id == ticketId);
        var createdAtUtc = await ticket.Select(t => (DateTimeOffset?)t.CreatedAtUtc).SingleOrDefaultAsync(cancellationToken);
        if (createdAtUtc is null)
        {
            return null;
        }

        // Ownership is part of this statement too. Only the safe columns are selected (P5).
        var newest = await (
                from a in dbContext.TicketActivities.AsNoTracking()
                join t in ticket on a.TicketId equals t.Id
                where a.Type == TicketActivityType.Created || a.Type == TicketActivityType.StatusChanged
                orderby a.OccurredAtUtc descending, a.Id descending
                select new PortalActivityEvent(a.Id, a.Type, a.OccurredAtUtc, a.OldValue, a.NewValue))
            .Take(maxEvents)
            .ToListAsync(cancellationToken);

        newest.Reverse(); // oldest first for display
        return new PortalTicketHistory(createdAtUtc.Value, newest);
    }

    public void AddFeedback(TicketFeedback feedback) => dbContext.TicketFeedback.Add(feedback);

    public Task<TicketFeedback?> GetFeedbackAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        dbContext.TicketFeedback.SingleOrDefaultAsync(f => f.TicketId == ticketId, cancellationToken);

    // Applied first, in SQL: a customer only ever reaches tickets of their own CRM profile. Staff scope (TicketRepository.Scope) is separate.
    private IQueryable<Ticket> Owned(Guid customerId) =>
        dbContext.Tickets.AsNoTracking().Where(t => t.CustomerId == customerId);
}
```

If EF reports that it cannot translate `orderby` over the record in `FindHistoryAsync`, keep the `orderby` on `a` **before** `select` (as written). That is the same rule as `TicketRepository.cs` lines 130–131.

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/HelpCenterRepository.cs`**

```csharp
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Portal;
using Crm.Domain.KnowledgeBase;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

/// <summary>PORTAL-001 CP4: Published AND Public in SQL for every query. No author/publisher/status/visibility columns are selected.</summary>
internal sealed class HelpCenterRepository(CrmDbContext dbContext) : IHelpCenterRepository
{
    public async Task<PagedResult<HelpCenterArticleSummary>> ListArticlesAsync(
        HelpCenterArticleFilter filter, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = PublicArticles();

        if (filter.Type is { } type)
        {
            query = query.Where(a => a.Type == type);
        }

        if (filter.CategoryId is { } categoryId)
        {
            query = query.Where(a => a.CategoryId == categoryId);
        }

        if (!string.IsNullOrEmpty(filter.Search))
        {
            // Parameterized LIKE, as KnowledgeBaseRepository.ListArticlesAsync.
            var search = filter.Search;
            query = query.Where(a => a.Title.Contains(search) || (a.Summary != null && a.Summary.Contains(search)) || a.Body.Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // orderby before the record projection: EF cannot order by record members. Body is never selected.
        var items = await (
                from a in query
                join c in dbContext.KnowledgeCategories on a.CategoryId equals c.Id
                orderby a.PublishedAtUtc descending, a.Id descending
                select new HelpCenterArticleSummary(
                    a.Id, a.Title, a.Summary, a.Type, a.CategoryId, c.Name, a.PublishedAtUtc ?? a.UpdatedAtUtc))
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return new PagedResult<HelpCenterArticleSummary>(items, page, pageSize, totalCount);
    }

    public Task<HelpCenterArticleDetails?> FindArticleAsync(Guid id, CancellationToken cancellationToken = default) =>
        (from a in PublicArticles()
         where a.Id == id
         join c in dbContext.KnowledgeCategories on a.CategoryId equals c.Id
         select new HelpCenterArticleDetails(
             a.Id, a.Title, a.Summary, a.Body, a.Type, a.CategoryId, c.Name, a.PublishedAtUtc ?? a.UpdatedAtUtc, a.UpdatedAtUtc))
        .SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<HelpCenterCategory>> ListCategoriesAsync(CancellationToken cancellationToken = default) =>
        await dbContext.KnowledgeCategories.AsNoTracking()
            .Where(c => PublicArticles().Any(a => a.CategoryId == c.Id))
            .OrderBy(c => c.Name).ThenBy(c => c.Id)
            .Select(c => new HelpCenterCategory(c.Id, c.Name))
            .ToListAsync(cancellationToken);

    // The single customer visibility rule (CP4), applied first in every statement.
    private IQueryable<KnowledgeArticle> PublicArticles() =>
        dbContext.KnowledgeArticles.AsNoTracking()
            .Where(a => a.Status == KnowledgeArticleStatus.Published && a.Visibility == KnowledgeArticleVisibility.Public);
}
```

**File: `backend/src/Crm.Infrastructure/DependencyInjection.cs`**: after line 58 (`IAiKnowledgeSearch`) add:

```csharp
        services.AddScoped<IPortalTicketRepository, PortalTicketRepository>();
        services.AddScoped<IHelpCenterRepository, HelpCenterRepository>();
```

### 8 — Infrastructure: migration

Run in `backend/`:

```powershell
dotnet ef migrations add AddTicketFeedback --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations -- --environment Development
```

**Review the generated `…_AddTicketFeedback.cs` before applying.** `Up` must contain exactly:

- one `CreateTable("TicketFeedback")`: `Id bigint IDENTITY(1,1)`, `TicketId uniqueidentifier NOT NULL`, `Rating int NOT NULL`, `Comment nvarchar(1000) NULL`, `CreatedAtUtc`/`UpdatedAtUtc datetimeoffset NOT NULL`, `Version rowversion`, `PK_TicketFeedback`, `FK_TicketFeedback_Tickets_TicketId` (`Restrict`), `CK_TicketFeedback_Rating`;
- one `CreateIndex("UX_TicketFeedback_TicketId", unique: true)`.

**Nothing else:** no `AddColumn`/`AlterColumn`/`DropTable`/`DropColumn`/`RenameColumn`/`InsertData`/`UpdateData`/`Sql(...)`, and no change to `Tickets`, `TicketActivities`, `KnowledgeArticles` or `Customers`. If anything else appears, the model drifted: stop and report. `Down` drops only `TicketFeedback`. Do not hand-edit `CrmDbContextModelSnapshot.cs`.

### 9 — API: contracts and controllers

**Create file: `backend/src/Crm.Api/Contracts/Portal/PortalContracts.cs`**

```csharp
using Crm.Application.Portal;
using Crm.Domain.KnowledgeBase;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Portal;

// CP2: exactly three fields. No customer, priority, status, assignee or role field exists, so forged JSON members are ignored.
public sealed record SubmitPortalTicketRequest(string Subject, string Description, TicketCategory? Category);

public sealed record SavePortalFeedbackRequest(int? Rating, string? Comment);

public sealed record PortalTicketSummaryResponse(Guid Id, string Subject, TicketCategory Category, TicketStatus Status, DateTimeOffset CreatedAtUtc)
{
    public static PortalTicketSummaryResponse From(PortalTicketSummary t) => new(t.Id, t.Subject, t.Category, t.Status, t.CreatedAtUtc);
}

public sealed record PortalFeedbackResponse(int Rating, string? Comment, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc);

public sealed record PortalTicketResponse(
    Guid Id, string Subject, string Description, TicketCategory Category, TicketStatus Status, DateTimeOffset CreatedAtUtc,
    bool CanSubmitFeedback, PortalFeedbackResponse? Feedback)
{
    public static PortalTicketResponse From(PortalTicketDetails t) =>
        new(t.Id, t.Subject, t.Description, t.Category, t.Status, t.CreatedAtUtc, t.CanSubmitFeedback,
            t.Feedback is { } f ? new PortalFeedbackResponse(f.Rating, f.Comment, f.CreatedAtUtc, f.UpdatedAtUtc) : null);
}

public sealed record PortalTimelineEntryResponse(
    PortalTimelineEntryKind Kind, TicketStatus? FromStatus, TicketStatus Status, DateTimeOffset OccurredAtUtc, bool IsRecorded)
{
    public static PortalTimelineEntryResponse From(PortalTimelineEntry e) => new(e.Kind, e.FromStatus, e.Status, e.OccurredAtUtc, e.IsRecorded);
}

public sealed record HelpCenterCategoryResponse(Guid Id, string Name)
{
    public static HelpCenterCategoryResponse From(HelpCenterCategory c) => new(c.Id, c.Name);
}

public sealed record HelpCenterArticleSummaryResponse(
    Guid Id, string Title, string? Summary, KnowledgeArticleType Type, Guid CategoryId, string CategoryName, DateTimeOffset PublishedAtUtc)
{
    public static HelpCenterArticleSummaryResponse From(HelpCenterArticleSummary a) =>
        new(a.Id, a.Title, a.Summary, a.Type, a.CategoryId, a.CategoryName, a.PublishedAtUtc);
}

public sealed record HelpCenterArticleResponse(
    Guid Id, string Title, string? Summary, string Body, KnowledgeArticleType Type, Guid CategoryId, string CategoryName,
    DateTimeOffset PublishedAtUtc, DateTimeOffset UpdatedAtUtc)
{
    public static HelpCenterArticleResponse From(HelpCenterArticleDetails a) =>
        new(a.Id, a.Title, a.Summary, a.Body, a.Type, a.CategoryId, a.CategoryName, a.PublishedAtUtc, a.UpdatedAtUtc);
}
```

**Create file: `backend/src/Crm.Api/Controllers/PortalTicketsController.cs`**

```csharp
[ApiController]
[Route(ApiRoutes.V1 + "/portal/tickets")]
[Authorize(Policy = AuthorizationPolicies.Customer)]
public sealed class PortalTicketsController(ISender sender) : ControllerBase
{
    private const string Scope = "Requires role: Customer (only tickets of the caller's own linked CRM profile; others 404; no linked profile: 404). Staff use /api/v1/tickets.";

    [EndpointDescription(Scope + " Every ticket of the customer, whoever created it. One-based page (default 1), pageSize (default 20, clamped to 100), optional search on subject or exact ticket id, status filter. Newest first. No priority, assignee, escalation or SLA fields.")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<PortalTicketSummaryResponse>>> List(
        [FromQuery] string? search, [FromQuery] TicketStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListPortalTicketsQuery(User.GetUserId(), search, status, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<PortalTicketSummaryResponse>(
            result.Items.Select(PortalTicketSummaryResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription(Scope)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PortalTicketResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(PortalTicketResponse.From(await sender.Send(new GetPortalTicketQuery(User.GetUserId(), id), cancellationToken)));

    [EndpointDescription("Requires role: Customer (own linked CRM profile). Subject, description and category only; the ticket is always Open with Medium priority and the customer is taken from the caller. SLA and automatic assignment rules apply as for staff-created tickets. Nothing is sent to anyone.")]
    [HttpPost]
    [ProducesResponseType<PortalTicketResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<PortalTicketResponse>> Submit(SubmitPortalTicketRequest request, CancellationToken cancellationToken)
    {
        var ticket = await sender.Send(
            new SubmitPortalTicketCommand(User.GetUserId(), request.Subject, request.Description, request.Category), cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = ticket.Id }, PortalTicketResponse.From(ticket));
    }

    [EndpointDescription(Scope + " Customer-safe history: ticket submission and status changes only (incl. reopening), oldest first, bounded to the newest 200 events. Tickets created before history tracking show one submission entry with isRecorded = false.")]
    [HttpGet("{id:guid}/timeline")]
    public async Task<ActionResult<IReadOnlyList<PortalTimelineEntryResponse>>> Timeline(Guid id, CancellationToken cancellationToken) =>
        Ok((await sender.Send(new GetPortalTicketTimelineQuery(User.GetUserId(), id), cancellationToken))
            .Select(PortalTimelineEntryResponse.From).ToList());

    [EndpointDescription(Scope + " Creates or updates the single satisfaction rating (1-5, optional comment max 1000) while the ticket is Resolved or Closed (otherwise 409). Never changes the ticket. Concurrent first submissions or overlapping edits: 409.")]
    [HttpPut("{id:guid}/feedback")]
    public async Task<ActionResult<PortalTicketResponse>> SaveFeedback(Guid id, SavePortalFeedbackRequest request, CancellationToken cancellationToken) =>
        Ok(PortalTicketResponse.From(await sender.Send(
            new SavePortalTicketFeedbackCommand(User.GetUserId(), id, request.Rating, request.Comment), cancellationToken)));
}
```

Usings: `Crm.Api.Authentication`, `Crm.Api.Contracts.Common`, `Crm.Api.Contracts.Portal`, `Crm.Application.Common.Paging`, `Crm.Application.Portal.GetTicket`, `…GetTimeline`, `…ListTickets`, `…SaveFeedback`, `…SubmitTicket`, `Crm.Domain.Tickets`, `Mediator`, `Microsoft.AspNetCore.Authorization`, `Microsoft.AspNetCore.Mvc`.

**Create file: `backend/src/Crm.Api/Controllers/PortalHelpCenterController.cs`**: `[Route(ApiRoutes.V1 + "/portal/help-center")]` with `[Authorize(Policy = AuthorizationPolicies.Customer)]` and the constant `private const string Visibility = "Requires role: Customer (linked profile). Published + Public articles only — Internal, Draft, Ready for Publication and Archived articles return 404 / are never listed. No anonymous access.";`. Actions:

- `[HttpGet("articles")]` `List([FromQuery] string? search, [FromQuery] KnowledgeArticleType? type, [FromQuery] Guid? categoryId, [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, …)` → `PagedResponse<HelpCenterArticleSummaryResponse>`. Description: `Visibility + " One-based page, pageSize (default 20, clamped to 100), search on title/summary/body, type, categoryId. Newest published first. Rows omit the body."`
- `[HttpGet("articles/{id:guid}")]` → `HelpCenterArticleResponse`.
- `[HttpGet("categories")]` → `IReadOnlyList<HelpCenterCategoryResponse>`. Description: `Visibility + " Only categories that contain at least one such article; id and name only. Ordered by name."`

**No `[AllowAnonymous]` anywhere** (intake §4: public visibility ≠ anonymous route).

### 10 — Text-only updates (P11)

- **File: `backend/src/Crm.Domain/KnowledgeBase/KnowledgeArticleVisibility.cs`** lines 3–6: replace the summary with `/// K2. Independent of status. Public + Published = readable by signed-in Customers in the Customer Help Center (PORTAL-001, /api/v1/portal/help-center). Internal = staff only. No anonymous access.`
- **File: `backend/src/Crm.Api/Controllers/KnowledgeArticlesController.cs`** line 24: change `"Staff only — no customer or anonymous access (Customer Portal is a future story). …"` to `"Staff only — customers read Published + Public articles through /api/v1/portal/help-center; no anonymous access. …"` (keep the rest of the string unchanged).

### 11 — Backend unit test (the single new one)

**Create file: `backend/tests/Crm.UnitTests/Portal/SubmitPortalTicketCommandHandlerTests.cs`** (structure of `CreateTicketCommandHandlerTests.cs`):

```csharp
public sealed class SubmitPortalTicketCommandHandlerTests
{
    [Fact]
    public async Task Handle_LinkedCustomerSubmitsTicket_DerivesProfileOpensMediumUnassignedAndRunsCreationAutomationOnce()
    {
        var userId = Guid.CreateVersion7();      // Identity account (JWT sub)
        var customerId = Guid.CreateVersion7();  // linked CRM profile — a different id
        var tickets = new FakeTicketRepository();
        var automation = new RecordingTicketAutomation();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new SubmitPortalTicketCommandHandler(
            new FakeCustomerRepository(userId, customerId), tickets, automation, new FakePortalTicketRepository(tickets), unitOfWork, TimeProvider.System);

        var result = await handler.Handle(
            new SubmitPortalTicketCommand(userId, "  Invoice is wrong ", " I was charged twice. ", TicketCategory.Billing), CancellationToken.None);

        var stored = Assert.Single(tickets.Added);
        Assert.Equal(customerId, stored.CustomerId);           // resolved through Customers.UserId, never the user id
        Assert.Equal(userId, stored.CreatedByUserId);          // the customer's own account, no staff actor
        Assert.Null(stored.AssignedAgentUserId);               // no self-assignment (P2); rules may assign later in automation
        Assert.Equal(TicketStatus.Open, stored.Status);
        Assert.Equal(TicketPriority.Medium, stored.Priority);  // CP2
        Assert.Equal(TicketCategory.Billing, stored.Category);
        Assert.Equal("Invoice is wrong", stored.Subject);
        Assert.Equal("I was charged twice.", stored.Description);
        Assert.Equal(TicketActivityType.Created, Assert.Single(stored.Activities).Type);
        Assert.Same(stored, Assert.Single(automation.Applied)); // the SLA/assignment hook ran exactly once, before the save
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(stored.Id, result.Id);
        Assert.False(result.CanSubmitFeedback);
    }
    // Fakes (private sealed, NotSupportedException for unused members):
    //  FakeCustomerRepository(userId, customerId): FindDetailsByUserIdAsync returns CustomerDetails(customerId, "Ada", "Lovelace",
    //    "ada@example.com", null, null, true, now, now) for userId, else null. All other members throw.
    //  FakeTicketRepository: Add records; every other member throws (proves no staff read path is used).
    //  RecordingTicketAutomation: ApplyAsync appends the ticket to Applied and asserts tickets.Added is still empty (runs before Add).
    //  FakePortalTicketRepository(FakeTicketRepository tickets): FindAsync maps the added Ticket to PortalTicketDetails(…, Feedback: null);
    //    other members throw.
    //  FakeUnitOfWork: as CreateTicketCommandHandlerTests lines 103–109.
}
```

For the "runs before Add" check, give `RecordingTicketAutomation` a constructor parameter `FakeTicketRepository tickets` and record `tickets.Added.Count` at call time. Assert that count is `0`. **No existing test changes.**

---

## Frontend Tasks

### 12 — Models and API service

**Create file: `frontend/src/app/features/portal/portal-models.ts`**

```ts
import type { KnowledgeArticleType } from '../knowledge-base/knowledge-base-models';
import type { TicketCategory, TicketStatus } from '../tickets/ticket-models';

/** Matches Crm.Api PortalTicketSummaryResponse — customer-safe fields only. */
export interface PortalTicketSummary { id: string; subject: string; category: TicketCategory; status: TicketStatus; createdAtUtc: string; }

export interface PortalFeedback { rating: number; comment: string | null; createdAtUtc: string; updatedAtUtc: string; }

/** Matches Crm.Api PortalTicketResponse. canSubmitFeedback = current status Resolved or Closed (the API re-checks). */
export interface PortalTicket extends PortalTicketSummary { description: string; canSubmitFeedback: boolean; feedback: PortalFeedback | null; }

export type PortalTimelineEntryKind = 'Submitted' | 'StatusChanged';

/** Matches Crm.Api PortalTimelineEntryResponse. isRecorded false = derived from the ticket's creation time (older ticket). */
export interface PortalTimelineEntry {
  kind: PortalTimelineEntryKind; fromStatus: TicketStatus | null; status: TicketStatus; occurredAtUtc: string; isRecorded: boolean;
}

export interface PortalTicketListParams { page: number; pageSize: number; search: string | null; status: TicketStatus | null; }

/** CP2: exactly these three fields. */
export interface SubmitPortalTicketRequest { subject: string; description: string; category: TicketCategory; }

export interface SavePortalFeedbackRequest { rating: number; comment: string | null; }

export interface HelpCenterCategory { id: string; name: string; }

export interface HelpCenterArticleSummary {
  id: string; title: string; summary: string | null; type: KnowledgeArticleType; categoryId: string; categoryName: string; publishedAtUtc: string;
}

export interface HelpCenterArticle extends HelpCenterArticleSummary { body: string; updatedAtUtc: string; }

export interface HelpCenterListParams {
  page: number; pageSize: number; search: string | null; type: KnowledgeArticleType | null; categoryId: string | null;
}

export const PORTAL_LIMITS = { subject: 200, description: 4000, feedbackComment: 1000 } as const;
export const NOT_LINKED = 'Your account is not linked to a customer profile. Contact support.';
```

**Create file: `frontend/src/app/features/portal/portal-api.ts`** (style of `tickets-api.ts` lines 18–49: `HttpParams`, empty filters omitted, `apiUrl.versioned`):

```ts
@Injectable({ providedIn: 'root' })
export class PortalApi {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(ApiUrl);

  listTickets({ page, pageSize, search, status }: PortalTicketListParams): Observable<PagedResponse<PortalTicketSummary>> { /* GET portal/tickets */ }
  getTicket(id: string): Observable<PortalTicket> { /* GET portal/tickets/{id} */ }
  submitTicket(body: SubmitPortalTicketRequest): Observable<PortalTicket> { /* POST portal/tickets */ }
  timeline(id: string): Observable<PortalTimelineEntry[]> { /* GET portal/tickets/{id}/timeline */ }
  saveFeedback(id: string, body: SavePortalFeedbackRequest): Observable<PortalTicket> { /* PUT portal/tickets/{id}/feedback */ }
  listArticles({ page, pageSize, search, type, categoryId }: HelpCenterListParams): Observable<PagedResponse<HelpCenterArticleSummary>> { /* GET portal/help-center/articles */ }
  getArticle(id: string): Observable<HelpCenterArticle> { /* GET portal/help-center/articles/{id} */ }
  categories(): Observable<HelpCenterCategory[]> { /* GET portal/help-center/categories */ }
}
```

Implement each body exactly like the matching `TicketsApi` / `KnowledgeBaseApi` method (trimmed `search` only when non-empty; `status`, `type`, `categoryId` only when set). `PagedResponse` is imported from `../customers/customer-models` (as `tickets-api.ts` line 5).

### 13 — Timeline wording (pure helper)

**Create file: `frontend/src/app/features/portal/portal-timeline.ts`**

```ts
import { PortalTimelineEntry } from './portal-models';

export interface TimelineText { title: string; text: string; }

/** Customer wording for one timeline entry. Facts only: no actor, no reason, no internal detail (CP3). */
export function describeTimelineEntry(entry: PortalTimelineEntry): TimelineText {
  if (entry.kind === 'Submitted') {
    return entry.isRecorded
      ? { title: 'Request submitted', text: 'We received your request.' }
      : { title: 'Request created', text: 'Earlier status history is not available for this request.' };
  }
  if (entry.fromStatus === 'Closed' && entry.status === 'Open') {
    return { title: 'Reopened', text: 'Your request was reopened.' };
  }
  if (entry.fromStatus === 'Resolved' && entry.status === 'InProgress') {
    return { title: 'Back in progress', text: 'We are continuing to work on your request.' };
  }
  switch (entry.status) {
    case 'InProgress':
      return { title: 'In progress', text: 'Our team is working on your request.' };
    case 'Resolved':
      return { title: 'Resolved', text: 'Your request has been resolved.' };
    case 'Closed':
      return { title: 'Closed', text: 'Your request is closed.' };
    default:
      return { title: 'Open', text: 'Your request is open.' };
  }
}
```

### 14 — Routes, navigation, Home card

**Create file: `frontend/src/app/features/portal/portal.routes.ts`**

```ts
export const PORTAL_ROUTES: Routes = [
  { path: '', title: 'Support Portal | Support CRM', loadComponent: () => import('./portal-home-page').then(m => m.PortalHomePage) },
  { path: 'tickets', title: 'My tickets | Support CRM', loadComponent: () => import('./portal-ticket-list-page').then(m => m.PortalTicketListPage) },
  { path: 'new-ticket', title: 'New support request | Support CRM', loadComponent: () => import('./portal-ticket-create-page').then(m => m.PortalTicketCreatePage) },
  { path: 'tickets/:id', title: 'My request | Support CRM', loadComponent: () => import('./portal-ticket-detail-page').then(m => m.PortalTicketDetailPage) },
  { path: 'help-center', title: 'Help Center | Support CRM', loadComponent: () => import('./help-center-page').then(m => m.HelpCenterPage) },
  { path: 'help-center/:id', title: 'Help article | Support CRM', loadComponent: () => import('./help-center-article-page').then(m => m.HelpCenterArticlePage) },
];
```

**File: `frontend/src/app/app.routes.ts`**: insert after the `ai-assistant` route (after line 79, before the comment on line 80):

```ts
      {
        path: 'portal',
        canActivate: [roleGuard('Customer')],
        loadChildren: () => import('./features/portal/portal.routes').then(m => m.PORTAL_ROUTES),
      },
```

**File: `frontend/src/app/core/layout/navigation.ts`**: insert after the Home entry (line 14):

```ts
  { label: 'Support Portal', icon: 'support_agent', path: '/portal', exact: true, roles: ['Customer'] },
  { label: 'My Tickets', icon: 'confirmation_number', path: '/portal/tickets', exact: false, roles: ['Customer'] },
  { label: 'New Ticket', icon: 'add_circle', path: '/portal/new-ticket', exact: true, roles: ['Customer'] },
  { label: 'Help Center', icon: 'help_center', path: '/portal/help-center', exact: false, roles: ['Customer'] },
```

**File: `frontend/src/app/features/home/home-page.ts`**: replace `protected readonly user = inject(AuthService).currentUser;` (line 37) with `private readonly auth = inject(AuthService);`, `protected readonly user = this.auth.currentUser;`, `protected readonly isCustomer = computed(() => this.auth.role() === 'Customer');` (add `computed` to the `@angular/core` import and `RouterLink` to `imports`). **File: `home-page.html`**: after the page header (line 3), add a Customer-only card:

```html
@if (isCustomer()) {
  <mat-card appearance="outlined" class="status-card">
    <mat-card-header><mat-card-title>Customer support</mat-card-title></mat-card-header>
    <mat-card-content><p>Submit a request, follow its progress and find answers in the Help Center.</p></mat-card-content>
    <mat-card-actions align="end">
      <a mat-button routerLink="/portal/help-center">Help Center</a>
      <a mat-flat-button routerLink="/portal">Open Support Portal</a>
    </mat-card-actions>
  </mat-card>
}
```

**File: `frontend/src/app/features/knowledge-base/knowledge-base-models.ts`** line 55 (P11):

```ts
export const PUBLIC_VISIBILITY_HINT = 'Published + Public articles appear in the Customer Help Center for signed-in customers.';
```

### 15 — Shared portal styles

**Create file: `frontend/src/app/features/portal/portal-page.scss`** (logical properties only; keep it under 1 kB). Every portal page uses `styleUrls: ['../../shared/ui/productivity-page.scss', './portal-page.scss']`:

```scss
.detail-stack { display: grid; gap: var(--crm-space-4); max-inline-size: 960px; }
.readonly-list { display: grid; grid-template-columns: max-content 1fr; gap: var(--crm-space-2) var(--crm-space-4); margin: 0; }
.readonly-list dd { margin: 0; overflow-wrap: anywhere; }
.timeline { list-style: none; margin: 0; padding: 0; display: grid; gap: var(--crm-space-3); }
.timeline li { padding-inline-start: var(--crm-space-3); border-inline-start: 3px solid var(--mat-sys-outline-variant); }
.quick-actions { display: flex; flex-wrap: wrap; gap: var(--crm-space-3); }
@media (max-width: 599px) { .readonly-list { grid-template-columns: 1fr; } }
```

### 16 — Portal landing page

**Create files: `frontend/src/app/features/portal/portal-home-page.ts` / `.html`**: `PortalHomePage`, `OnPush`. It calls `PortalApi.listTickets({ page: 1, pageSize: 5, search: null, status: null })` once and holds `recent`, `totalCount`, `loading`, `error` signals. On error: status 404 ⇒ `NOT_LINKED`, otherwise `describeHttpError(error, 'Could not load your requests.')`, plus a Retry button. Template:

- `<crm-page-header title="Support Portal" subtitle="Submit requests, track their progress and browse help articles." />`
- `.quick-actions`: `<a mat-flat-button routerLink="/portal/new-ticket"><mat-icon>add</mat-icon> New support request</a>`, `<a mat-stroked-button routerLink="/portal/tickets">My tickets</a>`, `<a mat-stroked-button routerLink="/portal/help-center">Help Center</a>`.
- "Recent requests" card: `.rows` list of `subject` (`[routerLink]="['/portal/tickets', t.id]"`) + status badge (`statusLabel`/`statusTone` from `ticket-models.ts`) + `createdAtUtc | date: 'medium'`. Empty state "You have no support requests yet." with the New request link. When `totalCount() > 5`, show "View all (N)".

No counts or charts beyond this (§5).

### 17 — My Tickets page

**Create files: `frontend/src/app/features/portal/portal-ticket-list-page.ts` / `.html`**: `PortalTicketListPage`. Copy the **request pipeline** of `ticket-list-page.ts` lines 93–157 (`Subject<PortalTicketListParams>` → `switchMap` with per-request `catchError` → signals; filters `valueChanges` with `debounceTime(300)` ⇒ `load(1)`; `onPage`). Filters form: `search: ['']`, `status: [null as TicketStatus | null]`. `pageSizeOptions = [10, 20, 50]`. Error 404 ⇒ `NOT_LINKED`.

Template (`.toolbar` + `.rows` list, like `knowledge-base-page.html` lines 3–87 — **a list, not a `mat-table`**, so it works at 400 px):

- Header `<crm-page-header title="My tickets" subtitle="All your current and previous support requests." />`.
- Toolbar: search field (`placeholder="Subject or request ID"`), Status select (Any + `TICKET_STATUSES` with `statusLabel`), `<a mat-flat-button routerLink="/portal/new-ticket"><mat-icon>add</mat-icon> New request</a>`.
- Rows: subject link → `/portal/tickets/{id}`, status badge, `.hint` category, `.meta` "Submitted {{ createdAtUtc | date: 'medium' }} · Ref {{ id }}".
- Empty: filters set ⇒ "No requests match your filters."; else "You have no support requests yet." + New request link.
- `mat-paginator` exactly as `knowledge-base-page.html` lines 80–86, `aria-label="Select page of requests"`.

**No** assignee, priority, escalation or SLA column.

### 18 — New Ticket page

**Create files: `frontend/src/app/features/portal/portal-ticket-create-page.ts` / `.html`**: `PortalTicketCreatePage`, the structure of `ticket-create-page.ts` lines 43–117 **without** customer picker, priority, agents or `AuthService`:

```ts
protected readonly categories = TICKET_CATEGORIES;
protected readonly limits = PORTAL_LIMITS;
protected readonly form = this.fb.group({
  subject: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(PORTAL_LIMITS.subject)]],
  description: ['', [Validators.required, Validators.pattern(/\S/), Validators.maxLength(PORTAL_LIMITS.description)]],
  category: this.fb.control<TicketCategory | null>(null, Validators.required),
});
protected readonly submitting = signal(false);
protected readonly error = signal<string | null>(null);

protected submit(): void {
  if (this.form.invalid || this.submitting()) { this.form.markAllAsTouched(); return; } // prevents duplicate submits while pending
  const v = this.form.getRawValue();
  this.submitting.set(true);
  this.error.set(null);
  this.api.submitTicket({ subject: v.subject.trim(), description: v.description.trim(), category: v.category! })
    .pipe(takeUntilDestroyed(this.destroyRef))
    .subscribe({
      next: ticket => {
        this.snackBar.open('Request submitted. Our team will review it.', undefined, { duration: 4000 });
        void this.router.navigate(['/portal/tickets', ticket.id]);
      },
      error: (error: HttpErrorResponse) => {
        this.error.set(error.status === 404 ? NOT_LINKED : describeHttpError(error, 'Could not submit your request.'));
        this.submitting.set(false);
      },
    });
}
```

Template: header "New support request" / "Describe your issue and we will get back to you." Then Subject, Description (`rows="6"`) and Category fields with the `mat-error` texts of `ticket-create-page.html` lines 14–40. Add `<mat-hint align="end">{{ form.controls.description.value.length }} / {{ limits.description }}</mat-hint>` on Description. Add a `.hint` line "New requests start as Open. Our team sets the priority and handles assignment." and the form error `role="alert"`. Actions: `<a mat-button routerLink="/portal/tickets">Cancel</a>` and `<button mat-flat-button type="submit" [disabled]="submitting()">{{ submitting() ? 'Submitting…' : 'Submit request' }}</button>`. **Map field errors from 400:** if `error.error?.errors` exists, `describeHttpError` already returns the title; that is enough for this story.

### 19 — Ticket Details page and feedback card

**Create files: `frontend/src/app/features/portal/portal-ticket-detail-page.ts` / `.html`**: `PortalTicketDetailPage`:

```ts
private readonly api = inject(PortalApi);
protected readonly ticket = signal<PortalTicket | null>(null);
protected readonly timeline = signal<PortalTimelineEntry[]>([]);
protected readonly loading = signal(true);
protected readonly loadError = signal<string | null>(null);
protected readonly describe = describeTimelineEntry;
protected readonly statusLabel = statusLabel;
protected readonly statusTone = statusTone;

constructor() {
  inject(ActivatedRoute).paramMap
    .pipe(
      map(params => params.get('id') ?? ''),
      tap(() => { this.ticket.set(null); this.timeline.set([]); this.loadError.set(null); this.loading.set(true); }), // never show the previous ticket
      switchMap(id =>
        forkJoin({ ticket: this.api.getTicket(id), timeline: this.api.timeline(id) }).pipe(
          catchError((error: HttpErrorResponse) => {
            this.loadError.set(error.status === 404 ? 'This request was not found.' : describeHttpError(error, 'Could not load the request.'));
            this.loading.set(false);
            return EMPTY;
          }),
        ),
      ),
      takeUntilDestroyed(),
    )
    .subscribe(({ ticket, timeline }) => { this.ticket.set(ticket); this.timeline.set(timeline); this.loading.set(false); });
}

protected onFeedbackSaved(updated: PortalTicket): void { this.ticket.set(updated); }
```

The 404 text is the **same** for a missing ticket and another customer's ticket. Add a `reload()` for Retry that re-runs the same load for the current id. Template inside `@if (ticket(); as current)` / `.detail-stack`:

1. `<p><a mat-button routerLink="/portal/tickets">Back to my tickets</a></p>`.
2. Card "Request" with `.readonly-list`:
   - Reference (`current.id`, plus `cdkCopyToClipboard` icon button `aria-label="Copy reference"` from `@angular/cdk/clipboard`)
   - Status badge
   - Category
   - Submitted `| date: 'medium'`
   - Subject (`.user-text`)
   - Description (`<p class="user-text">{{ current.description }}</p>`)
3. Card "History": `<ol class="timeline">` with, per entry, `describe(e).title` (strong), `occurredAtUtc | date: 'medium'` (`.hint`) and `describe(e).text`. Empty array ⇒ "No history yet."
4. Feedback: `@if (current.canSubmitFeedback || current.feedback) { <crm-portal-ticket-feedback [ticketId]="current.id" [canSubmit]="current.canSubmitFeedback" [feedback]="current.feedback" (saved)="onFeedbackSaved($event)" /> }`.

**No** edit, transition, escalation, priority, assignment, SLA, quick-reply, KB-link, AI or discussion components.

**Create files: `frontend/src/app/features/portal/portal-ticket-feedback.ts` / `.html`**: `PortalTicketFeedback` (selector `crm-portal-ticket-feedback`), standalone, `OnPush`. Imports `ReactiveFormsModule`, `MatCardModule`, `MatButtonToggleModule`, `MatFormFieldModule`, `MatInputModule`, `MatButtonModule`, `MatIconModule`, `DatePipe`.

```ts
readonly ticketId = input.required<string>();
readonly canSubmit = input.required<boolean>();
readonly feedback = input<PortalFeedback | null>(null);
readonly saved = output<PortalTicket>();
protected readonly ratings = [1, 2, 3, 4, 5];
protected readonly editing = signal(false);
protected readonly saving = signal(false);
protected readonly error = signal<string | null>(null);
protected readonly form = inject(NonNullableFormBuilder).group({
  rating: [null as number | null, [Validators.required, Validators.min(1), Validators.max(5)]],
  comment: ['', Validators.maxLength(PORTAL_LIMITS.feedbackComment)],
});

constructor() {
  // Reset whenever the ticket or its saved feedback changes (no stale form across tickets).
  effect(() => {
    const f = this.feedback();
    this.ticketId();
    untracked(() => {
      this.form.reset({ rating: f?.rating ?? null, comment: f?.comment ?? '' });
      this.editing.set(false);
      this.error.set(null);
    });
  });
}

protected save(): void { /* guard invalid/saving; api.saveFeedback(ticketId, { rating, comment: comment.trim() || null }); success ⇒
  snackbar 'Thank you for your feedback.', editing false, saved.emit(updated); 409 ⇒ show detail and emit nothing; others ⇒ describeHttpError */ }
```

Template states:

- **`canSubmit && !feedback`** — form: "How satisfied are you with how we handled this request?" Then `<mat-button-toggle-group formControlName="rating" aria-label="Rating from 1 to 5">` with one toggle per value showing `{{ r }} ★` (`aria-label="{{ r }} out of 5"`), an optional comment textarea with a `/ 1000` counter, the error `role="alert"`, and the "Submit feedback" button (disabled while saving).
- **`feedback && !editing()`** — read-only rating "{{ feedback.rating }} / 5", comment (`.user-text`) and "Last updated {{ updatedAtUtc | date: 'medium' }}". Show the **Edit** button only when `canSubmit()`. Otherwise show the hint "You can update your feedback after this request is resolved again." (P7).
- **`editing()`** — the same form prefilled, with **Save** and **Cancel**.

### 20 — Help Center pages

**Create files: `frontend/src/app/features/portal/help-center-page.ts` / `.html`**: `HelpCenterPage`. The same pipeline as task 17 with filters `search`, `type` (`ARTICLE_TYPES`, `articleTypeLabel` from `../knowledge-base/knowledge-base-models`) and `categoryId` (options from `PortalApi.categories()`, loaded once; on failure the category filter is hidden and the list still works). Template like `knowledge-base-page.html` without status/visibility badges, author, "New article" or "View":

- Header: "Help Center" / "FAQs, help articles, solutions and guides."
- Rows: title link `[routerLink]="['/portal/help-center', a.id]"`, `.hint` `{{ typeLabel(a.type) }} · {{ a.categoryName }}`, summary (`.user-text`), `.meta` "Published {{ a.publishedAtUtc | date: 'mediumDate' }}".
- Empty: "No articles match your search." / "No help articles are available yet."
- Paginator `aria-label="Select page of help articles"`.

**Create files: `frontend/src/app/features/portal/help-center-article-page.ts` / `.html`**: `HelpCenterArticlePage`, like `knowledge-article-page.ts` lines 52–83 but read-only. It uses `paramMap` + `switchMap` (as task 19) so a changed `:id` reloads. 404 ⇒ "This article is not available." Template:

- `<a mat-button routerLink="/portal/help-center">Back to Help Center</a>`
- title (`h2`), `.hint` type · category · "Published …"
- summary (`.user-text`)
- `<crm-markdown-view [source]="a.body" />` (import `MarkdownView` from `../knowledge-base/markdown-view`)

**No** copy, edit, workflow or status controls.

### 21 — No other frontend changes

`TicketsApi`, `KnowledgeBaseApi`, staff pages, `AuthService` and `roleGuard` are unchanged. No root service caches portal data. No `localStorage`.

### 22 — Frontend unit test (the single new one)

**Create file: `frontend/src/app/features/portal/portal-timeline.spec.ts`** (pattern of `markdown.spec.ts`, no TestBed):

```ts
import { PortalTimelineEntry } from './portal-models';
import { describeTimelineEntry } from './portal-timeline';

const at = '2030-01-01T00:00:00Z';
const entry = (e: Partial<PortalTimelineEntry>): PortalTimelineEntry =>
  ({ kind: 'StatusChanged', fromStatus: null, status: 'Open', occurredAtUtc: at, isRecorded: true, ...e });

describe('describeTimelineEntry', () => {
  it('words submission, legacy creation, progress, resolution, closing, resuming and reopening without internal detail', () => {
    expect(describeTimelineEntry(entry({ kind: 'Submitted' })).title).toBe('Request submitted');
    expect(describeTimelineEntry(entry({ kind: 'Submitted', isRecorded: false })).title).toBe('Request created');
    expect(describeTimelineEntry(entry({ fromStatus: 'Open', status: 'InProgress' })).title).toBe('In progress');
    expect(describeTimelineEntry(entry({ fromStatus: 'InProgress', status: 'Resolved' })).title).toBe('Resolved');
    expect(describeTimelineEntry(entry({ fromStatus: 'Resolved', status: 'Closed' })).title).toBe('Closed');
    expect(describeTimelineEntry(entry({ fromStatus: 'Resolved', status: 'InProgress' })).title).toBe('Back in progress');
    expect(describeTimelineEntry(entry({ fromStatus: 'Closed', status: 'Open' })).title).toBe('Reopened');
  });
});
```

---

## Documentation Tasks

### 23 — `docs/customer-portal.md` (new)

Sections:

1. Scope (CP1–CP5, five capabilities).
2. Identity resolution (`Customers.UserId`, 404 when unlinked, no email matching).
3. Role matrix (copy "Product rules").
4. API table (7 routes with methods, success codes and errors).
5. Submission pipeline (same creation hook, synchronous assignment P3, Medium/Open).
6. Timeline projection rules (P5, legacy, 200 cap).
7. Help Center visibility (Published + Public in SQL, categories P8, safe Markdown).
8. Feedback (P6/P7, unique index, check constraint, rowversion, reopened behaviour, no ticket side effects, future Reports & Management).
9. Angular routes and navigation.
10. Migration impact (`AddTicketFeedback`, additive) and rollback.
11. Out of scope / future (Communication Channels last, Reports & Management).

### 24 — Other docs

- `docs/identity-and-access.md`:
  - Matrix (after line 36): rows "Customer Portal: submit / list / view own tickets and their status history" (Customer ✔ own linked profile), "Customer Help Center: read Published + Public articles" (Customer ✔), "Ticket satisfaction feedback (own Resolved/Closed ticket)" (Customer ✔).
  - Routes table (before the `/api/health` row, line 83): `GET / POST /api/v1/portal/tickets`, `GET /api/v1/portal/tickets/{id}`, `GET /api/v1/portal/tickets/{id}/timeline`, `PUT /api/v1/portal/tickets/{id}/feedback`, `GET /api/v1/portal/help-center/articles[/{id}]`, `GET /api/v1/portal/help-center/categories`, each "Customer (own linked profile)".
- `docs/knowledge-base.md` line 8 (K2): replace "The future Customer Portal must expose…" with "The Customer Portal (PORTAL-001) exposes only `Published` + `Public` articles through `/api/v1/portal/help-center` (see [customer-portal.md](customer-portal.md))."
- `docs/ticket-management.md` line 3: replace "Customer Portal and SLA/automation remain future work" with "Customer self-service submission and tracking shipped in PORTAL-001 ([customer-portal.md](customer-portal.md))". Line 161: replace the Customer Portal bullet with "**Customer Portal:** shipped in `PORTAL-001`; portal tickets use the same creation hook and appear in staff lists like any other ticket."
- `README.md` line 3: append "and the Customer Portal (`PORTAL-001`)" to the list of delivered stories.

---

## Edge Cases & Failure Modes

- **Customer account without a CRM profile** (manual DB tampering, or a backfill that has not run): every portal endpoint ⇒ 404 "Customer profile not found." (`PortalCustomer.ResolveIdAsync`). The UI shows `NOT_LINKED`. No email lookup, no auto-creation in the request path (the startup backfill `CustomerProfileInitializer` remains the only creator).
- **Contact-only profile with the same email as a registered customer:** never matched. Resolution uses only `Customers.UserId` (`CustomerConfiguration.cs` line 24, unique). Signup never links existing contacts (`RegisterCustomerCommand.cs` line 34). Tickets on the contact-only profile are **not** visible in the portal. That is correct under CP1 (no claiming). Note it in `docs/customer-portal.md`.
- **Guessed ticket id of another customer:** `Owned(customerId)` filter in every statement ⇒ `FindAsync`/`FindHistoryAsync` null ⇒ 404 with the same text as a non-existent id. The feedback `PUT` loads through the same `FindAsync` before touching `TicketFeedback`, so another customer's id never reaches `GetFeedbackAsync`.
- **Staff token on portal routes / Customer token on staff routes:** policy `Customer` ⇒ 403 for Agent/SuperAdmin. Staff controllers keep `Staff` ⇒ 403 for Customers. No anonymous portal route (fallback policy `SecurityRegistration.cs` line 62 ⇒ 401).
- **Forged JSON fields** (`customerId`, `priority`, `status`, `assignedAgentUserId`, `role`) on `POST /portal/tickets`: not on `SubmitPortalTicketRequest` ⇒ ignored by System.Text.Json (no `UnmappedMemberHandling` configured). The handler hard-codes Medium/Open/null assignee and the resolved customer. Integer enum values ⇒ 400 (`allowIntegerValues: false`).
- **Whitespace-only subject/description, > 200 / > 4000 chars, missing/invalid category:** 400 from `SubmitPortalTicketValidator`. Angular blocks first (`Validators.pattern(/\S/)`, `maxLength`).
- **Double-click on Submit:** `submitting()` guard plus disabled button. Two real concurrent POSTs would create two tickets (no idempotency key). Accepted for the training scope and documented.
- **Automation assigns at creation (P3):** the ticket may already have an assignee in SQL. The portal DTO never shows it. A disabled rule target is skipped (`TicketCreationAutomation.cs` lines 44–48) ⇒ unassigned, still listed for the customer.
- **SLA disabled or not configured:** `ApplyAsync` writes no snapshot (lines 18–30). Portal behaviour is unchanged (no SLA in DTOs).
- **Legacy ticket without `Created` activity:** synthesized `Submitted` entry, `isRecorded: false`, "Request created / Earlier status history is not available…". Real `StatusChanged` rows still follow in order. No intermediate transition is invented.
- **More than 200 status events:** only the newest 200 are returned. If the `Created` row falls outside, a synthesized `Submitted` at `CreatedAtUtc` is prepended (`PortalTimeline.Build`). A true fact, documented.
- **Unparseable `OldValue`/`NewValue` on a `StatusChanged` row** (manual data edits): skipped (`Enum.TryParse`), never shown raw.
- **Equal timestamps:** `Id` tie-breaker (identity order) in `FindHistoryAsync`. The reverse keeps it stable.
- **Assignment / escalation / field edits / first response / automation events:** filtered out by the `where a.Type == Created || a.Type == StatusChanged` clause **and** their columns are never selected. Verified with API JSON (Verification 9).
- **Route param change on a reused detail component** (`/portal/tickets/A` → `/portal/tickets/B`): the `tap` reset clears ticket and timeline before the `switchMap` load, and `switchMap` cancels A's in-flight requests. The feedback card's `effect` resets its form on `ticketId` change.
- **Logout / account switch:** `endSession()` sets the session to null and navigates to `/sign-in`, which destroys every portal component. There is no root cache. A new user re-fetches everything.
- **Feedback on an Open/InProgress ticket:** 409 "Feedback can be given once the request is resolved or closed." The UI hides the form (`canSubmitFeedback` false).
- **Feedback rating 0/6/missing, comment > 1000:** 400 from the validator. The DB check constraint `CK_TicketFeedback_Rating` and `TicketFeedback.EnsureRating` are backstops.
- **Two first submissions racing:** the second insert violates `UX_TicketFeedback_TicketId` ⇒ `EfUnitOfWork` ⇒ 409 "Feedback for this request was already submitted. Reload to see it." On 409 the UI shows the message, and the user reloads to see the stored record.
- **Two overlapping edits:** `Version` rowversion ⇒ `DbUpdateConcurrencyException` ⇒ 409 "The record was changed by another request…" (`EfUnitOfWork.cs` lines 25–29).
- **Same values resubmitted:** `Update` returns false ⇒ no save, `UpdatedAtUtc` unchanged, 200 with the current state.
- **Ticket reopened after feedback (P7):** the record stays; `canSubmitFeedback` false ⇒ read-only plus hint; `PUT` ⇒ 409. When Resolved/Closed again ⇒ editable. Feedback is never deleted or reset.
- **Status changes between `FindAsync` and the feedback save** (staff reopens concurrently): the feedback write still commits (no lock on `Tickets`). Harmless: the record stays read-only on the next read. Documented. Feedback never writes `Tickets`, so `Tickets.Version` is untouched and staff saves never conflict with customer feedback.
- **Article unpublished, archived, returned to draft or switched to Internal while a customer has it open:** the next `GET …/articles/{id}` ⇒ 404 "This article is not available." The list no longer contains it. Categories without public articles disappear from the filter.
- **Published + Public article in an inactive category:** still listed (the category is shown as a filter option because it holds public articles). Matches staff behaviour where existing articles keep their category.
- **Untrusted content:** ticket text and feedback comments use interpolation (`.user-text`). Article bodies use `MarkdownView` (escaped HTML, safe links, no images) plus Angular's sanitizer.
- **Narrow screens (400 px):** lists are `.rows` cards (no tables), `.toolbar` wraps, `.readonly-list` collapses to one column under 600 px, Markdown code blocks scroll inside themselves.
- **Direct refresh of `/portal/...`:** `restoreSession()` runs in the app initializer, then `authGuard` and `roleGuard('Customer')` apply. A staff user hitting `/portal` ⇒ redirected to `/`.
- **No AI call** from any portal page or handler (no `IAiTextGenerator` dependency in `Crm.Application/Portal/`; grep check in Verification).

---

## Test Plan

1. **New backend unit test:** `backend/tests/Crm.UnitTests/Portal/SubmitPortalTicketCommandHandlerTests.cs` → `Handle_LinkedCustomerSubmitsTicket_DerivesProfileOpensMediumUnassignedAndRunsCreationAutomationOnce` (task 11). Fakes per `CreateTicketCommandHandlerTests.cs` lines 51–109.
2. **New frontend unit test:** `frontend/src/app/features/portal/portal-timeline.spec.ts` → `describeTimelineEntry` wording, including legacy and reopen (task 22). Pattern of `markdown.spec.ts`.
3. **Existing tests:** all must pass **unchanged** (no interface used by an existing fake changes). Baseline: backend 11, frontend 11. After: **12 and 12**.
4. **Manual (not automated)**: API (PowerShell `Invoke-RestMethod` / curl), SQL (LocalDB), browser checks. See Verification Steps 5–19. **No** Playwright/E2E, integration or SQL test projects (intake §13/§14).

---

## Migration / Rollback

- **Migration:** `AddTicketFeedback` creates one new table plus one unique index, one FK and one check constraint. Existing tables, rows and the ticket/KB schema are untouched. Existing tickets simply have no feedback.
- **Before applying:** record `SELECT COUNT(*) FROM Tickets; SELECT COUNT(*) FROM TicketActivities; SELECT COUNT(*) FROM KnowledgeArticles; SELECT COUNT(*) FROM Customers;`. Re-run after `dotnet ef database update` ⇒ identical.
- **Apply:** in `backend/`: `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` (or `.\run-dev.ps1`). Never `EnsureCreated`, drop or recreate `CrmDev`.
- **Rollback (local):** `dotnet ef database update AddKnowledgeBase --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` drops `TicketFeedback`, **including any feedback created during testing**. Confirm with the user first. Then run `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` if the migration is discarded. Tickets created through the portal stay valid (they are ordinary tickets).
- **Half-applied state:** the migration is a single `CREATE TABLE` plus index in one migration transaction, so it either exists completely or not at all. If the API runs against a database without the table, `GET /portal/tickets/{id}` fails (the left join targets a missing table) ⇒ 500. Apply the migration. `CustomerProfileInitializer` already logs a pending-migration warning on start (`CustomerProfileInitializer.cs` lines 19–26).

---

## Verification Steps

1. **Baseline:** in the repo root, `git status` ⇒ only the untracked customer-portal intake/plan folders. `git log --oneline -3` ⇒ `AI-001` present. In `backend/`: `dotnet test` ⇒ 11 passed. In `frontend/`: `npm test -- --watch=false` ⇒ 11 passed. Record both.
2. **Backend builds and tests:** in `backend/`: `dotnet build` (0 errors; list pre-existing warnings separately), `dotnet test` ⇒ 12 passed.
3. **Migration review and layering:** in `backend/`:
   - `dotnet ef migrations list --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ only `…_AddTicketFeedback (Pending)`.
   - `dotnet ef migrations script AddKnowledgeBase AddTicketFeedback --project src/Crm.Infrastructure --startup-project src/Crm.Api --output $env:TEMP/AddTicketFeedback.sql -- --environment Development` ⇒ one `CREATE TABLE [TicketFeedback]` with `[Version] rowversion`, `CONSTRAINT [CK_TicketFeedback_Rating] CHECK ([Rating] BETWEEN 1 AND 5)`, FK to `[Tickets]`, `CREATE UNIQUE INDEX [UX_TicketFeedback_TicketId]`. No `ALTER TABLE` on existing tables, no `DROP`/`UPDATE`/`DELETE`/`INSERT`.
   - Record the counts (Migration / Rollback) ⇒ `dotnet ef database update …` ⇒ counts unchanged.
   - `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ no changes.
   - `grep -rn "EntityFrameworkCore\|Microsoft.AspNetCore" src/Crm.Application src/Crm.Domain --include=*.cs` ⇒ nothing new. `grep -rn "IAiTextGenerator\|Gemini" src/Crm.Application/Portal src/Crm.Api/Controllers/Portal*.cs` ⇒ nothing.
4. **Frontend builds and tests:** in `frontend/`: `npx ng build` (no errors; budget warnings recorded; each portal component style ≤ 4 kB), `npm test -- --watch=false` ⇒ 12 passed.
5. **Setup (nothing existing deleted, reset or reassigned):** `.\run-dev.ps1`.
   - As SuperAdmin create Agent "Portal Alpha" (`portal.alpha@example.test`).
   - Register Customers A (`portal.a@example.test`) and B (`portal.b@example.test`) via `/sign-up`.
   - As SuperAdmin create a **contact-only** customer "Portal Contact" with email `portal.c@example.test`.
   - Get tokens `$admin`, `$alpha`, `$custA`, `$custB` from `POST /api/v1/auth/login` (never paste them into the report).
   - SQL `SELECT Id, UserId, Email FROM Customers WHERE Email LIKE 'portal.%'` ⇒ A/B have `UserId`, Contact has `NULL`. Record `CustA_Id`, `CustB_Id`.
   - As SuperAdmin create staff ticket TS1 for **Customer A** (assigned to Alpha), and TS2 for Customer B.
6. **Access (§13.1, §13.13):**
   - `$custA` `GET /api/v1/portal/tickets` ⇒ 200.
   - `$alpha` and `$admin` on every `/portal/*` route ⇒ 403. No token ⇒ 401.
   - `$custA` on `GET /api/v1/tickets`, `/tickets/{TS1}`, `/tickets/{TS1}/activity`, `/knowledge-base/articles`, `POST /tickets/{TS1}/ai/summary`, `/api/v1/agents` ⇒ 403 each.
   - Unlinked check (optional; only on a disposable account): `UPDATE Customers SET UserId = NULL WHERE Email = 'portal.b@example.test'` ⇒ `$custB` `GET /portal/tickets` ⇒ 404 "Customer profile not found.". **Restore it immediately** with `UPDATE Customers SET UserId = '<B user id>' WHERE Email='portal.b@example.test'`. If skipped, mark it **Unverified**.
   - There is no request field through which A could claim "Portal Contact": confirm in `openapi/v1.json` that no portal request schema has `customerId`/`userId`.
7. **Submission (§13.2–§13.4):**
   - Optionally create an enabled `TicketCreated` automation rule for `Billing` → Alpha in `/sla` (record whether one exists).
   - `$custA` `POST /api/v1/portal/tickets` `{ "subject": "  Invoice is wrong ", "description": "Charged twice.", "category": "Billing", "priority": "Urgent", "status": "Closed", "customerId": "<CustB_Id>", "assignedAgentUserId": "<alpha>" }` ⇒ **201**, `Location` `/api/v1/portal/tickets/{P1}`, body `status: "Open"`, `subject: "Invoice is wrong"`, and **no** `priority`/assignee/customer properties.
   - SQL `SELECT CustomerId, CreatedByUserId, Priority, Status, AssignedAgentUserId, SlaFirstResponseDueAtUtc, CreatedAtUtc FROM Tickets WHERE Id='<P1>'` ⇒ `CustomerId = CustA_Id`, `CreatedByUserId` = A's user id, `Medium`, `Open`, assignee = Alpha **only** if the rule exists (else NULL), SLA due set if SLA is enabled.
   - `SELECT Type, ActorUserId, ToAgentUserId, Reason FROM TicketActivities WHERE TicketId='<P1>' ORDER BY Id` ⇒ exactly one `Created`, plus one automatic `AssigneeChanged` only if the rule matched. **No duplicates.** `SELECT COUNT(*) FROM AutomationRuleExecutions WHERE TicketId='<P1>'` ⇒ 0 or 1.
   - Validation: subject `"   "` ⇒ 400 `subject`; 201-char subject ⇒ 400; missing category ⇒ 400 `category`; `"category": 2` ⇒ 400.
   - As SuperAdmin, `/tickets/{P1}` in the staff UI shows "Created by" Customer A and the normal cards.
8. **Tracking (§13.5–§13.7):**
   - `$custA` `GET /portal/tickets` ⇒ P1 **and** TS1 (staff-created), newest first; no TS2.
   - `?status=Open` filters correctly. `?search=Invoice` ⇒ P1. `?search=<P1 id>` ⇒ P1. `?page=2&pageSize=1` ⇒ second row, `totalCount` correct.
   - As SuperAdmin move TS1 Open→InProgress→Resolved→Closed. `$custA` list shows TS1 `Closed`.
   - `$custA` `GET /portal/tickets/{TS2}`, `/timeline` and `PUT …/feedback` ⇒ 404 each (same text as a random GUID). `$custB` on P1 ⇒ 404.
   - `$custA` `GET /portal/tickets/{P1}` JSON contains only `id, subject, description, category, status, createdAtUtc, canSubmitFeedback, feedback`.
9. **Timeline (§13.8, §13.9):**
    - As SuperAdmin create ticket TS3 for Customer A, assigned to Alpha. Then: escalate it, change its priority, record first response, reassign it to unassigned and back to Alpha, move it Open→InProgress→Resolved→Closed, then reopen Closed→Open.
    - `$custA` `GET /portal/tickets/{TS3}/timeline` ⇒ exactly `Submitted` (recorded), `Open→InProgress`, `InProgress→Resolved`, `Resolved→Closed`, `Closed→Open`, in that order with non-decreasing `occurredAtUtc`.
    - The raw JSON contains **no** `actorName`, `reason`, `field`, agent names, and no entry for the escalation, priority change, first response or assignment events (`Escalated`, `EscalationAutoCleared`, `FieldChanged`, `AssigneeChanged`, `FirstResponseRecorded`). No SLA data.
    - Legacy (§8): `SELECT t.Id FROM Tickets t JOIN Customers c ON c.Id = t.CustomerId WHERE c.UserId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM TicketActivities a WHERE a.TicketId = t.Id AND a.Type = 'Created')`. If a row exists for a portal test customer, its timeline has one `isRecorded: false` submission entry and only its real recorded transitions. If none exists, mark it **Unverified** and state why. **Never delete or edit activity rows to simulate it.**
10. **Help Center API (§13.10–§13.12):**
    - As SuperAdmin publish: H1 Public FAQ, H2 Public Guide (Markdown with heading, list, code, a safe link, `<script>`, `[evil](javascript:alert(1))`), H3 **Internal** Solution. Leave H4 as a Public Draft, H5 Public ReadyForPublication, and archive H6 (Public).
    - `$custA` `GET /portal/help-center/articles` ⇒ H1, H2 only. `?type=Faq` ⇒ H1. `?categoryId=` filters. `?search=` matches title/summary/body. Paging correct. Rows have no `body`/`authorName`/`status`/`visibility`.
    - `GET …/articles/{H3|H4|H5|H6}` ⇒ 404 each. `GET …/categories` ⇒ only categories of H1/H2, `{ id, name }` only.
    - Switch H1 to Internal (staff `PUT`) ⇒ `$custA` `GET …/articles/{H1}` ⇒ 404 immediately. Switch back. Archive H2 ⇒ 404. Restore, resubmit and republish H2 ⇒ 200 again.
    - Staff KB list/publish/link workflows are unchanged (`$alpha` sees H3).
11. **Feedback (§13.14–§13.17):**
    - Snapshot `SELECT Status, UpdatedAtUtc, AssignedAgentUserId, FirstRespondedAtUtc, CONVERT(varchar(20), Version, 1) FROM Tickets WHERE Id='<TS1>'; SELECT COUNT(*) FROM TicketActivities WHERE TicketId='<TS1>';`.
    - `$custA` `PUT /portal/tickets/{TS1}/feedback` `{ "rating": 4, "comment": "Quick help" }` ⇒ 200 with `feedback.rating 4`. Repeat with `{ "rating": 5, "comment": "Great" }` ⇒ 200, updated.
    - SQL `SELECT TicketId, Rating, Comment, CreatedAtUtc, UpdatedAtUtc FROM TicketFeedback WHERE TicketId='<TS1>'` ⇒ **one** row, `UpdatedAtUtc > CreatedAtUtc`.
    - Re-run the snapshot ⇒ **identical**.
    - `{ "rating": 0 }`, `{ "rating": 6 }`, `{}` ⇒ 400 `rating`. 1001-char comment ⇒ 400 `comment`.
    - On P1 (Open) ⇒ 409. On TS2 ⇒ 404.
    - Race: two parallel PowerShell jobs `PUT` a first rating on a freshly Resolved ticket ⇒ one 200, one 200 (update) **or** 409. Either way one row.
    - Direct SQL `INSERT INTO TicketFeedback (TicketId, Rating, CreatedAtUtc, UpdatedAtUtc) VALUES ('<TS1>', 3, SYSDATETIMEOFFSET(), SYSDATETIMEOFFSET())` ⇒ unique violation. `… Rating 9 …` on another ticket ⇒ check violation (run inside `BEGIN TRAN … ROLLBACK`).
    - No email/notification/message rows appear (`SELECT COUNT(*) FROM TaskReminderNotifications`, `SlaAlerts` unchanged).
12. **Reopen (§13.16):** as SuperAdmin TS1 Closed→Open ⇒ `$custA` `GET /portal/tickets/{TS1}` ⇒ `canSubmitFeedback: false`, `feedback` still present. `PUT` ⇒ 409. SQL row unchanged. Move TS1 InProgress→Resolved ⇒ `PUT` works again.
13. **Browser — portal (§13.18):**
    - As Customer A: nav shows Home, Support Portal, My Tickets, New Ticket, Help Center, Profile (no staff entries). Home shows the Customer support card.
    - `/portal` shows recent requests.
    - New Ticket: inline validation, button disabled while pending, success snackbar, redirect to details.
    - My Tickets: search/status filter, paging, empty state and CTA.
    - Details: reference copy, safe fields only, the timeline wording from `describeTimelineEntry`, the feedback form on Resolved/Closed only, the read-only state after reopening.
    - Help Center: type/category filters, search, paging, article page with rendered Markdown. Literal `<script>`, no alert, "evil" as plain text, the safe link opens in a new tab.
    - Stop the API ⇒ error messages and Retry. Open `/portal/tickets/<TS2 id>` ⇒ "This request was not found." Navigate from one ticket to another via the URL bar ⇒ no flash of the previous ticket.
    - Refresh every portal route ⇒ same data.
    - Sign out, sign in as Customer B ⇒ only B's data (no cached A data).
    - As Alpha open `/portal` ⇒ redirected to `/`.
14. **Narrow screen:** DevTools ~400 px on every portal page ⇒ toolbars wrap, no horizontal page scroll, readonly list single-column, code blocks scroll inside themselves.
15. **No AI (§13.19):** with DevTools Network open, use every portal page ⇒ no `/ai/` request. Backend logs show no Gemini call.
16. **Regression smoke (§13.19):**
    - Sign in/out for each role.
    - Staff ticket create/edit/assign/transition/escalate/activity.
    - Agent Dashboard counts.
    - Customer list/detail, notes and attachments.
    - KB create/submit/publish/link.
    - SLA card, `/sla` pages, bell.
    - AI card on staff Ticket Details still works only on click.
    - Tasks, quick replies, collaboration.
17. **OpenAPI:** `curl -s http://localhost:5080/openapi/v1.json` lists the 7 portal operations, each with "Requires role: Customer", enums as strings, and no `[AllowAnonymous]`.
18. **Secret hygiene and provenance (§13.20):** `git diff --stat` shows no `appsettings*.json` secret, no key, no token. Demo data was created only through the steps above (record it). Any rows touched by SQL in step 6 are restored.
19. **Record honestly:** list which of steps 5–18 were run and the observed results. Anything not run (especially browser steps 13–15, the optional unlinked check and the legacy-ticket check) is **Unverified**, not passed. Report `dotnet build`, `dotnet test`, `ng build`, `npm test`, migration status, P1–P11, and remaining blockers. Keep pre-existing warnings separate. **Do not commit or push before the user accepts PORTAL-001.**

---

## Done Criteria

- [ ] Every portal endpoint uses `AuthorizationPolicies.Customer` and resolves the CRM customer **only** through `Customers.UserId` from the JWT `sub`. Unlinked ⇒ 404 "Customer profile not found." No request DTO carries customer, user, role or ownership fields (CP1, P1).
- [ ] `POST /portal/tickets` accepts subject/description/category only. It creates an `Open`/`Medium` ticket for the resolved customer with the customer's own account as creator. It runs `ITicketCreationAutomation` once and saves once, with exactly one `Created` activity and no duplicate SLA/automation effects. `CreateTicketCommand`/`ITicketRepository` are unchanged (CP2, P2, P3).
- [ ] My Tickets lists every ticket of the customer (portal- and staff-created, all statuses), filtered in SQL, newest first, with paging, subject/id search and status filter. Details expose only id, subject, description, category, status, created time and feedback state. Other customers' ids ⇒ 404 (CP3, P4, P10).
- [ ] The timeline shows submission plus real status changes (incl. reopen and resume) oldest first, synthesizes a clearly labelled creation entry only for tickets without a `Created` event, and never selects or returns actor, assignment, escalation, field, reason, first-response or SLA data (CP3, P5).
- [ ] The Help Center lists, searches and filters (type, category) and reads only `Published` + `Public` articles in SQL, for both list and direct id (else 404). Categories endpoint returns safe names of categories with public articles. Bodies render through `MarkdownView` (CP4, P8).
- [ ] `TicketFeedback` (unique `TicketId`, rating check 1–5, comment ≤ 1000, rowversion) is created/updated through one `PUT` only for owned Resolved/Closed tickets. It is kept and shown read-only after reopening, and never changes the ticket, its activity, SLA or assignment (CP5, P6, P7).
- [ ] One additive migration `AddTicketFeedback`. `has-pending-model-changes` reports none. Existing row counts unchanged.
- [ ] Angular: Customer-only `/portal` route group and nav entries, landing, My Tickets, New Ticket, Ticket Details with timeline and feedback card, Help Center list and article. Loading, empty, error/Retry, 404 and validation states. Works at 400 px. No portal data survives logout or ticket switch.
- [ ] Staff UI, staff APIs, KB staff rules and AI-001 are unchanged except the P11 text updates. No portal code calls Gemini.
- [ ] Exactly one new backend test and one new frontend test. All existing tests pass (12/12 each after). Both builds succeed. No new package.
- [ ] `docs/customer-portal.md` created. Identity, KB, ticket docs and README updated.
- [ ] Manual matrix (Verification 5–18) recorded honestly. Unrun steps marked Unverified. Nothing committed or pushed until the user reviews and accepts PORTAL-001.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
