# Knowledge Base (KB-001)

Staff-only module: FAQs, help articles, solutions and guides, with a publishing workflow, Markdown rendering, search and ticket links. There is **no customer or anonymous endpoint** in this story.

## 1. Decisions and interpretations

- **K1** Agents create, edit and submit only their own Drafts. Only SuperAdmin returns, publishes, archives and restores. Nothing is hard-deleted.
- **K2** `Internal`/`Public` is stored independently of status. Staff see Published articles of both visibilities. The future Customer Portal must expose only `Published` + `Public` through its own contract.
- **K3** Markdown source is the only stored form; HTML is presentation. Preview and reading use one renderer (`marked` 18.0.14).
- **K4** Staff search, read, copy, link and unlink articles from Ticket Details, using the existing ticket authorization.
- **I1** A category is required and none is seeded; until a SuperAdmin adds one the editor cannot save.
- **I2** SuperAdmin may submit any Draft (restored articles keep their Agent author). Publish is strictly `ReadyForPublication → Published`.
- **I3** Archive is allowed from Draft, Ready for Publication and Published. Restore always goes to Draft and clears publish metadata.
- **I4** Routes are module-prefixed (`/api/v1/knowledge-base/...`), a documented exception to plural top-level resources.
- **I5** Link/unlink check ticket scope with a plain query, then save once (not transactional). A reassignment in that millisecond window can still add one link; a link grants no access.
- **I6** The link list returns metadata only (never the body).

## 2. Roles

| Operation | SuperAdmin | Agent |
|---|---|---|
| Create article (always Draft, author = caller) | ✔ | ✔ |
| Read/search Published | ✔ | ✔ |
| Read own Draft / Ready for Publication | ✔ all | ✔ own (others 404) |
| Read Archived | ✔ | 404 |
| Edit Draft | ✔ any | ✔ own |
| Edit Ready / Published | ✔ (status unchanged) | 409 (own submitted) / 403 (published) |
| Submit Draft | ✔ any | ✔ own |
| Return / Publish / Archive / Restore | ✔ | 403 (policy) |
| Categories: list active / manage | ✔ / ✔ | ✔ / 403 |
| Ticket links | any ticket | only tickets currently assigned to them (collaborators get 404) |

Customers get 403 and anonymous callers 401 on every route.

## 3. Article model

Title (≤200; for an FAQ the question), optional summary (≤500), Markdown body (≤20,000; for an FAQ the answer), type (`Faq`, `HelpArticle`, `Solution`, `Guide`), required category, visibility, status, author, UTC timestamps, submit/return/publish/archive metadata. Title and body are trimmed; an empty summary is stored as `null`. Enums are stored and serialized as member names.

## 4. Lifecycle

```
Draft ──submit──▶ ReadyForPublication ──publish (SA)──▶ Published
  ▲                 │ return (SA)                         │
  └─────────────────┘                                     │
Draft / Ready / Published ──archive (SA)──▶ Archived ──restore (SA)──▶ Draft
```

`KnowledgeArticleWorkflow.IsAllowed` is the single matrix. A wrong current status returns 409. Edits never change status.

## 5. Visibility

Applied first, in SQL (`KnowledgeBaseRepository.Visible`): SuperAdmin sees everything; an Agent sees Published articles plus their own Draft/Ready articles. Anything else is 404, so existence is not revealed.

## 6. Markdown

Headings, emphasis, lists, code and fenced code, blockquotes, and `http`/`https`/`mailto` links (opened in a new tab with `noopener noreferrer`). Raw HTML is escaped to text, images are reduced to their alt text, and other links render as plain labels. Angular's `[innerHTML]` sanitizer is a second layer. Copy copies the stored Markdown.

## 7. Search

`GET /knowledge-base/articles`: `LIKE` (parameterized `Contains`) over title, summary and body; filters `type`, `categoryId`, `status`, `mine`; one-based `page`, `pageSize` (default 20, max 100); ordered `UpdatedAtUtc DESC, Id DESC`; rows omit the body. No full-text index.

## 8. Ticket integration

Scope is `TicketRepository.Scope`. Only Published, visible articles can be linked (409 otherwise); duplicates 409; at most 25 per ticket (409). Links to archived articles remain and show their status. The ticket row is never tracked, so linking cannot change status, assignment, SLA, `UpdatedAtUtc` or activity. Nothing is sent to the customer.

## 9. API

| Route | Roles | Success |
|---|---|---|
| `GET/POST /knowledge-base/articles`, `GET/PUT .../{id}` | Staff | 200 / 201 |
| `POST .../{id}/submit` | Staff | 200 |
| `POST .../{id}/return\|publish\|archive\|restore` | SuperAdmin | 200 |
| `GET /knowledge-base/categories` | Staff (`includeInactive` honoured for SuperAdmin) | 200 |
| `POST/PUT /knowledge-base/categories[/{id}]` | SuperAdmin | 201 / 200 |
| `GET/POST /tickets/{id}/knowledge-articles`, `DELETE .../{articleId}` | Staff, ticket scope | 200 / 201 / 204 |

## 10. UI routes

`/knowledge-base` (list; `?view=` deep link), `/knowledge-base/new`, `/:id`, `/:id/edit`, `/knowledge-base/categories` (SuperAdmin), plus the Knowledge Base card on Ticket Details.

## 11. Migration

`AddKnowledgeBase` is additive: three new tables (`KnowledgeCategories`, `KnowledgeArticles` with `Version rowversion`, `TicketKnowledgeArticleLinks`), their indexes and `Restrict` FKs. No existing table changes and nothing is seeded.

## 12. Limitations

No revision history; edits to Published articles apply immediately; the I5 race window; no relative links or images; a category is required; body search scans `nvarchar(max)`.
