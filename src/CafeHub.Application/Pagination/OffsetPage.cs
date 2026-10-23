using System.Text.Json.Serialization;

namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// Offset page response. <see cref="TotalCount"/> is populated only when the request asked for it
    /// (<see cref="OffsetPageRequest.IncludeTotalCount"/>); otherwise it is null and omitted from serialized output.
    /// <see cref="HasNextPage"/> is derived without a count: the data source fetches <c>pageSize + 1</c> rows and
    /// trims (the documented count-savings contract; no data source is implemented here).
    /// </summary>
    public sealed record OffsetPage<T>
    {
        public required IReadOnlyList<T> Items { get; init; }

        public required int Page { get; init; }

        public required int PageSize { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required int? TotalCount { get; init; }

        public required bool HasNextPage { get; init; }

        public static OffsetPage<T> Create(IReadOnlyList<T> fetchedItems, int page, int pageSize, int? totalCount)
        {
            if (fetchedItems is null)
            {
                throw new ArgumentNullException(nameof(fetchedItems));
            }

            if (page < 1)
            {
                throw new InvalidOperationException("The page number must start at 1.");
            }

            if (pageSize < 1)
            {
                throw new InvalidOperationException("The page size must be at least 1.");
            }

            return new OffsetPage<T>
            {
                Items = [.. fetchedItems.Take(pageSize)],
                Page = page,
                PageSize = pageSize,
                TotalCount = totalCount,
                HasNextPage = fetchedItems.Count > pageSize,
            };
        }
    }
}
