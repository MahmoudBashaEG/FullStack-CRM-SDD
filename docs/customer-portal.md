# Customer Portal (PORTAL-001)

Self-service for signed-in, linked Customers: submit requests, track them, see a customer-safe history, read Help Center articles and leave one satisfaction rating per resolved request.

## 1. Scope

Approved decisions CP1–CP5:

- **CP1** Customer-only area (`/portal`, `AuthorizationPolicies.Customer`). The customer is resolved on the server.
- **CP2** Submit: subject, description, category. Always `Open` / `Medium`.
- **CP3** Track every ticket of the customer's profile and see a customer-safe timeline.
- **CP4** Help Center: `Published` + `Public` Knowledge Base articles only.
- **CP5** One editable CSAT feedback record per ticket, while the ticket is `Resolved` or `Closed`.

Not built: anonymous portal, contact claiming, customer replies/comments, email/SMS/chat, customer edit/close/reopen, priority/assignee/SLA display, any AI call, CSAT reports (shipped in REPORT-001, SuperAdmin only), rate limiting.

## 2. Identity resolution

`Customers.UserId` (unique filtered FK to `AspNetUsers`, set only by signup and the startup backfill) is the only link. Each request resolves the CRM customer id from the JWT `sub` through `ICustomerRepository.FindDetailsByUserIdAsync`. **Never by email, never from the request.** No linked profile ⇒ 404 `Customer profile not found.` (same as `GET /me/customer-profile`); the UI shows "Your account is not linked to a customer profile. Contact support."

Contact-only profiles (`UserId == null`) cannot be claimed. Tickets on a contact-only profile with the same email as a registered customer are **not** visible in the portal.

## 3. Role matrix

| Operation | Customer (linked) | Agent | SuperAdmin | Anonymous |
|---|---|---|---|---|
| `GET/POST /portal/tickets` | ✔ own profile only | 403 | 403 | 401 |
| `GET /portal/tickets/{id}`, `/timeline` | ✔ own (others 404) | 403 | 403 | 401 |
| `PUT /portal/tickets/{id}/feedback` | ✔ own **and** Resolved/Closed (else 404 / 409) | 403 | 403 | 401 |
| `GET /portal/help-center/*` | ✔ Published + Public only (else 404) | 403 | 403 | 401 |
| Staff ticket, KB, AI, admin endpoints | 403 (unchanged) | unchanged | unchanged | 401 |

## 4. API

| Method | Route | Success | Errors |
|---|---|---|---|
| GET | `/api/v1/portal/tickets?search&status&page&pageSize` | 200 paged | 400, 401, 403, 404 (no profile) |
| POST | `/api/v1/portal/tickets` `{subject, description, category}` | 201 | 400, 404 (no profile) |
| GET | `/api/v1/portal/tickets/{id}` | 200 | 404 (missing **or** not owned) |
| GET | `/api/v1/portal/tickets/{id}/timeline` | 200 | 404 |
| PUT | `/api/v1/portal/tickets/{id}/feedback` `{rating 1–5, comment ≤ 1000}` | 200 (refreshed ticket) | 400, 404, 409 |
| GET | `/api/v1/portal/help-center/articles?search&type&categoryId&page&pageSize` | 200 paged | 400 |
| GET | `/api/v1/portal/help-center/articles/{id}` | 200 | 404 (missing, unpublished or Internal) |
| GET | `/api/v1/portal/help-center/categories` | 200 `{id, name}[]` | — |

Customer DTOs are separate from staff DTOs. Ticket responses never contain priority, assignee, creator, escalation, SLA, `updatedAtUtc` or customer data.

## 5. Submission pipeline

`SubmitPortalTicketCommand` runs the same sequence as `CreateTicketCommand`: `Ticket.Create` (writes the `Created` activity) → `ITicketCreationAutomation.ApplyAsync` (SLA snapshot, at most one rule-based assignment) → `Add` → **one** save. The customer's own `AspNetUsers.Id` is `CreatedByUserId`; no staff actor is impersonated and the ticket is not self-assigned. Rule-based assignment runs **synchronously** at creation, so a portal ticket may already be assigned in the database; the customer never sees an assignee. Two concurrent POSTs create two tickets (no idempotency key); the UI blocks double submits.

## 6. Timeline

Only `Created` and `StatusChanged` activity rows are selected, and only `Id, Type, OccurredAtUtc, OldValue, NewValue`. Actor, assignee, reason, field, escalation, first-response and SLA data are never read. Creation is worded neutrally ("Request created") because staff may create tickets on a customer's behalf. Tickets without a `Created` row get one synthesized entry from `Tickets.CreatedAtUtc` with `isRecorded: false`. At most the newest 200 events are read; if `Created` falls outside, the synthesized entry is added.

## 7. Help Center

`Status == Published && Visibility == Public` is applied in SQL for list, detail and categories. Responses carry no author, publisher, status or visibility. `GET categories` lists only categories holding at least one such article (`id`, `name`). Bodies render through the safe Markdown renderer (raw HTML escaped, unsafe links dropped).

## 8. Feedback

Table `TicketFeedback`: unique `TicketId` (`UX_TicketFeedback_TicketId` ⇒ 409 on a racing insert), check `CK_TicketFeedback_Rating` (1–5), `Comment` ≤ 1000, `Version` rowversion (overlapping edit ⇒ 409). No `CustomerId` column: ownership is `Tickets.CustomerId`. `PUT` creates or updates the single row and returns the refreshed ticket (`canSubmitFeedback`, `feedback`). If a ticket is reopened the record is kept and shown read-only until it is Resolved/Closed again (`PUT` ⇒ 409). Feedback never changes the ticket, its activity, SLA or assignment. Staff screens and reports for feedback are future work.

## 9. Angular

Customer-only (`roleGuard('Customer')`): `/portal`, `/portal/tickets`, `/portal/new-ticket`, `/portal/tickets/:id`, `/portal/help-center`, `/portal/help-center/:id`. Navigation (Customer): Support Portal, My Tickets, New Ticket, Help Center. Home shows a Customer support card. Portal state lives only in component signals: no root cache, no `localStorage`.

## 10. Migration

`AddTicketFeedback` is additive: one table, one unique index, one FK, one check constraint. Rollback (local): `dotnet ef database update AddKnowledgeBase …` drops `TicketFeedback` **including any feedback created**; confirm first. Portal-created tickets are ordinary tickets and stay valid.

## 11. Future

Communication Channels (email/chat) remain the last story. CSAT reporting: see [reports-and-management.md](reports-and-management.md).
