using CafeHub.Application.Abstractions.Results;

namespace CafeHub.Application.Wifi
{
    public static class WifiErrors
    {
        public const string NotFoundCode = "Wifi.NotFound";
        public const string ConcurrencyConflictCode = "Wifi.ConcurrencyConflict";

        public static Error NotFound() =>
            Error.NotFound(NotFoundCode, "The WiFi settings were not found.");

        public static Error ConcurrencyConflict() =>
            Error.Conflict(
                ConcurrencyConflictCode,
                "The WiFi settings were modified by another request. Please reload and try again.");
    }
}
