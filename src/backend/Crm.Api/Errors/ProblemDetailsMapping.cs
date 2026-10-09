using Crm.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace Crm.Api.Errors;

public static class ProblemDetailsMapping
{
    public static ProblemDetails Create(Failure failure, HttpContext context)
    {
        var problem = new ProblemDetails { Type = "about:blank", Title = failure.Code.Replace('_', ' '), Status = failure.Status };
        if(failure.Code=="invalid_credentials")problem.Title="Email or password is incorrect.";
        problem.Extensions["code"] = failure.Code;
        problem.Extensions["traceId"] = context.TraceIdentifier;
        if (failure.Errors is not null) problem.Extensions["errors"] = failure.Errors;
        return problem;
    }
    public static ObjectResult Result(Failure failure, HttpContext context) => new(Create(failure, context)) { StatusCode = failure.Status, ContentTypes = { "application/problem+json" } };
    public static async Task WriteAsync(HttpContext context, Failure failure)
    {
        context.Response.StatusCode = failure.Status;
        await context.Response.WriteAsJsonAsync(Create(failure, context), options: (System.Text.Json.JsonSerializerOptions?)null, contentType: "application/problem+json", cancellationToken: context.RequestAborted);
    }
}
