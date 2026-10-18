namespace Crm.Application.Abstractions.Ai;

/// <summary>
/// AI-001: the single text-generation port (Gemini in Infrastructure). Stateless — every call carries the full prompt; nothing is
/// stored by the app or (generateContent) by the provider session. Throws AiUnavailableException for every provider failure.
/// Implementations never log prompt or response text.
/// </summary>
public interface IAiTextGenerator
{
    /// <summary>Returns the model's text (JSON text when <see cref="AiPrompt.ResponseSchema"/> is set).</summary>
    Task<string> GenerateAsync(AiPrompt prompt, CancellationToken cancellationToken = default);
}

public enum AiOperation
{
    Summary = 0,
    ReplyDraft = 1,
    CategorySuggestion = 2,
    Solutions = 3,
    Chat = 4,
}

public enum AiTurnRole
{
    User = 0,
    Model = 1,
}

public sealed record AiTurn(AiTurnRole Role, string Text);

/// <summary>
/// SystemInstruction is built on the server only (AiPromptBuilder). Turns hold data and conversation; the first turn is always User.
/// ResponseSchema: null = plain text; otherwise a Gemini responseSchema object (OpenAPI subset) serialized as-is, and JSON is requested.
/// </summary>
public sealed record AiPrompt(AiOperation Operation, string SystemInstruction, IReadOnlyList<AiTurn> Turns, object? ResponseSchema = null);
