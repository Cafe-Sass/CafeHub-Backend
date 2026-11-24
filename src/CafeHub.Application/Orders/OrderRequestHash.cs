using System.Security.Cryptography;
using System.Text;
using CafeHub.Domain.Tenants;

namespace CafeHub.Application.Orders
{
    public static class OrderRequestHash
    {
        public static string Compute(
            Guid? tableId,
            OrderServiceType serviceType,
            OrderSource source,
            string guestDisplayName,
            IReadOnlyList<SubmitOrderLine> lines)
        {
            ArgumentNullException.ThrowIfNull(guestDisplayName);
            ArgumentNullException.ThrowIfNull(lines);

            var builder = new StringBuilder()
                .Append(tableId?.ToString("N") ?? "wifi")
                .Append('|')
                .Append(serviceType)
                .Append('|')
                .Append(source)
                .Append('|')
                .Append(guestDisplayName.Trim());

            foreach (var line in lines)
            {
                builder
                    .Append('|')
                    .Append(line.WifiPackageId is { } packageId
                        ? $"W{packageId:N}:{line.Quantity}"
                        : $"P{line.ProductId:N}:{line.SizeId:N}:{line.Quantity}:{string.Join(',', line.AddonIds.Select(addonId => addonId.ToString("N")))}");
            }

            var digest = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
            return Convert.ToHexStringLower(digest);
        }
    }
}
