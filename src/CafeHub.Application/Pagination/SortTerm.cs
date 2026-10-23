namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// One resolved sort term: the canonical field name from the list's <see cref="SortWhitelist"/> plus its direction.
    /// A resolved list always ends with the identifier field <c>id</c> as the deterministic tiebreaker.
    /// </summary>
    /// <param name="Field">The canonical field name.</param>
    /// <param name="Direction">The sort direction.</param>
    public sealed record SortTerm(string Field, SortDirection Direction);
}
