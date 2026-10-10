using System.Net;
using System.Net.Http.Json;
using Crm.Api.Authorization;
using Crm.Application.Common;
using Crm.IntegrationTests.Fixtures;
using Crm.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Crm.IntegrationTests.Security;

[Collection("SQL")]
public sealed class AuthorizationBoundaryTests(SqlServerFixture sql)
{
    private static readonly (string Method, string Route)[] Routes =
    [
        ("POST", "api/tickets"), ("GET", "api/tickets/{reference}"), ("GET", "api/tickets"),
        ("GET", "api/tickets/{reference}/history"), ("GET", "api/ticket-reference-data/categories"),
        ("GET", "api/ticket-reference-data/agents"), ("PATCH", "api/tickets/{reference}/details"),
        ("PUT", "api/tickets/{reference}/assignment"), ("PUT", "api/tickets/{reference}/status")
    ];

    [Fact]
    public async Task Route_inventory_is_exact_and_every_action_requires_shared_queue_authorization()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt);
        using var client = api.CreateClient();
        var actions = api.Services.GetRequiredService<IActionDescriptorCollectionProvider>().ActionDescriptors.Items.OfType<ControllerActionDescriptor>().Where(action=>action.AttributeRouteInfo!.Template!.StartsWith("api/ticket",StringComparison.Ordinal)).ToArray();
        var actual = actions.SelectMany(action => action.ActionConstraints!.OfType<HttpMethodActionConstraint>().SelectMany(constraint => constraint.HttpMethods.Select(method => (method, action.AttributeRouteInfo!.Template!))));
        Assert.Equal(Routes.Order(), actual.Order());
        var endpoints = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().Where(endpoint=>endpoint.RoutePattern.RawText!.StartsWith("api/ticket",StringComparison.Ordinal));
        Assert.Equal(Routes.Order(), endpoints.SelectMany(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>()!.HttpMethods.Select(method => (method, endpoint.RoutePattern.RawText!))).Order());
        foreach (var action in actions)
        {
            Assert.Contains(action.EndpointMetadata.OfType<IAuthorizeData>(), data => data.Policy == SupportQueuePolicy.Name);
            Assert.Empty(action.EndpointMetadata.OfType<IAllowAnonymous>());
        }
        foreach (var (token, expected) in new (string? Token, HttpStatusCode Status)[] { (null, HttpStatusCode.Unauthorized), (jwt.Token(allowed: false), HttpStatusCode.Forbidden), (jwt.Token(issuer: "https://wrong.test"), HttpStatusCode.Unauthorized), (jwt.Token(audience: "wrong"), HttpStatusCode.Unauthorized), (jwt.Token(expired: true), HttpStatusCode.Unauthorized), (jwt.AlterSignedToken(jwt.Token(),"sub"," "), HttpStatusCode.Unauthorized) })
        {
            foreach (var (method, route) in Routes)
            {
                using var request = new HttpRequestMessage(new HttpMethod(method), "/" + route.Replace("{reference}", "missing"));
                if (token is not null) request.Headers.Authorization = new("Bearer", token);
                if (method is "POST" or "PATCH" or "PUT") request.Content = JsonContent.Create(new { });
                var response = await client.SendAsync(request);
                Assert.Equal(expected, response.StatusCode);
            }
        }
        using var otherSigner = new TestJwtFactory(); otherSigner.Bind(sql.Connection);
        client.DefaultRequestHeaders.Authorization = new("Bearer", otherSigner.Token());
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/ticket-reference-data/categories")).StatusCode);
    }

    [Fact]
    public async Task Two_authorized_staff_can_use_all_routes_including_unassigned_tickets()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt); using var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var created = await client.PostAsJsonAsync("/api/tickets", new { subject = "Boundary", description = "Synthetic" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var detail = await created.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>(); var reference = detail.GetProperty("reference").GetString();
        foreach (var subject in new[] { "staff-a", "staff-b" })
        {
            client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token(subject));
            foreach (var (_, route) in Routes.Where(route => route.Method == "GET"))
                Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/" + route.Replace("{reference}", reference))).StatusCode);
            var get = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/tickets/" + reference);
            var version = get.GetProperty("editVersion").GetString();
            Assert.Equal(HttpStatusCode.OK, (await client.PatchAsJsonAsync($"/api/tickets/{reference}/details", new { expectedVersion = version, subject = "Edited " + subject })).StatusCode);
            get = await client.GetFromJsonAsync<System.Text.Json.JsonElement>("/api/tickets/" + reference); version = get.GetProperty("editVersion").GetString();
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/tickets/{reference}/assignment", new { expectedVersion = version, agentId = (Guid?)null })).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await client.PutAsJsonAsync($"/api/tickets/{reference}/status", new { expectedVersion = version, status = "Open" })).StatusCode);
        }
    }

    [Theory]
    [InlineData("TestPublicKeyPath")]
    [InlineData("TestIssuer")]
    [InlineData("TestAudience")]
    [InlineData("testUnknown")]
    public void Production_rejects_every_test_auth_setting(string key)
    {
        var services = new ServiceCollection();
        Assert.Throws<InvalidOperationException>(() => services.AddQueueAuthentication(Configuration(new() { ["Authentication:" + key] = "synthetic" }), new EnvironmentStub()));
    }

    [Theory]
    [InlineData(null, "crm")]
    [InlineData("", "crm")]
    [InlineData("https://issuer.example.test", null)]
    public void Production_auth_fails_closed_without_a_secure_issuer_and_audience(string? authority, string? audience)
    {
        using var provider = ProductionServices(authority, audience);
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme));
    }

    [Fact]
    public void Production_requires_normal_TLS_and_JWT_validation_without_diagnostic_details()
    {
        using var signing=new LocalAccountFixture();using var provider = ProductionServices("urn:crm:local", "crm-api",signing);
        var options = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);
        Assert.True(options.RequireHttpsMetadata); Assert.False(options.IncludeErrorDetails); Assert.Null(options.BackchannelHttpHandler);
        Assert.True(options.TokenValidationParameters.ValidateIssuer); Assert.True(options.TokenValidationParameters.ValidateAudience);
        Assert.True(options.TokenValidationParameters.ValidateLifetime); Assert.True(options.TokenValidationParameters.ValidateIssuerSigningKey);
        Assert.True(options.TokenValidationParameters.RequireSignedTokens); Assert.True(options.TokenValidationParameters.RequireExpirationTime);
    }

    [Fact]
    public async Task Unexpected_failures_and_bad_payloads_do_not_expose_sensitive_diagnostics()
    {
        using var jwt = new TestJwtFactory(); await using var api = new CrmApiFactory(sql, jwt);
        await using var failing = api.WithWebHostBuilder(builder => builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Scoped<ICurrentActor, FailingActor>())));
        using var client = failing.CreateClient(); client.DefaultRequestHeaders.Authorization = new("Bearer", jwt.Token());
        var response = await client.PostAsJsonAsync("/api/tickets", new { subject = "Synthetic", description = "Synthetic" });
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("private-diagnostic-marker", body); Assert.DoesNotContain("Exception", body); Assert.DoesNotContain("ConnectionStrings", body);
        Assert.Contains("unexpected_error", body); Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        response = await client.PostAsJsonAsync("/api/tickets", new { subject = "Synthetic", description = "Synthetic", secret = "private-diagnostic-marker" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode); Assert.DoesNotContain("private-diagnostic-marker", await response.Content.ReadAsStringAsync());
    }

    private static IConfiguration Configuration(Dictionary<string, string?> values) => new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    private static ServiceProvider ProductionServices(string? authority, string? audience,LocalAccountFixture? signing=null)
    {
        var services = new ServiceCollection(); services.AddLogging();
        var values=signing?.Configuration(sqlConnection()).AsEnumerable().ToDictionary(x=>x.Key,x=>x.Value)??new Dictionary<string,string?>();values["Jwt:Issuer"]=authority;values["Jwt:Audience"]=audience;
        var configuration=Configuration(values);if(signing is not null)services.AddInfrastructure(configuration);else{services.AddSingleton(_=>Crm.Infrastructure.Authentication.JwtOptions.Load(configuration));services.AddSingleton<Crm.Infrastructure.Authentication.SigningCertificateProvider>();}
        services.AddQueueAuthentication(configuration, new EnvironmentStub());
        return services.BuildServiceProvider();
        static string sqlConnection()=>"Server=localhost;Database=CrmTicketsIntegration_"+Guid.NewGuid().ToString("N")+";Integrated Security=True;Encrypt=True;TrustServerCertificate=True";
    }
    private sealed class FailingActor : ICurrentActor { public Actor Get() => throw new InvalidOperationException("private-diagnostic-marker"); }
    private sealed class EnvironmentStub : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Crm.Api";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
