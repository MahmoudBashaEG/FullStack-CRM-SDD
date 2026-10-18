namespace Crm.Application.Common.Exceptions;

public enum AiFailureKind
{
    NotConfigured = 0,
    InvalidCredentials = 1,
    ModelUnavailable = 2,
    QuotaExceeded = 3,
    Timeout = 4,
    ProviderUnavailable = 5,
    Blocked = 6,
    InvalidResponse = 7,
}

/// <summary>Fixed, user-safe messages — never provider text, prompt content or the key.</summary>
public static class AiMessages
{
    public const string NotConfigured = "AI assistance is not configured on the server. Ask the administrator to set the Gemini API key and model.";
    public const string InvalidCredentials = "The AI provider rejected the server's API key. Ask the administrator to check the Gemini configuration.";
    public const string ModelUnavailable = "The configured AI model is not available. Ask the administrator to check the Gemini model id.";
    public const string QuotaExceeded = "The AI quota or rate limit has been reached. Try again later.";
    public const string Timeout = "The AI provider did not answer in time. Try again.";
    public const string ProviderUnavailable = "The AI provider is temporarily unavailable. Try again later.";
    public const string Blocked = "The AI provider declined to answer this request. Rephrase it or continue without AI.";
    public const string InvalidResponse = "The AI returned an unusable answer. Try again.";

    public static string For(AiFailureKind kind) => kind switch
    {
        AiFailureKind.NotConfigured => NotConfigured,
        AiFailureKind.InvalidCredentials => InvalidCredentials,
        AiFailureKind.ModelUnavailable => ModelUnavailable,
        AiFailureKind.QuotaExceeded => QuotaExceeded,
        AiFailureKind.Timeout => Timeout,
        AiFailureKind.Blocked => Blocked,
        AiFailureKind.InvalidResponse => InvalidResponse,
        _ => ProviderUnavailable,
    };
}

/// <summary>
/// Mapped by the API to 503 / 429 / 504 / 502 / 422 with ProblemDetails extension "aiError" = Kind. The message is a fixed,
/// user-safe sentence — never provider text, prompt content or the key — because >= 500 responses are logged with the exception.
/// </summary>
public sealed class AiUnavailableException(AiFailureKind kind, string message, TimeSpan? retryAfter = null) : Exception(message)
{
    public AiFailureKind Kind { get; } = kind;

    public TimeSpan? RetryAfter { get; } = retryAfter;
}
