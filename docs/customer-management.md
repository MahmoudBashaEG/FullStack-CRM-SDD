# Customer management (CUST-001, CUST-002)

## 1. CRM customers vs Identity accounts

- The `Customers` table holds CRM customer profiles. `Customers.Id` (`Guid`, generated in code) is the stable id that future stories (notes, attachments, interactions, tickets) reference — **not** `AspNetUsers.Id`.
- **Registered** customer: `UserId` is set (a `Customer`-role account that can sign in).
- **Contact-only** customer: `UserId` is `NULL` — created by SuperAdmin/Agent, no password, role or token, cannot sign in.
- A unique filtered index (`[UserId] IS NOT NULL`) allows at most one profile per account and any number of contact-only rows. `UserId` is set only by the server (signup / backfill); no request DTO carries it and no response exposes it.
- Agents and SuperAdmin never get a CRM profile. Customers are never deleted in this story.

## 2. Field ownership and sync

| Customer kind | Owner of email / first name / last name / phone | Owner of company |
|---|---|---|
| Registered | Identity (`AspNetUsers`); the CRM row is a mirror (needed for list/search) | CRM only |
| Contact-only | CRM row | CRM row |

- Three write paths keep Identity and the CRM row identical, in **one transaction** each: `PUT /api/v1/me`, `PUT /api/v1/me/customer-profile`, `PUT /api/v1/customers/{id}`.
- The login email is never changed by any path. A staff request with a different email for a registered customer is rejected with **422**.
- Staff may change a contact-only customer's email; it never touches `AspNetUsers`.
- Concurrency: last write wins (no row version) — acceptable for this CRM.

## 3. Role matrix

The server enforces roles (`Staff` / `Customer` policies) and ownership (the user id comes from the JWT subject only). Angular guards and menus are usability aids.

| Capability | SuperAdmin | Agent | Customer |
|---|:-:|:-:|:-:|
| List / search customers | ✔ | ✔ | — |
| View any customer | ✔ | ✔ | — |
| Create contact-only customer | ✔ | ✔ | — |
| Edit contact-only customer | ✔ | ✔ | — |
| Edit registered customer (name, phone, company) | ✔ | ✔ | — |
| View / edit own linked profile | — | — | ✔ |
| Add / read internal notes | ✔ | ✔ | — |
| Log / read interactions | ✔ | ✔ | — |
| Upload / list / download attachments | ✔ | ✔ | — |
| Edit / delete notes, interactions or attachments | — | — | — |
| Related tickets on Customer Details | ✔ all | ✔ assigned to them | — |

## 4. API

| Method | Route | Auth | Result |
|---|---|---|---|
| GET | `/api/v1/customers` | Staff | 200, paged |
| POST | `/api/v1/customers` | Staff | 201 + Location |
| GET / PUT | `/api/v1/customers/{id}` | Staff | 200; 404 unknown id; 422 changed login email |
| GET / PUT | `/api/v1/me/customer-profile` | Customer | 200; 404 when no profile |
| GET / POST | `/api/v1/customers/{id}/notes` | Staff | 200 paged / 201; 400 invalid; 404 unknown customer |
| GET / POST | `/api/v1/customers/{id}/interactions` | Staff | 200 paged / 201; 400 invalid; 404 unknown customer |
| GET / POST | `/api/v1/customers/{id}/attachments` | Staff | 200 paged metadata / 201 (POST = multipart `file`); 400 invalid file; 404 unknown customer |
| GET | `/api/v1/customers/{id}/attachments/{attachmentId}/content` | Staff | 200 file download; 404 unknown or other customer's attachment |

Customer-role tokens get 403 on all CUST-002 routes. Related tickets reuse the existing, scoped `GET /api/v1/tickets?customerId=`.

List contract (`GET /api/v1/customers`):

- `page` one-based, default 1 (1…1 000 000); `pageSize` default 20, minimum 1, values above 100 are clamped to 100.
- `search` (≤ 100 chars, trimmed) matches first name, last name, full name, email and phone; case-insensitive under the default SQL Server collation.
- Order: `CreatedAtUtc DESC, Id DESC`. Response: `{ items, page, pageSize, totalCount }`.

## 5. Signup linkage and backfill

- `POST /api/v1/auth/register` (contract unchanged) creates the account and its linked profile atomically.
- At startup, `CustomerProfileInitializer` (after identity bootstrap, before requests are served) creates one profile for every pre-existing `Customer`-role account that has none. It is idempotent, never touches Agents/SuperAdmin, and is skipped with a warning while migrations are pending (apply migrations, then restart). Timestamps equal the account's creation time.

## 6. Duplicate contact email trade-off

There is no automatic link or merge between a contact-only record and an account with the same email, because signup does not verify email ownership — linking would hand a stranger a staff-created record. Two records may therefore share an email; staff tell them apart by the Registered / Contact only badge. A verified claim/merge workflow is a future story.

## 7. Future stories

Notes, interactions and attachments shipped in CUST-002 (§8–§11) and reference `Customers.Id`; tickets already do (`Tickets.CustomerId`, see [ticket-management.md](ticket-management.md)). Deferred: verified linking/merge, deletion/archiving, invitations, per-agent assignment, departments/branches, optimistic concurrency, full-text search, attachment deletion, note/interaction corrections in place.

## 8. Notes and interactions (CUST-002)

- **Ownership:** staff-only data, global for SuperAdmin and Agents (any staff member can read and add on any customer). The author / recorder comes from the JWT subject only, never from the body. Names are joined from `AspNetUsers` at read time, not stored.
- **Append-only (C2 Option A):** there is no edit or delete API or UI. A correction is a new note or interaction.
- **Notes:** plain text, 1–2 000 characters (trimmed on save), listed newest first (`CreatedAtUtc DESC, Id DESC`).
- **Interactions:** type `PhoneCall` / `Meeting` / `Other`, summary 1–500 characters, `occurredAtUtc` optional (default now). `OccurredAtUtc` is when the contact happened (past allowed; later than now + 5 minutes is rejected; before 2000-01-01 is rejected); `CreatedAtUtc` is when it was logged. Listed newest occurrence first (`OccurredAtUtc DESC, Id DESC`), so a back-dated entry can appear below newer ones. After saving, Angular reloads the first page so order and paging stay correct.
- All lists use `page` / `pageSize` and return `PagedResponse<T>`; the UI shows 10 per page with a "Load older" button.

## 9. Related tickets

The Customer Details page lists the customer's tickets by calling the existing `GET /api/v1/tickets?customerId=`. The server scopes Agents to tickets assigned to them before the customer filter, so an Agent never sees an unassigned or other Agent's ticket. Ticket discussion membership (DASH-002) grants nothing here. No new ticket access path exists.

## 10. Attachments

- Stored in SQL Server (`CustomerAttachments.Content varbinary(max)`, C4). Bytes are never part of any JSON response or metadata query: list queries project metadata columns only, and only the download query selects `Content`.
- One file per upload, ≤ 5 MB, empty files rejected. Allowed: PDF, PNG, JPG/JPEG, TXT, DOCX. The extension (case-insensitive) **and** the file signature are checked on the server (`%PDF-`, PNG, JPEG and zip signatures; TXT must contain no NUL byte). The client `Content-Type` is ignored; the stored type comes from the extension allow-list. Validation errors use the `file` key (400).
- The request is capped at 6 MB (`RequestSizeLimit`), so 5–6 MB files get the friendly "larger than 5 MB" message; bigger requests are rejected by the framework with a 400 form-read error (observed, never 500).
- The stored file name is the sanitized last path segment (display only, never used as a path; nothing is written to disk).
- Download: authenticated `GET …/content` with `Content-Disposition: attachment`, `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-store`. No public URL, preview, delete or antivirus scan.
- **Trade-off:** files grow the database. If volumes grow, move content to object storage in a later story.

## 11. Not Communication Channels

Interactions are manual logs only. No Email, WhatsApp, SMS or Live Chat message is sent, received or synced; that is the final roadmap module (Communication Channels).