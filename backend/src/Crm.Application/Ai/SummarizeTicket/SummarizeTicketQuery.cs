using Crm.Application.Abstractions.Ai;
using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using FluentValidation;
using Mediator;

namespace Crm.Application.Ai.SummarizeTicket;

/// <summary>AI2: on-demand summary. Read-only — no IUnitOfWork, nothing is stored.</summary>
public sealed record SummarizeTicketQuery(StaffActor Actor, Guid TicketId) : IQuery<AiTextResult>;

public sealed class SummarizeTicketQueryHandler(
    AiTicketContextLoader contexts, IAiTextGenerator ai, AiLimits limits, TimeProvider timeProvider)
    : IQueryHandler<SummarizeTicketQuery, AiTextResult>
{
    private const string Task =
        "\nTask: Summarize the ticket for a support agent in at most 8 short lines: the customer-reported problem; known actions from " +
        "the activity; current state; open questions or next steps only if supported by the facts. If there is no activity, say " +
        "\"No recorded history.\" Do not invent earlier actions.";

    public async ValueTask<AiTextResult> Handle(SummarizeTicketQuery query, CancellationToken cancellationToken)
    {
        var context = await contexts.LoadAsync(query.Actor, query.TicketId, cancellationToken);
        var text = await ai.GenerateAsync(
            new AiPrompt(
                AiOperation.Summary, AiPromptBuilder.BaseRules + Task,
                [new AiTurn(AiTurnRole.User, AiPromptBuilder.Ticket(context, limits))]),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new AiUnavailableException(AiFailureKind.InvalidResponse, AiMessages.InvalidResponse);
        }

        return new AiTextResult(text.Trim(), timeProvider.GetUtcNow());
    }
}

public sealed class SummarizeTicketValidator : AbstractValidator<SummarizeTicketQuery>
{
    public SummarizeTicketValidator() => RuleFor(q => q.TicketId).NotEmpty();
}
