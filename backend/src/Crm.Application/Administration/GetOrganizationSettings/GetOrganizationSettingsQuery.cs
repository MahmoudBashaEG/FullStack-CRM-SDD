using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Administration;
using Mediator;

namespace Crm.Application.Administration.GetOrganizationSettings;

public sealed record GetOrganizationSettingsQuery : IQuery<OrganizationSettingsDetails>;

public sealed class GetOrganizationSettingsQueryHandler(IOrganizationSettingsRepository settings)
    : IQueryHandler<GetOrganizationSettingsQuery, OrganizationSettingsDetails>
{
    public async ValueTask<OrganizationSettingsDetails> Handle(GetOrganizationSettingsQuery query, CancellationToken cancellationToken) =>
        await OrganizationSettingsMapper.ToDetailsAsync(await settings.RequireAsync(cancellationToken), settings, cancellationToken);
}

internal static class OrganizationSettingsMapper
{
    public static async Task<OrganizationSettings> RequireAsync(this IOrganizationSettingsRepository repository, CancellationToken ct) =>
        await repository.FindAsync(ct)
            ?? throw new InvalidOperationException("Organization settings row is missing; apply the migrations (dotnet ef database update).");

    public static async Task<OrganizationSettingsDetails> ToDetailsAsync(
        OrganizationSettings s, IOrganizationSettingsRepository repository, CancellationToken ct) =>
        new(s.IsConfigured, s.DisplayName, s.SupportEmail, s.SupportPhone, s.SupportInformation, s.UpdatedAtUtc,
            s.UpdatedByUserId is { } by ? await repository.FindUserNameAsync(by, ct) : null,
            repository.GetVersion(s));
}
