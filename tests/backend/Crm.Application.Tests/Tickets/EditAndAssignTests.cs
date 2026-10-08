using Crm.Application.Common;
using Crm.Application.Tickets.Commands;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Domain.Tickets;
namespace Crm.Application.Tests.Tickets;

public sealed class EditAndAssignTests
{
    private sealed class ActorProvider : ICurrentActor { public Actor Get() => new("issuer", "staff", "Staff"); }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => DateTimeOffset.Parse("2026-10-08T01:00:00Z"); }
    private sealed class Store : ITicketMutationStore
    {
        public readonly Ticket Ticket = Ticket.Create("S", "D", new(TicketDefaults.CategoryId, "Old"), TicketPriority.Normal, new("issuer", "staff", "Staff"), DateTimeOffset.Parse("2026-10-08T00:00:00Z"));
        public Guid AgentId = Guid.NewGuid(); public bool Available = false; public int CategoryChecks; public int AgentChecks; public int Calls;
        public Task<OperationResult<TicketDetailDto>> CreateAsync(Guid id, Func<TicketCategoryDto?, OperationResult<Ticket>> create, CancellationToken token) => throw new NotSupportedException();
        public async Task<OperationResult<TicketDetailDto>> MutateAsync(string reference, byte[] version, Func<TicketMutationContext, CancellationToken, Task<Failure?>> mutate, CancellationToken token)
        {
            Calls++; token.ThrowIfCancellationRequested();
            if (!version.SequenceEqual(Convert.FromBase64String("AAAAAAAAAAE="))) return OperationResult<TicketDetailDto>.Fail("ticket_changed", 409);
            var category = new TicketCategoryDto(Ticket.CategoryId, "Old", false);
            var agent = Ticket.AssignedAgentId is null ? null : new TicketAgentDto(Ticket.AssignedAgentId.Value, "Old agent", false);
            var failure = await mutate(new(Ticket, category, agent,
                (id, _) => { CategoryChecks++; return Task.FromResult<TicketCategoryDto?>(new(id, "New", Available)); },
                (id, _) => { AgentChecks++; return Task.FromResult<TicketAgentDto?>(new(id, "New agent", Available)); }), token);
            return failure is not null ? new(null, failure) : OperationResult<TicketDetailDto>.Success(new(Ticket.Reference, Ticket.Subject, Ticket.Description, category, Ticket.Priority.ToString(), Ticket.Status.ToString(), agent, Ticket.CreatedAtUtc, Ticket.UpdatedAtUtc, "AAAAAAAAAAE="));
        }
    }
    [Fact]
    public async Task Omitted_fields_and_explicit_inactive_retention_are_preserved_without_lookup()
    {
        var store = new Store(); var result = await new UpdateTicketDetailsHandler(store, new ActorProvider(), new Clock()).Handle(new("ref", "AAAAAAAAAAE=", new(Subject: " Changed ", CategoryId: store.Ticket.CategoryId)), default);
        Assert.True(result.Succeeded); Assert.Equal("Changed", store.Ticket.Subject); Assert.Equal("D", store.Ticket.Description); Assert.Equal(TicketPriority.Normal, store.Ticket.Priority); Assert.Equal(0, store.CategoryChecks);
    }
    [Fact]
    public async Task New_inactive_category_rejects_whole_patch()
    {
        var store = new Store(); var result = await new UpdateTicketDetailsHandler(store, new ActorProvider(), new Clock()).Handle(new("ref", "AAAAAAAAAAE=", new(Subject: "Changed", CategoryId: Guid.NewGuid())), default);
        Assert.Equal("category_unavailable", result.Error!.Code); Assert.Equal("S", store.Ticket.Subject); Assert.Single(store.Ticket.PendingHistory); Assert.Equal(1, store.CategoryChecks);
    }
    [Fact]
    public async Task Retain_inactive_and_clear_without_replacement_check_then_reject_reselection()
    {
        var store = new Store(); store.Ticket.Assign(null, new(store.AgentId, "Old agent"), new("issuer", "staff", "Staff"), DateTimeOffset.UtcNow);
        var handler = new AssignTicketHandler(store, new ActorProvider(), new Clock());
        Assert.True((await handler.Handle(new("ref", "AAAAAAAAAAE=", store.AgentId), default)).Succeeded);
        Assert.True((await handler.Handle(new("ref", "AAAAAAAAAAE=", null), default)).Succeeded); Assert.Equal(0, store.AgentChecks); Assert.Null(store.Ticket.AssignedAgentId);
        Assert.Equal("agent_ineligible", (await handler.Handle(new("ref", "AAAAAAAAAAE=", store.AgentId), default)).Error!.Code); Assert.Equal(1, store.AgentChecks);
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("AAAAAAAAAAE= ")] [InlineData("AA==")]
    public async Task Bad_version_never_enters_store(string? version)
    {
        var store = new Store(); var result = await new AssignTicketHandler(store, new ActorProvider(), new Clock()).Handle(new("ref", version, null), default);
        Assert.Equal("invalid_version", result.Error!.Code); Assert.Equal(0, store.Calls); Assert.Contains("expectedVersion", result.Error.Errors!.Keys);
    }
    [Fact]
    public async Task Stale_noop_is_rejected_before_domain_or_reference_checks()
    {
        var store = new Store(); var result = await new UpdateTicketDetailsHandler(store, new ActorProvider(), new Clock()).Handle(new("ref", "AAAAAAAAAAI=", new(Subject: "S")), default);
        Assert.Equal("ticket_changed", result.Error!.Code); Assert.Equal(1, store.Ticket.Revision); Assert.Equal(0, store.CategoryChecks);
    }
    [Theory]
    [InlineData(" ", null)] [InlineData(null, "urgent")] [InlineData(null, "99")]
    public async Task Invalid_content_is_rejected_before_store(string? subject, string? priority)
    {
        var store = new Store(); var result = await new UpdateTicketDetailsHandler(store, new ActorProvider(), new Clock()).Handle(new("ref", "AAAAAAAAAAE=", new(Subject: subject, Priority: priority)), default);
        Assert.Equal("validation_failed", result.Error!.Code); Assert.Equal(0, store.Calls);
    }
    [Fact]
    public async Task Empty_patch_is_rejected_and_cancellation_propagates()
    {
        var store = new Store(); var handler = new UpdateTicketDetailsHandler(store, new ActorProvider(), new Clock());
        Assert.Equal("validation_failed", (await handler.Handle(new("ref", "AAAAAAAAAAE=", new()), default)).Error!.Code);
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new("ref", "AAAAAAAAAAE=", new(Subject: "S")), source.Token));
    }
}
