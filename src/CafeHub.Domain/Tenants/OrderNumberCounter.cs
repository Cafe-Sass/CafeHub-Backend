namespace CafeHub.Domain.Tenants
{
    public sealed class OrderNumberCounter
    {
        private OrderNumberCounter()
        {
        }

        public Guid BranchId { get; private set; }

        public DateOnly BusinessDate { get; private set; }

        public int LastNumber { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }
    }
}
