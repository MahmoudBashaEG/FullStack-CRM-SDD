using Crm.Infrastructure.Tickets;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Authentication;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options) : IdentityUserContext<LocalUser, Guid>(options)
{
    public DbSet<AuthenticationSession> AuthenticationSessions => Set<AuthenticationSession>();
    public DbSet<RefreshCredential> RefreshCredentials => Set<RefreshCredential>();
    public DbSet<InitialAdminSetup> InitialAdminSetup => Set<InitialAdminSetup>();
    public DbSet<CategoryReference> Categories => Set<CategoryReference>();
    public DbSet<AgentReference> Agents => Set<AgentReference>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketHistoryEntry> History => Set<TicketHistoryEntry>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes().SelectMany(x => x.GetForeignKeys()).Where(x => x.PrincipalEntityType.ClrType == typeof(LocalUser)))
            foreignKey.DeleteBehavior = DeleteBehavior.Restrict;
    }
}
