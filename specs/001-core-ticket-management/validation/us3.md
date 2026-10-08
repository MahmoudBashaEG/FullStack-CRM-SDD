# US3 edit/assignment checkpoint: T041–T055

Validated 2026-10-08. T001–T040 implementation and uncommitted US2 changes were preserved. T041–T055 are complete; T056–T081 were not started. Specification, planning artifacts, contracts and constitution are unchanged.

## Delivered behavior and requirement evidence

| Requirement | Implementation and verification |
| --- | --- |
| FR-005/006 | PATCH merges omitted fields, normalizes text, rejects explicit null/blank/invalid enum/server-owned/duplicate fields, and changes category only to an available newly selected ID. Domain and handler tests, real SQL/API tests and browser edits cover all four editable fields. |
| FR-007 | PUT explicitly requires agentId, permits null clearing, supports assign/reassign/retain, checks eligibility only for a different non-null ID, and never changes status or queue authorization. Domain, handler, SQL and browser checks cover one owner and inactive clearing/reselection. |
| FR-010 | Created, DetailsChanged and AssignmentChanged are readable now through GET history and the detail UI. Strings, category/agent label snapshots, assignment nulls, trusted actor/time and decimal revision strings use the existing contract. Browser checks all 11 entries of a complete edit/assignment journey. |
| FR-011 | One store-owned transaction saves the ticket and only pending history once; actual changes increment revision/time once. No-op responses retain token/time/history. Failed history insertion and cancellation roll back; provisioning never changes ticket metadata/history. |
| FR-012 | Canonical eight-byte expectedVersion is required; stale requests fail before no-op/reference decisions. Concurrent writers and real-write/no-op races serialize. Drafts survive conflict and transport uncertainty; reload, review and resubmit are explicit, without replay. |
| FR-013/014 | Both new endpoints use the existing signed-JWT shared-queue policy before validation/existence. No administration, delete, status/lifecycle, escalation or future-module operations were added. |

Retained inactive IDs can be explicitly resubmitted or left untouched. Replacing an inactive reference succeeds with an available/eligible value; selecting the former inactive reference again fails atomically. Reference CLI inactivation/renaming changes current presentation while leaving saved ticket versions, update times and every historical payload unchanged.

The mutation callback is the scoped operation: Infrastructure keeps its transaction open around Application orchestration, owns all commit/rollback/disposal, and exposes only Domain/application values and asynchronous selection functions. No extra repository, transaction pipeline or event store is introduced. Existing-ticket UPDLOCK/HOLDLOCK precedes version comparison; newly selected category/agent rows use HOLDLOCK. The EF original shadow rowversion predicate remains intact. Responses are constructed from this operation's tracked state before releasing its writer lock, rather than requerying another writer's state after commit.

History conversion now handles initial snapshots, scalar strings and reference snapshots without changing the database schema. Pending history is acknowledged only after successful commit; saved history has no supported edit/delete operation. Closed edit/assignment rejection is implemented, including identical inputs; tests prepare Closed records directly rather than implementing the later lifecycle story.

The Angular tickets feature owns forms, history types/rendering, HTTP mutations and the per-detail edit session. Current inactive options remain visible; only lookup choices are selectable as new references. Detail/history/choices refresh after success; returning to the queue reads fresh summaries rather than a stale list cache. Conflict reload preserves the form controls and dirty-field intent, displays the latest saved detail, and requires review before explicit resubmission. Late save/reload responses from a previous ticket cannot replace the next ticket's session. A lost-response browser fixture lets the real save commit, drops its response, then proves the deliberately retried identical change adds no activity.

## Verification results

| Check | Result |
| --- | --- |
| T041 initial red run | 7 new domain tests failed against compiling mutation stubs; 6 existing creation tests passed |
| T042 initial red run | 12 new application tests failed against handler stubs; 39 existing tests passed |
| T043 initial red run | 11 failures against missing mutation endpoints; the existing duplicate-JSON middleware already rejected one invalid request |
| `dotnet build Crm.slnx --no-restore` | Passed, zero warnings/errors |
| Backend suites | Domain 13, Application 51, SQL/API 88 passed: 152 total, no failures or skips |
| US3 SQL suite | 22 passed, including malformed versions, no-ops, stale conflicts, simultaneous writers/real-no-op, category and agent provisioning-lock races, cancellation, rollback, a real SQL deadlock and command timeout with no retry |
| `npm test --prefix src/frontend -- --watch=false` | 46 passed, including forms, actual DOM submit, inactive choices, history values, conflict/review, transport uncertainty and previous-ticket response isolation |
| `npm run build --prefix src/frontend` | Production build passed |
| `npm run e2e --prefix src/frontend` | All 9 passed: 3 US1, 3 US2 and 3 US3 journeys; retries remain zero and assertions/timeouts were not weakened |
| `dotnet ef migrations has-pending-model-changes --project src/backend/Crm.Infrastructure --startup-project src/backend/Crm.Api --no-build` | No model changes since the existing migration |
| Migration application | InitialTicketStorage applied to fresh owned integration/browser catalogs; no new migration or development-database schema change |
| Architecture | Inward project dependencies and thin MediatR controllers retained. Architecture test project remains a scaffold; T073 is outside this checkpoint and is not claimed as passing architecture coverage |

All database access used the existing local SQL Server and Windows account with the shared ConnectionStrings:Crm. No SQL users/passwords/principals/grants were introduced. Readiness remains the protected reference-data categories endpoint (401 without a token, 200 plus protected fallback with a signed token). Fixture owners guard catalog creation/cleanup. Temporary signed identities and CLI inputs remain outside tracked source.

## Problems and corrections

1. The initial Angular history template assumed every value was a Created snapshot. The new discriminated history union exposed that assumption during compilation; rendering now narrows by kind and correctly presents strings, reference snapshots and nulls.
2. The existing detail test needed to explicitly satisfy the two new reference-choice requests. Its safe-text, inactive-label, history and failure assertions remain intact.
3. The browser found assignment form submission did not dispatch Angular ngSubmit because only a standalone FormControl was bound. A DOM-submit regression test reproduced the failure, then a proper FormGroup binding corrected it. The complete browser suite subsequently passed without increasing timeouts or removing assertions.
4. A late mutation response could otherwise replace a newly opened ticket's detail session. A session generation guard and regression test prevent that cross-ticket overwrite.
5. The prior creation-test fake implements the extended feature port without changing its creation assertions. The task-completion script now allows IDs only through T055. DTOs/commands and the domain test were placed at the planned task paths.

## Deviations, assumptions and limits

- Execution-order deviation: Domain mutation implementation began after the T041 red run but before T042/T043 test files were authored, rather than waiting for all three test tasks. Application and endpoint behavior had failing tests before their implementations; additional race/regression cases were added during validation. This sequencing deviation is recorded rather than claiming the full test-task dependency wave was obeyed.
- Forms send changed/dirty fields; a fresh unchanged submit sends the saved subject to make a valid no-op PATCH. This is a client transport choice, not new business behavior. On review, untouched fields are omitted; no automatic merge or save is performed.
- The server checks retained-reference IDs rather than reference activity flags; label snapshots use reference values observed for that operation. No reference administration UI or HTTP mutation was added; browser inactivation uses the existing CLI against the owned test catalog.
- SQL command timeout and a deliberately constructed real deadlock exercise the unchanged dependency-failure translation. They do not disable locking, constraints, cancellation or concurrency checks and do not enable retries.
- Production OIDC issuer/client configuration remains an external deployment prerequisite. Tests use genuine signed JWTs in the existing Testing-only infrastructure.
- No new package, schema, business field, role system, performance gate or platform module was introduced. No remaining checkpoint blocker or product-design deviation was found. Lifecycle delivery, comprehensive US5 history review, architecture automation and SC-005 staff evaluation remain later work.
- `.specify/extensions.yml` is absent; no pre/post implementation hooks are registered.

## Created files

- `src/backend/Crm.Api/Binding/TicketMutationRequests.cs`
- `src/backend/Crm.Api/Controllers/TicketDetailsController.cs`
- `src/backend/Crm.Api/Controllers/TicketAssignmentController.cs`
- `src/backend/Crm.Application/Tickets/Contracts/TicketMutationDtos.cs`
- `src/backend/Crm.Application/Tickets/Commands/UpdateTicketDetailsCommand.cs`
- `src/backend/Crm.Application/Tickets/Commands/AssignTicketCommand.cs`
- `src/backend/Crm.Application/Tickets/Ports/TicketMutationContext.cs`
- `src/backend/Crm.Application/Tickets/Validation/TicketMutationValidators.cs`
- `src/backend/Crm.Domain/Tickets/TicketClosedException.cs`
- `src/backend/Crm.Infrastructure/Tickets/TicketHistoryValues.cs`
- `tests/backend/Crm.Domain.Tests/Tickets/TicketEditAssignmentTests.cs`
- `tests/backend/Crm.Application.Tests/Tickets/EditAndAssignTests.cs`
- `tests/backend/Crm.IntegrationTests/Tickets/EditAssignmentConcurrencyTests.cs`
- `src/frontend/src/app/features/tickets/components/ticket-edit-form.component.ts`
- `src/frontend/src/app/features/tickets/components/ticket-edit-form.component.spec.ts`
- `src/frontend/src/app/features/tickets/components/ticket-assignment.component.ts`
- `src/frontend/src/app/features/tickets/components/ticket-assignment.component.spec.ts`
- `src/frontend/src/app/features/tickets/components/ticket-conflict.component.ts`
- `src/frontend/src/app/features/tickets/services/ticket-edit-session.service.ts`
- `src/frontend/src/app/features/tickets/services/ticket-edit-session.service.spec.ts`
- `src/frontend/src/app/features/tickets/models/ticket-history.models.ts`
- `src/frontend/src/app/features/tickets/testing/ticket-fixtures.ts`
- `src/frontend/e2e/fixtures/provision-references.ts`
- `src/frontend/e2e/tickets/edit-assignment.spec.ts`
- `specs/001-core-ticket-management/validation/us3.md`

## Modified files in this checkpoint

- `scripts/complete-task.ps1`
- `specs/001-core-ticket-management/tasks.md`
- `src/backend/Crm.Application/Tickets/Ports/ITicketMutationStore.cs`
- `src/backend/Crm.Domain/Tickets/Ticket.cs`
- `src/backend/Crm.Domain/Tickets/TicketChangeValue.cs`
- `src/backend/Crm.Domain/Tickets/TicketHistoryEntry.cs`
- `src/backend/Crm.Infrastructure/Persistence/Configurations/TicketHistoryConfiguration.cs`
- `src/backend/Crm.Infrastructure/Tickets/TicketMutationStore.cs`
- `src/backend/Crm.Infrastructure/Tickets/TicketReadService.cs`
- `tests/backend/Crm.Application.Tests/Tickets/CreateAndGetTicketTests.cs`
- `src/frontend/src/app/features/tickets/models/ticket.models.ts`
- `src/frontend/src/app/features/tickets/data-access/tickets.service.ts`
- `src/frontend/src/app/features/tickets/components/ticket-history.component.ts`
- `src/frontend/src/app/features/tickets/pages/ticket-detail.page.ts`
- `src/frontend/src/app/features/tickets/pages/ticket-detail.page.spec.ts`

Existing uncommitted US2 files also appear in git status; they were retained and are listed in validation/us2.md, not attributed to this checkpoint.
