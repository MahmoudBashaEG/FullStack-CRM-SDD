using Crm.Application.Abstractions.Ai;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using FluentValidation;
using Mediator;

namespace Crm.Application.Ai.SuggestSolutions;

/// <summary>AI5: KB-supported steps (validated ids, DB titles) plus separately labelled general suggestions. Read-only.</summary>
public sealed record SuggestTicketSolutionsQuery(StaffActor Actor, Guid TicketId) : IQuery<AiSolutionSuggestions>;

public sealed class SuggestTicketSolutionsQueryHandler(
    AiTicketContextLoader contexts, IAiKnowledgeSearch knowledge, IAiTextGenerator ai, AiLimits limits, TimeProvider timeProvider)
    : IQueryHandler<SuggestTicketSolutionsQuery, AiSolutionSuggestions>
{
    private const string Task =
        "\nTask: Suggest troubleshooting steps for this ticket. Put a step in kbRecommendations ONLY if it comes from a provided " +
        "<kb_article>, and use that article's exact id. You may use your general troubleshooting knowledge: put those steps in " +
        "generalRecommendations; they will be shown as not verified against this Knowledge Base. Never present a general step as " +
        "Knowledge Base content, and never state ticket-specific facts that are not in the data. If no article is relevant, return an " +
        "empty kbRecommendations array. A Knowledge Base procedural instruction (for example \"issue a refund for the duplicate\") is a " +
        "recommended future or conditional action for the agent, not a claim that it already happened: keep such relevant steps in " +
        "kbRecommendations, worded as advice (\"If the duplicate is confirmed, issue a refund…\"), and never describe any step as already " +
        "completed unless the ticket data records it. Answer as JSON.";

    internal static readonly object Schema = new
    {
        type = "OBJECT",
        properties = new
        {
            kbRecommendations = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new { articleId = new { type = "STRING" }, steps = new { type = "ARRAY", items = new { type = "STRING" } } },
                    required = new[] { "articleId", "steps" },
                },
            },
            generalRecommendations = new { type = "ARRAY", items = new { type = "STRING" } },
        },
        required = new[] { "kbRecommendations", "generalRecommendations" },
    };

    public async ValueTask<AiSolutionSuggestions> Handle(SuggestTicketSolutionsQuery query, CancellationToken cancellationToken)
    {
        var context = await contexts.LoadAsync(query.Actor, query.TicketId, cancellationToken);
        var sources = await knowledge.FindPublishedAsync(
            AiKeywords.Extract(context.Ticket.Subject + " " + context.Ticket.Description),
            limits.MaxKnowledgeSources, limits.KnowledgeExcerptCharacters, cancellationToken);

        var json = await ai.GenerateAsync(
            new AiPrompt(
                AiOperation.Solutions, AiPromptBuilder.BaseRules + Task,
                [new AiTurn(AiTurnRole.User, AiPromptBuilder.Ticket(context, limits) + "\n" + AiPromptBuilder.Knowledge(sources))],
                Schema),
            cancellationToken);

        return AiResponseParser.ParseSolutions(json, sources, timeProvider.GetUtcNow());
    }
}

public sealed class SuggestTicketSolutionsValidator : AbstractValidator<SuggestTicketSolutionsQuery>
{
    public SuggestTicketSolutionsValidator() => RuleFor(q => q.TicketId).NotEmpty();
}
