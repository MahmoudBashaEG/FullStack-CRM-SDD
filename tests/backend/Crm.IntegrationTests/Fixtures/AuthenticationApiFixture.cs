using System.Net.Http.Json;
using System.Text.Json;
using Crm.Domain.Accounts;
using Crm.Infrastructure.Identity;
using Crm.IntegrationTests.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
namespace Crm.IntegrationTests.Fixtures;

public sealed class AuthenticationApiFixture : IAsyncDisposable
{
    private sealed class Api(string connection,LocalAccountFixture signing,string keyDirectory,AuthLogCapture logs) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {builder.UseEnvironment("Testing");builder.ConfigureLogging(logging=>logging.AddProvider(logs));builder.ConfigureAppConfiguration((_,config)=>config.AddInMemoryCollection(signing.Configuration(connection).AsEnumerable()).AddInMemoryCollection(new Dictionary<string,string?> { ["Auth:AllowedOrigins:0"]="https://localhost:4200",["DataProtection:KeyDirectory"]=keyDirectory }));}
    }
    public MigrationStartupFixture Sql {get;}
    public LocalAccountFixture Signing {get;}=new();
    private readonly WebApplicationFactory<Program> api;
    public HttpClient Client {get;}
    private readonly string keyDirectory;
    public Dictionary<string,string> Cookies {get;}=[];
    public AuthLogCapture Logs {get;}=new();
    public string? Csrf {get;private set;}
    private AuthenticationApiFixture(MigrationStartupFixture sql)
    {
        Sql=sql;var root=new DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!File.Exists(Path.Combine(root.FullName,"Crm.slnx")))root=root.Parent;
        keyDirectory=Path.Combine(root!.FullName,".local","auth-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(keyDirectory);
        api=new Api(sql.Connection,Signing,keyDirectory,Logs);Client=api.CreateClient(new(){BaseAddress=new("https://localhost:4200"),HandleCookies=false});
    }
    public static async Task<AuthenticationApiFixture> CreateAsync()=>new(await MigrationStartupFixture.CreateAsync(StartupSchema.Current));
    public async Task<LocalUser> PrepareAsync(AccountRole role,string password,bool active=true)
    {
        using var services=AdminBootstrapTests.Services(Sql.Connection);using var scope=services.CreateScope();var user=IdentityPersistenceTests.User(role);user.IsActive=active;
        if(!(await scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>().CreateAsync(user,password)).Succeeded)throw new InvalidOperationException("Private fixture preparation failed.");return user;
    }
    public async Task<HttpResponseMessage> SendAsync(string path,HttpMethod? method=null,object? body=null,string? token=null,bool csrf=true,string origin="https://localhost:4200",bool updateCookies=true)
    {
        using var request=new HttpRequestMessage(method??HttpMethod.Get,path);if(body is not null)request.Content=JsonContent.Create(body);
        if(token is not null)request.Headers.Authorization=new("Bearer",token);
        request.Headers.Add("Origin",origin);if(csrf&&Csrf is not null)request.Headers.Add("X-CSRF-TOKEN",Csrf);
        if(Cookies.Count>0)request.Headers.Add("Cookie",string.Join("; ",Cookies.Select(x=>$"{x.Key}={x.Value}")));
        var response=await Client.SendAsync(request);
        if(updateCookies&&response.Headers.TryGetValues("Set-Cookie",out var cookies))foreach(var cookie in cookies){var parts=cookie.Split(';')[0].Split('=',2);if(parts[1]=="")Cookies.Remove(parts[0]);else Cookies[parts[0]]=parts[1];}
        return response;
    }
    public async Task ObtainCsrfAsync(){using var response=await SendAsync("/api/auth/csrf");response.EnsureSuccessStatusCode();Csrf=(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestToken").GetString();}
    public async Task<string> SignInAsync(LocalUser user,string password){await ObtainCsrfAsync();using var response=await SendAsync("/api/auth/signin",HttpMethod.Post,new{email=user.Email,password});response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("accessToken").GetString()!;}
    public async ValueTask DisposeAsync(){Client.Dispose();await api.DisposeAsync();Signing.Dispose();await Sql.DisposeAsync();if(Path.GetFileName(keyDirectory).StartsWith("auth-test-",StringComparison.Ordinal)&&Path.GetDirectoryName(keyDirectory)!.EndsWith(".local",StringComparison.Ordinal))Directory.Delete(keyDirectory,true);}
}
