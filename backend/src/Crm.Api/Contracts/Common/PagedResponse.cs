namespace Crm.Api.Contracts.Common;

/// <summary>List endpoints only. page is one-based.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
