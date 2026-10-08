using Crm.Infrastructure.Tickets;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : DbContext(options)
{
    public DbSet<CategoryReference> Categories => Set<CategoryReference>();
    public DbSet<AgentReference> Agents => Set<AgentReference>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketHistoryEntry> History => Set<TicketHistoryEntry>();
    protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
}
