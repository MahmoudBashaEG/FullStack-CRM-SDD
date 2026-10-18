using Crm.Application.Common;
using Crm.Application.Common.Exceptions;
using Crm.Domain.KnowledgeBase;
using FluentValidation;
using FluentValidation.Results;

namespace Crm.Application.KnowledgeBase;

internal static class KnowledgeBaseGuards
{
    /// <summary>Called on an article the actor can already see (repository visibility ⇒ otherwise 404).</summary>
    public static void EnsureCanEdit(KnowledgeArticle article, StaffActor actor)
    {
        if (article.Status == KnowledgeArticleStatus.Archived)
        {
            throw new ConflictException("The article is archived. Restore it first.");
        }

        if (actor.IsSuperAdmin)
        {
            return;
        }

        if (article.Status == KnowledgeArticleStatus.ReadyForPublication)
        {
            throw new ConflictException("This article is waiting for publication. Ask a SuperAdmin to return it for revision before editing.");
        }

        if (article.Status != KnowledgeArticleStatus.Draft || article.AuthorUserId != actor.UserId)
        {
            throw new ForbiddenException("Published articles are maintained by SuperAdmin.");
        }
    }

    public static void EnsureSuperAdmin(StaffActor actor)
    {
        if (!actor.IsSuperAdmin)
        {
            throw new ForbiddenException("Only SuperAdmin can review, publish, archive or restore articles.");
        }
    }

    // 400 ValidationProblemDetails keyed by the camelCased field (as AgentTaskGuards.Invalid).
    public static ValidationException Invalid(string property, string message) => new([new ValidationFailure(property, message)]);

    public static string? NormalizeOptional(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
}
