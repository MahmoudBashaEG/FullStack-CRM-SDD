# customer-portal — plan overview

Entry point for the **customer-portal** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 12 | [12-story-customer-portal.md](12-story-customer-portal.md) | Customer Portal — Ticket Submission, Request Tracking, Customer-Safe History, Help Center & CSAT Feedback | PORTAL-001 (tracker: none) | Story 11 ([../ai-features/11-story-ai-features-gemini-assistance.md](../ai-features/11-story-ai-features-gemini-assistance.md), committed baseline), Story 10 ([../knowledge-base/10-story-knowledge-base-management.md](../knowledge-base/10-story-knowledge-base-management.md)), Story 09 ([../sla-automation/09-story-sla-basic-automation.md](../sla-automation/09-story-sla-basic-automation.md)), Stories 04–05, Stories 02–03 |

## Dependency notes

- **Story 12** (`PORTAL-001`) delivers all five portal capabilities in one story under approved decisions CP1–CP5.
  - **Identity link:** the customer is resolved on the server through `Customers.UserId`, a unique FK set only at signup or by the startup backfill. It is never matched by email.
  - **Submission:** a new `SubmitPortalTicketCommand` reuses `Ticket.Create` and the `ITicketCreationAutomation` hook (SLA snapshot plus rule-based assignment, synchronous) with one save. `CreateTicketCommand` and `ITicketRepository` are unchanged.
  - **Reads:** new read-only ports `IPortalTicketRepository` and `IHelpCenterRepository`. Customer ownership (`Tickets.CustomerId`) and `Published AND Public` are checked in SQL in every statement.
  - **Migration:** one additive migration `AddTicketFeedback` (unique `TicketId`, rating check 1–5, rowversion). No new packages.
- Shared contracts it touches (additive only):
  - New routes: `/api/v1/portal/tickets/**` and `/api/v1/portal/help-center/**`, both with the `Customer` policy.
  - `EfUnitOfWork.KnownUniqueIndexes` gets one entry.
  - Infrastructure DI gets two repositories.
  - `CrmDbContext` gets one `DbSet`.
  - Angular: the `/portal` route group, 4 Customer `NAV_ITEMS` entries, a Customer card on `HomePage`, and new text for `PUBLIC_VISIBILITY_HINT`.
  - Text only: the `KnowledgeArticleVisibility` doc comment and the `KnowledgeArticlesController` visibility description.
- Staff ticket scope (`TicketRepository.Scope`), staff KB visibility, AI-001 (staff only) and every existing test are unchanged.
- Later stories:
  - **Reports & Management** reads `TicketFeedback` for CSAT metrics. It must add its own staff read model. Story 12 exposes no staff feedback API.
  - **Communication Channels** (kept last) adds customer ↔ Agent conversations and may reuse `ITicketCreationAutomation` for channel-created tickets. The portal sends nothing.
