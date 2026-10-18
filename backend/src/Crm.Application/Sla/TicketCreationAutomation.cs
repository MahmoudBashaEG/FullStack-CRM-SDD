using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Automation;
using Crm.Domain.Identity;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.Application.Sla;

public sealed class TicketCreationAutomation(
    ISlaConfigurationRepository configuration,
    IAutomationRuleRepository rules,
    IIdentityService identity,
    TimeProvider timeProvider) : ITicketCreationAutomation
{
    public async Task ApplyAsync(Ticket ticket, CancellationToken cancellationToken)
    {
        var policy = await configuration.FindPolicyAsync(cancellationToken);
        var calendar = await configuration.FindCalendarAsync(cancellationToken);
        if (policy is { IsEnabled: true } && calendar is not null)
        {
            var target = policy.TargetFor(ticket.Priority);
            var calendarRules = calendar.ToRules();
            ticket.StartSla(
                target.FirstResponseMinutes,
                BusinessTimeCalculator.AddBusinessMinutes(ticket.CreatedAtUtc, target.FirstResponseMinutes, calendarRules),
                target.ResolutionMinutes,
                BusinessTimeCalculator.AddBusinessMinutes(ticket.CreatedAtUtc, target.ResolutionMinutes, calendarRules),
                calendar.TimeZoneId);
        }

        if (ticket.AssignedAgentUserId is not null)
        {
            return; // S5: a chosen or self assignment is never overridden
        }

        foreach (var rule in await rules.ListEnabledAsync(AutomationTrigger.TicketCreated, cancellationToken))
        {
            if (!rule.Matches(ticket.Category, ticket.Priority, isAssigned: false, kind: null) || rule.TargetAgentUserId is not { } agentId)
            {
                continue;
            }

            var agent = await identity.FindByIdAsync(agentId, cancellationToken);
            if (agent is not { Role: UserRoles.Agent, IsActive: true })
            {
                continue; // inactive/removed target: skip, never assign a disabled account
            }

            var now = timeProvider.GetUtcNow();
            ticket.AssignAutomatically(agentId, rule.Name, now);
            rules.AddExecution(AutomationRuleExecution.Create(
                rule.Id, ticket.Id, null, rule.Trigger, rule.Action, AutomationOutcome.Assigned, $"Assigned to {agent.FirstName} {agent.LastName}", now));
            return; // at most one automatic assignment per ticket
        }
    }
}
