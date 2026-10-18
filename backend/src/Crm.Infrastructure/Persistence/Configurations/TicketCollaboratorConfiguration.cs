using Crm.Domain.Collaboration;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class TicketCollaboratorConfiguration : IEntityTypeConfiguration<TicketCollaborator>
{
    public const string ActiveMembershipIndex = "UX_TicketCollaborators_ActiveMembership";

    public void Configure(EntityTypeBuilder<TicketCollaborator> builder)
    {
        builder.ToTable("TicketCollaborators");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.HasOne<Ticket>().WithMany().HasForeignKey(c => c.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.AgentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.AddedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.RemovedByUserId).OnDelete(DeleteBehavior.Restrict);

        // One active membership per (ticket, Agent). Referenced by EfUnitOfWork.KnownUniqueIndexes and by the lock query.
        builder.HasIndex(c => new { c.TicketId, c.AgentUserId }).IsUnique().HasFilter("[RemovedAtUtc] IS NULL").HasDatabaseName(ActiveMembershipIndex);
        builder.HasIndex(c => new { c.AgentUserId, c.RemovedAtUtc }); // My Collaborations / access check
    }
}
