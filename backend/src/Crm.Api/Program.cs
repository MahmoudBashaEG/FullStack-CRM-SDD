using Crm.Api;
using Crm.Application;
using Crm.Infrastructure;
using Crm.Infrastructure.Customers;
using Crm.Infrastructure.Identity;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddPresentation();

var app = builder.Build();

// Roles + initial SuperAdmin (idempotent). Skipped with a warning while migrations are pending.
await app.Services.BootstrapIdentityAsync();

// One CRM profile per pre-existing Customer account (idempotent). Skipped with a warning while migrations are pending.
await app.Services.InitializeCustomerProfilesAsync();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();              // /openapi/v1.json
    app.MapScalarApiReference().AllowAnonymous();   // /scalar
}
else
{
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();

app.Run();
