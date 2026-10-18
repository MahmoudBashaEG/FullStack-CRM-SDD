using System.Globalization;
using System.Text.Json;
using Crm.Application.Common.Exceptions;
using Crm.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Errors;

internal sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    IHostEnvironment environment,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        ProblemDetails problem = exception switch
        {
            ValidationException validation => new ValidationProblemDetails(
                validation.Errors
                    .GroupBy(e => JsonNamingPolicy.CamelCase.ConvertName(e.PropertyName))
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "One or more validation errors occurred.",
            },
            NotFoundException notFound => new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Resource not found.",
                Detail = notFound.Message,
            },
            ForbiddenException forbidden => new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Forbidden.",
                Detail = forbidden.Message,
            },
            PreconditionRequiredException precondition => new ProblemDetails
            {
                Status = StatusCodes.Status428PreconditionRequired,
                Title = "Precondition required.",
                Detail = precondition.Message,
            },
            CommitOutcomeUnknownException unknown => new ProblemDetails // >= 500 is logged with the inner exception below
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Outcome unknown.",
                Detail = unknown.Message,
            },
            DomainException domain => new ProblemDetails
            {
                Status = StatusCodes.Status422UnprocessableEntity,
                Title = "Business rule violation.",
                Detail = domain.Message,
            },
            UnauthorizedException unauthorized => new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Authentication failed.",
                Detail = unauthorized.Message,
            },
            ConflictException conflict => new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Conflict.",
                Detail = conflict.Message,
            },
            AiUnavailableException ai => new ProblemDetails
            {
                Status = ai.Kind switch
                {
                    AiFailureKind.QuotaExceeded => StatusCodes.Status429TooManyRequests,
                    AiFailureKind.Timeout => StatusCodes.Status504GatewayTimeout,
                    AiFailureKind.InvalidResponse => StatusCodes.Status502BadGateway,
                    AiFailureKind.Blocked => StatusCodes.Status422UnprocessableEntity,
                    _ => StatusCodes.Status503ServiceUnavailable, // NotConfigured, InvalidCredentials, ModelUnavailable, ProviderUnavailable
                },
                Title = "AI assistance unavailable.",
                Detail = ai.Message, // fixed, user-safe (AiMessages)
                Extensions = { ["aiError"] = ai.Kind.ToString() },
            },
            _ => new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Detail = environment.IsDevelopment() ? exception.Message : null,
            },
        };

        if (exception is AiUnavailableException { RetryAfter: { } wait })
        {
            var seconds = (int)Math.Ceiling(wait.TotalSeconds);
            httpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
            problem.Extensions["retryAfterSeconds"] = seconds;
        }

        if (problem.Status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }
        else
        {
            logger.LogInformation("Request failed with {StatusCode}: {Title}", problem.Status, problem.Title);
        }

        httpContext.Response.StatusCode = problem.Status!.Value;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problem,
            Exception = exception,
        });
    }
}
