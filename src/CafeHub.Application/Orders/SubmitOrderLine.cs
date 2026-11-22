namespace CafeHub.Application.Orders
{
    public sealed record SubmitOrderLine(
        Guid ProductId,
        Guid SizeId,
        IReadOnlyList<Guid> AddonIds,
        int Quantity,
        decimal ExpectedUnitPrice,
        IReadOnlyList<decimal> ExpectedAddonPrices);
}
