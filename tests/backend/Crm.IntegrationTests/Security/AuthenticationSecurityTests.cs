using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Domain.Accounts;
using Crm.IntegrationTests.Fixtures;
using Microsoft.EntityFrameworkCore;
namespace Crm.IntegrationTests.Security;

[Collection("SQL")]
public sealed class AuthenticationSecurityTests(SqlServerFixture sql)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_prior_signin_token_requires_explicit_rollover_configuration(bool allowPrior)
    {
        using var prior = new TestJwtFactory();
        await using var oldApi = new CrmApiFactory(sql, prior);
        using var oldClient = oldApi.CreateClient();
        var oldToken = prior.Token(); // Normal HTTP signin establishes the persisted family.
        using var current = new TestJwtFactory();
        var configuration = new Dictionary<string, string?>();
        if (allowPrior) configuration["Jwt:PreviousValidationCertificateThumbprints:0"] = prior.Configuration(sql.Connection)["Jwt:SigningCertificateThumbprint"];
        await using var newApi = new CrmApiFactory(sql, current, configuration);
        using var client = newApi.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", oldToken);
        using var oldResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(allowPrior ? HttpStatusCode.OK : HttpStatusCode.Unauthorized, oldResponse.StatusCode);
        if (allowPrior) Assert.Equal(prior.UserId(), (await oldResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("userId").GetGuid());
        client.DefaultRequestHeaders.Authorization = new("Bearer", current.Token());
        using var newResponse = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, newResponse.StatusCode);
    }

    [Fact]
    public async Task Absolute_session_expiry_rejects_unexpired_access_and_refresh()
    {
        await using var api = await AuthenticationApiFixture.CreateAsync();
        var password = Guid.NewGuid().ToString("N");
        var access = await api.SignInAsync(await api.PrepareAsync(AccountRole.Agent, password), password);
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(access);
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
        await using (var db = api.Sql.OpenContext())
        {
            var session = await db.AuthenticationSessions.SingleAsync();
            session.CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-8);
            session.ExpiresAtUtc = session.CreatedAtUtc.AddDays(7);
            var credential = await db.RefreshCredentials.SingleAsync();
            credential.CreatedAtUtc = session.CreatedAtUtc;
            credential.ExpiresAtUtc = session.ExpiresAtUtc;
            await db.SaveChangesAsync();
        }
        using var me = await api.SendAsync("/api/auth/me", token: access);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        using var refresh = await api.SendAsync("/api/auth/refresh", HttpMethod.Post, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
        Assert.False(api.Cookies.ContainsKey("__Host-crm-refresh"));
    }

    [Fact]
    public async Task Lost_successful_refresh_response_cannot_be_retried_or_keep_old_access_alive()
    {
        await using var api = await AuthenticationApiFixture.CreateAsync();
        var password = Guid.NewGuid().ToString("N");
        var access = await api.SignInAsync(await api.PrepareAsync(AccountRole.Agent, password), password);
        var predecessor = api.Cookies["__Host-crm-refresh"];
        using var lost = await api.SendAsync("/api/auth/refresh", HttpMethod.Post, new { }, updateCookies: false);
        Assert.Equal(HttpStatusCode.OK, lost.StatusCode);
        Assert.Equal(predecessor, api.Cookies["__Host-crm-refresh"]); // Delivery never updates this client.
        using var retried = await api.SendAsync("/api/auth/refresh", HttpMethod.Post, new { });
        Assert.Equal(HttpStatusCode.Unauthorized, retried.StatusCode);
        using var denied = await api.SendAsync("/api/auth/me", token: access);
        Assert.Equal(HttpStatusCode.Unauthorized, denied.StatusCode);
        await using var db = api.Sql.OpenContext();
        Assert.Equal(2, await db.RefreshCredentials.CountAsync());
        Assert.NotNull((await db.AuthenticationSessions.SingleAsync()).RevokedAtUtc);
    }

    [Fact]
    public async Task Successful_and_failed_transport_does_not_disclose_credentials_in_logs_or_safe_bodies()
    {
        await using var api = await AuthenticationApiFixture.CreateAsync();
        var password = "  عربي " + Guid.NewGuid().ToString("N") + "  ";
        var user = await api.PrepareAsync(AccountRole.Customer, password);
        var access = await api.SignInAsync(user, password);
        var refreshSecret = api.Cookies["__Host-crm-refresh"];
        var bodies = new List<string>();
        using (var me = await api.SendAsync("/api/auth/me", token: access)) { Assert.Equal(HttpStatusCode.OK, me.StatusCode); bodies.Add(await me.Content.ReadAsStringAsync()); }
        using (var failure = await api.SendAsync("/api/auth/signin", HttpMethod.Post, new { email = user.Email, password = password + "wrong" })) { Assert.Equal(HttpStatusCode.Unauthorized, failure.StatusCode); bodies.Add(await failure.Content.ReadAsStringAsync()); }
        using (var forbidden = await api.SendAsync("/api/agents", token: access)) { Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode); bodies.Add(await forbidden.Content.ReadAsStringAsync()); }
        using (var rotate = await api.SendAsync("/api/auth/refresh", HttpMethod.Post, new { }))
        {
            Assert.Equal(HttpStatusCode.OK, rotate.StatusCode);
            var envelope = await rotate.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(new[] { "accessToken", "expiresAtUtc", "tokenType" }, envelope.EnumerateObject().Select(x => x.Name).Order().ToArray());
            Assert.DoesNotContain(refreshSecret, envelope.GetRawText());
        }
        await using var db = api.Sql.OpenContext();
        var stored = await db.Users.SingleAsync();
        Assert.NotEqual(password, stored.PasswordHash);
        foreach (var value in new[] { password, refreshSecret, access, stored.PasswordHash!, stored.SecurityStamp! })
        {
            Assert.All(bodies, body => Assert.DoesNotContain(value, body));
            Assert.All(api.Logs.Messages, message => Assert.DoesNotContain(value, message));
        }
        Assert.All(await db.RefreshCredentials.ToListAsync(), credential => Assert.Equal(32, credential.TokenHash.Length));
    }
}
