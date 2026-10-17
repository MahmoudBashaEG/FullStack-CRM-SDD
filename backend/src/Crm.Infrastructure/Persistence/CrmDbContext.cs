using Crm.Domain.Customers;
using Crm.Domain.Identity;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public const string ConnectionStringName = "CrmDatabase";

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Ticket> Tickets => Set<Ticket>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's model first, so feature configurations can refine it.
        base.OnModelCreating(modelBuilder);
        // Feature stories add IEntityTypeConfiguration<T> classes under Persistence/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }
}
