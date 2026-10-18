using Crm.Domain.Administration;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationSettingsConfiguration : IEntityTypeConfiguration<OrganizationSettings>
{
    public void Configure(EntityTypeBuilder<OrganizationSettings> builder)
    {
        builder.ToTable("OrganizationSettings", t => t.HasCheckConstraint("CK_OrganizationSettings_Singleton", "[Id] = 1"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever(); // singleton: Id 1 (row inserted by the migration)
        builder.Property(s => s.DisplayName).HasMaxLength(150);
        builder.Property(s => s.SupportEmail).HasMaxLength(256);
        builder.Property(s => s.SupportPhone).HasMaxLength(40);
        builder.Property(s => s.SupportInformation).HasMaxLength(1000);
        builder.Ignore(s => s.IsConfigured);
        builder.Property<byte[]>("Version").IsRowVersion(); // ETag; overlapping saves ⇒ 409 (EfUnitOfWork)
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
