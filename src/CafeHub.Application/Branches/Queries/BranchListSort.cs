using CafeHub.Application.Pagination;

namespace CafeHub.Application.Branches.Queries
{
    /// <summary>
    /// The branches list sort contract: <c>name</c> and <c>createdAt</c> are requestable, and every resolved sort
    /// ends with the identifier tiebreaker. The default is <c>name ASC, id ASC</c>, the deterministic order named by
    /// <c>architecture/indexing-and-pagination-strategy.md</c> for the branches list.
    /// </summary>
    public static class BranchListSort
    {
        public const string NameField = "name";
        public const string CreatedAtField = "createdAt";

        public static SortWhitelist Whitelist { get; } = SortWhitelist.Create(
            new[] { NameField, CreatedAtField, SortWhitelist.IdentifierField },
            NameField,
            SortDirection.Asc);
    }
}
