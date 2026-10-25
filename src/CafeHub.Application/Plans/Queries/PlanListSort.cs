using CafeHub.Application.Pagination;

namespace CafeHub.Application.Plans.Queries
{
    /// <summary>
    /// The plans catalog sort contract: <c>key</c> and <c>createdAt</c> are requestable, and every resolved sort
    /// ends with the identifier tiebreaker. The default is <c>key ASC, id ASC</c>, the deterministic order named by
    /// <c>architecture/indexing-and-pagination-strategy.md</c> for the plans catalog.
    /// </summary>
    public static class PlanListSort
    {
        public const string KeyField = "key";
        public const string CreatedAtField = "createdAt";

        public static SortWhitelist Whitelist { get; } = SortWhitelist.Create(
            new[] { KeyField, CreatedAtField, SortWhitelist.IdentifierField },
            KeyField,
            SortDirection.Asc);
    }
}
