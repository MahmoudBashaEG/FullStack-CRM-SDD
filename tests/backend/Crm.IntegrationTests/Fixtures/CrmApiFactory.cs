using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Crm.IntegrationTests.Fixtures;

public sealed class CrmApiFactory(SqlServerFixture sql, TestJwtFactory jwt) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Crm"] = sql.Connection,
            ["Authentication:TestPublicKeyPath"] = jwt.PublicKeyPath,
            ["Authentication:TestIssuer"] = "https://example.test",
            ["Authentication:TestAudience"] = "crm-tickets-tests"
        }));
    }
}
