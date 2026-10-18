# Reports & Management (REPORT-001)

## 1. Scope

SuperAdmin-only, read-only reporting (RM1–RM6) over existing CRM data. No new tables, migrations, indexes, packages, AI calls or writes.

1. **Ticket Reports** — volume, current status/category/priority distributions, creation trend.
2. **SLA Performance** — met/breached/pending per target, compliance, cohort trend, live at-risk block.
3. **Agent Performance** — live workload per Agent, recorded resolution actions in the period.
4. **Customer Satisfaction** — feedback metrics, participation, recent feedback.
5. **Management Dashboard** (`/reports`) — composes the same four endpoints for one shared period.

## 2. Period rules

- Presets `Last7Days`, `Last30Days` (default), `Last90Days`, or `Custom` with `from`/`to` (`yyyy-MM-dd`). `from`/`to` are ignored unless `Custom`.
- Periods are **UTC calendar days**, inclusive. "Last N days" ends today (UTC). SQL uses the half-open range `[from 00:00Z, to+1 00:00Z)`.
- Custom: `from >= 2000-01-01`, `from <= to`, at most 366 days, `to <= today (UTC) + 1`. Violations return 400 `ValidationProblemDetails` (keys `preset`, `from`, `to`). An unknown preset or malformed date is also 400.
- Buckets: daily when the period is at most 31 days, otherwise consecutive 7-day buckets starting at `from` (the last may be shorter).
- The UI prints date-only values with an explicit UTC formatter (`formatUtcDay`, `formatBucketLabel`), never Angular's `DatePipe`: `DatePipe` parses `yyyy-MM-dd` as *local* midnight, which shows the previous day in timezones east of UTC (e.g. Africa/Cairo). Instants (`generatedAtUtc`, feedback times) use `DatePipe` with the `UTC` timezone, which is correct for full timestamps.
- Changing the period clears the previous report/dashboard figures until the new data arrives; a failed load also clears them.

## 3. Metric definitions

| Metric | Scope / anchor | Definition |
|---|---|---|
| Created tickets | Period · `Tickets.CreatedAtUtc` | Count of tickets with `CreatedAtUtc ∈ [start, endExclusive)` |
| Status / category / priority | Period cohort, **current** value | Grouped counts of the same cohort; every enum member is listed (zeros included); each sums to Created tickets |
| Creation trend | Period · `CreatedAtUtc` | Created tickets per daily or 7-day bucket; empty buckets = 0 |
| Response / resolution outcome | Period cohort, current outcome | Per target, exactly one of `NotApplicable` (untracked), `Met`, `Breached`, `Pending` |
| Compliance | Period cohort | `met / (met + breached)`, N/A when 0. Pending and NotApplicable are excluded and shown separately |
| SLA compliance trend | Period cohort per creation bucket | Same formula per bucket; N/A when the bucket has no decided target |
| Active tickets with a breached target | **Live**, all active tickets | `Status ∈ {Open, InProgress}` AND `IsBreached(now)` |
| At-risk response / resolution targets | **Live**, active tracked tickets (≤ 500 scanned) | `SlaTargetView.IsWarning` |
| Agent Open / In Progress / Active (= Open + In Progress) | **Live** | Tickets whose **current** `AssignedAgentUserId` = the Agent |
| Agent active priority mix | **Live** | Active assigned tickets grouped by current priority |
| Agent Resolved / Closed (supplementary) | **Live** | Current assignment with that status; **not** "handled historically" |
| Agent overdue | **Live** | Active assigned tickets with `IsBreached(now)`, once per ticket |
| Unassigned active | **Live** | `AssignedAgentUserId IS NULL` AND Open/InProgress |
| Recorded resolution actions | Period · `TicketActivities.OccurredAtUtc` | `StatusChanged` to `Resolved` by that Agent as recorded actor; plus distinct tickets |
| Resolution actions by others | Period | Same events whose actor is not an Agent account (e.g. SuperAdmin); never credited |
| Feedback received | Period · `TicketFeedback.CreatedAtUtc` | Rows first submitted in the period |
| Average rating | same cohort, **current** rating | Arithmetic mean, 2 decimals; N/A when 0 rows. Missing feedback is never 0 stars |
| Positive rating % | same cohort | `count(rating ≥ 4) / feedback received`, N/A when 0 |
| Rating distribution | same cohort | Counts for 1..5 (zeros included), sum = feedback received |
| Feedback participation | Ticket-created cohort, current eligibility | Tickets created in the period, currently Resolved/Closed, with a feedback row ÷ all such tickets |
| Recent feedback | same cohort as Feedback received | Newest first submission first, at most 20, current rating and comment, submission date |

Percentages are 0–100 with one decimal, computed on the server; `null` means N/A.

## 4. SLA semantics

- Each ticket/target gets one outcome via a SQL `CASE` that mirrors `SlaEvaluator.Target`: no due date ⇒ NotApplicable; completed ⇒ `completed <= due ? Met : Breached`; otherwise `due < now ? Breached : Pending`. The four counts always sum to the cohort size. The grouping runs entirely in SQL (`GROUP BY` over the `CASE` outcomes, projected as integer codes); no per-ticket in-memory fallback is used.
- **First response** is the manual *Mark as Responded* record — an internal acknowledgement, not a message sent to the customer. It can only be recorded on Open/In Progress tickets, so a ticket resolved without one stays Pending until the deadline passes, then Breached. A late recorded response stays Breached.
- **Resolution** uses `SlaResolvedAtUtc`, the **first** transition to Resolved. Reopening/re-resolving never changes the outcome and never counts twice.
- **At risk** reuses the existing SLA *Warning* (pending target, business calendar, enabled `SlaWarningDue` rule whose lead is reached), over the same bounded scan (500) as the `/sla` overview. The response reports `scannedActiveTickets`, `scanLimitReached`, `enabledWarningRules`, `calendarConfigured` so the UI can say why at-risk is 0.
- The live breached count is SQL over all active tickets; the `/sla` overview `breachedCount` covers at most 500 rows and excludes active tickets whose targets are both already completed, so the two can differ.

## 5. Agent attribution

- Resolution actions are credited to the **recorded actor** of each `StatusChanged → Resolved` activity, never to the current assignee. Roles are assigned only at account creation, so an actor's current role equals their role at the time of the action.
- Actions by SuperAdmin or other non-Agent accounts are totalled in `resolutionActionsByOthers` and not credited. A ticket resolved twice by the same Agent counts as two actions and one distinct ticket.
- Overdue = active assigned ticket with at least one breached target, counted once per ticket (same predicate as the ticket list `slaBreached` filter).
- Legacy tickets without activity rows contribute no resolution actions. No ranking, scoring or CSAT-per-Agent exists.

## 6. Customer satisfaction

Rating metrics use the submission cohort (`TicketFeedback.CreatedAtUtc`) with the **current** rating/comment, so editing a rating moves the distribution but not the period membership. Participation uses a different cohort (tickets *created* in the period and currently Resolved/Closed). A reopened ticket leaves the participation numerator and denominator until it is Resolved/Closed again. Ratings are not attributed to Agents. Comments render as text only; no customer name or email is exposed.

## 7. Dashboard composition

`/reports` calls the four report endpoints with the same period (one request each) and renders their fields; no dashboard-specific formula exists. Cards are labelled **Period** or **Live snapshot**. Live Open/In Progress totals come from the Agent report totals (all tickets, assigned or not). Each section has its own error and Retry. Refresh is manual; there is no polling. Sections are fetched separately, so live values can differ by seconds.

## 8. API

All routes: `GET /api/v1/reports/{tickets|sla|agents|customer-satisfaction}?preset=&from=&to=`. SuperAdmin only: 200; anonymous 401; Agent/Customer 403; invalid period 400.

## 9. Angular routes

`/reports`, `/reports/tickets`, `/reports/sla`, `/reports/agents`, `/reports/customer-satisfaction` (guard `roleGuard('SuperAdmin')`). The period travels in the URL query string and is preserved by the dashboard links. Navigation entry "Reports" is SuperAdmin-only.

## 10. Performance notes

Every metric is a grouped SQL statement; only aggregated cells are materialized (plus the existing bounded 500-row at-risk scan). No index was added. For larger data, candidate indexes are `TicketFeedback (CreatedAtUtc)` and `TicketActivities (Type, OccurredAtUtc)`, which need a separate review.

## 11. Out of scope

Agent/Customer report access, drill-down lists, exports, scheduled or e-mailed reports, polling/caching, saved filters, per-category SLA breakdowns, Agent rankings, CSAT trends, AI. Communication Channels remain the last story.
