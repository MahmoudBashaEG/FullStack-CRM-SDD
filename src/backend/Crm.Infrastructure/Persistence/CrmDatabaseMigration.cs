using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Infrastructure.Persistence;

public static class CrmDatabaseMigration
{
    public static async Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var assembly = database.GetService<IMigrationsAssembly>();
        if (assembly.Migrations.Count == 0 || assembly.ModelSnapshot is null)
            throw new InvalidOperationException("Source-controlled migrations and their model snapshot are required.");
        if (database.Database.HasPendingModelChanges())
            throw new InvalidOperationException("Generate and review an explicit migration for pending model changes.");
        // Avoid even conditional migration-history DDL on an already-current local database.
        if (!(await database.Database.GetPendingMigrationsAsync(cancellationToken)).Any()) return;
        await database.Database.MigrateAsync(cancellationToken);
    }
}
