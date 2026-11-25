using System.Globalization;

namespace CafeHub.Application.Inventory.Commands
{
    internal static class InventoryItemVersion
    {
        internal static bool TryParse(string? token, out uint version) =>
            uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }
}
