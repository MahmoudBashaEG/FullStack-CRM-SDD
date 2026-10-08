using Crm.Domain.Tickets;
using Crm.Infrastructure.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets", "dbo", table =>
        {
            table.HasCheckConstraint("CK_Tickets_Priority", "[Priority] IN ('Low','Normal','High','Urgent')");
            table.HasCheckConstraint("CK_Tickets_Status", "[Status] IN ('Open','InProgress','Resolved','Closed')");
            table.HasCheckConstraint("CK_Tickets_Revision", "[Revision] >= 1");
        });
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Reference).IsRequired().HasMaxLength(36).UseCollation("Latin1_General_100_BIN2"); builder.HasIndex(x => x.Reference).IsUnique();
        builder.Property(x => x.Subject).IsRequired().HasColumnType("nvarchar(max)").UseCollation("Latin1_General_100_CI_AS_SC");
        builder.Property(x => x.Description).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.Priority).HasConversion<string>().HasMaxLength(16);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(16);
        builder.Property<byte[]>("RowVersion").IsRequired().IsRowVersion();
        builder.HasOne<CategoryReference>().WithMany().HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<AgentReference>().WithMany().HasForeignKey(x => x.AssignedAgentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UpdatedAtUtc, x.Reference }).IsDescending(true, false);
        builder.Ignore(x => x.PendingHistory);
    }
}
