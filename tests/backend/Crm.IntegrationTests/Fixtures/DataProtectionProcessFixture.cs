using System.Diagnostics;
namespace Crm.IntegrationTests.Fixtures;

public static class DataProtectionProcessFixture
{
    public static string Root {get {var dir=new DirectoryInfo(AppContext.BaseDirectory);while(dir is not null&&!File.Exists(Path.Combine(dir.FullName,"Crm.slnx")))dir=dir.Parent;return dir!.FullName;}}
    public static async Task<string> RunAsync(string mode,string directory,string input)
    {
        var start=new ProcessStartInfo("dotnet"){WorkingDirectory=Root,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
        foreach(var arg in new[]{Path.Combine(Root,"tests/scripts/fixtures/DevFixture/bin/Debug/net10.0/DevFixture.dll"),mode,directory})start.ArgumentList.Add(arg);
        using var process=Process.Start(start)!;var output=process.StandardOutput.ReadToEndAsync();var error=process.StandardError.ReadToEndAsync();await process.StandardInput.WriteAsync(input);process.StandardInput.Close();
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(30));try{await process.WaitForExitAsync(timeout.Token);}catch{process.Kill(true);throw;}
        if(process.ExitCode!=0)throw new InvalidOperationException("Owned Data Protection process failed.");_ = await error;return await output;
    }
    public static string CreateDirectory()
    {
        var directory=Path.Combine(Root,".local","dp-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);return directory;
    }
    public static void Cleanup(string directory)
    {if(Path.GetDirectoryName(directory)==Path.Combine(Root,".local")&&Path.GetFileName(directory).StartsWith("dp-test-",StringComparison.Ordinal))Directory.Delete(directory,true);else throw new InvalidOperationException("Unsafe cleanup target.");}
}
