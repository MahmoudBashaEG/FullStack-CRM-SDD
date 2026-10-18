using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Automation;
using Crm.Application.Sla;
using Crm.Domain.Automation;
using Crm.Domain.Identity;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Sla;

public sealed class TicketCreationAutomationTests
{
    private static readonly Guid AdminId = Guid.CreateVersion7();
    private static readonly Guid InactiveAgentX = Guid.CreateVersion7();
    private static readonly Guid ActiveAgentY = Guid.CreateVersion7();
    private static readonly Guid ActiveAgentZ = Guid.CreateVersion7();

    [Fact]
    public async Task ApplyAsync_UnassignedTicketCreatedThursdayAfternoon_PersistsBusinessDeadlinesAcrossWeekendAndHolidayAndAssignsFirstActiveMatchingRule()
    {
        // Africa/Cairo is UTC+2 in January. Sunday–Thursday 09:00–17:00, Sunday 2030-01-06 is a holiday.
        var now = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var calendar = BusinessCalendar.Create(
            "Africa/Cairo",
            [DayOfWeek.Sunday, DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday],
            new TimeOnly(9, 0),
            new TimeOnly(17, 0),
            [new BusinessHoliday(new DateOnly(2030, 1, 6), "Example holiday")],
            AdminId,
            now);
        var policy = SlaPolicy.Create(
            isEnabled: true,
            [
                new SlaTarget(TicketPriority.Low, 960, 2880),
                new SlaTarget(TicketPriority.Medium, 480, 1440),
                new SlaTarget(TicketPriority.High, 240, 960),
                new SlaTarget(TicketPriority.Urgent, 2, 4),
            ],
            AdminId,
            now);

        var ruleA = Rule("Billing to X", sortOrder: 1, TicketCategory.Billing, InactiveAgentX, now);
        var ruleB = Rule("Billing to Y", sortOrder: 2, TicketCategory.Billing, ActiveAgentY, now);
        var ruleC = Rule("Anything to Z", sortOrder: 3, category: null, ActiveAgentZ, now);
        var rules = new FakeRuleRepository([ruleA, ruleB, ruleC]);

        // Thursday 2030-01-03 16:00 Cairo = 14:00 UTC.
        var createdAt = new DateTimeOffset(2030, 1, 3, 14, 0, 0, TimeSpan.Zero);
        var ticket = Ticket.Create(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Invoice wrong", "The invoice total is wrong.",
            TicketCategory.Billing, TicketPriority.High, AdminId, assignedAgentUserId: null, createdAt);

        var automation = new TicketCreationAutomation(
            new FakeConfigurationRepository(policy, calendar), rules, new FakeIdentityService(), new FixedTimeProvider(createdAt));

        await automation.ApplyAsync(ticket, CancellationToken.None);

        // First response: Thursday 60 min + Monday 180 min (Friday/Saturday closed, Sunday holiday) => Monday 12:00 Cairo.
        Assert.Equal(new DateTimeOffset(2030, 1, 7, 10, 0, 0, TimeSpan.Zero), ticket.SlaFirstResponseDueAtUtc);
        // Resolution: Thursday 60 + Monday 480 + Tuesday 420 => Tuesday 16:00 Cairo.
        Assert.Equal(new DateTimeOffset(2030, 1, 8, 14, 0, 0, TimeSpan.Zero), ticket.SlaResolutionDueAtUtc);
        Assert.Equal(240, ticket.SlaFirstResponseTargetMinutes);
        Assert.Equal(960, ticket.SlaResolutionTargetMinutes);
        Assert.Equal("Africa/Cairo", ticket.SlaTimeZoneId);

        // Rule A is skipped (inactive Agent); rule B wins; rule C is never reached.
        Assert.Equal(ActiveAgentY, ticket.AssignedAgentUserId);
        var execution = Assert.Single(rules.Executions);
        Assert.Equal(ruleB.Id, execution.RuleId);
        Assert.Equal(AutomationOutcome.Assigned, execution.Outcome);
        Assert.Null(execution.SlaKind);

        var assignment = ticket.Activities.Last();
        Assert.Equal(TicketActivityType.AssigneeChanged, assignment.Type);
        Assert.Null(assignment.ActorUserId);
        Assert.Equal(ActiveAgentY, assignment.ToAgentUserId);
        Assert.Contains("Billing to Y", assignment.Reason);
    }

    private static AutomationRule Rule(string name, int sortOrder, TicketCategory? category, Guid targetAgent, DateTimeOffset now) =>
        AutomationRule.Create(
            Guid.CreateVersion7(),
            new AutomationRuleDefinition(
                name, IsEnabled: true, AutomationTrigger.TicketCreated, AutomationAction.AssignToAgent, sortOrder,
                category, PriorityCondition: null, AssignedCondition: null, SlaKindCondition: null, targetAgent, WarningLeadMinutes: null),
            AdminId,
            now);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeConfigurationRepository(SlaPolicy policy, BusinessCalendar calendar) : ISlaConfigurationRepository
    {
        public Task<SlaPolicy?> FindPolicyAsync(CancellationToken cancellationToken = default) => Task.FromResult<SlaPolicy?>(policy);
        public Task<BusinessCalendar?> FindCalendarAsync(CancellationToken cancellationToken = default) => Task.FromResult<BusinessCalendar?>(calendar);
        public void Add(SlaPolicy policy) => throw new NotSupportedException();
        public void Add(BusinessCalendar calendar) => throw new NotSupportedException();
        public Task<string?> FindUserNameAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeRuleRepository(IReadOnlyList<AutomationRule> enabledTicketCreatedRules) : IAutomationRuleRepository
    {
        public List<AutomationRuleExecution> Executions { get; } = [];

        public Task<IReadOnlyList<AutomationRule>> ListEnabledAsync(AutomationTrigger trigger, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AutomationRule>>(trigger == AutomationTrigger.TicketCreated ? enabledTicketCreatedRules : []);

        public void AddExecution(AutomationRuleExecution execution) => Executions.Add(execution);
        public void Add(AutomationRule rule) => throw new NotSupportedException();
        public void AddAlert(SlaAlert alert) => throw new NotSupportedException();
        public Task<AutomationRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AutomationRuleDetails>> ListDetailsAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<AutomationRuleDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<AutomationExecutionDetails>> ListRecentExecutionsAsync(int limit, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeIdentityService : IIdentityService
    {
        public Task<UserAccount?> FindByIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult<UserAccount?>(
                new UserAccount(userId, $"{userId}@example.test", "Agent", userId == InactiveAgentX ? "X" : "Active", null, UserRoles.Agent,
                    IsActive: userId != InactiveAgentX, "stamp", DateTimeOffset.UtcNow));

        public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount?> CheckCredentialsAsync(string email, string password, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> CreateUserAsync(NewUserAccount account, string password, string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<UserAccount>> ListByRoleAsync(string role, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<UserAccount> UpdateProfileAsync(Guid userId, UserProfileUpdate update, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> ChangePasswordAsync(Guid userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetPasswordAsync(Guid userId, string newPassword, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SetActiveAsync(Guid userId, bool isActive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
