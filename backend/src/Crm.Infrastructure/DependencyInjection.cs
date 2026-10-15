using Crm.Application.Abstractions.Persistence;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Options are built lazily (first DbContext resolution), so the API starts without a DB.
        services.AddDbContext<CrmDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(CrmDbContext.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{CrmDbContext.ConnectionStringName}' is not configured. " +
                    "Set it with 'dotnet user-secrets' (see README, Local SQL Server).");
            }

            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(CrmDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
            });
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        return services;
    }
}
