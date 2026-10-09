using Crm.IntegrationTests.Fixtures;
using Crm.IntegrationTests.Identity;
using Crm.Application.Accounts.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
namespace Crm.IntegrationTests.Security;

[Collection("SQL")]
public sealed class DataProtectionPersistenceTests
{
    [Fact] public async Task Framework_Windows_protected_keys_survive_two_owned_processes()
    {
        var directory=DataProtectionProcessFixture.CreateDirectory();try
        {
            var value="owned noncredential antiforgery durability probe";var protectedValue=await DataProtectionProcessFixture.RunAsync("protect-state",directory,value);
            var keys=Directory.GetFiles(directory,"key-*.xml");Assert.NotEmpty(keys);
            foreach(var key in keys){var xml=await File.ReadAllTextAsync(key);Assert.Contains("encryptedSecret",xml);Assert.DoesNotContain("<masterKey",xml);}
            Assert.Equal(value,await DataProtectionProcessFixture.RunAsync("unprotect-state",directory,protectedValue));Assert.Equal(keys,Directory.GetFiles(directory,"key-*.xml"));
        }finally{DataProtectionProcessFixture.Cleanup(directory);}
    }
    [Fact] public async Task Real_HTTPS_API_restart_accepts_saved_CSRF_and_refresh_then_revokes_access()
    {
        await using var sql=await MigrationStartupFixture.CreateAsync(StartupSchema.Current);using var signing=new LocalAccountFixture();var directory=DataProtectionProcessFixture.CreateDirectory();
        try
        {
            var password=Guid.NewGuid().ToString("N");using(var provider=AdminBootstrapTests.Services(sql.Connection))using(var scope=provider.CreateScope())Assert.True((await scope.ServiceProvider.GetRequiredService<IMediator>().Send(AdminBootstrapTests.Command(password))).Succeeded);
            var cookies=new Dictionary<string,string>();string? csrf=null;
            async Task<JsonElement> Request(HttpClient client,string path,object? body=null,string? token=null,HttpStatusCode expected=HttpStatusCode.OK)
            {
                using var request=new HttpRequestMessage(body is null?HttpMethod.Get:HttpMethod.Post,path);if(body is not null)request.Content=JsonContent.Create(body);
                request.Headers.Add("Origin","https://localhost:4200");if(csrf is not null)request.Headers.Add("X-CSRF-TOKEN",csrf);if(token is not null)request.Headers.Authorization=new("Bearer",token);
                if(cookies.Count>0)request.Headers.Add("Cookie",string.Join("; ",cookies.Select(x=>$"{x.Key}={x.Value}")));
                using var response=await client.SendAsync(request);Assert.Equal(expected,response.StatusCode);
                if(response.Headers.TryGetValues("Set-Cookie",out var values))foreach(var cookie in values){var part=cookie.Split(';')[0].Split('=',2);if(part[1]=="")cookies.Remove(part[0]);else cookies[part[0]]=part[1];}
                return expected==HttpStatusCode.NoContent?default:await response.Content.ReadFromJsonAsync<JsonElement>();
            }
            string access;
            await using(var first=await AuthenticationProcessFixture.StartAsync(sql.Connection,signing,directory))
            {
                using var client=new HttpClient(new HttpClientHandler {UseCookies=false}){BaseAddress=first.Address};csrf=(await Request(client,"/api/auth/csrf")).GetProperty("requestToken").GetString();
                access=(await Request(client,"/api/auth/signin",new{email="admin@fixture.test",password})).GetProperty("accessToken").GetString()!;await Request(client,"/api/auth/me",token:access);
            }
            var oldCookie=cookies["__Host-crm-refresh"];var keys=Directory.GetFiles(directory,"key-*.xml");Assert.NotEmpty(keys);
            await using(var second=await AuthenticationProcessFixture.StartAsync(sql.Connection,signing,directory))
            {
                using var client=new HttpClient(new HttpClientHandler {UseCookies=false}){BaseAddress=second.Address};
                // Deliberately do not remint csrf: use both pre-restart cookie and request token.
                var envelope=await Request(client,"/api/auth/refresh",new{});Assert.NotEqual(oldCookie,cookies["__Host-crm-refresh"]);var fresh=envelope.GetProperty("accessToken").GetString()!;
                await Request(client,"/api/auth/me",token:fresh);await Request(client,"/api/ticket-reference-data/categories",token:fresh);
                await Request(client,"/api/auth/signout",new{},fresh,HttpStatusCode.NoContent);await Request(client,"/api/auth/me",token:fresh,expected:HttpStatusCode.Unauthorized);
            }
            Assert.Equal(keys,Directory.GetFiles(directory,"key-*.xml"));foreach(var key in keys)Assert.Contains("encryptedSecret",await File.ReadAllTextAsync(key));
        }finally{DataProtectionProcessFixture.Cleanup(directory);}
    }
}
