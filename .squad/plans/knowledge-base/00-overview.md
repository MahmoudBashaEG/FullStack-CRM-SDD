# knowledge-base — plan overview

Entry point for the **knowledge-base** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 10 | [10-story-knowledge-base-management.md](10-story-knowledge-base-management.md) | Knowledge Base Management — Articles, FAQs, Solutions, Guides, Publishing, Search & Ticket Integration | KB-001 (tracker: none) | Story 09 ([../sla-automation/09-story-sla-basic-automation.md](../sla-automation/09-story-sla-basic-automation.md)), Story 07 ([../agent-dashboard/07-story-agent-productivity-collaboration.md](../agent-dashboard/07-story-agent-productivity-collaboration.md)), Story 05 ([../ticket-management/05-story-ticket-workflow-assignment-history.md](../ticket-management/05-story-ticket-workflow-assignment-history.md)), Story 04, Story 02 |

## Dependency notes

- **Story 10** (`KB-001`) delivers the whole Knowledge Base module in one story, with business decisions K1–K4 approved in the intake: four article types, SuperAdmin categories, Draft → Ready for Publication → Published → Archived (SuperAdmin-only publishing), Internal/Public visibility with **no customer endpoint**, safe Markdown (`marked` 18.0.14), server-side search and paging, and ticket links. It adds one migration (`AddKnowledgeBase`: `KnowledgeCategories`, `KnowledgeArticles` with a `Version` rowversion, `TicketKnowledgeArticleLinks`) and changes no existing table.
- Shared contracts it touches (additive only): `CrmDbContext`, `EfUnitOfWork.KnownUniqueIndexes` (+2), Infrastructure DI (+2 repositories), `app.routes.ts`, `NAV_ITEMS`, and `TicketDetailPage` (+1 final card). Ticket access reuses `TicketRepository.Scope` unchanged. DASH-002 collaborators get no Knowledge Base access to a ticket. `TicketsController`, `TicketResponse`, ticket activity and SLA are not modified.
- Later stories: the **Customer Portal** must expose only `Published` + `Public` articles through its own contract (reuse the repository visibility predicate). The **Communication Channels** module (kept last) may later insert copied article text into outbound replies. KB-001 sends nothing.

## Implementation progress

| NN | Story | Status |
|----|-------|--------|
| 10 | KB-001 Knowledge Base Management | **Implemented, uncommitted — awaiting independent review.** Migration `AddKnowledgeBase` applied to local `CrmDev` (additive; existing row counts unchanged; no pending model changes). Backend 10/10 and frontend 10/10 unit tests (one new each). API/SQL matrix: 125 + 3 checks passed. **Browser acceptance checks (incl. F1-A/B/C, Markdown safety, empty categories, responsive UI, regression smoke): all 12 items reported passed by the user in manual testing** (not run by the implementation session). |
