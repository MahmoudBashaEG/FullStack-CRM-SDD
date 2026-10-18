namespace Crm.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 428 by the API. A mutation of a versioned resource was sent without If-Match.</summary>
public sealed class PreconditionRequiredException(string message) : Exception(message);
