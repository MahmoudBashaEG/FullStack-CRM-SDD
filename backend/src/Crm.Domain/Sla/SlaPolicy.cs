using Crm.Domain.Common;
using Crm.Domain.Tickets;

namespace Crm.Domain.Sla;

/// <summary>
/// SLA-001 S1: the one global policy (row Id 1). Targets are business minutes per existing TicketPriority — exactly one per member.
/// Changes affect tickets created afterwards only (S4): tickets copy their targets and due instants at creation.
/// </summary>
public sealed class SlaPolicy
{
    public const int SingletonId = 1;

    private SlaPolicy() { } // EF Core

    private readonly List<SlaTarget> _targets = [];

    public int Id { get; private set; }
    public bool IsEnabled { get; private set; }
    public IReadOnlyCollection<SlaTarget> Targets => _targets;
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public Guid UpdatedByUserId { get; private set; }

    public static SlaPolicy Create(bool isEnabled, IReadOnlyList<SlaTarget> targets, Guid actorUserId, DateTimeOffset now)
    {
        var policy = new SlaPolicy { Id = SingletonId };
        policy.Update(isEnabled, targets, actorUserId, now);
        return policy;
    }

    public void Update(bool isEnabled, IReadOnlyList<SlaTarget> targets, Guid actorUserId, DateTimeOffset now)
    {
        if (targets.Count != Enum.GetValues<TicketPriority>().Length
            || targets.Select(t => t.Priority).Distinct().Count() != targets.Count
            || targets.Any(t => t.FirstResponseMinutes <= 0 || t.ResolutionMinutes <= 0))
        {
            throw new DomainException("Provide one positive first-response and resolution target for every priority.");
        }

        IsEnabled = isEnabled;
        _targets.Clear();
        _targets.AddRange(targets);
        UpdatedAtUtc = now;
        UpdatedByUserId = actorUserId;
    }

    public SlaTarget TargetFor(TicketPriority priority) => _targets.Single(t => t.Priority == priority);
}
