using Crm.Application.Abstractions.Identity;
using Crm.Application.Abstractions.Persistence;
using Crm.Application.Common.Validation;
using Crm.Infrastructure.Customers;
using Crm.Infrastructure.Identity;
using Crm.Infrastructure.Persistence;
using Crm.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
