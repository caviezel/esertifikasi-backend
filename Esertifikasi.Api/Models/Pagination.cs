using Microsoft.EntityFrameworkCore;

namespace Esertifikasi.Api.Models;

public sealed class PagedResult<T> {
  public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();
  public int Page { get; init; }
  public int PageSize { get; init; }
  public int TotalCount { get; init; }
  public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public class PagedQuery {
  private const int MaximumPageSize = 100;
  private int _page = 1;
  private int _pageSize = 20;

  public int Page {
    get => _page;
    set => _page = value < 1 ? 1 : value;
  }

  public int PageSize {
    get => _pageSize;
    set => _pageSize = Math.Clamp(value, 1, MaximumPageSize);
  }

  public string? Search { get; set; }
  public string? SortBy { get; set; }
  public string SortDirection { get; set; } = "asc";
  public bool Descending => SortDirection.Equals("desc", StringComparison.OrdinalIgnoreCase);
}

public static class PaginationExtensions {
  public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
      this IQueryable<T> query,
      PagedQuery paging,
      CancellationToken ct) {
    var totalCount = await query.CountAsync(ct);
    var items = await query
        .Skip((paging.Page - 1) * paging.PageSize)
        .Take(paging.PageSize)
        .ToListAsync(ct);

    return new PagedResult<T> {
      Items = items,
      Page = paging.Page,
      PageSize = paging.PageSize,
      TotalCount = totalCount
    };
  }
}
