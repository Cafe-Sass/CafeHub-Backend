namespace CafeHub.Infrastructure.Identity
{
    public sealed class PlatformSeedOptions
    {
        public string? SuperAdminEmail { get; init; }

        public string? SuperAdminPassword { get; init; }

        public bool IsConfigured => !string.IsNullOrWhiteSpace(SuperAdminEmail);
    }
}
