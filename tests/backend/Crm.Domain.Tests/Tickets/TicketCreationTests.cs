using Crm.Domain.Tickets;
namespace Crm.Domain.Tests.Tickets;

public sealed class TicketCreationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    [Fact]
    public void Creation_preserves_unicode_and_interior_whitespace_and_sets_defaults()
    {
        var ticket = Ticket.Create("  مشكلة  اتصال  ", " وصف\n  مفصل ", new(Guid.NewGuid(), "Uncategorised"), TicketPriority.Normal, new("https://example.test", "staff-a", "Staff A"), Now);
        Assert.Equal("مشكلة  اتصال", ticket.Subject);
        Assert.Equal("وصف\n  مفصل", ticket.Description);
        Assert.Equal(TicketStatus.Open, ticket.Status);
        Assert.Equal(TicketPriority.Normal, ticket.Priority);
        Assert.Null(ticket.AssignedAgentId);
        Assert.Equal(Now, ticket.CreatedAtUtc);
        Assert.Equal(Now, ticket.UpdatedAtUtc);
        Assert.Equal(1, ticket.Revision);
        Assert.Matches("^TKT-[A-F0-9]{32}$", ticket.Reference);
        Assert.Equal("TKT-" + ticket.Id.ToString("N").ToUpperInvariant(), ticket.Reference);
    }
    [Theory]
    [InlineData("", "Description")]
    [InlineData("  \t", "Description")]
    [InlineData("Subject", "\n ")]
    public void Blank_content_is_rejected(string subject, string description) => Assert.Throws<ArgumentException>(() => Ticket.Create(subject, description, new(Guid.NewGuid(), "Category"), TicketPriority.Normal, new("https://example.test", "staff", "Staff"), Now));
    [Fact]
    public void Priority_must_be_a_defined_choice() => Assert.Throws<ArgumentException>(() => Ticket.Create("S", "D", new(Guid.NewGuid(), "Category"), (TicketPriority)99, new("https://example.test", "staff", "Staff"), Now));
    [Fact]
    public void Every_creation_receives_its_own_stable_identity()
    {
        var first = Ticket.Create("S", "D", new(Guid.NewGuid(), "Category"), TicketPriority.High, new("https://example.test", "staff", "Staff"), Now);
        var second = Ticket.Create("S", "D", new(Guid.NewGuid(), "Category"), TicketPriority.High, new("https://example.test", "staff", "Staff"), Now);
        Assert.NotEqual(first.Reference, second.Reference);
        Assert.True(typeof(Ticket).GetProperty(nameof(Ticket.Reference))!.SetMethod!.IsPrivate);
        Assert.True(typeof(Ticket).GetProperty(nameof(Ticket.CreatedAtUtc))!.SetMethod!.IsPrivate);
        Assert.Equal(TicketPriority.High, first.Priority);
    }
}
