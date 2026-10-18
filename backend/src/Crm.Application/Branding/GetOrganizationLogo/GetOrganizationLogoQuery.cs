using Crm.Application.Abstractions.Persistence;
using Crm.Application.Administration;
using Crm.Application.Common.Exceptions;
using Mediator;

namespace Crm.Application.Branding.GetOrganizationLogo;

public sealed record GetOrganizationLogoQuery : IQuery<OrganizationLogoFile>;

public sealed class GetOrganizationLogoQueryHandler(IOrganizationSettingsRepository settings)
    : IQueryHandler<GetOrganizationLogoQuery, OrganizationLogoFile>
{
    public async ValueTask<OrganizationLogoFile> Handle(GetOrganizationLogoQuery query, CancellationToken cancellationToken) =>
        await settings.GetLogoFileAsync(cancellationToken) ?? throw new NotFoundException("No organization logo is configured.");
}
