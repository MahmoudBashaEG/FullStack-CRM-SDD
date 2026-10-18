using System.Text.Json;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using Crm.Domain.Tickets;

namespace Crm.Application.Ai;

/// <summary>Validates every JSON answer from the model. Anything unexpected ⇒ InvalidResponse; nothing is invented or guessed.</summary>
internal static class AiResponseParser
{
    public static (TicketCategory Category, string Explanation) ParseCategory(string json)
    {
        using var document = Parse(json);
        var root = document.RootElement;
        var value = String(root, "category");
        var explanation = String(root, "explanation")?.Trim();
        if (value is null || string.IsNullOrEmpty(explanation)
            || !Enum.TryParse<TicketCategory>(value, ignoreCase: false, out var category)
            || !Enum.IsDefined(category) || value.Any(char.IsDigit))
        {
            throw Invalid();
        }

        return (category, Cut(explanation, AiRules.ExplanationMaxLength));
    }

    public static AiSolutionSuggestions ParseSolutions(string json, IReadOnlyList<AiKnowledgeSource> retrieved, DateTimeOffset now)
    {
        using var document = Parse(json);
        var root = document.RootElement;
        var general = new List<string>();
        var kb = new List<(AiKnowledgeSource Source, List<string> Steps)>();

        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("kbRecommendations", out var kbArray) || kbArray.ValueKind != JsonValueKind.Array
            || !root.TryGetProperty("generalRecommendations", out var generalArray) || generalArray.ValueKind != JsonValueKind.Array)
        {
            throw Invalid();
        }

        foreach (var entry in kbArray.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                throw Invalid();
            }

            var steps = Strings(entry, "steps");
            var source = Guid.TryParse(String(entry, "articleId"), out var id) ? retrieved.FirstOrDefault(s => s.ArticleId == id) : null;
            if (source is null)
            {
                general.AddRange(steps); // ungrounded step ⇒ labelled general (AI5)
                continue;
            }

            var existing = kb.FirstOrDefault(k => k.Source.ArticleId == source.ArticleId);
            if (existing.Steps is null)
            {
                kb.Add((source, steps));
            }
            else
            {
                existing.Steps.AddRange(steps);
            }
        }

        foreach (var item in generalArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw Invalid();
            }

            general.Add(item.GetString()!);
        }

        var kbResult = kb
            .Select(k => new AiKnowledgeRecommendation(k.Source.ArticleId, k.Source.Title, Clean(k.Steps)))
            .Where(r => r.Steps.Count > 0)
            .ToList();
        var generalResult = Clean(general);
        if (kbResult.Count == 0 && generalResult.Count == 0)
        {
            throw Invalid();
        }

        return new AiSolutionSuggestions(kbResult, generalResult, retrieved.Count, now);
    }

    public static AiChatReply ParseChat(string json, IReadOnlyList<AiKnowledgeSource> provided, Guid? ticketId, DateTimeOffset now)
    {
        using var document = Parse(json);
        var root = document.RootElement;
        var reply = String(root, "reply")?.Trim();
        if (string.IsNullOrEmpty(reply))
        {
            throw Invalid();
        }

        var sources = new List<AiSourceReference>();
        if (root.TryGetProperty("usedArticleIds", out var used) && used.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in used.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String && Guid.TryParse(item.GetString(), out var id)
                    && provided.FirstOrDefault(s => s.ArticleId == id) is { } source
                    && sources.All(s => s.ArticleId != id))
                {
                    sources.Add(new AiSourceReference(source.ArticleId, source.Title));
                }
            }
        }

        return new AiChatReply(reply, sources, ticketId, now);
    }

    private static JsonDocument Parse(string json)
    {
        try
        {
            var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                throw Invalid();
            }

            return document;
        }
        catch (JsonException)
        {
            throw Invalid();
        }
    }

    private static string? String(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static List<string> Strings(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            throw Invalid();
        }

        var list = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw Invalid();
            }

            list.Add(item.GetString()!);
        }

        return list;
    }

    private static List<string> Clean(IEnumerable<string> values) =>
        values.Select(v => v.Trim()).Where(v => v.Length > 0).Select(v => Cut(v, AiRules.RecommendationMaxLength))
            .Take(AiRules.MaxRecommendations).ToList();

    private static string Cut(string value, int max) => value.Length > max ? value[..max] : value;

    private static AiUnavailableException Invalid() => new(AiFailureKind.InvalidResponse, AiMessages.InvalidResponse);
}
