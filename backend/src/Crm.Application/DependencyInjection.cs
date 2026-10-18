using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Crm.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly, includeInternalTypes: true);
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<Auth.AuthSessionFactory>();
        services.AddScoped<Sla.SlaEvaluator>();
        services.AddScoped<Sla.ITicketCreationAutomation, Sla.TicketCreationAutomation>();
        return services;
    }
}
