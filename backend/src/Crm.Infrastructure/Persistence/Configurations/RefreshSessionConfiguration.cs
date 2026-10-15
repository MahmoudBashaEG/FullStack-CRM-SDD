using Crm.Domain.Identity;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> builder)
    {
        builder.ToTable("RefreshSessions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.TokenHash).HasMaxLength(64).IsFixedLength().IsUnicode(false).IsRequired();
        builder.HasIndex(s => s.TokenHash).IsUnique();
        builder.HasIndex(s => s.UserId);
        builder.HasIndex(s => s.FamilyId);
        builder.Property(s => s.RevocationReason).HasConversion<string>().HasMaxLength(32);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
