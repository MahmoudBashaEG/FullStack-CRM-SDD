using Crm.Domain.Tickets;

namespace Crm.Application.Ai;

public enum AiReplyTone { Professional = 0, Friendly = 1, Empathetic = 2, Concise = 3 }

public enum AiChatRole { User = 0, Assistant = 1 }

public sealed record AiChatTurn(AiChatRole Role, string Text);

/// <summary>Excerpt = the first KnowledgeExcerptCharacters of the Markdown body (source text, not rendered).</summary>
public sealed record AiKnowledgeSource(Guid ArticleId, string Title, string? Summary, string Excerpt);

public sealed record AiTextResult(string Text, DateTimeOffset GeneratedAtUtc);

public sealed record AiCategorySuggestion(
    Guid TicketId, TicketCategory CurrentCategory, TicketCategory SuggestedCategory, string Explanation, bool MatchesCurrent,
    DateTimeOffset GeneratedAtUtc);

public sealed record AiSourceReference(Guid ArticleId, string Title);

/// <summary>ArticleTitle always comes from the database, never from the model.</summary>
public sealed record AiKnowledgeRecommendation(Guid ArticleId, string ArticleTitle, IReadOnlyList<string> Steps);

public sealed record AiSolutionSuggestions(
    IReadOnlyList<AiKnowledgeRecommendation> KnowledgeBaseRecommendations, IReadOnlyList<string> GeneralRecommendations,
    int ArticlesConsidered, DateTimeOffset GeneratedAtUtc);

public sealed record AiChatReply(string Reply, IReadOnlyList<AiSourceReference> Sources, Guid? TicketId, DateTimeOffset GeneratedAtUtc);
