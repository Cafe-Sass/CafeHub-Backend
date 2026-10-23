namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// Offset paging request for the small administrative lists assigned to offset paging by
    /// <c>architecture/indexing-and-pagination-strategy.md</c> (section 4): tenants, plans, the onboarding review queue,
    /// branches, and staff users. Pages start at 1; a missing <c>pageSize</c> resolves to <see cref="PaginationOptions.DefaultPageSize"/>
    /// through <see cref="PaginationOptions.ResolvePageSize(int?)"/>; the exact total is fetched only when
    /// <see cref="IncludeTotalCount"/> is true (count semantics in section 4.1 of that document).
    /// Filters are typed properties of each list's own query and are validated by that list's own validator;
    /// a list query validator composes <see cref="OffsetPageRequestValidator"/> over this record through SetValidator.
    /// </summary>
    public sealed record OffsetPageRequest
    {
        public int Page { get; init; } = 1;

        public int? PageSize { get; init; }

        public bool IncludeTotalCount { get; init; }
    }
}
