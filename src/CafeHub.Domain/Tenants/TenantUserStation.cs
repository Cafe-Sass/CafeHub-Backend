namespace CafeHub.Domain.Tenants
{
    public sealed class TenantUserStation
    {
        private TenantUserStation()
        {
        }

        public Guid TenantUserId { get; private set; }

        public Guid StationId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public static TenantUserStation Create(
            Guid tenantUserId,
            Guid stationId,
            DateTimeOffset createdAt)
        {
            if (tenantUserId == Guid.Empty)
            {
                throw new ArgumentException("The tenant user id is required.", nameof(tenantUserId));
            }

            if (stationId == Guid.Empty)
            {
                throw new ArgumentException("The station id is required.", nameof(stationId));
            }

            return new TenantUserStation
            {
                TenantUserId = tenantUserId,
                StationId = stationId,
                CreatedAt = createdAt,
            };
        }
    }
}
