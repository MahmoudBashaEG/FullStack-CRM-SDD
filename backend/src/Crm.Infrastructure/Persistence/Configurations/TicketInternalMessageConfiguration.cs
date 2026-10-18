using Crm.Domain.Collaboration;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class TicketInternalMessageConfiguration : IEntityTypeConfiguration<TicketInternalMessage>
{
    public void Configure(EntityTypeBuilder<TicketInternalMessage> builder)
    {
        builder.ToTable("TicketInternalMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedOnAdd(); // bigint IDENTITY: insertion order is the tie-breaker
        builder.Property(m => m.Body).IsRequired().HasMaxLength(2000);

        builder.HasOne<Ticket>().WithMany().HasForeignKey(m => m.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(m => m.AuthorUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(m => new { m.TicketId, m.CreatedAtUtc, m.Id }).IsDescending(false, true, true);
    }
}
