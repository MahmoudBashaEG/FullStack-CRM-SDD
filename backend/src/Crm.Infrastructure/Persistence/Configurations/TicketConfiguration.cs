using Crm.Domain.Customers;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Subject).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(4000);

        // Fixed sets stored by name: readable rows, and TICK-002 appended statuses without remapping numbers.
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20);

        // References only (no copies): CRM customer, creating user, optional assigned Agent. Nothing cascades.
        builder.HasOne<Customer>().WithMany().HasForeignKey(t => t.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.AssignedAgentUserId).OnDelete(DeleteBehavior.Restrict);

        // TICK-002 escalation flag (not a status). IsEscalated defaults to false for existing rows.
        builder.Property(t => t.EscalationReason).HasMaxLength(500);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.EscalatedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Optimistic concurrency: two simultaneous writes to one ticket cannot both commit (EfUnitOfWork ⇒ 409).
        builder.Property<byte[]>("Version").IsRowVersion();

        // Append-only history owned by the ticket; inserted in the same SaveChanges as the ticket change.
        builder.HasMany(t => t.Activities).WithOne().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(t => t.Activities).HasField("_activities").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(t => new { t.CreatedAtUtc, t.Id }).IsDescending(true, true);                              // SuperAdmin default list
        builder.HasIndex(t => new { t.AssignedAgentUserId, t.CreatedAtUtc, t.Id }).IsDescending(false, true, true); // Agent-scoped list
        builder.HasIndex(t => new { t.CustomerId, t.CreatedAtUtc, t.Id }).IsDescending(false, true, true);          // customer filter
    }
}
