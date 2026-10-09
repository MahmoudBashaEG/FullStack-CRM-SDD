using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Crm.Application.Accounts.Commands;
using Crm.Domain.Accounts;
using Crm.Infrastructure.Identity;
using Crm.IntegrationTests.Fixtures;
using Crm.IntegrationTests.Identity;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
namespace Crm.IntegrationTests.Accounts;

[Collection("SQL")]
public sealed class CustomerSignupTests
{
    private const string Password="  عربي password  ";
    private static object Input(string email="customer@fixture.test",string password=Password)=>new{displayName=" Customer عربي ",email,password};
    [Fact] public async Task Signup_creates_only_active_Customer_without_session_and_requires_explicit_signin()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();await api.ObtainCsrfAsync();
        using var response=await api.SendAsync("/api/auth/signup",HttpMethod.Post,Input());
        Assert.Equal(HttpStatusCode.Created,response.StatusCode);
        var json=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Single(json.EnumerateObject());Assert.Equal("Registration complete. Sign in to continue.",json.GetProperty("message").GetString());
        Assert.False(response.Headers.Contains("Set-Cookie"));Assert.Equal("no-store",response.Headers.CacheControl!.ToString());
        await using var db=api.Sql.OpenContext();var user=Assert.Single(await db.Users.ToListAsync());Assert.NotEqual(Guid.Empty,user.Id);Assert.Equal(AccountRole.Customer,user.AccountRole);Assert.True(user.IsActive);Assert.Equal("Customer عربي",user.DisplayName);Assert.NotEqual(Password,user.PasswordHash);
        Assert.Empty(await db.Agents.ToListAsync());Assert.Empty(await db.AuthenticationSessions.ToListAsync());Assert.Empty(await db.RefreshCredentials.ToListAsync());Assert.Empty(await db.InitialAdminSetup.ToListAsync());
        using var anonymous=await api.SendAsync("/api/auth/me");Assert.Equal(HttpStatusCode.Unauthorized,anonymous.StatusCode);
        var token=await api.SignInAsync(user,Password);using var me=await api.SendAsync("/api/auth/me",token:token);Assert.Equal(HttpStatusCode.OK,me.StatusCode);Assert.Equal("Customer",(await me.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
        using var ticket=await api.SendAsync("/api/ticket-reference-data/categories",token:token);Assert.Equal(HttpStatusCode.Forbidden,ticket.StatusCode);
        Assert.DoesNotContain(api.Logs.Messages,x=>x.Contains(Password,StringComparison.Ordinal)||x.Contains(user.PasswordHash!,StringComparison.Ordinal));
    }
    [Theory] [InlineData(AccountRole.Customer)] [InlineData(AccountRole.Agent)] [InlineData(AccountRole.Admin)]
    public async Task Existing_identity_is_preserved_and_duplicate_error_is_generic(AccountRole role)
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();var existing=await api.PrepareAsync(role,Password);await api.ObtainCsrfAsync();
        await using var db=api.Sql.OpenContext();var before=JsonSerializer.Serialize(await db.Users.AsNoTracking().SingleAsync());
        using var response=await api.SendAsync("/api/auth/signup",HttpMethod.Post,Input(" "+existing.Email!.ToUpperInvariant()+" "));
        Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);var json=await response.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("registration_failed",json.GetProperty("code").GetString());Assert.Equal("Registration could not be completed.",json.GetProperty("title").GetString());Assert.False(json.TryGetProperty("errors",out _));
        Assert.Equal(before,JsonSerializer.Serialize(await db.Users.AsNoTracking().SingleAsync()));Assert.Empty(await db.AuthenticationSessions.ToListAsync());Assert.Empty(await db.Agents.ToListAsync());
    }
    [Fact] public async Task Concurrent_normalized_duplicates_create_exactly_one_account()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();await api.ObtainCsrfAsync();
        var responses=await Task.WhenAll(api.SendAsync("/api/auth/signup",HttpMethod.Post,Input(" RACE@fixture.test ")),api.SendAsync("/api/auth/signup",HttpMethod.Post,Input("race@fixture.test")));
        try{Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.Created);var rejected=Assert.Single(responses,x=>x.StatusCode==HttpStatusCode.BadRequest);Assert.Equal("registration_failed",(await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());}
        finally{foreach(var response in responses)response.Dispose();}
        await using var db=api.Sql.OpenContext();var user=Assert.Single(await db.Users.ToListAsync());Assert.Equal(AccountRole.Customer,user.AccountRole);Assert.Equal("RACE@FIXTURE.TEST",user.NormalizedEmail);Assert.Empty(await db.AuthenticationSessions.ToListAsync());
    }
    [Theory] [InlineData("role","Admin")] [InlineData("role","Agent")] [InlineData("role","Customer")] [InlineData("userId","caller-id")] [InlineData("isActive",false)] [InlineData("actor","caller")]
    public async Task Unknown_authority_fields_are_rejected_without_accounts(string field,object value)
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();await api.ObtainCsrfAsync();var body=new Dictionary<string,object>{{"displayName","Customer"},{"email","customer@fixture.test"},{"password",Password},{field,value}};
        using var response=await api.SendAsync("/api/auth/signup",HttpMethod.Post,body);Assert.Equal(HttpStatusCode.BadRequest,response.StatusCode);
        await using var db=api.Sql.OpenContext();Assert.Empty(await db.Users.ToListAsync());Assert.Empty(await db.Agents.ToListAsync());
    }
    [Theory] [InlineData(14,false)] [InlineData(15,true)] [InlineData(128,true)] [InlineData(129,false)]
    public async Task Password_scalar_boundaries_preserve_spaces_and_Unicode(int length,bool accepted)
    {
        foreach(var password in new[]{new string(' ',length),string.Concat(Enumerable.Repeat("😀",length))})
        {
            await using var api=await AuthenticationApiFixture.CreateAsync();await api.ObtainCsrfAsync();using var response=await api.SendAsync("/api/auth/signup",HttpMethod.Post,Input(password:password));
            Assert.Equal(accepted?HttpStatusCode.Created:HttpStatusCode.BadRequest,response.StatusCode);await using var db=api.Sql.OpenContext();Assert.Equal(accepted?1:0,await db.Users.CountAsync());
            if(accepted){using var services=AdminBootstrapTests.Services(api.Sql.Connection);using var scope=services.CreateScope();Assert.True(await scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>().CheckPasswordAsync(await db.Users.SingleAsync(),password));}
        }
    }
    [Fact] public async Task Csrf_origin_bearer_query_and_safe_format_validation_create_nothing()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();await api.ObtainCsrfAsync();
        using var absent=await api.SendAsync("/api/auth/signup",HttpMethod.Post,Input(),csrf:false);Assert.Equal(HttpStatusCode.Forbidden,absent.StatusCode);
        using var origin=await api.SendAsync("/api/auth/signup",HttpMethod.Post,Input(),origin:"https://untrusted.test");Assert.Equal(HttpStatusCode.Forbidden,origin.StatusCode);
        using var bearer=await api.SendAsync("/api/auth/signup",HttpMethod.Post,Input(),token:"caller-input");Assert.Equal(HttpStatusCode.BadRequest,bearer.StatusCode);
        using var query=await api.SendAsync("/api/auth/signup?password=forbidden",HttpMethod.Post,Input());Assert.Equal(HttpStatusCode.BadRequest,query.StatusCode);
        using var invalid=await api.SendAsync("/api/auth/signup",HttpMethod.Post,new{displayName=" ",email="bad",password="short"});Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);var json=await invalid.Content.ReadFromJsonAsync<JsonElement>();Assert.Equal("validation_failed",json.GetProperty("code").GetString());Assert.Equal(3,json.GetProperty("errors").EnumerateObject().Count());Assert.DoesNotContain("short",json.GetRawText());
        await using var db=api.Sql.OpenContext();Assert.Empty(await db.Users.ToListAsync());
    }
    private sealed class FailAfterSave(bool cancel) : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData data,int result,CancellationToken ct=default)
        {if(cancel)throw new OperationCanceledException();throw new InvalidOperationException("Injected failure after account insert");}
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Failure_after_insert_rolls_back_the_entire_account(bool cancel)
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var services=AdminBootstrapTests.Services(sql.Connection,new FailAfterSave(cancel));using var scope=services.CreateScope();
        await Assert.ThrowsAnyAsync<Exception>(()=>scope.ServiceProvider.GetRequiredService<IMediator>().Send(new RegisterCustomerCommand("Customer","customer@fixture.test",Password)));
        await using var db=sql.OpenContext();Assert.Empty(await db.Users.ToListAsync());Assert.Empty(await db.Agents.ToListAsync());Assert.Empty(await db.AuthenticationSessions.ToListAsync());
        using var clean=AdminBootstrapTests.Services(sql.Connection);using var retry=clean.CreateScope();Assert.True((await retry.ServiceProvider.GetRequiredService<IMediator>().Send(new RegisterCustomerCommand("Customer","customer@fixture.test",Password))).Succeeded);
    }
    [Fact] public async Task Signup_does_not_replace_an_existing_refresh_session()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();var user=await api.PrepareAsync(AccountRole.Admin,Password);var token=await api.SignInAsync(user,Password);var cookie=api.Cookies["__Host-crm-refresh"];
        using var signup=await api.SendAsync("/api/auth/signup",HttpMethod.Post,Input());Assert.Equal(HttpStatusCode.Created,signup.StatusCode);Assert.False(signup.Headers.Contains("Set-Cookie"));Assert.Equal(cookie,api.Cookies["__Host-crm-refresh"]);
        using var me=await api.SendAsync("/api/auth/me",token:token);Assert.Equal(HttpStatusCode.OK,me.StatusCode);Assert.Equal("Admin",(await me.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
        await using var db=api.Sql.OpenContext();Assert.Equal(2,await db.Users.CountAsync());Assert.Single(await db.AuthenticationSessions.ToListAsync());Assert.Empty(await db.Agents.ToListAsync());
    }
    [Fact] public async Task Signup_rate_limit_rejects_excess_attempts_without_accounts()
    {
        await using var api=await AuthenticationApiFixture.CreateAsync();await api.ObtainCsrfAsync();
        for(var i=0;i<5;i++){using var invalid=await api.SendAsync("/api/auth/signup",HttpMethod.Post,new{displayName="",email="bad",password="short"});Assert.Equal(HttpStatusCode.BadRequest,invalid.StatusCode);}
        using var limited=await api.SendAsync("/api/auth/signup",HttpMethod.Post,Input());Assert.Equal(HttpStatusCode.TooManyRequests,limited.StatusCode);Assert.NotNull(limited.Headers.RetryAfter);
        await using var db=api.Sql.OpenContext();Assert.Empty(await db.Users.ToListAsync());
    }
    [Theory] [InlineData(AccountRole.Agent)] [InlineData(AccountRole.Admin)]
    public async Task Registration_port_cannot_create_staff_even_without_HTTP(AccountRole role)
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var services=AdminBootstrapTests.Services(sql.Connection);using var scope=services.CreateScope();
        var store=scope.ServiceProvider.GetRequiredService<Crm.Application.Accounts.Ports.IAccountRegistrationStore>();var result=await store.RegisterAsync(new("Staff","staff@fixture.test",Password,role),default);Assert.Equal("registration_failed",result.Error!.Code);
        await using var db=sql.OpenContext();Assert.Empty(await db.Users.ToListAsync());Assert.Empty(await db.Agents.ToListAsync());
    }
}
