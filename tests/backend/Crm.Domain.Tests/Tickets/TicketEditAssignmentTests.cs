using Crm.Domain.Tickets;
namespace Crm.Domain.Tests.Tickets;

public sealed class TicketEditAssignmentTests
{
    private static readonly ActorSnapshot Actor = new("issuer", "staff", "Staff");
    private static readonly CategorySnapshot Category = new(Guid.NewGuid(), "Original");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-08T00:00:00Z");
    private static Ticket New() => Ticket.Create("Subject", "Description", Category, TicketPriority.Normal, Actor, Now);
    [Fact]
    public void Details_normalize_and_emit_only_actual_changes_in_field_order()
    {
        var ticket = New(); var next = new CategorySnapshot(Guid.NewGuid(), "New category");
        ticket.UpdateDetails(" New subject ", " New description ", Category, next, TicketPriority.High, Actor, Now.AddMinutes(1));
        Assert.Equal("New subject", ticket.Subject); Assert.Equal("New description", ticket.Description);
        Assert.Equal(next.Id, ticket.CategoryId); Assert.Equal(TicketPriority.High, ticket.Priority);
        Assert.Equal(2, ticket.Revision); Assert.Equal(TicketStatus.Open, ticket.Status);
        var changes = ticket.PendingHistory.Skip(1).ToArray();
        Assert.Equal(new[] { "subject", "description", "category", "priority" }, changes.Select(x => x.Field));
        Assert.Equal(new[] { 1, 2, 3, 4 }, changes.Select(x => x.Sequence));
        Assert.All(changes, x => { Assert.Equal(2, x.Revision); Assert.Equal("DetailsChanged", x.Kind); Assert.Equal(Now.AddMinutes(1), x.OccurredAtUtc); Assert.Equal("staff", x.ActorSubject); });
    }
    [Fact]
    public void Normalized_noops_preserve_revision_time_and_history()
    {
        var ticket = New();
        ticket.UpdateDetails(" Subject ", " Description ", Category, Category, TicketPriority.Normal, Actor, Now.AddMinutes(1));
        ticket.Assign(null, null, Actor, Now.AddMinutes(2));
        Assert.Equal(1, ticket.Revision); Assert.Equal(Now, ticket.UpdatedAtUtc); Assert.Single(ticket.PendingHistory);
    }
    [Fact]
    public void Assign_reassign_clear_have_one_owner_and_null_snapshots_without_status_changes()
    {
        var ticket = New(); var a = new AgentSnapshot(Guid.NewGuid(), "A"); var b = new AgentSnapshot(Guid.NewGuid(), "B");
        ticket.Assign(null, a, Actor, Now.AddMinutes(1)); ticket.Assign(a, a, Actor, Now.AddMinutes(2));
        ticket.Assign(a, b, Actor, Now.AddMinutes(3)); ticket.Assign(b, null, Actor, Now.AddMinutes(4));
        Assert.Null(ticket.AssignedAgentId); Assert.Equal(4, ticket.Revision); Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(4, ticket.PendingHistory.Count); Assert.Null(ticket.PendingHistory[1].PreviousValue);
        Assert.Null(ticket.PendingHistory[3].NewValue); Assert.All(ticket.PendingHistory.Skip(1), x => Assert.Equal("AssignmentChanged", x.Kind));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void Closed_rejects_details_and_assignment_even_noops(bool details)
    {
        var ticket = New(); typeof(Ticket).GetProperty(nameof(Ticket.Status))!.SetValue(ticket, TicketStatus.Closed);
        var error = Assert.Throws<TicketClosedException>(() => { if (details) ticket.UpdateDetails(ticket.Subject, ticket.Description, Category, Category, ticket.Priority, Actor, Now); else ticket.Assign(null, null, Actor, Now); });
        Assert.Equal(1, ticket.Revision); Assert.Single(ticket.PendingHistory);
    }
    [Theory]
    [InlineData("", "D")] [InlineData("S", " ")]
    public void Invalid_details_are_atomic(string subject, string description)
    {
        var ticket = New(); Assert.Throws<ArgumentException>(() => ticket.UpdateDetails(subject, description, Category, Category, TicketPriority.High, Actor, Now));
        Assert.Equal("Subject", ticket.Subject); Assert.Equal(TicketPriority.Normal, ticket.Priority); Assert.Equal(1, ticket.Revision);
    }
}
