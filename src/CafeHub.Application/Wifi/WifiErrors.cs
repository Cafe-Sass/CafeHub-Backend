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

        public const string PackageNotFoundCode = "Wifi.PackageNotFound";
        public const string PackageNameConflictCode = "Wifi.PackageNameConflict";
        public const string PackagesRequirePaidPolicyCode = "Wifi.PackagesRequirePaidPolicy";
        public const string PackageConcurrencyConflictCode = "Wifi.PackageConcurrencyConflict";

        public static Error PackageNotFound() =>
            Error.NotFound(PackageNotFoundCode, "The WiFi package was not found.");

        public static Error PackageNameConflict() =>
            Error.Conflict(
                PackageNameConflictCode,
                "Another active WiFi package already uses this name.");

        public static Error PackagesRequirePaidPolicy() =>
            Error.Conflict(
                PackagesRequirePaidPolicyCode,
                "WiFi packages can be managed only while the cafe policy is Paid.");

        public const string QrConflictCode = "Wifi.QrConflict";

        public static Error QrConflict() =>
            Error.Conflict(QrConflictCode, "The WiFi QR code could not be regenerated. Please retry.");

        public static Error PackageConcurrencyConflict() =>
            Error.Conflict(
                PackageConcurrencyConflictCode,
                "The WiFi package was modified by another request. Please reload and try again.");
    }
}
