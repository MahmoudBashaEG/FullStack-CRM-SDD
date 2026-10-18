# Story 08 — Customer Notes, Interaction History & Attachments — Story Intake

## Feature

- **Feature name (display):** Customer Management — Notes, Interactions & Attachments
- **Feature slug:** `customer-management`

## Tracker

- **Tracker type:** `none` (local training story)
- **Work item ID:** `CUST-002`
- **Work item type:** `Business Feature Story`
- **Status:** `Business decisions approved — ready for Squad-Kit planning`
- **Labels:** `customer-management, notes, interactions, attachments, aspnet-core, angular, cqrs, ef-core, squad-kit, training`

## Title

Customer Notes, Interaction History & Attachments

## Description

### Goal

Complete the outstanding **Customer Management** capabilities from the original *Customer Support CRM — Core Features* document: **interaction history, notes, and attachments**. Extend the customer profiles and contact-details workflows already delivered in `CUST-001` (Story 03).

Support staff should be able to open an existing customer profile, add private notes, log customer interactions such as phone calls, review the customer's relevant history, and upload/download small customer-related documents. Preserve all existing CRM functionality and keep the implementation appropriately simple for a **training project**.

### 1. Authoritative inputs and existing architecture

- The original two-page business document lists Customer Management as: **Customer profiles; Contact details; Interaction history; Notes and attachments**. This story completes the final two items; do not reimplement profiles or contacts.
- Reuse the implemented stories: `ARCH-001`, `AUTH-001`, `CUST-001`, `TICK-001`, `TICK-002`, `DASH-001`, and `DASH-002`.
- Use the existing .NET 10 Clean Architecture projects (`Crm.Domain`, `Crm.Application`, `Crm.Infrastructure`, `Crm.Api`), CQRS with source-generated Mediator, FluentValidation, EF Core + SQL Server, existing Identity/JWT authorization, and current `/api/v1/` conventions.
- Use existing Angular standalone feature organization, Angular Material, shared UI, route guards, typed HTTP services, and Customer Details page.
- The planner must inspect the repository before choosing precise class names, endpoints, and file paths. Follow actual code conventions, not guessed interfaces.
- The existing `Customer` entity remains authoritative for profile fields. All new data must reference its stable customer ID. Do not introduce another Customer table or authentication flow.

### 2. Business roles and access

| Action | SuperAdmin | Agent | Customer |
| --- | --- | --- | --- |
| View an existing customer profile | Existing `CUST-001` permissions | Existing `CUST-001` permissions | Own basic profile only (unchanged) |
| Add and read customer notes | Yes, on any customer | Yes, on any customer | No |
| Log and read customer interactions | Yes, on any customer | Yes, on any customer | No |
| Upload and download customer attachments | Yes, on any customer | Yes, on any customer | No |
| Review customer-linked tickets | Follow existing ticket visibility | Only tickets already visible under ticket authorization | No new access |
| Edit or delete notes/interactions | **No (C2 approved)** | **No (C2 approved)** | No |
| Delete attachments | Not in scope | Not in scope | No |

- Staff permissions are intentionally simple and global for customer-owned notes, manual interaction logs, and attachments, consistent with CUST-001.
- **Customers must never gain access to staff-only notes, interaction logs, or attachments** through this story, including registered customers viewing their own basic profiles.
- Maintain existing ticket-access restrictions: an Agent seeing a customer does **not** become authorized to view every ticket belonging to that customer. Collaborator-only membership from DASH-002 does not grant ordinary ticket-detail access.
- The backend must enforce these rules; frontend hiding is not sufficient.

### 3. Feature A — Internal customer notes

**Purpose:** Staff record useful customer-specific background, follow-up details, or preferences for later support interactions.

**Example:** Agent Ahmed opens customer Sara's profile and adds: “Customer prefers afternoon follow-up calls.” Another authorized Agent viewing Sara's profile can read the note.

**Requirements:**

- Provide an **Add Note** action inside the existing staff Customer Details screen.
- A note has: a server-generated ID, customer ID, plain-text body, staff author ID/display name, and UTC created timestamp.
- Text is required; trim whitespace and impose a sensible maximum length (suggested: 2,000 characters).
- Show notes newest first, with author and timestamp; use pagination or a simple “Load more” pattern consistent with the existing UI.
- Notes are **staff-only**, never messages to the customer and never entries in `TicketActivity` or DASH-002 internal ticket discussions.
- Per **approved C2 (Option A)**, notes are **append-only in CUST-002**: Agents and SuperAdmin can add and view notes, but **cannot edit or delete** them. Record any correction as a new note. No pinning, tagging, mentions, or note notifications.

### 4. Feature B — Customer interaction history

**Purpose:** Staff can understand prior contact with a customer without reopening unrelated screens or needing an external Email/WhatsApp integration.

**Example:** An Agent receives a phone call from Sara, records “Called to ask about refund status,” and can later see that call in her customer history.

**Requirements:**

- Provide **Log Interaction** on the staff Customer Details screen.
- An interaction has: ID, customer ID, type (`Phone Call`, `Meeting`, `Other`), short plain-text summary, occurred-at UTC, recorded-by staff ID/display name, and created-at UTC.
- Default the occurred-at value to the current time but allow staff to enter a past interaction time; do not require a future date.
- Provide chronological newest-first list of manual interactions, with type, summary, timestamp, and recorder; support straightforward pagination/“Load more” if needed.
- In the same customer detail view, show a **Related Tickets** subsection using existing ticket visibility rules and links to permitted tickets. Display basic ticket metadata (subject, status, priority, date), not sensitive ticket data to unauthorized Agents.
- This is a simple customer history view; it may use separately presented **Interactions**, **Notes**, and **Related Tickets** sections rather than building a complex universal activity feed or new analytics/event platform.
- Reuse existing tickets. Do not create duplicates of `TicketActivity` or copy messages from DASH-002 collaboration into customer history.
- No actual customer-facing Email, WhatsApp, SMS or Live Chat integration, sending, receiving, or syncing in this story. Those remain postponed to the final Communication Channels module.
- Per **approved C2 (Option A)**, interaction entries are **append-only**: Agents and SuperAdmin can log and view interactions, but **cannot edit or delete** existing entries. Corrections are logged as new interactions. No automatic event ingestion, sentiment analysis, or SLA rules.

### 5. Feature C — Customer attachments

**Purpose:** Staff can store and retrieve small customer-related files, for example a warranty PDF or a screenshot.

**Example:** Agent Ahmed opens Sara's profile, uploads `warranty.pdf`, and later another authorized Agent downloads it.

**Requirements:**

- Upload one attachment at a time from the staff Customer Details screen and show a list with filename, size, uploaded-by and uploaded-at UTC.
- Allow authorized staff to download an attachment using the existing authenticated API. Do **not** expose attachments through public static URLs.
- For training simplicity, store small attachment content and metadata in **SQL Server** using a new entity/table. No cloud object storage, file service, external provider, or complex folder management.
- Suggested maximum upload size: **5 MB per file**. Support a basic allowlist of common business files: PDF, PNG, JPG/JPEG, TXT, and DOCX. Reject empty files, oversize files and disallowed extensions/types with useful validation errors.
- Treat uploaded filenames as display metadata, not filesystem paths. Download using a safe filename and `Content-Disposition: attachment`. Do not render untrusted HTML or scripts in the app.
- Keep file bytes out of customer list/detail JSON and database queries that only need metadata. Download bytes only through the download endpoint.
- Uploads are staff-only; Customer users cannot access these files in CUST-002.
- No file preview, virus-scanning service, versioning, bulk upload, external storage, or physical/cloud document management required. No attachment deletion required for this first iteration.

### 6. Backend and persistence scope

- Add simple feature-scoped entities for **CustomerNote**, **CustomerInteraction**, and **CustomerAttachment**, each referencing the existing Customer ID; record creator/uploader using validated staff Identity IDs.
- Add **one additive EF Core migration** for these three entities/tables and practical indexes. Do not recreate Customer/Ticket tables, change previous migrations, reset the database, or lose existing data.
- Implement application CQRS commands and queries for creating/listing notes, recording/listing interactions, listing/downloading/uploading attachments, and listing the customer's authorized related tickets (reuse existing services/data access where practical).
- Use existing staff role policies and standard validation/error conventions, server-managed author/time metadata, and correct 400/403/404 responses.
- Follow current `/api/v1/customers/{customerId}/...` nested-resource style if consistent with the current API; decide exact HTTP contracts during planning after inspecting existing Customer controllers. Keep contracts simple, typed, and documented.
- Keep file-upload size validation and authorization enforced on the server. Use multipart/form-data for the upload endpoint.
- Prefer a small, clear implementation. Do not introduce sophisticated lock management, special transaction-retry frameworks, ETags, message queues, background workers, elaborate domain events, or new service infrastructure just for this story.

### 7. Angular UI/UX

Extend the existing **staff Customer Details** page rather than creating an unrelated application section.

- **Notes:** text area / Add Note form, author/time list, empty/loading/validation/error states.
- **Interactions:** type selector, summary, occurrence time, Save action, newest-first log.
- **Related Tickets:** concise list or section of tickets the logged-in staff member is already authorized to view; link to existing Ticket Details route.
- **Attachments:** file picker, validation/size feedback, upload button, metadata list, download action.
- Keep Customer profile and edit experiences from CUST-001 unchanged. Customer-role users continue seeing only their existing self-profile UI, not these staff panels.
- Follow the existing Angular Material design, responsive behavior, shared error display, and existing API/auth handling. No new UI library required.

### 8. Verification and testing policy

- Exactly **one new backend happy-path unit test** and **one new frontend happy-path unit test** for Story 08. Do not modify or remove existing tests unnecessarily. Existing tests must continue to pass.
- No Playwright/E2E, automated SQL/API integration suites, load/stress tests, or extra test frameworks.
- Run backend/frontend builds and existing unit suites. Apply the additive migration to the local development database and verify existing Customer/Ticket/Story 07 data is preserved.
- Verify manually using a simple API client, SQL and browser: add/read a note; log/read an interaction; view authorized related tickets; upload/list/download an allowed small file; reject an invalid file; verify Agent/SuperAdmin access and no Customer-role access to staff-only content.
- Verify the previous Customer Details, customer registration/self-profile, existing Tickets and DASH-002 features still work at a basic smoke-test level.
- A browser check can be manual. Document unverified items accurately; do not claim a browser test passed without actually performing it.

### 9. Explicitly out of scope

- Communication Channels (Email, WhatsApp, Live Chat, SMS, Web Forms), actual outbound/inbound customer messaging, message providers and channel synchronization — **deferred until the final module**.
- Customer Portal enhancements, customer-uploaded files, customer-visible staff notes, customer-facing interaction history, or new Customer permissions.
- AI summaries, chatbots, SLA automation, notifications, Knowledge Base, reports or advanced administration.
- File versioning/previews, third-party storage services, antivirus integrations, bulk document operations, full-text indexing, contact import/export, multi-department/branch rules, customer merges, or audit/event infrastructure.
- Advanced production hardening unrelated to a basic functioning training CRM.

## Approved business decisions — C1–C5

The original business document names these features but does not prescribe all workflow details. **The user approved C1, C3, C4, and C5 and explicitly selected Option A for C2: notes and interactions are append-only, with no edits or deletions.** These decisions are the business baseline for Story 08 planning:

| ID | Approved decision | Reason |
| --- | --- | --- |
| **C1** | Only SuperAdmin and Agent can create/read notes, interactions and attachments; Customers cannot. | Internal staff-only workflow, consistent with existing CRM access. |
| **C2 — Option A** | Notes and manual interactions are **append-only**. Staff may add and view them, but neither Agents nor SuperAdmin may edit or delete an existing note or interaction. Corrections require a new entry. | Preserves history while reducing complexity. |
| **C3** | Interactions are manually logged as Phone Call, Meeting or Other; customer-linked tickets are shown separately under existing ticket permissions. | Provides useful history without implementing deferred communication channels. |
| **C4** | Attachments live in SQL Server (max 5 MB; PDF, PNG, JPG/JPEG, TXT, DOCX), staff-only download, no deletion. | Simple single-database training implementation. |
| **C5** | Notes, interactions, attachments, and related tickets appear within the current staff Customer Details page, as distinct sections. | Reuses existing UX and avoids a complex unified activity feed. |

**Planning gate passed:** C1–C5 are **Approved**. The planner may refine technical implementation details without changing the approved business rules. No further approval is required before `/squad-plan` unless a material contradiction is discovered in the existing implementation.

## Acceptance criteria

- [ ] The original CUST-001 customer profile/contact functionality and Story 01–07 behavior remain intact.
- [ ] An authorized staff member can add and list private notes on an existing customer, with text, author and time; **no edit/delete APIs or UI actions for existing notes**.
- [ ] An authorized staff member can record and review chronological manual customer interactions, including type, summary and occurrence time; **no edit/delete APIs or UI actions for existing interactions**.
- [ ] Related tickets are visible from Customer Details only when the current staff member is authorized to access those tickets under existing rules.
- [ ] An authorized staff member can upload, list and download a permitted customer attachment from authenticated APIs and Angular.
- [ ] Attachment size/type and note/interaction inputs are validated; file content is not included in unrelated customer JSON responses.
- [ ] Customer-role users cannot access internal notes, interaction entries or attachments, including through direct API requests.
- [ ] The new persistence is introduced by one additive EF Core migration and existing customer/ticket data is preserved.
- [ ] Staff Customer Details contains working Notes, Interactions, Related Tickets and Attachments sections, including reasonable loading, empty and failure UI states.
- [ ] Exactly one new backend and one new frontend happy-path unit test are added; all existing tests and both builds pass.
- [ ] Manual API, SQL and browser acceptance checks are documented and genuinely run or marked unverified.
- [ ] Documentation explains ownership, API routes, attachment handling, interaction-history semantics and the separation from future Communication Channels.
- [ ] No implementation is committed until the user accepts the final Story 08 result.

## Attachments

No new attachment is required for this intake. The original *Customer Support CRM — Core Features* source document was supplied for earlier stories; use the existing repository copy if present, rather than duplicating it or silently substituting requirements.

## Dependencies

- **Story:** `CUST-002` (Story 08), under existing feature slug `customer-management`.
- **Depends on:** `CUST-001` (customer profiles), `AUTH-001` (roles/users), `TICK-001/002` (ticket visibility), and the existing architecture/Angular app. `DASH-002` remains unchanged.
- **Next planned area after this story:** SLA & Basic Automation; Communication Channels remain last in the roadmap.

## Extra notes

- This document is an **intake**, not an implementation plan. Create the story folder and copy this intake there before invoking the separate Squad-Kit planning command.
- Do not generate code, migrations, or a commit while preparing or reviewing this intake.
- Use the existing code as the source of truth for technical naming. If an assumption conflicts with implemented permissions, surface the conflict for review rather than quietly expanding access.
- The overarching objective is **training and learning**, not production-grade completeness. Prefer readable, correct, maintainable code with basic validation and authorization over enterprise-scale edge-case handling.
