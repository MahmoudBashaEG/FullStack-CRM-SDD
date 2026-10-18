using Crm.Domain.Sla;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>The two SLA singletons (Id 1). Find* are tracked (for update) and null until first saved. Add only stages.</summary>
public interface ISlaConfigurationRepository
{
    Task<SlaPolicy?> FindPolicyAsync(CancellationToken cancellationToken = default);

    Task<BusinessCalendar?> FindCalendarAsync(CancellationToken cancellationToken = default);

    void Add(SlaPolicy policy);

    void Add(BusinessCalendar calendar);

    /// <summary>Display name of a user id (UpdatedByUserId); null when unknown.</summary>
    Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken = default);
}
