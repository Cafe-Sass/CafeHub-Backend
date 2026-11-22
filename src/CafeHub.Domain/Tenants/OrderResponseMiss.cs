namespace CafeHub.Domain.Tenants
{
    public sealed class OrderResponseMiss
    {
        private readonly List<OrderResponseMissHolder> holders = [];

        private OrderResponseMiss()
        {
        }

        public Guid Id { get; private set; }

        public Guid OrderId { get; private set; }

        public Guid BranchId { get; private set; }

        public DateTimeOffset ExpiredAt { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public IReadOnlyList<OrderResponseMissHolder> Holders => holders;

        public static OrderResponseMiss Create(
            Guid id,
            Guid orderId,
            Guid branchId,
            DateTimeOffset expiredAt,
            DateTimeOffset now)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The miss id is required.", nameof(id));
            }

            if (orderId == Guid.Empty)
            {
                throw new ArgumentException("The order id is required.", nameof(orderId));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            return new OrderResponseMiss
            {
                Id = id,
                OrderId = orderId,
                BranchId = branchId,
                ExpiredAt = expiredAt,
                CreatedAt = now,
                UpdatedAt = now,
            };
        }

        public void AddHolder(Guid userId, DateTimeOffset now)
        {
            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The holder user id is required.", nameof(userId));
            }

            holders.Add(OrderResponseMissHolder.Create(Id, userId, now));
        }
    }
}
