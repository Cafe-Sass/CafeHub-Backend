namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// Keyset (cursor) paging request for the lists that grow without bound and are append-heavy per
    /// <c>architecture/indexing-and-pagination-strategy.md</c> (section 4): audit lists, payment transactions, and the
    /// operational streams. A null cursor requests the first page; a returned cursor is decoded with
    /// <see cref="CursorCodec"/> against the sort fingerprint of the resolved sort, so a cursor issued for a
    /// different sort is rejected. A missing <c>pageSize</c> resolves to <see cref="PaginationOptions.DefaultPageSize"/>
    /// through <see cref="PaginationOptions.ResolvePageSize(int?)"/>.
    /// Filters are typed properties of each list's own query and are validated by that list's own validator;
    /// a list query validator composes <see cref="KeysetPageRequestValidator"/> over this record through SetValidator.
    /// </summary>
    public sealed record KeysetPageRequest
    {
        public string? Cursor { get; init; }

        public int? PageSize { get; init; }
    }
}
