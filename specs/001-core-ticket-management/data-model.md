# Data Model: Core Ticket Management

**Date**: 2026-10-08. Physical details below are inferred technical design, not extra business
input. The business attributes and lifecycle come from spec.md. No customer profile relation,
department/branch ownership, SLA, communication or escalation fields are introduced.

## Ticket Aggregate

| Property | Domain meaning | Persistence mapping |
| --- | --- | --- |
| Id | Internal ticket identity | uniqueidentifier primary key, generated once |
| Reference | Stable public reference | nvarchar(36), unique exact-match index; TKT- plus 32 uppercase GUID digits |
| Subject | Required issue summary | Unicode nvarchar(max); nonblank after trimming |
| Description | Required issue details | Unicode nvarchar(max); nonblank after trimming |
| CategoryId | Current classification | Required stable category key, foreign key |
| Priority | Low, Normal, High, Urgent | Stable string code with check constraint |
| Status | Open, InProgress, Resolved, Closed | Stable string code with check constraint |
| AssignedAgentId | Current responsible agent or unassigned | Nullable foreign key |
| CreatedAtUtc | Creation instant | datetimeoffset, immutable |
| UpdatedAtUtc | Most recent successful change instant | datetimeoffset; unchanged on no-op/failure |
| Revision | Ordered successful mutation count | bigint; creation 1, increment once per changed operation |
| RowVersion | Infrastructure concurrency token only | SQL rowversion shadow property; never a Domain dependency |

Private setters and mutation methods enforce business invariants. Subject/description are
plain text; preserve Unicode. Trim surrounding whitespace consistently before validation and
comparison, preserve interior whitespace, and compare stored editable text ordinally for no-ops.
No extra business length limit is inferred; request-size protection is an operational setting.
Defaults: Open, Normal, Uncategorised, null agent. Reference/created time cannot be edited.
The domain receives trusted actor/time and validated reference selections from Application.

## Category Reference

| Property | Meaning |
| --- | --- |
| Id | Stable GUID label identity |
| DisplayName | Unicode business label |
| IsAvailable | Whether the category may be selected now |

Migration seeds one fixed Uncategorised ID with IsAvailable=true. Operator provisioning may
supply other approved labels; no invented category taxonomy. Retain unavailable references
for existing-ticket reads and unchanged retained selections; reject only newly selected unavailable IDs. Uncategorised has fixed ID 00000000-0000-0000-0000-000000000001; it cannot be renamed, deactivated or deleted while it is the default. SQL CHECK enforces availability for this ID; supported provisioning rejects protected changes and never deletes rows. No category CRUD UI.

## Eligible Agent Reference

| Property | Meaning |
| --- | --- |
| Id | Stable GUID used by assignment/filtering |
| Issuer and Subject | Externally supplied identity key, unique as a pair |
| DisplayName | Unicode staff label |
| IsEligible | Whether new assignment is allowed |

This is a reference directory, not user accounts or credentials. Source identities and
eligibility from an operator-provisioned input; no self-registration or permission management.
Disable eligibility without deleting historical references. An actor with queue access need
not be an eligible assignee; these are different concepts. Retained inactive assignment stays
readable and does not prevent unrelated edits, no-ops, closure or reopening. Reassignment checks eligibility only if the new non-null ID differs from the saved ID. Clearing an inactive assignment is allowed; selecting an inactive ID again after replacement is rejected.

## Ticket History Entry

| Property | Meaning |
| --- | --- |
| Id | Stable GUID entry identity |
| TicketId | Owning ticket, foreign key with delete restricted |
| Revision and Sequence | Revision bigint serialized as decimal string; Sequence positive int; unique together per ticket |
| Kind | Created, DetailsChanged, AssignmentChanged, StatusChanged |
| Field | Changed field name; null for Created |
| PreviousValue and NewValue | Typed JSON values; null represents unassigned/no previous value |
| ActorIssuer and ActorSubject | Trusted stable identity of responsible staff |
| ActorDisplayName | Historical display snapshot, fallback to subject |
| OccurredAtUtc | Time supplied once for the operation |

Creation stores a snapshot of initial business values. For each actual changed detail emit
one entry; assignment/status operations emit one entry. Several changes from one operation
share revision, actor and time; Sequence is deterministic within that revision. History
values for category/agent include ID and label snapshot, so later label changes do not rewrite
the account. History does not depend on a live actor-directory join. EF maps JSON to Unicode
columns with serialization owned by Infrastructure; Domain change values use simple typed
values without EF or HTTP types. Revision/Sequence, not identical timestamps, determine order. The complete field/value discriminators are defined in contracts/tickets-rest.md; use exactly that schema. Do not load all existing history for a mutation: load the ticket/current revision, then append only pending entries.

Append only through the ticket mutation unit. No entry editing/deletion, no event replay,
no separate business audit system. Failed/no-op changes create no entry; rollback removes all
pending changes. Customer/credential content is never copied into logs.

## Physical Object Names and Shared Connection

EF maps Ticket -> dbo.Tickets, TicketHistoryEntry -> dbo.TicketHistoryEntries,
CategoryReference -> dbo.CategoryReferences, AgentReference -> dbo.AgentReferences,
with migration metadata in dbo.__EFMigrationsHistory. Names/mappings and business fields are
unchanged. [database-principals.md](contracts/database-principals.md) now defines one local
Windows-authenticated connection/identity for runtime, migrations and provisioning, rather than
separate database rights. History/CLI restrictions are application guarantees, not denied SQL DML.

## Relationships and Constraints

- Ticket belongs to one category and zero/one assigned agent; it owns many history entries.
- Foreign keys preserve readable references; no cascaded deletion of history/reference records.
- Unique Ticket.Reference and unique TicketHistory(TicketId, Revision, Sequence).
- Index UpdatedAtUtc DESC, Reference ASC for tracking; selective status/category/priority/agent
  indices based on query plans at the stated test volume, not speculative exhaustive indexing.
- History index TicketId, Revision, Sequence; actor identity uses case-sensitive external keys.
- Subject collation Latin1_General_100_CI_AS_SC supports Unicode and case-insensitive substring
  matching. Reference uses a binary comparison for exact matching; tests cover Arabic content,
  Latin casing and literal wildcard characters. Parameterize queries and escape LIKE operands.
- Reference lookups validate only newly selected IDs during the mutation transaction. Retained
  inactive labels/assignments stay readable on unrelated changes.

## Lifecycle and No-Ops

| Current | Allowed different status |
| --- | --- |
| Open | InProgress, Resolved |
| InProgress | Resolved |
| Resolved | Closed, Open |
| Closed | Open |

All same-status requests are no-ops after authorization/concurrency checks. All other unequal
pairs are rejected. Closed details/assignment edits are rejected even if supplied values are
identical. Reopening preserves reference/details/assignment and adds a status history entry.
No assignee, customer approval, resolution note or automatic transition is required.

## Mutation Consistency

1. Authorize; validate payload and expectedVersion (required canonical base64 of 8 bytes).
2. Infrastructure mutation scope starts its sole transaction and locks the existing ticket.
   Compare the supplied token to the shadow rowversion before any no-op decision.
3. Merge omitted detail fields from saved values. Compare category/agent IDs, not labels or
   flags. Check only newly selected category/agent IDs under transaction-held reference locks.
   Clearing an assignment and status changes need no eligibility validation of retained IDs.
4. Apply domain rules. Closed details/assignment edits still reject; equal status is a no-op.
5. No-op completes lock scope without SaveChanges, new history, updated time or rowversion.
6. Real changes increment Revision once, append actual-field entries, save ticket/history once
   and commit. Creation selects an available category and creates revision 1 with initial history.
7. Return committed detail with editVersion. No ETag/Last-Modified or If-Match semantics.
   A reference-label/flag update does not change this aggregate token and cannot create history.

Infrastructure registers a shared scoped DbContext; DI disposes it. ITicketMutationStore owns transaction commit/rollback/disposal. Application orchestrates
through feature ports. Reads of new reference selections share that transaction. No nested
transaction or implicit EF execution strategy; EnableRetryOnFailure is not configured. No
automatic command or HTTP replay. Deadlock/timeout is a transient failure, not a stale-version
conflict. Rollback/disposal still executes after cancellation. Unknown commit outcome requires
refresh. Token/history representations must be built from the committed state, not a later
uncoordinated ticket requery that could pair another writer's token with this operation's values.

## Migrations and Reference Provisioning

Infrastructure owns mappings/migrations. Create ticket/history/reference storage and seed the
fixed Uncategorised ID with IsAvailable=true. Add a CHECK for fallback availability; migrations
cannot remove the fallback while this feature requires it. Supported tooling has no deletion mode,
and supported application operations never delete reference rows. The shared Windows identity
is not restricted by separate process-specific database grants; direct SQL is outside supported
reference changes. All supported operator tooling must preserve the fallback.

[reference-provisioning.md](contracts/reference-provisioning.md) defines explicit bounded upserts,
immutable agent issuer/subject keys, retained omitted rows, and all-or-nothing validation. Only
synthetic fixtures are versioned; no real identity input or credentials. Provisioning changes
references only, never tickets, revision, updated time or ticket history. No invented production
agents, runtime admin route or general import/synchronization subsystem.
