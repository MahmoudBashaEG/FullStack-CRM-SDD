using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.Customers;

public static class CustomerProfileInitializerExtensions
{
    /// <summary>Call after BootstrapIdentityAsync and before app.Run(), so no request races the backfill.</summary>
    public static async Task InitializeCustomerProfilesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<CustomerProfileInitializer>().RunAsync(cancellationToken);
    }
}
