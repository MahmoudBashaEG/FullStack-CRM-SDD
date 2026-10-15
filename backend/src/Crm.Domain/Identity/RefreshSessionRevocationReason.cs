namespace Crm.Domain.Identity;

public enum RefreshSessionRevocationReason
{
    Rotated,
    Logout,
    ReuseDetected,
    PasswordChanged,
    PasswordReset,
    AccountDisabled,
    AccountInvalid,
}
