using Crm.Application.Common;
using Crm.Application.Tickets.Commands;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Domain.Tickets;
namespace Crm.Application.Tests.Tickets;

public sealed class ChangeTicketStatusTests
{
    private sealed class ActorProvider : ICurrentActor { public Actor Get() => new("https://trusted.test", "trusted-staff", "Trusted Staff"); }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-08T01:00:00Z"); }
    private sealed class Store : ITicketMutationStore
    {
        public Ticket Ticket { get; } = Ticket.Create("Subject", "Description", new(TicketDefaults.CategoryId, "Inactive category"), TicketPriority.Normal, new("issuer", "creator", "Creator"), DateTimeOffset.Parse("2026-10-08T00:00:00Z"));
        public int Calls; public CancellationToken Received; public bool FailSave = false;
        public Task<OperationResult<TicketDetailDto>> CreateAsync(Guid id, Func<TicketCategoryDto?, OperationResult<Ticket>> create, CancellationToken token) => throw new NotSupportedException();
        public async Task<OperationResult<TicketDetailDto>> MutateAsync(string reference, byte[] version, Func<TicketMutationContext, CancellationToken, Task<Failure?>> mutation, CancellationToken token)
        {
            Calls++; Received = token; token.ThrowIfCancellationRequested();
            if (!version.SequenceEqual(Convert.FromBase64String("AAAAAAAAAAE="))) return OperationResult<TicketDetailDto>.Fail("ticket_changed", 409);
            if (FailSave) throw new DependencyUnavailableException();
            var category = new TicketCategoryDto(Ticket.CategoryId, "Inactive category", false); var agent = Ticket.AssignedAgentId is Guid id ? new TicketAgentDto(id, "Inactive agent", false) : null;
            var failure = await mutation(new(Ticket, category, agent, (_, _) => throw new InvalidOperationException("Status must not select categories."), (_, _) => throw new InvalidOperationException("Status must not select agents.")), token);
            return failure is not null ? new(null, failure) : OperationResult<TicketDetailDto>.Success(new(Ticket.Reference, Ticket.Subject, Ticket.Description, category, Ticket.Priority.ToString(), Ticket.Status.ToString(), agent, Ticket.CreatedAtUtc, Ticket.UpdatedAtUtc, "AAAAAAAAAAI="));
        }
    }
    public static IEnumerable<object[]> Allowed() => [ ["Open", "InProgress"], ["Open", "Resolved"], ["InProgress", "Resolved"], ["Resolved", "Closed"], ["Resolved", "Open"], ["Closed", "Open"] ];
    [Theory, MemberData(nameof(Allowed))]
    public async Task Allowed_transitions_retain_inactive_references_and_use_trusted_actor_time(string from, string to)
    {
        var store = new Store(); var agent = Guid.NewGuid(); store.Ticket.Assign(null, new(agent, "Inactive agent"), new("issuer", "staff", "Staff"), DateTimeOffset.UtcNow); typeof(Ticket).GetProperty(nameof(Ticket.Status))!.SetValue(store.Ticket, Enum.Parse<TicketStatus>(from));
        var result = await new ChangeTicketStatusHandler(store, new ActorProvider(), new Clock()).Handle(new("ref", "AAAAAAAAAAE=", to), default);
        Assert.True(result.Succeeded); Assert.Equal(to, result.Value!.Status); Assert.Equal(agent, store.Ticket.AssignedAgentId); Assert.Equal(TicketDefaults.CategoryId, store.Ticket.CategoryId);
        var entry = store.Ticket.PendingHistory[^1]; Assert.Equal("StatusChanged", entry.Kind); Assert.Equal("trusted-staff", entry.ActorSubject); Assert.Equal(new Clock().GetUtcNow(), entry.OccurredAtUtc);
    }
    [Fact]
    public async Task No_assignee_is_required_and_invalid_transition_is_atomic()
    {
        var store = new Store(); var handler = new ChangeTicketStatusHandler(store, new ActorProvider(), new Clock());
        var invalid = await handler.Handle(new("ref", "AAAAAAAAAAE=", "Closed"), default); Assert.Equal("invalid_transition", invalid.Error!.Code); Assert.Single(store.Ticket.PendingHistory); Assert.Equal(TicketStatus.Open, store.Ticket.Status);
        var valid = await handler.Handle(new("ref", "AAAAAAAAAAE=", "Resolved"), default); Assert.True(valid.Succeeded); Assert.Null(store.Ticket.AssignedAgentId);
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")] [InlineData("resolved")] [InlineData("In Progress")] [InlineData("99")] [InlineData("Escalated")]
    public async Task Invalid_status_is_rejected_before_entering_store(string? status)
    {
        var store = new Store(); var result = await new ChangeTicketStatusHandler(store, new ActorProvider(), new Clock()).Handle(new("ref", "AAAAAAAAAAE=", status), default);
        Assert.Equal("validation_failed", result.Error!.Code); Assert.Contains("status", result.Error.Errors!.Keys); Assert.Equal(0, store.Calls);
    }
    [Theory]
    [InlineData(null)] [InlineData("AA==")] [InlineData("AAAAAAAAAAE= ")]
    public async Task Invalid_version_is_rejected_before_status_validation_and_store(string? version)
    {
        var store = new Store(); var result = await new ChangeTicketStatusHandler(store, new ActorProvider(), new Clock()).Handle(new("ref", version, null), default);
        Assert.Equal("invalid_version", result.Error!.Code); Assert.Contains("expectedVersion", result.Error.Errors!.Keys); Assert.Equal(0, store.Calls);
    }
    [Fact]
    public async Task Stale_noop_and_invalid_transition_both_fail_version_first()
    {
        var store = new Store(); var handler = new ChangeTicketStatusHandler(store, new ActorProvider(), new Clock());
        foreach (var status in new[] { "Open", "Closed" }) Assert.Equal("ticket_changed", (await handler.Handle(new("ref", "AAAAAAAAAAI=", status), default)).Error!.Code);
        Assert.Single(store.Ticket.PendingHistory); Assert.Equal(TicketStatus.Open, store.Ticket.Status);
    }
    [Fact]
    public async Task Cancellation_propagates_and_dependency_failure_is_not_replayed()
    {
        var store = new Store(); var handler = new ChangeTicketStatusHandler(store, new ActorProvider(), new Clock()); using var cancellation = new CancellationTokenSource();
        await handler.Handle(new("ref", "AAAAAAAAAAE=", "Open"), cancellation.Token); Assert.Equal(cancellation.Token, store.Received);
        cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new("ref", "AAAAAAAAAAE=", "Resolved"), cancellation.Token));
        store.FailSave = true; await Assert.ThrowsAsync<DependencyUnavailableException>(() => handler.Handle(new("ref", "AAAAAAAAAAE=", "Resolved"), default)); Assert.Equal(2, store.Calls);
    }
}
