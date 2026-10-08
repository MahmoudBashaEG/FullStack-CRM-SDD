using Crm.Application.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
namespace Crm.Infrastructure.Persistence;

internal static class DatabaseOperation
{
    public static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (SqlException) { throw new DependencyUnavailableException(); }
        catch (DbUpdateException) { throw new DependencyUnavailableException(); }
    }
}
