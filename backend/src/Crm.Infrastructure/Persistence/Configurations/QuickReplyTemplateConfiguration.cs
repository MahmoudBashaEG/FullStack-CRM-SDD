using Crm.Domain.QuickReplies;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class QuickReplyTemplateConfiguration : IEntityTypeConfiguration<QuickReplyTemplate>
{
    public void Configure(EntityTypeBuilder<QuickReplyTemplate> builder)
    {
        builder.ToTable("QuickReplyTemplates", t => t.HasCheckConstraint(
            "CK_QuickReplyTemplates_Owner",
            "([Scope] = N'Personal' AND [OwnerUserId] IS NOT NULL) OR ([Scope] = N'Shared' AND [OwnerUserId] IS NULL)"));
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Title).IsRequired().HasMaxLength(120);
        builder.Property(t => t.Body).IsRequired().HasMaxLength(2000);
        builder.Property(t => t.Category).HasConversion<string>().HasMaxLength(20);
        builder.Property(t => t.Scope).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.OwnerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(t => new { t.Scope, t.IsArchived, t.Title, t.Id });
        builder.HasIndex(t => new { t.OwnerUserId, t.IsArchived, t.Title, t.Id });
    }
}
