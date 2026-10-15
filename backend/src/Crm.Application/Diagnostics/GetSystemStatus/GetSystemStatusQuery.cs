using Mediator;

namespace Crm.Application.Diagnostics.GetSystemStatus;

public sealed record GetSystemStatusQuery : IQuery<SystemStatus>;

public sealed record SystemStatus(string Status, DateTimeOffset CheckedAtUtc);

public sealed class GetSystemStatusQueryHandler(TimeProvider timeProvider)
    : IQueryHandler<GetSystemStatusQuery, SystemStatus>
{
    public const string Healthy = "Healthy";

    public ValueTask<SystemStatus> Handle(GetSystemStatusQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new SystemStatus(Healthy, timeProvider.GetUtcNow()));
}
