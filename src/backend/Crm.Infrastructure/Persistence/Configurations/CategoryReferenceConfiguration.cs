using Crm.Application.Tickets.Contracts;
using Crm.Infrastructure.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class CategoryReferenceConfiguration : IEntityTypeConfiguration<CategoryReference>
{
    public void Configure(EntityTypeBuilder<CategoryReference> builder)
    {
        builder.ToTable("CategoryReferences", "dbo", table => table.HasCheckConstraint("CK_CategoryReferences_Fallback", "[Id] <> '00000000-0000-0000-0000-000000000001' OR [IsAvailable] = 1"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.DisplayName).IsRequired().HasColumnType("nvarchar(max)");
        builder.HasData(new CategoryReference { Id = TicketDefaults.CategoryId, DisplayName = "Uncategorised", IsAvailable = true });
    }
}
