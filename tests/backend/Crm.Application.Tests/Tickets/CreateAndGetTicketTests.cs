using Crm.Application.Common;
using Crm.Application.Tickets.Commands;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Application.Tickets.Queries;
using Crm.Domain.Tickets;
namespace Crm.Application.Tests.Tickets;

public sealed class CreateAndGetTicketTests
{
    private sealed class ActorProvider : ICurrentActor { public Actor Get() => new("https://trusted.test", "trusted-staff", "Trusted Staff"); }
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(2026, 10, 8, 0, 0, 0, TimeSpan.Zero); }
    private sealed class Store : ITicketMutationStore
    {
        public Task<OperationResult<TicketDetailDto>> MutateAsync(string reference, byte[] version, Func<TicketMutationContext, CancellationToken, Task<Failure?>> mutation, CancellationToken token) => throw new NotSupportedException();
        public Ticket? Saved;
        public bool Available = true;
        public Guid Selected;
        public CancellationToken Received;
        public Task<OperationResult<TicketDetailDto>> CreateAsync(Guid categoryId, Func<TicketCategoryDto?, OperationResult<Ticket>> create, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); Received = cancellationToken; Selected = categoryId;
            var decision = create(new(categoryId, "Category snapshot", Available));
            if (decision.Error is not null) return Task.FromResult(new OperationResult<TicketDetailDto>(null, decision.Error));
            Saved = decision.Value!;
            return Task.FromResult(OperationResult<TicketDetailDto>.Success(new(Saved.Reference, Saved.Subject, Saved.Description, new(categoryId, "Category snapshot", true), Saved.Priority.ToString(), Saved.Status.ToString(), null, Saved.CreatedAtUtc, Saved.UpdatedAtUtc, "AAAAAAAAAAE=")));
        }
    }
    [Fact]
    public async Task Creation_defaults_and_history_use_trusted_actor_and_clock()
    {
        var store = new Store();
        var result = await new CreateTicketHandler(store, new ActorProvider(), new Clock()).Handle(new(" S ", " D ", null, null), CancellationToken.None);
        Assert.True(result.Succeeded); Assert.Equal(TicketDefaults.CategoryId, store.Selected);
        Assert.Equal("Normal", result.Value!.Priority); Assert.Equal("Open", result.Value.Status); Assert.Null(result.Value.AssignedAgent);
        var entry = Assert.Single(store.Saved!.PendingHistory);
        Assert.Equal("trusted-staff", entry.ActorSubject); Assert.Equal("Trusted Staff", entry.ActorDisplayName);
        Assert.Equal(new Clock().GetUtcNow(), entry.OccurredAtUtc);
    }
    [Theory]
    [InlineData(" ", "D", null)] [InlineData("S", " ", null)] [InlineData("S", "D", "normal")] [InlineData("S", "D", "99")]
    public async Task Invalid_input_never_reaches_persistence(string subject, string description, string? priority)
    {
        var store = new Store();
        var result = await new CreateTicketHandler(store, new ActorProvider(), new Clock()).Handle(new(subject, description, null, priority), CancellationToken.None);
        Assert.Equal("validation_failed", result.Error!.Code); Assert.Null(store.Saved);
    }
    [Fact]
    public async Task Unavailable_category_leaves_no_ticket_or_history()
    {
        var store = new Store { Available = false };
        var result = await new CreateTicketHandler(store, new ActorProvider(), new Clock()).Handle(new("S", "D", Guid.NewGuid(), "Urgent"), CancellationToken.None);
        Assert.Equal("category_unavailable", result.Error!.Code); Assert.Null(store.Saved);
    }
    [Fact]
    public async Task Cancellation_propagates_and_does_not_save()
    {
        var store = new Store(); using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CreateTicketHandler(store, new ActorProvider(), new Clock()).Handle(new("S", "D", null, null), source.Token));
        Assert.Null(store.Saved);
    }
    private sealed class Reader : ITicketReadService
    {
        public Task<TicketPageDto<TicketSummaryDto>> ListAsync(TicketListCriteria criteria, CancellationToken cancellationToken) => throw new NotSupportedException();
        public CancellationToken Received;
        public Task<TicketDetailDto?> GetAsync(string reference, CancellationToken cancellationToken) { Received = cancellationToken; return Task.FromResult<TicketDetailDto?>(null); }
        public Task<TicketPageDto<TicketHistoryDto>?> GetHistoryAsync(string reference, int page, int pageSize, CancellationToken cancellationToken) => Task.FromResult<TicketPageDto<TicketHistoryDto>?>(null);
    }
    [Fact]
    public async Task Reads_map_missing_and_preserve_cancellation()
    {
        var reader = new Reader(); using var source = new CancellationTokenSource();
        var result = await new GetTicketHandler(reader).Handle(new("missing"), source.Token);
        Assert.Equal("ticket_not_found", result.Error!.Code); Assert.Equal(source.Token, reader.Received);
        var history = await new GetTicketHistoryHandler(reader).Handle(new("missing", 1, 25), source.Token);
        Assert.Equal("ticket_not_found", history.Error!.Code);
    }
    [Theory]
    [InlineData(0, 25)] [InlineData(1, 0)] [InlineData(1, 101)]
    public async Task History_paging_is_bounded(int page, int pageSize)
    {
        var result = await new GetTicketHistoryHandler(new Reader()).Handle(new("ref", page, pageSize), CancellationToken.None);
        Assert.Equal("validation_failed", result.Error!.Code);
    }
}
