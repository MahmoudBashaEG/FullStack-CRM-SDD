using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Crm.IntegrationTests.Fixtures;

public sealed class CrmApiFactory(SqlServerFixture sql, TestJwtFactory jwt, IReadOnlyDictionary<string,string?>? overrides = null) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        jwt.Bind(sql.Connection);
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(jwt.Configuration(sql.Connection).AsEnumerable());
            if (overrides is not null) configuration.AddInMemoryCollection(overrides);
        });
    }
}
