using CafeHub.Application.Abstractions.Results;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders
{
    public static class OrderPricing
    {
        public static Result<PricedOrder> Evaluate(IReadOnlyList<SubmitOrderLine> lines, OrderCatalog catalog)
        {
            ArgumentNullException.ThrowIfNull(lines);
            ArgumentNullException.ThrowIfNull(catalog);

            var pricedLines = new List<PricedOrderLine>(lines.Count);
            var unavailable = new List<OrderUnavailableItem>();

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var lineNumber = index + 1;
                var product = catalog.FindProduct(line.ProductId);
                if (product is null
                    || product.Status != ProductStatus.Active
                    || !product.IsVisible
                    || !product.IsAvailableInBranch)
                {
                    unavailable.Add(new OrderUnavailableItem(lineNumber, line.ProductId));
                    continue;
                }

                var size = product.FindSize(line.SizeId);
                if (size is null)
                {
                    unavailable.Add(new OrderUnavailableItem(lineNumber, line.ProductId));
                    continue;
                }

                var addons = new List<PricedOrderAddon>(line.AddonIds.Count);
                foreach (var addonId in line.AddonIds)
                {
                    var addon = product.FindAddon(addonId);
                    if (addon is null)
                    {
                        addons = null;
                        break;
                    }

                    addons.Add(new PricedOrderAddon(addon.Id, addon.Name, addon.Price));
                }

                if (addons is null)
                {
                    unavailable.Add(new OrderUnavailableItem(lineNumber, line.ProductId));
                    continue;
                }

                var lineTotal = size.Price * line.Quantity + addons.Sum(addon => addon.Price);
                pricedLines.Add(new PricedOrderLine(
                    lineNumber,
                    product.Id,
                    size.Id,
                    product.StationId,
                    product.Name,
                    size.Name,
                    size.Price,
                    line.Quantity,
                    addons,
                    lineTotal));
            }

            if (unavailable.Count > 0)
            {
                return Result.Failure<PricedOrder>(OrdersErrors.ProductUnavailable(unavailable));
            }

            return Result.Success(new PricedOrder(pricedLines, pricedLines.Sum(line => line.LineTotal)));
        }
    }

    public sealed record PricedOrder(IReadOnlyList<PricedOrderLine> Lines, decimal TotalAmount);

    public sealed record PricedOrderLine(
        int LineNumber,
        Guid ProductId,
        Guid SizeId,
        Guid? StationId,
        string ProductName,
        string SizeName,
        decimal UnitPrice,
        int Quantity,
        IReadOnlyList<PricedOrderAddon> Addons,
        decimal LineTotal);

    public sealed record PricedOrderAddon(Guid ProductAddonId, string Name, decimal Price);
}
