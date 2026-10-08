using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Application.Tickets.Queries;
using Crm.Domain.Tickets;
namespace Crm.Application.Tests.Tickets;

public sealed class ListTicketsTests
{
    private sealed class Reader : ITicketReadService
    {
        public TicketListCriteria? Criteria;
        public CancellationToken Token;
        public Task<TicketPageDto<TicketSummaryDto>> ListAsync(TicketListCriteria criteria, CancellationToken token)
        { token.ThrowIfCancellationRequested(); Criteria = criteria; Token = token; return Task.FromResult(new TicketPageDto<TicketSummaryDto>([], criteria.Page, criteria.PageSize, false)); }
        public Task<TicketDetailDto?> GetAsync(string reference, CancellationToken token) => throw new NotSupportedException();
        public Task<TicketPageDto<TicketHistoryDto>?> GetHistoryAsync(string reference, int page, int size, CancellationToken token) => throw new NotSupportedException();
    }
    [Fact]
    public async Task All_selected_criteria_are_preserved_in_one_read_with_default_paging()
    {
        var reader = new Reader(); var category = Guid.NewGuid(); var agent = Guid.NewGuid(); using var source = new CancellationTokenSource();
        var result = await new ListTicketsHandler(reader).Handle(new("  Alpha%_[مشكلة]  ", "InProgress", category.ToString(), "High", agent.ToString()), source.Token);
        Assert.True(result.Succeeded); Assert.Empty(result.Value!.Items); Assert.False(result.Value.HasMore);
        Assert.Equal("Alpha%_[مشكلة]", reader.Criteria!.Q); Assert.Equal(TicketStatus.InProgress, reader.Criteria.Status); Assert.Equal(category, reader.Criteria.CategoryId);
        Assert.Equal(TicketPriority.High, reader.Criteria.Priority); Assert.Equal(agent, reader.Criteria.AgentId); Assert.False(reader.Criteria.Unassigned);
        Assert.Equal(1, reader.Criteria.Page); Assert.Equal(25, reader.Criteria.PageSize); Assert.Equal(source.Token, reader.Token);
    }
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("  \t ")]
    public async Task Empty_search_means_no_search_and_omitted_assignment_means_all(string? q)
    {
        var reader = new Reader(); await new ListTicketsHandler(reader).Handle(new(Q: q), CancellationToken.None);
        Assert.Null(reader.Criteria!.Q); Assert.Null(reader.Criteria.AgentId); Assert.False(reader.Criteria.Unassigned);
    }
    [Fact]
    public async Task Unassigned_is_distinct_from_all_and_unknown_well_formed_ids_are_valid()
    {
        var reader = new Reader(); var id = Guid.NewGuid(); var result = await new ListTicketsHandler(reader).Handle(new(CategoryId: id.ToString(), Assignment: "unassigned", Page: 2, PageSize: 100), CancellationToken.None);
        Assert.True(result.Succeeded); Assert.Equal(id, reader.Criteria!.CategoryId); Assert.True(reader.Criteria.Unassigned); Assert.Null(reader.Criteria.AgentId);
        Assert.Equal(2, result.Value!.Page); Assert.Equal(100, result.Value.PageSize);
    }
    [Theory]
    [InlineData("status", "open")] [InlineData("status", "Open,Closed")] [InlineData("status", "0")] [InlineData("status", "")]
    [InlineData("priority", "high")] [InlineData("priority", "High,Urgent")] [InlineData("priority", "")]
    [InlineData("categoryId", "not-a-guid")] [InlineData("categoryId", "")]
    [InlineData("assignment", "Unassigned")] [InlineData("assignment", "all")] [InlineData("assignment", "")]
    public async Task Invalid_single_value_syntax_is_rejected_before_persistence(string field, string value)
    {
        var query = field switch { "status" => new ListTicketsQuery(Status: value), "priority" => new ListTicketsQuery(Priority: value), "categoryId" => new ListTicketsQuery(CategoryId: value), _ => new ListTicketsQuery(Assignment: value) };
        var reader = new Reader(); var result = await new ListTicketsHandler(reader).Handle(query, CancellationToken.None);
        Assert.Equal("validation_failed", result.Error!.Code); Assert.Contains(field, result.Error.Errors!.Keys); Assert.Null(reader.Criteria);
    }
    [Theory]
    [InlineData(0, 25)] [InlineData(-1, 25)] [InlineData(1, 0)] [InlineData(1, 101)]
    public async Task Invalid_paging_is_rejected(int page, int size)
    {
        var reader = new Reader(); var result = await new ListTicketsHandler(reader).Handle(new(Page: page, PageSize: size), CancellationToken.None);
        Assert.Equal("validation_failed", result.Error!.Code); Assert.Null(reader.Criteria);
    }
    [Fact]
    public async Task Cancellation_is_propagated()
    {
        using var source = new CancellationTokenSource(); source.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new ListTicketsHandler(new Reader()).Handle(new(), source.Token));
    }
}
