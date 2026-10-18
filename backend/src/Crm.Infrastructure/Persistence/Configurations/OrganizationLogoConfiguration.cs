using Crm.Domain.Administration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class OrganizationLogoConfiguration : IEntityTypeConfiguration<OrganizationLogo>
{
    public void Configure(EntityTypeBuilder<OrganizationLogo> builder)
    {
        builder.ToTable("OrganizationLogos", t => t.HasCheckConstraint("CK_OrganizationLogos_Singleton", "[Id] = 1"));
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever(); // singleton: Id 1
        builder.Property(l => l.Content).IsRequired();      // varbinary(max)
        builder.Property(l => l.ContentType).HasMaxLength(20).IsRequired();
    }
}
