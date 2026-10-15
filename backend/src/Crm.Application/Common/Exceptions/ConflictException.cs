namespace Crm.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 409 by the API.</summary>
public sealed class ConflictException(string message) : Exception(message);
