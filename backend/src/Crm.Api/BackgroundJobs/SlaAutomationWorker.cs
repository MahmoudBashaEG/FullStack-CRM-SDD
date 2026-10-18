using Crm.Application.Sla.RunSlaAutomation;
using Mediator;
using Microsoft.Extensions.Options;

namespace Crm.Api.BackgroundJobs;

/// <summary>
/// SLA-001 S8: in-process poller. Runs once at startup (catches up deadlines missed while the API was stopped), then every PollInterval.
/// Nothing runs while the host is down. Every failure is logged and the loop continues; the database execution key keeps effects single.
/// </summary>
public sealed class SlaAutomationWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<SlaAutomationOptions> options,
    TimeProvider timeProvider,
    ILogger<SlaAutomationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("SLA automation worker is disabled (SlaAutomation:Enabled = false).");
            return;
        }

        try
        {
            using var timer = new PeriodicTimer(options.Value.PollInterval, timeProvider);
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ISender>().Send(new RunSlaAutomationCommand(), stoppingToken);
            logger.Log(
                result.Processed > 0 || result.Failed > 0 ? LogLevel.Information : LogLevel.Debug,
                "SLA automation pass: {Processed} processed, {Skipped} skipped, {Failed} failed.", result.Processed, result.Skipped, result.Failed);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // shutting down
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "SLA automation pass failed (database unavailable or migrations pending?). Retrying next interval.");
        }
    }
}
