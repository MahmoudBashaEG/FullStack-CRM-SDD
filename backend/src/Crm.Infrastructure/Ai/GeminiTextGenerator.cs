using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Crm.Application.Abstractions.Ai;
using Crm.Application.Common.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure.Ai;

/// <summary>
/// AI-001: stateless Gemini generateContent call. The key travels in the x-goog-api-key header only. Logs metadata only — never prompt,
/// response, provider error text, key or headers. Every provider failure becomes a typed AiUnavailableException with a fixed message.
/// </summary>
internal sealed partial class GeminiTextGenerator(
    HttpClient http, IOptions<GeminiOptions> options, TimeProvider timeProvider, ILogger<GeminiTextGenerator> logger) : IAiTextGenerator
{
    private const int MaxAttempts = 2;

    private static readonly HashSet<string> BlockedFinishReasons =
        new(StringComparer.Ordinal) { "SAFETY", "RECITATION", "PROHIBITED_CONTENT", "BLOCKLIST", "SPII", "LANGUAGE" };

    [GeneratedRegex("^[A-Za-z0-9._-]{1,100}$")]
    private static partial Regex ModelIdPattern();

    private sealed record Attempt(HttpStatusCode Status, string Body, TimeSpan? RetryAfterHeader);

    public async Task<string> GenerateAsync(AiPrompt prompt, CancellationToken cancellationToken = default)
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey) || string.IsNullOrWhiteSpace(settings.Model)
            || !ModelIdPattern().IsMatch(settings.Model))
        {
            logger.LogWarning("Gemini {Operation} skipped: not configured.", prompt.Operation);
            throw Fail(AiFailureKind.NotConfigured);
        }

        var body = BuildBody(prompt, settings);
        var path = $"models/{settings.Model}:generateContent";
        var stopwatch = Stopwatch.StartNew();

        for (var attempt = 1; ; attempt++)
        {
            Attempt result;
            try
            {
                result = await SendAsync(path, body, settings, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // the browser left: no mapping, no retry
            }
            catch (OperationCanceledException)
            {
                LogFailure(prompt.Operation, AiFailureKind.Timeout, null, null, stopwatch);
                throw Fail(AiFailureKind.Timeout);
            }
            catch (HttpRequestException)
            {
                if (attempt < MaxAttempts)
                {
                    await Task.Delay(settings.RetryDelay, timeProvider, cancellationToken);
                    continue;
                }

                LogFailure(prompt.Operation, AiFailureKind.ProviderUnavailable, null, null, stopwatch);
                throw Fail(AiFailureKind.ProviderUnavailable);
            }

            if (result.Status is HttpStatusCode.InternalServerError or HttpStatusCode.ServiceUnavailable && attempt < MaxAttempts)
            {
                await Task.Delay(settings.RetryDelay, timeProvider, cancellationToken);
                continue;
            }

            if (result.Status != HttpStatusCode.OK)
            {
                throw MapError(prompt.Operation, result, stopwatch);
            }

            return ParseSuccess(prompt.Operation, result.Body, stopwatch);
        }
    }

    private async Task<Attempt> SendAsync(string path, GeminiRequest body, GeminiOptions settings, CancellationToken cancellationToken)
    {
        // A fresh message, content, header and timeout per attempt: an HttpRequestMessage cannot be sent twice.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(settings.Timeout);
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body, options: GeminiJson.Options) };
        request.Headers.Add("x-goog-api-key", settings.ApiKey);

        using var response = await http.SendAsync(request, timeout.Token);
        var text = await response.Content.ReadAsStringAsync(timeout.Token);
        TimeSpan? retryAfter = response.Headers.RetryAfter?.Delta;
        return new Attempt(response.StatusCode, text, retryAfter);
    }

    private static GeminiRequest BuildBody(AiPrompt prompt, GeminiOptions settings) => new(
        new GeminiContent(null, [new GeminiPart(prompt.SystemInstruction)]),
        prompt.Turns.Select(t => new GeminiContent(t.Role == AiTurnRole.User ? "user" : "model", [new GeminiPart(t.Text)])).ToList(),
        new GeminiGenerationConfig(
            settings.MaxOutputTokens, settings.Temperature, prompt.ResponseSchema is null ? null : "application/json", prompt.ResponseSchema));

    private AiUnavailableException MapError(AiOperation operation, Attempt result, Stopwatch stopwatch)
    {
        var envelope = TryReadError(result.Body);
        var providerStatus = envelope?.Error?.Status;
        var code = (int)result.Status;

        var kind = code switch
        {
            400 when result.Body.Contains("API_KEY_INVALID", StringComparison.Ordinal) => AiFailureKind.InvalidCredentials,
            400 when providerStatus == "FAILED_PRECONDITION" => AiFailureKind.ModelUnavailable,
            400 => AiFailureKind.InvalidResponse,
            401 or 403 => AiFailureKind.InvalidCredentials,
            404 => AiFailureKind.ModelUnavailable,
            429 => AiFailureKind.QuotaExceeded,
            504 => AiFailureKind.Timeout,
            _ => AiFailureKind.ProviderUnavailable,
        };

        LogFailure(operation, kind, code, providerStatus, stopwatch);
        var retryAfter = kind == AiFailureKind.QuotaExceeded ? result.RetryAfterHeader ?? RetryDelayFromDetails(envelope) : null;
        return Fail(kind, retryAfter);
    }

    private string ParseSuccess(AiOperation operation, string body, Stopwatch stopwatch)
    {
        GeminiResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<GeminiResponse>(body, GeminiJson.Options);
        }
        catch (JsonException)
        {
            LogFailure(operation, AiFailureKind.InvalidResponse, 200, null, stopwatch);
            throw Fail(AiFailureKind.InvalidResponse);
        }

        if (response?.PromptFeedback?.BlockReason is not null || response?.Candidates is not { Count: > 0 })
        {
            LogFailure(operation, AiFailureKind.Blocked, 200, null, stopwatch);
            throw Fail(AiFailureKind.Blocked);
        }

        var candidate = response.Candidates[0];
        if (candidate.FinishReason is { } reason && BlockedFinishReasons.Contains(reason))
        {
            LogFailure(operation, AiFailureKind.Blocked, 200, null, stopwatch);
            throw Fail(AiFailureKind.Blocked);
        }

        var text = new StringBuilder();
        foreach (var part in candidate.Content?.Parts ?? [])
        {
            if (part.Thought != true && part.Text is not null)
            {
                text.Append(part.Text);
            }
        }

        if (string.IsNullOrWhiteSpace(text.ToString()))
        {
            LogFailure(operation, AiFailureKind.InvalidResponse, 200, null, stopwatch);
            throw Fail(AiFailureKind.InvalidResponse);
        }

        logger.LogInformation(
            "Gemini {Operation} succeeded in {ElapsedMs} ms (finish {FinishReason})", operation, stopwatch.ElapsedMilliseconds, candidate.FinishReason);
        return text.ToString();
    }

    private void LogFailure(AiOperation operation, AiFailureKind kind, int? status, string? providerStatus, Stopwatch stopwatch) =>
        logger.LogWarning(
            "Gemini {Operation} failed: {FailureKind} (HTTP {StatusCode}, provider status {ProviderStatus}) after {ElapsedMs} ms",
            operation, kind, status, providerStatus, stopwatch.ElapsedMilliseconds);

    private static GeminiErrorEnvelope? TryReadError(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<GeminiErrorEnvelope>(body, GeminiJson.Options);
        }
        catch (JsonException)
        {
            return null; // non-JSON error body is tolerated
        }
    }

    private static TimeSpan? RetryDelayFromDetails(GeminiErrorEnvelope? envelope)
    {
        if (envelope?.Error?.Details is not { ValueKind: JsonValueKind.Array } details)
        {
            return null;
        }

        foreach (var detail in details.EnumerateArray())
        {
            if (detail.ValueKind == JsonValueKind.Object
                && detail.TryGetProperty("@type", out var type) && type.GetString()?.EndsWith("RetryInfo", StringComparison.Ordinal) == true
                && detail.TryGetProperty("retryDelay", out var delay) && delay.ValueKind == JsonValueKind.String
                && delay.GetString() is { } value && value.EndsWith('s')
                && double.TryParse(value[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds) && seconds >= 0)
            {
                return TimeSpan.FromSeconds(seconds);
            }
        }

        return null;
    }

    private static AiUnavailableException Fail(AiFailureKind kind, TimeSpan? retryAfter = null) => new(kind, AiMessages.For(kind), retryAfter);
}
