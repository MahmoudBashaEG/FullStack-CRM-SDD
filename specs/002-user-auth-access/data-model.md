# Data Model: Shared Local CRM Identity

Proposed design, 2026-10-09. Same CrmDbContext/SQL database/Windows identity as tickets. EF mappings remain Infrastructure; Domain and Application never inherit Identity/EF types. Explicit generated/reviewed migration files required; no EnsureCreated or runtime generation.

## LocalUser

Infrastructure IdentityUser<Guid> mapped to dbo.CrmUsers. Guid Id is stable CRM UserId; display name nvarchar(400), validated at most 200 Unicode scalars; Email/NormalizedEmail/UserName/NormalizedUserName nvarchar(256) required. Use email as username; normalized values have binary collation. Unique non-null NormalizedEmail and framework unique NormalizedUserName indexes. Identity normalization is trimmed NFC+invariant uppercase; passwords are never normalized.

PasswordHash/SecurityStamp/ConcurrencyStamp are standard framework fields; password hash required for supported creation paths, no plaintext columns. AccountRole required (Customer=0, Agent=1, Admin=2) with CHECK IN(0,1,2). One scalar directly enforces exactly one role; no AspNetRoles/UserRoles or editable role vocabulary. IsActive required true on creation. AuthVersion bigint positive, initial 1, increments on disable, never decreases. RowVersion binary(8) SQL generated for Admin edit version tokens. Identity ConcurrencyStamp also maintained through UserManager; SQL/admin conflicts map to 409, not silent overwrite. Identity optional external login/token/claim tables may be mapped by IdentityUserContext but expose no external-login or stored-token endpoints; session credentials live only in the schema below. Explicitly leave unsupported passkey/MFA features disabled.

Role is immutable in supported operations. Customer/Admin disabling/management is not exposed. Index account role+display name+Id for Agent listing (actual collation ordering deterministic with Id tie-breaker). No account deletion/cascade paths. A User is not a Customer business/profile entity.

## AuthenticationSession

Table dbo.AuthenticationSessions: Id Guid PK (sid), UserId FK CrmUsers.Id RESTRICT, AuthVersionAtCreation bigint, CreatedAtUtc/ExpiresAtUtc datetimeoffset(7), RevokedAtUtc nullable, RevocationReason bounded internal enum, RowVersion binary(8). CHECK expiry>created, version>0. Expiry seven days from signin, absolute; revocation terminal. Index UserId for account-wide revocation and Id/UserId for lookup. Session active only when not revoked/unexpired and account active/version matches. Independent cookie jars create independent sessions, each supported with one active browser tab. Multiple simultaneously active tabs sharing the same authentication session are outside the supported scope of this feature. No schema change or cross-tab locking entity follows from this assumption. Signout revokes only that family; disabling revokes every session of its account. Re-enable does not clear revocations.

## RefreshCredential

Table dbo.RefreshCredentials: Id Guid PK, SessionId FK RESTRICT, TokenHash binary(32) unique non-null, CreatedAtUtc/ExpiresAtUtc, ConsumedAtUtc nullable, ReplacedById nullable self-FK RESTRICT, RowVersion binary(8). Store SHA-256 digest of opaque random32-byte Base64Url credential only; no raw token or successor plaintext/ciphertext. Index SessionId; filtered unique SessionId WHERE ConsumedAtUtc IS NULL enforces at most one current generation. Consumed predecessors are retained until family expiry for replay detection. Mark old consumed before insert successor within one SQL transaction; then set successor link, commit. Consume must succeed once (locked row/version). No successor on revoked/expired state. Known consumed replay atomically revokes family. Token digest never logged/returned.

## InitialAdminSetup

Table dbo.InitialAdminSetup: singleton integer PK Id=1 with CHECK, AdminUserId Guid required unique FK RESTRICT, CompletedAtUtc datetimeoffset. Initial bootstrap transaction uses SERIALIZABLE key-range locking on singleton key before lookup/create and locks account identity uniqueness consistently. Creating Admin and completed marker is atomic; a completed marker yields already-complete without credential prompting or mutation. Never promote an existing account when normalized email conflicts. No password in marker/migrations.

## AgentReference compatibility

Existing dbo.AgentReferences keeps Id, Issuer, Subject, DisplayName, IsEligible and computed IdentityKey uniqueness. Add LocalUserId Guid nullable FK CrmUsers.Id RESTRICT with unique filtered index WHERE LocalUserId IS NOT NULL. Each new Agent gets a new reference Id, LocalUserId=UserId, Issuer=urn:crm:local:user, Subject=canonical UserId; never derive an Id by matching legacy name/email/external subject. Admin/Customer never receive references.

Migration leaves old LocalUserId null and sets old IsEligible=false; leaves Id/Issuer/Subject/DisplayName and all ticket/history data unchanged. Null links remain valid for saved tickets but cannot be newly selected. New-link adapters require role Agent and active state in addition to eligibility; cross-table role/active consistency is enforced transactionally by supported operations, not claimed as a SQL CHECK across tables. Category-only provisioning cannot mutate any agents.

Rename updates current user/reference display name atomically. Disable updates account version/stamp/active state, session revocation and reference eligibility atomically. Re-enable changes active/reference eligibility, not prior versions/sessions. Every create/update failure rolls back Identity and reference changes. Admin writes require expected rowversion; serialize changes by account lock. Unchanged display/state can return same version only after stale-version comparison.

## Ticket/history and actor identity

Ticket.AssignedAgentId continues referencing AgentReferences.Id; no ticket FK/schema rewrite. Existing history snapshots retain saved issuer/subject/name/reference values. CurrentActor translates new trusted local identity into existing Actor(urn:crm:local:user, canonical UserId, current display name). No caller can select actor. Renaming/disablement or issuer-key rollover must not rewrite saved events. Category and lifecycle/concurrency designs are unchanged.

## Migration and validation

One or more versioned migrations may stage schema creation/link addition and explicit legacy-ineligibility update; operation order must support fresh and populated databases. No password/Admin account seed; bootstrap runs separately. No full rollback promise that drops new accounts/sessions without backup. Document rollback/restore before deployment.

Verify fresh+populated migration, unchanged legacy IDs/ticket/history bytes, unique normalized email under racing writes, exact role CHECK, reference FK/uniqueness, session digest uniqueness/one-current generation, rollback injection and rowversion conflicts. Current Windows-integrated connection validation and existing SQL fixture ownership guards stay intact. No blanket optimistic/autoretry on uncertain commits.
