using System.Text;

namespace Crm.Application.Ai;

/// <summary>Cheap keyword extraction for the SQL LIKE retrieval (no embeddings, no index).</summary>
public static class AiKeywords
{
    private static readonly HashSet<string> StopWords = new(StringComparer.Ordinal)
    {
        "this", "that", "with", "have", "from", "they", "what", "when", "your", "there", "their", "about", "would", "could",
        "should", "please", "thanks", "hello", "after", "before", "cannot", "since", "still", "will", "been", "were", "into",
        "them", "than", "then", "does", "just", "also", "some", "more", "only", "very", "help", "issue", "problem", "customer",
    };

    public static IReadOnlyList<string> Extract(string? text, int max = 8)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var terms = new List<string>();
        var current = new StringBuilder();
        foreach (var ch in text.ToLowerInvariant() + " ")
        {
            if (char.IsLetterOrDigit(ch))
            {
                current.Append(ch);
                continue;
            }

            if (current.Length == 0)
            {
                continue;
            }

            var token = current.ToString();
            current.Clear();
            if (token.Length is >= 4 and <= 40 && !StopWords.Contains(token) && !terms.Contains(token))
            {
                terms.Add(token);
                if (terms.Count == max)
                {
                    break;
                }
            }
        }

        return terms;
    }
}
