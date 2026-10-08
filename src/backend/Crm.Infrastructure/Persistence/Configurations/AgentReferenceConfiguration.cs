using Crm.Infrastructure.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class AgentReferenceConfiguration : IEntityTypeConfiguration<AgentReference>
{
    public void Configure(EntityTypeBuilder<AgentReference> builder)
    {
        builder.ToTable("AgentReferences", "dbo");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Issuer).IsRequired().HasColumnType("nvarchar(max)").UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.Subject).IsRequired().HasColumnType("nvarchar(max)").UseCollation("Latin1_General_100_BIN2");
        builder.Property(x => x.DisplayName).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property<byte[]>("IdentityKey").HasComputedColumnSql("CONVERT(binary(32), HASHBYTES('SHA2_256', CONVERT(nvarchar(max), DATALENGTH([Issuer])) + N':' + [Issuer] + CONVERT(nvarchar(max), DATALENGTH([Subject])) + N':' + [Subject]))", stored: true);
        builder.HasIndex("IdentityKey").IsUnique().HasFilter(null);
    }
}
