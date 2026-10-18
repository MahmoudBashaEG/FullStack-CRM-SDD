using Crm.Api.Authentication;
using Crm.Api.Contracts.AgentTasks;
using Crm.Api.Contracts.Common;
using Crm.Api.Http;
using Crm.Application.AgentTasks;
using Crm.Application.AgentTasks.CreateAgentTask;
using Crm.Application.AgentTasks.GetAgentTask;
using Crm.Application.AgentTasks.ListAgentTasks;
using Crm.Application.AgentTasks.ListLinkableTickets;
using Crm.Application.AgentTasks.ReassignAgentTask;
using Crm.Application.AgentTasks.ScheduleTaskReminder;
using Crm.Application.AgentTasks.SetTaskArchived;
using Crm.Application.AgentTasks.SetTaskCompletion;
using Crm.Application.AgentTasks.SetTaskTicketLink;
using Crm.Application.AgentTasks.UpdateAgentTask;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Domain.AgentTasks;
using Crm.Domain.Identity;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/tasks")]
[Authorize(Policy = AuthorizationPolicies.Staff)]
public sealed class AgentTasksController(ISender sender) : ControllerBase
{
    private const string Concurrency = " Requires If-Match: the task's ETag (428 if missing, 400 if malformed, 409 if stale).";

    [EndpointDescription("Requires role: SuperAdmin (all tasks; optional assignedAgentUserId filter) or Agent (own tasks only; filters only narrow). One-based page (default 1), pageSize (default 20, clamped to 100), optional search on title, status, dueState (Overdue, DueSoon, NoDueDate), archived (default false), hasReminder. Order: no-due-date last, due ascending, id.")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AgentTaskResponse>>> List(
        [FromQuery] string? search,
        [FromQuery] AgentTaskStatus? status,
        [FromQuery] TaskDueFilter? dueState,
        [FromQuery] bool archived = false,
        [FromQuery] bool? hasReminder = null,
        [FromQuery] Guid? assignedAgentUserId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(
            new ListAgentTasksQuery(Actor, search, status, dueState, archived, hasReminder, assignedAgentUserId, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<AgentTaskResponse>(
            result.Items.Select(AgentTaskResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: SuperAdmin (any task) or Agent (own tasks only; others 404). Returns the ETag header (the task version).")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AgentTaskResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        TaskResult(await sender.Send(new GetAgentTaskQuery(Actor, id), cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin or Agent. Picker data only: at most 10 tickets currently assigned to the owner. Agent: the owner is the caller (another ownerUserId is 400). SuperAdmin: ownerUserId is required (an active Agent). Linking is re-validated on write.")]
    [HttpGet("linkable-tickets")]
    public async Task<ActionResult<IReadOnlyList<LinkableTicketResponse>>> LinkableTickets(
        [FromQuery] Guid? ownerUserId, [FromQuery] string? search, CancellationToken cancellationToken)
    {
        var tickets = await sender.Send(new ListLinkableTicketsQuery(Actor, ownerUserId, search), cancellationToken);
        return Ok(tickets.Select(LinkableTicketResponse.From).ToList());
    }

    [EndpointDescription("Requires role: SuperAdmin or Agent. Agent: the owner is the caller; omit assignedAgentUserId. SuperAdmin: assignedAgentUserId (an active Agent) is required. Optional ticketId must be currently assigned to the owner. Optional remindAtUtc must be in the future and at or before dueAtUtc. Returns the ETag header; no If-Match needed on create.")]
    [HttpPost]
    [ProducesResponseType<AgentTaskResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AgentTaskResponse>> Create(CreateAgentTaskRequest request, CancellationToken cancellationToken)
    {
        var task = await sender.Send(
            new CreateAgentTaskCommand(
                Actor, request.Title, request.Description, request.AssignedAgentUserId, request.TicketId, request.DueAtUtc, request.RemindAtUtc),
            cancellationToken);
        Response.Headers.ETag = EntityTags.Format(task.Version);
        return CreatedAtAction(nameof(Get), new { id = task.Id }, AgentTaskResponse.From(task));
    }

    [EndpointDescription("Requires role: SuperAdmin (any task) or Agent (own tasks only). Title, description and due time only; the link, owner and reminder have their own endpoints." + Concurrency)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AgentTaskResponse>> Update(Guid id, UpdateAgentTaskRequest request, CancellationToken cancellationToken) =>
        TaskResult(await sender.Send(
            new UpdateAgentTaskCommand(Actor, id, EntityTags.RequireIfMatch(Request), request.Title, request.Description, request.DueAtUtc),
            cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin (any task) or Agent (own tasks only). The ticket must be currently assigned to the task owner (400 otherwise)." + Concurrency)]
    [HttpPut("{id:guid}/ticket-link")]
    public async Task<ActionResult<AgentTaskResponse>> SetTicketLink(Guid id, SetTaskTicketLinkRequest request, CancellationToken cancellationToken) =>
        TaskResult(await sender.Send(
            new SetTaskTicketLinkCommand(Actor, id, EntityTags.RequireIfMatch(Request), request.TicketId), cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin (any task) or Agent (own tasks only). Removes the ticket link (also when it is no longer accessible)." + Concurrency)]
    [HttpDelete("{id:guid}/ticket-link")]
    public async Task<ActionResult<AgentTaskResponse>> RemoveTicketLink(Guid id, CancellationToken cancellationToken) =>
        TaskResult(await sender.Send(
            new SetTaskTicketLinkCommand(Actor, id, EntityTags.RequireIfMatch(Request), null), cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin (any task) or Agent (own tasks only). One reminder per task, delivered in-app to the task's current owner only. Must be in the future and at or before the due time; Pending, unarchived tasks only (409 otherwise). The same time is a no-op." + Concurrency)]
    [HttpPut("{id:guid}/reminder")]
    public async Task<ActionResult<AgentTaskResponse>> ScheduleReminder(Guid id, ScheduleTaskReminderRequest request, CancellationToken cancellationToken) =>
        TaskResult(await sender.Send(
            new ScheduleTaskReminderCommand(Actor, id, EntityTags.RequireIfMatch(Request), request.RemindAtUtc), cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin (any task) or Agent (own tasks only). Cancels the pending reminder (no-op if none)." + Concurrency)]
    [HttpDelete("{id:guid}/reminder")]
    public async Task<ActionResult<AgentTaskResponse>> CancelReminder(Guid id, CancellationToken cancellationToken) =>
        TaskResult(await sender.Send(
            new ScheduleTaskReminderCommand(Actor, id, EntityTags.RequireIfMatch(Request), null), cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin (any task) or Agent (own tasks only). Completing cancels a pending reminder; repeating is a no-op." + Concurrency)]
    [HttpPost("{id:guid}/completion")]
    public async Task<ActionResult<AgentTaskResponse>> Complete(Guid id, CancellationToken cancellationToken) =>
        TaskResult(await sender.Send(
            new SetTaskCompletionCommand(Actor, id, EntityTags.RequireIfMatch(Request), true), cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin (any task) or Agent (own tasks only). Reopens a completed task; the reminder is not recreated." + Concurrency)]
    [HttpDelete("{id:guid}/completion")]
    public async Task<ActionResult<AgentTaskResponse>> Reopen(Guid id, CancellationToken cancellationToken) =>
        TaskResult(await sender.Send(
            new SetTaskCompletionCommand(Actor, id, EntityTags.RequireIfMatch(Request), false), cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin (any task) or Agent (own tasks only). Archiving cancels a pending reminder. There is no hard delete." + Concurrency)]
    [HttpPost("{id:guid}/archive")]
    public async Task<ActionResult<AgentTaskResponse>> Archive(Guid id, CancellationToken cancellationToken) =>
        TaskResult(await sender.Send(
            new SetTaskArchivedCommand(Actor, id, EntityTags.RequireIfMatch(Request), true), cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin (any task) or Agent (own tasks only). Restores an archived task; the reminder is not recreated." + Concurrency)]
    [HttpDelete("{id:guid}/archive")]
    public async Task<ActionResult<AgentTaskResponse>> Restore(Guid id, CancellationToken cancellationToken) =>
        TaskResult(await sender.Send(
            new SetTaskArchivedCommand(Actor, id, EntityTags.RequireIfMatch(Request), false), cancellationToken));

    [EndpointDescription("Requires role: SuperAdmin. Reassigns the task to an active Agent (400 otherwise). Unread reminders of the task move with it. 409 when the linked ticket is not assigned to the new owner and removeTicketLink is false." + Concurrency)]
    [HttpPut("{id:guid}/assignee")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    public async Task<ActionResult<AgentTaskResponse>> Reassign(Guid id, ReassignAgentTaskRequest request, CancellationToken cancellationToken) =>
        TaskResult(await sender.Send(
            new ReassignAgentTaskCommand(Actor, id, EntityTags.RequireIfMatch(Request), request.AssignedAgentUserId, request.RemoveTicketLink),
            cancellationToken));

    // F1: every response that carries one task also carries its version as a strong ETag.
    private ActionResult<AgentTaskResponse> TaskResult(AgentTaskDetails details)
    {
        Response.Headers.ETag = EntityTags.Format(details.Version);
        return Ok(AgentTaskResponse.From(details));
    }

    // From the validated JWT only (sub + role claims). Never from the request.
    private StaffActor Actor => new(User.GetUserId(), User.IsInRole(UserRoles.SuperAdmin));
}
