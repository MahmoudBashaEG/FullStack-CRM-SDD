using System.Text.Json;
using Crm.Application.Abstractions.Ai;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Ai;
using Crm.Application.Ai.SuggestCategory;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.Tickets;
using Crm.Domain.Tickets;

namespace Crm.UnitTests.Ai;

public sealed class SuggestTicketCategoryQueryHandlerTests
{
    [Fact]
    public async Task Handle_ModelRecommendsBilling_ReturnsRealEnumWithExplanationUsingOnlyServerTicketData()
    {
        var agentId = Guid.NewGuid();
        var now = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var ticket = new TicketDetails(
            Guid.NewGuid(), "Charged twice for March", "My card shows two identical payments for the March invoice.",
            TicketCategory.Technical, TicketPriority.Medium, TicketStatus.Open, Guid.NewGuid(), "Ada Lovelace", "ada@example.test",
            agentId, "Test Agent", agentId, "Test Agent", now.AddHours(-1), now.AddHours(-1), false, null, null, null, null);
        var tickets = new FakeTicketRepository(ticket);
        var ai = new FakeAiTextGenerator("""{ "category": "Billing", "explanation": "The customer reports a duplicate payment." }""");
        var handler = new SuggestTicketCategoryQueryHandler(
            new AiTicketContextLoader(tickets, new AiLimits()), ai, new FixedTimeProvider(now));

        var result = await handler.Handle(
            new SuggestTicketCategoryQuery(new StaffActor(agentId, IsSuperAdmin: false), ticket.Id), CancellationToken.None);

        Assert.Equal(TicketCategory.Technical, result.CurrentCategory);
        Assert.Equal(TicketCategory.Billing, result.SuggestedCategory);
        Assert.False(result.MatchesCurrent);
        Assert.Equal("The customer reports a duplicate payment.", result.Explanation);
        Assert.Equal(now, result.GeneratedAtUtc);
        Assert.Equal(agentId, tickets.LastAgentScope); // Agent scope applied to the server read
        var prompt = Assert.Single(ai.Prompts);
        Assert.Equal(AiOperation.CategorySuggestion, prompt.Operation);
        Assert.Contains("\"enum\":[\"General\",\"Technical\",\"Billing\",\"Other\"]", JsonSerializer.Serialize(prompt.ResponseSchema));
        var userTurn = Assert.Single(prompt.Turns);
        Assert.Contains("Charged twice for March", userTurn.Text);
        Assert.DoesNotContain("ada@example.test", userTurn.Text + prompt.SystemInstruction); // no customer identity sent (D6)
        Assert.DoesNotContain("Ada Lovelace", userTurn.Text + prompt.SystemInstruction);
    }

    private sealed class FakeTicketRepository(TicketDetails ticket) : ITicketRepository
    {
        public Guid? LastAgentScope { get; private set; }

        public Task<TicketDetails?> FindDetailsByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default)
        {
            LastAgentScope = agentScope;
            return Task.FromResult<TicketDetails?>(id == ticket.Id ? ticket : null);
        }

        public void Add(Ticket ticket) => throw new NotSupportedException();
        public Task<Ticket?> GetByIdAsync(Guid id, Guid? agentScope, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<TicketDetails>> ListAsync(TicketListFilter filter, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<TicketActivityDetails>?> ListActivityAsync(Guid ticketId, Guid? agentScope, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeAiTextGenerator(string reply) : IAiTextGenerator
    {
        public List<AiPrompt> Prompts { get; } = [];

        public Task<string> GenerateAsync(AiPrompt prompt, CancellationToken cancellationToken = default)
        {
            Prompts.Add(prompt);
            return Task.FromResult(reply);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
