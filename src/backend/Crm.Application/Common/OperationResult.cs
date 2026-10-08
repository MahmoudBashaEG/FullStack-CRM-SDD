namespace Crm.Application.Common;

public sealed record Failure(string Code, int Status, Dictionary<string, string[]>? Errors = null);
public sealed class DependencyUnavailableException : Exception;
public sealed record OperationResult<T>(T? Value, Failure? Error)
{
    public bool Succeeded => Error is null;
    public static OperationResult<T> Success(T value) => new(value, null);
    public static OperationResult<T> Fail(string code, int status, Dictionary<string, string[]>? errors = null) => new(default, new(code, status, errors));
}
