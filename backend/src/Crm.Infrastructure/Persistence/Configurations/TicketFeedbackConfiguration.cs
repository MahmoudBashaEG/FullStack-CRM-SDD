using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class TicketFeedbackConfiguration : IEntityTypeConfiguration<TicketFeedback>
{
    public const string TicketIndex = "UX_TicketFeedback_TicketId";

    public void Configure(EntityTypeBuilder<TicketFeedback> builder)
    {
        builder.ToTable("TicketFeedback", t => t.HasCheckConstraint("CK_TicketFeedback_Rating", "[Rating] BETWEEN 1 AND 5"));
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedOnAdd(); // bigint IDENTITY
        builder.Property(f => f.Comment).HasMaxLength(1000);
        builder.Property<byte[]>("Version").IsRowVersion(); // overlapping edits ⇒ 409 (EfUnitOfWork), as Tickets.Version

        // Ownership through Tickets.CustomerId (no copy). Nothing cascades: tickets are never deleted.
        builder.HasOne<Ticket>().WithMany().HasForeignKey(f => f.TicketId).OnDelete(DeleteBehavior.Restrict);

        // One record per ticket (CP5). Referenced by EfUnitOfWork.KnownUniqueIndexes.
        builder.HasIndex(f => f.TicketId).IsUnique().HasDatabaseName(TicketIndex);
    }
}
