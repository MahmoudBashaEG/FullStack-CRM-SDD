using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Crm.Domain.Automation;
using Crm.Domain.Identity;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Sla.RunSlaAutomation;

/// <summary>
/// SLA-001 §7: one worker pass. NOT ITransactionalCommand — each (rule, ticket, kind) runs in its own short transaction through
/// IUnitOfWork.ExecuteInTransactionAsync, so one failure (409 from Tickets.Version, deadlock) never blocks the others; the next pass retries it.
/// Idempotent: the unique execution key makes a replayed or duplicated item insert nothing twice.
/// </summary>
public sealed record RunSlaAutomationCommand : ICommand<SlaAutomationRunResult>;

public sealed record SlaAutomationRunResult(int Processed, int Skipped, int Failed, DateTimeOffset RanAtUtc);

public sealed class RunSlaAutomationCommandHandler(
    IAutomationRuleRepository rules,
    ISlaMonitoringRepository monitoring,
    ISlaConfigurationRepository configuration,
    ITicketRepository tickets,
    IIdentityService identity,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider,
    ILogger<RunSlaAutomationCommandHandler> logger) : ICommandHandler<RunSlaAutomationCommand, SlaAutomationRunResult>
{
    private static readonly SlaKind[] BothKinds = [SlaKind.FirstResponse, SlaKind.Resolution];

    public async ValueTask<SlaAutomationRunResult> Handle(RunSlaAutomationCommand command, CancellationToken cancellationToken)
    {
        // The same `now` and calendar are used for the whole pass (F3). No calendar => warnings are skipped, as the display shows none.
        var now = timeProvider.GetUtcNow();
        var calendar = (await configuration.FindCalendarAsync(cancellationToken))?.ToRules();
        var superAdmins = (await identity.ListByRoleAsync(UserRoles.SuperAdmin, cancellationToken))
            .Where(u => u.IsActive)
            .Select(u => u.Id)
            .ToList();
        var accounts = new Dictionary<Guid, bool>(); // per-pass cache: user id => is an active Agent

        var processed = 0;
        var skipped = 0;
        var failed = 0;

        var triggers = new[] { AutomationTrigger.SlaWarningDue, AutomationTrigger.SlaBreached };
        foreach (var trigger in triggers)
        {
            if (trigger == AutomationTrigger.SlaWarningDue && calendar is null)
            {
                continue;
            }

            foreach (var rule in await rules.ListEnabledAsync(trigger, cancellationToken))
            {
                foreach (var kind in rule.SlaKindCondition is { } only ? new[] { only } : BothKinds)
                {
                    var attempted = new HashSet<Guid>();
                    for (var page = 0; page < SlaRules.WorkerMaxPagesPerRule; page++)
                    {
                        var query = new SlaCandidateQuery(
                            kind, rule.CategoryCondition, rule.PriorityCondition, rule.AssignedCondition, rule.Id, now,
                            rule.EnabledAtUtc ?? rule.CreatedAtUtc, attempted, SlaRules.WorkerBatchSize);
                        var candidates = trigger == AutomationTrigger.SlaWarningDue
                            ? await monitoring.ListWarningCandidatesAsync(query, cancellationToken)
                            : await monitoring.ListBreachCandidatesAsync(query, cancellationToken);

                        var reachedHorizon = false;
                        foreach (var candidate in candidates)
                        {
                            // Warnings: due ASC => remaining business minutes ASC. The first candidate not yet at the threshold ends this (rule, kind).
                            if (trigger == AutomationTrigger.SlaWarningDue
                                && !SlaEvaluator.IsWarningThresholdReached(rule, SlaEvaluator.RemainingBusinessMinutes(now, candidate.DueAtUtc, calendar)))
                            {
                                reachedHorizon = true;
                                break;
                            }

                            attempted.Add(candidate.TicketId);
                            try
                            {
                                var done = await unitOfWork.ExecuteInTransactionAsync(
                                    ct => ProcessAsync(rule, kind, candidate.TicketId, now, calendar, superAdmins, accounts, ct),
                                    cancellationToken);
                                if (done)
                                {
                                    processed++;
                                }
                                else
                                {
                                    skipped++;
                                }
                            }
                            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                            {
                                throw;
                            }
                            catch (Exception exception)
                            {
                                failed++;
                                logger.LogWarning(
                                    exception, "SLA automation failed for rule {RuleId}, ticket {TicketId}, {Kind}; retrying next pass.",
                                    rule.Id, candidate.TicketId, kind);
                            }
                        }

                        if (reachedHorizon || candidates.Count < SlaRules.WorkerBatchSize)
                        {
                            break;
                        }
                    }
                }
            }
        }

        return new SlaAutomationRunResult(processed, skipped, failed, now);
    }

    // Replay-safe: reloads the ticket, re-checks eligibility, then writes the execution row and its effect in this one transaction.
    // true = an effect was recorded; false = nothing written (no longer eligible).
    private async Task<bool> ProcessAsync(
        AutomationRule rule, SlaKind kind, Guid ticketId, DateTimeOffset now, BusinessCalendarRules? calendar,
        IReadOnlyList<Guid> superAdmins, Dictionary<Guid, bool> accounts, CancellationToken cancellationToken)
    {
        var ticket = await tickets.GetByIdAsync(ticketId, null, cancellationToken);
        if (ticket is null || !IsEligible(ticket, rule, kind, now, calendar, out var due))
        {
            return false;
        }

        AutomationRuleExecution execution;
        if (rule.Action == AutomationAction.CreateInAppNotification)
        {
            var assignedIsActiveAgent = ticket.AssignedAgentUserId is { } assigned
                && await IsActiveAgentAsync(assigned, accounts, cancellationToken);
            var assignee = assignedIsActiveAgent ? new List<Guid> { ticket.AssignedAgentUserId!.Value } : new List<Guid>();
            var recipients = rule.Trigger == AutomationTrigger.SlaWarningDue
                ? (assignee.Count > 0 ? assignee : superAdmins.ToList()) // unassigned (or inactive assignee) => SuperAdmins
                : assignee.Union(superAdmins).ToList(); // breach: assigned Agent and every SuperAdmin

            // Zero recipients still records the execution (detail below) so the item is not retried every pass.
            execution = AutomationRuleExecution.Create(
                rule.Id, ticket.Id, kind, rule.Trigger, rule.Action, AutomationOutcome.Notified,
                recipients.Count == 0 ? "No active recipient" : $"{recipients.Count} recipient(s)", now);
            rules.AddExecution(execution);

            var type = rule.Trigger == AutomationTrigger.SlaWarningDue ? SlaAlertType.Warning : SlaAlertType.Breach;
            foreach (var recipient in recipients)
            {
                rules.AddAlert(SlaAlert.Create(execution, recipient, type, kind, due, now));
            }
        }
        else
        {
            AutomationOutcome outcome;
            if (ticket.IsEscalated)
            {
                outcome = AutomationOutcome.AlreadyEscalated;
            }
            else if (!TicketWorkflow.CanEscalate(ticket.Status))
            {
                outcome = AutomationOutcome.NotEscalatable;
            }
            else
            {
                var what = kind == SlaKind.FirstResponse ? "first response" : "resolution";
                ticket.EscalateAutomatically($"SLA breached: {what} was due {due:yyyy-MM-dd HH:mm} UTC (rule \"{rule.Name}\").", now);
                outcome = AutomationOutcome.Escalated;
            }

            execution = AutomationRuleExecution.Create(rule.Id, ticket.Id, kind, rule.Trigger, rule.Action, outcome, null, now);
            rules.AddExecution(execution);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static bool IsEligible(
        Ticket ticket, AutomationRule rule, SlaKind kind, DateTimeOffset now, BusinessCalendarRules? calendar, out DateTimeOffset due)
    {
        due = default;
        if (!ticket.IsSlaTracked || !TicketWorkflow.CanEscalate(ticket.Status))
        {
            return false;
        }

        DateTimeOffset? completed;
        if (kind == SlaKind.FirstResponse)
        {
            due = ticket.SlaFirstResponseDueAtUtc!.Value;
            completed = ticket.FirstRespondedAtUtc;
        }
        else
        {
            due = ticket.SlaResolutionDueAtUtc!.Value;
            completed = ticket.SlaResolvedAtUtc;
        }

        if (due < (rule.EnabledAtUtc ?? rule.CreatedAtUtc)
            || !rule.Matches(ticket.Category, ticket.Priority, ticket.AssignedAgentUserId is not null, kind))
        {
            return false;
        }

        if (rule.Trigger == AutomationTrigger.SlaBreached)
        {
            return due <= now && (completed is null || completed > due);
        }

        return completed is null && now < due
            && SlaEvaluator.IsWarningThresholdReached(rule, SlaEvaluator.RemainingBusinessMinutes(now, due, calendar));
    }

    private async Task<bool> IsActiveAgentAsync(Guid userId, Dictionary<Guid, bool> cache, CancellationToken cancellationToken)
    {
        if (!cache.TryGetValue(userId, out var isActiveAgent))
        {
            isActiveAgent = await identity.FindByIdAsync(userId, cancellationToken) is { Role: UserRoles.Agent, IsActive: true };
            cache[userId] = isActiveAgent;
        }

        return isActiveAgent;
    }
}
