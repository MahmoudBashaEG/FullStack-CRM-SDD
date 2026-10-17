using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;

namespace Crm.Application.Abstractions.Persistence;

/// <summary>Add only stages; the caller saves through IUnitOfWork (or the transaction does).</summary>
public interface ICustomerRepository
{
    void Add(Customer customer);

    /// <summary>Tracked, for updates.</summary>
    Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Tracked, for updates. The profile linked to an Identity account, if any.</summary>
    Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<CustomerDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<CustomerDetails?> FindDetailsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>search: already trimmed, null/empty = no filter. Ordered CreatedAtUtc DESC, Id DESC.</summary>
    Task<PagedResult<CustomerDetails>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default);
}
