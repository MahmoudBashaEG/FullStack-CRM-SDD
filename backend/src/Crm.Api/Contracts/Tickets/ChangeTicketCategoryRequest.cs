using Crm.Domain.Tickets;

namespace Crm.Api.Contracts.Tickets;

// AI-001 D1: category-only edit. ExpectedCategory = the value the caller last saw (409 if it changed).
public sealed record ChangeTicketCategoryRequest(TicketCategory? ExpectedCategory, TicketCategory? Category);
