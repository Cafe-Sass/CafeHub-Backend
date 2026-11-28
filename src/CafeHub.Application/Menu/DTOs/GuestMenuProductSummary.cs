namespace CafeHub.Application.Menu.DTOs
{
    /// <summary>
    /// One product that is visible and available in a branch, with the price range of its active sizes
    /// (<c>null</c> when it has none). Used by discovery grounding and its keyword-search fallback.
    /// </summary>
    public sealed record GuestMenuProductSummary(
        Guid Id,
        string Name,
        decimal? PriceFrom,
        decimal? PriceTo);
}
