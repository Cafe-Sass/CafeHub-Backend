namespace CafeHub.WebApi.Controllers
{
    public sealed record SubmitGuestOrderLineRequest(
        Guid ProductId,
        Guid SizeId,
        IReadOnlyList<Guid>? AddonIds,
        int Quantity,
        decimal ExpectedUnitPrice,
        IReadOnlyList<decimal>? ExpectedAddonPrices,
        Guid? WifiPackageId = null);
}
