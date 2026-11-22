namespace CafeHub.Application.Orders.Contracts
{
    /// <summary>
    /// Read-side persistence port for the missed-response summaries. The branch always comes from the
    /// authenticated caller's scope (the owner passes one), never from an unvalidated client claim.
    /// </summary>
    public interface IMissedResponseStore
    {
        Task<MissedResponseSummaryPage> ListSummaryAsync(
            MissedResponseSummaryRequest request,
            CancellationToken cancellationToken);
    }
}
