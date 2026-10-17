using FluentValidation;

namespace Crm.Application.Common.Validation;

public static class CustomerRules
{
    public const int CompanyMaxLength = 200;
    public const int SearchMaxLength = 100;

    public static IRuleBuilderOptions<T, string?> OptionalCompany<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(CompanyMaxLength);
}
