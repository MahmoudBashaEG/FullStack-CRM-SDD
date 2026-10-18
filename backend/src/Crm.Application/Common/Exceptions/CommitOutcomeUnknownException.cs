namespace Crm.Application.Common.Exceptions;

/// <summary>
/// Mapped to HTTP 503 by the API. COMMIT failed or was interrupted, so the transaction may or may not have been applied.
/// Deliberately NOT transient and NOT a DbUpdateException, so the execution strategy never replays the handler after a commit attempt.
/// </summary>
public sealed class CommitOutcomeUnknownException(Exception innerException)
    : Exception("The change may have been saved. Reload before trying again.", innerException);
