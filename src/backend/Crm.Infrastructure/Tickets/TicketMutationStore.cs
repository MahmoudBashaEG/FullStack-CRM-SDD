using Crm.Application.Common;
using Crm.Application.Tickets.Contracts;
using Crm.Application.Tickets.Ports;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Crm.Domain.Accounts;
namespace Crm.Infrastructure.Tickets;

public sealed class TicketMutationStore(CrmDbContext database) : ITicketMutationStore
{
    private Task<bool> EligibleAsync(AgentReference agent,CancellationToken ct)=>agent.IsEligible&&agent.LocalUserId.HasValue
        ?database.Users.AsNoTracking().AnyAsync(user=>user.Id==agent.LocalUserId&&user.IsActive&&user.AccountRole==AccountRole.Agent,ct):Task.FromResult(false);
    public Task<OperationResult<TicketDetailDto>> MutateAsync(string reference, byte[] expectedVersion, Func<TicketMutationContext, CancellationToken, Task<Failure?>> mutation, CancellationToken cancellationToken) => DatabaseOperation.RunAsync(() => MutateCoreAsync(reference, expectedVersion, mutation, cancellationToken));
    private async Task<OperationResult<TicketDetailDto>> MutateCoreAsync(string reference, byte[] expectedVersion, Func<TicketMutationContext, CancellationToken, Task<Failure?>> mutation, CancellationToken token)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(token);
        try
        {
            var ticket = await database.Tickets.FromSqlInterpolated($"SELECT * FROM dbo.Tickets WITH (UPDLOCK,HOLDLOCK) WHERE Reference={reference}").SingleOrDefaultAsync(token);
            if (ticket is null) return OperationResult<TicketDetailDto>.Fail("ticket_not_found", 404);
            var rowVersion = database.Entry(ticket).Property<byte[]>("RowVersion").CurrentValue!;
            if (!rowVersion.SequenceEqual(expectedVersion)) return OperationResult<TicketDetailDto>.Fail("ticket_changed", 409);
            var category = await database.Categories.SingleAsync(x => x.Id == ticket.CategoryId, token);
            var agent = ticket.AssignedAgentId is null ? null : await database.Agents.SingleAsync(x => x.Id == ticket.AssignedAgentId, token);
            var revision = ticket.Revision;
            var scope = new TicketMutationContext(ticket, new(category.Id, category.DisplayName, category.IsAvailable), agent is null ? null : new(agent.Id, agent.DisplayName, await EligibleAsync(agent,token)),
                async (id, ct) => { var selected = await database.Categories.FromSqlInterpolated($"SELECT * FROM dbo.CategoryReferences WITH (HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct); return selected is null ? null : new(selected.Id, selected.DisplayName, selected.IsAvailable); },
                // An update lock excludes account writers at their reference-lock step,
                // before their user UPDATE can block this transaction's eligibility read.
                async (id, ct) => { var selected = await database.Agents.FromSqlInterpolated($"SELECT * FROM dbo.AgentReferences WITH (UPDLOCK,HOLDLOCK) WHERE Id={id}").SingleOrDefaultAsync(ct); return selected is null ? null : new(selected.Id, selected.DisplayName, await EligibleAsync(selected,ct)); });
            var failure = await mutation(scope, token);
            if (failure is not null) return new(null, failure);
            if (ticket.Revision != revision)
            {
                database.History.AddRange(ticket.PendingHistory);
                await database.SaveChangesAsync(token);
            }
            // Build from this operation's tracked state while its writer lock is still held.
            category = database.Categories.Local.Single(x => x.Id == ticket.CategoryId);
            agent = ticket.AssignedAgentId is null ? null : database.Agents.Local.Single(x => x.Id == ticket.AssignedAgentId);
            var detail = new TicketDetailDto(ticket.Reference, ticket.Subject, ticket.Description, new(category.Id, category.DisplayName, category.IsAvailable), ticket.Priority.ToString(), ticket.Status.ToString(),
                agent is null ? null : new(agent.Id, agent.DisplayName, await EligibleAsync(agent,token)), ticket.CreatedAtUtc, ticket.UpdatedAtUtc, Convert.ToBase64String(database.Entry(ticket).Property<byte[]>("RowVersion").CurrentValue!));
            await transaction.CommitAsync(token); ticket.AcknowledgePersistedHistory(); return OperationResult<TicketDetailDto>.Success(detail);
        }
        catch
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            try { await transaction.RollbackAsync(cleanup.Token); } catch { /* Preserve the original failure, including uncertain commit outcome. */ }
            throw;
        }
    }
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
            ticket.AcknowledgePersistedHistory();
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
