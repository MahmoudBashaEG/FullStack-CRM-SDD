using System.Diagnostics;
using System.Text.Json;
namespace Crm.IntegrationTests.Fixtures;

// Credentials are synthetic, generated in-process and sent only through redirected stdin.
public static class BootstrapCliFixture
{
    public sealed record Result(int ExitCode,string Output,string Error);
    public static async Task<Result> RunAsync(string connection,string? input,string[]? arguments=null,string environment="Production",bool viaRun=false)
    {
        var root=new DirectoryInfo(AppContext.BaseDirectory);while(root is not null&&!File.Exists(Path.Combine(root.FullName,"Crm.slnx")))root=root.Parent;
        var start=new ProcessStartInfo("dotnet") {WorkingDirectory=root!.FullName,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        if(viaRun)
        {
            foreach(var arg in new[]{"run","--no-launch-profile","--project","src/backend/Crm.Api","--"})start.ArgumentList.Add(arg);
        }
        else start.ArgumentList.Add(Path.Combine(root.FullName,"src/backend/Crm.Api/bin/Debug/net10.0/Crm.Api.dll"));
        foreach(var arg in arguments??["bootstrap-initial-admin","--input-stdin"])start.ArgumentList.Add(arg);
        start.Environment["ConnectionStrings__Crm"]=connection;start.Environment["DOTNET_ENVIRONMENT"]=start.Environment["ASPNETCORE_ENVIRONMENT"]=environment;
        start.Environment["Jwt__SigningCertificateThumbprint"]="missing-test-signer";
        using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();
        if(input is not null)await process.StandardInput.WriteAsync(input);process.StandardInput.Close();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(45));
        try{await process.WaitForExitAsync(timeout.Token);}catch{if(!process.HasExited)process.Kill(entireProcessTree:true);throw;}
        return new(process.ExitCode,await output,await error);
    }
    public static string Input(string password,string email="operator@fixture.test")=>JsonSerializer.Serialize(new {displayName="Operator",email,password});
}
