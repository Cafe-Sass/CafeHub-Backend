namespace CafeHub.Domain.Tenants
{
    public sealed class GuestMute
    {
        public const int MinimumReasonLength = 3;
        public const int MaximumReasonLength = 256;
        public const int DefaultExpiryHours = 24;
        public const int MaximumExpiryHours = 168;
        public const int MuteSuggestionThreshold = 3;
        public const int RejectionWindowHours = 24;

        private GuestMute()
        {
        }

        public Guid Id { get; private set; }

        public Guid BranchId { get; private set; }

        public Guid OrderId { get; private set; }

        public string DeviceId { get; private set; } = string.Empty;

        public Guid MutedByUserId { get; private set; }

        public string Reason { get; private set; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; private set; }

        public DateTimeOffset? LiftedAt { get; private set; }

        public Guid? LiftedByUserId { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static GuestMute Create(
            Guid id,
            Guid branchId,
            Guid orderId,
            string deviceId,
            Guid mutedByUserId,
            string reason,
            DateTimeOffset now,
            int? expiryHours,
            Guid? actorId)
        {
            if (id == Guid.Empty)
            {
                throw new ArgumentException("The guest mute id is required.", nameof(id));
            }

            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            if (orderId == Guid.Empty)
            {
                throw new ArgumentException("The order id is required.", nameof(orderId));
            }

            if (!Order.IsValidGuestDeviceId(deviceId))
            {
                throw new ArgumentException(
                    "The guest device id must be 16 to 64 letters, digits, hyphens, or underscores.",
                    nameof(deviceId));
            }

            if (mutedByUserId == Guid.Empty)
            {
                throw new ArgumentException("The muting user id is required.", nameof(mutedByUserId));
            }

            if (!IsValidReason(reason))
            {
                throw new ArgumentException(
                    $"The mute reason must be {MinimumReasonLength} to {MaximumReasonLength} characters after trimming, with no control characters.",
                    nameof(reason));
            }

            if (expiryHours is < 1 or > MaximumExpiryHours)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(expiryHours),
                    $"The mute expiry must be 1 to {MaximumExpiryHours} hours.");
            }

            return new GuestMute
            {
                Id = id,
                BranchId = branchId,
                OrderId = orderId,
                DeviceId = deviceId.Trim(),
                MutedByUserId = mutedByUserId,
                Reason = reason.Trim(),
                ExpiresAt = ResolveExpiry(now, expiryHours),
                CreatedAt = now,
                UpdatedAt = now,
                CreatedBy = actorId,
                UpdatedBy = actorId,
            };
        }

        public bool IsActive(DateTimeOffset now) => LiftedAt is null && ExpiresAt > now;

        public bool Lift(Guid liftedByUserId, DateTimeOffset now)
        {
            if (liftedByUserId == Guid.Empty)
            {
                throw new ArgumentException("The lifting user id is required.", nameof(liftedByUserId));
            }

            if (!IsActive(now))
            {
                return false;
            }

            LiftedAt = now;
            LiftedByUserId = liftedByUserId;
            UpdatedAt = now;
            UpdatedBy = liftedByUserId;
            return true;
        }

        public static bool IsValidReason(string? reason)
        {
            var trimmed = reason?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length >= MinimumReasonLength
                && trimmed.Length <= MaximumReasonLength
                && !trimmed.Any(char.IsControl);
        }

        public static DateTimeOffset ResolveExpiry(DateTimeOffset now, int? expiryHours) =>
            now.AddHours(expiryHours ?? DefaultExpiryHours);

        public static bool ShouldSuggestMute(int rejectedOrderCount) =>
            rejectedOrderCount >= MuteSuggestionThreshold;
    }
}
