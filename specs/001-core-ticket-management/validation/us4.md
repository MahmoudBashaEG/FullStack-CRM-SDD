# US4 ticket lifecycle checkpoint: T056–T065

Validated 2026-10-08, continuing committed T001–T055 (`52cc021`). T056–T065 are complete. T066–T081 were not started. Specification, plan, research, data model, contracts, quickstart and constitution are unchanged.

## Delivered behavior and SC-003 evidence

The Domain method enforces the exact transition matrix below. Application validates canonical status/version input, supplies trusted actor/time, and orchestrates through the existing transaction-owning mutation store. The protected PUT `/api/tickets/{reference}/status` accepts only expectedVersion/status; its controller contains no lifecycle rules or persistence access.

| Current → requested | Open | InProgress | Resolved | Closed |
| --- | --- | --- | --- | --- |
| Open | No-op | Changed | Changed | Rejected |
| InProgress | Rejected | No-op | Changed | Rejected |
| Resolved | Changed (reopen) | Rejected | No-op | Changed |
| Closed | Changed (reopen) | Rejected | Rejected | No-op |

Domain and real SQL/API tests independently cover all 16 pairs: six successful unequal transitions, six rejected unequal transitions, and four same-status no-ops. This proves SC-003's transition matrix for the checkpoint. Reopen tests preserve ID/reference, subject/description, category, priority, assignment, creation time and prior history. No assignee, resolution note, customer approval, automatic transition or escalation is required or introduced.

| Requirement | Verification |
| --- | --- |
| FR-008 | Exact matrix in Domain, application cases and 16 SQL/API cases; a browser journey exercises all six allowed edges. Another unassigned journey resolves/closes/reopens without assigning an agent. |
| FR-009 | Closed details/assignment reject even identical requests. UI fieldsets are read-only while the status action permits reopening. Inactive saved category/agent references remain through progress, resolution, closure and reopen. |
| FR-010 | StatusChanged is readable now through GET history and the detail UI with field=status, canonical string before/after values, trusted actor/time, decimal-string revision and sequence 1. Earlier creation/assignment/detail history remains readable and unchanged. |
| FR-011 | Real changes and one status history entry commit together. No-ops preserve version/update time/history; invalid transitions, failed history save and cancellation leave persisted ticket/history unchanged. |
| FR-012 | Stale same-status and invalid-transition requests get ticket_changed before Domain/no-op decisions. Competing lifecycle writes have one winner and a consistent saved detail/token. UI preserves status/edit drafts, then requires explicit reload/review/allowed resubmission. A deliberately lost response proves commit uncertainty without automatic replay. |
| FR-013/014 | Genuine signed JWT tests cover denied, expired, wrong-issuer/wrong-audience and anonymous status requests before validation/existence. Both authorized staff can act on the shared queue. Unknown/duplicate fields, unsupported media, invalid status and missing ticket return their contracted errors. No future module/admin/delete route was added. |

Status operations do not invoke category/agent selection functions or eligibility checks. The existing mutation store still owns the sole transaction, ticket UPDLOCK/HOLDLOCK, version comparison before business decisions, EF shadow rowversion predicate, pending-history save and cancellation-safe cleanup. No retry strategy, transaction pipeline or new persistence abstraction was added.

The existing TicketTextValue serializer/projection already supports scalar status strings; SQL/API assertions prove the correct payload rather than adding a redundant serializer or DTO. The TypeScript history union/rendering now includes StatusChanged and human-readable status labels. No schema/migration change is needed.

The feature-local lifecycle form offers only allowed different statuses and submits on explicit action. Successful responses refresh detail/history/choices; the existing queue page fetches current summaries on return. Closed detail/assignment controls are disabled, but the status session's guard allows a reviewed reopen. On conflict reload, a now-invalid pending status remains visible as a disabled draft choice until staff choose an allowed action; drafts are not silently merged or replayed.

## Test-first execution and results

| Check | Result |
| --- | --- |
| T056 domain red run | All 20 new cases failed against the compiling lifecycle stub |
| T057 application red run | All 19 new cases failed against the compiling handler stub |
| T057 SQL/API red run | 38 failures against missing status behavior; one duplicate-JSON case was already rejected by existing middleware |
| Frontend red run | 13 new cases failed against compiling lifecycle/transport/session stubs; all 46 earlier cases passed |
| Closed-control regression red run | The new read-only detail-page case failed before the fieldset correction; five existing page cases passed |
| `dotnet build Crm.slnx --no-restore` | Passed, zero warnings/errors |
| `dotnet test Crm.slnx --no-build` with a fresh CRM_TEST_SQL_CONNECTION | Domain 33, Application 70, real SQL/API 127 passed: 230 tests, zero failures/skips |
| Lifecycle SQL/API suite | All 39 passed, including the full matrix, preserved inactive references, stale no-op precedence, concurrent writers, exact history actor/time/value shapes, failed-save rollback and cancellation |
| `npm test --prefix src/frontend -- --watch=false` | All 60 passed, including actual DOM submission, exact choices, closed controls/reopen, preserved draft selection, 409 distinction and transport uncertainty |
| `npm run build --prefix src/frontend` | Production build passed |
| `npm run e2e --prefix src/frontend` | All 13 passed: nine earlier-story regressions and four lifecycle journeys; final run also confirms the status tracking warning is removed |
| `dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build` | No changes since the last migration |
| Migration execution | Existing InitialTicketStorage applied by owned integration/browser fixtures; no new migration or development-catalog change |
| Architecture scope | Project dependencies remain inward; controllers dispatch MediatR only. ArchitectureTests remains the existing scaffold with no discovered tests; T073 was not implemented or claimed as automated architecture coverage |
| Final cleanup | Zero CrmTicketsIntegration_/CrmTicketsE2E_ catalogs, zero temporary e2e authentication directories, zero listeners on 4200/7043 |

Both backend test waves (T056/T057) were authored and demonstrated failing before T058 implementation. Frontend tests were also authored before their paired implementation and completed after T062. Task IDs, definitions and ordering were retained; only authorized completion status and the completion-summary metadata changed. No permanent stubs, skipped suites, reduced assertions or increased browser retry counts/timeouts remain.

## Reproduction

Run from repository root after sourcing `scripts/environment.ps1`. A new owned test catalog is required each time; existing catalogs are rejected rather than reset.

```powershell
. ./scripts/environment.ps1
dotnet build Crm.slnx --no-restore
$taskCatalog = 'CrmTicketsIntegration_' + [guid]::NewGuid().ToString('N')
$env:CRM_TEST_SQL_CONNECTION = "Server=localhost;Database=$taskCatalog;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
dotnet test Crm.slnx --no-build --logger 'console;verbosity=minimal'
npm test --prefix src/frontend -- --watch=false
npm run build --prefix src/frontend
$taskCatalog = 'CrmTicketsE2E_' + [guid]::NewGuid().ToString('N')
$env:CRM_E2E_SQL_CONNECTION = "Server=localhost;Database=$taskCatalog;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
npm run e2e --prefix src/frontend
dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build
```

The API, EF tooling, provisioning CLI and fixtures retain the same configured local SQL Server connection/Windows account within each run. Tests change only the catalog and use guarded ownership cleanup. No SQL usernames/passwords, principals, grant scripts or runtime identity changes were introduced. Browser startup retains the same protected reference-data readiness endpoint, signed Testing-only JWTs, HTTPS/proxy infrastructure and zero retries.

## Problems discovered and corrected

1. ASP.NET's existing JSON compatibility check accepted application/merge-patch+json, which is outside the custom application/json mutation contract. The SQL case initially returned 404 instead of 415. The shared HTTP check now compares the parsed media type exactly, accepting application/json with normal parameters while rejecting unsupported JSON variants. This is a required correction to an existing dependency, not a contract change; all prior backend/browser tests still pass.
2. Existing closed-ticket forms blocked save buttons but left controls editable. A new DOM-level detail test failed, then native disabled fieldsets made information/assignment read-only until reopen while preserving the lifecycle action. Earlier form assertions remain intact.
3. The first passing browser run reported unnecessary reconstruction of the tiny status option collection when every allowed choice changed. Tracking bounded option positions removed that warning; the draft-selection DOM assertion and final browser run verify selection remains preserved.
4. The existing closed edit-session guard could not govern reopening. A separate status guard retains busy/conflict/review protection while leaving closed detail/assignment restrictions intact.

## Assumptions, deviations and limits

- No new business assumptions or product-design deviations were introduced. The specified four-state workflow and current inactive-reference decisions remain authoritative.
- Prepared test states use reflection/direct SQL only in isolated fixtures to arrange independent matrix cases; production state changes use the new Domain/command/API path. No fixture/admin HTTP endpoint was introduced.
- New history kinds reuse the existing immutable scalar-value persistence/projection. Infrastructure and migrations remain unchanged because they already satisfy the status payload/atomicity contract.
- Existing OIDC issuer/client deployment configuration remains external. Tests exercise real signed JWT validation via the existing Testing-only harness; external IdP sign-in itself is not claimed as evaluated.
- No remaining blocker or scope/dependency-order deviation was found for this checkpoint. Comprehensive US5 history review, architecture/security/platform final checks and actual SC-005 staff evaluation remain T066+ work.
- `.specify/extensions.yml` is absent; no pre/post implementation hooks are registered.
- Changes remain uncommitted at checkpoint completion.

## Created files

- `src/backend/Crm.Domain/Tickets/InvalidTicketTransitionException.cs`
- `src/backend/Crm.Application/Tickets/Commands/ChangeTicketStatusCommand.cs`
- `src/backend/Crm.Application/Tickets/Validation/ChangeTicketStatusValidator.cs`
- `src/backend/Crm.Api/Binding/TicketStatusRequest.cs`
- `src/backend/Crm.Api/Controllers/TicketStatusController.cs`
- `tests/backend/Crm.Domain.Tests/Tickets/TicketLifecycleTests.cs`
- `tests/backend/Crm.Application.Tests/Tickets/ChangeTicketStatusTests.cs`
- `tests/backend/Crm.IntegrationTests/Tickets/TicketStatusTests.cs`
- `src/frontend/src/app/features/tickets/models/ticket-lifecycle.ts`
- `src/frontend/src/app/features/tickets/components/ticket-status.component.ts`
- `src/frontend/src/app/features/tickets/components/ticket-status.component.spec.ts`
- `src/frontend/e2e/tickets/lifecycle.spec.ts`
- `specs/001-core-ticket-management/validation/us4.md`

## Modified files

- `src/backend/Crm.Domain/Tickets/Ticket.cs`
- `src/backend/Crm.Api/HttpConfiguration.cs`
- `src/frontend/src/app/features/tickets/data-access/tickets.service.ts`
- `src/frontend/src/app/features/tickets/models/ticket-history.models.ts`
- `src/frontend/src/app/features/tickets/components/ticket-history.component.ts`
- `src/frontend/src/app/features/tickets/components/ticket-edit-form.component.ts`
- `src/frontend/src/app/features/tickets/components/ticket-assignment.component.ts`
- `src/frontend/src/app/features/tickets/pages/ticket-detail.page.ts`
- `src/frontend/src/app/features/tickets/pages/ticket-detail.page.spec.ts`
- `src/frontend/src/app/features/tickets/services/ticket-edit-session.service.ts`
- `src/frontend/src/app/features/tickets/services/ticket-edit-session.service.spec.ts`
- `scripts/complete-task.ps1`
- `specs/001-core-ticket-management/tasks.md`
