using Crm.Domain.AgentTasks;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class TaskReminderNotificationConfiguration : IEntityTypeConfiguration<TaskReminderNotification>
{
    public void Configure(EntityTypeBuilder<TaskReminderNotification> builder)
    {
        builder.ToTable("TaskReminderNotifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedOnAdd(); // bigint IDENTITY: insertion order is the tie-breaker

        builder.HasOne<AgentTask>().WithMany().HasForeignKey(n => n.TaskId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(n => n.TransferredFromUserId).OnDelete(DeleteBehavior.Restrict);

        // The dedup guarantee: at most one notification per (task, reminder generation). TaskId leads, so it also serves the transfer.
        builder.HasIndex(n => new { n.TaskId, n.ReminderGeneration }).IsUnique().HasDatabaseName("UX_TaskReminderNotifications_TaskGeneration");
        builder.HasIndex(n => new { n.RecipientUserId, n.IssuedAtUtc, n.Id }).IsDescending(false, true, true); // inbox page / unread count
    }
}
