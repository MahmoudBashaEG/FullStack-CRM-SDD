namespace Crm.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 403 by the API. Only for records the caller can already see but may not change.</summary>
public sealed class ForbiddenException(string message) : Exception(message);
