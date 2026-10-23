namespace CafeHub.Application.Pagination
{
    /// <summary>
    /// A shared sort request: the raw field name is matched case-insensitively against the list's
    /// <see cref="SortWhitelist"/> and resolved to its canonical name; the direction is Asc or Desc.
    /// </summary>
    /// <param name="Field">The raw field name as supplied by the caller.</param>
    /// <param name="Direction">The requested direction.</param>
    public sealed record SortRequest(string? Field, SortDirection Direction);
}
