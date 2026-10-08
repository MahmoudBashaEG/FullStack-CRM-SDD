using Crm.Application.Common;
namespace Crm.Application.Tickets.Contracts;

public sealed record TicketCategoryDto(Guid Id, string DisplayName, bool IsAvailable);
public sealed record TicketAgentDto(Guid Id, string DisplayName, bool IsEligible);
public sealed record TicketDetailDto(string Reference, string Subject, string Description, TicketCategoryDto Category, string Priority, string Status, TicketAgentDto? AssignedAgent, DateTimeOffset CreatedAtUtc, DateTimeOffset UpdatedAtUtc, string EditVersion);
public sealed record TicketHistoryDto(Guid Id, string Revision, int Sequence, string Kind, string? Field, object? PreviousValue, object? NewValue, Actor Actor, DateTimeOffset OccurredAtUtc);
public sealed record TicketPageDto<T>(IReadOnlyList<T> Items, int Page, int PageSize, bool HasMore);
