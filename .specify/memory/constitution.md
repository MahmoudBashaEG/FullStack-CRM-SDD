# Customer Support CRM Constitution

## Core Principles

### I. Incomplete Requirements and Assumptions

Available business requirements are high-level features; detailed fields, workflows,
validations, permissions, data models, and UI/UX are generally unspecified. Reasonable
CRM/helpdesk conventions MAY fill missing low-impact details, but inferred requirements
MUST be documented explicitly as assumptions in the active specification. Prefer simple,
conventional behavior over speculative complexity. High-impact ambiguities affecting
architecture, security, permissions, integrations, or major workflows MUST be clarified
or explicitly documented with their implications before dependent implementation.
Unrelated functionality MUST NOT be added merely because mature CRM products support it.

### II. Spec-Driven Development

Every business feature MUST be specified before implementation. `spec.md` defines WHAT
and WHY, `plan.md` defines HOW, and `tasks.md` defines implementation work. Implementation
MUST remain within the active feature scope and MUST NOT prematurely implement future
CRM modules. These artifacts MUST remain consistent with each other and this constitution.

### III. Backend Architecture

The backend MUST use an ASP.NET Core REST API, Clean Architecture with Domain, Application,
Infrastructure, and API layers, CQRS with MediatR, and EF Core with SQL Server. Dependencies
MUST point inward: Domain MUST NOT depend on ASP.NET Core, EF Core, Application,
Infrastructure, or API; Application MUST NOT depend on Infrastructure or API.
Business logic MUST reside in Domain or Application rather than controllers.
CQRS MUST NOT be interpreted as a requirement for event sourcing.

I/O operations MUST use asynchronous APIs and propagate `CancellationToken` where supported
and appropriate. APIs MUST use consistent REST semantics, validation, HTTP status codes,
and error responses so clients can rely on predictable contracts.

### IV. Frontend Architecture

The frontend MUST use Angular and TypeScript with a feature-based architecture organized
around `core`, `shared`, and `features`. Feature-specific components, models, data-access,
routes, and services MUST belong inside their feature. `core` MUST contain application-wide
infrastructure and `shared` MUST contain reusable, feature-independent code; neither may
become a generic dumping ground. This keeps ownership and dependencies clear.

### V. Quality

Meaningful domain and application behavior MUST have automated tests. Important API and
persistence flows SHOULD have integration tests; omission MUST be justified in the plan
or verification evidence. Architecture dependency boundaries SHOULD have automated checks
where practical; limitations MUST be documented. Tests MUST verify observable behavior.
Database migrations MUST be version controlled. Secrets MUST never be committed.
Relevant projects MUST build and relevant tests MUST pass before implementation is complete.

### VI. Platform Constraints

Architecture MUST remain compatible with Arabic and English, responsive/mobile-friendly
web UI, multiple departments, multiple branches, and custom branding. Plans MUST consider
these constraints where they affect localization, layout direction, organizational scope,
or presentation. Compatibility MUST NOT cause premature implementation of unrelated
functionality; concrete capabilities MUST be delivered within their specified feature scope.

## Business Scope

The high-level business feature list defines product scope: Customer Management;
Ticket Management; Communication Channels; Agent Dashboard; SLA & Automation;
Knowledge Base; AI Features; Customer Portal; Reports & Management;
Security & Administration; Integrations; and Platform capabilities.
This list identifies feature areas, not detailed acceptance criteria or authorization
to implement all modules at once.

## Development Workflow

Specifications MUST distinguish provided requirements from inferred assumptions.
Plans MUST check the six principles before research and again after design, recording
applicable architectural decisions, high-impact ambiguities, and verification requirements.
Tasks MUST trace to the active specification and plan. Completion review MUST verify scope,
layer boundaries, platform compatibility, migrations, builds, and relevant test results.
Inapplicable checks MUST have a recorded reason.

## Governance

This constitution governs project-wide development rules. Specifications, plans, tasks,
and implementation reviews MUST check compliance. Exceptions MUST document the affected
rule, rationale, risk, and mitigation and receive project maintainer approval before merge.

Future amendments MUST document their rationale and impact and receive project maintainer
approval. Versioning MUST use MAJOR for incompatible rule removals or redefinitions,
MINOR for new or materially expanded rules, and PATCH for non-semantic clarifications.
Amendments MUST preserve the ratification date and update the last amended date.
Version 1.0.0 completes the initial, uncommitted constitution using the actual project
constraints. Temporary review reports MUST NOT remain in the committed constitution.

**Version**: 1.0.0 | **Ratified**: 2026-10-07 | **Last Amended**: 2026-10-07
