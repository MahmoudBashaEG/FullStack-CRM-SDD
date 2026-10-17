# Agent Dashboard — Ticket Overview & Work Queue — Story Intake

## Feature

- **Feature name (display):** Agent Dashboard — Ticket Overview & Work Queue
- **Feature slug (folder under `plans/`):** `agent-dashboard`

## Tracker

- **Tracker type:** `none`
- **Work item id:** `DASH-001`
- **Work item type:** `Business Feature Story`
- **Status:** `Draft`
- **Assignee:** `Unassigned`
- **Labels:** `agent-dashboard, assigned-tickets, work-queue, ticket-summary, customer-context, authorization, aspnet-core, angular, cqrs, happy-path-unit-tests, squad-kit`

## Title

Agent Dashboard — Ticket Overview & Work Queue

## Description

### Goal

Implement **Story 06**, the first useful Agent Dashboard experience in the existing Customer Support CRM. Give an authenticated **Agent** a fast view of their **currently assigned tickets**, meaningful workload counts, tickets needing attention, customer context, and links to the existing ticket/customer workflows. All data must come from the completed Ticket Management and Customer Management modules; the dashboard must not become a second ticket system.

The original *Customer Support CRM — Core Features* document lists five Agent Dashboard areas: **assigned tickets, customer information, tasks and reminders, quick replies, and team collaboration**. **This DASH-001 story delivers the first two using existing records.** The other three require new business entities/workflows and are explicitly deferred to later dashboard/productivity stories. A ticket's urgency or escalation is **not** itself a task or a reminder.

### 1. Existing system and compatibility

- Prerequisite: completed and committed Stories `ARCH-001`, `AUTH-001`, `CUST-001`, `TICK-001`, and `TICK-002`. If `TICK-002` is not yet committed, finish it first.
- Extend the existing .NET 10 ASP.NET Core REST API, Clean Architecture (`Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`), source-generated Mediator/CQRS, EF Core SQL Server, existing authentication/authorization, validation, `/api/v1/` and `ProblemDetails` conventions.
- Reuse the existing Ticket entity and persistence, the live assignment/visibility rules, status and priority string enums, manual escalation flag, Customers/Identity references, Angular 22 standalone and Material components, typed API clients and route guards.
- Inspect the **actual committed repository** and `docs/ticket-management.md` before planning. Do not assume any proposed filenames, routes, interfaces, or code line numbers here are necessarily exact.
- No changes to ticket creation, assignment permissions, workflow transitions, escalation or append-only history are authorized by this story. Preserve existing data and API contracts.

### 2. Dashboard audience and data access

- **Agent** is the primary and only new dashboard audience in DASH-001. A logged-in Agent sees **only tickets whose current `AssignedAgentUserId` equals their validated Identity user ID**. Do not count tickets created by an Agent if they are now assigned elsewhere. Do not include unassigned or other Agents' tickets.
- **SuperAdmin** retains existing admin Home and Tickets pages; do **not** create a company-wide management dashboard in this story. SuperAdmin cannot access an Agent's private dashboard by passing an `agentId` query parameter. A future Reports & Management story may add admin-wide metrics.
- **Customer** has no Agent Dashboard access. Preserve all Customer Portal exclusions.
- Apply authorization in the **backend aggregate and queue queries**, not merely Angular UI filters. The actor must come from authenticated claims and existing active-account policy, never a request-supplied Agent ID. Ensure role guards and navigation are aligned with API authorization.
- Dashboard data is a **live snapshot on request**, not persisted KPI counters, cached private cross-user data, or a background-maintained denormalized table. After reassignment, counts and work queue reflect the new current owner on refresh.

### 3. Workload summary — precise metric definitions

Display compact summary cards based only on tickets currently assigned to the authenticated Agent:

| Metric | Definition |
| --- | --- |
| **Open** | `Status == Open` |
| **In Progress** | `Status == InProgress` |
| **Escalated** | `IsEscalated == true` and status is `Open` or `InProgress` |
| **High / Urgent** | Status `Open` or `InProgress`, and priority is `High` or `Urgent` |

- A ticket may appear in more than one metric (e.g. an escalated, urgent, in-progress ticket). These **are overlapping counts and must never be presented as additive totals**.
- If an **Active Tickets** total is displayed, define it unambiguously as `Open + InProgress` (these statuses are disjoint). `Resolved` and `Closed` remain accessible through the existing Tickets page but are **not** attention-needed work.
- All counts must be computed at the server from authoritative persisted tickets, without fetching all ticket rows to Angular or iterating only the first paged response.
- Counters have sensible zero/empty states and display loading/retry on failure; avoid presenting failed requests as valid zero counts.

### 4. Attention queue and customer context

- Show a **bounded list of up to 10 currently assigned, active tickets** (`Open` or `InProgress`) as the Agent's *Needs attention* / *My work queue* section.
- Sort deterministically by: escalated tickets first; then priority `Urgent`, `High`, `Medium`, `Low`; then oldest `UpdatedAtUtc` first; then Ticket ID as a stable tie-breaker. Explain that this is a **display ordering**, not an SLA engine or automatic priority change.
- Show at minimum ticket subject, category, priority, status, escalation indicator, Customer name, and last-updated date. Make the ticket title open the existing ticket details route (which already offers authorized workflow actions and history).
- Where helpful, show available Customer email or a link to the **existing** Customer details view, following existing staff permissions. Never show Identity secrets, account hashes or session metadata. Do not create a second customer profile/editor in the dashboard.
- Clearly label the list as a **top 10 attention queue**, not every assigned ticket. Provide a **View all my tickets** link to the existing Agent-scoped Tickets page and a **Create ticket** action reusing the existing Ticket create route.
- When no active tickets are assigned, show a useful empty state; when the Agent has only Resolved/Closed tickets, do not show them in the attention queue.
- No inline ticket editing or new status/assignment controls in the dashboard; link to the working TICK-002 UI instead.

### 5. API, application and data access

- Implement a **single read-only, Agent-authorized dashboard query/API** (suggested `GET /api/v1/agent-dashboard`, exact route per existing conventions) returning typed summary counts and bounded attention-ticket DTOs. Avoid many independent full-list HTTP requests to derive counts.
- Derive the Agent ID from validated JWT claims inside the server; **do not accept `agentId`, arbitrary staff IDs, or client-supplied permission flags**.
- Reuse the existing Ticket repository/EF Core query style and Customer display-name projection where practical. Keep EF Core inside Infrastructure, orchestration/DTOs in Application/API, and UI types/services in the Angular feature. Prefer efficient database projections, `AsNoTracking`, and indexed filtering where possible.
- Keep ordering/filters **in SQL before `Take(10)`**, not Angular sorting of a paged subset. Use one logically consistent server-side query path for ownership scoping across counts and queue results.
- A read-only feature ordinarily needs **no new database migration**. If the planner finds a concrete need to add persistence/schema, it must justify it explicitly before implementation; do not add a new Dashboard table merely to cache existing ticket counters.
- Use established success and error formats and document the route's Agent-only access in OpenAPI/docs. Do not alter existing `/api/v1/tickets` response contracts solely for dashboard presentation.

### 6. Angular UI/UX

- Add an **Agent Dashboard** Angular feature integrated into existing shell/navigation, using standalone components, the established Material theme, role-aware guards, typed API service, signals or existing reactive style, and responsive/direction-neutral CSS.
- Add a visible navigation entry such as **My Dashboard** for the `Agent` role. Do not replace SuperAdmin Home or redirect all roles to an Agent-only route. The planner may use `/agent-dashboard` or another non-conflicting path after checking current routes.
- Above the fold: summary cards for Open, In Progress, Escalated, High / Urgent. Below: the bounded attention queue with customer context and direct links to the existing ticket/customer screens.
- Summary cards may link to the existing Tickets page with supported filter parameters only where the existing route actually understands them; **do not promise an unsupported combined High/Urgent filter**. A non-clickable summary card is preferable to an incorrect link.
- Provide a user-triggered **Refresh** control and refresh when the dashboard is revisited; no timers, real-time sockets, polling, notifications or background jobs.
- Include loading, error with Retry, empty, and responsive mobile layouts; never display stale data as if it were freshly loaded after a failed refresh. Reuse shared error handling and components where appropriate.
- Preserve Customers, Tickets, Profile, Agents administration, login/logout and the existing app navigation. Do not introduce new UI libraries, charts, widgets, or dashboard frameworks for four counts and one queue.

### 7. Verification and documentation

- Add **exactly one meaningful backend happy-path unit test** and **one meaningful frontend happy-path unit test** for DASH-001 using existing frameworks and conventions. Keep the existing unit tests unchanged unless compilation requires a narrow fake/interface adjustment. Do not add automated API, SQL/integration, negative-path, E2E/browser, load or security-abuse test suites.
- Build both projects and run **all existing plus new unit tests**. Manually verify: Agent dashboard populated with live assigned tickets, counts and attention sorting; different Agents see different content; reassignment is reflected after refresh; zero-state; ticket/customer links; Customer and SuperAdmin cannot access Agent-only dashboard; regression for Tickets, Customers, Profile and authentication.
- Verify server-side ownership and aggregate correctness with targeted manual API checks where appropriate; document exactly which checks were run. Do not treat client-only hiding as sufficient.
- Verify no unexpected schema changes or data migrations and no introduced secrets. Preserve historical ticket and activity data.
- Update relevant docs (new `docs/agent-dashboard.md` or equivalent plus nav/authorization/API/README references) describing audience, metric definitions, attention ordering, refresh semantics, authorization and deferred Agent Dashboard capabilities.

## Acceptance criteria

- [ ] DASH-001 extends the committed CRM/Ticket Management implementation without rebuilding ticket/customer management, changing workflow or altering stored ticket data.
- [ ] An authenticated Agent can open **My Dashboard** from a role-appropriate navigation entry; Customer and SuperAdmin do not see or access the Agent-only dashboard.
- [ ] All dashboard data is limited **server-side** to tickets currently assigned to the authenticated Agent; no client-provided Agent ID can expand visibility.
- [ ] The Open, In Progress, Escalated and High / Urgent counts use the exact definitions in §3 and are computed against the whole authorized data set, not only a page of tickets.
- [ ] The attention queue contains at most 10 assigned `Open`/`InProgress` tickets, in the specified escalated-first, priority, oldest-updated deterministic order.
- [ ] Each queue item exposes safe ticket/customer context and links to the existing ticket workflow; View all my tickets and Create ticket routes reuse current screens.
- [ ] No assignment, ticket status transition, escalation, customer data or activity-history API is duplicated or given additional powers by this story.
- [ ] A ticket reassigned to another Agent disappears from the previous Agent's dashboard after refresh and appears for the new Agent if active.
- [ ] Zero/empty/loading/error/retry and Refresh behavior are correct; request failures are not silently shown as zero workload.
- [ ] Agent Dashboard is readable on desktop/mobile and follows current Angular Material layout, role guards and direction-neutral styles.
- [ ] The API is read-only, typed, documented and restricted to Agent; existing API contracts remain backward compatible and no unnecessary migration is introduced.
- [ ] Authentication, SuperAdmin admin screens, Customers and existing TICK-001/TICK-002 functions continue working.
- [ ] Backend/frontend builds and all previous unit tests pass, plus one new backend and one new frontend happy-path unit test; manual authorization and usage checks pass.
- [ ] Documentation distinguishes delivered assigned-ticket/customer-context dashboard features from deferred tasks/reminders, quick replies, team collaboration and later Reports & Management/SLA features.

## Attachments

None required. The original 2-page *Customer Support CRM — Core Features* business scope is already present with the initial project story. Relevant Agent Dashboard items are reproduced in this intake. The actual committed CRM source and prior story files provide technical context.

## Dependencies

- **Blocked by / related ids:** `ARCH-001`, `AUTH-001`, `CUST-001`, `TICK-001`, `TICK-002`. DASH-001 follows committed Story 05 and should receive **Story number 06** if no other plan was created in between.
- **Depends on code areas or other stories:** Existing Ticket entity/repository/read projections and scope, `TicketStatus`, `TicketPriority`, escalation fields, `TicketDetails`, Agent role policy and JWT actor, `frontend/src/app/features/tickets/`, `frontend/src/app/core/layout/navigation.ts`, `frontend/src/app/app.routes.ts`, Customer Details routes and relevant docs. Inspect the actual code and docs rather than assuming exact paths for every symbol.

## Extra notes

- This is a **requirements intake, not an implementation plan**. Generate the Story 06 implementation plan separately through Squad-Kit after reviewing this file; do not write application code now.
- Deliberate scope decision: first deliver a real read-only Agent work dashboard using the Ticket data already available; plan **tasks/reminders, quick replies and team collaboration** in later stories because none of those business models is currently defined.
- `SuperAdmin` intentionally does not get a separate admin metrics dashboard in this story; global management analytics belong to Reports & Management. Existing staff Tickets functionality remains available to SuperAdmin.
- The summary cards overlap, and attention ordering is not an SLA guarantee; both facts must be clear in the UI/docs.
- Prefer a maintainable, small read-only capability with no extra dependency/package, persistence table or background process.
- The planner must not invent fake project completion state; check that `TICK-002` was actually committed before assuming Story 05 is complete.

## Technical hints (optional)

- Current stack: .NET 10, ASP.NET Core REST + source-generated Mediator/CQRS, EF Core SQL Server, `CrmDbContext`, ASP.NET Core Identity/JWT, Angular 22 standalone/Material.
- Consider a `GetAgentDashboardQuery` with server-derived authenticated Agent ID and a feature-focused read-projection method, using `Ticket` query scoping already established in `TicketRepository`. Let the planner select exact interfaces/names to avoid coupling Application to EF Core.
- Keep summary counters and top-10 queue projections efficient and use stable sort keys; `TicketPriority` is stored as **string**, so do not rely on alphabetical SQL ordering for priority (`Urgent > High > Medium > Low`). Use an explicit conditional rank in the database query.
- Reuse existing typed API URL/auth interceptor/error handling, current UserRole and route guards, UI badges, ticket/customer links and Material layout patterns. Do not create independent authentication state or client-side ticket access policies.
- Only positive-path automated unit tests; authorization scoping can also be confirmed with concise manual API/browser checks.

## Out of scope

- Tasks and reminders, task assignment/completion, calendar integration, scheduled reminders or notifications (later Agent Dashboard/productivity story).
- Quick-reply templates, canned responses, draft sending, email/chat/SMS or external channel communication (later Agent Dashboard/Communication Channels stories).
- Team collaboration, agent comments, mentions, group chat, approval workflows, shared private notes (later story).
- SuperAdmin company-wide dashboard, Agent performance analytics, SLA targets or management reports (Reports & Management/SLA & Automation stories).
- Automatic assignment/reassignment, workload-based routing, dynamic urgency, AI recommendations, alerts, background processing or real-time socket updates.
- Customer Portal features, new Ticket status/priority rules, ticket editing in dashboard, customer editing, ticket deletion, additional migrations or data backfills without demonstrated necessity.
- New integration/API/E2E/browser/negative-path/performance/security-abuse automated suites, new frameworks or unnecessary infrastructure.
