using Crm.Domain.Customers;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.FirstName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.LastName).IsRequired().HasMaxLength(100);
        builder.Property(c => c.Email).IsRequired().HasMaxLength(256);
        builder.Property(c => c.PhoneNumber).HasMaxLength(32);
        builder.Property(c => c.Company).HasMaxLength(200);
        builder.Ignore(c => c.IsRegistered);

        // Optional 1:1 with an Identity account; many contact-only rows may have no account.
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<Customer>(c => c.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(c => c.UserId).IsUnique().HasFilter("[UserId] IS NOT NULL");

        builder.HasIndex(c => new { c.CreatedAtUtc, c.Id }).IsDescending(true, true); // default list order
        builder.HasIndex(c => c.Email);                                                 // not unique: duplicates allowed
    }
}
