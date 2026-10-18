using System.Text.Encodings.Web;
using System.Text.Json;
using Crm.Domain.Administration;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class AdminAuditEntryConfiguration : IEntityTypeConfiguration<AdminAuditEntry>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // rendered by Angular interpolation only; keeps non-ASCII compact (C7)
    };

    public void Configure(EntityTypeBuilder<AdminAuditEntry> builder)
    {
        builder.ToTable("AdminAuditLogs");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedOnAdd(); // bigint IDENTITY: append order breaks timestamp ties
        builder.Property(e => e.Action).HasConversion<string>().HasMaxLength(40);
        builder.Property(e => e.TargetType).HasConversion<string>().HasMaxLength(30);
        builder.Property(e => e.TargetId).IsRequired().HasMaxLength(36);
        builder.Property(e => e.TargetLabel).IsRequired().HasMaxLength(256);

        // No HasMaxLength ⇒ nvarchar(max). Values are already truncated to AdminAuditChange.ValueMaxLength, but JSON escaping can
        // multiply the size (a control character becomes \u00XX = 6 chars), so a fixed 4000 could overflow on unusual input.
        builder.Property(e => e.Changes)
            .HasConversion(
                v => JsonSerializer.Serialize(v, Json),
                v => JsonSerializer.Deserialize<List<AdminAuditChange>>(v, Json) ?? new List<AdminAuditChange>(),
                new ValueComparer<IReadOnlyList<AdminAuditChange>>(
                    (a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, c) => HashCode.Combine(h, c.GetHashCode())), v => v.ToList()))
            .IsRequired();

        // References only; nothing cascades (accounts are never deleted).
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(e => e.ActorUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.OccurredAtUtc, e.Id }).IsDescending(true, true); // newest-first audit page
    }
}
