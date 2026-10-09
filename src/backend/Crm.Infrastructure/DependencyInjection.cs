using Crm.Application.Tickets.Ports;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Authentication;
using Crm.Application.Authentication.Ports;

namespace Crm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        _ = CrmDatabaseSettings.Load(configuration);
        services.AddDbContext<CrmDbContext>(options => options.UseSqlServer(CrmDatabaseSettings.Load(configuration), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo")));
        services.AddCrmIdentity();
        services.TryAddSingleton(TimeProvider.System);
        // Lazy factories keep installation/design-time persistence independent of HTTP signing setup.
        services.AddSingleton(_ => JwtOptions.Load(configuration));
        services.AddSingleton<SigningCertificateProvider>();
        services.AddScoped<IAccessTokenIssuer, JwtIssuer>();
        services.AddScoped<ICurrentAuthenticationReader, CurrentAuthenticationReader>();
        services.AddScoped<ITicketReferenceData, TicketReferenceData>();
        services.AddScoped<ITicketMutationStore, TicketMutationStore>();
        services.AddScoped<ITicketReadService, TicketReadService>();
        services.AddScoped<ReferenceProvisioner>();
        return services;
    }
}
