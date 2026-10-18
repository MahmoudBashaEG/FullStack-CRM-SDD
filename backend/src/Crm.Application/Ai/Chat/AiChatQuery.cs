using Crm.Application.Abstractions.Ai;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Validation;
using FluentValidation;
using Mediator;

namespace Crm.Application.Ai.Chat;

/// <summary>AI6: stateless staff chat. History comes from the client (capped), ticket access is re-checked every turn. No IUnitOfWork.</summary>
public sealed record AiChatQuery(StaffActor Actor, Guid? TicketId, IReadOnlyList<AiChatTurn> Messages) : IQuery<AiChatReply>;

public sealed class AiChatQueryHandler(
    AiTicketContextLoader contexts, IAiKnowledgeSearch knowledge, IAiTextGenerator ai, AiLimits limits, TimeProvider timeProvider)
    : IQueryHandler<AiChatQuery, AiChatReply>
{
    private const string Task =
        "\nTask: Answer the staff member's latest message, using earlier turns for context. Use <kb_article> content when relevant and " +
        "list the ids you used in usedArticleIds. You may also answer from general troubleshooting knowledge; say when an answer is " +
        "general guidance rather than Knowledge Base content, and do not list ids for it. Statements about the selected ticket must be " +
        "supported by its data. Answer as JSON.";

    internal static readonly object Schema = new
    {
        type = "OBJECT",
        properties = new
        {
            reply = new { type = "STRING" },
            usedArticleIds = new { type = "ARRAY", items = new { type = "STRING" } },
        },
        required = new[] { "reply" },
    };

    public async ValueTask<AiChatReply> Handle(AiChatQuery query, CancellationToken cancellationToken)
    {
        // Every turn: a reassigned Agent gets 404 on the next message.
        var context = query.TicketId is { } id ? await contexts.LoadAsync(query.Actor, id, cancellationToken) : null;
        var messages = Trim(query.Messages);
        var last = messages[^1].Text;

        var terms = AiKeywords.Extract(last + (context is null ? "" : " " + context.Ticket.Subject));
        var sources = await knowledge.FindPublishedAsync(terms, limits.MaxKnowledgeSources, limits.KnowledgeExcerptCharacters, cancellationToken);

        var system = AiPromptBuilder.BaseRules + Task
            + (context is null
                ? "\nNo ticket is selected; do not discuss specific tickets or customers."
                : "\nThe conversation is about this ticket:\n" + AiPromptBuilder.Ticket(context, limits))
            + "\nReference articles:\n" + AiPromptBuilder.Knowledge(sources);

        var turns = messages
            .Select(m => new AiTurn(m.Role == AiChatRole.User ? AiTurnRole.User : AiTurnRole.Model, m.Text))
            .ToList();

        var json = await ai.GenerateAsync(new AiPrompt(AiOperation.Chat, system, turns, Schema), cancellationToken);
        return AiResponseParser.ParseChat(json, sources, query.TicketId, timeProvider.GetUtcNow());
    }

    /// <summary>Newest MaxChatMessages, then drop the oldest while over the character budget; first turn is always User; the new message stays.</summary>
    private List<AiChatTurn> Trim(IReadOnlyList<AiChatTurn> messages)
    {
        var kept = messages.Skip(Math.Max(0, messages.Count - limits.MaxChatMessages)).ToList();
        while (kept.Count > 1 && kept.Sum(m => m.Text.Length) > limits.MaxChatHistoryCharacters)
        {
            kept.RemoveAt(0);
        }

        while (kept.Count > 1 && kept[0].Role != AiChatRole.User)
        {
            kept.RemoveAt(0);
        }

        return kept;
    }
}

public sealed class AiChatValidator : AbstractValidator<AiChatQuery>
{
    public AiChatValidator()
    {
        RuleFor(q => q.TicketId).NotEqual(Guid.Empty).When(q => q.TicketId is not null);
        RuleFor(q => q.Messages).NotNull().Must(m => m is { Count: >= 1 and <= AiRules.ChatMessagesPerRequestMax })
            .WithMessage($"Send between 1 and {AiRules.ChatMessagesPerRequestMax} messages.");
        RuleForEach(q => q.Messages).ChildRules(message =>
        {
            message.RuleFor(m => m.Role).IsInEnum();
            message.RuleFor(m => m.Text).NotEmpty().Must(t => !string.IsNullOrWhiteSpace(t)).WithMessage("'Text' must not be empty.")
                .MaximumLength(AiRules.ChatMessageMaxLength);
        });
        RuleFor(q => q.Messages).Must(m => m.Count == 0 || m[^1].Role == AiChatRole.User)
            .WithMessage("The last message must be from the user.")
            .When(q => q.Messages is not null);
    }
}
