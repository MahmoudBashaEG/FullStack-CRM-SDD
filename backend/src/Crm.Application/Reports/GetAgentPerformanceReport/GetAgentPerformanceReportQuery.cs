using Crm.Application.Abstractions.Persistence;
using Crm.Domain.Tickets;
using FluentValidation;
using Mediator;

namespace Crm.Application.Reports.GetAgentPerformanceReport;

public sealed record GetAgentPerformanceReportQuery(ReportPreset Preset, DateOnly? From, DateOnly? To)
    : IQuery<AgentPerformanceReport>, IReportPeriodQuery;

public sealed class GetAgentPerformanceReportQueryHandler(IReportingRepository reporting, TimeProvider timeProvider)
    : IQueryHandler<GetAgentPerformanceReportQuery, AgentPerformanceReport>
{
    public async ValueTask<AgentPerformanceReport> Handle(GetAgentPerformanceReportQuery query, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var period = ReportPeriod.Resolve(query.Preset, query.From, query.To, ReportPeriod.TodayUtc(now));

        var agents = await reporting.ListAgentsAsync(cancellationToken);
        var workload = await reporting.ListWorkloadAsync(cancellationToken);
        var overdue = (await reporting.ListOverdueActiveByAssigneeAsync(now, cancellationToken))
            .ToDictionary(o => o.AgentUserId ?? Guid.Empty, o => o.Count);
        var actions = (await reporting.ListResolutionActionsAsync(period.StartUtc, period.EndExclusiveUtc, cancellationToken))
            .ToDictionary(a => a.ActorUserId);

        static bool IsActive(TicketStatus s) => s is TicketStatus.Open or TicketStatus.InProgress;

        var rows = agents.Select(agent =>
        {
            var mine = workload.Where(c => c.AgentUserId == agent.UserId).ToList();
            int Status(TicketStatus s) => mine.Where(c => c.Status == s).Sum(c => c.Count);
            var open = Status(TicketStatus.Open);
            var inProgress = Status(TicketStatus.InProgress);
            var recorded = actions.GetValueOrDefault(agent.UserId);
            return new AgentPerformanceRow(
                agent.UserId, agent.Name, agent.IsActive,
                open, inProgress, open + inProgress, overdue.GetValueOrDefault(agent.UserId),
                Status(TicketStatus.Resolved), Status(TicketStatus.Closed),
                ReportMath.CountByMember(mine.Where(c => IsActive(c.Status)), c => c.Priority, c => c.Count),
                recorded?.Actions ?? 0, recorded?.DistinctTickets ?? 0);
        }).ToList();

        var agentIds = agents.Select(a => a.UserId).ToHashSet();
        var totals = new AgentPerformanceTotals(
            workload.Where(c => c.Status == TicketStatus.Open).Sum(c => c.Count),
            workload.Where(c => c.Status == TicketStatus.InProgress).Sum(c => c.Count),
            rows.Sum(r => r.ActiveAssigned),
            rows.Sum(r => r.OverdueActive),
            workload.Where(c => c.AgentUserId is null && IsActive(c.Status)).Sum(c => c.Count),
            actions.Values.Where(a => agentIds.Contains(a.ActorUserId)).Sum(a => a.Actions),
            actions.Values.Where(a => !agentIds.Contains(a.ActorUserId)).Sum(a => a.Actions)); // SuperAdmin etc.: shown, never credited (R5)

        return new AgentPerformanceReport(period, rows, totals, now);
    }
}

public sealed class GetAgentPerformanceReportValidator : AbstractValidator<GetAgentPerformanceReportQuery>
{
    public GetAgentPerformanceReportValidator(TimeProvider timeProvider) => this.AddReportPeriodRules(timeProvider);
}
