namespace Crm.Application.Abstractions.Messaging;

/// <summary>
/// Marker for commands that perform more than one SaveChanges or several dependent
/// write steps. TransactionBehavior wraps the whole handler in one explicit transaction.
/// Do NOT apply to queries or to commands that save exactly once.
/// </summary>
public interface ITransactionalCommand;
