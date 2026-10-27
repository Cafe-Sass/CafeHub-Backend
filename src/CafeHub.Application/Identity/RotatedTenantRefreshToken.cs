namespace CafeHub.Application.Identity
{
    public sealed class RotatedTenantRefreshToken
    {
        public required string RawToken { get; init; }

        public required DateTimeOffset ExpiresAt { get; init; }

        public required Guid UserId { get; init; }
    }
}
