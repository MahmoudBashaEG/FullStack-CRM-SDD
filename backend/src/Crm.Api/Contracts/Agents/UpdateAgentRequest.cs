namespace Crm.Api.Contracts.Agents;

public sealed record UpdateAgentRequest(string FirstName, string LastName, string? PhoneNumber);
