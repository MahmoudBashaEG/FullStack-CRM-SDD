using Crm.Domain.Sla;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class BusinessCalendarConfiguration : IEntityTypeConfiguration<BusinessCalendar>
{
    public void Configure(EntityTypeBuilder<BusinessCalendar> builder)
    {
        builder.ToTable("BusinessCalendars");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever(); // singleton: Id 1

        builder.Property(c => c.TimeZoneId).IsRequired().HasMaxLength(64);
        builder.Ignore(c => c.WorkingDays); // computed from WorkingDaysMask

        builder.OwnsMany(c => c.Holidays, holidays =>
        {
            holidays.ToTable("BusinessHolidays");
            holidays.WithOwner().HasForeignKey("BusinessCalendarId");
            holidays.Property(h => h.Name).IsRequired().HasMaxLength(100);
            holidays.HasKey("BusinessCalendarId", nameof(BusinessHoliday.Date));
        });
        builder.Navigation(c => c.Holidays).HasField("_holidays").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(c => c.UpdatedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
