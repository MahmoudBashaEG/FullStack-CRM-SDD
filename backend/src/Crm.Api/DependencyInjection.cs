using Crm.Api.Errors;
using Crm.Application.Common.Behaviors;
using Crm.Application.Diagnostics.GetSystemStatus;
using Mediator;

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
                "Errors: application/problem+json (ProblemDetails / ValidationProblemDetails).";
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

        return services;
    }
}
