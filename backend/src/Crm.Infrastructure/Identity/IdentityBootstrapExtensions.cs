using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.Identity;

public static class IdentityBootstrapExtensions
{
    /// <summary>Call after builder.Build(). EF design-time tools stop at Build(), so this never runs during 'dotnet ef'.</summary>
    public static async Task BootstrapIdentityAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IdentityBootstrapper>().RunAsync(cancellationToken);
    }
}
