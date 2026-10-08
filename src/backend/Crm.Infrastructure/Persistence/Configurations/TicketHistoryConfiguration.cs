using System.Text.Json;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Crm.Infrastructure.Persistence.Configurations;

public sealed class TicketHistoryConfiguration : IEntityTypeConfiguration<TicketHistoryEntry>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static string? Serialize(TicketChangeValue? value) => value is null ? null : JsonSerializer.Serialize(value, value.GetType(), JsonOptions);
    private static TicketChangeValue? Deserialize(string? value) => value is null ? null : JsonSerializer.Deserialize<TicketCreationValue>(value, JsonOptions);
    public void Configure(EntityTypeBuilder<TicketHistoryEntry> builder)
    {
        builder.ToTable("TicketHistoryEntries", "dbo", table => table.HasCheckConstraint("CK_TicketHistory_Order", "[Revision] >= 1 AND [Sequence] >= 1"));
        builder.HasKey(x => x.Id); builder.Property(x => x.Id).ValueGeneratedNever();
        builder.HasOne<Ticket>().WithMany().HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.TicketId, x.Revision, x.Sequence }).IsUnique();
        builder.Property(x => x.Kind).HasMaxLength(32).IsRequired(); builder.Property(x => x.Field).HasMaxLength(32);
        builder.Property(x => x.PreviousValue).HasConversion(value => Serialize(value), value => Deserialize(value)).HasColumnType("nvarchar(max)");
        builder.Property(x => x.NewValue).HasConversion(value => Serialize(value), value => Deserialize(value)).HasColumnType("nvarchar(max)");
        builder.Property(x => x.ActorIssuer).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.ActorSubject).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(x => x.ActorDisplayName).IsRequired().HasColumnType("nvarchar(max)");
    }
}
