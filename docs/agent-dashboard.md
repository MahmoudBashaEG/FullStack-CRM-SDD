# Agent Dashboard (DASH-001)

A read-only "My Dashboard" for Agents: a live workload summary and an attention queue built from the tickets currently assigned to them. It adds no ticket operations, tables or migrations.

## 1. Audience

- **Agent only** (`AuthorizationPolicies.Agent`). SuperAdmin keeps Home / Tickets (no admin dashboard yet; see section 8). Customers have no access.
- Navigation entry **My Dashboard** (`/agent-dashboard`), guarded by `roleGuard('Agent')`. Both the guard and the hidden nav entry are UX only; the API enforces access.

## 2. Data scope

- Tickets whose **current** `AssignedAgentUserId` equals the token `sub`. The Agent id is never a request parameter (`?agentId=` is ignored).
- The same SQL filter as the ticket module (`TicketRepository.Scope`), applied to every statement.
- Reassignment is reflected on the next load. Tickets an Agent created but no longer holds are irrelevant.

## 3. Metrics

| Metric / rule | Definition (server-side, current assignee = caller) |
|---|---|
| **Open** | `Status == Open` |
| **In Progress** | `Status == InProgress` |
| **Active** | `Open + In Progress` (disjoint); shown as a caption, not a card |
| **Escalated** | `IsEscalated && Status in (Open, InProgress)` |
| **High / Urgent** | `Status in (Open, InProgress) && Priority in (High, Urgent)` |

Counts overlap: one ticket can be in Open/In Progress, Escalated and High / Urgent at once. Never add the cards together. Resolved and Closed tickets are excluded (they remain in Tickets).

## 4. Attention queue

- At most **10** Open/In Progress tickets, ordered in SQL before the limit: escalated first, then priority Urgent, High, Medium, Low (explicit rank, not alphabetical), then oldest `UpdatedAtUtc`, then `Id` (stable tie-breaker).
- This is a display order, not an SLA; priority is never changed by it.
- Each item shows subject (links to `/tickets/:id`), category, priority, status, an Escalated badge, customer name (links to `/customers/:id`), customer email and last update.
- **View all my tickets** opens `/tickets`; **Create ticket** opens `/tickets/new`. Summary cards are not links (the ticket list does not read query parameters yet).

## 5. Refresh semantics

- Live on every request; no caching, polling, sockets, timers or stored counters.
- Loads on every visit and on **Refresh**; a newer load cancels an in-flight one.
- Counts and queue are two consecutive queries: a change committed between them shows fully on the next refresh.
- A failed load shows an error with **Retry** and hides the cards and queue; it never shows zeros or stale numbers.

## 6. API

`GET /api/v1/agent-dashboard` — no parameters. Agent: 200. SuperAdmin / Customer: 403. No token, disabled account or changed password: 401.

```json
{
  "summary": { "open": 3, "inProgress": 2, "active": 5, "escalated": 1, "highOrUrgent": 2 },
  "attentionQueue": [
    { "id": "…", "subject": "Cannot sign in", "category": "Technical", "priority": "Urgent", "status": "InProgress",
      "isEscalated": true, "customerId": "…", "customerName": "Ada Lovelace", "customerEmail": "ada@example.com",
      "updatedAtUtc": "2030-01-01T09:00:00+00:00" }
  ],
  "attentionQueueLimit": 10,
  "generatedAtUtc": "2030-01-01T10:00:00+00:00"
}
```

`GET /api/v1/agent-dashboard/tasks` (Agent, DASH-002): the caller's Pending, unarchived task counts (pending, overdue, dueSoon) plus the next 5 tasks; a separate query so either section can fail independently.

## 7. Persistence

Read-only: no migration and no new index. The existing `(AssignedAgentUserId, CreatedAtUtc, Id)` index supports the Agent filter. It does not necessarily cover the other filtered, grouped, ordered or selected columns, so those may be looked up and sorted per Agent, which is acceptable at per-Agent volumes.

Each load runs **two** ticket statements, both under the Agent scope and the Open/InProgress filter: one grouped aggregate (`COUNT(CASE WHEN …)` over `GroupBy(_ => 1)`) and one queue statement (`INNER JOIN Customers`, `ORDER BY` escalation / priority `CASE` rank / `UpdatedAtUtc` / `Id`, `TOP(@limit)`). EF Core translated the grouped query on SQL Server, so the four-`CountAsync` fallback was not needed.

## 8. Deferred Agent Dashboard capabilities

- Tasks & reminders, quick replies and team collaboration: delivered by DASH-002 — see [agent-productivity-and-collaboration.md](agent-productivity-and-collaboration.md). The dashboard adds a separate Tasks section (`GET /api/v1/agent-dashboard/tasks`); the ticket snapshot and its metrics are unchanged.
- SuperAdmin or company-wide dashboard and analytics: shipped in REPORT-001 ([reports-and-management.md](reports-and-management.md)); the Agent Dashboard stays Agent-only.
- Clickable, filtered summary cards (requires Ticket list query-parameter support).
