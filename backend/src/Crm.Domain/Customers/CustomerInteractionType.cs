namespace Crm.Domain.Customers;

/// <summary>Manually logged contact kinds (C3). Stored as the member name. Channel integrations are a later module.</summary>
public enum CustomerInteractionType
{
    PhoneCall = 0,
    Meeting = 1,
    Other = 2,
}
