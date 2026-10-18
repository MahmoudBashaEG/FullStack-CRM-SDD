using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common;
using Crm.Application.Common.Paging;
using Crm.Application.KnowledgeBase;
using Crm.Application.KnowledgeBase.ChangeArticleStatus;
using Crm.Domain.KnowledgeBase;

namespace Crm.UnitTests.KnowledgeBase;

public sealed class ChangeKnowledgeArticleStatusCommandHandlerTests
{
    [Fact]
    public async Task Handle_SuperAdminPublishesSubmittedAgentArticle_PublishesWithPublisherAndTimeKeepsAuthorAndSavesOnce()
    {
        var agentId = Guid.NewGuid();
        var superAdminId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
        var now = createdAt.AddHours(2);
        var article = KnowledgeArticle.CreateDraft(
            Guid.NewGuid(), agentId, "Reset a locked account", "Unlock steps for agents", "1. Open **Agents**\n2. Choose *Unlock*",
            KnowledgeArticleType.Solution, Guid.NewGuid(), KnowledgeArticleVisibility.Internal, createdAt);
        article.Submit(agentId, createdAt.AddMinutes(30));
        var knowledgeBase = new FakeKnowledgeBaseRepository(article);
        var unitOfWork = new FakeUnitOfWork();
        var handler = new ChangeKnowledgeArticleStatusCommandHandler(knowledgeBase, unitOfWork, new FixedTimeProvider(now));

        var result = await handler.Handle(
            new ChangeKnowledgeArticleStatusCommand(new StaffActor(superAdminId, IsSuperAdmin: true), article.Id, KnowledgeArticleAction.Publish),
            CancellationToken.None);

        Assert.Equal(KnowledgeArticleStatus.Published, article.Status);
        Assert.Equal(superAdminId, article.PublishedByUserId);
        Assert.Equal(now, article.PublishedAtUtc);
        Assert.Equal(now, article.UpdatedAtUtc);
        Assert.Equal(superAdminId, article.UpdatedByUserId);
        Assert.Equal(agentId, article.AuthorUserId); // authorship never changes
        Assert.Equal(KnowledgeArticleVisibility.Internal, article.Visibility);
        Assert.Equal(1, unitOfWork.SaveCount);
        Assert.Equal(KnowledgeArticleStatus.Published, result.Status);
        Assert.Equal("Test Admin", result.PublishedByName);
    }

    private sealed class FakeKnowledgeBaseRepository(KnowledgeArticle article) : IKnowledgeBaseRepository
    {
        public Task<KnowledgeArticle?> GetVisibleArticleAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default) =>
            Task.FromResult<KnowledgeArticle?>(id == article.Id ? article : null);

        public Task<KnowledgeArticleDetails?> FindArticleDetailsAsync(Guid id, StaffActor actor, CancellationToken cancellationToken = default) =>
            Task.FromResult<KnowledgeArticleDetails?>(new KnowledgeArticleDetails(
                article.Id, article.Title, article.Summary, article.Body, article.Type, article.CategoryId, "Accounts", true,
                article.Visibility, article.Status, article.AuthorUserId, "Test Agent", "Test Admin", article.CreatedAtUtc,
                article.UpdatedAtUtc, article.SubmittedAtUtc, article.ReturnReason, article.PublishedAtUtc,
                article.PublishedByUserId is null ? null : "Test Admin", article.ArchivedAtUtc, CanEdit: true, CanSubmit: false));

        public void AddArticle(KnowledgeArticle article) => throw new NotSupportedException();
        public Task<PagedResult<KnowledgeArticleSummary>> ListArticlesAsync(KnowledgeArticleListFilter filter, StaffActor actor, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void AddCategory(KnowledgeCategory category) => throw new NotSupportedException();
        public Task<KnowledgeCategory?> GetCategoryAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> CategoryNameExistsAsync(string name, Guid? exceptId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<KnowledgeCategoryDetails> GetCategoryDetailsAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<KnowledgeCategoryDetails>> ListCategoriesAsync(bool includeInactive, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.FromResult(1); }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
