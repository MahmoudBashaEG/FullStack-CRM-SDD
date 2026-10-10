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
        // An intentional dependency-outage host cannot establish the prerequisite
        // session. That negative fixture signs in against its real owned baseline DB.
        jwt.SignInClient=overrides is not null&&overrides.TryGetValue("ConnectionStrings:Crm",out var target)&&target!=sql.Connection
            ?null:()=>CreateClient(new(){BaseAddress=new("https://localhost:4200")});
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(jwt.Configuration(sql.Connection).AsEnumerable());
            configuration.AddInMemoryCollection(new Dictionary<string,string?>{["Auth:AllowedOrigins:0"]="https://localhost:4200"});
            if (overrides is not null) configuration.AddInMemoryCollection(overrides);
        });
    }
}
