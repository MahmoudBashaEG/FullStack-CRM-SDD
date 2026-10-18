using Crm.Application.Abstractions.Persistence;
using Crm.Application.Sla;
using Crm.Domain.Sla;
using Crm.Domain.Tickets;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class SlaMonitoringRepository(CrmDbContext dbContext) : ISlaMonitoringRepository
{
    public async Task<IReadOnlyList<SlaCandidate>> ListWarningCandidatesAsync(SlaCandidateQuery query, CancellationToken cancellationToken = default) =>
        await Candidates(query, breach: false).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<SlaCandidate>> ListBreachCandidatesAsync(SlaCandidateQuery query, CancellationToken cancellationToken = default) =>
        await Candidates(query, breach: true).ToListAsync(cancellationToken);

    // F2: eligibility AND the rule's conditions AND the in-pass exclusion are all SQL predicates applied BEFORE Take, so tickets that do not
    // match the rule can never crowd matching ones out of the page. Ordered (due, Id) for a deterministic, resumable scan.
    private IQueryable<SlaCandidate> Candidates(SlaCandidateQuery q, bool breach)
    {
        var now = q.Now;
        var since = q.EnabledSinceUtc;
        var ruleId = q.RuleId;
        var kind = q.Kind;
        var tickets = dbContext.Tickets.AsNoTracking().Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress);

        if (q.CategoryCondition is { } category)
        {
            tickets = tickets.Where(t => t.Category == category);
        }

        if (q.PriorityCondition is { } priority)
        {
            tickets = tickets.Where(t => t.Priority == priority);
        }

        if (q.AssignedCondition is { } assigned)
        {
            tickets = assigned ? tickets.Where(t => t.AssignedAgentUserId != null) : tickets.Where(t => t.AssignedAgentUserId == null);
        }

        if (q.ExcludedTicketIds.Count > 0)
        {
            var excluded = q.ExcludedTicketIds.ToList();
            tickets = tickets.Where(t => !excluded.Contains(t.Id));
        }

        tickets = tickets.Where(t => !dbContext.AutomationRuleExecutions.Any(e => e.RuleId == ruleId && e.TicketId == t.Id && e.SlaKind == kind));

        if (kind == SlaKind.FirstResponse)
        {
            tickets = tickets.Where(t => t.SlaFirstResponseDueAtUtc != null && t.SlaFirstResponseDueAtUtc >= since);
            tickets = breach
                ? tickets.Where(t => t.SlaFirstResponseDueAtUtc <= now && (t.FirstRespondedAtUtc == null || t.FirstRespondedAtUtc > t.SlaFirstResponseDueAtUtc))
                : tickets.Where(t => t.FirstRespondedAtUtc == null && t.SlaFirstResponseDueAtUtc > now);
            return tickets
                .OrderBy(t => t.SlaFirstResponseDueAtUtc).ThenBy(t => t.Id)
                .Select(t => new SlaCandidate(t.Id, t.Category, t.Priority, t.AssignedAgentUserId, t.SlaFirstResponseDueAtUtc!.Value))
                .Take(q.Limit);
        }

        tickets = tickets.Where(t => t.SlaResolutionDueAtUtc != null && t.SlaResolutionDueAtUtc >= since);
        tickets = breach
            ? tickets.Where(t => t.SlaResolutionDueAtUtc <= now && (t.SlaResolvedAtUtc == null || t.SlaResolvedAtUtc > t.SlaResolutionDueAtUtc))
            : tickets.Where(t => t.SlaResolvedAtUtc == null && t.SlaResolutionDueAtUtc > now);
        return tickets
            .OrderBy(t => t.SlaResolutionDueAtUtc).ThenBy(t => t.Id)
            .Select(t => new SlaCandidate(t.Id, t.Category, t.Priority, t.AssignedAgentUserId, t.SlaResolutionDueAtUtc!.Value))
            .Take(q.Limit);
    }

    public async Task<IReadOnlyList<SlaOverviewRow>> ListActiveTrackedAsync(int limit, CancellationToken cancellationToken = default)
    {
        var rows = await (
                from t in dbContext.Tickets.AsNoTracking()
                where (t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)
                      && t.SlaFirstResponseDueAtUtc != null
                      && (t.FirstRespondedAtUtc == null || t.SlaResolvedAtUtc == null)
                from agent in dbContext.Users.Where(u => u.Id == t.AssignedAgentUserId).DefaultIfEmpty()
                from responder in dbContext.Users.Where(u => u.Id == t.FirstRespondedByUserId).DefaultIfEmpty()
                orderby (t.FirstRespondedAtUtc == null ? t.SlaFirstResponseDueAtUtc : t.SlaResolutionDueAtUtc), t.Id
                select new
                {
                    t.Id,
                    t.Subject,
                    t.Status,
                    t.Category,
                    t.Priority,
                    IsAssigned = t.AssignedAgentUserId != null,
                    AgentName = agent == null ? null : agent.FirstName + " " + agent.LastName,
                    ResponderName = responder == null ? null : responder.FirstName + " " + responder.LastName,
                    t.CreatedAtUtc,
                    t.SlaTimeZoneId,
                    FirstTarget = t.SlaFirstResponseTargetMinutes,
                    FirstDue = t.SlaFirstResponseDueAtUtc,
                    t.FirstRespondedAtUtc,
                    ResolutionTarget = t.SlaResolutionTargetMinutes,
                    ResolutionDue = t.SlaResolutionDueAtUtc,
                    t.SlaResolvedAtUtc,
                })
            .Take(limit)
            .ToListAsync(cancellationToken);

        return rows
            .Select(r => new SlaOverviewRow(
                r.Id, r.Subject, r.Status, r.AgentName,
                new TicketSlaInput(
                    r.Category, r.Priority, r.IsAssigned,
                    new TicketSlaSnapshot(
                        r.CreatedAtUtc, r.SlaTimeZoneId!, r.FirstTarget!.Value, r.FirstDue!.Value, r.FirstRespondedAtUtc, r.ResponderName,
                        r.ResolutionTarget!.Value, r.ResolutionDue!.Value, r.SlaResolvedAtUtc))))
            .ToList();
    }
}
