using FluentValidation;

namespace Crm.Application.Common.Validation;

public static class AccountRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 256;
    public const int PhoneMaxLength = 32;

    /// <summary>Min 8, max 128, no composition rules (intake section 4).</summary>
    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MinimumLength(PasswordMinLength).MaximumLength(PasswordMaxLength);

    public static IRuleBuilderOptions<T, string> PersonName<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(NameMaxLength);

    public static IRuleBuilderOptions<T, string> AccountEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().EmailAddress().MaximumLength(EmailMaxLength);

    public static IRuleBuilderOptions<T, string?> OptionalPhone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(PhoneMaxLength);

    /// <summary>Trimmed value, or null when empty.</summary>
    public static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
