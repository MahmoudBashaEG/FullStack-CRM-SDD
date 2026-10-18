namespace Crm.Application.Common.Validation;

/// <summary>Hard request caps (400). Configurable trimming lives in AiLimits.</summary>
public static class AiRules
{
    public const int ReplyInstructionMaxLength = 500;
    public const int ChatMessageMaxLength = 4_000;
    public const int ChatMessagesPerRequestMax = 50;
    public const int ExplanationMaxLength = 500;
    public const int RecommendationMaxLength = 1_000;
    public const int MaxRecommendations = 8;
}
