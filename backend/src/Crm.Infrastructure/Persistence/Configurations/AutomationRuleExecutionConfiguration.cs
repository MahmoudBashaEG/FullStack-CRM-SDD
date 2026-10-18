using Crm.Domain.Automation;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class AutomationRuleExecutionConfiguration : IEntityTypeConfiguration<AutomationRuleExecution>
{
    public void Configure(EntityTypeBuilder<AutomationRuleExecution> builder)
    {
        builder.ToTable("AutomationRuleExecutions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd(); // bigint IDENTITY

        builder.Property(e => e.SlaKind).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Trigger).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Outcome).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.Detail).HasMaxLength(300);

        builder.HasOne<AutomationRule>().WithMany().HasForeignKey(e => e.RuleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(e => e.TicketId).OnDelete(DeleteBehavior.Restrict);

        // The dedup key for every automated effect (S8). HasFilter(null): EF must NOT add its default "WHERE [SlaKind] IS NOT NULL" for the
        // nullable column — SQL Server treats NULLs as equal in a unique index, which is what dedups assignment rows (SlaKind NULL).
        // Deliberately NOT in EfUnitOfWork.KnownUniqueIndexes: a violation is a defect, never a user conflict.
        builder.HasIndex(e => new { e.RuleId, e.TicketId, e.SlaKind })
            .IsUnique()
            .HasFilter(null)
            .HasDatabaseName("UX_AutomationRuleExecutions_RuleTicketKind");
        builder.HasIndex(e => new { e.OccurredAtUtc, e.Id }).IsDescending(true, true); // "recent automation"
    }
}
