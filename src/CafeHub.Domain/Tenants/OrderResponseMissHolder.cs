namespace CafeHub.Domain.Tenants
{
    public sealed class OrderResponseMissHolder
    {
        private OrderResponseMissHolder()
        {
        }

        public Guid OrderResponseMissId { get; private set; }

        public Guid UserId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public static OrderResponseMissHolder Create(Guid missId, Guid userId, DateTimeOffset createdAt)
        {
            if (missId == Guid.Empty)
            {
                throw new ArgumentException("The miss id is required.", nameof(missId));
            }

            if (userId == Guid.Empty)
            {
                throw new ArgumentException("The holder user id is required.", nameof(userId));
            }

            return new OrderResponseMissHolder
            {
                OrderResponseMissId = missId,
                UserId = userId,
                CreatedAt = createdAt,
            };
        }
    }
}
