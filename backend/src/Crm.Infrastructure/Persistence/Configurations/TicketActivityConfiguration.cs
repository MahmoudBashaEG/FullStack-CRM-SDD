using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class TicketActivityConfiguration : IEntityTypeConfiguration<TicketActivity>
{
    public void Configure(EntityTypeBuilder<TicketActivity> builder)
    {
        builder.ToTable("TicketActivities");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd(); // bigint IDENTITY: append order breaks timestamp ties

        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Field).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.OldValue).HasMaxLength(200);
        builder.Property(a => a.NewValue).HasMaxLength(200);
        builder.Property(a => a.Reason).HasMaxLength(500);

        // References only; nothing cascades (accounts and tickets are never deleted). ActorUserId null = SLA-001 automation.
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.FromAgentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.ToAgentUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.TicketId, a.OccurredAtUtc, a.Id }).IsDescending(false, true, true); // newest-first history page
    }
}
