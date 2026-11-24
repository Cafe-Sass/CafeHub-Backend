namespace CafeHub.Domain.Tenants
{
    public sealed class BranchWifiNetwork
    {
        public const int MaximumSsidLength = 32;
        public const int MaximumPasswordLength = 63;

        private BranchWifiNetwork()
        {
        }

        public Guid BranchId { get; private set; }

        public string Ssid { get; private set; } = string.Empty;

        public WifiSecurity Security { get; private set; }

        public string? Password { get; private set; }

        public DateTimeOffset CreatedAt { get; private set; }

        public DateTimeOffset UpdatedAt { get; private set; }

        public Guid? CreatedBy { get; private set; }

        public Guid? UpdatedBy { get; private set; }

        public static BranchWifiNetwork Create(
            Guid branchId,
            string ssid,
            WifiSecurity security,
            string? password,
            DateTimeOffset now,
            Guid? actorId)
        {
            var network = new BranchWifiNetwork
            {
                BranchId = ValidateBranch(branchId),
                CreatedAt = now,
                CreatedBy = actorId,
            };
            network.Apply(ssid, security, password, now, actorId);
            return network;
        }

        public void Update(
            string ssid,
            WifiSecurity security,
            string? password,
            DateTimeOffset now,
            Guid? actorId) =>
            Apply(ssid, security, password, now, actorId);

        public static bool IsValidSsid(string? ssid)
        {
            var trimmed = ssid?.Trim();
            return !string.IsNullOrEmpty(trimmed)
                && trimmed.Length <= MaximumSsidLength
                && !trimmed.Any(char.IsControl);
        }

        public static bool IsValidPassword(string? password)
        {
            var trimmed = password?.Trim();
            return trimmed is null
                || (trimmed.Length >= 1
                    && trimmed.Length <= MaximumPasswordLength
                    && !trimmed.Any(char.IsControl));
        }

        private void Apply(
            string ssid,
            WifiSecurity security,
            string? password,
            DateTimeOffset now,
            Guid? actorId)
        {
            if (!IsValidSsid(ssid))
            {
                throw new ArgumentException(
                    $"The WiFi network name must be 1 to {MaximumSsidLength} characters without control characters.",
                    nameof(ssid));
            }

            if (!Enum.IsDefined(security))
            {
                throw new ArgumentOutOfRangeException(nameof(security), "The WiFi security type is not defined.");
            }

            if (!IsValidPassword(password))
            {
                throw new ArgumentException(
                    $"The WiFi password must be empty or 1 to {MaximumPasswordLength} characters without control characters.",
                    nameof(password));
            }

            Ssid = ssid.Trim();
            Security = security;
            Password = string.IsNullOrWhiteSpace(password) ? null : password.Trim();
            UpdatedAt = now;
            UpdatedBy = actorId;
        }

        private static Guid ValidateBranch(Guid branchId)
        {
            if (branchId == Guid.Empty)
            {
                throw new ArgumentException("The branch id is required.", nameof(branchId));
            }

            return branchId;
        }
    }
}
