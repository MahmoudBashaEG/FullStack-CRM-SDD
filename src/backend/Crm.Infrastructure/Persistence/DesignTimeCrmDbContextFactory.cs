using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using System.Text.Json;
namespace Crm.Infrastructure.Persistence;

public sealed class DesignTimeCrmDbContextFactory : IDesignTimeDbContextFactory<CrmDbContext>
{
    public CrmDbContext CreateDbContext(string[] args)
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (current is not null && !File.Exists(Path.Combine(current.FullName, "Crm.slnx"))) current = current.Parent;
        var root = current?.FullName ?? throw new InvalidOperationException("Run EF tools within the repository.");
        using var configuration = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "src/backend/Crm.Api/appsettings.json")));
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Crm") ?? configuration.RootElement.GetProperty("ConnectionStrings").GetProperty("Crm").GetString()!;
        return new(new DbContextOptionsBuilder<CrmDbContext>().UseSqlServer(CrmDatabaseSettings.Validate(connection), sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo")).Options);
    }
}
