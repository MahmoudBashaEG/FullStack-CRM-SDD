namespace Crm.Application.Common.Paging;

/// <summary>One page of a server-side paginated list. Page is one-based.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class Paging
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPage = 1_000_000; // keeps (page - 1) * pageSize far from int overflow
}
