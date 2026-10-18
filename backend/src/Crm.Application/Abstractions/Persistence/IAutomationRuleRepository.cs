using Crm.Application.Automation;
using Crm.Domain.Automation;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>Add* only stage; the caller saves through IUnitOfWork.</summary>
public interface IAutomationRuleRepository
{
    void Add(AutomationRule rule);

    void AddExecution(AutomationRuleExecution execution);

    void AddAlert(SlaAlert alert);

    /// <summary>Tracked, for updates.</summary>
    Task<AutomationRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Enabled rules of one trigger, ordered SortOrder, CreatedAtUtc, Id. No tracking.</summary>
    Task<IReadOnlyList<AutomationRule>> ListEnabledAsync(AutomationTrigger trigger, CancellationToken cancellationToken = default);

    /// <summary>All rules (same order) with the target Agent's name and active flag joined.</summary>
    Task<IReadOnlyList<AutomationRuleDetails>> ListDetailsAsync(CancellationToken cancellationToken = default);

    Task<AutomationRuleDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AutomationExecutionDetails>> ListRecentExecutionsAsync(int limit, CancellationToken cancellationToken = default);
}
