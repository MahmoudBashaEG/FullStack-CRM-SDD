namespace Crm.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 404 by the API.</summary>
public sealed class NotFoundException(string message) : Exception(message);
