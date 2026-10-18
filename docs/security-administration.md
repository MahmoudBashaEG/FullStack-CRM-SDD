# Security & Administration (ADMIN-001)

## 1. Scope

SuperAdmin-only administration on top of the existing identity stack. Roles stay fixed (SuperAdmin, Agent, Customer); no role-changing code path exists.

| Operation | SuperAdmin | Agent | Customer | Anonymous |
|---|---|---|---|---|
| `GET /api/v1/admin/users`, `GET /admin/users/{id}` | ✔ | 403 | 403 | 401 |
| `PUT /api/v1/admin/users/{id}/status` | ✔ (not self, not the last active SuperAdmin) | 403 | 403 | 401 |
| `POST /api/v1/agents` (role forced to Agent, audited) | ✔ | 403 | 403 | 401 |
| `PUT /api/v1/agents/{id}/status` (Agent ids only, same audited command) | ✔ | 403 | 403 | 401 |
| `GET /api/v1/admin/audit-logs` | ✔ | 403 | 403 | 401 |
| `GET` / `PUT /api/v1/admin/organization-settings` | ✔ | 403 | 403 | 401 |

## 2. Accounts

| Situation | Result | Audit |
|---|---|---|
| Deactivate an active account | 200 `changed: true`; stamp rotated, refresh sessions revoked | `AccountDeactivated` |
| Reactivate | 200 `changed: true`; no token restored (fresh login) | `AccountReactivated` |
| Same state as requested | 200 `changed: false`; nothing written | none |
| Deactivate yourself | 409 | none |
| Deactivate the last active SuperAdmin | 409 | none |
| Unknown id (or non-Agent id on `/agents/{id}/status`) | 404 | none |
| Missing `isActive` | 400 | none |

- **Revocation:** `JwtBearerEvents.OnTokenValidated` re-reads the account on every authenticated request and fails the token when it is missing, inactive or the stamp differs. Verified with retained Agent and Customer tokens: 401 with no account detail. Login and refresh are refused for inactive accounts.
- **Concurrency:** the handler locks the target `AspNetUsers` row (`UPDLOCK, HOLDLOCK`) and, for a SuperAdmin target, all SuperAdmin rows. A concurrent duplicate becomes a no-op. Mutual SuperAdmin deactivation may deadlock; the retry strategy re-runs the victim, which then sees the committed state.
- **Initial access:** the SuperAdmin types the initial Agent password in `/agents/new`; it is hashed, never echoed, never logged and never audited. No forced change on first login.
- **Tickets of a deactivated Agent** are unchanged; reassign them through Tickets.
- The bootstrap SuperAdmin is never reactivated or recreated by the bootstrapper if it exists.

## 3. Permission matrix

Static data in `frontend/src/app/features/admin/permission-matrix.ts`, display only (no endpoint, no permission store). When a backend permission changes, edit that file and [identity-and-access.md](identity-and-access.md) together.

## 4. Audit

- Table `AdminAuditLogs`, append-only through the CRM (no update/delete API). Database operators are outside the CRM permission model, so the log is **not** physically immutable.
- Exactly four actions: `AgentCreated`, `AccountDeactivated`, `AccountReactivated`, `OrganizationSettingsUpdated`.
- Fields: actor id, target type/id/label (email or "Organization settings"), UTC time, list of `{field, oldValue, newValue}`. Values are truncated to 200 characters (never splitting a surrogate pair). `Changes` is JSON in an `nvarchar(max)` column: JSON escaping can multiply size (a control character becomes 6 characters), so a fixed 4000 limit could overflow.
- Atomic: each entry commits in the same transaction as its change (`ITransactionalCommand` for Agent creation and status changes; one `SaveChanges` for settings).
- Not audited (follow-up): Agent profile edits, password resets, logins, ticket/KB/SLA/AI actions. The log starts with ADMIN-001; nothing is backfilled.
- Filters: action, UTC `from`/`to` (inclusive days, `yyyy-MM-dd`), search (actor/target id, name, email, label), paging (default 20, max 100). Newest first.

## 5. Organization settings

Singleton row (Id 1, inserted by the migration with all values NULL = "not configured"). Fields: display name (required, ≤ 150), support email (optional, valid, ≤ 256), support phone (≤ 40), support information (≤ 1000). `ETag` / `If-Match` (missing 428, malformed 400, stale 409). Identical values change nothing and write no audit. Informational only: nothing else reads these values.

## 6. Routes

API: see section 1. Angular (SuperAdmin only): `/admin/users`, `/admin/users/:id`, `/admin/permissions`, `/admin/audit-logs`, `/admin/settings`.

## 7. Follow-ups

Audit Agent profile edits and password resets; audit retention/export; use the settings in the portal or branding; Communication Channels (final module).
