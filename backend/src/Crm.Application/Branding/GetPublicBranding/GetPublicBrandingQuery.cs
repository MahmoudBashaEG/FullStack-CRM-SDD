using Crm.Application.Abstractions.Persistence;
using Crm.Application.Administration;
using Mediator;

namespace Crm.Application.Branding.GetPublicBranding;

public sealed record GetPublicBrandingQuery : IQuery<PublicBrandingDetails>;

public sealed class GetPublicBrandingQueryHandler(IOrganizationSettingsRepository settings)
    : IQueryHandler<GetPublicBrandingQuery, PublicBrandingDetails>
{
    public async ValueTask<PublicBrandingDetails> Handle(GetPublicBrandingQuery query, CancellationToken cancellationToken) =>
        await settings.GetPublicBrandingAsync(cancellationToken)
            ?? throw new InvalidOperationException("Organization settings row is missing; apply the migrations (dotnet ef database update).");
}
