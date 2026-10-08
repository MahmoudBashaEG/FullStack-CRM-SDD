using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Crm.Infrastructure.Tickets;

public sealed class TicketMutationStore(CrmDbContext database) : ITicketMutationStore
{
    public Task<OperationResult<TicketDetailDto>> CreateAsync(Guid categoryId, Func<TicketCategoryDto?, OperationResult<Ticket>> create, CancellationToken cancellationToken) => DatabaseOperation.RunAsync(() => CreateCoreAsync(categoryId, create, cancellationToken));
    private async Task<OperationResult<TicketDetailDto>> CreateCoreAsync(Guid categoryId, Func<TicketCategoryDto?, OperationResult<Ticket>> create, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var category = await database.Categories.FromSqlInterpolated($"SELECT * FROM dbo.CategoryReferences WITH (HOLDLOCK) WHERE Id={categoryId}").SingleOrDefaultAsync(cancellationToken);
            var decision = create(category is null ? null : new(category.Id, category.DisplayName, category.IsAvailable));
            if (decision.Error is not null) return new(null, decision.Error);
            var ticket = decision.Value!;
            database.Tickets.Add(ticket); database.History.AddRange(ticket.PendingHistory);
            await database.SaveChangesAsync(cancellationToken);
            var detail = TicketReadService.CreatedDetail(ticket, category!, database.Entry(ticket).Property<byte[]>("RowVersion").CurrentValue!);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<TicketDetailDto>.Success(detail);
        }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await transaction.RollbackAsync(cleanup.Token); } catch { /* Original failure/unknown commit outcome is preserved. */ }
            throw;
        }
    }
}
