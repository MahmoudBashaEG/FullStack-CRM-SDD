using System.Buffers;
using System.Text;
namespace Crm.Domain.Accounts;

public static class AccountRules
{
    public static bool PasswordAccepted(string? password) => TryScalarLength(password, out var length) && length is >= 15 and <= 128;
    public static bool TryScalarLength(string? value, out int length)
    {
        length = 0; if (value is null) return false;
        var remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            if (Rune.DecodeFromUtf16(remaining, out _, out var consumed) != OperationStatus.Done) return false;
            length++; remaining = remaining[consumed..];
        }
        return true;
    }
    public static bool DisplayNameAccepted(string? name) => !string.IsNullOrWhiteSpace(name) && TryScalarLength(name.Trim(), out var length) && length <= 200;
}
