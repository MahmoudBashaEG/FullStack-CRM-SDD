using System.Text.Json;
using System.Text.Json.Serialization;
using Crm.Api.Errors;
using Crm.Application.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.Net.Http.Headers;
using Microsoft.EntityFrameworkCore;
using Crm.Application.Authentication.Ports;
using Crm.Api.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;

namespace Crm.Api;

public static class HttpConfiguration
{
    public static IServiceCollection AddCrmHttp(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options=>
        {
            var proxies=configuration.GetSection("ReverseProxy:KnownProxies").Get<string[]>()??[];
            options.ForwardedHeaders=ForwardedHeaders.None;
            if(proxies.Length>0)
            {
                options.KnownProxies.Clear();options.KnownIPNetworks.Clear();options.ForwardLimit=1;
                foreach(var value in proxies){if(!IPAddress.TryParse(value,out var address))throw new InvalidOperationException("Invalid trusted proxy configuration.");options.KnownProxies.Add(address);}
                options.ForwardedHeaders=ForwardedHeaders.XForwardedFor|ForwardedHeaders.XForwardedProto;
            }
        });
        services.AddControllers().AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.PropertyNameCaseInsensitive = false;
            options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
        });
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.SuppressMapClientErrors = true;
            options.InvalidModelStateResponseFactory = context => ProblemDetailsMapping.Result(new("validation_failed", 400,
                context.ModelState.Where(x => x.Value?.Errors.Count > 0).ToDictionary(x => x.Key, _ => new[] { "Invalid or missing value." })), context.HttpContext);
        });
        services.AddCors(options => options.AddDefaultPolicy(policy =>
        {
            var origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
            if (origins.Length > 0) policy.WithOrigins(origins).WithMethods("GET", "POST", "PATCH", "PUT").WithHeaders("Authorization", "Content-Type").WithExposedHeaders("Location");
        }));
        return services;
    }
    public static void UseCrmHttp(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            try { await next(context); }
            catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested) { context.Abort(); }
            catch (SqlException) { await ProblemDetailsMapping.WriteAsync(context, new("service_unavailable", 503)); }
            catch(Exception error) when(error is InvalidOperationException or DbUpdateException && error.InnerException is SqlException {Number:not (2601 or 2627)}) { await ProblemDetailsMapping.WriteAsync(context,new("service_unavailable",503)); }
            catch (DependencyUnavailableException) { await ProblemDetailsMapping.WriteAsync(context, new("service_unavailable", 503)); }
            catch (JsonException) { await ProblemDetailsMapping.WriteAsync(context, new("validation_failed", 400)); }
            catch (Exception) { if (!context.Response.HasStarted) await ProblemDetailsMapping.WriteAsync(context, new("unexpected_error", 500)); else context.Abort(); }
        });
        app.UseStatusCodePages(async status =>
        {
            var context = status.HttpContext;
            if (context.Response.StatusCode is 400 or 415) await ProblemDetailsMapping.WriteAsync(context, new(context.Response.StatusCode == 415 ? "unsupported_media_type" : "validation_failed", context.Response.StatusCode));
        });
        app.UseForwardedHeaders();
        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.Use(async(context,next)=>
        {
            if(context.Request.Path=="/api/auth/refresh"&&!context.Request.Headers.ContainsKey("Authorization"))
            {
                var session=await context.RequestServices.GetRequiredService<IRefreshSessionReader>().ReadLiveSessionAsync(context.Request.Cookies[AuthCookieWriter.RefreshName],context.RequestAborted);
                if(session.HasValue)context.Items["AuthRefreshSession"]=session.Value;
            }
            await next(context);
        });
        app.UseRateLimiter();
        // Authorization runs before parsing/existence decisions.
        app.Use(async (context, next) =>
        {
            if (context.Request.Query.Any(pair => pair.Value.Count != 1))
            { await ProblemDetailsMapping.WriteAsync(context, new("validation_failed", 400)); return; }
            if (context.Request.Method is "POST" or "PUT" or "PATCH")
            {
                if (!MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var contentType) || !contentType.MediaType.ToString().Equals("application/json", StringComparison.OrdinalIgnoreCase))
                { await ProblemDetailsMapping.WriteAsync(context, new("unsupported_media_type", 415)); return; }
                context.Request.EnableBuffering();
                using var json = await JsonDocument.ParseAsync(context.Request.Body, cancellationToken: context.RequestAborted);
                if (json.RootElement.ValueKind != JsonValueKind.Object || HasDuplicate(json.RootElement))
                { await ProblemDetailsMapping.WriteAsync(context, new("validation_failed", 400)); return; }
                context.Request.Body.Position = 0;
            }
            await next(context);
        });
        app.MapControllers();
    }
    private static bool HasDuplicate(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            return value.EnumerateObject().Any(property => !names.Add(property.Name) || HasDuplicate(property.Value));
        }
        return value.ValueKind == JsonValueKind.Array && value.EnumerateArray().Any(HasDuplicate);
    }
}
