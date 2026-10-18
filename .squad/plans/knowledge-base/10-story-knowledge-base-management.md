# Story 10 — Knowledge Base Management — Articles, FAQs, Solutions, Guides, Publishing, Search & Ticket Integration

## Prerequisites

- **Story 09 completed:** [../sla-automation/09-story-sla-basic-automation.md](../sla-automation/09-story-sla-basic-automation.md) (`SLA-001`, committed as `2702200` on `master` at planning time). At planning time the working tree had only the untracked `.squad/stories/knowledge-base/` intake and `.squad/plans/knowledge-base/` folder. **Run `git status` before implementing. Stop and report if any application file is modified.** Do not depend on the exact commit id.
- **Story 07 completed:** [../agent-dashboard/07-story-agent-productivity-collaboration.md](../agent-dashboard/07-story-agent-productivity-collaboration.md) (`DASH-002`, `b9eec58`). This story copies the **Quick Replies** stack most closely: `QuickReplyTemplate` (archive/restore, no hard delete), `QuickReplyGuards`, `QuickReplyRepository.Visible` (visibility in SQL), `QuickRepliesController` (`StaffActor` from the JWT), `copyQuickReply`, `QuickReplyPanel` (debounced search card on Ticket Details). Also `StaffActor`, `ForbiddenException`, and `EfUnitOfWork.KnownUniqueIndexes`. **The DASH-002 collaborator access path (`CollaborationWriteAccess`, `ITicketAccessLock`) is deliberately not used:** collaborators get no Knowledge Base access to a ticket (intake §2, §10).
- **Stories 04–05 completed:** [../ticket-management/04-story-ticket-creation-management.md](../ticket-management/04-story-ticket-creation-management.md), [../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md). `TicketRepository.Scope` is **the** ticket authorization boundary: SuperAdmin sees every ticket, an Agent only tickets currently assigned to them, and anything else returns 404. Also `Tickets.Version` as a shadow rowversion ⇒ 409, and the Angular `TicketDetailPage` with its 404 handling ("no longer available").
- **Story 08 completed:** [../customer-management/08-story-customer-notes-interactions-attachments.md](../customer-management/08-story-customer-notes-interactions-attachments.md) (`CUST-002`, `6b569b2`). Same tone and test policy. Precedent for `bigint IDENTITY` link rows and for "one new backend + one new frontend test".
- **Stories 01–02 completed:** Mediator/CQRS pipeline (`ValidationBehavior`, `GlobalExceptionHandler`), JWT `sub`/`role`, `User.GetUserId()`, `AuthorizationPolicies.Staff` / `.SuperAdmin`, `roleGuard`, the auth interceptor and `NAV_ITEMS`.
- Applied migrations (verified in `backend/src/Crm.Infrastructure/Persistence/Migrations/`): `…_InitialCreate`, `…_AddIdentityAndRefreshSessions`, `…_AddCustomers`, `…_AddTickets`, `…_AddTicketWorkflowAndActivity`, `…_AddAgentProductivityAndCollaboration`, `20261018010157_AddCustomerNotesInteractionsAttachments`, `20261018022517_AddSlaAndAutomation`. **This story adds exactly one additive migration, `AddKnowledgeBase`, and edits none of the existing ones.**
- Tooling: .NET SDK 10, EF Core 10, Mediator (source-generated), FluentValidation, xUnit 2.9.3 (`backend/tests/Crm.UnitTests/Crm.UnitTests.csproj` line 22 references only `Crm.Application`), Angular / Material 22.2, Vitest via `ng test`. **One new npm package: `marked` `18.0.14`** (published 2026-09-22, zero dependencies). It is the "one small maintained library" the intake allows (§8). **No new NuGet packages.**
- Intake: `.squad/stories/knowledge-base/KB-001/intake.md` (`attachments/` is empty). The original *Customer Support CRM — Core Features* PDF is **not** in the repository. The approved intake, including **K1–K4**, is the only requirements source.
- Shared contracts touched (additive only): `CrmDbContext` (+3 `DbSet`s), `Infrastructure/DependencyInjection.cs` (+2 repositories), `EfUnitOfWork.KnownUniqueIndexes` (+2 entries), Angular `app.routes.ts` (+1 route), `NAV_ITEMS` (+1 entry), `TicketDetailPage` (+1 card), `frontend/package.json` (+`marked`), docs. **No change** to `Ticket`, `TicketDetails`, `TicketResponse`, `ITicketRepository`, `TicketRepository`, `TicketsController`, `TicketActivity`, SLA, quick reply, collaboration, customer or notification code. No existing migration or test changes.

---

## Story Goal

Deliver the four Knowledge Base requirements of the core-features document (**FAQs, help articles, solutions and guides, search**) as one working staff module, under the approved decisions K1–K4:

1. **One article concept, four types:** `Faq`, `HelpArticle`, `Solution`, `Guide`. Each article stores a title, optional summary, Markdown body, type, category, visibility (`Internal`/`Public`), lifecycle status, author, timestamps and publication metadata. For an FAQ, the title is the question and the body is the answer.
2. **Categories:** a flat list maintained by SuperAdmin (add, rename, deactivate/reactivate). Names are unique. Deactivated categories stay valid on existing articles but cannot be chosen for new or re-categorized articles.
3. **Lifecycle (K1):** `Draft` → `ReadyForPublication` → `Published` → `Archived`. Agents create their own Drafts, edit them and submit them. **Only SuperAdmin** returns a submission to Draft (optional reason), publishes, archives and restores (Archived → Draft). Nothing is hard-deleted.
4. **Visibility (K2):** `Internal`/`Public` is stored and returned. Every read applies the status rule server-side: staff see Published articles of both visibilities. Unpublished articles are visible only to their author and SuperAdmin. Archived articles are visible only to SuperAdmin. **There is no customer or anonymous endpoint.** Customer tokens get 403 and anonymous requests get 401.
5. **Markdown (K3):** a textarea editor with Write/Preview tabs and a short syntax help. Rendering uses `marked` with raw HTML escaped, images reduced to their alt text and only `http:`/`https:`/`mailto:` links. Angular's `[innerHTML]` sanitizer is a second layer. The same renderer is used for preview and reading. Only the Markdown source is stored.
6. **Search:** a server-side `LIKE` search over title, summary and body, with type, category and status filters, the caller's own articles, one-based paging (default 20, max 100) and a deterministic order (`UpdatedAtUtc DESC, Id DESC`).
7. **Ticket integration (K4):** a "Knowledge Base" card on the existing Ticket Details page. Staff can search Published articles, read them, copy their Markdown and link or unlink them. Links persist in `TicketKnowledgeArticleLinks` (unique per ticket and article, maximum 25 per ticket). Ticket access uses `TicketRepository.Scope` on every request, so collaborators get nothing. Linking never changes the ticket (status, assignment, SLA, `UpdatedAtUtc`, activity). Links to archived articles remain and show "Archived". The article body is not exposed through the link.
8. Exactly **one** new backend unit test and **one** new frontend unit test, plus a documented manual API / SQL / browser matrix. A new `docs/knowledge-base.md` and small updates to existing docs.

**Not in scope** (do **not** build): Customer Portal or any public/anonymous article endpoint, customer feedback or ratings; email/chat/SMS sending or "send to customer"; AI suggestions or auto-recommendations from ticket text; revision history, diffing or version snapshots; WYSIWYG editors, embedded HTML, images or uploads; comments, likes or analytics; full-text indexes or external search; category hierarchies; collaborator access changes; new test frameworks; integration or E2E suites.

### Conflicts and interpretations (surface these in the implementation report)

| # | Intake point | Resolution in this plan |
|---|---|---|
| I1 | §5 requires a category on every article, and §11 forbids seeding content. | The category is **required**, and the migration seeds **no** categories. Until a SuperAdmin creates the first category, the editor shows "No active categories yet — ask a SuperAdmin to add one." and the article form cannot be submitted. This is part of the manual setup in Verification step 4. |
| I2 | §4 lists "Submit own Draft" for both roles. §6 says SuperAdmin restores an Archived article to **Draft** and then publishes again "after review". | Restored articles keep their original Agent author. If only the author could submit, SuperAdmin could never republish them. **SuperAdmin may therefore submit any Draft.** Agents may submit only their own Drafts, which are the only Drafts they can see. Publish stays strictly `ReadyForPublication → Published`, so no Draft is ever published directly. |
| I3 | §6: "Only SuperAdmin can archive an article." | Archive is allowed from `Draft`, `ReadyForPublication` and `Published`. This gives SuperAdmin a way to retire abandoned drafts without hard delete. Restore always goes to `Draft` and clears the publish metadata. |
| I4 | §11 suggests `/api/v1/knowledge-base/articles`. `docs/api-conventions.md` line 3 asks for plural top-level resources. | The routes use `/api/v1/knowledge-base/articles` and `/api/v1/knowledge-base/categories` as the intake suggests. This is documented as a module-prefix exception next to the existing `agent-dashboard` exception. Ticket links are nested under the existing `/api/v1/tickets/{ticketId}/knowledge-articles`. |
| I5 | §10 requires ticket authorization "per request". DASH-002 locks rows (`ITicketAccessLock`) for collaborator writes. | Link and unlink check `TicketRepository.Scope` with a plain query and then save once (not transactional). This is the same pattern as `RecordFirstResponseCommand`. If the ticket is reassigned between the check and the insert, a link can still be added in that millisecond window. A link grants no ticket access, so the risk is accepted under §11 ("no concurrency-retry framework … merely for … ticket links"). Documented in Edge Cases. |
| I6 | §6: "Show a clear Archived reference" on linked tickets. | The link list returns **metadata only**: title, type, category name, status, who linked it and when. It never returns the body. An Agent opening an archived or unpublished linked article gets 404 from `GET /knowledge-base/articles/{id}`, and the card shows "No longer published". |

### Recorded decisions

| Concern | Decision |
|---|---|
| Naming | Domain namespace `Crm.Domain.KnowledgeBase`: `KnowledgeArticle`, `KnowledgeArticleType`, `KnowledgeArticleStatus`, `KnowledgeArticleVisibility`, `KnowledgeArticleWorkflow`, `KnowledgeCategory`, `TicketKnowledgeArticleLink`. Tables `KnowledgeArticles`, `KnowledgeCategories`, `TicketKnowledgeArticleLinks`. Use cases under `Crm.Application/KnowledgeBase/<UseCase>/`. |
| Actor | Reuse `StaffActor` (`Crm.Application/Common/StaffActor.cs` lines 7–11), built in controllers as `new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin))` (precedent: `QuickRepliesController.cs` line 73). Author, updater, publisher and linker ids always come from it. **No request DTO has a status, author, publisher or role field.** |
| IDs | Articles and categories: `Guid.CreateVersion7()` (as `CreateQuickReplyCommand.cs` line 23). Links: `long`, `bigint IDENTITY` (as `CustomerNoteConfiguration.cs` line 14). |
| Enums | Stored as member names (`HasConversion<string>().HasMaxLength(30)`, as `QuickReplyTemplateConfiguration.cs` lines 19–20). JSON uses names (`"Faq"`, `"ReadyForPublication"`) through the global `JsonStringEnumConverter` (`Crm.Api/DependencyInjection.cs` line 17). Integers are rejected. UI labels: "FAQ", "Help Article", "Solution", "Guide", "Ready for Publication". |
| Limits | `KnowledgeBaseRules`: title 200, summary 500, body 20,000, category name 100, return reason 500, search 100, max links per ticket 25. Title and body must not be empty or whitespace (FluentValidation `NotEmpty`), are trimmed on save, and an empty summary is stored as `null`. |
| Read visibility (SQL, applied first) | SuperAdmin: every article. Agent: `Status == Published` **or** (`AuthorUserId == me` **and** `Status in (Draft, ReadyForPublication)`). Anything not visible returns **404** (not 403), so its existence is not revealed (`docs/api-conventions.md`, auth paragraph). |
| Edit rule | Archived ⇒ **409** "The article is archived. Restore it first." SuperAdmin may edit Draft, ReadyForPublication and Published articles, and the status is unchanged. Agent may edit only their own Draft. Own ReadyForPublication ⇒ **409** "This article is waiting for publication. Ask a SuperAdmin to return it for revision before editing." A Published article ⇒ **403** "Published articles are maintained by SuperAdmin." (visible but not changeable, same rule as `QuickReplyGuards.cs` lines 9–19). |
| Workflow | `KnowledgeArticleWorkflow.IsAllowed(from, to)` is the single transition matrix (style of `TicketWorkflow.cs` lines 3–18). Endpoints: `POST {id}/submit` (Staff), and `POST {id}/return`, `/publish`, `/archive`, `/restore` with **`[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]`**, so an Agent gets 403 from the policy (K1) before any handler runs. The handler also throws `ForbiddenException` for non-SuperAdmins on those actions as a backstop. A wrong current status ⇒ 409. |
| Concurrency | `KnowledgeArticles` gets a shadow rowversion `Version` exactly like `TicketConfiguration.cs` line 39. Two overlapping saves on the same article ⇒ the second gets `DbUpdateConcurrencyException`, which `EfUnitOfWork.cs` lines 23–27 turn into a 409. No ETag or `If-Match`, and no transactions. Every command saves **once**. |
| Uniqueness | `UX_KnowledgeCategories_Name` (case-insensitive under the default `SQL_Latin1_General_CP1_CI_AS` collation) and `UX_TicketKnowledgeArticleLinks_TicketArticle`. Handlers pre-check with a query and return a friendly 409. Both index names are also added to `EfUnitOfWork.KnownUniqueIndexes` (lines 12–15), so a concurrent duplicate becomes 409, not 500. |
| Search | Parameterized `string.Contains` (EF escapes LIKE wildcards; comment precedent `TicketRepository.cs` line 66) on `Title`, `Summary`, `Body`. Body is `nvarchar(max)`. No full-text index (intake §9, §14). |
| Lists | Articles: paged `PagedResponse<KnowledgeArticleSummaryResponse>` **without body**. Categories: unpaged `IReadOnlyList`, ordered by name (a small admin list, precedent `ListAutomationRulesQuery.cs` line 7). Ticket links: unpaged, bounded by the 25-link cap, ordered `LinkedAtUtc DESC, Id DESC`. |
| Ticket links | `POST /api/v1/tickets/{ticketId}/knowledge-articles` `{ articleId }` ⇒ 201. `GET` ⇒ 200 list. `DELETE …/{articleId}` ⇒ 204. Ticket not in scope ⇒ 404 "Ticket '{id}' was not found." (same text as `GetTicketQuery.cs` line 13). Article not visible ⇒ 404. Article not Published ⇒ 409 "Only published articles can be linked to a ticket." Duplicate ⇒ 409. More than 25 ⇒ 409. Unlink on a missing link ⇒ 404. **The ticket row is never loaded for tracking** (`AnyAsync` only), so `Tickets.UpdatedAtUtc` / `Version` and `TicketActivities` cannot change. |
| Markdown | `frontend/src/app/features/knowledge-base/markdown.ts` `renderMarkdown(source)` uses a private `Marked` instance (not the global `marked`) with renderer overrides `html` ⇒ escaped text, `image` ⇒ escaped alt text, `link` ⇒ `<a … target="_blank" rel="noopener noreferrer">` for `http`/`https`/`mailto` only, otherwise the label as plain text. Displayed through `MarkdownView` (`[innerHTML]`, which Angular sanitizes). Copy copies the stored Markdown source through the existing `copyQuickReply` helper. |
| UI placement | New lazy route `/knowledge-base` with `roleGuard('SuperAdmin', 'Agent')` and a nav entry "Knowledge Base" (icon `menu_book`) after "Quick Replies". The new `TicketKnowledgeArticles` component is rendered as the last card inside the existing `@if (ticket(); as current)` block of `ticket-detail-page.html`. The collaboration ticket page (`/collaboration/:ticketId`) is **not** changed. |

---

## Context — Read These Files First

1. `.squad/stories/knowledge-base/KB-001/intake.md` — the contract: §3 (K1–K4), §4 role table, §5–§10 behaviour, §11 backend boundaries, §13 the 13 manual checks, §14 out of scope.
2. [../sla-automation/09-story-sla-basic-automation.md](../sla-automation/09-story-sla-basic-automation.md) — tone, migration review wording (task 9), Verification and Done Criteria style. [../customer-management/08-story-customer-notes-interactions-attachments.md](../customer-management/08-story-customer-notes-interactions-attachments.md) — "Recorded decisions" style.
3. `backend/src/Crm.Domain/QuickReplies/QuickReplyTemplate.cs` lines 1–94 — **the** entity template: private EF ctor, private setters, static factories, `bool Update(...)` returning "changed", `DomainException` backstop, archive/restore with `UpdatedByUserId`/`UpdatedAtUtc`. `backend/src/Crm.Domain/Tickets/TicketWorkflow.cs` lines 3–18 — transition-matrix style.
4. `backend/src/Crm.Application/QuickReplies/` — `QuickReplyDetails.cs` lines 6–13 (read model + filter record), `QuickReplyGuards.cs` lines 7–20 (403 guard), `CreateQuickReply/CreateQuickReplyCommand.cs` lines 11–46 (command + handler + validator in one file, trim, single save, read-back), `UpdateQuickReply/UpdateQuickReplyCommand.cs` lines 11–49 (404 → guard → 409 → change → save if changed), `SetQuickReplyArchived/SetQuickReplyArchivedCommand.cs` lines 8–34, `ListQuickReplies/ListQuickRepliesQuery.cs` lines 12–42 (search trim, `Math.Min(pageSize, Paging.MaxPageSize)`, page validator).
5. `backend/src/Crm.Application/Abstractions/Persistence/IQuickReplyRepository.cs` lines 8–23 and `backend/src/Crm.Infrastructure/Persistence/Repositories/QuickReplyRepository.cs` lines 10–75 — `Visible(...)` applied first (46–58), `Project(...)` with user joins and `orderby` before `select` (60–74), paged `ListAsync` with `Contains` search (20–44).
6. `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketRepository.cs` lines 82–84 — `internal static IQueryable<Ticket> Scope(...)`, the ticket boundary to reuse. `AgentDashboardRepository.cs` line 79 shows it called from another repository. `Crm.Application/Tickets/GetTicket/GetTicketQuery.cs` lines 9–14 (404 text). `Crm.Application/Tickets/RecordFirstResponse/RecordFirstResponseCommand.cs` lines 8–40 (single save, not transactional).
7. `backend/src/Crm.Infrastructure/Persistence/Configurations/QuickReplyTemplateConfiguration.cs` lines 8–29 (string enums, `Restrict` FKs to `ApplicationUser`, composite indexes). `TicketCollaboratorConfiguration.cs` lines 11 and 25 (named unique index constant). `TicketConfiguration.cs` line 39 (shadow rowversion). `CustomerNoteConfiguration.cs` lines 10–21 (identity `long` id, descending index).
8. `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs` lines 21–37 (DbSets) and 44 (`ApplyConfigurationsFromAssembly`, so new configurations need no registration). `backend/src/Crm.Infrastructure/DependencyInjection.cs` lines 37–51 (repository registrations). `backend/src/Crm.Infrastructure/Persistence/EfUnitOfWork.cs` lines 12–15 (`KnownUniqueIndexes`), 23–31 (409 mapping).
9. `backend/src/Crm.Api/Controllers/QuickRepliesController.cs` lines 20–74 (class attributes, `EndpointDescription` wording, `[FromQuery]` filters, `CreatedAtAction`, `Actor`). `TicketsController.cs` lines 24–27 and 76–80 (per-action `[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]`). `Contracts/QuickReplies/QuickReplyContracts.cs` lines 1–20 (several records + `From(...)` in one file). `Contracts/Common/PagedResponse.cs` line 4. `ApiRoutes.cs` line 10. `Authentication/AuthorizationPolicies.cs` lines 5–8.
10. `backend/src/Crm.Api/Errors/GlobalExceptionHandler.cs` lines 20–78 — `ValidationException`⇒400, `NotFoundException`⇒404, `ForbiddenException`⇒403, `DomainException`⇒422, `ConflictException`⇒409. **No change needed.** `Crm.Application/AgentTasks/AgentTaskGuards.cs` line 13 — the `Invalid(property, message)` ⇒ 400 helper to copy.
11. `backend/src/Crm.Application/Common/Validation/ProductivityRules.cs` lines 19–24 (rules-constants style). `Common/Paging/PagedResult.cs` lines 4–11.
12. `backend/tests/Crm.UnitTests/Customers/UploadCustomerAttachmentCommandHandlerTests.cs` — whole file (77 lines): naming, hand-written fakes with `NotSupportedException` for unused members (39–76), `FakeUnitOfWork.SaveCount`, `FixedTimeProvider`. **The pattern for the single new backend test.**
13. `frontend/src/app/features/quick-replies/` — `quick-reply-list-page.ts` lines 26–130 + `.html` lines 1–84 (toolbar filters, `Subject` + `switchMap` loader, paginator, retry), `quick-reply-edit-page.ts` lines 21–124 + `.html` lines 1–53 (form, load 404 message, disable when read-only), `quick-reply-panel.ts` lines 18–59 + `.html` lines 1–22 (debounced card search), `copy-quick-reply.ts` lines 4–8 (**reuse it as is**), `quick-replies-api.ts` lines 8–47, `quick-reply-models.ts` lines 1–37, `quick-replies.routes.ts` lines 3–7.
14. `frontend/src/app/features/tickets/ticket-detail-page.ts` lines 1–59 (imports array) and 248–252 (`onAccessLost`). `ticket-detail-page.html` line 9 (`@if (ticket(); as current)`), lines 207–212 (discussion card with `(accessLost)`), 221–224 (Quick replies card, the last card), 225 (`</div>` closing `detail-stack`). `ticket-detail-page.scss` lines 1–44.
15. `frontend/src/app/features/collaboration/ticket-discussion.ts` lines 35–36 (`accessLost = output<void>()`) and 52–58 (404 ⇒ emit). The new ticket card copies this **only for its link-list load**. Link and unlink 404s are ambiguous and are handled differently (task 15, F1).
16. `frontend/src/app/shared/ui/productivity-page.scss` lines 1–88 (`.toolbar`, `.search`, `.filter`, `.rows`, `.row`, `.row-title`, `.meta`, `.hint`, `.error`, `.stack`, `.form`, `.actions`). `shared/ui/status-badge/status-badge.ts` line 3 (`StatusTone`). `shared/ui/page-header/page-header.ts`.
17. `frontend/src/app/core/api/describe-http-error.ts` lines 5–14 and `frontend/src/app/features/sla/sla-errors.ts` lines 6–10 (`describeSaveError`, which joins 400 field messages; **reuse it for article and category saves**). `frontend/src/app/features/customers/customer-models.ts` lines 14–20 (`PagedResponse<T>`).
18. `frontend/src/app/app.routes.ts` lines 55–59 (quick-replies route) and 70 (insertion comment). `frontend/src/app/core/layout/navigation.ts` lines 13–24 (`NAV_ITEMS`; Quick Replies on line 19). `frontend/src/app/core/auth/auth-guards.ts` lines 14–19 (`roleGuard`). `frontend/src/app/features/sla/sla-admin-page.html` lines 6–19 (`mat-tab-group animationDuration="0ms"`).
19. `frontend/src/app/features/tickets/tickets-api.spec.ts` lines 1–50 and `customers/customer-records-api.spec.ts` lines 1–39 — Vitest + TestBed style (`describe`/`it`/`expect`). The new spec is a plain function test and needs no TestBed.
20. `frontend/angular.json` budgets (lines ~37–48: `anyComponentStyle` warning 4 kB / error 8 kB). Keep each new component stylesheet small. Routes are lazy, so `marked` is not in the initial bundle.
21. Docs: `docs/api-conventions.md` line 3 (route exceptions) and the auth paragraph. `docs/identity-and-access.md` lines 7–32 (matrix) and 36–68 (routes). `docs/persistence-and-transactions.md` line 6. `docs/ticket-management.md` §9 (line 119) and §12 (line 150). `README.md` line 3.

---

## Product rules (from story)

### Role matrix (server-enforced)

| Operation | SuperAdmin | Agent | Customer / anonymous |
|---|---|---|---|
| Create article (always `Draft`, author = caller) | ✔ | ✔ | 403 / 401 |
| Read / search `Published` (Internal + Public) | ✔ | ✔ | 403 / 401 |
| Read own `Draft` / `ReadyForPublication` | ✔ (all) | ✔ own only (others 404) | 403 / 401 |
| Read `Archived` | ✔ | 404 | 403 / 401 |
| Edit `Draft` | ✔ any | ✔ own | 403 / 401 |
| Edit `ReadyForPublication` / `Published` | ✔ (status unchanged) | 409 (own submitted) / 403 (published) | 403 / 401 |
| Submit `Draft` → `ReadyForPublication` | ✔ any (I2) | ✔ own | 403 / 401 |
| Return → `Draft` / Publish / Archive / Restore | ✔ | **403 (policy)** | 403 / 401 |
| List categories (active) | ✔ (+ `includeInactive`) | ✔ active only | 403 / 401 |
| Create / rename / deactivate categories | ✔ | 403 (policy) | 403 / 401 |
| List / link / unlink ticket articles | ✔ any ticket | ✔ only tickets **currently assigned** to them (others 404, including tickets where they are only a collaborator) | 403 / 401 |

### Current vs new behaviour

| Area | Current | New (KB-001) |
|---|---|---|
| Navigation | No Knowledge Base | "Knowledge Base" for SuperAdmin and Agent |
| Ticket Details | Summary, SLA, Workflow, Assignment, Edit, Activity, Discussion, Collaborators, Quick replies | Same cards, **unchanged**, plus a final "Knowledge Base" card |
| `GET /api/v1/tickets/{id}` / `TicketResponse` | — | **Unchanged.** Links come only from the new nested endpoint |
| Ticket activity / SLA / `UpdatedAtUtc` | — | **Never changed** by linking or unlinking |
| Collaboration page `/collaboration/:ticketId` | Discussion only | **Unchanged.** No KB card |

---

## Backend Tasks

### 1 — Domain (`backend/src/Crm.Domain/KnowledgeBase/`)

**Create file: `backend/src/Crm.Domain/KnowledgeBase/KnowledgeArticleType.cs`**

```csharp
namespace Crm.Domain.KnowledgeBase;

/// <summary>KB-001 article kinds, one shared editor/renderer. Stored as the member name. FAQ: Title = question, Body = answer.</summary>
public enum KnowledgeArticleType
{
    Faq = 0,
    HelpArticle = 1,
    Solution = 2,
    Guide = 3,
}
```

**Create file: `backend/src/Crm.Domain/KnowledgeBase/KnowledgeArticleStatus.cs`**

```csharp
namespace Crm.Domain.KnowledgeBase;

/// <summary>Lifecycle (K1). Stored as the member name. Only Published articles appear in staff search and can be linked to tickets.</summary>
public enum KnowledgeArticleStatus
{
    Draft = 0,
    ReadyForPublication = 1,
    Published = 2,
    Archived = 3,
}
```

**Create file: `backend/src/Crm.Domain/KnowledgeBase/KnowledgeArticleVisibility.cs`**

```csharp
namespace Crm.Domain.KnowledgeBase;

/// <summary>
/// K2. Independent of status. Public = eligible for the future Customer Portal (Published + Public only).
/// KB-001 exposes no customer/anonymous endpoint, so today both values are staff-only.
/// </summary>
public enum KnowledgeArticleVisibility
{
    Internal = 0,
    Public = 1,
}
```

**Create file: `backend/src/Crm.Domain/KnowledgeBase/KnowledgeArticleWorkflow.cs`**

```csharp
namespace Crm.Domain.KnowledgeBase;

/// <summary>KB-001 transition matrix — the single source of truth. Every pair not listed is illegal. Who may trigger each one is an application rule.</summary>
public static class KnowledgeArticleWorkflow
{
    public static bool IsAllowed(KnowledgeArticleStatus from, KnowledgeArticleStatus to) => (from, to) switch
    {
        (KnowledgeArticleStatus.Draft, KnowledgeArticleStatus.ReadyForPublication) => true,     // submit
        (KnowledgeArticleStatus.ReadyForPublication, KnowledgeArticleStatus.Draft) => true,     // return for revision
        (KnowledgeArticleStatus.ReadyForPublication, KnowledgeArticleStatus.Published) => true, // publish (SuperAdmin)
        (KnowledgeArticleStatus.Draft, KnowledgeArticleStatus.Archived) => true,                // archive (SuperAdmin)
        (KnowledgeArticleStatus.ReadyForPublication, KnowledgeArticleStatus.Archived) => true,
        (KnowledgeArticleStatus.Published, KnowledgeArticleStatus.Archived) => true,
        (KnowledgeArticleStatus.Archived, KnowledgeArticleStatus.Draft) => true,                // restore (SuperAdmin)
        _ => false,
    };
}
```

**Create file: `backend/src/Crm.Domain/KnowledgeBase/KnowledgeCategory.cs`**

```csharp
namespace Crm.Domain.KnowledgeBase;

/// <summary>Flat SuperAdmin-maintained grouping (KB-001 §5). Never deleted: deactivate instead; existing articles keep it.</summary>
public sealed class KnowledgeCategory
{
    private KnowledgeCategory() { } // EF Core

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public static KnowledgeCategory Create(Guid id, string name, Guid superAdminUserId, DateTimeOffset now) =>
        new()
        {
            Id = id, Name = name, IsActive = true, CreatedByUserId = superAdminUserId, UpdatedByUserId = superAdminUserId,
            CreatedAtUtc = now, UpdatedAtUtc = now,
        };

    public bool Update(string name, bool isActive, Guid superAdminUserId, DateTimeOffset now)
    {
        if (string.Equals(Name, name, StringComparison.Ordinal) && IsActive == isActive)
        {
            return false;
        }

        Name = name;
        IsActive = isActive;
        UpdatedByUserId = superAdminUserId;
        UpdatedAtUtc = now;
        return true;
    }
}
```

**Create file: `backend/src/Crm.Domain/KnowledgeBase/KnowledgeArticle.cs`**

```csharp
using Crm.Domain.Common;

namespace Crm.Domain.KnowledgeBase;

/// <summary>
/// One Knowledge Base article (KB-001). AuthorUserId is fixed at creation (server-side caller). Status changes only through the
/// workflow methods below (KnowledgeArticleWorkflow); UpdateContent never touches Status, so no edit payload can publish (K1).
/// Visibility is stored independently of Status (K2). Body is Markdown source; HTML is presentation only (K3). Never hard-deleted.
/// </summary>
public sealed class KnowledgeArticle
{
    private KnowledgeArticle() { } // EF Core

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Summary { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public KnowledgeArticleType Type { get; private set; }
    public Guid CategoryId { get; private set; }
    public KnowledgeArticleVisibility Visibility { get; private set; }
    public KnowledgeArticleStatus Status { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public Guid UpdatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public DateTimeOffset? SubmittedAtUtc { get; private set; }
    public string? ReturnReason { get; private set; }
    public DateTimeOffset? PublishedAtUtc { get; private set; }
    public Guid? PublishedByUserId { get; private set; }
    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public static KnowledgeArticle CreateDraft(
        Guid id, Guid authorUserId, string title, string? summary, string body, KnowledgeArticleType type, Guid categoryId,
        KnowledgeArticleVisibility visibility, DateTimeOffset now) =>
        new()
        {
            Id = id, Title = title, Summary = summary, Body = body, Type = type, CategoryId = categoryId, Visibility = visibility,
            Status = KnowledgeArticleStatus.Draft, AuthorUserId = authorUserId, UpdatedByUserId = authorUserId,
            CreatedAtUtc = now, UpdatedAtUtc = now,
        };

    /// <summary>Same article id on every edit. Returns false when nothing changed (no save, no timestamp change).</summary>
    public bool UpdateContent(
        string title, string? summary, string body, KnowledgeArticleType type, Guid categoryId, KnowledgeArticleVisibility visibility,
        Guid actorUserId, DateTimeOffset now)
    {
        if (Status == KnowledgeArticleStatus.Archived)
        {
            throw new DomainException("Restore the article before editing it.");
        }

        if (string.Equals(Title, title, StringComparison.Ordinal) && string.Equals(Summary, summary, StringComparison.Ordinal)
            && string.Equals(Body, body, StringComparison.Ordinal) && Type == type && CategoryId == categoryId && Visibility == visibility)
        {
            return false;
        }

        Title = title;
        Summary = summary;
        Body = body;
        Type = type;
        CategoryId = categoryId;
        Visibility = visibility;
        Touch(actorUserId, now);
        return true;
    }

    public void Submit(Guid actorUserId, DateTimeOffset now)
    {
        MoveTo(KnowledgeArticleStatus.ReadyForPublication, actorUserId, now);
        SubmittedAtUtc = now;
        ReturnReason = null;
    }

    public void ReturnToDraft(string? reason, Guid superAdminUserId, DateTimeOffset now)
    {
        MoveTo(KnowledgeArticleStatus.Draft, superAdminUserId, now);
        ReturnReason = reason;
    }

    public void Publish(Guid superAdminUserId, DateTimeOffset now)
    {
        MoveTo(KnowledgeArticleStatus.Published, superAdminUserId, now);
        PublishedAtUtc = now;
        PublishedByUserId = superAdminUserId;
    }

    public void Archive(Guid superAdminUserId, DateTimeOffset now)
    {
        MoveTo(KnowledgeArticleStatus.Archived, superAdminUserId, now);
        ArchivedAtUtc = now;
    }

    /// <summary>Archived → Draft. Publication metadata is cleared; republishing records the new publisher/time. Ticket links keep the same article id.</summary>
    public void Restore(Guid superAdminUserId, DateTimeOffset now)
    {
        MoveTo(KnowledgeArticleStatus.Draft, superAdminUserId, now);
        ArchivedAtUtc = null;
        PublishedAtUtc = null;
        PublishedByUserId = null;
        SubmittedAtUtc = null;
        ReturnReason = null;
    }

    // Backstop only: handlers check the matrix first and return 409 with a friendly message.
    private void MoveTo(KnowledgeArticleStatus to, Guid actorUserId, DateTimeOffset now)
    {
        if (!KnowledgeArticleWorkflow.IsAllowed(Status, to))
        {
            throw new DomainException($"A {Status} article cannot move to {to}.");
        }

        Status = to;
        Touch(actorUserId, now);
    }

    private void Touch(Guid actorUserId, DateTimeOffset now)
    {
        UpdatedByUserId = actorUserId;
        UpdatedAtUtc = now;
    }
}
```

**Create file: `backend/src/Crm.Domain/KnowledgeBase/TicketKnowledgeArticleLink.cs`**

```csharp
namespace Crm.Domain.KnowledgeBase;

/// <summary>
/// Ticket ↔ article association (K4). Insert/delete only; never changes the ticket (status, assignment, SLA, UpdatedAtUtc, activity).
/// Kept when the article is archived (traceability). Id is a store-generated identity.
/// </summary>
public sealed class TicketKnowledgeArticleLink
{
    private TicketKnowledgeArticleLink() { } // EF Core

    public long Id { get; private set; }
    public Guid TicketId { get; private set; }
    public Guid ArticleId { get; private set; }
    public Guid LinkedByUserId { get; private set; }
    public DateTimeOffset LinkedAtUtc { get; private set; }

    public static TicketKnowledgeArticleLink Create(Guid ticketId, Guid articleId, Guid linkedByUserId, DateTimeOffset now) =>
        new() { TicketId = ticketId, ArticleId = articleId, LinkedByUserId = linkedByUserId, LinkedAtUtc = now };
}
```

### 2 — Application: rules, read models, guards, repository contracts

**Create file: `backend/src/Crm.Application/Common/Validation/KnowledgeBaseRules.cs`**

```csharp
namespace Crm.Application.Common.Validation;

public static class KnowledgeBaseRules
{
    public const int TitleMaxLength = 200;
    public const int SummaryMaxLength = 500;
    public const int BodyMaxLength = 20_000;
    public const int CategoryNameMaxLength = 100;
    public const int ReturnReasonMaxLength = 500;
    public const int SearchMaxLength = 100;
    /// <summary>Keeps the unpaged ticket link list small (KB-001 §10).</summary>
    public const int MaxLinksPerTicket = 25;
}
```

**Create file: `backend/src/Crm.Application/KnowledgeBase/KnowledgeBaseDetails.cs`**

```csharp
using Crm.Domain.KnowledgeBase;

namespace Crm.Application.KnowledgeBase;

public sealed record KnowledgeCategoryDetails(Guid Id, string Name, bool IsActive, DateTimeOffset UpdatedAtUtc);

/// <summary>List row — never contains the body.</summary>
public sealed record KnowledgeArticleSummary(
    Guid Id, string Title, string? Summary, KnowledgeArticleType Type, Guid CategoryId, string CategoryName,
    KnowledgeArticleVisibility Visibility, KnowledgeArticleStatus Status, Guid AuthorUserId, string AuthorName,
    DateTimeOffset UpdatedAtUtc, DateTimeOffset? PublishedAtUtc);

/// <summary>CanEdit / CanSubmit are computed for the requesting actor. Display only — commands re-check.</summary>
public sealed record KnowledgeArticleDetails(
    Guid Id, string Title, string? Summary, string Body, KnowledgeArticleType Type, Guid CategoryId, string CategoryName,
    bool CategoryIsActive, KnowledgeArticleVisibility Visibility, KnowledgeArticleStatus Status, Guid AuthorUserId, string AuthorName,
    string UpdatedByName, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, DateTimeOffset? SubmittedAtUtc, string? ReturnReason,
    DateTimeOffset? PublishedAtUtc, string? PublishedByName, DateTimeOffset? ArchivedAtUtc, bool CanEdit, bool CanSubmit);

/// <summary>Search already trimmed (title, summary or body). Mine = only the caller's own articles.</summary>
public sealed record KnowledgeArticleListFilter(
    string? Search, KnowledgeArticleType? Type, Guid? CategoryId, KnowledgeArticleStatus? Status, bool Mine);

/// <summary>Metadata only (I6): never the body, so archived content is not exposed through a link.</summary>
public sealed record TicketKnowledgeLinkDetails(
    long Id, Guid TicketId, Guid ArticleId, string ArticleTitle, KnowledgeArticleType ArticleType, string CategoryName,
    KnowledgeArticleStatus ArticleStatus, string LinkedByName, DateTimeOffset LinkedAtUtc);
```

**Create file: `backend/src/Crm.Application/KnowledgeBase/KnowledgeBaseGuards.cs`**

```csharp
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Domain.KnowledgeBase;
using FluentValidation;
using FluentValidation.Results;

namespace Crm.Application.KnowledgeBase;

internal static class KnowledgeBaseGuards
{
    /// <summary>Called on an article the actor can already see (repository visibility ⇒ otherwise 404).</summary>
    public static void EnsureCanEdit(KnowledgeArticle article, StaffActor actor)
    {
        if (article.Status == KnowledgeArticleStatus.Archived)
        {
            throw new ConflictException("The article is archived. Restore it first.");
        }

        if (actor.IsSuperAdmin)
        {
            return;
        }

        if (article.Status == KnowledgeArticleStatus.ReadyForPublication)
        {
            throw new ConflictException("This article is waiting for publication. Ask a SuperAdmin to return it for revision before editing.");
        }

        if (article.Status != KnowledgeArticleStatus.Draft || article.AuthorUserId != actor.UserId)
        {
            throw new ForbiddenException("Published articles are maintained by SuperAdmin.");
        }
    }

    public static void EnsureSuperAdmin(StaffActor actor)
    {
        if (!actor.IsSuperAdmin)
        {
            throw new ForbiddenException("Only SuperAdmin can review, publish, archive or restore articles.");
        }
    }

    // 400 ValidationProblemDetails keyed by the camelCased field (as AgentTaskGuards.Invalid).
    public static ValidationException Invalid(string property, string message) => new([new ValidationFailure(property, message)]);
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/IKnowledgeBaseRepository.cs`**

```csharp
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Articles and categories. Article visibility is applied first, in SQL: SuperAdmin ⇒ every article;
/// Agent ⇒ Published + own Draft/ReadyForPublication. Not visible ⇒ null (handlers return 404).
/// </summary>
public interface IKnowledgeBaseRepository
{
    void AddArticle(KnowledgeArticle article);

    /// <summary>Tracked, for updates. Null when missing or not visible to the actor.</summary>
    Task<KnowledgeArticle?> GetVisibleArticleAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default);

    Task<KnowledgeArticleDetails?> FindArticleDetailsAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default);

    /// <summary>Ordered UpdatedAtUtc DESC, Id DESC. Rows never contain the body.</summary>
    Task<PagedResult<KnowledgeArticleSummary>> ListArticlesAsync(
        KnowledgeArticleListFilter filter, StaffActor actor, int page, int pageSize, CancellationToken cancellationToken = default);

    void AddCategory(KnowledgeCategory category);

    /// <summary>Tracked.</summary>
    Task<KnowledgeCategory?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Uses the column collation (case-insensitive by default).</summary>
    Task<bool> CategoryNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default);

    Task<KnowledgeCategoryDetails> GetCategoryDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Ordered Name, Id.</summary>
    Task<IReadOnlyList<KnowledgeCategoryDetails>> ListCategoriesAsync(bool includeInactive, CancellationToken cancellationToken = default);
}
```

**Create file: `backend/src/Crm.Application/Abstractions/Persistence/ITicketKnowledgeLinkRepository.cs`**

```csharp
using Crm.Application.KnowledgeBase;
using Crm.Domain.KnowledgeBase;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>
/// Ticket ↔ article links (K4). Ticket access uses the existing ticket boundary (TicketRepository.Scope):
/// agentScope null = SuperAdmin (any ticket), otherwise only tickets currently assigned to that Agent. Collaboration membership is ignored.
/// The ticket row is never tracked, so links can never change it.
/// </summary>
public interface ITicketKnowledgeLinkRepository
{
    Task<bool> TicketIsVisibleAsync(Guid ticketId, Guid? agentScope, CancellationToken cancellationToken = default);

    Task<bool> LinkExistsAsync(Guid ticketId, Guid articleId, CancellationToken cancellationToken = default);

    Task<int> CountForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);

    void Add(TicketKnowledgeArticleLink link);

    /// <summary>Tracked. Null when not linked.</summary>
    Task<TicketKnowledgeArticleLink?> GetAsync(Guid ticketId, Guid articleId, CancellationToken cancellationToken = default);

    void Remove(TicketKnowledgeArticleLink link);

    Task<TicketKnowledgeLinkDetails> GetDetailsAsync(long id, CancellationToken cancellationToken = default);

    /// <summary>Ordered LinkedAtUtc DESC, Id DESC. Includes links to archived/unpublished articles (status shown, body never).</summary>
    Task<IReadOnlyList<TicketKnowledgeLinkDetails>> ListForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
```

### 3 — Application: use cases (`backend/src/Crm.Application/KnowledgeBase/…`)

Each file holds the record, the handler and (when needed) the validator, as in `CreateQuickReplyCommand.cs` lines 11–46. Handlers inject `TimeProvider` for `now`. Every command saves **once** through `IUnitOfWork.SaveChangesAsync` and reads the result back through the repository. None implements `ITransactionalCommand`.

**Create file: `Crm.Application/KnowledgeBase/ListCategories/ListKnowledgeCategoriesQuery.cs`**

- `public sealed record ListKnowledgeCategoriesQuery(StaffActor Actor, bool IncludeInactive) : IQuery<IReadOnlyList<KnowledgeCategoryDetails>>;`
- The handler passes `IncludeInactive && Actor.IsSuperAdmin`, so Agents always get active categories only.

**Create file: `Crm.Application/KnowledgeBase/CreateCategory/CreateKnowledgeCategoryCommand.cs`**

- `public sealed record CreateKnowledgeCategoryCommand(StaffActor Actor, string Name) : ICommand<KnowledgeCategoryDetails>;`
- Handler: `KnowledgeBaseGuards.EnsureSuperAdmin(Actor)`. `name = Name.Trim()`. If `CategoryNameExistsAsync(name, null)` ⇒ `ConflictException("A category with this name already exists.")`. Then `KnowledgeCategory.Create(Guid.CreateVersion7(), name, Actor.UserId, now)`, `AddCategory`, save, `GetCategoryDetailsAsync`.
- Validator: `RuleFor(c => c.Name).NotEmpty().MaximumLength(KnowledgeBaseRules.CategoryNameMaxLength);`

**Create file: `Crm.Application/KnowledgeBase/UpdateCategory/UpdateKnowledgeCategoryCommand.cs`**

- `public sealed record UpdateKnowledgeCategoryCommand(StaffActor Actor, Guid Id, string Name, bool IsActive) : ICommand<KnowledgeCategoryDetails>;`
- Handler: `EnsureSuperAdmin`. `GetCategoryAsync(Id)` ⇒ null ⇒ `NotFoundException($"Knowledge category '{Id}' was not found.")`. Normalize first: `var name = command.Name.Trim();` (same as create). Use this trimmed `name` in **both** the duplicate check `CategoryNameExistsAsync(name, command.Id)` ⇒ 409 (same text as create) **and** the update `category.Update(name, command.IsActive, command.Actor.UserId, now)`. Never use the raw `command.Name` after this line. Save only when `Update` returns true, so re-submitting `" Accounts "` for "Accounts" is a no-op. Return the details.
- Validator: `Id` NotEmpty, `Name` as above.

**Create file: `Crm.Application/KnowledgeBase/ListArticles/ListKnowledgeArticlesQuery.cs`**

```csharp
public sealed record ListKnowledgeArticlesQuery(
    StaffActor Actor,
    string? Search,
    KnowledgeArticleType? Type,
    Guid? CategoryId,
    KnowledgeArticleStatus? Status,
    bool Mine = false,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<KnowledgeArticleSummary>>;
```

- The handler trims the search (null when blank), builds the `KnowledgeArticleListFilter` and calls `ListArticlesAsync(filter, Actor, Page, Math.Min(PageSize, Paging.MaxPageSize))`. This is the pattern of `ListQuickRepliesQuery.cs` lines 24–29.
- Validator (as `ListQuickRepliesQuery.cs` lines 32–41): `Page` 1…`Paging.MaxPage`, `PageSize ≥ 1`, `Search` max `KnowledgeBaseRules.SearchMaxLength`, `Type` / `Status` `IsInEnum()`.
- An Agent filtering `status=Archived` gets an empty page, because the visibility predicate already excludes archived articles. No error is needed.

**Create file: `Crm.Application/KnowledgeBase/GetArticle/GetKnowledgeArticleQuery.cs`**

- `public sealed record GetKnowledgeArticleQuery(StaffActor Actor, Guid Id) : IQuery<KnowledgeArticleDetails>;`
- Returns `FindArticleDetailsAsync(Id, Actor)`, or throws `NotFoundException($"Knowledge article '{Id}' was not found.")`.

**Create file: `Crm.Application/KnowledgeBase/CreateArticle/CreateKnowledgeArticleCommand.cs`**

```csharp
/// <summary>Always creates a Draft owned by the caller (K1). The command has no status or author. Single save.</summary>
public sealed record CreateKnowledgeArticleCommand(
    StaffActor Actor, string Title, string? Summary, string Body, KnowledgeArticleType? Type, Guid? CategoryId,
    KnowledgeArticleVisibility? Visibility) : ICommand<KnowledgeArticleDetails>;
```

- Handler: load `GetCategoryAsync(CategoryId!.Value)`. If it is null or `!IsActive` ⇒ `throw KnowledgeBaseGuards.Invalid("categoryId", "Choose an active category.")`. Then `KnowledgeArticle.CreateDraft(Guid.CreateVersion7(), Actor.UserId, Title.Trim(), Normalize(Summary), Body.Trim(), Type!.Value, CategoryId.Value, Visibility!.Value, now)`, `AddArticle`, save, `FindArticleDetailsAsync(id, Actor)!`.
- `private static string? Normalize(string? summary) => string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();`. Put it in a shared validator helper (below) so update can reuse it.
- **Create file: `Crm.Application/KnowledgeBase/KnowledgeArticleInputRules.cs`** containing an `internal static class KnowledgeArticleInput` with `Normalize`, and an extension used by both validators:

```csharp
internal static class KnowledgeArticleInput
{
    public static string? NormalizeSummary(string? summary) => string.IsNullOrWhiteSpace(summary) ? null : summary.Trim();

    public static void AddContentRules<T>(
        AbstractValidator<T> v, Expression<Func<T, string>> title, Expression<Func<T, string?>> summary, Expression<Func<T, string>> body,
        Expression<Func<T, KnowledgeArticleType?>> type, Expression<Func<T, Guid?>> categoryId, Expression<Func<T, KnowledgeArticleVisibility?>> visibility)
    {
        v.RuleFor(title).NotEmpty().MaximumLength(KnowledgeBaseRules.TitleMaxLength);
        v.RuleFor(summary).MaximumLength(KnowledgeBaseRules.SummaryMaxLength);
        v.RuleFor(body).NotEmpty().MaximumLength(KnowledgeBaseRules.BodyMaxLength);
        v.RuleFor(type).NotNull().IsInEnum();
        v.RuleFor(categoryId).NotNull().NotEqual(Guid.Empty);
        v.RuleFor(visibility).NotNull().IsInEnum();
    }
}
```

  If the generic-expression helper causes overload trouble with `RuleFor` on nullable members, write the six rules out in each validator instead. Duplicating six lines is acceptable. The messages are FluentValidation defaults, as in the existing validators.

**Create file: `Crm.Application/KnowledgeBase/UpdateArticle/UpdateKnowledgeArticleCommand.cs`**

```csharp
/// <summary>Content only — status, author and publication fields never change here (K1). Not visible ⇒ 404; see KnowledgeBaseGuards.EnsureCanEdit. Single save.</summary>
public sealed record UpdateKnowledgeArticleCommand(
    StaffActor Actor, Guid Id, string Title, string? Summary, string Body, KnowledgeArticleType? Type, Guid? CategoryId,
    KnowledgeArticleVisibility? Visibility) : ICommand<KnowledgeArticleDetails>;
```

- Handler order: `GetVisibleArticleAsync(Id, Actor)`, null ⇒ 404 → `EnsureCanEdit(article, Actor)` → category check. **An unchanged `CategoryId` is accepted even when that category is now inactive.** A changed one must exist and be active, otherwise 400 `categoryId` "Choose an active category." → `article.UpdateContent(...)`, saving only if it returns true → read back.
- Validator: `Id` NotEmpty plus the content rules.

**Create file: `Crm.Application/KnowledgeBase/ChangeArticleStatus/ChangeKnowledgeArticleStatusCommand.cs`**

```csharp
public enum KnowledgeArticleAction
{
    Submit = 0,
    Return = 1,
    Publish = 2,
    Archive = 3,
    Restore = 4,
}

/// <summary>
/// One handler for the K1 workflow. Submit: author (Agent) or SuperAdmin (any Draft, I2). Return/Publish/Archive/Restore: SuperAdmin only
/// (controller policy + handler backstop ⇒ 403). Not visible ⇒ 404. Illegal from the current status ⇒ 409. Single save.
/// </summary>
public sealed record ChangeKnowledgeArticleStatusCommand(StaffActor Actor, Guid Id, KnowledgeArticleAction Action, string? Reason = null)
    : ICommand<KnowledgeArticleDetails>;

public sealed class ChangeKnowledgeArticleStatusCommandHandler(
    IKnowledgeBaseRepository knowledgeBase,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider) : ICommandHandler<ChangeKnowledgeArticleStatusCommand, KnowledgeArticleDetails>
{
    public async ValueTask<KnowledgeArticleDetails> Handle(ChangeKnowledgeArticleStatusCommand command, CancellationToken cancellationToken)
    {
        var article = await knowledgeBase.GetVisibleArticleAsync(command.Id, command.Actor, cancellationToken)
            ?? throw new NotFoundException($"Knowledge article '{command.Id}' was not found.");

        if (command.Action != KnowledgeArticleAction.Submit)
        {
            KnowledgeBaseGuards.EnsureSuperAdmin(command.Actor);
        }

        var target = command.Action switch
        {
            KnowledgeArticleAction.Submit => KnowledgeArticleStatus.ReadyForPublication,
            KnowledgeArticleAction.Return => KnowledgeArticleStatus.Draft,
            KnowledgeArticleAction.Publish => KnowledgeArticleStatus.Published,
            KnowledgeArticleAction.Archive => KnowledgeArticleStatus.Archived,
            KnowledgeArticleAction.Restore => KnowledgeArticleStatus.Draft,
            _ => throw new ArgumentOutOfRangeException(nameof(command)),
        };

        // Return and Restore both target Draft; distinguish them by the required current status.
        var expectedFrom = command.Action switch
        {
            KnowledgeArticleAction.Return => KnowledgeArticleStatus.ReadyForPublication,
            KnowledgeArticleAction.Restore => KnowledgeArticleStatus.Archived,
            _ => article.Status,
        };
        if (article.Status != expectedFrom || !KnowledgeArticleWorkflow.IsAllowed(article.Status, target))
        {
            throw new ConflictException(ConflictMessage(command.Action));
        }

        var now = timeProvider.GetUtcNow();
        var actorId = command.Actor.UserId;
        switch (command.Action)
        {
            case KnowledgeArticleAction.Submit: article.Submit(actorId, now); break;
            case KnowledgeArticleAction.Return: article.ReturnToDraft(Normalize(command.Reason), actorId, now); break;
            case KnowledgeArticleAction.Publish: article.Publish(actorId, now); break;
            case KnowledgeArticleAction.Archive: article.Archive(actorId, now); break;
            case KnowledgeArticleAction.Restore: article.Restore(actorId, now); break;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return (await knowledgeBase.FindArticleDetailsAsync(article.Id, command.Actor, cancellationToken))!;
    }

    private static string ConflictMessage(KnowledgeArticleAction action) => action switch
    {
        KnowledgeArticleAction.Submit => "Only a Draft article can be submitted for publication.",
        KnowledgeArticleAction.Return => "Only an article that is Ready for Publication can be returned for revision.",
        KnowledgeArticleAction.Publish => "Only an article that is Ready for Publication can be published.",
        KnowledgeArticleAction.Archive => "The article is already archived.",
        _ => "Only an archived article can be restored.",
    };

    private static string? Normalize(string? reason) => string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
}

public sealed class ChangeKnowledgeArticleStatusValidator : AbstractValidator<ChangeKnowledgeArticleStatusCommand>
{
    public ChangeKnowledgeArticleStatusValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Action).IsInEnum();
        RuleFor(c => c.Reason).MaximumLength(KnowledgeBaseRules.ReturnReasonMaxLength);
    }
}
```

Note the `FindArticleDetailsAsync` read-back after **Archive** by a SuperAdmin: SuperAdmin sees archived articles, so it is never null. Submit is only ever done by a visible author or a SuperAdmin, both of whom can still see the article afterwards.

**Create file: `Crm.Application/KnowledgeBase/TicketLinks/ListTicketKnowledgeArticlesQuery.cs`**

- `public sealed record ListTicketKnowledgeArticlesQuery(StaffActor Actor, Guid TicketId) : IQuery<IReadOnlyList<TicketKnowledgeLinkDetails>>;`
- `TicketIsVisibleAsync(TicketId, Actor.AgentScope)` false ⇒ `NotFoundException($"Ticket '{TicketId}' was not found.")`. Otherwise `ListForTicketAsync(TicketId)`.

**Create file: `Crm.Application/KnowledgeBase/TicketLinks/LinkKnowledgeArticleCommand.cs`**

```csharp
/// <summary>K4. Ticket scope = TicketRepository.Scope (collaborators ⇒ 404). Published articles only. Never changes the ticket. Single save (I5).</summary>
public sealed record LinkKnowledgeArticleCommand(StaffActor Actor, Guid TicketId, Guid ArticleId) : ICommand<TicketKnowledgeLinkDetails>;
```

Handler order (each failure stops):
1. `TicketIsVisibleAsync(TicketId, Actor.AgentScope)` false ⇒ 404 "Ticket '{id}' was not found."
2. `FindArticleDetailsAsync(ArticleId, Actor)` null ⇒ 404 "Knowledge article '{id}' was not found."
3. `Status != Published` ⇒ `ConflictException("Only published articles can be linked to a ticket.")`
4. `LinkExistsAsync` ⇒ `ConflictException("This article is already linked to the ticket.")`
5. `CountForTicketAsync >= KnowledgeBaseRules.MaxLinksPerTicket` ⇒ `ConflictException("A ticket can have at most 25 linked articles.")`
6. `TicketKnowledgeArticleLink.Create(TicketId, ArticleId, Actor.UserId, now)`, then `Add`, save, `GetDetailsAsync(link.Id)`.

Validator: `TicketId`, `ArticleId` NotEmpty.

**Create file: `Crm.Application/KnowledgeBase/TicketLinks/UnlinkKnowledgeArticleCommand.cs`**

- `public sealed record UnlinkKnowledgeArticleCommand(StaffActor Actor, Guid TicketId, Guid ArticleId) : ICommand;`. Use the unit-returning `ICommand` / `ICommandHandler<T>`, the same as `SlaAlerts/MarkSlaAlertRead/MarkSlaAlertReadCommand.cs` line 13.
- Ticket not visible ⇒ 404. `GetAsync(TicketId, ArticleId)` null ⇒ `NotFoundException("This article is not linked to the ticket.")`. Then `Remove`, save. Unlinking works for any article status, including links to archived articles.

### 4 — Infrastructure: EF configuration, DbSets, repositories, unique-conflict messages

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/KnowledgeCategoryConfiguration.cs`**

```csharp
internal sealed class KnowledgeCategoryConfiguration : IEntityTypeConfiguration<KnowledgeCategory>
{
    public const string NameIndex = "UX_KnowledgeCategories_Name";

    public void Configure(EntityTypeBuilder<KnowledgeCategory> builder)
    {
        builder.ToTable("KnowledgeCategories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Referenced by EfUnitOfWork.KnownUniqueIndexes. Case-insensitive under the default database collation.
        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName(NameIndex);
    }
}
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/KnowledgeArticleConfiguration.cs`**

```csharp
internal sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.ToTable("KnowledgeArticles");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Title).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Summary).HasMaxLength(500);
        builder.Property(a => a.Body).IsRequired().HasMaxLength(20_000); // > 4000 ⇒ nvarchar(max)
        builder.Property(a => a.ReturnReason).HasMaxLength(500);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Visibility).HasConversion<string>().HasMaxLength(30);
        builder.Property<byte[]>("Version").IsRowVersion(); // overlapping saves ⇒ 409 (EfUnitOfWork), as Tickets.Version

        builder.HasOne<KnowledgeCategory>().WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.Status, a.UpdatedAtUtc, a.Id }).IsDescending(false, true, true);                  // published browse / review queue
        builder.HasIndex(a => new { a.AuthorUserId, a.Status, a.UpdatedAtUtc, a.Id }).IsDescending(false, false, true, true); // my articles
        builder.HasIndex(a => a.CategoryId);
    }
}
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Configurations/TicketKnowledgeArticleLinkConfiguration.cs`**

```csharp
internal sealed class TicketKnowledgeArticleLinkConfiguration : IEntityTypeConfiguration<TicketKnowledgeArticleLink>
{
    public const string TicketArticleIndex = "UX_TicketKnowledgeArticleLinks_TicketArticle";

    public void Configure(EntityTypeBuilder<TicketKnowledgeArticleLink> builder)
    {
        builder.ToTable("TicketKnowledgeArticleLinks");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedOnAdd(); // bigint IDENTITY

        builder.HasOne<Ticket>().WithMany().HasForeignKey(l => l.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeArticle>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(l => l.LinkedByUserId).OnDelete(DeleteBehavior.Restrict);

        // One link per (ticket, article). Referenced by EfUnitOfWork.KnownUniqueIndexes.
        builder.HasIndex(l => new { l.TicketId, l.ArticleId }).IsUnique().HasDatabaseName(TicketArticleIndex);
        builder.HasIndex(l => l.ArticleId);
    }
}
```

**File: `backend/src/Crm.Infrastructure/Persistence/CrmDbContext.cs`** — add `using Crm.Domain.KnowledgeBase;` (alphabetical, after `Crm.Domain.Identity` on line 5) and three DbSets after line 37:

```csharp
    public DbSet<KnowledgeCategory> KnowledgeCategories => Set<KnowledgeCategory>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<TicketKnowledgeArticleLink> TicketKnowledgeArticleLinks => Set<TicketKnowledgeArticleLink>();
```

**File: `backend/src/Crm.Infrastructure/Persistence/EfUnitOfWork.cs`** lines 12–15 — extend the dictionary. Keep the existing entry unchanged:

```csharp
        ["UX_TicketCollaborators_ActiveMembership"] = "This Agent is already a collaborator.",
        ["UX_KnowledgeCategories_Name"] = "A category with this name already exists.",
        ["UX_TicketKnowledgeArticleLinks_TicketArticle"] = "This article is already linked to the ticket.",
```

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/KnowledgeBaseRepository.cs`** — follow `QuickReplyRepository.cs` lines 10–75:

- `Visible(IQueryable<KnowledgeArticle> articles, StaffActor actor)`:

```csharp
    // Applied first, in SQL. SuperAdmin: everything (incl. Archived). Agent: Published + own Draft/ReadyForPublication (K1/K2).
    private static IQueryable<KnowledgeArticle> Visible(IQueryable<KnowledgeArticle> articles, StaffActor actor)
    {
        if (actor.IsSuperAdmin)
        {
            return articles;
        }

        var agentId = actor.UserId;
        return articles.Where(a =>
            a.Status == KnowledgeArticleStatus.Published
            || (a.AuthorUserId == agentId
                && (a.Status == KnowledgeArticleStatus.Draft || a.Status == KnowledgeArticleStatus.ReadyForPublication)));
    }
```

- `GetVisibleArticleAsync`: `Visible(dbContext.KnowledgeArticles, actor).SingleOrDefaultAsync(a => a.Id == id, ct)` (tracked).
- `ListArticlesAsync`: `Visible(dbContext.KnowledgeArticles.AsNoTracking(), actor)`, then `Status` / `Type` / `CategoryId` filters, `Mine` ⇒ `a.AuthorUserId == actor.UserId`, and search:

```csharp
        if (!string.IsNullOrEmpty(filter.Search))
        {
            // Parameterized LIKE; EF Core escapes wildcards for string.Contains. Body is nvarchar(max).
            var search = filter.Search;
            query = query.Where(a => a.Title.Contains(search) || (a.Summary != null && a.Summary.Contains(search)) || a.Body.Contains(search));
        }
```

  Then `CountAsync`, followed by a summary projection with `join c in dbContext.KnowledgeCategories on a.CategoryId equals c.Id`, `join author in dbContext.Users on a.AuthorUserId equals author.Id`, `orderby a.UpdatedAtUtc descending, a.Id descending` **before** `select new KnowledgeArticleSummary(...)` (EF cannot order by record members, see `TicketRepository.cs` comment lines 130–132), and finally `Skip((page - 1) * pageSize).Take(pageSize)`. **Do not select `Body`** in the summary.
- `FindArticleDetailsAsync`: the same `Visible` + joins, plus left joins for `updater` (inner, required) and `publisher` (`from p in dbContext.Users.Where(u => u.Id == a.PublishedByUserId).DefaultIfEmpty()`). Compute in SQL:
  - `CanEdit = a.Status != Archived && (isSuperAdmin || (a.AuthorUserId == agentId && a.Status == Draft))`
  - `CanSubmit = a.Status == Draft && (isSuperAdmin || a.AuthorUserId == agentId)`
  - `CategoryIsActive = c.IsActive`
- Categories: `GetCategoryAsync` (tracked `FindAsync` or `SingleOrDefaultAsync`). `CategoryNameExistsAsync(name, exceptId)` = `AnyAsync(c => c.Name == name && (exceptId == null || c.Id != exceptId))`. `ListCategoriesAsync(includeInactive)` ordered `Name, Id`. `GetCategoryDetailsAsync`.

**Create file: `backend/src/Crm.Infrastructure/Persistence/Repositories/TicketKnowledgeLinkRepository.cs`**

```csharp
internal sealed class TicketKnowledgeLinkRepository(CrmDbContext dbContext) : ITicketKnowledgeLinkRepository
{
    // The existing ticket boundary (TICK-002): SuperAdmin any ticket, Agent only currently assigned tickets. Never tracked.
    public Task<bool> TicketIsVisibleAsync(Guid ticketId, Guid? agentScope, CancellationToken cancellationToken = default) =>
        TicketRepository.Scope(dbContext.Tickets.AsNoTracking(), agentScope).AnyAsync(t => t.Id == ticketId, cancellationToken);
    …
}
```

- `ListForTicketAsync` / `GetDetailsAsync`: `from l in dbContext.TicketKnowledgeArticleLinks.AsNoTracking() join a in dbContext.KnowledgeArticles on l.ArticleId equals a.Id join c in dbContext.KnowledgeCategories on a.CategoryId equals c.Id join u in dbContext.Users on l.LinkedByUserId equals u.Id orderby l.LinkedAtUtc descending, l.Id descending select new TicketKnowledgeLinkDetails(l.Id, l.TicketId, l.ArticleId, a.Title, a.Type, c.Name, a.Status, u.FirstName + " " + u.LastName, l.LinkedAtUtc)`. **Never select `a.Body`.**
- `Add`, `Remove`, `GetAsync` (tracked), `LinkExistsAsync`, `CountForTicketAsync`: straightforward queries.

**File: `backend/src/Crm.Infrastructure/DependencyInjection.cs`** — after line 51 (`ISlaAlertRepository`) add:

```csharp
        services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();
        services.AddScoped<ITicketKnowledgeLinkRepository, TicketKnowledgeLinkRepository>();
```

### 5 — API: contracts and controllers

**Create file: `backend/src/Crm.Api/Contracts/KnowledgeBase/KnowledgeBaseContracts.cs`** (several records in one file, like `QuickReplyContracts.cs`):

```csharp
// No status, author, publisher or role fields: they come from the workflow endpoints and the caller's token only (K1).
public sealed record SaveKnowledgeArticleRequest(
    string Title, string? Summary, string Body, KnowledgeArticleType? Type, Guid? CategoryId, KnowledgeArticleVisibility? Visibility);

public sealed record ReturnKnowledgeArticleRequest(string? Reason);

public sealed record CreateKnowledgeCategoryRequest(string Name);

public sealed record UpdateKnowledgeCategoryRequest(string Name, bool IsActive);

public sealed record LinkKnowledgeArticleRequest(Guid ArticleId);

public sealed record KnowledgeCategoryResponse(Guid Id, string Name, bool IsActive, DateTimeOffset UpdatedAtUtc)
{
    public static KnowledgeCategoryResponse From(KnowledgeCategoryDetails c) => new(c.Id, c.Name, c.IsActive, c.UpdatedAtUtc);
}

public sealed record KnowledgeArticleSummaryResponse(…same fields as KnowledgeArticleSummary…) { public static … From(KnowledgeArticleSummary a) … }

public sealed record KnowledgeArticleResponse(…same fields as KnowledgeArticleDetails…) { public static … From(KnowledgeArticleDetails a) … }

public sealed record TicketKnowledgeLinkResponse(…same fields as TicketKnowledgeLinkDetails…) { public static … From(TicketKnowledgeLinkDetails l) … }
```

Write the response records out field by field, in the same order as the read models.

**Create file: `backend/src/Crm.Api/Controllers/KnowledgeArticlesController.cs`**

```csharp
[ApiController]
[Route(ApiRoutes.V1 + "/knowledge-base/articles")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class KnowledgeArticlesController(ISender sender) : ControllerBase
{
    private const string Visibility = "Staff only — no customer or anonymous access (Customer Portal is a future story). Agent: Published (Internal + Public) and own Draft/Ready for Publication; SuperAdmin: all including Archived. Not visible: 404.";

    [EndpointDescription("Requires role: SuperAdmin or Agent. " + Visibility + " One-based page, pageSize (default 20, clamped to 100), search on title/summary/body, type, categoryId, status, mine (own articles). Ordered by last update, newest first. Rows omit the body.")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<KnowledgeArticleSummaryResponse>>> List(
        [FromQuery] string? search, [FromQuery] KnowledgeArticleType? type, [FromQuery] Guid? categoryId,
        [FromQuery] KnowledgeArticleStatus? status, [FromQuery] bool mine = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = Paging.DefaultPageSize, CancellationToken cancellationToken = default) { … }

    [HttpGet("{id:guid}")]            // Get ⇒ KnowledgeArticleResponse
    [HttpPost]                        // Create ⇒ 201 CreatedAtAction(nameof(Get)); "Always created as Draft owned by the caller."
    [HttpPut("{id:guid}")]            // Update; "Agent: own Draft only (submitted 409, published 403). SuperAdmin: Draft, Ready for Publication, Published. Archived 409. Status never changes here."
    [HttpPost("{id:guid}/submit")]    // Staff; "Draft → Ready for Publication. Agent: own Draft; SuperAdmin: any Draft."
    [HttpPost("{id:guid}/return")]    // [Authorize(Policy = AuthorizationPolicies.SuperAdmin)], body ReturnKnowledgeArticleRequest; "Ready for Publication → Draft with optional reason (max 500)."
    [HttpPost("{id:guid}/publish")]   // [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]; "Ready for Publication → Published; records publisher and time. Agents: 403."
    [HttpPost("{id:guid}/archive")]   // [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]; "Draft/Ready/Published → Archived. Removed from search; existing ticket links are kept."
    [HttpPost("{id:guid}/restore")]   // [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]; "Archived → Draft (publish again after review). Same article id."

    private StaffActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
```

Every action carries an `EndpointDescription` starting "Requires role: …". The workflow actions all return `Ok(KnowledgeArticleResponse.From(await sender.Send(new ChangeKnowledgeArticleStatusCommand(Actor, id, KnowledgeArticleAction.X, reason), ct)))`.

**Create file: `backend/src/Crm.Api/Controllers/KnowledgeCategoriesController.cs`** — `[Route(ApiRoutes.V1 + "/knowledge-base/categories")]`, class-level `Staff` policy.
- `GET` returns the unpaged `IReadOnlyList<KnowledgeCategoryResponse>`, with `[FromQuery] bool includeInactive = false` honoured for SuperAdmin only.
- `POST` returns 201 (`[Authorize(Policy = SuperAdmin)]`). There is no single-item GET, so use `Created(string.Empty, …)` or `StatusCode(201, …)`. Precedent: if `CreatedAtAction` to the list action is used elsewhere (`CollaborationsController.cs` line 60), do the same: `CreatedAtAction(nameof(List), null, response)`.
- `PUT {id:guid}` returns 200 (`[Authorize(Policy = SuperAdmin)]`).

**Create file: `backend/src/Crm.Api/Controllers/TicketKnowledgeArticlesController.cs`** — `[Route(ApiRoutes.V1 + "/tickets/{ticketId:guid}/knowledge-articles")]`, `Staff` policy. Shared description constant: `"Ticket access: SuperAdmin any ticket; Agent only tickets currently assigned to them (others 404 — collaborator membership does not grant access). Linking never changes the ticket (status, assignment, SLA, timestamps, activity). Nothing is sent to the customer."`.
- `GET` returns 200 `IReadOnlyList<TicketKnowledgeLinkResponse>`.
- `POST` with body `LinkKnowledgeArticleRequest` returns 201 `CreatedAtAction(nameof(List), new { ticketId }, response)`.
- `DELETE {articleId:guid}` returns 204 `NoContent()`.

`TicketsController.cs` is **not** modified.

### 6 — Migration

From `backend/`:

```powershell
dotnet ef migrations add AddKnowledgeBase --project src/Crm.Infrastructure --startup-project src/Crm.Api --output-dir Persistence/Migrations -- --environment Development
```

Review the generated `…_AddKnowledgeBase.cs` before applying. It must contain **only**:
- `CreateTable` ×3: `KnowledgeCategories`, `KnowledgeArticles` (with `Body nvarchar(max)` and `Version rowversion`), `TicketKnowledgeArticleLinks` (`bigint IDENTITY`). All FKs are `ReferentialAction.Restrict`.
- The indexes: `UX_KnowledgeCategories_Name` (unique, **no filter**), `UX_TicketKnowledgeArticleLinks_TicketArticle` (unique), the two article composite indexes, `IX_KnowledgeArticles_CategoryId`, `IX_TicketKnowledgeArticleLinks_ArticleId`, plus the FK indexes EF adds (`AuthorUserId`, `UpdatedByUserId`, `PublishedByUserId`, `CreatedByUserId`, `LinkedByUserId`).
- The model snapshot update.

**No** `AddColumn`/`AlterColumn` on existing tables, and no `DropTable`, `DropColumn`, `RenameColumn`, `InsertData`, `UpdateData` or `Sql(...)`. No seed categories (I1). Apply with `dotnet ef database update --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` (or `.\run-dev.ps1`). Never `EnsureCreated`, drop or recreate `CrmDev`.

### 7 — Backend unit test (the single new one)

**Create file: `backend/tests/Crm.UnitTests/KnowledgeBase/ChangeKnowledgeArticleStatusCommandHandlerTests.cs`** — style of `UploadCustomerAttachmentCommandHandlerTests.cs` (hand-written fakes, `NotSupportedException` for unused members, `FakeUnitOfWork.SaveCount`, `FixedTimeProvider`):

```csharp
public sealed class ChangeKnowledgeArticleStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_SuperAdminPublishesSubmittedAgentArticle_PublishesWithPublisherAndTimeKeepsAuthorAndSavesOnce()
    {
        var agentId = Guid.NewGuid();
        var superAdminId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var now = createdAt.AddHours(2);
        var article = KnowledgeArticle.CreateDraft(
            Guid.NewGuid(), agentId, "Reset a locked account", "Unlock steps for agents", "1. Open **Agents**\n2. Choose *Unlock*",
            KnowledgeArticleType.Solution, Guid.NewGuid(), KnowledgeArticleVisibility.Internal, createdAt);
        article.Submit(agentId, createdAt.AddMinutes(30));
        var knowledgeBase = new FakeKnowledgeBaseRepository(article);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangeKnowledgeArticleStatusCommandHandler(knowledgeBase, unitOfWork, new FixedTimeProvider(now));

        var result = await handler.Handle(
            new ChangeKnowledgeArticleStatusCommand(new StaffActor(superAdminId, IsSuperAdmin: true), article.Id, KnowledgeArticleAction.Publish),
            CancellationToken.None);

        Assert.Equal(KnowledgeArticleStatus.Published, article.Status);
        Assert.Equal(superAdminId, article.PublishedByUserId);
        Assert.Equal(now, article.PublishedAtUtc);
        Assert.Equal(now, article.UpdatedAtUtc);
        Assert.Equal(superAdminId, article.UpdatedByUserId);
        Assert.Equal(agentId, article.AuthorUserId);               // authorship never changes
        Assert.Equal(KnowledgeArticleVisibility.Internal, article.Visibility);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(KnowledgeArticleStatus.Published, result.Status);
        Assert.Equal("Test Admin", result.PublishedByName);
    }
}
```

`FakeKnowledgeBaseRepository(KnowledgeArticle article)`:
- `GetVisibleArticleAsync` returns the article when the id matches.
- `FindArticleDetailsAsync` builds a `KnowledgeArticleDetails` from the entity (`CategoryName "Accounts"`, `CategoryIsActive true`, `AuthorName "Test Agent"`, `UpdatedByName "Test Admin"`, `PublishedByName = article.PublishedByUserId is null ? null : "Test Admin"`, `CanEdit true`, `CanSubmit false`).
- Every other member throws `NotSupportedException`.

`FakeUnitOfWork` and `FixedTimeProvider` are copied from the precedent file (lines 65–76).

**No existing test changes.** `ITicketRepository`, `ICustomerRepository` and every other faked interface are unchanged, so all existing fakes keep compiling.

---

## Frontend Tasks

### 8 — Package

In `frontend/`: `npm install marked@18.0.14`. This adds `"marked": "^18.0.14"` to `dependencies` in `package.json` (same caret style as `@angular/core`) and updates `package-lock.json`. Commit both files later. If `npm install` reports a peer or engine problem, stop and report it; do not switch libraries silently.

### 9 — Models, API service, Markdown renderer

**Create file: `frontend/src/app/features/knowledge-base/knowledge-base-models.ts`**

```ts
import { StatusTone } from '../../shared/ui/status-badge/status-badge';

export type KnowledgeArticleType = 'Faq' | 'HelpArticle' | 'Solution' | 'Guide';
export type KnowledgeArticleStatus = 'Draft' | 'ReadyForPublication' | 'Published' | 'Archived';
export type KnowledgeArticleVisibility = 'Internal' | 'Public';

export const ARTICLE_TYPES: readonly KnowledgeArticleType[] = ['Faq', 'HelpArticle', 'Solution', 'Guide'];
export const ARTICLE_VISIBILITIES: readonly KnowledgeArticleVisibility[] = ['Internal', 'Public'];

const TYPE_LABELS: Record<KnowledgeArticleType, string> = { Faq: 'FAQ', HelpArticle: 'Help Article', Solution: 'Solution', Guide: 'Guide' };
const STATUS_LABELS: Record<KnowledgeArticleStatus, string> = {
  Draft: 'Draft', ReadyForPublication: 'Ready for Publication', Published: 'Published', Archived: 'Archived',
};
const STATUS_TONES: Record<KnowledgeArticleStatus, StatusTone> = {
  Draft: 'neutral', ReadyForPublication: 'pending', Published: 'success', Archived: 'neutral',
};

export const articleTypeLabel = (t: KnowledgeArticleType) => TYPE_LABELS[t];
export const articleStatusLabel = (s: KnowledgeArticleStatus) => STATUS_LABELS[s];
export const articleStatusTone = (s: KnowledgeArticleStatus) => STATUS_TONES[s];

/** Matches Crm.Api KnowledgeCategoryResponse. */
export interface KnowledgeCategory { id: string; name: string; isActive: boolean; updatedAtUtc: string; }

/** Matches Crm.Api KnowledgeArticleSummaryResponse (no body). */
export interface KnowledgeArticleSummary {
  id: string; title: string; summary: string | null; type: KnowledgeArticleType; categoryId: string; categoryName: string;
  visibility: KnowledgeArticleVisibility; status: KnowledgeArticleStatus; authorUserId: string; authorName: string;
  updatedAtUtc: string; publishedAtUtc: string | null;
}

/** Matches Crm.Api KnowledgeArticleResponse. canEdit / canSubmit are display hints; the API re-checks. */
export interface KnowledgeArticle extends KnowledgeArticleSummary {
  body: string; categoryIsActive: boolean; updatedByName: string; createdAtUtc: string; submittedAtUtc: string | null;
  returnReason: string | null; publishedByName: string | null; archivedAtUtc: string | null; canEdit: boolean; canSubmit: boolean;
}

/** Matches Crm.Api TicketKnowledgeLinkResponse — metadata only, never the body. */
export interface TicketKnowledgeLink {
  id: number; ticketId: string; articleId: string; articleTitle: string; articleType: KnowledgeArticleType; categoryName: string;
  articleStatus: KnowledgeArticleStatus; linkedByName: string; linkedAtUtc: string;
}

export interface KnowledgeArticleListParams {
  page: number; pageSize: number; search: string | null; type: KnowledgeArticleType | null; categoryId: string | null;
  status: KnowledgeArticleStatus | null; mine: boolean;
}

/** No status / author fields: those come from workflow endpoints and the token. */
export interface SaveKnowledgeArticleRequest {
  title: string; summary: string | null; body: string; type: KnowledgeArticleType; categoryId: string; visibility: KnowledgeArticleVisibility;
}

export const KB_COPY_HINT = 'Copies the article’s Markdown text only — nothing is sent to the customer.';
export const PUBLIC_VISIBILITY_HINT = 'Public articles become eligible for the future Customer Portal. Nothing is visible to customers today.';
export const KB_LIMITS = { title: 200, summary: 500, body: 20000, categoryName: 100, returnReason: 500 } as const;
```

**Create file: `frontend/src/app/features/knowledge-base/knowledge-base-api.ts`** — `@Injectable({ providedIn: 'root' })`, `HttpClient` + `ApiUrl` (pattern `quick-replies-api.ts` lines 8–47). Base paths: `knowledge-base/articles`, `knowledge-base/categories`, `tickets/${ticketId}/knowledge-articles`.
- `listArticles(params)`: always send `page`, `pageSize`, and `mine` only when true. Send the trimmed `search`, `type`, `categoryId` and `status` only when set (omit empty values, as `QuickRepliesApi.list`).
- `getArticle(id)`, `createArticle(body)`, `updateArticle(id, body)`.
- `submit(id)` / `publish(id)` / `archive(id)` / `restore(id)`: `POST …/{id}/<action>` with body `{}`. `returnToDraft(id, reason: string | null)`: `POST …/{id}/return` with `{ reason }`.
- `listCategories(includeInactive = false)`, `createCategory(name)`, `updateCategory(id, { name, isActive })`.
- `listTicketLinks(ticketId)`, `linkArticle(ticketId, articleId)` (body `{ articleId }`), `unlinkArticle(ticketId, articleId)` (`DELETE`).

**Create file: `frontend/src/app/features/knowledge-base/markdown.ts`**

```ts
import { Marked, Tokens } from 'marked';

const SAFE_LINK = /^(https?:|mailto:)/i;

function escapeHtml(text: string): string {
  return text.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

// Private instance: never mutates the global `marked` defaults.
const markdown = new Marked({
  gfm: true,
  renderer: {
    // Author-provided HTML (block or inline) is shown as text, never rendered (K3).
    html({ text }: Tokens.HTML | Tokens.Tag): string {
      return escapeHtml(text);
    },
    // No embedded images in KB-001: show the alt text only.
    image({ text }: Tokens.Image): string {
      return escapeHtml(text);
    },
    // Only http(s) and mailto links; anything else (javascript:, data:, relative) is rendered as its plain label.
    link({ href, title, tokens }: Tokens.Link): string {
      const label = this.parser.parseInline(tokens);
      if (!SAFE_LINK.test(href.trim())) {
        return label;
      }
      const titleAttr = title ? ` title="${escapeHtml(title)}"` : '';
      return `<a href="${escapeHtml(href)}"${titleAttr} target="_blank" rel="noopener noreferrer">${label}</a>`;
    },
  },
});

/** Markdown source → HTML string for MarkdownView. Bound through [innerHTML], so Angular's sanitizer runs as a second layer. */
export function renderMarkdown(source: string): string {
  return markdown.parse(source, { async: false }) as string;
}
```

If `marked` 18's typings name the token types differently (for example, if `Tokens.Tag` does not exist), use the types from `node_modules/marked/lib/marked.d.ts` and keep the behaviour unchanged. Verify it with the unit test (task 15).

**Create file: `frontend/src/app/features/knowledge-base/markdown-view.ts`**

```ts
/** Safe Markdown display used by preview, article page and ticket card. ViewEncapsulation.None so styles reach [innerHTML] content; all selectors are scoped under .crm-markdown. */
@Component({
  selector: 'crm-markdown-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  template: `<div class="crm-markdown" [innerHTML]="html()"></div>`,
  styles: `
    .crm-markdown { overflow-wrap: anywhere; }
    .crm-markdown :is(h1, h2, h3, h4) { font: var(--mat-sys-title-large); margin-block: var(--crm-space-4) var(--crm-space-2); }
    .crm-markdown :is(p, ul, ol) { margin-block: 0 var(--crm-space-3); }
    .crm-markdown code { font-family: ui-monospace, monospace; background: var(--mat-sys-surface-container-high); padding-inline: var(--crm-space-1); border-radius: 4px; }
    .crm-markdown pre { overflow-x: auto; padding: var(--crm-space-3); background: var(--mat-sys-surface-container-high); border-radius: var(--crm-radius-md); }
    .crm-markdown pre code { padding: 0; background: none; }
    .crm-markdown blockquote { margin-inline: 0; padding-inline-start: var(--crm-space-3); border-inline-start: 3px solid var(--mat-sys-outline-variant); }
  `,
})
export class MarkdownView {
  readonly source = input.required<string>();
  protected readonly html = computed(() => renderMarkdown(this.source()));
}
```

### 10 — Routes and navigation

**Create file: `frontend/src/app/features/knowledge-base/knowledge-base.routes.ts`**

```ts
import { Routes } from '@angular/router';
import { roleGuard } from '../../core/auth/auth-guards';

export const KNOWLEDGE_BASE_ROUTES: Routes = [
  { path: '', title: 'Knowledge Base | Support CRM', loadComponent: () => import('./knowledge-base-page').then(m => m.KnowledgeBasePage) },
  { path: 'new', title: 'New article | Support CRM', loadComponent: () => import('./knowledge-article-edit-page').then(m => m.KnowledgeArticleEditPage) },
  {
    path: 'categories',
    title: 'Knowledge Base categories | Support CRM',
    canActivate: [roleGuard('SuperAdmin')],
    loadComponent: () => import('./knowledge-categories-page').then(m => m.KnowledgeCategoriesPage),
  },
  { path: ':id', title: 'Article | Support CRM', loadComponent: () => import('./knowledge-article-page').then(m => m.KnowledgeArticlePage) },
  { path: ':id/edit', title: 'Edit article | Support CRM', loadComponent: () => import('./knowledge-article-edit-page').then(m => m.KnowledgeArticleEditPage) },
];
```

**File: `frontend/src/app/app.routes.ts`** — insert before the comment on line 70:

```ts
      {
        path: 'knowledge-base',
        canActivate: [roleGuard('SuperAdmin', 'Agent')],
        loadChildren: () => import('./features/knowledge-base/knowledge-base.routes').then(m => m.KNOWLEDGE_BASE_ROUTES),
      },
```

**File: `frontend/src/app/core/layout/navigation.ts`** — after the Quick Replies entry (line 19):

```ts
  { label: 'Knowledge Base', icon: 'menu_book', path: '/knowledge-base', exact: false, roles: ['SuperAdmin', 'Agent'] },
```

### 11 — Knowledge Base list page

**Create files: `frontend/src/app/features/knowledge-base/knowledge-base-page.ts`, `.html`, `.scss`** — copy the structure of `quick-reply-list-page.ts` lines 26–130 / `.html` lines 1–84: `Subject<KnowledgeArticleListParams>` + `switchMap` + `catchError` loader, `loading` / `error` signals, Retry button, `mat-paginator` with `[10, 20, 50]`. The scss is `@use '../../shared/ui/productivity-page';` plus a 2-line clamp for `.summary`.

- Header: `<crm-page-header title="Knowledge Base" subtitle="FAQs, help articles, solutions and guides for staff." />`.
- Toolbar (`[formGroup]="filters"`):
  - `search` ("Title, summary or text");
  - `type` ("Any" + `ARTICLE_TYPES` with `articleTypeLabel`);
  - `categoryId` ("Any" + categories, loaded once with `listCategories(isSuperAdmin())`; inactive ones labelled "(inactive)");
  - `view` select.
  - `view` options:
    - Agent: `published` ("Published"), `mine` ("My articles").
    - SuperAdmin: `published`, `review` ("Ready for Publication"), `all` ("All statuses"), `archived` ("Archived"), `mine` ("My articles").
  - Map `view` to params: `published` ⇒ `status: 'Published'`; `review` ⇒ `'ReadyForPublication'`; `archived` ⇒ `'Archived'`; `all` ⇒ `status: null`; `mine` ⇒ `mine: true, status: null`.
  - Default view `published`. Read an optional `?view=` query param once at start, so the dashboard and nav can deep-link. Refreshing never widens access, because the server re-applies visibility.
  - Buttons: `<a mat-flat-button routerLink="/knowledge-base/new"><mat-icon>add</mat-icon> New article</a>`, and for SuperAdmin `<a mat-button routerLink="/knowledge-base/categories">Manage categories</a>`.
- Rows (`.rows` / `.row`):
  - title as `routerLink` to `['/knowledge-base', a.id]`;
  - `<crm-status-badge [label]="articleStatusLabel(a.status)" [tone]="articleStatusTone(a.status)" />`;
  - `<crm-status-badge [label]="a.visibility" [tone]="a.visibility === 'Public' ? 'pending' : 'neutral'" />`;
  - `<span class="hint">{{ articleTypeLabel(a.type) }} · {{ a.categoryName }}</span>`;
  - `<p class="meta">{{ a.authorName }} · updated {{ a.updatedAtUtc | date: 'medium' }}</p>`;
  - `@if (a.summary) { <p class="summary user-text">{{ a.summary }}</p> }`.
  - Plain interpolation only: list rows never render Markdown.
- Empty state: "No articles match." When no categories exist: Agent sees "No active categories yet — ask a SuperAdmin to add one before creating articles."; SuperAdmin sees a link "Add the first category".

### 12 — Article page (read, copy, workflow)

**Create files: `frontend/src/app/features/knowledge-base/knowledge-article-page.ts`, `.html`** (styleUrl `../../shared/ui/productivity-page.scss`, plus the `.readonly-list` rules copied from `ticket-detail-page.scss` lines 7–21 into a small `knowledge-article-page.scss`):

- Load `getArticle(id)`. On 404 show "This article is not available to you. It may be unpublished or archived." Other errors use `describeHttpError` and a Retry button.
- Back link "← Back to Knowledge Base". Title as `<h1>` via `crm-page-header [title]="article.title"`, with the summary as subtitle.
- Metadata `<dl class="readonly-list">`:
  - Type, Category (+ " (inactive)" when `!categoryIsActive`), Visibility (+ `PUBLIC_VISIBILITY_HINT` as `.hint` when Public), Status badge;
  - Author, Created, Last updated (`updatedByName`, date);
  - Submitted (if any), Published (date + `publishedByName`, when set), Archived (when set);
  - **Returned for revision:** `returnReason` (when Draft and set).
- Body: `<crm-markdown-view [source]="article.body" />` in an outlined `mat-card`.
- Actions (`.actions`, disabled while `busy()`):
  - always "Copy" (`copyQuickReply(this.clipboard, this.snackBar, article.body)`) with `KB_COPY_HINT` as `.hint`;
  - `canEdit` ⇒ `<a mat-stroked-button [routerLink]="['/knowledge-base', article.id, 'edit']">Edit</a>`;
  - `canSubmit` ⇒ "Submit for publication";
  - SuperAdmin + `ReadyForPublication` ⇒ an optional "Reason for returning" textarea (`maxlength 500`) + "Return to Draft" and "Publish";
  - SuperAdmin + not `Archived` ⇒ "Archive";
  - SuperAdmin + `Archived` ⇒ "Restore to Draft".
  - Each action calls the API, replaces `article` with the response and shows a snackbar ("Submitted for publication.", "Returned to Draft.", "Published.", "Archived.", "Restored to Draft.").
  - 409 ⇒ show the server `detail` and reload. 404 ⇒ the not-available message. 403 ⇒ the server `detail`.

### 13 — Article editor with Markdown preview

**Create files: `frontend/src/app/features/knowledge-base/knowledge-article-edit-page.ts`, `.html`** (styleUrl `../../shared/ui/productivity-page.scss`; pattern `quick-reply-edit-page.ts` lines 21–124):

- `id` from the route (`null` ⇒ new). For an existing id, load `getArticle(id)`. If `!canEdit`, show "You cannot edit this article." plus a link to the article page, and no form.
- Load `listCategories(false)` (active). When editing an article whose category is inactive, add that category to the options as "<name> (inactive)" so the unchanged value stays selectable (the server accepts it unchanged).
- Form (`NonNullableFormBuilder`):
  - `title` (`required`, `pattern(/\S/)`, `maxLength(200)`);
  - `summary` (`maxLength(500)`);
  - `type` (`'Solution'` default, required);
  - `categoryId` (`''`, required);
  - `visibility` (`'Internal'` default);
  - `body` (`required`, `pattern(/\S/)`, `maxLength(20000)`).
  - Character hints `x / 200`, `x / 500`, `x / 20000`. Show `PUBLIC_VISIBILITY_HINT` under the visibility select.
- Body editing: `<mat-tab-group animationDuration="0ms">` (as `sla-admin-page.html` lines 6–19) with tabs "Write" (`<textarea matInput rows="16" formControlName="body">`) and "Preview" (`<crm-markdown-view [source]="form.controls.body.value" />`, or `<p class="hint">Nothing to preview yet.</p>` when blank).
- Below it, `<details class="hint"><summary>Markdown help</summary>…</details>` listing `# Heading`, `**bold**`, `*italic*`, `1. step`, `- item`, `` `code` ``, a fenced code block, and `[text](https://example.com)`. Add a sentence: "HTML and images are not supported and are shown as plain text."
- Status line `.hint`: new ⇒ "New articles are saved as Draft. Submit them for publication when ready." Existing ⇒ "Status: <label>. Saving does not change the status."
- Save: trim `title`/`body`; empty summary ⇒ `null`. `createArticle` ⇒ navigate to `['/knowledge-base', a.id]`; `updateArticle` ⇒ navigate to the same. Errors via `describeSaveError(error, 'Could not save the article.')` (`features/sla/sla-errors.ts` lines 6–10), so 400 field messages are visible. On 409/403, show the server detail.
- When the category list is empty: disable the form submit and show the I1 message.

### 14 — Categories page (SuperAdmin)

**Create files: `frontend/src/app/features/knowledge-base/knowledge-categories-page.ts`, `.html`** (styleUrl `../../shared/ui/productivity-page.scss`):

- `listCategories(true)`. "Add category" form (`name`, `required`, `pattern(/\S/)`, `maxLength(100)`) ⇒ `createCategory` ⇒ reload.
- One `.row` per category: name input (a `FormControl` per row, kept in a `Map<string, FormControl<string>>`), an "Active"/"Inactive" status badge, "Save name", and "Deactivate" / "Activate" (both call `updateCategory(id, { name, isActive })`).
- 409 duplicate ⇒ show the server detail. Hint: "Deactivated categories stay on existing articles but cannot be chosen for new ones."

### 15 — Ticket Details Knowledge Base card

**Create files: `frontend/src/app/features/knowledge-base/ticket-knowledge-articles.ts`, `.html`** (styleUrl `../../shared/ui/productivity-page.scss`):

```ts
/** KB-001 K4 card for the authorized Ticket Details page. Search/read/copy published articles and link/unlink them. Copy only — nothing is sent. */
@Component({ selector: 'crm-ticket-knowledge-articles', … })
export class TicketKnowledgeArticles {
  readonly ticketId = input.required<string>();
  /** The caller lost ticket access (404): the parent shows its "no longer available" state (as TicketDiscussion). */
  readonly accessLost = output<void>();
  …
}
```

- **Linked articles** (loaded on init and after each change): `listTicketLinks(ticketId)`. Each row:
  - Published ⇒ title as `routerLink` `['/knowledge-base', l.articleId]`; otherwise plain title + status badge (`articleStatusLabel`), with `.hint` "No longer published — its content is hidden.";
  - `articleTypeLabel(l.articleType) · l.categoryName`;
  - `.meta` "Linked by {{ l.linkedByName }} · {{ l.linkedAtUtc | date: 'medium' }}";
  - "Unlink" (`mat-button`).
  - Empty state: "No articles linked to this ticket yet."
- **Find an article:** a `FormControl` search with `debounceTime(300)` + `switchMap` (pattern `quick-reply-panel.ts` lines 37–54) calling `listArticles({ page: 1, pageSize: 5, search, type: null, categoryId: null, status: 'Published', mine: false })`. Each result row has title, type/category, and the buttons:
  - "Read" toggles an inline panel: `getArticle(id)` ⇒ `<crm-markdown-view [source]="selected.body" />` + "Copy" (`copyQuickReply`, synchronous inside the click so the clipboard works) + `KB_COPY_HINT`;
  - "Link" (disabled while busy, or when `links().some(l => l.articleId === a.id)`; the label then reads "Linked").
- Errors. A 404 means different things depending on the call, so only a confirmed ticket-access loss hides the page:
  - **`listTicketLinks` 404** ⇒ the caller lost access to the ticket (the endpoint returns 404 only when `TicketIsVisibleAsync` is false) ⇒ `accessLost.emit()`. This is the same behaviour as `TicketDiscussion`.
  - **`linkArticle` / `unlinkArticle` 404** ⇒ ambiguous. It can mean "Knowledge article … was not found." (article archived, unpublished or invisible), "This article is not linked to the ticket." (already unlinked elsewhere), or "Ticket … was not found." (ticket access lost). **Do not emit `accessLost` directly.** Show the server `detail` as an inline `.form-error` in the card (`describeHttpError(error, 'The article could not be linked.')` / `'… unlinked.'`), then call `reloadLinks()`. `reloadLinks()` calls `listTicketLinks`: if that returns 404, the ticket really is gone and the list-load rule above emits `accessLost`; if it succeeds, the list is refreshed and the Ticket Details page stays visible. After a link 404, also re-run the current search so an article that is no longer published drops out of the results.
  - **`getArticle` 404 on "Read"** ⇒ inline "This article is no longer available." in the result row, close the preview and re-run the search. Never emit `accessLost` (article access is independent of ticket access).
  - **409** (duplicate, not published, cap reached) ⇒ show the server detail inline and call `reloadLinks()`.
  - Other errors use `describeHttpError` and a Retry button that calls `reloadLinks()`.
  - Clear the inline error on the next successful link, unlink or search.
- After link/unlink, show a snackbar ("Article linked to the ticket." / "Article unlinked.").
- There is **no** Send/Reply button anywhere.

**File: `frontend/src/app/features/tickets/ticket-detail-page.ts`** — add `import { TicketKnowledgeArticles } from '../knowledge-base/ticket-knowledge-articles';` (after line 22) and `TicketKnowledgeArticles` in the `imports` array (after `QuickReplyPanel`, line 53). No other change: `onAccessLost()` (lines 248–252) is reused.

**File: `frontend/src/app/features/tickets/ticket-detail-page.html`** — after the Quick replies card (lines 221–224), before `</div>` on line 225:

```html
    <mat-card appearance="outlined">
      <mat-card-header><mat-card-title>Knowledge Base</mat-card-title></mat-card-header>
      <mat-card-content>
        <crm-ticket-knowledge-articles [ticketId]="current.id" (accessLost)="onAccessLost()" />
      </mat-card-content>
    </mat-card>
```

The card is inside `@if (ticket(); as current)` (line 9). It renders only after `GET /api/v1/tickets/{id}` succeeded, so a collaborator-only Agent (404) never sees it. **`frontend/src/app/features/collaboration/collaboration-ticket-page.html` is not changed.**

### 16 — Frontend unit test (the single new one)

**Create file: `frontend/src/app/features/knowledge-base/markdown.spec.ts`** — Vitest globals as in `tickets-api.spec.ts`. No TestBed is needed (pure function):

```ts
import { renderMarkdown } from './markdown';

describe('renderMarkdown', () => {
  it('renders headings, ordered steps, emphasis, code and safe links, and shows raw HTML as text', () => {
    const html = renderMarkdown(
      [
        '## Reset a password',
        '',
        '1. Open **Agents**',
        '2. Choose *Set password*',
        '',
        'Run `reset` and read [the guide](https://example.com/guide).',
        '',
        '<script>alert(1)</script>',
        '',
        '[bad link](javascript:alert(1))',
      ].join('\n'),
    );

    expect(html).toContain('<h2>Reset a password</h2>');
    expect(html).toContain('<ol>');
    expect(html).toContain('<strong>Agents</strong>');
    expect(html).toContain('<em>Set password</em>');
    expect(html).toContain('<code>reset</code>');
    expect(html).toContain('<a href="https://example.com/guide" target="_blank" rel="noopener noreferrer">the guide</a>');
    expect(html).toContain('&lt;script&gt;');
    expect(html).not.toContain('<script');
    expect(html).not.toContain('javascript:');
    expect(html).toContain('bad link');
  });
});
```

If `marked` 18 adds `id` attributes to headings (older majors did), adjust only that expectation to `toMatch(/<h2[^>]*>Reset a password<\/h2>/)`. Do not change the renderer to satisfy the test.

---

## Documentation Tasks

### 17 — `docs/knowledge-base.md` (new)

Sections:
1. Decisions K1–K4 (approved) and interpretations I1–I6.
2. Role matrix (copy from this plan).
3. Article model (fields, limits, types; FAQ = title/answer).
4. Lifecycle diagram (text) and the transition matrix, including who may trigger what.
5. Visibility: Internal/Public vs status. **No customer endpoint in KB-001; the future Portal must expose only Published + Public through its own contract.**
6. Markdown: supported syntax, escaped HTML, images as alt text, link protocols, `marked` version, source-only storage, copy = Markdown source.
7. Search and paging (LIKE on title/summary/body, filters, order, limits, no full-text).
8. Ticket integration: scope = `TicketRepository.Scope`, collaborators excluded, Published-only linking, 25-link cap, archived links kept and labelled, nothing sent, ticket unchanged.
9. API table (all routes with roles and status codes).
10. UI routes.
11. Migration impact.
12. Limitations: no revision history; published edits take effect immediately; the I5 race window; no relative links; category required.

### 18 — Other docs

- `docs/identity-and-access.md`:
  - Matrix (after line 31) — add rows: "Knowledge Base: read/search Published articles | ✔ | ✔ | —", "Knowledge Base: create/edit own Draft, submit | ✔ any Draft | ✔ own | —", "Knowledge Base: return/publish/archive/restore, manage categories | ✔ | — | —", "Link/unlink KB articles on tickets | ✔ any ticket | ✔ assigned to them | —".
  - Routes (before the `/api/health` row, line 68) — add `* /api/v1/knowledge-base/articles/*` (SuperAdmin; Agent with `return|publish|archive|restore` 403), `* /api/v1/knowledge-base/categories/*` (GET staff; POST/PUT SuperAdmin), and `GET / POST / DELETE /api/v1/tickets/{id}/knowledge-articles[/{articleId}]` (SuperAdmin, Agent only own assigned).
- `docs/api-conventions.md` line 3 — add `/api/v1/knowledge-base/{articles|categories}` as the module-prefix exception (I4). In the auth paragraph, add "Agents editing a Published KB article" to the 403 examples.
- `docs/persistence-and-transactions.md` line 6 — mention `KnowledgeArticles.Version` (rowversion ⇒ 409) and the two new `KnownUniqueIndexes` entries.
- `docs/ticket-management.md` §9 API (line 119) — the nested `knowledge-articles` endpoints ("never change the ticket"). §12 (line 150) — a link to `knowledge-base.md`.
- `README.md` line 3 — append "and Knowledge Base management (`KB-001`)" to the delivered-stories sentence. Change the existing "… and SLA & basic automation (`SLA-001`)" to use a comma before the new item.

---

## Edge Cases & Failure Modes

- **Agent publishes via direct API:** `POST /knowledge-base/articles/{id}/publish` with an Agent token ⇒ **403** from `[Authorize(Policy = SuperAdmin)]` (controller). Even if the policy were removed, `KnowledgeBaseGuards.EnsureSuperAdmin` ⇒ 403. The edit payload (`SaveKnowledgeArticleRequest`) has no `status` field. A crafted `"status": "Published"` JSON property is ignored by System.Text.Json, and `UpdateContent` never touches `Status`.
- **Spoofed author/publisher in the body:** extra JSON properties are ignored. `AuthorUserId`, `UpdatedByUserId`, `PublishedByUserId` and `LinkedByUserId` come only from `StaffActor` (JWT `sub`).
- **Agent reads or edits another Agent's Draft/Submitted article:** the `Visible` predicate (`KnowledgeBaseRepository`) excludes it ⇒ **404** for GET, PUT, submit and link. Not 403, so the article's existence is not revealed.
- **Agent edits own submitted article:** 409 "This article is waiting for publication…" (`KnowledgeBaseGuards.EnsureCanEdit`). **Agent edits a Published article:** 403. **Anyone edits an Archived article:** 409 (SuperAdmin) or 404 (Agent, not visible).
- **Unpublished Public article:** never in Published search (`status=Published` filter). Invisible to other Agents. No customer endpoint exists, so customers and anonymous callers get 403 / 401 on every KB route (class-level `Staff` policy plus the fallback auth policy).
- **Wrong-state workflow call** (publish a Draft, return a Published article, submit twice, restore a non-archived article): 409 with the action-specific message (`ChangeKnowledgeArticleStatusCommandHandler.ConflictMessage`). `KnowledgeArticle.MoveTo` throws `DomainException` (422) only if a handler bug skips the check.
- **Two SuperAdmins act at once** (one publishes while the other edits or archives): the shadow `Version` rowversion ⇒ `DbUpdateConcurrencyException` ⇒ 409 "The record was changed by another request…" (`EfUnitOfWork.cs` lines 23–27). The UI shows the detail and reloads.
- **Agent edits a Draft in one tab and submits it in another:** the second save hits the rowversion ⇒ 409, so content cannot change silently after submission.
- **Validation:** whitespace-only title or body ⇒ 400 (`NotEmpty`). Title > 200, summary > 500, body > 20,000 ⇒ 400 with camelCased keys (`title`, `summary`, `body`). Missing type/category/visibility ⇒ 400 (nullable + `NotNull`). Integer enum values ⇒ 400 (`allowIntegerValues: false`). Search > 100 ⇒ 400. `page=0` ⇒ 400. `pageSize=500` ⇒ clamped to 100.
- **Inactive or unknown category:** create, or update with a changed category ⇒ 400 `categoryId` "Choose an active category.". Update with the unchanged inactive category ⇒ accepted. Deactivating a category never changes existing articles. Category names differing only by case ⇒ 409 (pre-check plus `UX_KnowledgeCategories_Name` in `KnownUniqueIndexes`).
- **No categories yet (I1):** the editor shows the I1 message and cannot save. API create without `categoryId` ⇒ 400.
- **Search input with `%`, `_` or `[`:** EF parameterizes and escapes for `Contains`, so these match literally. Unicode is fine (nvarchar).
- **Large body search:** `LIKE '%term%'` on `nvarchar(max)` scans the rows. This is acceptable for a training dataset and documented in Limitations. Paging is always server-side.
- **Ticket links — collaborator-only Agent:** `TicketRepository.Scope` uses `AssignedAgentUserId` only ⇒ 404 on GET/POST/DELETE. The card never renders because `GET /tickets/{id}` already returned 404.
- **Ticket links — Agent on another Agent's ticket via direct API:** 404 (same scope).
- **Link a Draft / Submitted / Archived article:** visible to the caller but not Published ⇒ 409. Invisible ⇒ 404.
- **Duplicate link:** pre-check ⇒ 409. A concurrent duplicate ⇒ the unique index ⇒ `KnownUniqueIndexes` ⇒ 409 (never 500).
- **26th link:** 409 "A ticket can have at most 25 linked articles."
- **Reassignment between check and insert (I5):** a link may be added by an Agent who lost the ticket a moment earlier. A link grants no access and changes nothing on the ticket. Accepted and documented. A plain read afterwards returns 404 for that Agent.
- **Linked article archived:** the link row stays (FK `Restrict`; archive is a status change, not a delete). The list shows the title and status "Archived" but never the body. An Agent opening it ⇒ 404 from `GET /knowledge-base/articles/{id}` ⇒ the card shows "No longer published". SuperAdmin can open it. Restore + republish ⇒ the same `ArticleId` and the same link `Id`.
- **Unlink twice:** second call ⇒ 404 "This article is not linked to the ticket." The card shows the message inline and reloads the list. The page stays visible (F1).
- **Article-related 404 on Ticket Details (F1):** linking an article that was archived or unpublished in another tab ⇒ `POST` 404 or 409 ⇒ inline error, links and search reloaded, Ticket Details stays visible. Only a 404 from `GET /tickets/{id}/knowledge-articles` (the list load, including the reload after a failed link or unlink) emits `accessLost`, which runs the parent's existing `onAccessLost()` (`ticket-detail-page.ts` lines 248–252). A ticket reassigned away between page load and a link click ⇒ `POST` 404 ⇒ reload ⇒ list 404 ⇒ `accessLost`, so a real access loss is still detected.
- **Ticket unchanged by links:** `TicketIsVisibleAsync` uses `AsNoTracking().AnyAsync`, so the `Ticket` entity is never attached. `Tickets.UpdatedAtUtc`, `Version`, status, assignment, SLA columns and `TicketActivities` are untouched. Verified by SQL in Verification step 12.
- **Unsafe Markdown:** `<script>`, `<img onerror>`, `<iframe>` ⇒ escaped text (renderer `html`). `![x](http://…)` ⇒ alt text only. `[x](javascript:…)` / `data:` / relative links ⇒ plain label. Angular's sanitizer runs on `[innerHTML]` as a second layer. In Development, Angular logs a console warning only if it stripped something; it should strip nothing because the renderer already removed it.
- **Very long words or code lines:** `overflow-wrap: anywhere` and `pre { overflow-x: auto }` prevent horizontal page scroll at narrow widths.
- **Clipboard blocked:** `copyQuickReply` shows "Could not copy. Select the text and copy it manually." Copy runs synchronously in the click handler after the article is loaded (ticket card "Read" first).
- **Refresh on `/knowledge-base/:id/edit` as an Agent for a now-submitted article:** `canEdit` false ⇒ read-only message. The server also returns 409 if a stale form is submitted.
- **SuperAdmin `/knowledge-base/categories` deep link as Agent:** `roleGuard('SuperAdmin')` redirects home. The API returns 403 for POST/PUT anyway.
- **Migration pending:** KB endpoints fail with 500 (tables missing) and other modules are unaffected. Apply the migration before use.

---

## Test Plan

1. **Unit (backend, new — the only one):** `backend/tests/Crm.UnitTests/KnowledgeBase/ChangeKnowledgeArticleStatusCommandHandlerTests.cs` — `Handle_SuperAdminPublishesSubmittedAgentArticle_PublishesWithPublisherAndTimeKeepsAuthorAndSavesOnce` (task 7). Covers K1 publish metadata, author preservation and the single save.
2. **Unit (frontend, new — the only one):** `frontend/src/app/features/knowledge-base/markdown.spec.ts` — "renders headings, ordered steps, emphasis, code and safe links, and shows raw HTML as text" (task 16). Covers K3.
3. **Existing tests:** all must pass unchanged. Record the baseline before starting (`dotnet test` in `backend/`, `npm test -- --watch=false` in `frontend/`; expected **9 and 9** at `2702200`). After: **10 and 10**.
4. **No** integration, API, SQL, E2E or Playwright suites, and no new test packages (intake §13).
5. **Manual (API via `Invoke-RestMethod` or Postman CLI, SQL via `sqlcmd`, browser):** Verification Steps 4–17, mapped to intake §13 checks 1–13.

---

## Migration / Rollback

- **Before applying:** record baselines: `sqlcmd -S "(localdb)\MSSQLLocalDB" -d CrmDev -Q "SELECT COUNT(*) FROM Tickets; SELECT COUNT(*) FROM TicketActivities; SELECT COUNT(*) FROM Customers; SELECT COUNT(*) FROM AgentTasks; SELECT COUNT(*) FROM QuickReplyTemplates; SELECT COUNT(*) FROM CustomerNotes; SELECT COUNT(*) FROM SlaAlerts; SELECT COUNT(*) FROM AspNetUsers; SELECT MAX(UpdatedAtUtc) FROM Tickets;"`. After applying: identical results. The three new tables exist and are empty.
- **Changes to existing tables:** none (only new FKs **from** the new tables to `Tickets` and `AspNetUsers`).
- **Rollback (local):** `dotnet ef database update AddSlaAndAutomation --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` drops the three tables, **including any KB articles and links created during testing**. Confirm with the user before running it on a database with real content. Then run `dotnet ef migrations remove --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` if the migration is discarded.
- **Half-applied state:** SQL Server runs the migration in one transaction, so a failure leaves no partial tables and no `__EFMigrationsHistory` row. Fix and re-run.

---

## Verification Steps

1. **Baseline:** in the repo root, `git status`: only the untracked KB intake/plan folders. In `backend/`: `dotnet test` ⇒ 9 passed. In `frontend/`: `npm test -- --watch=false` ⇒ 9 passed. Record both.
2. **Backend builds and tests:** in `backend/`: `dotnet build` (0 errors; list pre-existing warnings separately), `dotnet test` ⇒ 10 passed.
3. **Migration review and layering:** in `backend/`:
   - `dotnet ef migrations list --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ only `…_AddKnowledgeBase (Pending)`.
   - `dotnet ef migrations script AddSlaAndAutomation AddKnowledgeBase --project src/Crm.Infrastructure --startup-project src/Crm.Api --output $env:TEMP/AddKnowledgeBase.sql -- --environment Development` ⇒ 3 `CREATE TABLE`, the unique indexes, `[Body] nvarchar(max)`, `[Version] rowversion`, no `ALTER TABLE [Tickets] ADD`, no `DROP`/`UPDATE`/`DELETE`/`INSERT`.
   - Record baselines (Migration / Rollback) ⇒ `dotnet ef database update …` ⇒ counts unchanged.
   - `dotnet ef migrations has-pending-model-changes --project src/Crm.Infrastructure --startup-project src/Crm.Api -- --environment Development` ⇒ no changes.
   - `grep -rn "EntityFrameworkCore\|Microsoft.AspNetCore" src/Crm.Application src/Crm.Domain --include=*.cs` ⇒ nothing new.
4. **Frontend builds and tests:** in `frontend/`: `npx ng build` (no errors; budget warnings recorded), `npm test -- --watch=false` ⇒ 10 passed. Confirm `marked` is in a lazy chunk (the initial bundle size is unchanged within a few kB).
5. **Setup (nothing existing deleted, reset or reassigned):** `.\run-dev.ps1`. As SuperAdmin create Agents "Kb Alpha" (`kb.alpha@example.test`) and "Kb Bravo" (`kb.bravo@example.test`). Register Customer `kb.customer@example.test`. Get tokens `$admin`, `$alpha`, `$bravo`, `$customer` from `POST /api/v1/auth/login` (never paste them into the report). Create tickets T1 assigned to Alpha and T2 assigned to Bravo (SuperAdmin `POST /api/v1/tickets`). Invite Alpha as a collaborator on T2 (Bravo, `POST /api/v1/collaborations/{T2}/collaborators`).
6. **Categories (§13.6):** `$alpha` `POST /api/v1/knowledge-base/articles` with no categories ⇒ 400 `categoryId`. `$admin` `POST /knowledge-base/categories` `{ "name": "Accounts" }` ⇒ 201; `"accounts"` ⇒ 409; `"Billing"` ⇒ 201. `$alpha` POST category ⇒ 403. `PUT` "Billing" `isActive: false` ⇒ 200; `$alpha` `GET /categories` ⇒ only Accounts; `$admin` `?includeInactive=true` ⇒ both. SQL `SELECT Name, IsActive FROM KnowledgeCategories`.
7. **Drafts and validation (§13.1):** `$alpha` creates a Solution in Accounts (Internal) ⇒ 201, `status: "Draft"`, `authorName` "Kb Alpha". Try with body `"   "` ⇒ 400 `body`, title of 201 characters ⇒ 400 `title`, body of 20,001 characters ⇒ 400 `body`, `"type": 2` ⇒ 400, the inactive "Billing" category ⇒ 400 `categoryId`. Send `"status": "Published"` and `"authorUserId": "<bravo>"` in the body ⇒ ignored (response is still Draft/Alpha). `$admin` creates its own Draft ⇒ 201. SQL `SELECT Title, Status, AuthorUserId, CreatedAtUtc FROM KnowledgeArticles` ⇒ UTC, the correct authors.
8. **Own-draft rules (§13.2):** `$alpha` `PUT` own Draft ⇒ 200 (same `id`; `updatedAtUtc` changes). `$bravo` `GET`/`PUT`/`submit` on Alpha's Draft ⇒ 404 each. `$alpha` `POST …/submit` ⇒ `ReadyForPublication`. `$alpha` `PUT` it ⇒ 409. `$alpha` `POST …/publish` ⇒ **403** (`/return`, `/archive`, `/restore` ⇒ 403 too).
9. **Review and publish (§13.3):** `$admin` `GET ?status=ReadyForPublication` ⇒ Alpha's article listed. `POST …/return` `{ "reason": "Add the unlock step." }` ⇒ Draft with `returnReason`. `$alpha` edits and resubmits ⇒ `returnReason` null. `$admin` `PUT` (edit while submitted) ⇒ 200, still `ReadyForPublication`. `POST …/publish` ⇒ `Published`, `publishedByName` "System Administrator" (or the bootstrap SuperAdmin's name), `publishedAtUtc` set, `authorName` still "Kb Alpha". `publish` again ⇒ 409. SQL `SELECT Status, AuthorUserId, PublishedByUserId, PublishedAtUtc, UpdatedByUserId FROM KnowledgeArticles WHERE Id='<A1>'`. `$alpha` `PUT` the Published article ⇒ 403; `$admin` `PUT` ⇒ 200, still Published.
10. **Visibility and search (§13.4, §13.5, §13.8):** publish A1 (Internal Solution) and A2 (Public FAQ "How do I change my email?"). Leave A3 as a Public Draft and A4 Submitted. `$alpha` `GET ?status=Published` ⇒ A1 and A2 only. `$bravo` default list ⇒ no A3/A4 (not his). `$admin` `?status=Published` ⇒ no A3/A4. Search: a word only in A2's **body** ⇒ A2; a word only in A1's **summary** ⇒ A1; a title word ⇒ match; `search=%` ⇒ only articles containing a literal `%`. `type=Faq` ⇒ A2; `categoryId=<Accounts>` ⇒ the matching rows; `page=2&pageSize=1` ⇒ the second item and `totalCount` correct; nonsense search ⇒ empty `items`, `totalCount: 0`. Response rows have **no** `body` property. `$customer` on `GET /knowledge-base/articles`, `/articles/{A2}`, `/categories`, `/tickets/{T1}/knowledge-articles` ⇒ 403 each. Without a token ⇒ 401. Confirm in `openapi/v1.json` that no `[AllowAnonymous]` KB route exists.
11. **Types and Markdown (§13.6, §13.7):** create and publish one article of each type (FAQ, Help Article, Solution, Guide), with a Guide body containing `## Steps`, `1. …`, `2. …`, `**bold**`, a fenced code block, `[docs](https://example.com)`, `<script>alert('x')</script>`, `<img src=x onerror=alert(1)>`, `[evil](javascript:alert(1))` and `![logo](https://example.com/x.png)`. API and SQL: the stored `body` (`GET …/articles/{id}` and `SELECT Body FROM KnowledgeArticles WHERE Id='<guide>'`) equals the **normalized** Markdown source, i.e. the submitted text with leading and trailing whitespace trimmed (`Body.Trim()` in the create/update handlers). Interior content, line breaks, Markdown syntax and the literal `<script>` / `<img>` text are unchanged. No HTML is stored. To check trimming, submit the body with leading blank lines and trailing spaces and confirm only those are removed. Browser: preview and reader show the heading, numbered steps, bold, code block and a working external link (new tab). The script/img text appears literally, no alert fires, "evil" is plain text and the image shows as "logo" text. Check the DevTools Elements panel: no `<script>`, no `onerror` and no `javascript:` href.
12. **Ticket integration (§13.9, §13.10, §13.12):** before linking, record a SQL snapshot: `SELECT Status, AssignedAgentUserId, UpdatedAtUtc, IsEscalated, SlaFirstResponseDueAtUtc, CONVERT(varchar(20), Version, 1) FROM Tickets WHERE Id IN ('<T1>','<T2>'); SELECT COUNT(*) FROM TicketActivities WHERE TicketId IN ('<T1>','<T2>');`.
    - `$alpha` `POST /api/v1/tickets/<T1>/knowledge-articles` `{ "articleId": "<A1>" }` ⇒ 201. Repeat ⇒ 409. Link A3 (own Draft, visible) ⇒ 409 "Only published…". Link Bravo's Draft ⇒ 404. `GET` ⇒ A1 with `linkedByName` "Kb Alpha" and no `body`.
    - `$alpha` on T2 (collaborator only) ⇒ GET/POST/DELETE 404. `$bravo` on T1 ⇒ 404. `$admin` links A2 on T2 ⇒ 201 and unlinks ⇒ 204.
    - `$alpha` `DELETE …/<T1>/knowledge-articles/<A1>` ⇒ 204, again ⇒ 404. Re-link A1.
    - Re-run the snapshot query ⇒ **identical** (same `UpdatedAtUtc`, `Version`, status, assignee, SLA, activity count).
    - SQL `SELECT TicketId, ArticleId, LinkedByUserId, LinkedAtUtc FROM TicketKnowledgeArticleLinks`.
    - Link 25 articles to one ticket (a script loop) ⇒ the 26th returns 409. Unlink the extras afterwards.
13. **Archive / restore (§13.11):** `$admin` `POST …/<A1>/archive` ⇒ Archived. `$alpha` `GET ?status=Published` ⇒ no A1; `GET /articles/<A1>` ⇒ 404; `GET /tickets/<T1>/knowledge-articles` ⇒ A1 still listed with `articleStatus: "Archived"` and no body. New link of A1 to another ticket ⇒ 409 (SuperAdmin) / 404 (Agent). `$admin` `GET /articles/<A1>` ⇒ 200. `POST …/restore` ⇒ Draft with publish fields null. `/submit` then `/publish` ⇒ Published. SQL: the link row has the same `Id` and the same `ArticleId` as before archiving.
14. **Browser — KB workspace (§13.13):**
    - As Alpha: the "Knowledge Base" nav entry appears. `/knowledge-base` shows Published by default, the "My articles" view, type/category filters and paging, plus loading bar, empty state and stop-API ⇒ error + Retry.
    - New article form: inline validation and character counters; Write/Preview tabs; Markdown help; the I1 message when no category exists (verify before step 6 or on a fresh category state, otherwise mark it **Unverified**).
    - Detail page: metadata, Copy (snackbar "Copied to clipboard."), Submit; no Publish/Archive buttons for Alpha.
    - As SuperAdmin: views Ready for Publication / All / Archived; Return with reason, Publish, Archive, Restore; `/knowledge-base/categories` add/rename/deactivate with the duplicate 409 message.
    - As Alpha, `/knowledge-base/categories` ⇒ redirected home.
    - Refresh each page ⇒ same data; nothing extra becomes visible.
15. **Browser — Ticket Details card (§13.9, §13.13):** as Alpha on T1: the Knowledge Base card is the last card. Search finds published articles only. Read shows the rendered body. Copy ⇒ snackbar. Link ⇒ appears under Linked articles. **Refresh ⇒ still linked.** The Link button reads "Linked" for linked items. Unlink works. The archived article shows the "Archived" badge and "No longer published" without a title link. No Send/Reply control exists. **F1:** with T1 open as Alpha, archive a search result in another tab as SuperAdmin, then click Link on it ⇒ an inline error in the card, links and search reload, and the Ticket Details page stays visible. Unlink an article in a second tab, then click Unlink again in the first ⇒ inline "not linked" message, page stays visible. Reassign T1 to Bravo as SuperAdmin, then click Link as Alpha ⇒ the page switches to "This ticket is no longer available to you…" (genuine access loss via the list reload). Reassign T1 back to Alpha. As Alpha open `/tickets/<T2>` ⇒ "no longer available" message, no card. `/collaboration/<T2>` ⇒ discussion only, no KB card. As SuperAdmin on any ticket ⇒ card works.
16. **Narrow screen:** DevTools ~400 px on `/knowledge-base`, an article page with a long code block, the editor and the Ticket Details card ⇒ toolbars wrap, no horizontal page scroll, code blocks scroll inside themselves.
17. **Regression smoke (§13.12):** sign in/out for each role; customer list/detail + notes/attachments (CUST-001/002); ticket create/edit/assign/transition/escalate/activity (TICK-001/002); My Dashboard counts unchanged (DASH-001); task + reminder, quick-reply copy, collaboration post (DASH-002); SLA card and the `/sla` pages still load; the bell/notifications still work (SLA-001).
18. **OpenAPI:** `curl -s http://localhost:5080/openapi/v1.json` lists `/api/v1/knowledge-base/articles`, `/articles/{id}`, `/articles/{id}/submit|return|publish|archive|restore`, `/knowledge-base/categories`, `/categories/{id}`, `/api/v1/tickets/{ticketId}/knowledge-articles`, `/…/{articleId}`, each with "Requires role:" and enums as strings.
19. **Record honestly:** list which of steps 5–18 were run and the observed results. Anything not run (especially browser steps 11 and 14–16) is **Unverified**, not passed. Report `dotnet build`, `dotnet test`, `ng build`, `npm test`, migration status, the `marked` version installed, I1–I6, and any remaining blockers. Keep pre-existing warnings separate.

---

## Done Criteria

- [ ] One `KnowledgeArticle` concept supports FAQ, Help Article, Solution and Guide with title, summary, Markdown body, type, category, visibility, status, author, UTC timestamps and publication metadata (§5).
- [ ] SuperAdmin adds, renames, deactivates and reactivates unique categories; Agents read active ones; inactive categories stay valid on existing articles (§5).
- [ ] Lifecycle Draft → Ready for Publication → Published → Archived with return-for-revision and restore-to-Draft, defined by `KnowledgeArticleWorkflow`; no hard delete (§6).
- [ ] K1: Agents create, edit and submit only their own Drafts; only SuperAdmin returns, publishes, archives and restores (policy 403 plus handler backstop); no status or author field in any request (§4).
- [ ] K2: Internal/Public stored independently of status; Published search shows both to staff; unpublished articles are visible only to the author/SuperAdmin; archived articles only to SuperAdmin; no customer or anonymous endpoint (403 / 401) (§7).
- [ ] K3: Markdown editor with Write/Preview and help; the same safe renderer for preview and reading; raw HTML escaped, images reduced to alt text, only http/https/mailto links; source-only storage; Copy copies Markdown with feedback (§8).
- [ ] Server-side LIKE search over title, summary and body with type, category, status and mine filters, one-based paging (max 100) and order `UpdatedAtUtc DESC, Id DESC`; list rows omit the body (§9).
- [ ] K4: the Ticket Details card searches, reads and copies published articles and links/unlinks them; links persist, are unique and capped at 25; ticket access via `TicketRepository.Scope` (collaborators 404); Published-only linking; archived links kept and labelled without body; the ticket and its activity/SLA are unchanged (§10).
- [ ] Exactly one migration `AddKnowledgeBase`, reviewed; existing data counts unchanged; no existing migration edited; no seed data.
- [ ] Exactly one new backend test and one new frontend test; all existing tests pass; both builds succeed; the only new package is `marked` 18.0.14.
- [ ] `docs/knowledge-base.md` created (K1–K4, I1–I6, API, limitations); identity, API conventions, persistence, ticket docs and README updated.
- [ ] Manual API/SQL/browser matrix (Verification 5–18) recorded honestly (unrun = Unverified).
- [ ] Nothing committed or pushed until the user reviews and accepts KB-001.

**STOP HERE. Report to the user and wait for confirmation before proceeding to the next story.**
