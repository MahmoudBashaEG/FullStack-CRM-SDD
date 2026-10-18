using Crm.Application.Abstractions.Persistence;
using Crm.Application.Automation;
using Crm.Domain.Automation;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class AutomationRuleRepository(CrmDbContext dbContext) : IAutomationRuleRepository
{
    public void Add(AutomationRule rule) => dbContext.AutomationRules.Add(rule);

    public void AddExecution(AutomationRuleExecution execution) => dbContext.AutomationRuleExecutions.Add(execution);

    public void AddAlert(SlaAlert alert) => dbContext.SlaAlerts.Add(alert);

    public Task<AutomationRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.AutomationRules.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AutomationRule>> ListEnabledAsync(AutomationTrigger trigger, CancellationToken cancellationToken = default) =>
        await dbContext.AutomationRules.AsNoTracking()
            .Where(r => r.IsEnabled && r.Trigger == trigger)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.CreatedAtUtc).ThenBy(r => r.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AutomationRuleDetails>> ListDetailsAsync(CancellationToken cancellationToken = default) =>
        await ProjectDetails(dbContext.AutomationRules.AsNoTracking()).ToListAsync(cancellationToken);

    public Task<AutomationRuleDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        ProjectDetails(dbContext.AutomationRules.AsNoTracking().Where(r => r.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<AutomationExecutionDetails>> ListRecentExecutionsAsync(int limit, CancellationToken cancellationToken = default) =>
        await (
                from e in dbContext.AutomationRuleExecutions.AsNoTracking()
                join r in dbContext.AutomationRules on e.RuleId equals r.Id
                join t in dbContext.Tickets on e.TicketId equals t.Id
                orderby e.OccurredAtUtc descending, e.Id descending
                select new AutomationExecutionDetails(
                    e.Id, r.Id, r.Name, t.Id, t.Subject, e.SlaKind, e.Trigger, e.Action, e.Outcome, e.Detail, e.OccurredAtUtc))
            .Take(limit)
            .ToListAsync(cancellationToken);

    // Ordered here (not by members of the constructed record), with the target Agent's name / active flag left-joined.
    private IQueryable<AutomationRuleDetails> ProjectDetails(IQueryable<AutomationRule> source) =>
        from r in source
        from agent in dbContext.Users.Where(u => u.Id == r.TargetAgentUserId).DefaultIfEmpty()
        orderby r.SortOrder, r.CreatedAtUtc, r.Id
        select new AutomationRuleDetails(
            r.Id,
            r.Name,
            r.IsEnabled,
            r.EnabledAtUtc,
            r.Trigger,
            r.Action,
            r.SortOrder,
            r.CategoryCondition,
            r.PriorityCondition,
            r.AssignedCondition,
            r.SlaKindCondition,
            r.TargetAgentUserId,
            agent == null ? null : agent.FirstName + " " + agent.LastName,
            agent == null ? null : agent.IsActive,
            r.WarningLeadMinutes,
            r.CreatedAtUtc,
            r.UpdatedAtUtc);
}
