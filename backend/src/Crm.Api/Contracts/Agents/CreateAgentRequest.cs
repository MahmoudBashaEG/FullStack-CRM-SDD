namespace Crm.Api.Contracts.Agents;

public sealed record CreateAgentRequest(string FirstName, string LastName, string Email, string? PhoneNumber, string Password);
