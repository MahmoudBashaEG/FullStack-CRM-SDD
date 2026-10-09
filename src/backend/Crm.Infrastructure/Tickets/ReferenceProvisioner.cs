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
        if (input.Agents is { Length: > 0 }) throw new ProvisioningValidationException();
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
        if (inserted + updated > 0) await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(inserted, updated, unchanged);
    }
}
