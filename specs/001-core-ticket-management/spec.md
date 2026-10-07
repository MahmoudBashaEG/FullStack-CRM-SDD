# Feature Specification: Core Ticket Management

**Feature Branch**: No feature branch created; specification directory is independent of branch.

**Created**: 2026-10-07

**Status**: Draft

**Input**: Specify the first Customer Support CRM feature: the core ticket lifecycle,
using the received high-level business scope and explicitly documented assumptions.

## Clarifications

### Session 2026-10-08

- Q: Which tickets should support staff be allowed to view and manage in this first feature?
  → A: Authorized support staff can view and manage all tickets in one shared support queue.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Create and View a Ticket (Priority: P1)

As a support staff member, I want to record a support issue and retrieve its current
information so the team has a dependable record to work from.

**Why this priority**: Recording and retrieving issues is the foundation of ticket handling.

**Independent Test**: Create a ticket and retrieve it by its reference without depending
on assignment, status changes, or other CRM modules.

**Acceptance Scenarios**:

1. **Given** authorized staff and available categories, **When** they provide a nonblank
   subject and description and save, **Then** one ticket receives a unique reference,
   Open status, Normal priority unless another is selected, and an Uncategorised category
   unless another is selected; it is initially unassigned and its creation is in history.
2. **Given** an existing accessible ticket, **When** staff retrieve it, **Then** they see
   its reference, subject, description, category, priority, status, assigned agent or
   unassigned state, creation time, last change time, and history.
3. **Given** blank required content or an unavailable category or priority, **When** staff
   try to save, **Then** creation is rejected with an explanation and no ticket is created.
4. **Given** staff authorized for the shared support queue, **When** they retrieve or manage
   a ticket assigned to another agent or unassigned, **Then** the same lifecycle permissions
   apply. A person without that authorization cannot view or change any queue ticket.

### User Story 2 - List and Track Tickets (Priority: P1)

As a support staff member, I want to find tickets and compare their current state so I can
identify issues needing attention.

**Why this priority**: A usable shared work list prevents recorded issues from being lost.

**Independent Test**: Use a prepared set of tickets with different categories, priorities,
statuses, and assignments to verify finding and filtering without changing them.

**Acceptance Scenarios**:

1. **Given** accessible tickets, **When** staff list them, **Then** each result identifies
   the reference, subject, category, priority, status, assignment, and last change time;
   results are ordered by last change time, newest first, with reference as a stable tie-breaker.
2. **Given** tickets with differing attributes, **When** staff filter by status, category,
   priority, or assigned agent (including unassigned), **Then** only tickets matching all
   selected filters appear. Clearing filters restores the accessible list.
3. **Given** a known reference or subject term, **When** staff search, **Then** matching
   references or subjects appear; reference matching is exact and subject matching is
   a case-insensitive substring match, combined with any selected filters.
4. **Given** no tickets match, **When** staff search or filter, **Then** an empty result
   is clearly distinguished from a failure to retrieve tickets.

### User Story 3 - Edit and Assign a Ticket (Priority: P2)

As a support staff member, I want to correct ticket information and choose the responsible
agent so the issue is understandable and has clear ownership.

**Why this priority**: Accurate information and manual ownership support effective handling.

**Independent Test**: Start with an existing non-closed ticket, edit its details, assign it,
reassign it, and remove its assignment, then inspect current values and history.

**Acceptance Scenarios**:

1. **Given** a non-closed ticket, **When** authorized staff change its subject, description,
   category, or priority, **Then** the changes are saved and history records who changed
   each value, when, and its previous and new values.
2. **Given** a non-closed ticket and an eligible agent, **When** staff assign or reassign it,
   **Then** it has exactly that one agent, its status is unchanged, and history records
   the previous and new assignment. Removing assignment returns it to unassigned.
3. **Given** invalid required content or an ineligible agent, **When** staff attempt a
   change, **Then** the change is rejected with an explanation and existing information
   and history remain unchanged.
4. **Given** a saved ticket, **When** staff submit identical values, **Then** no new history
   entry or last change time is produced.

### User Story 4 - Progress, Close, and Reopen a Ticket (Priority: P2)

As a support staff member, I want to show work progress, mark resolution, close completed
issues, and reopen issues that need further work so ticket state reflects reality.

**Why this priority**: Explicit states distinguish outstanding work from completed support.

**Independent Test**: Use prepared tickets in each status to exercise every allowed
transition and reject every disallowed transition independently of assignment.

**Acceptance Scenarios**:

1. **Given** an Open ticket, **When** staff start work, **Then** it becomes In Progress;
   **When** they mark it resolved, **Then** it becomes Resolved, with both changes in history.
2. **Given** an Open ticket resolved without a separate work step, **When** staff mark it
   resolved, **Then** it becomes Resolved and history records the change.
3. **Given** a Resolved ticket, **When** staff close it, **Then** it becomes Closed and its
   information and history remain readable. A direct Open-to-Closed or In-Progress-to-Closed
   change is rejected.
4. **Given** a Resolved or Closed ticket needing more work, **When** staff reopen it,
   **Then** the same ticket returns to Open with its reference, details, and assignment
   preserved and the reopening in history.
5. **Given** a Closed ticket, **When** staff attempt an information or assignment edit,
   **Then** the edit is rejected until the ticket is reopened.

### User Story 5 - Review Ticket History (Priority: P2)

As a support staff member, I want to review how a ticket changed so I can understand prior
handling and responsibility without relying on memory.

**Why this priority**: History explains the current record and preserves continuity.

**Independent Test**: Use a prepared ticket with creation, edits, assignments, and status
changes to verify the complete chronological account, without making new changes.

**Acceptance Scenarios**:

1. **Given** a ticket with recorded activity, **When** staff review history, **Then** they
   see creation and every successful information, assignment, and status change in
   chronological order, including the responsible staff member and time.
2. **Given** existing history, **When** staff edit or reopen the ticket, **Then** prior
   entries remain unchanged; staff cannot edit or delete history through this feature.

### Edge Cases

- Missing or inaccessible references reveal no ticket contents; access denial does not
  permit any changes. Denied operations produce no ticket activity entry.
- A ticket may remain unassigned throughout its lifecycle; assignment is not a transition
  prerequisite and changing assignment never implicitly changes status.
- Category or agent eligibility is checked at the time of saving. If unavailable, reject
  the entire attempted change and preserve the last saved ticket and history.
- Invalid status changes are rejected without adding history; selecting the existing
  status is a no-op.
- If another staff member has changed a ticket since it was retrieved, reject the stale
  change and ask the user to review the latest information rather than silently overwrite it.
- If a change fails, it must not leave partial information changes or history claiming success.
- Search treats user-entered Arabic or English subjects as text; this feature does not
  introduce translated ticket content, department routing, or branch management.

## Requirements *(mandatory)*

### Functional Requirements

**Provided requirements**: The source is [business scope](../../docs/business/customer-support-crm-scope.md),
section 2: create and track tickets; categories and priorities; assign tickets to agents;
status and escalation; ticket history. The user narrows this feature to the core lifecycle.
Detailed behavior below is inferred from those requirements and the requested minimum
behaviors, except decisions explicitly confirmed in Clarifications.

- **FR-001**: Authorized staff MUST create tickets with a nonblank subject and description,
  a category, and a priority. Each ticket MUST receive a unique, stable reference.
- **FR-002**: New tickets MUST start Open and unassigned. Defaults MUST be Normal priority
  and Uncategorised category unless staff choose other available values.
- **FR-003**: Staff MUST retrieve accessible tickets with all current information described
  in Story 1, including readable history, regardless of status.
- **FR-004**: Staff MUST list accessible tickets with the summary and ordering in Story 2,
  search by exact reference or case-insensitive subject substring, and combine category,
  priority, status, and assignment filters using all-match behavior.
- **FR-005**: Staff MUST edit subject, description, category, and priority on non-closed
  tickets. Blank required content and unavailable choices MUST be rejected.
- **FR-006**: Priority choices MUST be Low, Normal, High, and Urgent. Categories MUST come
  from available business labels with Uncategorised as a valid fallback.
- **FR-007**: Staff MUST manually assign, reassign, or unassign a non-closed ticket.
  A ticket MUST have at most one assigned eligible agent. Assignment MUST NOT change status.
- **FR-008**: Statuses MUST be Open, In Progress, Resolved, and Closed. Only the following
  transitions MUST be allowed; no automatic transitions occur:

  | Current status | Allowed next status |
  | --- | --- |
  | Open | In Progress, Resolved |
  | In Progress | Resolved |
  | Resolved | Closed, Open (reopen) |
  | Closed | Open (reopen) |

- **FR-009**: Closing MUST preserve the ticket and history. Closed tickets MUST reject
  edits and assignment changes until reopened. Reopening MUST preserve reference, details,
  and assignment and MUST record a new status change.
- **FR-010**: Ticket history MUST record creation and every successful change to editable
  information, assignment, or status with actor, time, and previous/new values for changes.
  History MUST remain readable and immutable through this feature.
- **FR-011**: Failed or no-op changes MUST NOT alter saved values, last change time, or
  history. Successful changes MUST update last change time and their history together.
- **FR-012**: Stale edits MUST be rejected without losing the latest saved changes.
  Staff MUST receive an explanation allowing them to review and retry.
- **FR-013**: Explicitly authorized support staff MUST be able to view and perform the
  specified lifecycle operations on all tickets in one shared support queue, regardless
  of assignment. Department, branch, and assignee restrictions are outside this feature.
  People without queue authorization MUST NOT view or change its tickets. Assignment
  alone MUST NOT grant access. Inaccessible tickets MUST NOT expose their information.
- **FR-014**: This feature MUST NOT provide ticket deletion, customer communications,
  category administration, or escalation behavior.

### Key Entities *(include if feature involves data)*

- **Ticket**: A recorded support issue with a stable reference, subject, description,
  classification, priority, current status, optional agent assignment, and activity times.
- **Agent**: An existing staff member eligible to receive ticket assignments.
- **Category**: An available label for classifying an issue; managing labels is outside scope.
- **Ticket History Entry**: An immutable account of ticket creation or a successful change,
  identifying the actor, time, and changed values.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In acceptance testing, 100% of valid ticket creations produce a distinct,
  retrievable reference and the specified default state; 100% of invalid creations leave
  no ticket behind.
- **SC-002**: Across a prepared set covering every priority, status, assignment state, and
  category, all search/filter combinations return exactly the matching accessible tickets.
- **SC-003**: Every allowed lifecycle transition succeeds and every disallowed transition
  is rejected; all reopenings preserve the original reference and prior history.
- **SC-004**: For every tested successful edit, assignment, and transition, staff can identify
  who changed what and when from history; failed and no-op changes add zero activity entries.
- **SC-005**: At least 4 of 5 representative support staff can create, retrieve, assign,
  resolve, close, and reopen a sample ticket without facilitator assistance after a brief
  introduction, and can correctly identify its current owner and status.

## Assumptions

- **A-001 — Source fidelity**: The business supplied feature headings only. Subject,
  description, reference, default values, searches, filters, transition rules, edit rules,
  history detail, and success targets are inferred defaults for this specification.
- **A-002 — Staff and access (confirmed)**: This is an internal staff feature with one
  shared support queue. The user confirmed that explicitly authorized support staff can
  view and manage all queue tickets. Department, branch, and assignee restrictions and
  role hierarchies are deferred. Staff identities, explicit queue authorization, and
  eligible agents remain dependencies; user administration is outside scope. A missing
  authorization context MUST NOT be replaced with access for everyone.
- **A-003 — Customer association**: A ticket can record the issue without requiring an
  existing customer profile. Customer registration, profile management, and mandatory
  customer linkage are deferred rather than making this feature depend on a future module.
- **A-004 — Classification dependencies**: Available categories and eligible agents are
  supplied as reference information. Only Uncategorised is prescribed as a category;
  no further business category names or management workflows are inferred.
- **A-005 — Lifecycle (high impact)**: Four statuses and the explicit transition table are
  the inferred initial workflow. Resolved means the staff member believes the issue is
  addressed; Closed means handling is complete. No customer approval, waiting state,
  resolution note, automatic closure, or mandatory assignee is required. Reopening returns
  to Open. These choices allow basic handling without creating additional workflows.
- **A-006 — Editing and history**: Subject and description remain editable until closure;
  history retains previous values for continuity. Ticket deletion, history correction,
  retention periods, attachments, and additional comments are outside this feature.
- **A-007 — Escalation boundary**: Escalation is explicitly present in the received scope
  but both manual escalation and escalation automation are deferred from this initial
  lifecycle. High/Urgent priority is classification, not an escalation mechanism.
- **A-008 — Explicit exclusions**: SLA rules, automatic assignment, alerts, notifications,
  email integration, WhatsApp, SMS, live chat, web forms, customer portal, AI features,
  reports, advanced administration, and all other future CRM modules are outside scope.
- **A-009 — Platform compatibility**: This specification imposes no English-only content
  restriction or desktop-only usage requirement. Broader localization, organizational
  management, and branding capabilities remain separate scoped work under the constitution.
- **A-010 — Evaluation**: Success criteria are proposed acceptance targets, not supplied
  business metrics. Prepared ticket records and representative support staff are needed
  to evaluate tracking accuracy and independent task completion.
