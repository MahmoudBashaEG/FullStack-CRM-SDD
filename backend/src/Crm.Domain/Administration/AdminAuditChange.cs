namespace Crm.Domain.Administration;

/// <summary>One safe field change. Values are display text, truncated; never secrets.</summary>
public sealed record AdminAuditChange(string Field, string? OldValue, string? NewValue)
{
    public const int ValueMaxLength = 200;

    public static AdminAuditChange Of(string field, string? oldValue, string? newValue) =>
        new(field, Truncate(oldValue), Truncate(newValue));

    private static string? Truncate(string? value)
    {
        if (value is not { Length: > ValueMaxLength })
        {
            return value;
        }

        // Never cut a surrogate pair in half (the half would serialize as an invalid character).
        var end = char.IsHighSurrogate(value[ValueMaxLength - 2]) ? ValueMaxLength - 2 : ValueMaxLength - 1;
        return value[..end] + "…";
    }
}
