# security-administration — plan overview

Entry point for the **security-administration** feature. Stories execute in order by their `NN` prefix.

## Stories

| NN | File | Title | Tracker id | Depends on |
|----|------|-------|------------|------------|
| 14 | [14-story-security-administration.md](14-story-security-administration.md) | Security & Administration — User Management, Effective Deactivation, Read-Only Permission Matrix, Administrative Audit Logs & Organization Settings | ADMIN-001 (tracker: none) | Story 13 ([../reports-management/13-story-reports-management.md](../reports-management/13-story-reports-management.md), committed baseline), Story 02 ([../identity-access-management/02-story-users-roles-authentication-authorization.md](../identity-access-management/02-story-users-roles-authentication-authorization.md), JWT stamp/`IsActive` check, Agent provisioning), Stories 04–05, 09–12 (must not regress) |

## Dependency notes

- **Story 14** (`ADMIN-001`) delivers all four Security & Administration areas under approved decisions AD1–AD4. Access is SuperAdmin only.
  - **Accounts:** reuses AUTH-001.
    - The per-request `IsActive` + security-stamp check in `OnTokenValidated` already revokes JWTs issued before deactivation. It is verified, not rebuilt.
    - Agent creation stays on `POST /api/v1/agents` (role forced server-side).
    - The new audited `SetAccountStatusCommand` replaces `SetAgentStatusCommand`, serves both `/admin/users/{id}/status` and `/agents/{id}/status`, and adds row locks and anti-lockout (not self, not the last active SuperAdmin).
  - **Permissions:** a static, read-only Angular matrix transcribed from `docs/identity-and-access.md` §1. No endpoint, no table.
  - **Audit:** a new append-only `AdminAuditLogs` table with 4 actions, written in the same transaction as each change.
  - **Settings:** a new singleton `OrganizationSettings` (row 1 inserted by the migration, all null), with ETag/If-Match concurrency. It is informational only.
  - **Schema:** one additive migration, `AddAdministrationAuditAndSettings`. No new package.
- Shared contracts it touches:
  - New routes `/api/v1/admin/{users|users/{id}|users/{id}/status|audit-logs|organization-settings}`, all with the `SuperAdmin` policy.
  - `CreateAgentCommand` gains `ActorUserId`; `SetAgentStatusCommand` is removed (route kept).
  - `IdentityService.ThrowIfFailed` maps `ConcurrencyFailure` to 409.
  - The `EntityTags` 428 message becomes generic.
  - Infrastructure DI gets 3 registrations.
  - Angular: the `/admin` route group and 1 SuperAdmin `NAV_ITEMS` entry.
- Unchanged:
  - `SecurityRegistration`, `IIdentityService` (existing test fakes untouched) and the roles themselves (never changed after creation; REPORT-001 attribution depends on this);
  - ticket assignment/history, SLA, KB, AI, portal, reports, and every existing test.
- Later stories:
  - auditing Agent profile edits and password resets;
  - audit export/retention;
  - using organization settings in portal/branding (Platform);
  - **Communication Channels stays last** (invitations and outbound messages are not built here).
