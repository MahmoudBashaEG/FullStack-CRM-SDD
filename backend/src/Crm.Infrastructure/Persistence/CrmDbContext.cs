using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public const string ConnectionStringName = "CrmDatabase";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Feature stories add IEntityTypeConfiguration<T> classes under Persistence/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
