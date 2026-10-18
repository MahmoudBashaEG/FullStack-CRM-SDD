using Crm.Domain.Automation;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class SlaAlertConfiguration : IEntityTypeConfiguration<SlaAlert>
{
    public void Configure(EntityTypeBuilder<SlaAlert> builder)
    {
        builder.ToTable("SlaAlerts");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedOnAdd(); // bigint IDENTITY: insertion order is the tie-breaker

        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Kind).HasConversion<string>().HasMaxLength(20);

        // ExecutionId is store-generated: it is fixed up through the private navigation set in SlaAlert.Create.
        builder.HasOne<AutomationRuleExecution>("Execution").WithMany().HasForeignKey(a => a.ExecutionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(a => a.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.RecipientUserId).OnDelete(DeleteBehavior.Restrict);

        // Not in EfUnitOfWork.KnownUniqueIndexes: a violation is a defect.
        builder.HasIndex(a => new { a.ExecutionId, a.RecipientUserId }).IsUnique().HasDatabaseName("UX_SlaAlerts_ExecutionRecipient");
        builder.HasIndex(a => new { a.RecipientUserId, a.CreatedAtUtc, a.Id }).IsDescending(false, true, true); // inbox page / unread count
    }
}
