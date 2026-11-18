namespace CafeHub.Domain.Tenants
{
    public sealed class ProductStationAssignment
    {
        private ProductStationAssignment() { }

        public Guid Id { get; private init; }

        public Guid ProductId { get; private init; }

        public Guid BranchId { get; private init; }

        public Guid StationId { get; private init; }

        public DateTimeOffset StartedAt { get; private init; }

        public Guid? StartedBy { get; private init; }

        public DateTimeOffset? EndedAt { get; private set; }

        public Guid? EndedBy { get; private set; }

        public static ProductStationAssignment Create(
            Guid productId, Guid branchId, Guid stationId, DateTimeOffset now, Guid actorId) =>
            new()
            {
                Id = Guid.CreateVersion7(),
                ProductId = productId,
                BranchId = branchId,
                StationId = stationId,
                StartedAt = now,
                StartedBy = actorId,
            };

        public void End(DateTimeOffset now, Guid actorId)
        {
            if (EndedAt is not null)
            {
                throw new InvalidOperationException("The assignment has already ended.");
            }

            EndedAt = now;
            EndedBy = actorId;
        }
    }
}
