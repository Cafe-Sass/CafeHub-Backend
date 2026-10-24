namespace CafeHub.Application.Identity
{
    public sealed class RotatedRefreshToken
    {
        public required string RawToken { get; init; }

        public required DateTimeOffset ExpiresAt { get; init; }

        public required Guid AdminId { get; init; }
    }
}
