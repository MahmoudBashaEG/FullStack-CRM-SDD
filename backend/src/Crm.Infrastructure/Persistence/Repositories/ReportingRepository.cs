using Crm.Application.Abstractions.Persistence;
using Crm.Application.Reports;
using Crm.Domain.Identity;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

/// <summary>REPORT-001: read-only, organisation-wide aggregates. Grouped in SQL; only aggregated cells are materialized.</summary>
internal sealed class ReportingRepository(CrmDbContext dbContext) : IReportingRepository
{
    public async Task<IReadOnlyList<TicketCohortCell>> ListTicketCohortAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default)
    {
        // DATEPART on the stored datetimeoffset: every write uses TimeProvider.GetUtcNow() (offset +00:00) ⇒ UTC day (R8).
        var rows = await CreatedIn(startUtc, endExclusiveUtc)
            .GroupBy(t => new { t.CreatedAtUtc.Year, t.CreatedAtUtc.Month, t.CreatedAtUtc.Day, t.Status, t.Category, t.Priority })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Status, g.Key.Category, g.Key.Priority, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows.Select(r => new TicketCohortCell(new DateOnly(r.Year, r.Month, r.Day), r.Status, r.Category, r.Priority, r.Count)).ToList();
    }

    public async Task<IReadOnlyList<SlaCohortCell>> ListSlaCohortAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        // R2: the SlaEvaluator.Target rule as CASE expressions (completed ⇒ completed <= due ? Met : Breached;
        // else due < now ? Breached : Pending; untracked ⇒ NotApplicable). One outcome per target per ticket.
        // Outcomes are projected as int codes (0 NotApplicable, 1 Met, 2 Breached, 3 Pending) so EF groups them in SQL.
        var rows = await CreatedIn(startUtc, endExclusiveUtc)
            .Select(t => new
            {
                t.CreatedAtUtc.Year,
                t.CreatedAtUtc.Month,
                t.CreatedAtUtc.Day,
                First = t.SlaFirstResponseDueAtUtc == null ? 0
                    : t.FirstRespondedAtUtc != null
                        ? (t.FirstRespondedAtUtc <= t.SlaFirstResponseDueAtUtc ? 1 : 2)
                        : (t.SlaFirstResponseDueAtUtc < now ? 2 : 3),
                Resolution = t.SlaResolutionDueAtUtc == null ? 0
                    : t.SlaResolvedAtUtc != null
                        ? (t.SlaResolvedAtUtc <= t.SlaResolutionDueAtUtc ? 1 : 2)
                        : (t.SlaResolutionDueAtUtc < now ? 2 : 3),
            })
            .GroupBy(x => new { x.Year, x.Month, x.Day, x.First, x.Resolution })
            .Select(g => new { g.Key.Year, g.Key.Month, g.Key.Day, g.Key.First, g.Key.Resolution, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new SlaCohortCell(
                new DateOnly(r.Year, r.Month, r.Day), (ReportSlaOutcome)r.First, (ReportSlaOutcome)r.Resolution, r.Count))
            .ToList();
    }

    public Task<int> CountActiveBreachedTicketsAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        Active().Where(TicketRepository.IsBreached(now)).CountAsync(cancellationToken);

    public async Task<IReadOnlyList<ReportAgent>> ListAgentsAsync(CancellationToken cancellationToken = default) =>
        // Same role filter as IdentityService.ListByRoleAsync; order on the entity before projecting the record.
        await (from u in dbContext.Users.AsNoTracking()
               where dbContext.UserRoles.Any(ur => ur.UserId == u.Id
                   && dbContext.Roles.Any(r => r.Id == ur.RoleId && r.Name == UserRoles.Agent))
               orderby u.LastName, u.FirstName, u.Id
               select new ReportAgent(u.Id, u.FirstName + " " + u.LastName, u.IsActive))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<WorkloadCell>> ListWorkloadAsync(CancellationToken cancellationToken = default) =>
        await dbContext.Tickets.AsNoTracking()
            .GroupBy(t => new { t.AssignedAgentUserId, t.Status, t.Priority })
            .Select(g => new WorkloadCell(g.Key.AssignedAgentUserId, g.Key.Status, g.Key.Priority, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<AssigneeCount>> ListOverdueActiveByAssigneeAsync(DateTimeOffset now, CancellationToken cancellationToken = default) =>
        // A WHERE on Tickets ⇒ a ticket with both targets breached is counted once (R6).
        await Active().Where(TicketRepository.IsBreached(now))
            .GroupBy(t => t.AssignedAgentUserId)
            .Select(g => new AssigneeCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ResolutionActionCount>> ListResolutionActionsAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default)
    {
        var resolved = nameof(TicketStatus.Resolved); // StatusChanged stores the target status name (TicketActivity.StatusChanged)
        return await dbContext.TicketActivities.AsNoTracking()
            .Where(a => a.Type == TicketActivityType.StatusChanged && a.NewValue == resolved
                && a.OccurredAtUtc >= startUtc && a.OccurredAtUtc < endExclusiveUtc && a.ActorUserId != null)
            .GroupBy(a => a.ActorUserId!.Value)
            .Select(g => new ResolutionActionCount(g.Key, g.Count(), g.Select(a => a.TicketId).Distinct().Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RatingCount>> ListFeedbackRatingsAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default) =>
        await SubmittedIn(startUtc, endExclusiveUtc)
            .GroupBy(f => f.Rating)
            .Select(g => new RatingCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

    public async Task<FeedbackParticipationCounts> GetFeedbackParticipationAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, CancellationToken cancellationToken = default)
    {
        var eligible = CreatedIn(startUtc, endExclusiveUtc)
            .Where(t => t.Status == TicketStatus.Resolved || t.Status == TicketStatus.Closed);
        var total = await eligible.CountAsync(cancellationToken);
        var withFeedback = await eligible.Where(t => dbContext.TicketFeedback.Any(f => f.TicketId == t.Id)).CountAsync(cancellationToken);
        return new FeedbackParticipationCounts(total, withFeedback);
    }

    public async Task<IReadOnlyList<RecentFeedback>> ListRecentFeedbackAsync(
        DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc, int limit, CancellationToken cancellationToken = default) =>
        await (from f in SubmittedIn(startUtc, endExclusiveUtc)
               join t in dbContext.Tickets.AsNoTracking() on f.TicketId equals t.Id
               orderby f.CreatedAtUtc descending, f.Id descending
               select new RecentFeedback(t.Id, t.Subject, f.Rating, f.Comment, f.CreatedAtUtc, f.UpdatedAtUtc))
            .Take(limit)
            .ToListAsync(cancellationToken);

    private IQueryable<Ticket> CreatedIn(DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc) =>
        dbContext.Tickets.AsNoTracking().Where(t => t.CreatedAtUtc >= startUtc && t.CreatedAtUtc < endExclusiveUtc);

    private IQueryable<Ticket> Active() =>
        dbContext.Tickets.AsNoTracking().Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress);

    private IQueryable<TicketFeedback> SubmittedIn(DateTimeOffset startUtc, DateTimeOffset endExclusiveUtc) =>
        dbContext.TicketFeedback.AsNoTracking().Where(f => f.CreatedAtUtc >= startUtc && f.CreatedAtUtc < endExclusiveUtc);
}
