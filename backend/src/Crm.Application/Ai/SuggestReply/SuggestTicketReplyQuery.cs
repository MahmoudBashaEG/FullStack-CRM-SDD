using Crm.Application.Abstractions.Ai;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Application.Common.Validation;
using FluentValidation;
using Mediator;

namespace Crm.Application.Ai.SuggestReply;

/// <summary>AI3: a reply DRAFT only. Never sent, never recorded as a first response. No IUnitOfWork.</summary>
public sealed record SuggestTicketReplyQuery(StaffActor Actor, Guid TicketId, AiReplyTone? Tone, string? Instructions) : IQuery<AiTextResult>;

public sealed class SuggestTicketReplyQueryHandler(
    AiTicketContextLoader contexts, IAiKnowledgeSearch knowledge, IAiTextGenerator ai, AiLimits limits, TimeProvider timeProvider)
    : IQueryHandler<SuggestTicketReplyQuery, AiTextResult>
{
    private const string Task =
        "\nTask: Write a reply DRAFT addressed to the customer. Ticket-specific statements must be supported by the data. Do not claim " +
        "actions, refunds, timelines or policies that are not in the data. You may suggest generic troubleshooting steps the customer " +
        "can try, worded as suggestions. Use <kb_article> content when relevant. Follow the <staff_instruction>, if present, for tone, " +
        "focus, length and what to ask or include, as long as it does not require unsupported facts or break the rules. If it does, " +
        "omit that statement or use accurate pending/conditional wording, for example \"[Agent: Verify the refund status before sending]\". " +
        "A staff instruction is a request about what to write, not evidence that it is true. Do not include a signature with a real name; end with \"[Agent name]\". The draft is not " +
        "sent automatically. Before answering, classify every action as completed, actively ongoing, pending or proposed. Only " +
        "completed or ongoing actions that are explicitly recorded in the data may be stated as such. If an action is not recorded as " +
        "ongoing, never use present-progressive customer-facing phrases such as \"we are reviewing\", \"we are investigating\", \"we " +
        "are looking into\", \"I am checking\" or \"while we look into this\" in any tone. Use the documented pending status (for " +
        "example \"your request is awaiting review\") or future/conditional wording (\"we will review the details and update you\"). " +
        "Do not invent completed actions, current work, refund processing or guaranteed timelines. Keep natural customer-service wording.";

    public async ValueTask<AiTextResult> Handle(SuggestTicketReplyQuery query, CancellationToken cancellationToken)
    {
        var context = await contexts.LoadAsync(query.Actor, query.TicketId, cancellationToken);
        var sources = await knowledge.FindPublishedAsync(
            AiKeywords.Extract(context.Ticket.Subject + " " + context.Ticket.Description),
            limits.MaxKnowledgeSources, limits.KnowledgeExcerptCharacters, cancellationToken);

        var instruction = query.Instructions?.Trim();
        var userTurn = AiPromptBuilder.Ticket(context, limits) + "\n" + AiPromptBuilder.Knowledge(sources)
            + (string.IsNullOrEmpty(instruction)
                ? ""
                : "\n<staff_instruction unverified=\"true\">" + AiPromptBuilder.Data(instruction, AiRules.ReplyInstructionMaxLength) + "</staff_instruction>");

        var text = await ai.GenerateAsync(
            new AiPrompt(
                AiOperation.ReplyDraft, AiPromptBuilder.BaseRules + "\n" + ToneLine(query.Tone!.Value) + Task,
                [new AiTurn(AiTurnRole.User, userTurn)]),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new AiUnavailableException(AiFailureKind.InvalidResponse, AiMessages.InvalidResponse);
        }

        return new AiTextResult(text.Trim(), timeProvider.GetUtcNow());
    }

    private static string ToneLine(AiReplyTone tone) => tone switch
    {
        AiReplyTone.Friendly => "Tone: warm and friendly, still professional.",
        AiReplyTone.Empathetic => "Tone: empathetic; acknowledge the customer's situation before the solution.",
        AiReplyTone.Concise => "Tone: concise; at most 5 short sentences.",
        _ => "Tone: professional, clear and courteous.",
    };
}

public sealed class SuggestTicketReplyValidator : AbstractValidator<SuggestTicketReplyQuery>
{
    public SuggestTicketReplyValidator()
    {
        RuleFor(q => q.TicketId).NotEmpty();
        RuleFor(q => q.Tone).NotNull().IsInEnum();
        RuleFor(q => q.Instructions).MaximumLength(AiRules.ReplyInstructionMaxLength);
    }
}
