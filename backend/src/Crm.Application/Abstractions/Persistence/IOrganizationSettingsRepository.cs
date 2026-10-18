using Crm.Application.Administration;
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

    /// <summary>Tracked logo row; null = no logo.</summary>
    Task<OrganizationLogo?> FindLogoAsync(CancellationToken cancellationToken = default);

    void AddLogo(OrganizationLogo logo);

    void RemoveLogo(OrganizationLogo logo);

    /// <summary>AsNoTracking projection of the public fields only (never support data or the updater). null = migration missing.</summary>
    Task<PublicBrandingDetails?> GetPublicBrandingAsync(CancellationToken cancellationToken = default);

    /// <summary>AsNoTracking bytes for the public logo route; null = no logo.</summary>
    Task<OrganizationLogoFile?> GetLogoFileAsync(CancellationToken cancellationToken = default);
}
