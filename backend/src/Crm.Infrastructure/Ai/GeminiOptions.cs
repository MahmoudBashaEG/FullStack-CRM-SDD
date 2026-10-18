namespace Crm.Infrastructure.Ai;

/// <summary>
/// Section "Gemini" (AI-001 AI1/AI8). ApiKey is a SECRET: user-secrets "Gemini:ApiKey" or env GEMINI_API_KEY — never appsettings,
/// never logged, never returned. Model: the exact id of a Free Tier–eligible model chosen in Google AI Studio (no default in source).
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string? ApiKey { get; set; }
    public string? Model { get; set; }
    public Uri BaseUrl { get; set; } = new("https://generativelanguage.googleapis.com/v1beta/");
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxOutputTokens { get; set; } = 2048;
    public double Temperature { get; set; } = 0.3;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(2);
}
