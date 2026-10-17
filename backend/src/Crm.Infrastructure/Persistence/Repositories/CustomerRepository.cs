using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Domain.Customers;
using Microsoft.EntityFrameworkCore;

namespace Crm.Infrastructure.Persistence.Repositories;

internal sealed class CustomerRepository(CrmDbContext dbContext) : ICustomerRepository
{
    public void Add(Customer customer) => dbContext.Customers.Add(customer);

    public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.Customers.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.Customers.SingleOrDefaultAsync(c => c.UserId == userId, cancellationToken);

    public Task<CustomerDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        Project(dbContext.Customers.Where(c => c.Id == id)).SingleOrDefaultAsync(cancellationToken);

    public Task<CustomerDetails?> FindDetailsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) =>
        Project(dbContext.Customers.Where(c => c.UserId == userId)).SingleOrDefaultAsync(cancellationToken);

    public async Task<PagedResult<CustomerDetails>> ListAsync(
        string? search, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Customers.AsNoTracking();
        if (!string.IsNullOrEmpty(search))
        {
            // Parameterized; EF Core escapes LIKE wildcards for string.Contains. Case-insensitive under the default collation.
            query = query.Where(c =>
                c.FirstName.Contains(search) ||
                c.LastName.Contains(search) ||
                (c.FirstName + " " + c.LastName).Contains(search) ||
                c.Email.Contains(search) ||
                (c.PhoneNumber != null && c.PhoneNumber.Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await Project(query
                .OrderByDescending(c => c.CreatedAtUtc)
                .ThenByDescending(c => c.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<CustomerDetails>(items, page, pageSize, totalCount);
    }

    // Filter/order BEFORE projecting (same rule as IdentityService.Project); IsRegistered is not mapped.
    private static IQueryable<CustomerDetails> Project(IQueryable<Customer> customers) =>
        customers.AsNoTracking().Select(c => new CustomerDetails(
            c.Id, c.FirstName, c.LastName, c.Email, c.PhoneNumber, c.Company, c.UserId != null, c.CreatedAtUtc, c.UpdatedAtUtc));
}
