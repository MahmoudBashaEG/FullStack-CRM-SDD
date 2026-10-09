using System.Threading.RateLimiting;
using Crm.Api.Errors;
namespace Crm.Api.Authentication;

public static class AuthRateLimits
{
    public static IServiceCollection AddAuthRateLimits(this IServiceCollection services)=>services.AddRateLimiter(options=>
    {
        options.AddPolicy("signin",context=>Partition(context,10));
        options.AddPolicy("signup",context=>Partition(context,5));
        options.AddPolicy("refresh",context=>Partition(context,30));
        options.OnRejected=async(context,ct)=>
        {
            var seconds=context.Lease.TryGetMetadata(MetadataName.RetryAfter,out var delay)?Math.Max(1,(int)Math.Ceiling(delay.TotalSeconds)):60;
            context.HttpContext.Response.Headers.RetryAfter=seconds.ToString(System.Globalization.CultureInfo.InvariantCulture);
            await ProblemDetailsMapping.WriteAsync(context.HttpContext,new("rate_limited",429));
        };
    });
    private static RateLimitPartition<string> Partition(HttpContext context,int limit)=>RateLimitPartition.GetFixedWindowLimiter(
        (context.Items.TryGetValue("AuthRefreshSession",out var session)?session+":" : "")+(context.Connection.RemoteIpAddress?.ToString()??"local"),
        _=>new FixedWindowRateLimiterOptions {PermitLimit=limit,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true});
}
