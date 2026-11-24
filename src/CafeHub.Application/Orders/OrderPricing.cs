using CafeHub.Application.Abstractions.Results;
using CafeHub.Application.Wifi.DTOs;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders
{
    public static class OrderPricing
    {
        private static readonly IReadOnlyDictionary<Guid, WifiPackageResponse> NoWifiPackages =
            new Dictionary<Guid, WifiPackageResponse>();

        public static Result<PricedOrder> Evaluate(
            IReadOnlyList<SubmitOrderLine> lines,
            OrderCatalog catalog,
            decimal? expectedTotal) =>
            Evaluate(lines, catalog, NoWifiPackages, expectedTotal);

        public static Result<PricedOrder> Evaluate(
            IReadOnlyList<SubmitOrderLine> lines,
            OrderCatalog catalog,
            IReadOnlyDictionary<Guid, WifiPackageResponse> wifiPackages,
            decimal? expectedTotal)
        {
            ArgumentNullException.ThrowIfNull(lines);
            ArgumentNullException.ThrowIfNull(catalog);
            ArgumentNullException.ThrowIfNull(wifiPackages);

            var pricedLines = new List<PricedOrderLine>(lines.Count);
            var unavailable = new List<OrderUnavailableItem>();
            var priceChanges = new List<OrderPriceChange>();
            var packageUnavailable = false;

            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var lineNumber = index + 1;
                if (line.WifiPackageId is { } packageId)
                {
                    if (!wifiPackages.TryGetValue(packageId, out var package)
                        || package.Status != WifiPackageStatus.Active.ToString())
                    {
                        packageUnavailable = true;
                        continue;
                    }

                    pricedLines.Add(new PricedOrderLine(
                        lineNumber,
                        null,
                        null,
                        package.Id,
                        null,
                        package.Name,
                        "WiFi",
                        package.DurationMinutes,
                        package.SpeedMbps,
                        package.Price,
                        line.Quantity,
                        [],
                        package.Price * line.Quantity));
                    continue;
                }

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
                    null,
                    product.StationId,
                    product.Name,
                    size.Name,
                    null,
                    null,
                    size.Price,
                    line.Quantity,
                    addons,
                    lineTotal));

                if (HasChanged(size.Price, addons, line))
                {
                    priceChanges.Add(new OrderPriceChange(
                        lineNumber,
                        product.Id,
                        size.Id,
                        size.Price,
                        addons
                            .Select(addon => new OrderAddonPrice(
                                addon.ProductAddonId, addon.Name, addon.Price))
                            .ToArray(),
                        lineTotal));
                }
            }

            if (packageUnavailable)
            {
                return Result.Failure<PricedOrder>(OrdersErrors.WifiPackageUnavailable());
            }

            if (unavailable.Count > 0)
            {
                return Result.Failure<PricedOrder>(OrdersErrors.ProductUnavailable(unavailable));
            }

            var total = pricedLines.Sum(line => line.LineTotal);
            if (priceChanges.Count > 0)
            {
                return Result.Failure<PricedOrder>(OrdersErrors.PriceChanged(priceChanges, total));
            }

            if (expectedTotal is not null && expectedTotal != total)
            {
                return Result.Failure<PricedOrder>(OrdersErrors.PriceChanged([], total));
            }

            return Result.Success(new PricedOrder(pricedLines, total));
        }

        private static bool HasChanged(
            decimal unitPrice, IReadOnlyList<PricedOrderAddon> addons, SubmitOrderLine line)
        {
            if (unitPrice != line.ExpectedUnitPrice)
            {
                return true;
            }

            for (var index = 0; index < addons.Count; index++)
            {
                if (addons[index].Price != line.ExpectedAddonPrices[index])
                {
                    return true;
                }
            }

            return false;
        }
    }

    public sealed record PricedOrder(IReadOnlyList<PricedOrderLine> Lines, decimal TotalAmount);

    public sealed record PricedOrderLine(
        int LineNumber,
        Guid? ProductId,
        Guid? SizeId,
        Guid? WifiPackageId,
        Guid? StationId,
        string ProductName,
        string SizeName,
        int? WifiDurationMinutes,
        decimal? WifiSpeedMbps,
        decimal UnitPrice,
        int Quantity,
        IReadOnlyList<PricedOrderAddon> Addons,
        decimal LineTotal);

    public sealed record PricedOrderAddon(Guid ProductAddonId, string Name, decimal Price);
}
