using Crm.Domain.Tickets;
namespace Crm.Domain.Tests.Tickets;

public sealed class TicketLifecycleTests
{
    private static readonly DateTimeOffset Created = DateTimeOffset.Parse("2026-10-08T00:00:00Z");
    private static readonly ActorSnapshot Actor = new("https://trusted.test", "staff", "Staff");
    private static readonly CategorySnapshot Category = new(Guid.NewGuid(), "Category");
    public static IEnumerable<object[]> StatePairs()
    {
        var allowed = new HashSet<(TicketStatus, TicketStatus)> {
            (TicketStatus.Open, TicketStatus.InProgress), (TicketStatus.Open, TicketStatus.Resolved),
            (TicketStatus.InProgress, TicketStatus.Resolved), (TicketStatus.Resolved, TicketStatus.Closed),
            (TicketStatus.Resolved, TicketStatus.Open), (TicketStatus.Closed, TicketStatus.Open) };
        foreach (var from in Enum.GetValues<TicketStatus>()) foreach (var to in Enum.GetValues<TicketStatus>()) yield return [from, to, allowed.Contains((from, to))];
    }
    private static Ticket Prepared(TicketStatus status)
    {
        var ticket = Ticket.Create("Subject", "Description", Category, TicketPriority.High, Actor, Created);
        typeof(Ticket).GetProperty(nameof(Ticket.Status))!.SetValue(ticket, status); return ticket;
    }
    [Theory, MemberData(nameof(StatePairs))]
    public void Every_state_pair_has_the_exact_outcome_and_history(TicketStatus from, TicketStatus to, bool allowed)
    {
        var ticket = Prepared(from); var originalHistory = ticket.PendingHistory.ToArray(); var now = Created.AddMinutes(1);
        if (from != to && !allowed) Assert.Throws<InvalidTicketTransitionException>(() => ticket.ChangeStatus(to, Actor, now));
        else ticket.ChangeStatus(to, Actor, now);
        var changed = from != to && allowed;
        Assert.Equal(changed ? to : from, ticket.Status); Assert.Equal(changed ? 2 : 1, ticket.Revision); Assert.Equal(changed ? now : Created, ticket.UpdatedAtUtc);
        Assert.Equal(changed ? 2 : 1, ticket.PendingHistory.Count); Assert.Same(originalHistory[0], ticket.PendingHistory[0]);
        Assert.Null(ticket.AssignedAgentId); Assert.Equal("Subject", ticket.Subject); Assert.Equal("Description", ticket.Description); Assert.Equal(Category.Id, ticket.CategoryId); Assert.Equal(TicketPriority.High, ticket.Priority);
        if (changed)
        {
            var entry = ticket.PendingHistory[1]; Assert.Equal("StatusChanged", entry.Kind); Assert.Equal("status", entry.Field); Assert.Equal(2, entry.Revision); Assert.Equal(1, entry.Sequence);
            Assert.Equal(new TicketTextValue(from.ToString()), entry.PreviousValue); Assert.Equal(new TicketTextValue(to.ToString()), entry.NewValue);
            Assert.Equal("staff", entry.ActorSubject); Assert.Equal("https://trusted.test", entry.ActorIssuer); Assert.Equal("Staff", entry.ActorDisplayName); Assert.Equal(now, entry.OccurredAtUtc);
        }
    }
    [Theory]
    [InlineData(TicketStatus.Resolved)] [InlineData(TicketStatus.Closed)]
    public void Reopen_preserves_identity_content_assignment_and_prior_history(TicketStatus from)
    {
        var ticket = Prepared(TicketStatus.Open); var agent = new AgentSnapshot(Guid.NewGuid(), "Agent"); ticket.Assign(null, agent, Actor, Created.AddSeconds(1));
        typeof(Ticket).GetProperty(nameof(Ticket.Status))!.SetValue(ticket, from);
        var identity = (ticket.Id, ticket.Reference, ticket.Subject, ticket.Description, ticket.CategoryId, ticket.Priority, ticket.AssignedAgentId, ticket.CreatedAtUtc); var history = ticket.PendingHistory.ToArray();
        ticket.ChangeStatus(TicketStatus.Open, Actor, Created.AddMinutes(2));
        Assert.Equal(identity, (ticket.Id, ticket.Reference, ticket.Subject, ticket.Description, ticket.CategoryId, ticket.Priority, ticket.AssignedAgentId, ticket.CreatedAtUtc)); Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(history, ticket.PendingHistory.Take(history.Length)); Assert.Equal(3, ticket.Revision); Assert.Equal(from.ToString(), ((TicketTextValue)ticket.PendingHistory[^1].PreviousValue!).Value);
        ticket.UpdateDetails("After reopen", ticket.Description, Category, Category, ticket.Priority, Actor, Created.AddMinutes(3)); Assert.Equal("After reopen", ticket.Subject);
    }
    [Fact]
    public void Closing_rejects_even_identical_edits_and_assignment_until_reopened()
    {
        var ticket = Prepared(TicketStatus.Resolved); ticket.ChangeStatus(TicketStatus.Closed, Actor, Created.AddMinutes(1)); var history = ticket.PendingHistory.ToArray();
        Assert.Throws<TicketClosedException>(() => ticket.UpdateDetails(ticket.Subject, ticket.Description, Category, Category, ticket.Priority, Actor, Created.AddMinutes(2)));
        Assert.Throws<TicketClosedException>(() => ticket.Assign(null, null, Actor, Created.AddMinutes(2))); Assert.Equal(history, ticket.PendingHistory);
        ticket.ChangeStatus(TicketStatus.Open, Actor, Created.AddMinutes(3)); ticket.Assign(null, new(Guid.NewGuid(), "Agent"), Actor, Created.AddMinutes(4)); Assert.NotNull(ticket.AssignedAgentId);
    }
    [Fact]
    public void Invalid_enum_or_actor_does_not_mutate_or_add_history()
    {
        var ticket = Prepared(TicketStatus.Open); Assert.Throws<ArgumentException>(() => ticket.ChangeStatus((TicketStatus)99, Actor, Created));
        Assert.Throws<ArgumentException>(() => ticket.ChangeStatus(TicketStatus.Resolved, new("", "staff", "Staff"), Created)); Assert.Equal(TicketStatus.Open, ticket.Status); Assert.Single(ticket.PendingHistory);
    }
}
