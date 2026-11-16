using System.Globalization;

namespace CafeHub.Application.Menu.Commands
{
    internal static class ProductVersion
    {
        internal static bool TryParse(string? token, out uint version) =>
            uint.TryParse(token, NumberStyles.None, CultureInfo.InvariantCulture, out version);
    }
}
