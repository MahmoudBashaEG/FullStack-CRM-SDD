using System.Text.Json;
using Crm.Api;
using Crm.Api.Authorization;
using Crm.Application;
using Crm.Application.Common;
using Crm.Infrastructure;
using Crm.Infrastructure.Tickets;

var cli = args.Length > 0 && args[0] == "provision-ticket-references";
var builder = WebApplication.CreateBuilder(cli ? [] : args);
builder.Services.AddSingleton(TimeProvider.System);
if (cli)
{
    if (args.Length != 3 || args[1] != "--input") { Console.Error.WriteLine("Invalid provisioning invocation."); return 2; }
    try
    {
        var input = ReferenceProvisioningInput.Parse(await File.ReadAllTextAsync(args[2]));
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
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
builder.Services.AddCrmHttp(builder.Configuration);
var app = builder.Build();
app.UseCrmHttp();
await app.RunAsync();
return 0;

public partial class Program;
