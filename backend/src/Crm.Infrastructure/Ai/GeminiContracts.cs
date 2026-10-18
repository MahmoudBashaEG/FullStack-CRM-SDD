using System.Text.Json;
using System.Text.Json.Serialization;

namespace Crm.Infrastructure.Ai;

// Wire format of the Gemini generateContent REST API (camelCase). Store is a bool, not bool?, so it is always serialized (D2).
internal sealed record GeminiRequest(
    GeminiContent SystemInstruction, IReadOnlyList<GeminiContent> Contents, GeminiGenerationConfig GenerationConfig, bool Store = false);

internal sealed record GeminiContent(string? Role, IReadOnlyList<GeminiPart> Parts);

internal sealed record GeminiPart(string? Text, bool? Thought = null);

internal sealed record GeminiGenerationConfig(int MaxOutputTokens, double Temperature, string? ResponseMimeType, object? ResponseSchema);

internal sealed record GeminiResponse(IReadOnlyList<GeminiCandidate>? Candidates, GeminiPromptFeedback? PromptFeedback);

internal sealed record GeminiCandidate(GeminiContent? Content, string? FinishReason);

internal sealed record GeminiPromptFeedback(string? BlockReason);

internal sealed record GeminiErrorEnvelope(GeminiError? Error);

internal sealed record GeminiError(int Code, string? Status, string? Message, JsonElement? Details);

internal static class GeminiJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
