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
| `PUT` / `DELETE /api/v1/admin/organization-settings/logo` (PLAT-001, If-Match required) | ✔ | 403 | 403 | 401 |
| `GET /api/v1/branding`, `GET /api/v1/branding/logo` (PLAT-001, public branding only) | anonymous | anonymous | anonymous | 200 (logo 404 when none) |

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

Singleton row (Id 1, inserted by the migration with all values NULL = "not configured"). Fields: display name (required, ≤ 150), support email (optional, valid, ≤ 256), support phone (≤ 40), support information (≤ 1000), and (PLAT-001) the accent color preset and the logo. `ETag` / `If-Match` (missing 428, malformed 400, stale 409). Identical values change nothing and write no audit. The support values are informational only; the display name, accent and logo are shown by the UI (see [platform-ui-and-localization.md](platform-ui-and-localization.md)).

**Branding (PLAT-001)**
- `accentColor`: one of `violet` (default, stored as NULL), `blue`, `azure`, `green`, `orange`, `rose`; anything else is 400. **An update that omits `accentColor` (or sends null) keeps the saved accent**, so older clients cannot reset it; send a preset id to change it.
- Logo: `PUT /admin/organization-settings/logo` (multipart field `file`, If-Match). PNG, JPEG or WebP only, ≤ 512 KB; the extension **and** the magic bytes must agree and the stored content type comes from the signature, never from the client. Bytes live in the singleton table `OrganizationLogos` (never in `OrganizationSettings`, never shared with customer attachments); no file name is stored. `DELETE` removes it (no logo ⇒ 200 unchanged, no audit).
- `logoVersion`: an integer counter on the settings row that increments on **every** successful logo replacement and never resets, so each replacement (even rapid or identical) has a distinct public URL `/branding/logo?v=<logoVersion>`.
- Public, anonymous `GET /api/v1/branding` returns exactly `displayName`, `accentColor`, `logoVersion` (null = no logo). `GET /api/v1/branding/logo` returns only the `OrganizationLogos` row with `X-Content-Type-Options: nosniff`, `Content-Security-Policy: default-src 'none'; sandbox`, `Content-Disposition: inline` and `Cache-Control: public, max-age=86400`.
- Audit: the same `OrganizationSettingsUpdated` action (no new action). New audited fields: `accentColor` (old/new preset id) and `logo` (old/new description such as `image/png, 48 KB`, never bytes or a file name). Each mutation is one `SaveChanges` (settings UPDATE + logo INSERT/UPDATE/DELETE + audit INSERT).

## 6. Routes

API: see section 1. Angular (SuperAdmin only): `/admin/users`, `/admin/users/:id`, `/admin/permissions`, `/admin/audit-logs`, `/admin/settings`.

## 7. Follow-ups

Audit Agent profile edits and password resets; audit retention/export; use the settings in the portal or branding; Communication Channels (final module).
