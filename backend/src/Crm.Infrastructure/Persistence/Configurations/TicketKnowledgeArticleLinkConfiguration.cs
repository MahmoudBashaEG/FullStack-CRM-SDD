using Crm.Domain.KnowledgeBase;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Crm.Infrastructure.Persistence.Configurations;

internal sealed class TicketKnowledgeArticleLinkConfiguration : IEntityTypeConfiguration<TicketKnowledgeArticleLink>
{
    public const string TicketArticleIndex = "UX_TicketKnowledgeArticleLinks_TicketArticle";

    public void Configure(EntityTypeBuilder<TicketKnowledgeArticleLink> builder)
    {
        builder.ToTable("TicketKnowledgeArticleLinks");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedOnAdd(); // bigint IDENTITY

        builder.HasOne<Ticket>().WithMany().HasForeignKey(l => l.TicketId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<KnowledgeArticle>().WithMany().HasForeignKey(l => l.ArticleId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(l => l.LinkedByUserId).OnDelete(DeleteBehavior.Restrict);

        // One link per (ticket, article). Referenced by EfUnitOfWork.KnownUniqueIndexes.
        builder.HasIndex(l => new { l.TicketId, l.ArticleId }).IsUnique().HasDatabaseName(TicketArticleIndex);
        builder.HasIndex(l => l.ArticleId);
    }
}
