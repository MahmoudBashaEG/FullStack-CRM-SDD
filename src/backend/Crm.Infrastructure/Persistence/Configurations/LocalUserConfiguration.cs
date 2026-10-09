using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class LocalUserConfiguration : IEntityTypeConfiguration<LocalUser>
{
    public void Configure(EntityTypeBuilder<LocalUser> user)
    {
        user.ToTable("CrmUsers", "dbo", table => { table.HasCheckConstraint("CK_CrmUsers_AccountRole", "[AccountRole] IN (0,1,2)"); table.HasCheckConstraint("CK_CrmUsers_AuthVersion", "[AuthVersion]>0"); });
        user.Property(x => x.DisplayName).IsRequired().HasMaxLength(400);
        user.Property(x => x.Email).IsRequired().HasMaxLength(256);
        user.Property(x => x.UserName).IsRequired().HasMaxLength(256);
        user.Property(x => x.NormalizedEmail).IsRequired().HasMaxLength(256).UseCollation("Latin1_General_100_BIN2");
        user.Property(x => x.NormalizedUserName).IsRequired().HasMaxLength(256).UseCollation("Latin1_General_100_BIN2");
        user.Property(x => x.PasswordHash).IsRequired(); user.Property(x => x.SecurityStamp).IsRequired(); user.Property(x => x.ConcurrencyStamp).IsRequired();
        user.Property(x => x.AccountRole).HasConversion<int>().IsRequired(); user.Property(x => x.IsActive).IsRequired();
        user.Property(x => x.RowVersion).IsRowVersion();
        user.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("IX_CrmUsers_NormalizedEmail").HasFilter(null);
        user.HasIndex(x => x.NormalizedUserName).IsUnique().HasDatabaseName("IX_CrmUsers_NormalizedUserName").HasFilter(null);
        user.HasIndex(x => new { x.AccountRole, x.DisplayName, x.Id });
    }
}
