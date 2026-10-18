using Crm.Domain.KnowledgeBase;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class KnowledgeArticleConfiguration : IEntityTypeConfiguration<KnowledgeArticle>
{
    public void Configure(EntityTypeBuilder<KnowledgeArticle> builder)
    {
        builder.ToTable("KnowledgeArticles");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Title).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Summary).HasMaxLength(500);
        builder.Property(a => a.Body).IsRequired().HasMaxLength(20_000); // > 4000 ⇒ nvarchar(max)
        builder.Property(a => a.ReturnReason).HasMaxLength(500);
        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.Visibility).HasConversion<string>().HasMaxLength(30);
        builder.Property<byte[]>("Version").IsRowVersion(); // overlapping saves ⇒ 409 (EfUnitOfWork), as Tickets.Version

        builder.HasOne<KnowledgeCategory>().WithMany().HasForeignKey(a => a.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(a => a.PublishedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.Status, a.UpdatedAtUtc, a.Id }).IsDescending(false, true, true);                  // published browse / review queue
        builder.HasIndex(a => new { a.AuthorUserId, a.Status, a.UpdatedAtUtc, a.Id }).IsDescending(false, false, true, true); // my articles
        builder.HasIndex(a => a.CategoryId);
    }
}
