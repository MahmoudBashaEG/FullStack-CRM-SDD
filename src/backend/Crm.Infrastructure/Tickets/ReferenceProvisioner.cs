using Crm.Application.Tickets.Contracts;
using Crm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
namespace Crm.Infrastructure.Tickets;

public sealed record ProvisioningCounts(int Inserted, int Updated, int Unchanged);
public sealed class ProvisioningValidationException : Exception;
public sealed class ReferenceProvisioner(CrmDbContext database)
{
    public async Task<ProvisioningCounts> ApplyAsync(ReferenceProvisioningInput input, CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var fallback = await database.Categories.FromSqlInterpolated($"SELECT * FROM dbo.CategoryReferences WITH (UPDLOCK,HOLDLOCK) WHERE Id = {TicketDefaults.CategoryId}").SingleOrDefaultAsync(cancellationToken);
        if (fallback is null || !fallback.IsAvailable || fallback.DisplayName != "Uncategorised") throw new ProvisioningValidationException();
        int inserted = 0, updated = 0, unchanged = 0;
        foreach (var item in (input.Categories ?? []).OrderBy(x => x.Id))
        {
            var category = await database.Categories.FromSqlInterpolated($"SELECT * FROM dbo.CategoryReferences WITH (UPDLOCK,HOLDLOCK) WHERE Id = {item.Id}").SingleOrDefaultAsync(cancellationToken);
            var label = item.DisplayName.Trim();
            if (category is null) { database.Categories.Add(new() { Id = item.Id, DisplayName = label, IsAvailable = item.IsAvailable }); inserted++; }
            else if (category.DisplayName == label && category.IsAvailable == item.IsAvailable) unchanged++;
            else { category.DisplayName = label; category.IsAvailable = item.IsAvailable; updated++; }
        }
        foreach (var item in (input.Agents ?? []).OrderBy(x => x.Id))
        {
            var agent = await database.Agents.FromSqlInterpolated($"SELECT * FROM dbo.AgentReferences WITH (UPDLOCK,HOLDLOCK) WHERE Id = {item.Id}").SingleOrDefaultAsync(cancellationToken);
            var matches = await database.Agents.FromSqlInterpolated($"SELECT * FROM dbo.AgentReferences WITH (UPDLOCK,HOLDLOCK) WHERE Issuer = {item.Issuer} AND Subject = {item.Subject}").ToListAsync(cancellationToken);
            if (matches.Any(x => x.Issuer == item.Issuer && x.Subject == item.Subject && x.Id != item.Id) || agent is not null && (agent.Issuer != item.Issuer || agent.Subject != item.Subject)) throw new ProvisioningValidationException();
            var label = item.DisplayName.Trim();
            if (agent is null) { database.Agents.Add(new() { Id = item.Id, Issuer = item.Issuer, Subject = item.Subject, DisplayName = label, IsEligible = item.IsEligible }); inserted++; }
            else if (agent.DisplayName == label && agent.IsEligible == item.IsEligible) unchanged++;
            else { agent.DisplayName = label; agent.IsEligible = item.IsEligible; updated++; }
        }
        if (inserted + updated > 0) await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(inserted, updated, unchanged);
    }
}
