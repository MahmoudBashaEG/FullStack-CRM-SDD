using Crm.Api.Authentication;
using Crm.Api.Contracts.Common;
using Crm.Api.Contracts.Notifications;
using Crm.Application.AgentTasks.Reminders.CheckTaskReminders;
using Crm.Application.AgentTasks.Reminders.ListTaskReminders;
using Crm.Application.AgentTasks.Reminders.MarkTaskReminderRead;
using Crm.Application.Common.Paging;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/notifications")]
[Authorize(Policy = AuthorizationPolicies.Agent)]
public sealed class NotificationsController(ISender sender) : ControllerBase
{
    [EndpointDescription("Requires role: Agent (SuperAdmin and Customer: 403). Issues any due, not-yet-issued task reminders for the caller (idempotent) and returns the unread count. Called on app open and about every 60 seconds by the Agent UI; not a push channel.")]
    [HttpPost("check")]
    public async Task<ActionResult<TaskReminderCheckResponse>> Check(CancellationToken cancellationToken) =>
        Ok(TaskReminderCheckResponse.From(await sender.Send(new CheckTaskRemindersCommand(User.GetUserId()), cancellationToken)));

    [EndpointDescription("Requires role: Agent. The caller's reminders for tasks they currently own, newest first. Optional unreadOnly. One-based page (default 1), pageSize (default 20, clamped to 100). wasMissed = delivered more than 5 minutes after its scheduled time (delayed issuance, not proof of absence).")]
    [HttpGet]
    public async Task<ActionResult<PagedResponse<TaskReminderResponse>>> List(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize,
        CancellationToken cancellationToken = default)
    {
        var result = await sender.Send(new ListTaskRemindersQuery(User.GetUserId(), unreadOnly, page, pageSize), cancellationToken);
        return Ok(new PagedResponse<TaskReminderResponse>(
            result.Items.Select(TaskReminderResponse.From).ToList(), result.Page, result.PageSize, result.TotalCount));
    }

    [EndpointDescription("Requires role: Agent. Marks one of the caller's reminders as read (already read = success). Never completes the task. A reminder that moved to another owner: 404.")]
    [HttpPut("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        await sender.Send(new MarkTaskReminderReadCommand(User.GetUserId(), id), cancellationToken);
        return NoContent();
    }
}
