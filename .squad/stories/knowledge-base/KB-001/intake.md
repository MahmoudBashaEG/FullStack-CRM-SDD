# Story 10 — Knowledge Base Management — Business Feature Intake

## Feature

- **Display name:** Knowledge Base — FAQs, Help Articles, Solutions, Guides & Search
- **Feature slug:** `knowledge-base`
- **Work item ID:** `KB-001`
- **Work item type:** `Business Feature Story`
- **Story number:** `10` (verify against the repository's Squad-Kit index)
- **Tracker:** `none` (local training project)
- **Status:** `Business decisions K1–K4 approved — ready for Squad-Kit planning`
- **Labels:** `knowledge-base, articles, faq, solutions, guides, markdown, publishing, visibility, search, ticket-links, roles, aspnet-core, angular, squad-kit, training`

## Title

Knowledge Base Management — Articles, FAQs, Solutions, Guides, Publishing, Search & Ticket Integration

## 1. Business goal and source

Implement the **Knowledge Base** module in the existing Customer Support CRM. Support Agents need a reliable place to record recurring solutions, find troubleshooting instructions, and reuse knowledge when handling tickets. SuperAdmin controls publication quality. The original two-page *Customer Support CRM — Core Features* document requires under Knowledge Base: **FAQs, help articles, solutions and guides, and search**. All four requirements must be functional in Story 10.

The user additionally approved: **K1** Agents draft/submit while only SuperAdmin publishes; **K2** Internal and Public visibility with customer access deferred to the future Customer Portal; **K3** a basic Markdown editor with formatted preview; **K4** search, read, copy and link published articles to authorized tickets. These four decisions are **final business approvals**, not questions to reopen during planning.

**Training scope:** deliver the full basic business workflow with understandable .NET/Angular code. Do not substitute placeholders for search, publication, article editing, Markdown preview, or ticket linking; equally, do not build enterprise content-management infrastructure.

### Required end-to-end capabilities

1. Create and maintain knowledge articles of four types: **FAQ**, **Help Article**, **Solution**, **Guide**.
2. Basic **categories** to organize articles, with simple SuperAdmin category maintenance.
3. A usable **article lifecycle**: Draft → Ready for Publication → Published → Archived, including a simple path to return a submitted article for revision.
4. **Role-based authorship and publication**: Agents create/modify their own drafts and submit them; SuperAdmin reviews and publishes.
5. **Internal/Public visibility**, retained on the article and respected by all reads; customer-facing exposure is reserved for the future Portal.
6. A basic **Markdown editor and safe formatted preview** for articles.
7. Server-side **search, filters, and paging** over authorized Knowledge Base content.
8. **Ticket Details integration**: search/read/copy a published article and link/unlink relevant articles to an existing authorized ticket, with persistent associations.
9. Angular pages and documented staff APIs for these flows.

## 2. Prerequisites and integration boundaries

- Build inside the **existing** monorepo and inspect the committed implementations of `ARCH-001`, `AUTH-001`, `CUST-001/CUST-002`, `TICK-001/TICK-002`, `DASH-001/DASH-002`, and `SLA-001`. Do not create another solution, authorization model, ticket service, or notification system.
- Backend: .NET 10, the existing `Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api` Clean Architecture projects, source-generated Mediator/CQRS, FluentValidation, EF Core and SQL Server LocalDB, Identity/JWT, and `/api/v1/` conventions.
- Frontend: existing Angular standalone feature organization, Angular Material, shared components, auth/role guards, typed API access, and responsive design.
- Use the **existing Customer, Ticket, Identity Agent/SuperAdmin IDs**, and current ticket categories/permissions where suitable. Don't guess code names, DTOs, action signatures, migrations or router paths: verify them in the repository before planning.
- Preserve TICK-002's **ticket scope**: SuperAdmin may manage any ticket; an Agent may work with their **currently assigned tickets** through normal Ticket Details. An invited DASH-002 collaborator is not thereby authorized to open/manage Ticket Details. KB article access never confers ticket access.
- Preserve SLA-001 state, ticket workflow/history, existing notifications, and all earlier module behavior. **No external customer communication** occurs in this story; that module remains last on the roadmap.
- Confirm Story 09 is committed and the working tree has no unrelated application changes before implementation. Do not assume an exact commit ID.

## 3. Approved business decisions — K1–K4

| ID | Approved decision | Required consequence |
| --- | --- | --- |
| **K1 — Publishing authority** | **Option A:** Agents create and submit drafts; only **SuperAdmin** can publish. | No Agent publish endpoint or UI permission. SuperAdmin controls publishing and archiving. |
| **K2 — Visibility** | **Option A:** Support **Internal** and **Public** articles. Public articles become customer-accessible **later via the Customer Portal**. | Visibility is stored now. **Story 10 provides no customer/public anonymous KB endpoint**. Drafts and submissions remain staff restricted, even if marked Public. |
| **K3 — Editor** | **Option B:** Markdown editor. | Agents and SuperAdmin can enter Markdown; display an escaped/safely rendered preview supporting common text formatting. No complex rich-text editor. |
| **K4 — Ticket integration** | **Option B:** Search, read, copy, and **link Knowledge Base articles to tickets**. | Persist ticket/article associations; permit only published articles to be linked/used; enforce existing ticket authorization server-side; no outbound messaging or automatic status changes. |

**Approval gate passed:** K1–K4 are confirmed. The operational defaults below express a simple implementation of those decisions; they are not additional choices falsely attributed to the user. If the actual code reveals a material conflict, surface it during planning.

## 4. Roles and authorization

| Operation | SuperAdmin | Agent | Customer |
| --- | --- | --- | --- |
| Create article as Draft | Yes | Yes | No |
| View/edit own Draft | Yes | Yes | No |
| View/edit any Agent Draft | Yes | No | No |
| Submit own Draft for publication | Yes | Yes | No |
| View own Ready for Publication article | Yes | Yes | No |
| Review all submitted articles | Yes | No | No |
| Return submitted article to Draft for revision | Yes | No | No |
| Publish an article | **Yes, only SuperAdmin** | No | No |
| Change published content/visibility | Yes | No | No |
| Archive or restore articles | Yes | No | No |
| List/search/read all Published articles, Internal or Public | Yes | Yes | No new access |
| List/search own unpublished articles | All | Own only | No |
| Create/manage KB categories | Yes | Read/use active categories | No |
| Search/read/copy published articles from authorized Ticket Details | Yes | On own assigned tickets | No |
| Link/unlink published articles to tickets | Yes, any ticket | Only own currently assigned tickets | No |
| View linked articles on a ticket | Subject to existing ticket access | Subject to existing ticket access | No new access |

- Identity (`sub`) from the authenticated backend principal determines article creator/owner and acting staff member. Do not accept spoofed authors, publishers, or authorization flags from request bodies.
- Check permissions **in the API/application**, not only through Angular navigation or disabled buttons. For records an Agent does not own or tickets they cannot access, follow established repository 404/403 conventions without revealing protected content.
- Customers are not granted public-article access during Story 10. Public is a **future visibility classification**, not a request to implement Customer Portal endpoints now.
- `Ready for Publication` means a submitted article awaiting SuperAdmin action, **not** a published article. An Agent cannot bypass approval by changing `Status` in a generic edit payload.

## 5. Knowledge articles: content and article types

- Use **one Knowledge Article concept** with an `ArticleType` covering **FAQ**, **HelpArticle**, **Solution**, and **Guide**, rather than four duplicated CRUD systems.
- For each article store: stable generated ID; title; short summary/description; Markdown body; article type; category; visibility (`Internal` or `Public`); lifecycle status; creator/author Identity ID; created and updated UTC timestamps; and publication metadata (published-at and publishing SuperAdmin ID where applicable).
- Suggested simple limits (planner may match existing conventions): title required/trimmed, max ~200 characters; summary optional, max ~500; Markdown body required/trimmed, bounded around 20,000 characters. Empty/whitespace-only titles and bodies are invalid. Avoid duplicate articles on each edit; update the same article.
- Use the article's **Title** as the FAQ question and its Markdown body as the answer; Guides use Markdown headings/numbered steps; Solutions and Help Articles use the same editor and renderer. Distinct templates/wizards per article type are unnecessary.
- Category is a simple label/grouping used in lists and filters; **SuperAdmin** can add, rename, and disable/archive categories, keeping previously categorized articles valid. Category names should be unique within the chosen matching semantics. No hierarchical taxonomy or approval process for categories.
- Basic content metadata (author, timestamps, type, category, visibility, status) is shown in article details. No analytics, popularity ranking, separate revision-history table, media library or article comments required.

## 6. Article lifecycle and business behavior

**Lifecycle:** `Draft` → `ReadyForPublication` → `Published` → `Archived`.

- **Create:** An Agent or SuperAdmin creates an article in `Draft`. Author/creator ID and creation time are server-generated.
- **Edit:** An Agent may edit **only their own Draft**. SuperAdmin may edit Drafts, submissions and Published articles. Editing does not create a new article ID; store the updated-at time and staff actor where practical.
- **Submit:** The author may change their Draft to `ReadyForPublication`. This makes it available in SuperAdmin's review list, but not in normal published search.
- **Review / return:** SuperAdmin can review submissions and return an article to `Draft` for revisions, without adding a complicated multistage approval engine. An Agent may then edit and resubmit their own returned Draft. A short optional return reason is a small UX convenience, not a separate approval system.
- **Publish:** **Only SuperAdmin** can move an article to `Published`; record the publisher and publish time. It then appears in published search for staff, whether Internal or Public. An Agent cannot publish via direct API calls or by editing status.
- **Archive:** Only SuperAdmin can archive an article. Archived content is removed from normal published search and cannot be newly linked to tickets.
- **Restore:** For simplicity, SuperAdmin can restore an Archived article to Draft, then publish it again after review. Do not hard-delete articles as part of this story.
- **Existing ticket links:** Archiving an article must **not** delete its past ticket-link records. On a previously linked ticket, show a clear `Archived` reference without exposing archived article content to Agents through the published-article API. SuperAdmin can inspect it from management. If the article is subsequently republished, its link still points to the same article ID.
- For this training story, published edits by SuperAdmin take effect directly without multi-version approval or content snapshots; no speculative version/concurrency framework is required beyond existing app conventions.

## 7. Internal/Public visibility and future Portal boundary (K2)

- `Internal`: staff knowledge only; never included in future public customer-facing reads.
- `Public`: still visible to staff now; **eligible** for a future Customer Portal only if `Published` when that module is implemented.
- An unpublished Public article must **never** be exposed to Customer/anonymous users. Status and visibility are separate dimensions.
- The public-facing API, customer-facing search, SEO, public article pages and customer feedback belong to the future **Customer Portal** story. Do not create a partial public endpoint and call the Portal complete.
- Keep visibility checks in backend queries/contracts so future Portal work can safely reuse them without trusting the UI.

## 8. Markdown editor and safe preview (K3)

- Article create/edit screen has a Markdown input area, basic help/examples and a **Preview** mode/area that displays formatted content.
- Support common Markdown: headings, paragraphs, bold/italic, ordered/unordered lists, inline code, code blocks, and links. Use the same safe rendering in article reading and preview.
- Treat article text as untrusted even from authenticated staff. **Do not render arbitrary author-provided HTML** or unsafe scriptable links. Use a suitable safe Markdown-rendering strategy aligned with Angular security practices; adding one small maintained library is acceptable if needed for this feature, but no large WYSIWYG framework.
- No arbitrary embedded HTML, JavaScript, iframe, external widgets, embedded images or attachment upload feature is needed; external hyperlinks must use safe protocols.
- Persist Markdown **source**; rendered HTML is presentation only. Copying an article copies its saved plain-text Markdown content to clipboard, with clear success/failure feedback. This is **copy-only**, not customer messaging.

## 9. Search, browse, filters and article details

- Provide a staff Knowledge Base page within existing navigation, with server-paged article list/search and clear article type, category, and visibility/status indicators.
- **Published browse/search:** both SuperAdmin and Agent may search across Internal and Public Published articles. Search article **title, summary and Markdown content** with a straightforward SQL-backed keyword search (`LIKE` or the existing pattern) and filter by article type and category. No Elasticsearch, vector database or advanced indexing engine.
- **My Drafts / My Submissions:** an Agent can find their own Draft and ReadyForPublication articles, without discovering another Agent's unpublished content. SuperAdmin has all-status lists and a `Ready for Publication` review queue, plus archive visibility.
- Support simple one-based paging, an existing reasonable page-size cap, and deterministic ordering (e.g., most recently updated/published first with ID tie-breaker). Avoid loading all articles into the browser to filter them there.
- Article Details displays correctly formatted Markdown and relevant safe metadata; draft/article editing actions appear only when authorized. Refreshing an edit/search page must not widen permissions.
- Empty results, validation errors, loading states, access-denied and retry behavior should match the existing Angular UX.

## 10. Ticket integration (K4)

An Agent handling a ticket should be able to find and reuse an existing published solution without leaving the normal authorized Ticket Details workflow.

- Add a **Knowledge Base** section/card to the existing **Ticket Details** page for current authorized viewers. Reuse the existing Ticket Details routing and permission checks; do not create a collaboration-only backdoor to ticket details.
- Inside that section, staff may search **Published** articles (Internal and Public), filter or open details, and **copy** article Markdown source to the clipboard. There is **no Send/Reply action** and no customer-facing message.
- Staff authorized to manage the ticket may **Link Article to Ticket**. Persist a ticket↔article association using the existing stable Ticket ID and Article ID. Multiple relevant published articles may be linked to a ticket; each article/ticket pair appears at most once.
- Display linked articles with title/type/category and a link to the article when accessible. Also allow authorized staff to **Unlink** an accidental/obsolete association; unlinking does not delete the article or ticket. A link does not change ticket status, assignment, escalation, SLA, `UpdatedAtUtc`, or create a misleading ticket workflow event.
- Agents may link/unlink only on their currently assigned tickets; SuperAdmin may do so on any ticket. Backend verifies this per request using **the existing ticket authorization boundary**. A DASH-002 invited collaborator must not gain access to this section via normal Ticket Details just from collaboration membership.
- New links can be made **only to Published articles**. Old links to archived articles remain recorded for traceability and are labeled Archived; normal Agent search must not expose archived content through those links.
- No AI-generated recommendations, article auto-suggestions based on ticket text, automatic replies, sending, ticket resolution automation, or synchronization to external communication providers.

## 11. Backend, persistence, migration and API boundaries

- Extend the existing Clean Architecture and CQRS/Mediator/validation stack with feature-focused Knowledge Base use cases: category maintenance; create/update article; submit/return/publish/archive/restore; authorized article queries/search; ticket article link/list/unlink. These are behavioral requirements, not mandatory class names.
- Use EF Core SQL Server and a **reviewed additive migration** for the minimum data needed (e.g., categories, articles, ticket-article links). Preserve existing `Customers`, `Tickets`, user accounts, SLA data, dashboard and previous migrations; do not reset `CrmDev` or seed fake knowledge content in migrations.
- Foreign keys use existing Identity/Ticket IDs where applicable; maintain unique ticket/article association and deterministic indexable paging. Existing Ticket and Identity tables should not be duplicated.
- Use server-derived actor/author IDs and UTC timestamps. Status, ownership, and publish transitions must be validated in the backend. Prevent crafted request bodies from bypassing K1/K2.
- Follow established `/api/v1/` REST conventions with typed request/response DTOs, one-based paging, proper 400/401/403/404/409 handling, consistent `ProblemDetails`, and OpenAPI descriptions. Suggested endpoint family: `/api/v1/knowledge-base/articles`, `/api/v1/knowledge-base/categories`, and nested ticket links under existing `/api/v1/tickets/{id}/...`. **Planner must check the real code before choosing exact paths or command shapes.**
- Reuse existing Unit of Work and standard EF transactions when a write truly touches multiple entities. No separate broker, workflow engine, new background worker or concurrency-retry framework is justified merely for Knowledge Base CRUD and ticket links.
- Do not alter SLA deadlines, auto-assignment, ticket activity history, quick replies, customer notes or reminders as side effects of KB reads and links.

## 12. Angular UI requirements

- Add a protected **Knowledge Base** navigation entry visible to Agent and SuperAdmin, using existing shell/guards and Angular Material styling.
- Staff KB workspace: searchable/paginated article list, article type/category filters, article details, draft editor, Markdown preview, article status and visibility; Agent's own Draft/Submitted view; SuperAdmin review/publish/archive/restore controls and basic category management.
- Article form: title, summary, type, category, visibility and Markdown body; sensible inline validation and readable error feedback. Use the existing theme and responsive layouts.
- Add Knowledge Base card/section on authorized Ticket Details: search published articles, view/copy content, link/unlink, and show linked articles with an understandable archived state.
- No public navigation or Customer KB page in this story. Customer self-profile and future Customer Portal are unchanged.
- Keep customer names, ticket descriptions, and other sensitive CRM content out of KB article API responses unless it is part of the article's intentionally authored content; linking must not grant broader ticket or article read privileges.

## 13. Manual acceptance and testing policy

- Exactly **one new backend happy-path unit test** and **one new frontend happy-path unit test**, using the existing frameworks. Choose two meaningful feature paths rather than adding large automated suites. Continue running existing tests.
- No Playwright/E2E, automated API/SQL integration suites, extra testing frameworks, or exhaustive negative tests unless explicitly requested.
- Build backend and frontend; apply and inspect the additive migration against development SQL Server without destroying previous data. Verify key flows manually in the API, SQL and browser; report actual passes versus unverified items transparently.
- Required manual checks:
  1. SuperAdmin and Agent can create their own Draft; required-field and Markdown length validation work.
  2. Agent can edit **own Draft** and submit; Agent cannot edit another Agent's unpublished article or publish via direct API or UI.
  3. SuperAdmin sees submitted articles, can return one to Draft, edit/review, and publish it; author/publisher/time metadata are correct.
  4. An Internal and a Public Published article are searchable to staff; a Draft or Submitted article is **not** shown in published search, regardless of visibility.
  5. Customer/anonymous cannot access the Story 10 staff Knowledge Base endpoints; future Public eligibility does not leak articles now.
  6. Article types FAQ, Help Article, Solution, and Guide can be created, browsed and filtered; category maintenance and filtering work.
  7. Markdown preview and reader render headings, ordered steps and safe links correctly; unsafe raw HTML/script content is not executed.
  8. Search matches title, summary and body; paging/filtering/empty results are correct and enforced by server.
  9. On an Agent's assigned ticket, search/open/copy a published article; add a ticket link and refresh the page to confirm persistence; prevent duplicate links; unlink without deleting either record.
  10. SuperAdmin can manage links on any ticket; an Agent cannot link to another Agent's ticket, including via direct API; collaborator-only access doesn't bypass restrictions.
  11. Archive a linked article: old link remains, article no longer appears in normal published search, and Agents cannot read archived body via the link; restore/republication works without changing the link ID.
  12. Ticket status, assignment, activity/SLA state, and other Story 01–09 functionality are unchanged by linking/unlinking articles. Check quick replies and notifications at a basic smoke-test level.
  13. Browser: KB list/details/editor/preview, SuperAdmin review/publish, Ticket Details KB card, clipboard feedback, errors/retry and narrow-screen layout.
- Report backend/frontend builds and all unit results, migration details, API/SQL/browser checks, any old warnings and all limitations. Do not claim a browser test passed if it was not run.
- **Do not commit or push** until implementation has been independently reviewed, user confirms acceptance, and any blockers are resolved.

## 14. Out of scope

- Customer Portal implementation, anonymous/customer article endpoints, customer feedback and ratings. Only the **Public/Internal metadata and correct staff-only enforcement** are added now.
- Email, WhatsApp, SMS, Live Chat, Web Forms, actual customer conversations, using copied text to send messages, or changing the deferred Communication Channels roadmap.
- AI-generated article summaries/answers, chatbot, semantic/vector retrieval, knowledge recommendations based on ticket content or automatic categorization.
- Full CMS/revision-history systems, article version diffing, coauthor approval chains, rich-text/WYSIWYG suites, arbitrary embedded HTML, uploads/media hosting, comments, likes, personalized feeds or complex article analytics.
- External search clusters, Elasticsearch, full-text infrastructure migration, dedicated storage services, distributed transactions, queues, new background workers or speculative concurrency infrastructure.
- Ticket permissions changes for invited collaborators, branch/department-specific publication, custom roles, advanced taxonomy trees, article localization and public SEO.
- Extra automated integration/E2E/browser/performance test suites beyond the team's agreed lightweight policy.

## 15. Dependencies, planning handoff and delivery gates

- **Story:** `KB-001` (Story 10), first story in `knowledge-base`.
- **Depends on:** completed/committed stories `AUTH-001` for role/identity; `TICK-001` and `TICK-002` for ticket linking, visibility and ticket details; `DASH-002` for existing Quick Replies/Collaboration boundaries; and the architecture/Angular shell. `SLA-001` must remain unaffected. Verify baseline at the actual current commit before implementation.
- **Future:** The Customer Portal should expose **only `Published` + `Public` articles** through its own authorized/public contracts. The Communication Channels module remains last.
- **Recommended sequence for the Squad-Kit plan:** verify repository baseline → domain/models + approval rules → persistence/additive migration → article CRUD/workflow/category queries → safe Markdown Angular UI → server-side search/paging → Ticket Details link flow → tests/docs → manual API/SQL/browser verification.
- **Suggested plan location:** `.squad/plans/knowledge-base/10-story-knowledge-base-management.md` (the planner determines actual filename and updates `.squad/plans/knowledge-base/00-overview.md` and `.squad/plans/00-index.md`).
- **Planning only at intake stage:** no application changes, migrations or commits. Review generated plan independently before implementing it.

## Attachments and source note

- Original **Customer Support CRM — Core Features**, 2 pages, previously provided: Module 6 lists *FAQs, Help articles, Solutions and guides, Search*. The original high-level source does **not** specify approval steps, Markdown, visibility, category management or ticket-link workflows; those details come from user-approved K1–K4 and the training-scope operational defaults documented here.
- If the original PDF is not in the repository, the **approved intake** is the authoritative planning input; don't claim the PDF was inspected locally when it wasn't.

## Notes for the Squad-Kit planner

This intake defines **what must work**, with **K1–K4 explicitly approved**. Inspect the real code for type names, roles, repository conventions, current ticket scope, Angular routes and test patterns before prescribing implementation paths. Surface material conflicts rather than widening permissions or omitting approved workflows. Keep solutions simple and complete for a training CRM, with **one backend and one frontend happy-path unit test** and robust manual verification. Do not write implementation code, create migrations or commit while only generating the plan.
