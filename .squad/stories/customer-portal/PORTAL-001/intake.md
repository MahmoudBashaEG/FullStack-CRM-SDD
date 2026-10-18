# Story 12 — Customer Portal — Business Feature Intake

## Feature

- **Display name:** Customer Portal — Ticket Submission, Tracking, Customer-Safe History, Help Center & Feedback
- **Feature slug:** `customer-portal`
- **Work item ID:** `PORTAL-001`
- **Work item type:** `Business Feature Story`
- **Story number:** `12` (verify against the repository's Squad-Kit index)
- **Tracker:** `none` (local training/assessment CRM)
- **Status:** `Business decisions CP1–CP5 approved — ready for Squad-Kit planning`
- **Labels:** `customer-portal, self-service, customer-tickets, tracking, status-timeline, public-knowledge-base, faq, csat, feedback, security, aspnet-core, angular, squad-kit, training`

## Title

Customer Portal — Registered-Customer Ticket Submission, Request Tracking, Safe Status History, Public Help Center and Satisfaction Feedback

## 1. Business goal and authoritative requirements

Implement **all five** Customer Portal capabilities from the original two-page *Customer Support CRM — Core Features* document: **Submit tickets**, **Track requests**, **View history**, **Access FAQs**, **Submit feedback**. Deliver working customer-facing UI and authorized APIs inside the existing application, not a separate CRM or a mock portal. The user has explicitly approved **CP1–CP5** in §3.

This is a **training/assessment project**: implement complete basic workflows, straightforward .NET/Angular code, clear backend authorization, and focused manual testing. Do not substitute UI-only placeholders for any of the five requirements, and do not create elaborate enterprise identity verification, event infrastructure, multichannel messaging, reporting analytics, or extensive test suites.

**Required end-to-end capabilities:**

1. Signed-in, linked **Customer** accounts can enter a Customer Portal; unlinked/contact-only profiles cannot claim another profile.
2. Customers can submit a ticket with subject, description and category; the system derives customer identity, opens the ticket with Medium priority, and preserves current SLA and automatic-assignment behavior.
3. Customers can list/search or browse their own current/previous tickets, view safe ticket details, and follow a **customer-safe creation/status timeline**.
4. Customers can browse/search/filter/read **Published + Public** Knowledge Base articles, including FAQs, help articles, solutions and guides, using the existing safe Markdown renderer.
5. Customers can submit and later edit **one CSAT feedback record per own Resolved/Closed ticket**, with rating 1–5 and optional comment; feedback remains available to future Reports & Management.

**Explicit boundaries:** no customer-to-Agent conversation, chat widget, message sending, ticket comments/replies, WhatsApp/email/SMS, staff-only ticket controls, customer-facing Gemini, or automatic AI requests. The **Communication Channels module remains the final module** on the roadmap.

## 2. Existing app and dependencies

- Build **in place** on Stories **01–11**, including .NET 10 ASP.NET Core Clean Architecture (`Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`), source-generated Mediator/CQRS, FluentValidation, EF Core SQL Server LocalDB, Identity/JWT, versioned `/api/v1/` endpoints, Angular 22 standalone/Angular Material, existing typed API services and role guards.
- **Pre-implementation gate:** confirm **Story 11 `AI-001` is committed** and the working tree has no unexpected application changes. At intake creation, the user's last report approved the Story 11 commit but did **not** confirm that commit was actually made. Verify with `git status` and `git log`; do not assume a commit hash. No implementation should be layered on an unresolved/dirty baseline.
- Reuse the existing registered Customer↔CRM Customer profile mapping from `AUTH-001`/`CUST-001`. **Identity User ID and CRM Customer ID are different concepts.** Inspect the code to find the authoritative linkage before designing query handlers; do not match or claim profiles solely by an unverified email address.
- Reuse existing ticket creation, activity, category and priority values (`General`, `Technical`, `Billing`, `Other`; Medium priority); ticket status (`Open`, `InProgress`, `Resolved`, `Closed`), customer relationship, SLA snapshot, automation and assignment flows. Customer submission must not bypass existing ticket-created business behavior.
- Reuse existing KB `Published` lifecycle and `Public` visibility semantics, article categories/types, safe Markdown rendering, article IDs and search conventions. The current staff KB read contracts expose **Internal** articles to staff; **do not reuse those contracts without adding explicit customer-safe server-side visibility filtering**.
- Preserve TICK-002 staff boundaries: SuperAdmin all tickets, Agent only currently assigned tickets, collaborator-only Agent has no normal ticket access. **Customer ownership is a separate customer-only scope** and must not be mixed into or weaken staff authorization.
- Reuse or extend application migrations **additively** for the minimum feedback persistence (and only other changes truly required by verified source code). Do not reset `CrmDev`, edit already-applied migrations, or duplicate existing Ticket/Customer/Article tables.
- Existing staff AI assistance remains staff-only. The Portal may not call Gemini or expose AI-generated staff drafts, suggestions, internal prompts or chat history.

## 3. Approved business decisions — CP1–CP5

All rows are **explicitly approved business decisions**. Do not reopen during planning without discovering a material incompatibility.

| ID | Approved business choice | Binding requirements |
| --- | --- | --- |
| **CP1 — Access** | **Option A: Registered Customers only.** | Portal requires authenticated `Customer` Identity account **linked to its own CRM customer profile**. No contact-only profile claiming, anonymous ticket access or alternative customer-login system. |
| **CP2 — Submission** | **Option A: Simple form.** | Customer enters **subject, description, category** only. Backend derives linked Customer ID; new ticket starts `Open`, `Medium`; preserve existing SLA/automatic assignment logic. No customer control of Agent, priority, status, escalation or SLA. |
| **CP3 — Tracking/history** | **Option A: Customer-safe timeline.** | List/detail previous/current tickets plus **ticket creation and status transitions**, including reopening. Hide internal activities, assignment, escalation, notes, staff identities and SLA details. |
| **CP4 — Help Center** | **Option A: Customer Help Center.** | Authenticated Customer browse/search/filter/read **only `Published` + `Public`** articles; safe Markdown; filters for type/category. **No automatic related-article suggestions** on submission. |
| **CP5 — Feedback** | **Option A: Basic ticket CSAT.** | For own **`Resolved` or `Closed`** ticket, submit 1–5 star rating plus optional comment; **one editable feedback record per ticket**; retain for future reporting. |

**Approval gate:** CP1–CP5 complete. Later implementation defaults are suggestions for a simple training workflow, **not additional decisions falsely attributed to the user**. The planner must inspect existing code and surface genuine conflicts.

## 4. Roles, identity resolution, record ownership

| Operation | Customer | Agent | SuperAdmin | Anonymous |
| --- | --- | --- | --- | --- |
| Open customer portal | Linked account only | No | No as Customer | No |
| Submit customer ticket | For own linked CRM profile | Existing staff flow only | Existing staff flow only | No |
| List/read customer ticket details | Own CustomerId tickets only | Existing staff scope; **not through customer contracts** | Existing staff scope; **not through customer contracts** | No |
| Read customer-safe ticket timeline | Own tickets only | Existing staff history | Existing staff history | No |
| View customer Help Center | `Published` + `Public` | Existing KB staff view | Existing KB staff view | No new anonymous access |
| Submit/edit CSAT feedback | Own eligible ticket only | No new customer feedback mutation | No new customer feedback mutation | No |
| View another customer's records | **Never** | Existing ticket rules apply separately | Existing staff rules apply separately | Never |

- Establish the CRM Customer ID from the **authenticated principal's validated server-side profile relationship**; never accept `customerId`, `userId`, role or ownership flags supplied by the browser for Customer-scoped operations. For any unlinked Customer account, return a consistent safe denial/clear setup message, **not** another customer's information or an automatic email-based claim.
- Filter ticket list/paging **at the database/query layer** using the resolved linked CRM Customer ID; GET by ticket ID, timeline, feedback read/write all independently re-check ownership. An ID guessed from another customer must not reveal existence or contents (use existing 404/403 conventions consistently).
- Do not expose staff DTOs to Customers merely because they own the ticket. Build minimal customer-specific response projections/DTOs without `AssignedAgentUserId`, escalation reasons, SLA deadline/alerts, internal notes, other Customers, staff activity payloads, or employee names.
- Customer-role API authorization is enforced **on the backend** even if Angular route guards conceal staff screens. Staff-only CRUD, workflow, notifications, articles marked Internal, AI and admin endpoints remain forbidden to Customer.
- No anonymous customer-facing endpoints are added in this story, including public KB reads: **Public article visibility ≠ anonymous route** under CP1/CP4.

## 5. Customer Portal shell and navigation

- Provide a Customer-only landing/portal area in the existing Angular app with simple accessible navigation: **My Tickets**, **New Ticket**, **Help Center**; feedback is reached from eligible Ticket Details. Reuse login, JWT session, existing logout handling, global styling and responsive navigation. No second Angular app or new design system.
- Portal summary/landing may show a modest **My Tickets** preview or status counts drawn only from the customer's own tickets; complex customer dashboard analytics are unnecessary.
- Staff navigation/role checks, staff Ticket Details and Knowledge Base editing remain unchanged. Don't add Customer permission to existing staff screens merely to simplify routing.
- Loading, empty, validation, access denied, save-success and retry states should follow existing Angular Material patterns. Support narrow-width/mobile layouts without redesigning the entire app.

## 6. Customer ticket submission (CP2)

- Show a customer-specific **New Support Request** form: **Subject** (required), **Description** (required), **Category** (required, one of the current predefined Ticket categories). Use established limits and FluentValidation behavior, trimming and safe text display. Provide submit button, progress state and clear validation/API feedback; prevent accidental duplicate submission while pending.
- Server automatically supplies CRM `CustomerId` from the logged-in Customer's linked profile, assigns **`Open` status and `Medium` priority**, and sets existing server-generated creator/timestamps. Ignore or reject forged category/priority/Agent/status/customer fields; keep the request DTO restricted to these three fields.
- Follow the current ticket creation pathway so a new customer ticket receives the same applicable **SLA snapshot, ticket-created activity/history, and configured background automatic assignment/notification rules** as other eligible tickets. If existing creation assumes StaffActor, add the smallest safe customer submission entry point/reuse of core creation logic; do not invent a fake Agent or SuperAdmin actor, grant staff permissions to Customer, or duplicate ticket-creation business logic.
- **Automatic assignment might run asynchronously** under the existing SLA/Automation background service; do not promise an Agent is assigned instantly. If no existing rule matches, leave the ticket unassigned for staff follow-up, as current rules permit.
- Submission returns the new ticket identifier and customer-safe result; navigate to its Customer Ticket Details or confirmation. A customer's new ticket must appear in **My Tickets** even if unassigned or later reassigned to different Agents.
- Do not send an actual email/SMS/WhatsApp, generate a Gemini reply, create a customer conversation, or record an SLA first response. On-screen confirmation is sufficient.

## 7. My Tickets — tracking and customer-safe details (CP3)

- List **all tickets associated with the authenticated customer's CRM Customer ID**, including those originally created by staff for that Customer and historical `Resolved`/`Closed` tickets. The user said **their own tickets and previous requests**, not only records whose `CreatedByUserId` is Customer.
- Basic list: Subject, Category, current Status, Created date, useful ticket reference/ID. Show search/filter by subject and/or status if consistent with existing simple server paging; one-based bounded paging and stable newest-first ordering. No staff assignee or internal escalation filter displayed.
- Customer Ticket Details: original/current subject and description, category, status, date submitted/created, and the **customer-safe status timeline** below. No staff editing, workflow transitions, priority selectors, assignment, escalation, SLA admin controls or internal notes.
- A customer **tracks** requests; they do not edit/close/reopen/escalate tickets under Story 12. A future communication workflow may allow adding messages, but **not here**.
- Validate ownership on each list/detail query, never by fetching broad staff results and filtering them in Angular.

## 8. Customer-safe status timeline (CP3)

- Reuse **existing immutable Ticket Activity** where possible but **project only safe events**: ticket submission/creation and valid `StatusChanged` transitions (`Open`, `InProgress`, `Resolved`, `Closed`, including `Closed` → `Open`). Display safe event label, resulting status, UTC event time converted by UI, and a simple explanatory phrase with **no internal reason/details/actor identity**.
- Keep **assignment and reassignment**, escalation/clearance, category/priority/description edits, internal notes/attachments, SLA/automation notifications, first-response signals, staff identity, and raw audit/event payloads **entirely out of the Customer response**. Do not rely only on Angular hiding fields.
- **Legacy tickets:** TICK-002 did not fabricate historical `Created` events for pre-tracking tickets. The portal can show a ticket-created entry based on its authoritative `CreatedAtUtc` metadata (clearly labeled ticket submission/creation), rather than claiming a recorded event exists or inventing past intermediate transitions. Show actual status-change events only when recorded. Avoid a duplicate creation entry if a real creation event also exists.
- Timeline may use a simple paged/bounded read consistent with existing history access patterns. Sort chronologically for display with stable tie-breaks; display only recorded facts. A reopened ticket displays its subsequent status transitions appropriately.
- Refreshing, changing ticket ID, or navigating backward must never display another customer's timeline or cached internal activity.

## 9. Customer Help Center (CP4)

- Add a Customer-only **Help Center** section that displays existing four knowledge article types: **FAQ**, **Help Article**, **Solution**, **Guide**. Reuse established Knowledge Article storage, article/category relationships, and staff-created content; do not copy articles into a new customer table.
- **Every customer-facing list/search/detail query MUST enforce `Status == Published && Visibility == Public` on the backend**, including requests for an article by guessed ID. SuperAdmin/Agent can still use their existing staff KB views; adding customer reads does not relax staff publication rules.
- Server-backed keyword search over title/summary/body using the repository's established straightforward SQL strategy, plus filters for **article type and category**, reasonable paging, stable ordering, empty state and loading/error UX. If category labels are included, expose only the safe names required for the public Help Center rather than internal management metadata.
- Article detail shows title, type, category, summary, safe rendered Markdown body and suitable published metadata. **Reuse the Story 10 safe Markdown renderer**; treat even published article content as untrusted and do not allow scriptable HTML/unsafe links.
- An article moved from Published to Archived or changed from Public to Internal becomes inaccessible to customers on the next backend request, even if they retained its URL. Unpublished `Public` articles remain inaccessible.
- No guest/anonymous Help Center, no search suggestions during ticket submission, no Gemini recommendations, no KB editing/publishing or customer comments/ratings on articles.

## 10. Ticket CSAT feedback (CP5)

- On **Customer Ticket Details**, show feedback entry only when the owned ticket currently has status **`Resolved` or `Closed`**. Include **one required integer rating from 1 through 5 stars**, plus an **optional short text comment** (suggested max 1,000 characters, subject to existing field conventions). Show clear Submit/Save success and inline validation.
- **One durable feedback record per Ticket** (and therefore per owning Customer under the single-customer Ticket model). An eligible Customer can **edit the existing record** rather than adding another. Backend checks the rating range, ticket existence, authenticated Customer ownership and eligible status on every create/update. Use a database uniqueness constraint on TicketId and appropriate FK to existing Ticket; do not trust CustomerId from the request.
- Minimal recommended fields: ID or TicketId key, TicketId, rating, optional comment, CreatedAtUtc, UpdatedAtUtc (plus CustomerId/author only if actually necessary to fulfill the existing data model). The Ticket↔Customer relationship supplies ownership. Persist through EF Core and an **additive migration**, not a side table of duplicated ticket details.
- Read/display saved rating and comment on the owning customer's ticket, including after reload; maintain edit capability while ticket is eligible. **Operational default for reopened tickets:** keep any existing feedback record for reporting/history, display it read-only or defer editing until the ticket becomes Resolved/Closed again; do not delete feedback or reopen tickets automatically. Confirm only if real repository behavior makes this ambiguous.
- Feedback **does not** create a customer conversation, notify/send messages, reopen/close tickets, alter assignment/SLA, mark first response, or trigger AI. Staff may use this data later in **Reports & Management**; no CSAT analytics/dashboard/report pages are required in Story 12.
- Customer feedback text is customer-supplied/untrusted; render safely and avoid revealing comments or ratings to other customers. If future reports require staff access, add only a minimal safe read model then; do not pre-build the reporting module.

## 11. Backend/API/persistence expectations

- Follow the existing Clean Architecture/Mediator/FluentValidation and `/api/v1/` conventions. Add **customer-authorized use cases/DTOs** for customer ticket submission/list/detail/history, help-center list/detail, and feedback create/read/update. Endpoint names below are **illustrative**; planner must inspect real routing, Identity linkage and authorization code first.
- Suggested separate customer route families to avoid changing staff guards: `/api/v1/portal/tickets`, `/api/v1/portal/tickets/{id}`, `/api/v1/portal/tickets/{id}/timeline`, `/api/v1/portal/help-center/articles`, `/api/v1/portal/help-center/articles/{id}`, `/api/v1/portal/tickets/{id}/feedback`.
- Customers must not gain access to `/api/v1/tickets` staff controllers, raw ticket activity, staff KB CRUD or AI endpoints. Customer API contracts include only approved customer-safe data. Use existing 400/401/403/404/409 patterns, validation, safe `ProblemDetails`, OpenAPI notes and server-derived identity fields.
- Add only minimal feedback EF persistence and a reviewed **additive migration**. Preserve old feedback-free tickets and existing data. No database schema changes to ticket categories/workflow/SLA unless code inspection proves one necessary; show the reason in the plan before implementation.
- On submission reuse current validation and ticket-created side effects **without double-running** ticket activities, SLA snapshots or automation; existing worker determines background processing. Be careful not to create an artificial Agent assignee or staff identity for a Customer creator.
- For two rapid feedback submissions, enforce the one-record-per-ticket invariant at database level and return a sensible conflict/read-current response; no elaborate retry/distributed locking system. Keep updates compatible with the repository's existing concurrency style.
- If customer Identity-to-CRM linkage or existing activity metadata cannot support an assumption above, planner should document the actual code reality and propose the smallest safe solution **without silently changing CP1–CP5**.

## 12. Angular customer UX

- Use a **Customer-only route group** and navigation compatible with existing login/session/role guards, e.g. `/portal`, `/portal/tickets`, `/portal/tickets/new`, `/portal/tickets/:id`, `/portal/help-center`, `/portal/help-center/:id`. Verify current routes before picking exact names.
- **My Tickets**: list with page/filter, status indicators, previous ticket access and empty-state call-to-action for New Ticket.
- **New Ticket**: subject/description/category controls, required validation, submit progress, safe backend errors and a confirmation/detail redirect.
- **Ticket Details**: safe status timeline and Resolved/Closed CSAT form, displayed saved feedback and allowed edit action. No staff ticket cards, escalation controls, category Apply, AI assistance or communication components.
- **Help Center**: searchable paged list, article type/category filters, article details with safe Markdown and navigation back to search.
- Loading/empty/error/403-404 states, copyable ticket reference if useful, and responsive layout on narrow screens; reuse existing Angular Material design patterns. Never cache another customer's ticket data across logout or account switch.

## 13. Manual acceptance and testing policy

- Follow established story policy: add **exactly one new backend happy-path unit test** and **one new frontend happy-path unit test** using existing xUnit/Vitest patterns. Choose high-value paths, such as Customer ticket submission with derived profile and a working customer Help Center/feedback UI helper; the planner may select better seams. Run existing unit suites. No Playwright/E2E, provider/API/SQL integration test projects, large negative test suites or new frameworks.
- Build both backend and frontend. Inspect/apply the additive feedback migration in dev LocalDB without data loss. Manually validate customer role/API ownership, SQL feedback uniqueness, ticket/SLA preservation and Angular flows. Report precisely what was actually checked versus unavailable.
- Suggested manual acceptance matrix:
  1. Linked registered Customer can sign in and navigate portal; Agent/SuperAdmin retain their own staff UI; an unlinked/contact-only account cannot claim a CRM profile by guessing identifiers.
  2. Create ticket with subject, description and category only; new ticket belongs to authenticated linked CRM Customer, status `Open`, priority `Medium`, server-generated ID/created time.
  3. Forged `CustomerId`, priority, assignee, status or role in a customer request cannot override backend-derived fields.
  4. Existing ticket-created history, SLA snapshot and configured auto-assignment/automation remain functional without duplicate runs; unassigned tickets still track correctly.
  5. Customer My Tickets includes both own newly submitted and previous staff-created tickets, including Resolved/Closed; supports paging/search/filter if implemented.
  6. Customer A cannot list/detail/timeline/feedback Customer B's tickets using another ID or manually crafted request; anonymous/staff-only endpoints remain protected.
  7. Customer Ticket Details shows only safe subject/description/category/status/created information and does not include staff controls or sensitive fields.
  8. Timeline shows creation and actual status transitions/reopening in correct order with timestamps, no staff actor names or internal reasons.
  9. Timeline excludes assignment, escalation, internal notes, first-response/SLA, and arbitrary activity payloads even in direct API JSON; legacy tickets do not get invented transitions.
  10. Customer Help Center browses all four article types, keyword search, article type/category filters, paging, and safe Markdown rendering.
  11. `Published + Public` article is readable. `Published + Internal`, Draft Public, ReadyForPublication Public and Archived Public are **not** returned in search **or direct ID detail**.
  12. Changing a formerly public article to Internal/Archived immediately removes customer access to its detail endpoint; staff KB workflows remain intact.
  13. Customer cannot use staff KB CRUD or internal KB search, staff Ticket API, Gemini AI, or administrative routes.
  14. On own Resolved/Closed ticket, submit valid 1–5 CSAT rating and optional comment, reload and verify persistence; edit existing feedback without creating a second row.
  15. Feedback on Open/InProgress ticket or another Customer's ticket is rejected; invalid ratings, duplicate creations and overlong comments get clear API/UI responses.
  16. Reopened ticket retains saved CSAT record without reopening automatically or deleting data; confirm documented edit eligibility behavior.
  17. Creating/updating CSAT does not alter ticket status, activity/first-response or SLA, and does not send email/SMS/chat/customer messages.
  18. Angular Customer portal screens work with 400px viewport, loading/empty/validation/unauthorized states, logout/account changes and a direct refresh of portal routes.
  19. Smoke check existing Agent/SuperAdmin ticket workflows, staff KB publishing/linking, SLA/assignment and AI-001 (staff-only and on-demand); no extra automatic Gemini request occurs in Customer Portal.
  20. Validate final migration/schema, seed/demo data provenance, build/test results and secret hygiene, and **do not commit before user acceptance**.
- Document results as manual browser/API/SQL checks, not automated integration tests; never claim an unavailable browser check passed.

## 14. Explicitly out of scope

- Anonymous/guest portal or Help Center, contact-only customer account claiming, new identity-verification/email confirmation/recovery systems, Customer role escalation to staff access.
- Customer ticket editing after submission, choosing priority/Agent/status/escalation, closing/reopening/assigning tickets, internal staff workflow actions, modifying SLA policies.
- Customer↔Agent **ticket conversations**, comments/replies/messages, inbound email, outbound email, SMS, WhatsApp, live chat or web-form communication adapters. **Communication Channels last**.
- Customer AI chatbot, AI-generated FAQ recommendations, Gemini-based categorization on customer submission, any automatic Gemini calls.
- Article publishing/editing, Internal KB reads, customer article ratings/comments, automatic article suggestions on the ticket form, anonymous public article endpoints.
- Detailed feedback questionnaires, multiple feedback entries for one ticket, agent evaluation dashboards, CSAT analytics/aggregation/charts (future Reports & Management).
- New event sourcing, vector search, message queues, duplicate CRM databases, distributed transaction/concurrency machinery, advanced workflow/rules engines, complex multi-branch/department support or speculative hardening.
- Playwright/E2E, automated HTTP/SQL integration/performance suites, extra automated tests beyond exactly one new backend and one new frontend happy-path unit test.

## 15. Dependencies, planning handoff and delivery gates

- **Story:** `PORTAL-001` — Story 12, first Customer Portal story.
- **Depends on:** committed `AUTH-001`, `CUST-001/CUST-002`, `TICK-001/TICK-002`, `SLA-001`, `KB-001`, and current architectural/UI shell. `AI-001` is the immediately preceding story and must be committed before starting implementation; it remains **staff-only**, and ordinary customer portal navigation makes no AI calls.
- **Future use:** CSAT data feeds Reports & Management (planned later). Any conversation/communication actions remain in the **last** module.
- **Recommended planning sequence:** verify Story 11 baseline → inspect Customer/Identity profile linkage and existing Ticket/SLA creation path → define Customer-only authorization/projections → create customer ticket submission and safe list/details → customer-safe history projection → Public+Published Help Center → feedback entity/unique key/additive migration → Angular portal pages → docs and exactly 2 new unit tests → manual API/SQL/browser verification.
- **Intake location:** `.squad/stories/customer-portal/PORTAL-001/intake.md`.
- **Suggested plan location:** `.squad/plans/customer-portal/12-story-customer-portal.md`; planner selects final filename and updates `.squad/plans/customer-portal/00-overview.md` and `.squad/plans/00-index.md`.
- **Planning only at intake stage:** do not write application code, create migrations, commit or push. Review the Squad-Kit-generated plan independently before implementation.

## Source and approval note

- Source: *Customer Support CRM — Core Features* (2 pages), **Customer Portal** lists: Submit tickets; Track requests; View history; Access FAQs; Submit feedback. The document itself does **not** prescribe registration-only access, Medium default priority, timeline redaction rules, Public+Published filtering or star-rating feedback; those are derived from user-approved CP1–CP5 and the existing CRM modules.
- CP1–CP5 were explicitly approved through the Story 12 discussion. The implementation defaults (suggested endpoints, field lengths, page/filter shape, reopening feedback behavior, dev manual checks) are reasonable planning proposals and are **not** additional user decisions.
- The planner must inspect the **actual checked-out repository** rather than assuming source paths, method names, migrations, git status or existence of user-profile mappings from this intake alone.

## Notes for the Squad-Kit planner

Plan **what must work** for all five customer-facing workflows. The main correctness risks are: authoritative Customer Identity↔CRM Customer linkage; avoiding staff DTO/data leakage; preserving ticket-created SLA and automation side effects; safely handling older ticket histories; strict `Published AND Public` KB reads; one CSAT record per eligible owned ticket. Explain those boundaries in the plan and verify them without building unnecessary supporting infrastructure. Preserve the team's lightweight test policy and defer Communication Channels to the end.
