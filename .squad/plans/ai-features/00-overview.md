# ai-features — plan overview

Entry point for the **ai-features** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 11 | [11-story-ai-features-gemini-assistance.md](11-story-ai-features-gemini-assistance.md) | AI Features — Gemini Ticket Summaries, Reply Drafts, Category Suggestions, KB-Grounded Solutions & Staff Chatbot | AI-001 (tracker: none) | Story 10 ([../knowledge-base/10-story-knowledge-base-management.md](../knowledge-base/10-story-knowledge-base-management.md)), Story 09 ([../sla-automation/09-story-sla-basic-automation.md](../sla-automation/09-story-sla-basic-automation.md)), Story 07, Stories 04–05, Story 02 |

## Dependency notes

- **Story 11** (`AI-001`) delivers all five AI features in one story with approved decisions AI1–AI8. It uses a single Infrastructure Gemini adapter that makes stateless `generateContent` calls, keeps the key in user secrets or `GEMINI_API_KEY`, and has no settings UI. **No migration and no persisted AI data.** It adds one NuGet package (`Microsoft.Extensions.Http`) and no npm package.
- Shared contracts it touches (additive only):
  - `TicketsController` gets a new category-only edit, `PUT /tickets/{id}/category` with `expectedCategory`. This is decision D1: the existing full `PUT` is last-write-wins.
  - `GlobalExceptionHandler` gets an `AiUnavailableException` arm.
  - The rate limiter gets a per-user `ai` policy.
  - Infrastructure DI gets the Gemini client, the options and `IAiKnowledgeSearch`.
  - Angular: `TicketsApi.changeCategory`, a final "AI assistance" card on `TicketDetailPage`, the `/ai-assistant` route and the `NAV_ITEMS` entry.
  - `ITicketRepository`, `IKnowledgeBaseRepository`, `Ticket`, SLA and KB code are unchanged.
- Ticket access reuses `TicketRepository.Scope` and is re-checked on every AI call and every chat turn. Collaborator-only Agents get nothing. Knowledge retrieval uses Published articles only.
- Later stories: the **Customer Portal** or a public chatbot needs its own contract and must not reuse the staff prompts or endpoints. **Communication Channels** (kept last) may later let staff send an edited AI draft. AI-001 sends nothing.
