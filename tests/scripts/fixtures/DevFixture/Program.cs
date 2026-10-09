extern alias ApiApp;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Crm.Infrastructure;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Crm.Domain.Accounts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

if(args.FirstOrDefault()=="auth-api")
{
    var fixtureRoot=new DirectoryInfo(AppContext.BaseDirectory);while(fixtureRoot is not null&&!File.Exists(Path.Combine(fixtureRoot.FullName,"Crm.slnx")))fixtureRoot=fixtureRoot.Parent;
    await using var api=new WebApplicationFactory<ApiApp::Program>().WithWebHostBuilder(builder=>builder.UseEnvironment("Testing").UseContentRoot(Path.Combine(fixtureRoot!.FullName,"src/backend/Crm.Api")));
    api.UseKestrel(options=>options.ListenLocalhost(int.Parse(args[1]),listen=>listen.UseHttps()));
    api.StartServer();Console.WriteLine("Owned authentication API ready.");
    _ = await Console.In.ReadLineAsync(); // private control pipe closes host normally, no product endpoint.
    return 0;
}
if(args.FirstOrDefault()=="prepare-local-auth")
{
    var builder=Host.CreateApplicationBuilder();builder.Logging.ClearProviders();builder.Services.AddInfrastructure(builder.Configuration);
    using var host=builder.Build();using var scope=host.Services.CreateScope();
    var db=scope.ServiceProvider.GetRequiredService<CrmDbContext>();
    var settings=new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(CrmDatabaseSettings.Load(builder.Configuration));
    if(!settings.InitialCatalog.StartsWith("CrmTicketsE2E_",StringComparison.Ordinal)||!Guid.TryParseExact(settings.InitialCatalog["CrmTicketsE2E_".Length..],"N",out _) || !Guid.TryParseExact(args[2],"N",out _))throw new InvalidOperationException("Unsafe owned fixture.");
    var owner=await db.Database.SqlQuery<string>($"SELECT CONVERT(nvarchar(128),value) AS [Value] FROM sys.extended_properties WHERE class=0 AND name=N'CrmFixtureOwner'").SingleAsync();
    if(owner!=args[2])throw new InvalidOperationException("Fixture owner mismatch.");
    var identities=new Dictionary<string,object>();var manager=scope.ServiceProvider.GetRequiredService<UserManager<LocalUser>>();
    foreach(var kind in new[]{"admin","agent","customer","disabled"})
    {
        var email=kind+"@owned-fixture.test";var password=Guid.NewGuid().ToString("N");identities[kind]=new{email,password};
        if(kind!="admin")
        {
            var user=new LocalUser{DisplayName=kind,Email=email,UserName=email,AccountRole=kind=="customer"?AccountRole.Customer:AccountRole.Agent,IsActive=kind!="disabled"};
            if(!(await manager.CreateAsync(user,password)).Succeeded)throw new InvalidOperationException("Owned identity preparation failed.");
        }
    }
    using var rsa=RSA.Create(3072);var request=new CertificateRequest("CN=CRM Owned Auth Browser "+args[2],rsa,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
    using var temporary=request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(2));var secret=Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
    using var certificate=X509CertificateLoader.LoadPkcs12(temporary.Export(X509ContentType.Pkcs12,secret),secret,X509KeyStorageFlags.UserKeySet|X509KeyStorageFlags.PersistKeySet);
    using var store=new X509Store(StoreName.My,StoreLocation.CurrentUser);store.Open(OpenFlags.ReadWrite);store.Add(certificate);
    try {await File.WriteAllTextAsync(args[1],JsonSerializer.Serialize(new{owner=args[2],thumbprint=certificate.Thumbprint,identities}));}
    catch {store.Remove(certificate);using var key=certificate.GetRSAPrivateKey();if(key is RSACng cng&&!cng.Key.IsEphemeral)cng.Key.Delete();throw;}
    return 0;
}
if(args.FirstOrDefault()=="cleanup-local-auth")
{
    using var manifest=JsonDocument.Parse(await File.ReadAllTextAsync(args[1]));var owner=manifest.RootElement.GetProperty("owner").GetString();var thumbprint=manifest.RootElement.GetProperty("thumbprint").GetString();
    if(!Guid.TryParseExact(owner,"N",out _)||string.IsNullOrWhiteSpace(thumbprint))throw new InvalidOperationException("Invalid owned certificate metadata.");
    using var store=new X509Store(StoreName.My,StoreLocation.CurrentUser);store.Open(OpenFlags.ReadWrite);
    foreach(var certificate in store.Certificates.Find(X509FindType.FindByThumbprint,thumbprint,false))
    {if(certificate.Subject!="CN=CRM Owned Auth Browser "+owner)throw new InvalidOperationException("Certificate ownership mismatch.");store.Remove(certificate);using var key=certificate.GetRSAPrivateKey();if(key is RSACng cng&&!cng.Key.IsEphemeral)cng.Key.Delete();certificate.Dispose();}
    return 0;
}

if(args.FirstOrDefault() is "protect-state" or "unprotect-state")
{
    if(!OperatingSystem.IsWindows())throw new InvalidOperationException("Windows protection required.");
    var hostBuilder=Host.CreateApplicationBuilder();hostBuilder.Logging.ClearProviders();hostBuilder.Services.AddDataProtection().SetApplicationName("Crm").PersistKeysToFileSystem(new DirectoryInfo(args[1])).ProtectKeysWithDpapi();
    using var host=hostBuilder.Build();var protector=host.Services.GetRequiredService<IDataProtectionProvider>().CreateProtector("Owned restart test");
    var value=await Console.In.ReadToEndAsync();Console.Write(args[0]=="protect-state"?protector.Protect(value):protector.Unprotect(value));return 0;
}

if (args.FirstOrDefault() == "console-launch")
{
    var log = Path.Combine(args[2],".local","console-results-"+Guid.NewGuid().ToString("N")+".txt");
    var start = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden };
    start.Arguments = string.Join(" ", new[]{"console",args[1],args[2],args[3],log}.Select(value => "\""+value.Replace("\"","\\\"")+"\""));
    using var child = Process.Start(start)!; child.WaitForExit();
    if(File.Exists(log)) { Console.Write(File.ReadAllText(log)); File.Delete(log); }
    return child.ExitCode;
}
if (args.FirstOrDefault() == "console")
{
    using var log = new StreamWriter(args[4]) { AutoFlush = true }; Console.SetOut(log); Console.SetError(log);
    return ConsoleTests(args[1], args[2], args[3]);
}
if (args.FirstOrDefault() == "--version") { Console.WriteLine("10.0.400"); return 0; }
if (args.FirstOrDefault() == "dev-certs") { Console.WriteLine("fixture certificate preflight"); return 0; }
if (args.FirstOrDefault() is "wait" or "tree")
{
    bool stopped = false;
    var mode = Environment.GetEnvironmentVariable("DEV_FIXTURE_MODE");
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; if (mode == "cooperative") stopped = true; };
    if (args[0] == "tree")
    {
        var child = Process.Start(Environment.ProcessPath!, "wait")!;
        Console.WriteLine("fixture descendant " + child.Id);
        if (mode == "orphan") return 0;
    }
    while (!stopped) Thread.Sleep(50);
    return 0;
}
if (args.FirstOrDefault() is "signal" or "break")
{
    FreeConsole(); if (!AttachConsole(uint.Parse(args[1]))) return 1;
    SetConsoleCtrlHandler(IntPtr.Zero, true);
    var delivered = GenerateConsoleCtrlEvent(args[0] == "break" ? 1u : 0u, args[0] == "break" ? uint.Parse(args[1]) : 0u); Thread.Sleep(200); FreeConsole(); return delivered ? 0 : 1;
}
if (args.FirstOrDefault() == "issuer")
{
    var certificate = Certificate();
    using var rsa = RSA.Create(2048);
    var key = rsa.ExportParameters(false); var issuer = "https://localhost:7443";
    string Token(bool allowed, string subject = "launcher-staff", int expiry = 300, string audience = "crm-dev-fixture")
    {
        var header = Base64(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", kid = "dev-fixture", typ = "JWT" }));
        var payload = Base64(JsonSerializer.SerializeToUtf8Bytes(new { iss = issuer, aud = audience, sub = subject, name = "Synthetic Staff", crm_support_queue = allowed ? "shared" : "denied", exp = DateTimeOffset.UtcNow.AddSeconds(expiry).ToUnixTimeSeconds() }));
        var message = header + "." + payload;
        return message + "." + Base64(rsa.SignData(System.Text.Encoding.UTF8.GetBytes(message), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }
    File.WriteAllText(args[1], JsonSerializer.Serialize(new { staff = Token(true), denied = Token(false), expired = Token(true, expiry: -600), wrongAudience = Token(true, audience: "wrong") }));
    var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.ConfigureKestrel(k => k.ListenLocalhost(7443, listen => listen.UseHttps(certificate)));
    var app = builder.Build();
    app.MapGet("/.well-known/openid-configuration", () => Results.Json(new { issuer, jwks_uri = issuer + "/jwks" }));
    app.MapGet("/jwks", () => Results.Json(new { keys = new[] { new { kty = "RSA", use = "sig", alg = "RS256", kid = "dev-fixture", n = Base64(key.Modulus!), e = Base64(key.Exponent!) } } }));
    app.MapGet("/health", () => "issuer fixture"); app.Run(); return 0;
}
// Native tool stand-ins, selected only by the isolated test PATH. Product code knows nothing about these flags.
var isApi = args.FirstOrDefault() == "watch";
if (isApi || args.FirstOrDefault()?.EndsWith("npm-cli.js") == true)
{
    Console.WriteLine(isApi ? "FIXTURE API stdout" : "FIXTURE frontend stdout"); Console.Error.WriteLine("FIXTURE useful stderr");
    var mode = Environment.GetEnvironmentVariable("DEV_FIXTURE_CASE") ?? "normal";
    if (isApi && mode == "root-first") { Process.Start(Environment.ProcessPath!, "wait"); return 2; }
    if (isApi && mode == "timeout") { while (true) Thread.Sleep(100); }
    var expected = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") == "Development" && Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development" && Environment.GetEnvironmentVariable("ASPNETCORE_URLS") == "https://localhost:7043";
    if (!expected || isApi && (!args.Contains("--non-interactive") || !args.Contains("--no-launch-profile") || args.Contains("--no-build"))) return 3;
    var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders(); builder.WebHost.ConfigureKestrel(k => k.ListenLocalhost(isApi ? 7043 : 4200, listen => listen.UseHttps(Certificate())));
    var app = builder.Build(); app.MapGet("/api/ticket-reference-data/categories", () => Results.StatusCode(401)); app.MapGet("/", () => Results.Content("<html>fixture</html>","text/html")); app.Run(); return 0;
}
return 2;

static X509Certificate2 Certificate()
{
    using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser); store.Open(OpenFlags.ReadOnly);
    return store.Certificates.Cast<X509Certificate2>().Where(c => c.Subject == "CN=localhost" && c.HasPrivateKey && c.NotAfter > DateTime.Now && c.Extensions.Cast<X509Extension>().Any(e => e.Oid?.Value == "1.3.6.1.4.1.311.84.1.1")).OrderByDescending(c => c.NotBefore).First();
}
static string Base64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');
static int ConsoleTests(string launcher, string repo, string shell)
{
    AllocConsole(); ShowWindow(GetConsoleWindow(), 0);
    Console.CancelKeyPress += (_, e) => e.Cancel = true;
    var temporary = Path.Combine(repo, ".local", "dev-tests-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(temporary);
    var fixtureRepo = Path.Combine(temporary, "repo with spaces"); var bin = Path.Combine(temporary, "tools with spaces");
    Directory.CreateDirectory(bin); Directory.CreateDirectory(Path.Combine(bin,"node_modules/npm/bin")); File.WriteAllText(Path.Combine(bin,"node_modules/npm/bin/npm-cli.js"), "fixture");
    foreach (var file in Directory.GetFiles(AppContext.BaseDirectory)) File.Copy(file, Path.Combine(bin,Path.GetFileName(file)), true);
    File.Copy(Environment.ProcessPath!, Path.Combine(bin,"dotnet.exe")); File.Copy(Environment.ProcessPath!,Path.Combine(bin,"node.exe")); File.WriteAllText(Path.Combine(bin,"npm.cmd"),"@echo fixture");
    Directory.CreateDirectory(Path.Combine(fixtureRepo,"scripts"));
    foreach (var name in new[]{"run-dev.ps1","environment.ps1","DevProcess.cs"}) File.Copy(Path.Combine(repo,"scripts",name),Path.Combine(fixtureRepo,"scripts",name));
    var dependency = Path.Combine(fixtureRepo,"src/frontend/node_modules/@angular/cli/bin/ng.js"); Directory.CreateDirectory(Path.GetDirectoryName(dependency)!); File.WriteAllText(dependency,"fixture");
    Directory.CreateDirectory(Path.Combine(fixtureRepo,".local/dev-certs"));
    File.WriteAllText(Path.Combine(fixtureRepo,".local/dev-certs/localhost.pem"),"fixture path");
    File.WriteAllText(Path.Combine(fixtureRepo,".local/dev-certs/localhost.key"),"fixture path");
    try
    {
        foreach (var test in new[]{"normal","normal","root-first","occupied","test-auth","missing-dependency","missing-certificate","missing-tool","partial","timeout"})
        {
            using var listener = new TcpListener(IPAddress.Loopback,4200);
            if (test == "occupied") listener.Start();
            if (test == "missing-dependency") File.Delete(dependency);
            if (test == "missing-certificate") File.Delete(Path.Combine(fixtureRepo,".local/dev-certs/localhost.pem"));
            if (test == "missing-tool") File.Move(Path.Combine(bin,"dotnet.exe"),Path.Combine(bin,"dotnet-disabled.tmp"));
            var start = new ProcessStartInfo(shell) { WorkingDirectory = temporary, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            start.ArgumentList.Add("-NoProfile"); start.ArgumentList.Add("-File"); start.ArgumentList.Add(Path.Combine(fixtureRepo,"scripts/run-dev.ps1"));
            start.Environment["PATH"] = bin + ";" + Environment.GetEnvironmentVariable("PATH"); start.Environment["DEV_FIXTURE_CASE"] = test;
            if (test == "missing-tool") start.Environment["PATH"]=bin;
            if (test == "test-auth") start.Environment["Authentication__TestIssuer"] = "forbidden";
            if (test == "partial") File.Delete(Path.Combine(bin,"node_modules/npm/bin/npm-cli.js"));
            using var process = Process.Start(start)!;
            var stdout = new System.Text.StringBuilder(); var stderr = new System.Text.StringBuilder();
            process.OutputDataReceived += (_, e) => { lock(stdout) stdout.AppendLine(e.Data); };
            process.ErrorDataReceived += (_, e) => { lock(stderr) stderr.AppendLine(e.Data); }; process.BeginOutputReadLine(); process.BeginErrorReadLine();
            var clock = Stopwatch.StartNew();
            if (test == "normal")
            {
                while (!process.HasExited && clock.Elapsed.TotalSeconds < 30) { lock(stdout) if(stdout.ToString().Contains("Both servers are available")) break; Thread.Sleep(100); }
                lock(stdout) if (!stdout.ToString().Contains("Both servers are available")) throw new Exception("Fixture launcher not ready: " + stdout + stderr);
                using var signal = Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, ArgumentList = { "signal", process.Id.ToString() } })!; signal.WaitForExit(); if(signal.ExitCode != 0) throw new Exception("Ctrl+C delivery failed.");
            }
            if (!process.WaitForExit(test == "timeout" ? 140000 : 15000)) { process.Kill(true); throw new Exception("Launcher did not exit: " + stdout + stderr); }
            process.WaitForExit();
            var output = stdout.ToString();
            if (test == "normal" && (!output.Contains("graceful exit") || !output.Contains("FIXTURE API stdout") || !output.Contains("FIXTURE frontend stdout") || !stderr.ToString().Contains("useful stderr"))) throw new Exception("Missing live logs/graceful exit: " + output + stderr);
            if (test != "normal" && process.ExitCode == 0) throw new Exception("Expected setup/child failure: " + test);
            if (test == "timeout" && (!output.Contains("120 seconds") || clock.Elapsed.TotalSeconds < 120)) throw new Exception("Readiness timeout policy not exercised.");
            Console.WriteLine("PASS console " + test + " " + Math.Round(clock.Elapsed.TotalSeconds,1) + "s");
            listener.Stop(); File.WriteAllText(dependency,"fixture"); File.WriteAllText(Path.Combine(bin,"node_modules/npm/bin/npm-cli.js"),"fixture");
            File.WriteAllText(Path.Combine(fixtureRepo,".local/dev-certs/localhost.pem"),"fixture path");
            if (test == "missing-tool") File.Move(Path.Combine(bin,"dotnet-disabled.tmp"),Path.Combine(bin,"dotnet.exe"));
            foreach(var port in new[]{4200,7043}) { using var probe = new TcpListener(IPAddress.Loopback,port); probe.Start(); probe.Stop(); }
        }
        return 0;
    }
    catch(Exception error) { Console.Error.WriteLine(error.Message); return 1; }
    finally { FreeConsole(); if(Path.GetFullPath(temporary).StartsWith(Path.GetFullPath(Path.Combine(repo,".local"))+Path.DirectorySeparatorChar)) Directory.Delete(temporary,true); }
}
[DllImport("kernel32")] static extern bool AllocConsole();
[DllImport("kernel32")] static extern bool FreeConsole();
[DllImport("kernel32")] static extern bool AttachConsole(uint id);
[DllImport("kernel32")] static extern bool GenerateConsoleCtrlEvent(uint kind,uint group);
[DllImport("kernel32")] static extern bool SetConsoleCtrlHandler(IntPtr handler,bool add);
[DllImport("kernel32")] static extern IntPtr GetConsoleWindow();
[DllImport("user32")] static extern bool ShowWindow(IntPtr window,int command);
