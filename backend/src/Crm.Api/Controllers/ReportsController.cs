using Crm.Api.Authentication;
using Crm.Api.Contracts.Reports;
using Crm.Application.Reports;
using Crm.Application.Reports.GetAgentPerformanceReport;
using Crm.Application.Reports.GetCustomerSatisfactionReport;
using Crm.Application.Reports.GetSlaReport;
using Crm.Application.Reports.GetTicketReport;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/reports")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class ReportsController(ISender sender) : ControllerBase
{
    private const string Period = "Requires role: SuperAdmin (Agent and Customer: 403). Read-only. preset = Last7Days | Last30Days (default) | Last90Days | Custom; from/to (yyyy-MM-dd, UTC calendar days, inclusive) are required for Custom and ignored otherwise; at most 366 days, from >= 2000-01-01, to <= today (UTC) + 1. Percentages are 0-100 with 1 decimal, null = N/A (no denominator). Buckets: daily up to 31 days, else 7-day buckets starting at from.";

    [EndpointDescription(Period + " Tickets CREATED in the period: volume, CURRENT status/category/priority of that cohort (every value, zeros included; each sums to createdTickets) and the creation trend.")]
    [HttpGet("tickets")]
    public async Task<ActionResult<TicketReportResponse>> Tickets(
        [FromQuery] ReportPreset preset = ReportPreset.Last30Days, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(TicketReportResponse.From(await sender.Send(new GetTicketReportQuery(preset, from, to), cancellationToken)));

    [EndpointDescription(Period + " For tickets created in the period, per target: notApplicable (not SLA-tracked), met, breached (late or past due without a recorded completion), pending; compliance = met / (met + breached). First response = recorded 'Mark as Responded' (internal, not a sent message); resolution = first transition to Resolved. live: NOT affected by the period — active tickets with a breached target, and at-risk targets (SLA Warning rules) over at most 500 active tracked tickets.")]
    [HttpGet("sla")]
    public async Task<ActionResult<SlaReportResponse>> Sla(
        [FromQuery] ReportPreset preset = ReportPreset.Last30Days, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(SlaReportResponse.From(await sender.Send(new GetSlaReportQuery(preset, from, to), cancellationToken)));

    [EndpointDescription(Period + " One row per Agent account. LIVE (not affected by the period): current assignment by status and active priority, overdue = Open/In Progress with at least one breached SLA target (once per ticket). PERIOD: resolutionActions = recorded transitions to Resolved performed by that Agent (event actor and time; never the current assignee); a ticket resolved twice counts twice, resolvedTicketsDistinct once. Actions by SuperAdmin or other accounts are totalled separately and not credited.")]
    [HttpGet("agents")]
    public async Task<ActionResult<AgentPerformanceReportResponse>> Agents(
        [FromQuery] ReportPreset preset = ReportPreset.Last30Days, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(AgentPerformanceReportResponse.From(await sender.Send(new GetAgentPerformanceReportQuery(preset, from, to), cancellationToken)));

    [EndpointDescription(Period + " Feedback FIRST SUBMITTED in the period (current rating/comment after edits): count, average (2 decimals), positive (4-5) percentage, 1-5 distribution, the 20 most recent rows. participation = tickets created in the period and currently Resolved/Closed that have feedback / all such tickets. Missing feedback is never a zero rating.")]
    [HttpGet("customer-satisfaction")]
    public async Task<ActionResult<CustomerSatisfactionReportResponse>> CustomerSatisfaction(
        [FromQuery] ReportPreset preset = ReportPreset.Last30Days, [FromQuery] DateOnly? from = null, [FromQuery] DateOnly? to = null,
        CancellationToken cancellationToken = default) =>
        Ok(CustomerSatisfactionReportResponse.From(await sender.Send(new GetCustomerSatisfactionReportQuery(preset, from, to), cancellationToken)));
}
