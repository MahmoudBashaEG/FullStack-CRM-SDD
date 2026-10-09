using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Configuration;
namespace Crm.IntegrationTests.Fixtures;

public sealed class AuthenticationProcessFixture : IAsyncDisposable
{
    private readonly Process process;private readonly Task<string> output,error;
    private bool ready;
    public Uri Address {get;}
    private AuthenticationProcessFixture(Process process,int port){this.process=process;output=process.StandardOutput.ReadToEndAsync();error=process.StandardError.ReadToEndAsync();Address=new("https://localhost:"+port);}
    public static async Task<AuthenticationProcessFixture> StartAsync(string connection,LocalAccountFixture signing,string repository)
    {
        using var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();var port=((IPEndPoint)listener.LocalEndpoint).Port;listener.Stop();
        var start=new ProcessStartInfo("dotnet"){WorkingDirectory=DataProtectionProcessFixture.Root,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(var arg in new[]{Path.Combine(DataProtectionProcessFixture.Root,"tests/scripts/fixtures/DevFixture/bin/Debug/net10.0/DevFixture.dll"),"auth-api",port.ToString()})start.ArgumentList.Add(arg);
        foreach(var setting in signing.Configuration(connection).AsEnumerable())if(setting.Value is not null)start.Environment[setting.Key.Replace(":","__")]=setting.Value;
        start.Environment["DOTNET_ENVIRONMENT"]=start.Environment["ASPNETCORE_ENVIRONMENT"]="Testing";start.Environment["Auth__AllowedOrigins__0"]="https://localhost:4200";start.Environment["DataProtection__KeyDirectory"]=repository;
        var fixture=new AuthenticationProcessFixture(Process.Start(start)!,port);try
        {
            using var client=new HttpClient();client.Timeout=TimeSpan.FromSeconds(2);
            var watch=Stopwatch.StartNew();while(watch.Elapsed<TimeSpan.FromSeconds(30))
            {
                if(fixture.process.HasExited)throw new InvalidOperationException("Owned authentication process exited before readiness.");
                try{using var response=await client.GetAsync(new Uri(fixture.Address,"/api/ticket-reference-data/categories"));if(response.StatusCode==HttpStatusCode.Unauthorized){fixture.ready=true;return fixture;}}catch(HttpRequestException){}catch(TaskCanceledException){}
                await Task.Delay(100);
            }
            throw new InvalidOperationException("Owned authentication process did not become ready.");
        }catch{await fixture.DisposeAsync();throw;}
    }
    public async ValueTask DisposeAsync()
    {
        if(!process.HasExited){await process.StandardInput.WriteLineAsync("stop");process.StandardInput.Close();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));try{await process.WaitForExitAsync(timeout.Token);}catch{process.Kill(true);await process.WaitForExitAsync();throw;}}
        _ = await output;_ = await error;var code=process.ExitCode;process.Dispose();if(code!=0&&ready)throw new InvalidOperationException("Owned authentication process did not stop normally.");
    }
}
