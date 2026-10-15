using System.Diagnostics;
using Mediator;
using Microsoft.Extensions.Logging;

namespace Crm.Application.Common.Behaviors;

public sealed class LoggingBehavior<TMessage, TResponse>(ILogger<LoggingBehavior<TMessage, TResponse>> logger)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public async ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var messageName = typeof(TMessage).Name;
        var start = Stopwatch.GetTimestamp();
        logger.LogDebug("Handling {MessageName}", messageName);

        var response = await next(message, cancellationToken);

        logger.LogInformation(
            "Handled {MessageName} in {ElapsedMs:0.0} ms",
            messageName,
            Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        return response;
    }
}
