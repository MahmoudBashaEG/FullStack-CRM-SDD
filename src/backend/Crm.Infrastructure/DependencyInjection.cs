using Crm.Application.Tickets.Ports;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        _ = CrmDatabaseSettings.Load(configuration);
        services.AddDbContext<CrmDbContext>(options => options.UseSqlServer(CrmDatabaseSettings.Load(configuration), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo")));
        services.AddScoped<ITicketReferenceData, TicketReferenceData>();
        services.AddScoped<ITicketMutationStore, TicketMutationStore>();
        services.AddScoped<ITicketReadService, TicketReadService>();
        services.AddScoped<ReferenceProvisioner>();
        return services;
    }
}
