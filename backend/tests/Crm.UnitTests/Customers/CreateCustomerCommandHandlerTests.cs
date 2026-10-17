using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Paging;
using Crm.Application.Customers;
using Crm.Application.Customers.CreateCustomer;
using Crm.Domain.Customers;

namespace Crm.UnitTests.Customers;

public sealed class CreateCustomerCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidContact_CreatesUnlinkedContactOnlyCustomerAndSavesOnce()
    {
        var customers = new FakeCustomerRepository();
        var unitOfWork = new FakeUnitOfWork();
        var handler = new CreateCustomerCommandHandler(customers, unitOfWork, TimeProvider.System);

        var result = await handler.Handle(
            new CreateCustomerCommand("  Grace ", " Hopper ", " grace@example.com ", "  ", " Navy Labs "),
            CancellationToken.None);

        var stored = Assert.Single(customers.Added);
        Assert.Null(stored.UserId);
        Assert.False(result.IsRegistered);
        Assert.Equal(stored.Id, result.Id);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("Grace", result.FirstName);
        Assert.Equal("Hopper", result.LastName);
        Assert.Equal("grace@example.com", result.Email);
        Assert.Null(result.PhoneNumber);
        Assert.Equal("Navy Labs", result.Company);
        Assert.Equal(result.CreatedAtUtc, result.UpdatedAtUtc);
        Assert.Equal(1, unitOfWork.SaveCount);
    }

    private sealed class FakeCustomerRepository : ICustomerRepository
    {
        public List<Customer> Added { get; } = [];
        public void Add(Customer customer) => Added.Add(customer);

        public Task<Customer?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Customer?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerDetails?> FindDetailsByIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CustomerDetails?> FindDetailsByUserIdAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<CustomerDetails>> ListAsync(string? search, int page, int pageSize, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakeUnitOfWork : IUnitOfWork
    {
        public int SaveCount { get; private set; }
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) { SaveCount++; return Task.FromResult(1); }
        public Task<TResult> ExecuteInTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> operation, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
