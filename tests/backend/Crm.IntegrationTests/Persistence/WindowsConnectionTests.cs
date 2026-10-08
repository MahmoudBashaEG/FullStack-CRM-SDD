using System.Security.Principal;
using Crm.Infrastructure.Persistence;
using Crm.IntegrationTests.Fixtures;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Crm.IntegrationTests.Persistence;

[Collection("SQL")]
public sealed class WindowsConnectionTests(SqlServerFixture sql)
{
    [Theory]
    [InlineData("Server=localhost;Database=Test;Integrated Security=True;User ID=")]
    [InlineData("Server=localhost;Database=Test;Integrated Security=True;Password=")]
    [InlineData("Server=localhost;Database=Test;Integrated Security=False")]
    [InlineData("Server=remote.example;Database=Test;Integrated Security=True")]
    public void Credentials_or_remote_database_are_rejected(string connection) => Assert.Throws<InvalidOperationException>(() => CrmDatabaseSettings.Validate(connection));
    [Fact]
    public async Task Api_design_time_and_cli_use_shared_connection_and_inherited_windows_identity()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient();
        using var scope = api.Services.CreateScope();
        var apiDb = scope.ServiceProvider.GetRequiredService<CrmDbContext>();
        var previous = Environment.GetEnvironmentVariable("ConnectionStrings__Crm");
        try
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__Crm", sql.Connection);
            await using var design = new DesignTimeCrmDbContextFactory().CreateDbContext([]);
            foreach (var db in new[] { apiDb, design })
            {
                Assert.Equal(CrmDatabaseSettings.Validate(sql.Connection), CrmDatabaseSettings.Validate(db.Database.GetConnectionString()!));
                await db.Database.OpenConnectionAsync();
                await using var command = db.Database.GetDbConnection().CreateCommand(); command.CommandText = "SELECT ORIGINAL_LOGIN(),SUSER_SNAME(),DB_NAME()";
                await using var reader = await command.ExecuteReaderAsync(); await reader.ReadAsync();
                Assert.Equal(sql.Login, reader.GetString(0)); Assert.Equal(sql.CurrentLogin, reader.GetString(1)); Assert.Equal(new SqlConnectionStringBuilder(sql.Connection).InitialCatalog, reader.GetString(2));
                // Windows may display a local account name while SQL displays its linked MicrosoftAccount alias.
                // Compare OS names across processes separately from SQL ORIGINAL_LOGIN/SUSER_SNAME.
                if (OperatingSystem.IsWindows()) Assert.Equal(sql.WindowsAccount, WindowsIdentity.GetCurrent().Name);
            }
            Assert.Equal(0, (await Crm.IntegrationTests.Tickets.ReferenceProvisioningTests.RunCliAsync(sql.Connection, "{\"schemaVersion\":1,\"categories\":[{\"id\":\"00000000-0000-0000-0000-000000000001\",\"displayName\":\"Uncategorised\",\"isAvailable\":true}]}")).Exit);
        }
        finally { Environment.SetEnvironmentVariable("ConnectionStrings__Crm", previous); }
    }
}
