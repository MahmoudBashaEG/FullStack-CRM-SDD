using Crm.Application.Administration;
using Crm.Application.Common.Paging;
using Crm.Domain.Administration;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>Append-only: Add stages (the caller saves in the same transaction as the audited change); there is no update or delete.</summary>
public interface IAdminAuditRepository
{
    void Add(AdminAuditEntry entry);

    /// <summary>Newest first (OccurredAtUtc DESC, Id DESC). Filters before paging. fromUtc inclusive, toUtcExclusive exclusive.</summary>
    Task<PagedResult<AdminAuditEntryDetails>> ListAsync(
        AdminAuditAction? action, DateTimeOffset? fromUtc, DateTimeOffset? toUtcExclusive, string? search,
        int page, int pageSize, CancellationToken cancellationToken = default);
}
