using Crm.Domain.Administration;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>The singleton row (Id 1, created by the migration). Tracked. null only when the migration was not applied.</summary>
public interface IOrganizationSettingsRepository
{
    Task<OrganizationSettings?> FindAsync(CancellationToken cancellationToken = default);

    /// <summary>Current rowversion of the tracked entity (shadow property "Version").</summary>
    byte[] GetVersion(OrganizationSettings settings);

    /// <summary>Display name of a user id; null when unknown (same as ISlaConfigurationRepository.FindUserNameAsync).</summary>
    Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken = default);
}
