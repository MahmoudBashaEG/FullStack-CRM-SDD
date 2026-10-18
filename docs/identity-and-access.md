# Identity and access (AUTH-001)

## 1. Role matrix

Roles are assigned by the server only. Angular guards and navigation filtering are usability aids; the API enforces every rule.

| Capability | SuperAdmin | Agent | Customer |
|---|:-:|:-:|:-:|
| Sign in | ✔ | ✔ | ✔ |
| View / update own profile | ✔ | ✔ | ✔ |
| Change own password | ✔ | ✔ | ✔ |
| Public signup | — | — | ✔ (creates a Customer) |
| List / create / edit / enable-disable / set password of Agents | ✔ | — | — | (status changes and creation are audited; see [security-administration.md](security-administration.md))
| Manage CRM customers (list / view / create contact-only / edit) | ✔ | ✔ | — |
| View / edit own CRM customer profile | — | — | ✔ |
| Customer notes / interactions / attachments (read / add; append-only, no delete) | ✔ | ✔ | — |
| Create tickets (Agent: assigned to self; SuperAdmin: active Agent or unassigned) | ✔ | ✔ | — |
| List / view / edit tickets | ✔ all | ✔ assigned to them | — |
| Assign / reassign / unassign existing tickets | ✔ | — | — |
| Change ticket status (workflow) | ✔ all | ✔ assigned to them | — |
| Escalate ticket | ✔ | ✔ assigned to them | — |
| Clear escalation | ✔ | — | — |
| View ticket activity | ✔ all | ✔ assigned to them | — |
| View own Agent Dashboard (assigned-ticket workload) | — | ✔ | — |
| Create / manage tasks | ✔ for any active Agent | ✔ own | — |
| Reassign a task | ✔ | — | — |
| Task reminder inbox | — | ✔ own tasks | — |
| Shared quick replies | ✔ manage | ✔ read / copy | — |
| Personal quick replies | — (never visible) | ✔ own | — |
| Internal discussion (read / post) | ✔ any ticket | ✔ primary or active collaborator | — |
| Invite / remove collaborators | ✔ any ticket | ✔ primary assignee only | — |
| Knowledge Base: read/search Published articles | ✔ | ✔ | — |
| Knowledge Base: create/edit own Draft, submit | ✔ any Draft | ✔ own | — |
| Knowledge Base: return/publish/archive/restore, manage categories | ✔ | — | — |
| Link/unlink KB articles on tickets | ✔ any ticket | ✔ assigned to them | — |
| AI ticket assistance (summary, reply draft, category suggestion, solutions) | ✔ any ticket | ✔ assigned to them | — |
| Apply a category (`PUT /tickets/{id}/category`) | ✔ any ticket | ✔ assigned to them | — |
| AI Assistant chat (general / ticket context) | ✔ | ✔ (ticket: assigned only) | — |
| Customer Portal: submit / list / view own tickets and their status history | — | — | ✔ own linked profile |
| Customer Help Center: read Published + Public articles | — | — | ✔ |
| Ticket satisfaction feedback (own Resolved/Closed ticket) | — | — | ✔ |
| Management dashboard and reports (tickets, SLA, Agents, CSAT) | ✔ | — | — |
| Create a SuperAdmin | nobody via the API (bootstrap only) | | |
| Administration: users, account (de)activation (any role), audit logs, organization settings, permission matrix | ✔ | — | — |

## 2. API routes

| Method | Route | Auth | Success |
|---|---|---|---|
| POST | `/api/v1/auth/register` | anonymous (rate limited) | 201 |
| POST | `/api/v1/auth/login` | anonymous (rate limited) | 200 |
| POST | `/api/v1/auth/refresh` | anonymous + refresh cookie + CSRF header | 200 |
| POST | `/api/v1/auth/logout` | anonymous + refresh cookie + CSRF header | 204 |
| GET / PUT | `/api/v1/me` | authenticated | 200 |
| POST | `/api/v1/me/password` | authenticated | 204 |
| GET / POST | `/api/v1/agents` | SuperAdmin | 200 / 201 |
| GET / PUT | `/api/v1/agents/{id}` | SuperAdmin | 200 |
| PUT | `/api/v1/agents/{id}/status` | SuperAdmin | 204 |
| PUT | `/api/v1/agents/{id}/password` | SuperAdmin | 204 |
| GET / POST | `/api/v1/customers` | SuperAdmin, Agent | 200 / 201 |
| GET / PUT | `/api/v1/customers/{id}` | SuperAdmin, Agent | 200 |
| GET / POST | `/api/v1/customers/{id}/notes` | SuperAdmin, Agent | 200 / 201 |
| GET / POST | `/api/v1/customers/{id}/interactions` | SuperAdmin, Agent | 200 / 201 |
| GET / POST | `/api/v1/customers/{id}/attachments` | SuperAdmin, Agent | 200 / 201 |
| GET | `/api/v1/customers/{id}/attachments/{attachmentId}/content` | SuperAdmin, Agent | 200 |
| GET / POST | `/api/v1/tickets` | SuperAdmin, Agent | 200 / 201 |
| GET / PUT | `/api/v1/tickets/{id}` | SuperAdmin, Agent (Agent only own assigned) | 200 |
| PUT | `/api/v1/tickets/{id}/assignee` | SuperAdmin | 200 |
| POST | `/api/v1/tickets/{id}/transitions` | SuperAdmin, Agent (only own assigned) | 200 |
| POST | `/api/v1/tickets/{id}/escalation` | SuperAdmin, Agent (only own assigned) | 200 |
| DELETE | `/api/v1/tickets/{id}/escalation` | SuperAdmin | 200 |
| GET | `/api/v1/tickets/{id}/activity` | SuperAdmin, Agent (only own assigned) | 200 |
| GET | `/api/v1/agent-dashboard` | Agent | 200 |
| GET | `/api/v1/agent-dashboard/tasks` | Agent | 200 |
| * | `/api/v1/tasks/*` | SuperAdmin, Agent (own); `PUT .../assignee` SuperAdmin | 200 / 201 |
| POST / GET / PUT | `/api/v1/notifications/*` | Agent | 200 / 204 |
| * | `/api/v1/quick-replies/*` | SuperAdmin (Shared), Agent (Personal + read Shared) | 200 / 201 |
| * | `/api/v1/collaborations/*` | SuperAdmin, Agent (primary / collaborator); `GET /collaborations` Agent | 200 / 201 / 204 |
| GET / PUT | `/api/v1/me/customer-profile` | Customer | 200 |
| * | `/api/v1/knowledge-base/articles/*` | SuperAdmin, Agent (`return`/`publish`/`archive`/`restore`: SuperAdmin only, Agent 403) | 200 / 201 |
| GET / POST / PUT | `/api/v1/knowledge-base/categories[/{id}]` | GET staff; POST/PUT SuperAdmin | 200 / 201 |
| GET / POST / DELETE | `/api/v1/tickets/{id}/knowledge-articles[/{articleId}]` | SuperAdmin any ticket; Agent only tickets assigned to them | 200 / 201 / 204 |
| POST | `/api/v1/tickets/{id}/ai/{summary\|reply-draft\|category-suggestion\|solutions}` | SuperAdmin any ticket; Agent own assigned; rate limited | 200 |
| PUT | `/api/v1/tickets/{id}/category` | SuperAdmin any ticket; Agent own assigned | 200 |
| POST | `/api/v1/ai/chat` | SuperAdmin, Agent; rate limited | 200 |
| GET / POST | `/api/v1/portal/tickets` | Customer (own linked profile) | 200 / 201 |
| GET | `/api/v1/portal/tickets/{id}` | Customer (own linked profile) | 200 |
| GET | `/api/v1/portal/tickets/{id}/timeline` | Customer (own linked profile) | 200 |
| PUT | `/api/v1/portal/tickets/{id}/feedback` | Customer (own linked profile) | 200 |
| GET | `/api/v1/portal/help-center/articles[/{id}]` | Customer (Published + Public only) | 200 |
| GET | `/api/v1/portal/help-center/categories` | Customer | 200 |
| GET | `/api/v1/reports/tickets` | SuperAdmin | 200 |
| GET | `/api/v1/reports/sla` | SuperAdmin | 200 |
| GET | `/api/v1/reports/agents` | SuperAdmin | 200 |
| GET | `/api/v1/reports/customer-satisfaction` | SuperAdmin | 200 |
| GET | `/api/v1/admin/users`, `/api/v1/admin/users/{id}` | SuperAdmin | 200 |
| PUT | `/api/v1/admin/users/{id}/status` | SuperAdmin | 200 |
| GET | `/api/v1/admin/audit-logs` | SuperAdmin | 200 |
| GET / PUT | `/api/v1/admin/organization-settings` | SuperAdmin | 200 |
| GET | `/api/health` | anonymous | 200 |

`/api/v1/agents/{id}` only reaches Agent accounts; SuperAdmin and Customer ids return 404.

Signup also creates the linked CRM customer profile (see [customer-management.md](customer-management.md)).

## 3. Sessions

- **Access token:** HS256 JWT, 10 minutes, claims `sub`, `role`, `sstamp`, `jti` and time claims only. Held in Angular memory, never in web storage.
- **Refresh token:** 256 random bits, stored only as a SHA-256 hash in `RefreshSessions`; 7 days sliding; single use. Reusing a rotated token outside the grace window revokes the whole family.
- **Multi-tab grace window** (`Jwt:RefreshTokenReuseGracePeriod`, default 30 s): a token rotated moments ago by another tab, re-presented while the login is still alive, yields a sibling session in the same family instead of a revocation.
- **Logout** revokes the whole family of the presented token, so siblings never outlive it.
- **Cookies:** `crm_refresh` (HttpOnly, Secure, SameSite=Strict, Path `/api/v1/auth`) and `XSRF-TOKEN` (readable, Secure, SameSite=Strict, Path `/`). Refresh and logout validate the double-submit `X-XSRF-TOKEN` header (403 on mismatch).
- **Reload:** `provideAppInitializer` calls `POST auth/refresh` when an `XSRF-TOKEN` cookie exists, so the session is restored before the first navigation.

## 4. Revocation and JWT trade-offs

- Every authenticated request re-reads `IsActive` and the security stamp, so disable / password change / password reset take effect immediately (one extra indexed read per request). Verified for ADMIN-001 with retained Agent and Customer tokens.
- **Logout does not rotate the stamp**: an access token copied before logout stays valid until it expires (at most 10 minutes).
- Inside the grace window, a thief replaying a just-rotated token while the victim's login is still active gets a sibling session instead of triggering revocation. Accepted: the window is shorter than an access-token lifetime, siblings die with logout / password change / disable, and any replay after the window revokes the family. Set the grace period to `00:00:00` for strict rotation.

## 5. Brute force

Identity lockout after 5 failures for 15 minutes; 10 credential requests per minute per IP on `auth/login` and `auth/register`; one uniform login error ("Invalid email or password.").

## 6. Bootstrap

- The migration creates schema only. On startup the API creates the three roles and `admin@admin.com` if absent, using the password from `Bootstrap:SuperAdmin:Password` (user secret; placeholder `<initial-superadmin-password>` in documentation).
- An existing admin is never reset. An existing account with that email but without the SuperAdmin role is logged and not promoted.
- With pending migrations the bootstrap is skipped with a warning; run `dotnet ef database update` and restart.
- No password change is forced on first login; change it (Profile → Change password) before any non-local deployment.

## 7. Local development

```powershell
cd backend
dotnet user-secrets set "Jwt:SigningKey" ([Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64))) --project src/Crm.Api
dotnet user-secrets set "Bootstrap:SuperAdmin:Password" "<initial-superadmin-password>" --project src/Crm.Api
```

- The API now touches the database at startup (bootstrap), so it fails to start when `ConnectionStrings:CrmDatabase` is missing. This replaces ARCH-001's "starts without a DB" behaviour.
- `Jwt:SigningKey` must be Base64 of at least 32 random bytes; startup validation stops the API otherwise.
- Cookies are `Secure` and work on `http://localhost:4200` in Chromium-based browsers and Firefox (localhost is a secure context). For Safari or other hosts use HTTPS (`dotnet dev-certs https --trust`, the API `https` profile, `ng serve --ssl`, proxy target `https://localhost:7080`). Never relax `Secure` / `SameSite` in code.

## 8. Deferred

Email verification (public signup does **not** verify email ownership), forgot-password, MFA / SSO, email change, session / device management, absolute refresh-family lifetime, custom roles / permissions. (Agent ticket permissions are implemented; see [ticket-management.md](ticket-management.md).)
