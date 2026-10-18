using Crm.Domain.Sla;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class SlaPolicyConfiguration : IEntityTypeConfiguration<SlaPolicy>
{
    public void Configure(EntityTypeBuilder<SlaPolicy> builder)
    {
        builder.ToTable("SlaPolicies");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever(); // singleton: Id 1

        builder.OwnsMany(p => p.Targets, targets =>
        {
            targets.ToTable("SlaPolicyTargets");
            targets.WithOwner().HasForeignKey("SlaPolicyId");
            targets.Property(t => t.Priority).HasConversion<string>().HasMaxLength(20);
            targets.HasKey("SlaPolicyId", nameof(SlaTarget.Priority));
        });
        builder.Navigation(p => p.Targets).HasField("_targets").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(p => p.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
