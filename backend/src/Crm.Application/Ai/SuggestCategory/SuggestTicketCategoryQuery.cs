using Crm.Application.Abstractions.Ai;
using Crm.Application.Common;
using FluentValidation;
using Mediator;
using Crm.Domain.Tickets;

namespace Crm.Application.Ai.SuggestCategory;

public sealed record SuggestTicketCategoryQuery(StaffActor Actor, Guid TicketId) : IQuery<AiCategorySuggestion>;

/// <summary>AI4: recommendation only — no IUnitOfWork, the ticket is never modified. Apply is PUT /tickets/{id}/category (D1).</summary>
public sealed class SuggestTicketCategoryQueryHandler(AiTicketContextLoader contexts, IAiTextGenerator ai, TimeProvider timeProvider)
    : IQueryHandler<SuggestTicketCategoryQuery, AiCategorySuggestion>
{
    // Gemini responseSchema (OpenAPI subset). The enum is generated from the real TicketCategory, never hard-coded.
    internal static readonly object Schema = new
    {
        type = "OBJECT",
        properties = new
        {
            category = new { type = "STRING", @enum = Enum.GetNames<TicketCategory>() },
            explanation = new { type = "STRING" },
        },
        required = new[] { "category", "explanation" },
    };

    public async ValueTask<AiCategorySuggestion> Handle(SuggestTicketCategoryQuery query, CancellationToken cancellationToken)
    {
        var ticket = await contexts.GetTicketAsync(query.Actor, query.TicketId, cancellationToken);
        var json = await ai.GenerateAsync(
            new AiPrompt(
                AiOperation.CategorySuggestion,
                AiPromptBuilder.BaseRules + "\nTask: Choose the single best category for this ticket from exactly: " +
                    string.Join(", ", Enum.GetNames<TicketCategory>()) +
                    ". Base it on the subject and description only. Explain the choice in one or two sentences. Answer as JSON.",
                [new AiTurn(AiTurnRole.User,
                    $"<ticket>\nSubject: {AiPromptBuilder.Data(ticket.Subject, 200)}\nDescription: {AiPromptBuilder.Data(ticket.Description, 4000)}\n</ticket>")],
                Schema),
            cancellationToken);

        var (category, explanation) = AiResponseParser.ParseCategory(json);
        return new AiCategorySuggestion(
            ticket.Id, ticket.Category, category, explanation, category == ticket.Category, timeProvider.GetUtcNow());
    }
}

public sealed class SuggestTicketCategoryValidator : AbstractValidator<SuggestTicketCategoryQuery>
{
    public SuggestTicketCategoryValidator() => RuleFor(q => q.TicketId).NotEmpty();
}
