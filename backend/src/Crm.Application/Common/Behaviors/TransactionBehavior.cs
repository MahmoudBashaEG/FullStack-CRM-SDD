using Crm.Application.Abstractions.Messaging;
using Crm.Application.Abstractions.Persistence;
using Mediator;
using Microsoft.Extensions.DependencyInjection;

namespace Crm.Application.Common.Behaviors;

/// <summary>
/// The single owner of explicit transactions. Only messages marked ITransactionalCommand
/// are wrapped. IUnitOfWork is resolved lazily so queries never construct a DbContext here.
/// </summary>
public sealed class TransactionBehavior<TMessage, TResponse>(IServiceProvider serviceProvider)
    : IPipelineBehavior<TMessage, TResponse>
    where TMessage : notnull, IMessage
{
    public ValueTask<TResponse> Handle(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        if (message is not ITransactionalCommand)
        {
            return next(message, cancellationToken);
        }

        return HandleInTransactionAsync(message, next, cancellationToken);
    }

    private async ValueTask<TResponse> HandleInTransactionAsync(
        TMessage message,
        MessageHandlerDelegate<TMessage, TResponse> next,
        CancellationToken cancellationToken)
    {
        var unitOfWork = serviceProvider.GetRequiredService<IUnitOfWork>();
        return await unitOfWork.ExecuteInTransactionAsync(
            async ct => await next(message, ct),
            cancellationToken);
    }
}
