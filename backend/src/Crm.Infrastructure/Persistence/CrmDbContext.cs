using Crm.Domain.Administration;
using Crm.Domain.AgentTasks;
using Crm.Domain.Automation;
using Crm.Domain.Collaboration;
using Crm.Domain.Customers;
using Crm.Domain.Identity;
using Crm.Domain.KnowledgeBase;
using Crm.Domain.QuickReplies;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence;

public sealed class CrmDbContext(DbContextOptions<CrmDbContext> options)
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>(options)
{
    public const string ConnectionStringName = "CrmDatabase";

    public DbSet<RefreshSession> RefreshSessions => Set<RefreshSession>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketActivity> TicketActivities => Set<TicketActivity>();
    public DbSet<AgentTask> AgentTasks => Set<AgentTask>();
    public DbSet<TaskReminderNotification> TaskReminderNotifications => Set<TaskReminderNotification>();
    public DbSet<QuickReplyTemplate> QuickReplyTemplates => Set<QuickReplyTemplate>();
    public DbSet<TicketCollaborator> TicketCollaborators => Set<TicketCollaborator>();
    public DbSet<TicketInternalMessage> TicketInternalMessages => Set<TicketInternalMessage>();
    public DbSet<CustomerNote> CustomerNotes => Set<CustomerNote>();
    public DbSet<CustomerInteraction> CustomerInteractions => Set<CustomerInteraction>();
    public DbSet<CustomerAttachment> CustomerAttachments => Set<CustomerAttachment>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<BusinessCalendar> BusinessCalendars => Set<BusinessCalendar>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<AutomationRuleExecution> AutomationRuleExecutions => Set<AutomationRuleExecution>();
    public DbSet<SlaAlert> SlaAlerts => Set<SlaAlert>();
    public DbSet<KnowledgeCategory> KnowledgeCategories => Set<KnowledgeCategory>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<TicketKnowledgeArticleLink> TicketKnowledgeArticleLinks => Set<TicketKnowledgeArticleLink>();
    public DbSet<TicketFeedback> TicketFeedback => Set<TicketFeedback>();
    public DbSet<AdminAuditEntry> AdminAuditLogs => Set<AdminAuditEntry>();
    public DbSet<OrganizationSettings> OrganizationSettings => Set<OrganizationSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity's model first, so feature configurations can refine it.
        base.OnModelCreating(modelBuilder);
        // Feature stories add IEntityTypeConfiguration<T> classes under Persistence/Configurations.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CrmDbContext).Assembly);
    }
}
