namespace Crm.Application.Common.Validation;

public static class AdministrationRules
{
    public const int OrganizationNameMaxLength = 150;
    public const int SupportEmailMaxLength = AccountRules.EmailMaxLength;
    public const int SupportPhoneMaxLength = 40;
    public const int SupportInformationMaxLength = 1000;
    public const int SearchMaxLength = 100;
    public static readonly DateOnly EarliestAuditDate = new(2000, 1, 1);

    /// <summary>Trimmed value, or null when empty (same rule as AccountRules.NormalizeOptional).</summary>
    public static string? Normalize(string? value) => AccountRules.NormalizeOptional(value);
}
