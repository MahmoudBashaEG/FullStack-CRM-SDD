using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Crm.Application.Ai;
using Crm.Infrastructure.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Crm.Api.Authentication;

/// <summary>AI-001 D4: per-user fixed window (JWT sub) for the AI endpoints only. Single API instance — in-memory, no distributed limiter.</summary>
internal sealed class AiRateLimitPolicy(IOptions<AiLimits> limits) : IRateLimiterPolicy<string>
{
    public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.User.FindFirstValue(CrmClaimTypes.Subject) ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = limits.Value.RequestsPerUserPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
            });

    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, _) =>
    {
        var seconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? (int)Math.Ceiling(retryAfter.TotalSeconds) : 60;
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.HttpContext.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "AI request limit reached.",
            Detail = $"You can make {limits.Value.RequestsPerUserPerMinute} AI requests per minute. Wait about {seconds} seconds and try again.",
        };
        problem.Extensions["aiError"] = "UsageLimit";
        problem.Extensions["retryAfterSeconds"] = seconds;
        await context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>()
            .WriteAsync(new ProblemDetailsContext { HttpContext = context.HttpContext, ProblemDetails = problem });
    };
}
