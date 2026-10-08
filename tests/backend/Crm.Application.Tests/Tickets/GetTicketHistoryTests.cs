using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Application.Tickets.Queries;
namespace Crm.Application.Tests.Tickets;

public sealed class GetTicketHistoryTests
{
    private sealed class Reader : ITicketReadService
    {
        public TicketPageDto<TicketHistoryDto>? Result; public int Calls; public (string Reference, int Page, int PageSize, CancellationToken Token) Received;
        public bool Unavailable = false;
        public Task<TicketDetailDto?> GetAsync(string reference, CancellationToken token) => throw new NotSupportedException();
        public Task<TicketPageDto<TicketSummaryDto>> ListAsync(TicketListCriteria criteria, CancellationToken token) => throw new NotSupportedException();
        public Task<TicketPageDto<TicketHistoryDto>?> GetHistoryAsync(string reference, int page, int pageSize, CancellationToken token)
        {
            Calls++; Received = (reference, page, pageSize, token); token.ThrowIfCancellationRequested();
            if (Unavailable) throw new DependencyUnavailableException();
            return Task.FromResult(Result);
        }
    }
    [Theory]
    [InlineData(0,25)] [InlineData(-1,25)] [InlineData(1,0)] [InlineData(1,-1)] [InlineData(1,101)]
    public async Task Invalid_paging_is_rejected_before_read(int page, int pageSize)
    {
        var reader = new Reader(); var result = await new GetTicketHistoryHandler(reader).Handle(new("ref",page,pageSize),default);
        Assert.Equal("validation_failed", result.Error!.Code); Assert.Equal(400, result.Error.Status); Assert.Equal(0, reader.Calls);
    }
    [Fact]
    public async Task Defaults_and_explicit_bounds_are_forwarded_without_rewriting_revision_or_payload()
    {
        var actor = new Actor("issuer", "staff", "Historical Staff"); var now = DateTimeOffset.Parse("2026-10-08T09:00:00Z");
        var entries = new TicketHistoryDto[] {
            new(Guid.NewGuid(), "9007199254740992",1,"AssignmentChanged","assignedAgent",null,new ReferenceChoice(Guid.NewGuid(),"Historical Agent"),actor,now),
            new(Guid.NewGuid(), "9007199254740993",1,"StatusChanged","status","Closed","Open",actor,now) };
        var reader = new Reader { Result = new(entries,1,25,false) }; using var cancellation = new CancellationTokenSource();
        var handler = new GetTicketHistoryHandler(reader); var result = await handler.Handle(new("reference"), cancellation.Token);
        Assert.Same(reader.Result, result.Value); Assert.Equal(("reference",1,25,cancellation.Token), reader.Received); Assert.Equal("9007199254740993", result.Value!.Items[1].Revision); Assert.Null(result.Value.Items[0].PreviousValue);
        await handler.Handle(new("reference",int.MaxValue,100),cancellation.Token); Assert.Equal(("reference",int.MaxValue,100,cancellation.Token), reader.Received);
    }
    [Fact]
    public async Task Missing_is_404_but_an_empty_page_is_success()
    {
        var reader = new Reader(); var handler = new GetTicketHistoryHandler(reader); Assert.Equal("ticket_not_found", (await handler.Handle(new("missing"),default)).Error!.Code);
        reader.Result = new([],3,25,false); var result = await handler.Handle(new("present",3),default); Assert.True(result.Succeeded); Assert.Empty(result.Value!.Items); Assert.False(result.Value.HasMore);
    }
    [Fact]
    public async Task Cancellation_and_dependency_failure_propagate_without_replay()
    {
        var reader = new Reader(); var handler = new GetTicketHistoryHandler(reader); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.Handle(new("ref"),cancellation.Token)); Assert.Equal(1,reader.Calls);
        reader.Unavailable = true; await Assert.ThrowsAsync<DependencyUnavailableException>(() => handler.Handle(new("ref"),default)); Assert.Equal(2,reader.Calls);
    }
}
