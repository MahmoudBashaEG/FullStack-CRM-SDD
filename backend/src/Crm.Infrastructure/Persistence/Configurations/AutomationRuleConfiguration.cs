using Crm.Domain.Automation;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class AutomationRuleConfiguration : IEntityTypeConfiguration<AutomationRule>
{
    public void Configure(EntityTypeBuilder<AutomationRule> builder)
    {
        builder.ToTable("AutomationRules");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).IsRequired().HasMaxLength(100);

        // Fixed sets stored by name.
        builder.Property(r => r.Trigger).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Action).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.CategoryCondition).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.PriorityCondition).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.SlaKindCondition).HasConversion<string>().HasMaxLength(30);

        // References only; rules are never deleted and accounts are never deleted.
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.TargetAgentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(r => r.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => new { r.Trigger, r.IsEnabled, r.SortOrder, r.CreatedAtUtc, r.Id });
    }
}
