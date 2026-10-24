namespace CafeHub.Application.Identity
{
    public sealed class IssuedRefreshToken
    {
        public required string RawToken { get; init; }

        public required DateTimeOffset ExpiresAt { get; init; }

        public required Guid FamilyId { get; init; }
    }
}
