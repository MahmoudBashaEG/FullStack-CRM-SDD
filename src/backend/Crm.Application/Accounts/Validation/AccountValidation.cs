using System.Net.Mail;
using System.Text;
namespace Crm.Application.Accounts.Validation;

public static class AccountValidation
{
    public static bool TryNormalizeEmail(string? email, out string value, out string normalized)
    {
        value = normalized = "";
        if (string.IsNullOrWhiteSpace(email)) return false;
        var candidate = email.Trim();
        if (candidate.Length > 256 || candidate.Any(char.IsControl) || !MailAddress.TryCreate(candidate, out var address) || address.Address != candidate || !candidate.Contains('@')) return false;
        try { normalized = candidate.Normalize(NormalizationForm.FormC).ToUpperInvariant(); }
        catch (ArgumentException) { return false; }
        if (normalized.Length > 256) return false;
        value = candidate; return true;
    }
}
