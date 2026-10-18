using Crm.Api.Authentication;
using Crm.Api.Contracts.Sla;
using Crm.Application.Sla.GetBusinessCalendar;
using Crm.Application.Sla.GetSlaOverview;
using Crm.Application.Sla.GetSlaPolicy;
using Crm.Application.Sla.PreviewDueDate;
using Crm.Application.Sla.UpdateBusinessCalendar;
using Crm.Application.Sla.UpdateSlaPolicy;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/sla")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class SlaController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: SuperAdmin. The one global SLA policy. isConfigured=false until it is first saved.")]
    [HttpGet("policy")]
    public async Task<ActionResult<SlaPolicyResponse>> GetPolicy(CancellationToken cancellationToken) =>
        Ok(SlaPolicyResponse.From(await sender.Send(new GetSlaPolicyQuery(), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin. Exactly one target per priority (Low, Medium, High, Urgent), positive business minutes; resolution not shorter than first response. Applies to tickets created afterwards only; existing deadlines never change.")]
    [HttpPut("policy")]
    public async Task<ActionResult<SlaPolicyResponse>> UpdatePolicy(UpdateSlaPolicyRequest request, CancellationToken cancellationToken)
    {
        var targets = request.Targets?.Select(t => new SlaTargetInput(t.Priority, t.FirstResponseMinutes, t.ResolutionMinutes)).ToList();
        return Ok(SlaPolicyResponse.From(
            await sender.Send(new UpdateSlaPolicyCommand(User.GetUserId(), request.IsEnabled, targets), cancellationToken)));
    }

    [EndpointDescription("Requires role: SuperAdmin. The one global business calendar. isConfigured=false until it is first saved.")]
    [HttpGet("calendar")]
    public async Task<ActionResult<BusinessCalendarResponse>> GetCalendar(CancellationToken cancellationToken) =>
        Ok(BusinessCalendarResponse.From(await sender.Send(new GetBusinessCalendarQuery(), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin. IANA time zone id (e.g. Africa/Cairo), at least one working weekday, one daily opening interval (opensAt < closesAt, \"HH:mm:ss\") and holiday dates (\"yyyy-MM-dd\", unique). Applies to tickets created afterwards only.")]
    [HttpPut("calendar")]
    public async Task<ActionResult<BusinessCalendarResponse>> UpdateCalendar(UpdateBusinessCalendarRequest request, CancellationToken cancellationToken)
    {
        var holidays = request.Holidays?.Select(h => new HolidayInput(h.Date, h.Name)).ToList();
        return Ok(BusinessCalendarResponse.From(await sender.Send(
            new UpdateBusinessCalendarCommand(
                User.GetUserId(), request.TimeZoneId, request.WorkingDays, request.OpensAt, request.ClosesAt, holidays),
            cancellationToken)));
    }

    [EndpointDescription("Requires role: SuperAdmin. Deterministic due-date check with the saved calendar: startUtc (default now) + minutes of business time. 409 when no calendar is saved.")]
    [HttpGet("calendar/preview")]
    public async Task<ActionResult<SlaDuePreviewResponse>> PreviewDueDate(
        [FromQuery] DateTimeOffset? startUtc, [FromQuery] int minutes, CancellationToken cancellationToken) =>
        Ok(SlaDuePreviewResponse.From(await sender.Send(new PreviewSlaDueDateQuery(startUtc, minutes), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin. Warning and breached SLA-tracked Open/In Progress tickets (at most 50 each) plus recent automation results. Counts cover the first 500 active tracked tickets.")]
    [HttpGet("overview")]
    public async Task<ActionResult<SlaOverviewResponse>> Overview(CancellationToken cancellationToken) =>
        Ok(SlaOverviewResponse.From(await sender.Send(new GetSlaOverviewQuery(), cancellationToken)));
}
