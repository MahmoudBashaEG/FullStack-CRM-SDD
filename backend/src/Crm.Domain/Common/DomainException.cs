namespace Crm.Domain.Common;

/// <summary>Raised when a domain rule is violated. Mapped to HTTP 422 by the API.</summary>
public class DomainException(string message) : Exception(message);
