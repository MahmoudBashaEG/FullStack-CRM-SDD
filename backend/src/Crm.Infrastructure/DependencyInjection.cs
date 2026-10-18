using Crm.Application.Abstractions.Ai;
using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Ai;
using Crm.Application.Common.Validation;
using Crm.Infrastructure.Ai;
using Crm.Infrastructure.Customers;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Crm.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Options are built lazily (first DbContext resolution), so the API starts without a DB.
        services.AddDbContext<CrmDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString(CrmDbContext.ConnectionStringName);
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    $"Connection string '{CrmDbContext.ConnectionStringName}' is not configured. " +
                    "Set it with 'dotnet user-secrets' (see README, Local SQL Server).");
            }

            options.UseSqlServer(connectionString, sql =>
            {
                sql.MigrationsAssembly(typeof(CrmDbContext).Assembly.FullName);
                sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: null);
            });
        });

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IRefreshSessionRepository, RefreshSessionRepository>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<ICustomerRecordRepository, CustomerRecordRepository>();
        services.AddScoped<ITicketRepository, TicketRepository>();
        services.AddScoped<IAgentDashboardRepository, AgentDashboardRepository>();
        services.AddScoped<IAgentTaskRepository, AgentTaskRepository>();
        services.AddScoped<ITaskReminderRepository, TaskReminderRepository>();
        services.AddScoped<IQuickReplyRepository, QuickReplyRepository>();
        services.AddScoped<ICollaborationRepository, CollaborationRepository>();
        services.AddScoped<ITicketAccessLock, TicketAccessLock>();
        services.AddScoped<ISlaConfigurationRepository, SlaConfigurationRepository>();
        services.AddScoped<IAutomationRuleRepository, AutomationRuleRepository>();
        services.AddScoped<ISlaMonitoringRepository, SlaMonitoringRepository>();
        services.AddScoped<ISlaAlertRepository, SlaAlertRepository>();
        services.AddScoped<IKnowledgeBaseRepository, KnowledgeBaseRepository>();
        services.AddScoped<ITicketKnowledgeLinkRepository, TicketKnowledgeLinkRepository>();
        services.AddScoped<IAiKnowledgeSearch, AiKnowledgeSearch>();
        services.AddScoped<IPortalTicketRepository, PortalTicketRepository>();
        services.AddScoped<IHelpCenterRepository, HelpCenterRepository>();
        services.AddScoped<IReportingRepository, ReportingRepository>();

        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.User.AllowedUserNameCharacters = string.Empty; // UserName == email; allow any email characters
                options.Password.RequiredLength = AccountRules.PasswordMinLength;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;
                options.Password.RequiredUniqueChars = 1;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole<Guid>>()
            .AddEntityFrameworkStores<CrmDbContext>();

        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IdentityBootstrapper>();
        services.AddScoped<CustomerProfileInitializer>();
        services.AddSingleton<ITokenService, JwtTokenService>();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .Validate(o => o.HasValidSigningKey(),
                "Jwt:SigningKey must be Base64 of at least 32 random bytes. Set it with 'dotnet user-secrets' (see README).")
            .Validate(o => !string.IsNullOrWhiteSpace(o.Issuer) && !string.IsNullOrWhiteSpace(o.Audience),
                "Jwt:Issuer and Jwt:Audience are required.")
            .Validate(o => o.AccessTokenLifetime > TimeSpan.Zero && o.RefreshTokenLifetime > o.AccessTokenLifetime,
                "Jwt lifetimes must be positive and RefreshTokenLifetime must exceed AccessTokenLifetime.")
            .Validate(o => o.RefreshTokenReuseGracePeriod >= TimeSpan.Zero && o.RefreshTokenReuseGracePeriod <= TimeSpan.FromMinutes(2),
                "Jwt:RefreshTokenReuseGracePeriod must be between 00:00:00 and 00:02:00.")
            .ValidateOnStart();

        services.AddOptions<BootstrapSuperAdminOptions>()
            .Bind(configuration.GetSection(BootstrapSuperAdminOptions.SectionName));

        // AI-001 — Gemini. Key/model are NOT validated on start: a missing key must not stop the CRM (AI-only 503 at call time).
        services.AddOptions<GeminiOptions>()
            .Bind(configuration.GetSection(GeminiOptions.SectionName))
            .PostConfigure(o =>
            {
                if (string.IsNullOrWhiteSpace(o.ApiKey))
                {
                    o.ApiKey = configuration["GEMINI_API_KEY"]; // documented environment variable alternative
                }
            })
            .Validate(o => o.Timeout >= TimeSpan.FromSeconds(5) && o.Timeout <= TimeSpan.FromMinutes(2), "Gemini:Timeout must be between 00:00:05 and 00:02:00.")
            .Validate(o => o.MaxOutputTokens is >= 256 and <= 8192, "Gemini:MaxOutputTokens must be between 256 and 8192.")
            .Validate(o => o.Temperature is >= 0 and <= 1, "Gemini:Temperature must be between 0 and 1.")
            .Validate(o => o.RetryDelay >= TimeSpan.Zero && o.RetryDelay <= TimeSpan.FromSeconds(10), "Gemini:RetryDelay must be between 00:00:00 and 00:00:10.")
            .Validate(o => o.BaseUrl.IsAbsoluteUri && o.BaseUrl.AbsolutePath.EndsWith('/')
                    && (o.BaseUrl.Scheme == Uri.UriSchemeHttps || o.BaseUrl.IsLoopback),
                "Gemini:BaseUrl must be an absolute https URL ending with '/' (http only for localhost).")
            .ValidateOnStart();

        services.AddOptions<AiLimits>()
            .Bind(configuration.GetSection(AiLimits.SectionName))
            .Validate(o => o.RequestsPerUserPerMinute is >= 1 and <= 60 && o.MaxChatMessages is >= 2 and <= AiRules.ChatMessagesPerRequestMax
                    && o.MaxChatHistoryCharacters is >= 1000 and <= 100_000 && o.MaxTicketContextCharacters is >= 1000 and <= 20_000
                    && o.ActivityEntries is >= 0 and <= 50 && o.MaxKnowledgeSources is >= 0 and <= 5 && o.KnowledgeExcerptCharacters is >= 200 and <= 5000,
                "Ai limits are out of range (see docs/ai-features.md).")
            .ValidateOnStart();
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<AiLimits>>().Value);

        services.AddHttpClient<IAiTextGenerator, GeminiTextGenerator>((sp, client) =>
        {
            var gemini = sp.GetRequiredService<IOptions<GeminiOptions>>().Value;
            client.BaseAddress = gemini.BaseUrl;
            client.Timeout = gemini.Timeout + TimeSpan.FromSeconds(10); // backstop; the per-call CancelAfter(Timeout) fires first
        });

        return services;
    }
}
