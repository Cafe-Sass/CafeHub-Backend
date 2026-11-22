namespace CafeHub.Domain.Tenants
{
    public static class BranchProductAvailabilityReasons
    {
        public const string Manual = "manual";
        public const string OutOfStock = "out_of_stock";
        public const string Expired = "expired";

        public static IReadOnlyList<string> All { get; } =
            Array.AsReadOnly(new[] { Manual, OutOfStock, Expired });

        public static bool IsValid(string? reasonCode) =>
            reasonCode is Manual or OutOfStock or Expired;
    }
}
