using System.Text.Json.Serialization;

namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// Keyset page response. <see cref="NextCursor"/> is only returned when <see cref="HasNextPage"/> is true, and
    /// then carries the opaque cursor encoded by <see cref="CursorCodec"/> from the last row kept on the page.
    /// <see cref="HasNextPage"/> is derived without a count: the data source fetches <c>pageSize + 1</c> rows and
    /// trims (the documented count-savings contract; no data source is implemented here).
    /// </summary>
    public sealed record KeysetPage<T>
    {
        public required IReadOnlyList<T> Items { get; init; }

        public required int PageSize { get; init; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public required string? NextCursor { get; init; }

        public required bool HasNextPage { get; init; }

        /// <summary>
        /// Builds the page from a <c>pageSize + 1</c> fetch: the fetched set is trimmed to the page size and the
        /// anchor must be present exactly when a next page exists, otherwise the PageSize+1 contract is violated.
        /// </summary>
        public static KeysetPage<T> Create(IReadOnlyList<T> fetchedItems, int pageSize, CursorPage? nextAnchor)
        {
            if (fetchedItems is null)
            {
                throw new ArgumentNullException(nameof(fetchedItems));
            }

            if (pageSize < 1)
            {
                throw new InvalidOperationException("The page size must be at least 1.");
            }

            var hasNextPage = fetchedItems.Count > pageSize;
            if (hasNextPage != (nextAnchor is not null))
            {
                throw new InvalidOperationException(
                    "The cursor anchor must be present exactly when the fetched set exceeds the page size.");
            }

            return new KeysetPage<T>
            {
                Items = [.. fetchedItems.Take(pageSize)],
                PageSize = pageSize,
                NextCursor = nextAnchor is null ? null : CursorCodec.Encode(nextAnchor),
                HasNextPage = hasNextPage,
            };
        }
    }
}
