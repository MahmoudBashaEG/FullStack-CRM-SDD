# REST Contract: Core Ticket Management

Revised 2026-10-08 against confirmed spec A-011/A-012. Production routes are HTTPS/JSON and
protected by SupportQueue: trusted JWT issuer/audience/signature/expiry, stable issuer/subject,
and crm_support_queue=shared. Assignment neither grants nor restricts access. Authorize before
existence checks. No deletion, escalation, communication or administration routes.

## Common Conventions

Base `/api`; ticket path key is its stable public reference. GUIDs are strings; times UTC
ISO-8601. Priority codes Low/Normal/High/Urgent; status Open/InProgress/Resolved/Closed.
Codes are case-sensitive; display labels may be localized. Text is plain Unicode. Trim subject/
description around the edges before validation/no-op comparison; preserve interior whitespace.
Server owns reference, actor, times, saved version and history. Reject unknown writable fields.
Only expectedVersion is accepted as concurrency metadata; clients cannot set editVersion.

Errors use application/problem+json with type, title, status, code, traceId and optional field
errors. Standard authentication challenges/forbids may have an empty body. No ticket data,
secrets or exception internals in diagnostics. Responses use Cache-Control: no-store and no
ETag or Last-Modified. No conditional-header contract; If-Match is not a substitute for a body
version. Approved CORS origins allow Authorization/Content-Type and expose Location.

## Ticket Representations and Edit Version

Detail has reference, subject, description, category: { id, displayName, isAvailable },
priority, status, assignedAgent: null or { id, displayName, isEligible }, createdAtUtc,
updatedAtUtc and editVersion. List summaries omit description/createdAtUtc/editVersion and
include reference, subject, category, priority, status, assignedAgent and updatedAtUtc.

editVersion is canonical base64 of the SQL ticket rowversion (8 bytes), with no surrounding
quotes. It is an opaque aggregate edit token, not an HTTP validator for the whole response.
Category/agent label or active-flag changes can change the detail representation without
changing editVersion. They do not mutate the ticket or produce ticket history. Clients read
current labels/flags anew and compare reference IDs, never labels or flags, when submitting.
Every successful mutation returns committed detail/editVersion; no-op returns unchanged token.
History has its own paged read and is displayed alongside details.

Page: { items, page, pageSize, hasMore }. Page starts at 1; default pageSize=25, max=100.
Invalid paging is 400. No total count. Concurrent changes can move entries between offset
pages; no snapshot across requests is promised. Empty pages are 200 with [] and hasMore=false.

## Endpoints and Payloads

| Method/path | JSON body or query | Success |
| --- | --- | --- |
| POST /tickets | subject, description; optional categoryId/priority | 201 detail, Location |
| GET /tickets/{reference} | none | 200 detail |
| GET /tickets | q/status/categoryId/priority/assignment/page/pageSize | 200 summary page |
| PATCH /tickets/{reference}/details | expectedVersion; at least one of subject/description/categoryId/priority | 200 detail |
| PUT /tickets/{reference}/assignment | expectedVersion, agentId (GUID or null) | 200 detail |
| PUT /tickets/{reference}/status | expectedVersion, status | 200 detail |
| GET /tickets/{reference}/history | page/pageSize | 200 history page |
| GET /ticket-reference-data/categories | none | 200 available category ID/name array |
| GET /ticket-reference-data/agents | none | 200 eligible agent ID/name array |

Creation defaults to Open/Normal/Uncategorised/unassigned. Omitted optional values use defaults;
explicit null or invalid choices are rejected. Subject/description must be nonblank. The
protected fallback always remains available even when all other categories are unavailable.

PATCH details uses the documented custom application/json object, not JSON Patch/Merge Patch.
Advertise Accept-Patch: application/json. Omitted editable fields are unchanged; explicit null
for any detail field is invalid. Merge with saved values, validate and apply all-or-nothing.
PUT assignment/status replaces all writable subresource state: agentId/status is required,
including explicit agentId=null to unassign. No HTTP validator is emitted after normalization.
Unsupported media type is 415. Duplicate JSON properties and unknown fields are 400.

## Retained Inactive References

Compare requested IDs to saved IDs after authorization/version checks:

- Same category or agent ID means retention, even when explicitly resubmitted; do not check
  its current availability/eligibility. Unrelated edits/no-ops must not fail because of it.
- Different category ID must currently exist and be available; a new non-null agent ID must
  currently exist and be eligible. Formerly used inactive IDs cannot be selected again.
- agentId=null clears any saved assignment, including inactive, with no replacement check.
- Status changes perform no availability/eligibility check on unchanged references.
- No automatic clearing/reclassification on inactivation. Closed details/assignment restrictions
  and stale edit rejection still apply. Invalid new selections reject the entire operation.
- Lookup lists contain selectable references only; current inactive values remain visible on
  ticket detail and may remain selected. Administration/provisioning is not exposed over HTTP.

## Search and Filters

q is trimmed; empty means no search. Match exact generated reference OR case-insensitive
subject substring, with percent/underscore/brackets treated literally. AND this search with
all selected filters. status/categoryId/priority each accept one value. assignment is omitted
(all), unassigned, or a GUID; historical inactive references can be used as filters.
Reject invalid enum/ID syntax and duplicate query keys; well-formed unknown reference IDs
return an empty list. List order updatedAtUtc descending then reference ascending; history
order revision numerically ascending then sequence ascending.

## Version Conflicts and Errors

Every existing-ticket mutation requires expectedVersion from the last detail/mutation response.
Missing/null/malformed/noncanonical/not-8-byte base64 is 400 invalid_version with field errors.
Well-formed but unequal to the saved rowversion is 409 ticket_changed, including stale no-ops.
No 412/428 precondition semantics are used. On conflict preserve unsaved inputs, retrieve current
state, review and explicitly resubmit. Never auto-merge/replay. A lost response may conceal a
successful commit; refresh before deliberate retry. No-op preserves time/token/history.

| Status | Code | Meaning |
| --- | --- | --- |
| 400 | validation_failed, invalid_version | Shape, required content, enum or edit-version syntax |
| 401 | unauthenticated | Missing/invalid/expired JWT; standard challenge |
| 403 | queue_access_denied | Authenticated but lacks explicit queue access |
| 404 | ticket_not_found | Authorized caller requests missing ticket |
| 409 | ticket_closed, invalid_transition | Closed edit or prohibited lifecycle |
| 409 | category_unavailable, agent_ineligible | Newly selected reference unknown/inactive |
| 409 | ticket_changed | Stale aggregate edit version |
| 415 | unsupported_media_type | Unsupported mutation content type |
| 503 | service_unavailable | Dependency failure/deadlock/lock timeout; no replay |
| 500 | unexpected_error | Sanitized unexpected failure |

Cancellation may end the connection without a response. Atomicity protects persisted consistency,
not a guarantee that a lost response means rollback. Return no saved values/existence details to
unauthorized callers. A failed known-rollback save creates no ticket history.

## Complete Ticket History Payload

Each entry requires id (GUID), revision (positive decimal string), sequence (positive integer),
kind, field, previousValue, newValue, actor: { issuer, subject, displayName }, occurredAtUtc.
All actor fields are nonblank strings from trusted identity; displayName falls back to subject.
Revision is a string to avoid bigint precision loss in TypeScript. Sequence starts at 1.
One successful operation increments revision once; its entries share actor/time/revision.
Detail fields use deterministic sequence order subject, description, category, priority.

| kind | field | previousValue/newValue JSON shape |
| --- | --- | --- |
| Created | null | previousValue=null; newValue=initial snapshot below |
| DetailsChanged | subject or description | strings, including previous/new normalized text |
| DetailsChanged | category | { id: GUID, displayName: string } snapshots |
| DetailsChanged | priority | one of the four priority strings |
| AssignmentChanged | assignedAgent | null or { id: GUID, displayName: string } snapshots |
| StatusChanged | status | one of the four status strings |

Initial snapshot requires reference, subject, description, category: { id, displayName },
priority, status and assignedAgent (null for initial creation). It excludes editVersion,
active flags and activity metadata, which belong to other fields. Created is revision "1",
sequence 1. Changed category snapshots are non-null; assignment null means unassigned.
Creation/null previousValue is distinct from an assignment change whose old/new value is null.
No entries with equal old/new business values. Snapshot labels reflect values at that event,
never a live directory join. Later reference changes cannot rewrite any old entry.

Example assignment change, with synthetic IDs:

```json
{
  "id": "10000000-0000-0000-0000-000000000001",
  "revision": "2",
  "sequence": 1,
  "kind": "AssignmentChanged",
  "field": "assignedAgent",
  "previousValue": null,
  "newValue": { "id": "20000000-0000-0000-0000-000000000001", "displayName": "Test Agent" },
  "actor": { "issuer": "https://example.test", "subject": "staff-a", "displayName": "Test Staff" },
  "occurredAtUtc": "2026-10-08T09:00:00Z"
}
```

No edit/delete history endpoints. Append only pending changes; no event replay or generic audit
system. Persist history in the ticket transaction; failed/no-op operations produce no entries.

## Lifecycle and Traceability

Open -> InProgress/Resolved; InProgress -> Resolved; Resolved -> Closed/Open; Closed -> Open.
Equal status is a no-op after version checks. Other unequal pairs are rejected. Reopen preserves
reference/details/assignment, even if inactive. No required assignee/customer approval/resolution
note or automatic transition. Assignment/priority never implicitly change status.

FR-001/002/003 -> create/detail; FR-004 -> list; FR-005/006 -> patch/category/fallback rules;
FR-007 -> assignment; FR-008/009 -> status; FR-010/011 -> history/atomic results; FR-012 -> body
versions; FR-013 -> every protected route; FR-014 -> absent operations. SC-001 through SC-004
are covered by these operations; SC-005 is a separate staff evaluation.
