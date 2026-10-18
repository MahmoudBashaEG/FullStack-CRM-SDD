namespace Crm.Application.Ai;

/// <summary>Section "Ai". Usage bounds for one local API instance on the Gemini Free Tier (AI-001 §10). Defaults documented in docs/ai-features.md.</summary>
public sealed class AiLimits
{
    public const string SectionName = "Ai";

    public int RequestsPerUserPerMinute { get; set; } = 6;
    public int MaxChatMessages { get; set; } = 20;
    public int MaxChatHistoryCharacters { get; set; } = 16_000;
    public int MaxTicketContextCharacters { get; set; } = 6_000;
    public int ActivityEntries { get; set; } = 10;
    public int MaxKnowledgeSources { get; set; } = 3;
    public int KnowledgeExcerptCharacters { get; set; } = 1_500;
}
