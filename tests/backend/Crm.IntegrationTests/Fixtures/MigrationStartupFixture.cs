using System.Collections.Concurrent;
using System.Data.Common;
using System.Security.Cryptography;
using Crm.Domain.Tickets;
using Crm.Infrastructure.Persistence;
using Crm.IntegrationTests.Tickets;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Crm.IntegrationTests.Fixtures;

public enum StartupSchema { Fresh, InitialStorage, Current }
public enum StartupFault { None, MigrationCommand, Cancellation, MissingMigrations, ModelDrift }

public sealed class MigrationStartupFixture : IAsyncDisposable
{
    public const string InitialMigration = "20261008164804_InitialTicketStorage";
    public const string SearchMigration = "20261008224935_LongTicketSubjectSearch";
    public static readonly string[] CurrentMigrations=[InitialMigration,SearchMigration,"20261009124742_LocalCrmAuthentication","20261009131155_RefreshSessionLookupIndex"];
    private readonly SqlServerFixture owned;
    private MigrationStartupFixture(SqlServerFixture owned) => this.owned = owned;
    public string Connection => owned.Connection;
    public string WindowsAccount => owned.WindowsAccount;
    public CrmDbContext OpenContext() => owned.OpenContext();

    public static async Task<MigrationStartupFixture> CreateAsync(StartupSchema schema)
    {
        var configured = CrmDatabaseSettings.Validate(Environment.GetEnvironmentVariable("CRM_TEST_SQL_CONNECTION")
            ?? throw new InvalidOperationException("CRM_TEST_SQL_CONNECTION is required."));
        var settings = new SqlConnectionStringBuilder(configured);
        if (!settings.InitialCatalog.StartsWith("CrmTicketsIntegration_", StringComparison.Ordinal)
            || settings.InitialCatalog.Length > 128 || settings.InitialCatalog.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            throw new InvalidOperationException("A disposable integration catalog is required before selecting a child fixture catalog.");
        // Only the catalog changes; all fixture/API/tool hosts inherit the same Windows identity.
        settings.InitialCatalog = "CrmTicketsIntegration_" + Guid.NewGuid().ToString("N");
        var owned = new SqlServerFixture(false, settings.ConnectionString);
        try
        {
            await owned.InitializeAsync();
            if (schema != StartupSchema.Fresh)
            {
                await using var context = owned.OpenContext();
                if (schema == StartupSchema.InitialStorage) await context.GetService<IMigrator>().MigrateAsync(InitialMigration);
                else await context.Database.MigrateAsync();
            }
            return new(owned);
        }
        catch { await owned.DisposeAsync(); throw; }
    }

    public async Task<string[]> AppliedMigrationsAsync()
    {
        await using var context = OpenContext();
        return (await context.Database.GetAppliedMigrationsAsync()).ToArray();
    }

    public async Task<int> ObjectExistsAsync(string name)
    {
        await using var connection = new SqlConnection(Connection); await connection.OpenAsync();
        await using var command = new SqlCommand("SELECT CASE WHEN OBJECT_ID(@name) IS NULL THEN 0 ELSE 1 END", connection);
        command.Parameters.AddWithValue("@name", name); return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public static Dictionary<string, string> SourceFingerprints()
    {
        var root = Path.Combine(ReferenceProvisioningTests.RepositoryRoot(), "src", "backend");
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(part => part is "bin" or "obj") && Path.GetExtension(path) is ".cs" or ".csproj")
            .Order(StringComparer.Ordinal).ToDictionary(path => path, path => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
    }

    public ValueTask DisposeAsync() => new(owned.DisposeAsync());
}

public sealed class MigrationStartupApiFactory(MigrationStartupFixture sql, TestJwtFactory jwt,
    StartupFault fault = StartupFault.None, string environment = "Development") : WebApplicationFactory<Program>
{
    public MigrationStartupProbe Probe { get; } = new(fault);
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(environment);
        jwt.Bind(sql.Connection);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(jwt.Configuration(sql.Connection).AsEnumerable());
        });
        builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<CrmDbContext>((provider, options) =>
            {
                options.UseSqlServer(sql.Connection, server =>
                {
                    server.MigrationsHistoryTable("__EFMigrationsHistory", "dbo");
                    if (fault == StartupFault.MissingMigrations) server.MigrationsAssembly(typeof(MigrationStartupApiFactory).Assembly.FullName);
                }).AddInterceptors(Probe);
                if (fault == StartupFault.ModelDrift) options.ReplaceService<IModelCustomizer, StartupDriftModelCustomizer>();
                Probe.StopApplication = () => provider.GetRequiredService<IHostApplicationLifetime>().StopApplication();
            });
        });
    }
}

public sealed class StartupDriftModelCustomizer(ModelCustomizerDependencies dependencies) : ModelCustomizer(dependencies)
{
    public override void Customize(ModelBuilder modelBuilder, DbContext context)
    { base.Customize(modelBuilder, context); modelBuilder.Entity<Ticket>().Property<string>("StartupDriftOnlyInTests"); }
}

public sealed class MigrationStartupProbe(StartupFault fault) : DbCommandInterceptor
{
    public const string PrivateMarker = "private-startup-diagnostic-marker";
    public ConcurrentQueue<string> Commands { get; } = new();
    public ConcurrentDictionary<DbContext, byte> Contexts { get; } = new();
    public Action? StopApplication { get; set; }
    public bool CancellationObserved { get; private set; }
    public static bool IsMigrationWrite(string command) => command.Contains("CREATE TABLE", StringComparison.OrdinalIgnoreCase)
        || command.Contains("CREATE FUNCTION", StringComparison.OrdinalIgnoreCase) || command.Contains("ALTER TABLE", StringComparison.OrdinalIgnoreCase)
        || command.Contains("INSERT INTO [dbo].[__EFMigrationsHistory]", StringComparison.OrdinalIgnoreCase)
        || command.Contains("INSERT INTO [__EFMigrationsHistory]", StringComparison.OrdinalIgnoreCase);
    private void Observe(DbCommand command, CommandEventData data, CancellationToken token)
    {
        Commands.Enqueue(command.CommandText); if (data.Context is not null) Contexts.TryAdd(data.Context, 0);
        if (!IsMigrationWrite(command.CommandText)) return;
        if (fault == StartupFault.MigrationCommand) throw new InvalidOperationException(PrivateMarker);
        if (fault == StartupFault.Cancellation)
        {
            StopApplication!(); CancellationObserved = token.CanBeCanceled && token.IsCancellationRequested;
            token.ThrowIfCancellationRequested(); throw new InvalidOperationException("Startup cancellation was not propagated.");
        }
    }
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken token = default)
    { Observe(command, data, token); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken token = default)
    { Observe(command, data, token); return ValueTask.FromResult(result); }
    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<object> result, CancellationToken token = default)
    { Observe(command, data, token); return ValueTask.FromResult(result); }
}
