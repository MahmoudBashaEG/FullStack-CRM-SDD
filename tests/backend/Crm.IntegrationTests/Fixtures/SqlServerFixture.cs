using System.Security.Principal;
using Crm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Crm.IntegrationTests.Fixtures;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private readonly bool applyMigrations;
    private readonly string? connectionOverride;
    public SqlServerFixture() : this(true, null) { }
    internal SqlServerFixture(bool applyMigrations, string? connectionOverride)
    { this.applyMigrations = applyMigrations; this.connectionOverride = connectionOverride; }
    public string Connection { get; private set; } = "";
    public string Login { get; private set; } = "";
    public string CurrentLogin { get; private set; } = "";
    public string WindowsAccount { get; private set; } = "";
    private string server = "";
    private string catalog = "";
    private readonly string owner = Guid.NewGuid().ToString("N");
    private bool created;
    public CrmDbContext OpenContext() => new(new DbContextOptionsBuilder<CrmDbContext>().UseSqlServer(Connection, sql => sql.MigrationsHistoryTable("__EFMigrationsHistory", "dbo")).Options);
    public async Task InitializeAsync()
    {
        Connection = CrmDatabaseSettings.Validate(connectionOverride ?? Environment.GetEnvironmentVariable("CRM_TEST_SQL_CONNECTION") ?? throw new InvalidOperationException("CRM_TEST_SQL_CONNECTION is required; SQL tests cannot be skipped."));
        var settings = new SqlConnectionStringBuilder(Connection);
        catalog = settings.InitialCatalog;
        if (!catalog.StartsWith("CrmTicketsIntegration_", StringComparison.Ordinal) || catalog.Length > 128 || catalog.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_')) throw new InvalidOperationException("Unsafe integration catalog.");
        settings.InitialCatalog = "master";
        await using var master = new SqlConnection(settings.ConnectionString);
        await master.OpenAsync();
        await using (var identity = new SqlCommand("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('MachineName')),CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),ORIGINAL_LOGIN(),SUSER_SNAME()", master))
        await using (var reader = await identity.ExecuteReaderAsync())
        {
            await reader.ReadAsync(); server = reader.GetString(1); Login = reader.GetString(2); CurrentLogin = reader.GetString(3);
            if (!reader.GetString(0).Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase) || !OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(Login)) throw new InvalidOperationException("Local Windows SQL identity mismatch.");
            WindowsAccount = WindowsIdentity.GetCurrent().Name;
        }
        await using (var exists = new SqlCommand("SELECT DB_ID(@catalog)", master))
        { exists.Parameters.AddWithValue("@catalog", catalog); if (await exists.ExecuteScalarAsync() is not DBNull) throw new InvalidOperationException("Refusing to reset an existing catalog; supply a fresh exclusively owned test name."); }
        await using (var create = new SqlCommand($"CREATE DATABASE [{catalog}]", master)) await create.ExecuteNonQueryAsync();
        created = true;
        await using var database = OpenContext();
        await database.Database.ExecuteSqlInterpolatedAsync($"EXEC sys.sp_addextendedproperty @name=N'CrmFixtureOwner', @value={owner}");
        if (applyMigrations) await database.Database.MigrateAsync();
    }
    public async Task DisposeAsync()
    {
        if (!created) return;
        await using (var connection = new SqlConnection(Connection))
        {
            await connection.OpenAsync();
            await using var verify = new SqlCommand("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ServerName')),DB_NAME(),CONVERT(nvarchar(128),value) FROM sys.extended_properties WHERE class=0 AND name=N'CrmFixtureOwner'", connection);
            await using var reader = await verify.ExecuteReaderAsync();
            if (!await reader.ReadAsync() || reader.GetString(0) != server || reader.GetString(1) != catalog || reader.GetString(2) != owner) throw new InvalidOperationException("Ownership verification failed; database retained.");
        }
        SqlConnection.ClearAllPools();
        var settings = new SqlConnectionStringBuilder(Connection) { InitialCatalog = "master" };
        await using var master = new SqlConnection(settings.ConnectionString);
        await master.OpenAsync();
        await using var drop = new SqlCommand($"ALTER DATABASE [{catalog}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{catalog}]", master);
        await drop.ExecuteNonQueryAsync();
    }
}

[CollectionDefinition("SQL", DisableParallelization = true)]
public sealed class SqlCollection : ICollectionFixture<SqlServerFixture>;
