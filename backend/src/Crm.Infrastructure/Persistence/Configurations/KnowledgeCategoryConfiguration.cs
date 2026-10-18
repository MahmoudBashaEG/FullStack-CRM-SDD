using Crm.Domain.KnowledgeBase;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class KnowledgeCategoryConfiguration : IEntityTypeConfiguration<KnowledgeCategory>
{
    public const string NameIndex = "UX_KnowledgeCategories_Name";

    public void Configure(EntityTypeBuilder<KnowledgeCategory> builder)
    {
        builder.ToTable("KnowledgeCategories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).IsRequired().HasMaxLength(100);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Referenced by EfUnitOfWork.KnownUniqueIndexes. Case-insensitive under the default database collation.
        builder.HasIndex(c => c.Name).IsUnique().HasDatabaseName(NameIndex);
    }
}
