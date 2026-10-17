using Crm.Domain.Common;

namespace Crm.Domain.Customers;

/// <summary>
/// CRM customer profile/contact. Separate from the Identity account: UserId is null for
/// contact-only customers and set (by the server only) for registered customers.
/// For a registered customer, Identity owns email/name/phone and this row mirrors them;
/// Company is CRM-only.
/// </summary>
public sealed class Customer
{
    private Customer() { } // EF Core

    public Guid Id { get; private set; }
    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string? PhoneNumber { get; private set; }
    public string? Company { get; private set; }
    public Guid? UserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public bool IsRegistered => UserId is not null;

    /// <summary>Staff-created record. No Identity user, password or login.</summary>
    public static Customer CreateContact(
        Guid id, string firstName, string lastName, string email, string? phoneNumber, string? company, DateTimeOffset now) =>
        new()
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            Company = company,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

    /// <summary>Profile of a registered Customer account (signup or backfill).</summary>
    public static Customer CreateForAccount(
        Guid id, Guid userId, string firstName, string lastName, string email, string? phoneNumber, DateTimeOffset createdAtUtc) =>
        new()
        {
            Id = id,
            UserId = userId,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            CreatedAtUtc = createdAtUtc,
            UpdatedAtUtc = createdAtUtc,
        };

    public void UpdateDetails(string firstName, string lastName, string? phoneNumber, string? company, DateTimeOffset now)
    {
        FirstName = firstName;
        LastName = lastName;
        PhoneNumber = phoneNumber;
        Company = company;
        UpdatedAtUtc = now;
    }

    /// <summary>
    /// Contact-only: replaces the business email (never touches any login).
    /// Registered: the email is the login email (AUTH-001: not editable); an equal value
    /// (ignoring case) is accepted as "unchanged", anything else is a rule violation.
    /// </summary>
    public void ChangeContactEmail(string email, DateTimeOffset now)
    {
        if (IsRegistered)
        {
            if (!string.Equals(email, Email, StringComparison.OrdinalIgnoreCase))
            {
                throw new DomainException("A registered customer's email is their login email and cannot be changed.");
            }

            return;
        }

        if (string.Equals(email, Email, StringComparison.Ordinal))
        {
            return;
        }

        Email = email;
        UpdatedAtUtc = now;
    }
}
