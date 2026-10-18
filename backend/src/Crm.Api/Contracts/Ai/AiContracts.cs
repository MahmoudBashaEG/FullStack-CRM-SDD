using Crm.Application.Ai;
using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Ai;

// No user id, role, ticket fields, system prompt or KB text in any request: the server rebuilds all context (AI-001 §4).
public sealed record SuggestReplyRequest(AiReplyTone? Tone, string? Instructions);

public sealed record AiChatMessageRequest(AiChatRole? Role, string? Text);

public sealed record AiChatRequest(Guid? TicketId, IReadOnlyList<AiChatMessageRequest>? Messages);

public sealed record AiTextResponse(string Text, DateTimeOffset GeneratedAtUtc)
{
    public static AiTextResponse From(AiTextResult r) => new(r.Text, r.GeneratedAtUtc);
}

public sealed record AiCategorySuggestionResponse(
    Guid TicketId, TicketCategory CurrentCategory, TicketCategory SuggestedCategory, string Explanation, bool MatchesCurrent,
    DateTimeOffset GeneratedAtUtc)
{
    public static AiCategorySuggestionResponse From(AiCategorySuggestion s) =>
        new(s.TicketId, s.CurrentCategory, s.SuggestedCategory, s.Explanation, s.MatchesCurrent, s.GeneratedAtUtc);
}

public sealed record AiSourceResponse(Guid ArticleId, string Title)
{
    public static AiSourceResponse From(AiSourceReference s) => new(s.ArticleId, s.Title);
}

public sealed record AiKnowledgeRecommendationResponse(Guid ArticleId, string ArticleTitle, IReadOnlyList<string> Steps)
{
    public static AiKnowledgeRecommendationResponse From(AiKnowledgeRecommendation r) => new(r.ArticleId, r.ArticleTitle, r.Steps);
}

public sealed record AiSolutionsResponse(
    IReadOnlyList<AiKnowledgeRecommendationResponse> KnowledgeBaseRecommendations, IReadOnlyList<string> GeneralRecommendations,
    int ArticlesConsidered, DateTimeOffset GeneratedAtUtc)
{
    public static AiSolutionsResponse From(AiSolutionSuggestions s) =>
        new(s.KnowledgeBaseRecommendations.Select(AiKnowledgeRecommendationResponse.From).ToList(), s.GeneralRecommendations,
            s.ArticlesConsidered, s.GeneratedAtUtc);
}

public sealed record AiChatResponse(string Reply, IReadOnlyList<AiSourceResponse> Sources, Guid? TicketId, DateTimeOffset GeneratedAtUtc)
{
    public static AiChatResponse From(AiChatReply r) => new(r.Reply, r.Sources.Select(AiSourceResponse.From).ToList(), r.TicketId, r.GeneratedAtUtc);
}
