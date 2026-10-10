# Ticket integration files

US5 changes and dependent-story evidence are listed below. The pre-existing uncommitted US4 implementation remains in the working tree and is separately indexed in [agent-management-files.md](agent-management-files.md).

## Production and current development guidance

- src/backend/Crm.Api/Authorization/CurrentActor.cs
- src/backend/Crm.Api/Authorization/SupportQueuePolicy.cs
- src/backend/Crm.Infrastructure/Tickets/TicketReferenceData.cs
- src/backend/Crm.Infrastructure/Tickets/TicketReadService.cs
- src/backend/Crm.Infrastructure/Tickets/TicketMutationStore.cs
- src/frontend/src/app/core/http/auth.interceptor.ts
- src/frontend/src/app/core/http/auth.interceptor.spec.ts
- docs/development/core-ticket-management.md
- scripts/run-dev.ps1
- scripts/e2e-database.ps1

## Backend verification and real local-auth fixtures

- tests/backend/Crm.Application.Tests/Tickets/LocalActorTests.cs (new)
- tests/backend/Crm.IntegrationTests/Tickets/LocalIdentityAuthorizationTests.cs (new)
- tests/backend/Crm.IntegrationTests/Tickets/LocalAgentReferenceTests.cs (new)
- tests/backend/Crm.IntegrationTests/Fixtures/LocalAgentFixture.cs (new)
- tests/backend/Crm.IntegrationTests/Fixtures/CrmApiFactory.cs
- tests/backend/Crm.IntegrationTests/Fixtures/TestJwtFactory.cs
- tests/backend/Crm.IntegrationTests/Fixtures/MigrationStartupFixture.cs
- tests/backend/Crm.IntegrationTests/Persistence/DevelopmentMigrationStartupTests.cs
- tests/backend/Crm.IntegrationTests/Security/AuthorizationBoundaryTests.cs
- tests/backend/Crm.IntegrationTests/Security/MigrationEnvironmentBoundaryTests.cs
- tests/backend/Crm.IntegrationTests/Tickets/CreateAndViewTests.cs
- tests/backend/Crm.IntegrationTests/Tickets/EditAssignmentConcurrencyTests.cs
- tests/backend/Crm.IntegrationTests/Tickets/ReferenceProvisioningTests.cs
- tests/backend/Crm.IntegrationTests/Tickets/TicketHistoryTests.cs
- tests/backend/Crm.IntegrationTests/Tickets/TicketStatusTests.cs

## Browser and launcher verification

- src/frontend/e2e/fixtures/ticket-local.fixture.ts (new)
- src/frontend/e2e/ticket-local-auth.spec.ts (new)
- src/frontend/e2e/fixtures/auth.ts
- src/frontend/e2e/fixtures/local-auth.fixture.ts
- src/frontend/e2e/fixtures/provision-references.ts
- src/frontend/e2e/run-e2e.mjs
- src/frontend/e2e/authentication.spec.ts
- src/frontend/e2e/customer-authentication.spec.ts
- src/frontend/e2e/agent-management.spec.ts (US5 extends pre-existing US4 test)
- src/frontend/e2e/tickets/create-view.spec.ts
- src/frontend/e2e/tickets/edit-assignment.spec.ts
- src/frontend/e2e/tickets/history.spec.ts
- src/frontend/e2e/tickets/lifecycle.spec.ts
- src/frontend/e2e/tickets/platform.spec.ts
- src/frontend/e2e/tickets/tracking.spec.ts
- tests/scripts/fixtures/DevFixture/Program.cs
- tests/scripts/fixtures/browser-observations.mjs
- tests/scripts/dev-proxy.tests.mjs
- tests/scripts/run-dev.integration.ps1

## Task completion and evidence

- specs/002-user-auth-access/tasks.md (verified checkboxes only)
- specs/002-user-auth-access/validation/customer-registration.md (later integrated result appended)
- specs/002-user-auth-access/validation/agent-management.md (later integrated result appended)
- specs/002-user-auth-access/validation/ticket-integration.md (new)
- specs/002-user-auth-access/validation/ticket-integration-files.md (new)

No new migration, package, runtime secret configuration or approved spec/design change. Historical Feature001 artifacts and the SQL fixture templates remain unchanged. Launcher watch probes temporarily edit the API project and signin page, then restore their exact original bytes; these are not final source changes.
