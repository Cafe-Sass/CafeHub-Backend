using CafeHub.Application.Pagination;

namespace CafeHub.Application.Menu.Queries
{
    /// <summary>
    /// The products list sort contract: <c>sortOrder</c>, <c>name</c>, and <c>createdAt</c> are requestable, and every
    /// resolved sort ends with the identifier tiebreaker. The default is <c>sortOrder ASC, id ASC</c>, the deterministic
    /// order named by <c>architecture/indexing-and-pagination-strategy.md</c> section 4.1.
    /// </summary>
    public static class ProductListSort
    {
        public const string SortOrderField = "sortOrder";
        public const string NameField = "name";
        public const string CreatedAtField = "createdAt";

        public static SortWhitelist Whitelist { get; } = SortWhitelist.Create(
            new[] { SortOrderField, NameField, CreatedAtField, SortWhitelist.IdentifierField },
            SortOrderField,
            SortDirection.Asc);
    }
}
