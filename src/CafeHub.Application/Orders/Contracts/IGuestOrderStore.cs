namespace CafeHub.Application.Orders.Contracts
{
    /// <summary>
    /// Public read port for the guest status route. The public reference is the only lookup key; the tenant comes
    /// from the resolved route, so a reference from another cafe is indistinguishable from a missing one. The
    /// projection carries no staff identity and no internal ids other than the one used to apply expiry.
    /// </summary>
    public interface IGuestOrderStore
    {
        Task<GuestOrderEntry?> FindByPublicReferenceAsync(
            string publicReference,
            CancellationToken cancellationToken);
    }
}
