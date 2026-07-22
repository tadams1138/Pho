using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pho.Domain;

/// <summary>A page of results plus the total count for paging controls.</summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);

/// <summary>
/// Records and queries received requests (F5). Recording also prunes entries past the
/// retention window. Queries return newest-first, filterable by method and URL-path substring,
/// and paged.
/// </summary>
public interface IReceivedRequestLog
{
    Task RecordAsync(ReceivedRequest request);

    Task<PagedResult<ReceivedRequest>> QueryAsync(string? method, string? pathContains, int page, int pageSize);

    Task ClearAsync();
}
