namespace CafeHub.Domain.Tenants
{
    public sealed class WifiSettings
    {
        private WifiSettings()
        {
        }

        public Guid Id { get; private set; }

        public WifiPolicy Policy { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static WifiSettings Create(Guid id, DateTimeOffset now, Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The WiFi settings id is required.", nameof(id));
            }

            return new WifiSettings
            {
                Id = id,
                Policy = WifiPolicy.Free,
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public void SetPolicy(WifiPolicy policy, DateTimeOffset now, Guid? actorId)
        {
            if (!Enum.IsDefined(policy))
            {
                throw new ArgumentOutOfRangeException(nameof(policy), "The WiFi policy is not defined.");
            }

            Policy = policy;
            UpdatedAt = now;
            UpdatedBy = actorId;
        }
    }
}
