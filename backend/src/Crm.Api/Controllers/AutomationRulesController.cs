using Crm.Api.Authentication;
using Crm.Api.Contracts.Automation;
using Crm.Application.Automation.CreateAutomationRule;
using Crm.Application.Automation.GetAutomationRule;
using Crm.Application.Automation.ListAutomationRules;
using Crm.Application.Automation.UpdateAutomationRule;
using Mediator;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Controllers;

[ApiController]
[Route(ApiRoutes.V1 + "/automation-rules")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class AutomationRulesController(ISender sender) : ControllerBase
{
    private const string Combinations =
        "Valid combinations: TicketCreated→AssignToAgent (targetAgentUserId required, an active Agent); " +
        "SlaWarningDue→CreateInAppNotification (warningLeadMinutes required, 1–10080); " +
        "SlaBreached→CreateInAppNotification or EscalateTicket. Conditions are optional: categoryCondition, priorityCondition, " +
        "assignedCondition and slaKindCondition (the last two are not allowed for TicketCreated). Rules are never deleted — disable them.";

    [EndpointDescription("Requires role: SuperAdmin. All rules ordered by sortOrder, creation time, id.")]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AutomationRuleResponse>>> List(CancellationToken cancellationToken) =>
        Ok((await sender.Send(new ListAutomationRulesQuery(), cancellationToken)).Select(AutomationRuleResponse.From).ToList());

    [EndpointDescription("Requires role: SuperAdmin. One rule; unknown id: 404.")]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AutomationRuleResponse>> Get(Guid id, CancellationToken cancellationToken) =>
        Ok(AutomationRuleResponse.From(await sender.Send(new GetAutomationRuleQuery(id), cancellationToken)));

    [EndpointDescription("Requires role: SuperAdmin. Creates a rule. " + Combinations)]
    [HttpPost]
    [ProducesResponseType<AutomationRuleResponse>(StatusCodes.Status201Created)]
    public async Task<ActionResult<AutomationRuleResponse>> Create(AutomationRuleRequest request, CancellationToken cancellationToken)
    {
        var rule = await sender.Send(
            new CreateAutomationRuleCommand(
                User.GetUserId(), request.Name, request.IsEnabled, request.Trigger, request.Action, request.SortOrder, request.CategoryCondition,
                request.PriorityCondition, request.AssignedCondition, request.SlaKindCondition, request.TargetAgentUserId, request.WarningLeadMinutes),
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = rule.Id }, AutomationRuleResponse.From(rule));
    }

    [EndpointDescription("Requires role: SuperAdmin. Replaces a rule's definition; re-enabling a disabled rule means it only reacts to SLA events due afterwards. " + Combinations)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AutomationRuleResponse>> Update(Guid id, AutomationRuleRequest request, CancellationToken cancellationToken) =>
        Ok(AutomationRuleResponse.From(await sender.Send(
            new UpdateAutomationRuleCommand(
                id, User.GetUserId(), request.Name, request.IsEnabled, request.Trigger, request.Action, request.SortOrder, request.CategoryCondition,
                request.PriorityCondition, request.AssignedCondition, request.SlaKindCondition, request.TargetAgentUserId, request.WarningLeadMinutes),
            cancellationToken)));
}
