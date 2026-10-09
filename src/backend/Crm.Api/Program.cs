using System.Text.Json;
using Crm.Api;
using Crm.Api.Authorization;
using Crm.Application;
using Crm.Application.Common;
using Crm.Infrastructure;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Tickets;
using Crm.Infrastructure.Authentication;
using Crm.Api.Installation;
using Crm.Application.Accounts.Commands;
using MediatR;
using Crm.Api.Authentication;

var cli = args.Length > 0 && args[0] == "provision-ticket-references";
var bootstrap = args.Length > 0 && args[0] == "bootstrap-initial-admin";
var builder = WebApplication.CreateBuilder(cli || bootstrap ? [] : args);
builder.Services.AddSingleton(TimeProvider.System);
if(bootstrap)
{
    builder.Logging.ClearProviders();
    if(args.Length!=1 && !(args.Length==2 && args[1]=="--input-stdin")) {Console.Error.WriteLine("Invalid bootstrap invocation.");return 2;}
    using var cancellation=new CancellationTokenSource();
    ConsoleCancelEventHandler cancel=(_,signal)=>{signal.Cancel=true;cancellation.Cancel();};
    Console.CancelKeyPress+=cancel;
    try
    {
        // Installation has no HTTP actor: register only its approved command handler.
        builder.Services.AddMediatR(configuration=>
        {
            configuration.RegisterServicesFromAssemblyContaining<BootstrapInitialAdminHandler>();
            configuration.TypeEvaluator=type=>type==typeof(BootstrapInitialAdminHandler);
        });
        builder.Services.AddInfrastructure(builder.Configuration);
        await using var host=builder.Build();await using var scope=host.Services.CreateAsyncScope();
        var result=await scope.ServiceProvider.GetRequiredService<IMediator>().Send(new BootstrapInitialAdminCommand(ct=>AdminBootstrapInput.ReadAsync(args.Length==2,ct)),cancellation.Token);
        if(!result.Succeeded){Console.Error.WriteLine("Invalid bootstrap input or identity conflict.");return 2;}
        Console.WriteLine(JsonSerializer.Serialize(result.Value,new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        return 0;
    }
    catch(Exception error) when(error is JsonException or IOException){Console.Error.WriteLine("Invalid bootstrap input or identity conflict.");return 2;}
    catch(OperationCanceledException){Console.Error.WriteLine("Bootstrap cancelled; setup was not confirmed.");return 3;}
    catch(Exception){Console.Error.WriteLine("Bootstrap database or dependency operation failed; setup was not confirmed.");return 3;}
    finally{Console.CancelKeyPress-=cancel;}
}
if (cli)
{
    // Keep installation-command stdout machine-readable in Development as well as Production.
    builder.Logging.ClearProviders();
    if (args.Length != 3 || args[1] != "--input") { Console.Error.WriteLine("Invalid provisioning invocation."); return 2; }
    try
    {
        var input = ReferenceProvisioningInput.Parse(await File.ReadAllTextAsync(args[2]));
        // CLI provisioning needs persistence only, not HTTP ticket handlers/current actors.
        builder.Services.AddInfrastructure(builder.Configuration);
        await using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<ReferenceProvisioner>().ApplyAsync(input, CancellationToken.None);
        Console.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        return 0;
    }
    catch (Exception error) when (error is JsonException or ProvisioningValidationException or IOException) { Console.Error.WriteLine("Invalid provisioning input."); return 2; }
    catch (Exception) { Console.Error.WriteLine("Provisioning database operation failed."); return 3; }
}
builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentActor, CurrentActor>();
builder.Services.AddQueueAuthentication(builder.Configuration, builder.Environment);
builder.Logging.AddFilter("Microsoft.AspNetCore.Authentication.JwtBearer", LogLevel.None);
builder.Services.AddCrmHttp(builder.Configuration);
builder.Services.AddCrmDataProtection(builder.Configuration);
builder.Services.AddAntiforgery(options=>
{
    options.HeaderName="X-CSRF-TOKEN";options.Cookie.Name="__Host-crm-csrf";options.Cookie.SecurePolicy=CookieSecurePolicy.Always;
    options.Cookie.HttpOnly=true;options.Cookie.SameSite=SameSiteMode.Strict;options.Cookie.Path="/";
});
builder.Services.AddScoped<AuthCsrfProtection>();builder.Services.AddScoped<AuthCookieWriter>();builder.Services.AddAuthRateLimits();
await using var app = builder.Build();
// Fail before HTTP/migration startup if local signing configuration is unusable.
_ = app.Services.GetRequiredService<SigningCertificateProvider>();
if (app.Environment.IsDevelopment())
{
    // ConsoleLifetime registers at RunAsync, so also honour Ctrl+C during awaited startup.
    ConsoleCancelEventHandler cancelStartup = (_, signal) => { signal.Cancel = true; app.Lifetime.StopApplication(); };
    Console.CancelKeyPress += cancelStartup;
    try { await CrmDatabaseMigration.ApplyAsync(app.Services, app.Lifetime.ApplicationStopping); }
    catch (OperationCanceledException) { Console.Error.WriteLine("Development database migration was cancelled; API startup stopped."); return 1; }
    catch (Exception) { Console.Error.WriteLine("Development database migration failed; API startup stopped."); return 1; }
    finally { Console.CancelKeyPress -= cancelStartup; }
}
app.UseCrmHttp();
await app.RunAsync();
return 0;

public partial class Program;
