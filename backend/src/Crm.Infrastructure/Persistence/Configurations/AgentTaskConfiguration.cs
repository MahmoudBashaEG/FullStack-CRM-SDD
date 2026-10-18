using Crm.Domain.AgentTasks;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class AgentTaskConfiguration : IEntityTypeConfiguration<AgentTask>
{
    public void Configure(EntityTypeBuilder<AgentTask> builder)
    {
        builder.ToTable("AgentTasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).HasMaxLength(2000);
        builder.Property(t => t.Status).HasConversion<string>().HasMaxLength(20); // 'Pending' / 'Completed' (raw SQL in TaskReminderRepository relies on it)
        builder.Ignore(t => t.IsOpenForReminders);

        // References only; nothing cascades (accounts, tickets and tasks are never deleted).
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.AssignedAgentUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(t => t.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Ticket>().WithMany().HasForeignKey(t => t.TicketId).OnDelete(DeleteBehavior.Restrict);

        // Agent and SuperAdmin can edit the same task: two simultaneous writes cannot both commit (EfUnitOfWork ⇒ 409).
        builder.Property(t => t.Version).IsRowVersion(); // F1: exposed as ETag; EF adds WHERE [Version] = @original to every UPDATE

        builder.HasIndex(t => new { t.AssignedAgentUserId, t.IsArchived, t.Status, t.DueAtUtc, t.Id });         // owner lists, counts, preview
        builder.HasIndex(t => new { t.AssignedAgentUserId, t.RemindAtUtc }).HasFilter("[RemindAtUtc] IS NOT NULL"); // reminder eligibility
    }
}
