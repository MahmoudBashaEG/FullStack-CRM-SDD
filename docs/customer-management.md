# Customer management (CUST-001)

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

## 4. API

| Method | Route | Auth | Result |
|---|---|---|---|
| GET | `/api/v1/customers` | Staff | 200, paged |
| POST | `/api/v1/customers` | Staff | 201 + Location |
| GET / PUT | `/api/v1/customers/{id}` | Staff | 200; 404 unknown id; 422 changed login email |
| GET / PUT | `/api/v1/me/customer-profile` | Customer | 200; 404 when no profile |

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

CUST-002 notes, CUST-003 attachments, CUST-004 interaction history reference `Customers.Id`; tickets already do (`Tickets.CustomerId`, see [ticket-management.md](ticket-management.md)). Deferred: verified linking/merge, deletion/archiving, invitations, per-agent assignment, departments/branches, optimistic concurrency, full-text search.
