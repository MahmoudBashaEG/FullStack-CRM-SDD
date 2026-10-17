using Crm.Api.Authentication;
using Crm.Api.Errors;
using Crm.Application.Common.Behaviors;
using Crm.Application.Diagnostics.GetSystemStatus;
using Mediator;
using Microsoft.OpenApi;

namespace Crm.Api;

public static class DependencyInjection
{
    public static IServiceCollection AddPresentation(this IServiceCollection services)
    {
        services.AddControllers(); // System.Text.Json web defaults: camelCase properties
        services.AddRouting(options => options.LowercaseUrls = true);

        services.AddProblemDetails(options =>
            options.CustomizeProblemDetails = context =>
                context.ProblemDetails.Extensions["traceId"] = context.HttpContext.TraceIdentifier);
        services.AddExceptionHandler<GlobalExceptionHandler>();

        services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
        {
            document.Info.Title = "Customer Support CRM API";
            document.Info.Description =
                "Business routes: /api/v1/{plural-resource}. Infrastructure: /api/health (unversioned). " +
                "DTOs: *Request / *Response, camelCase JSON. Success: direct DTO with 200/201(+Location)/204. " +
                "Lists: ?page (one-based, default 1)&pageSize (default 20, max 100) -> { items, page, pageSize, totalCount }. " +
                "Errors: application/problem+json (ProblemDetails / ValidationProblemDetails). " +
                "Auth: Bearer JWT from POST /api/v1/auth/login; anonymous only where stated.";
            document.Components ??= new OpenApiComponents();
            document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
            document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token from /api/v1/auth/login or /api/v1/auth/refresh.",
            };
            return Task.CompletedTask;
        }));

        services.AddMediator((MediatorOptions options) =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped; // handlers share the request-scoped DbContext
            options.Assemblies = [typeof(GetSystemStatusQuery)];
            options.PipelineBehaviors =
            [
                typeof(LoggingBehavior<,>),     // outermost
                typeof(ValidationBehavior<,>),
                typeof(TransactionBehavior<,>), // innermost, wraps the handler only
            ];
        });

        services.AddSecurity();

        return services;
    }
}
