namespace Crm.Api.BackgroundJobs;

public static class BackgroundJobsRegistration
{
    public static IServiceCollection AddBackgroundJobs(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SlaAutomationOptions>()
            .Bind(configuration.GetSection(SlaAutomationOptions.SectionName))
            .Validate(
                o => o.PollInterval >= TimeSpan.FromSeconds(10) && o.PollInterval <= TimeSpan.FromHours(1),
                "SlaAutomation:PollInterval must be between 00:00:10 and 01:00:00.")
            .ValidateOnStart();
        services.AddHostedService<SlaAutomationWorker>();
        return services;
    }
}
