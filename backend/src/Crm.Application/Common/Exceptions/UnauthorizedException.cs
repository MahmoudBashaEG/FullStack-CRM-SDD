namespace Crm.Application.Common.Exceptions;

/// <summary>Mapped to HTTP 401 by the API. Message must never reveal whether an account exists.</summary>
public sealed class UnauthorizedException(string message) : Exception(message);
