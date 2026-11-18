namespace CafeHub.Domain.Tenants
{
    public sealed class ProductBranchStationSlot
    {
        private ProductBranchStationSlot() { }

        public Guid ProductId { get; private init; }

        public Guid BranchId { get; private init; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public static ProductBranchStationSlot Create(Guid productId, Guid branchId, DateTimeOffset now) =>
            new() { ProductId = productId, BranchId = branchId, UpdatedAt = now };

        public void Touch(DateTimeOffset now) => UpdatedAt = now;
    }
}
