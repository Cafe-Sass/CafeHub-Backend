namespace CafeHub.Application.Identity
{
    public sealed class IssuedTenantRefreshToken
    {
        public required string RawToken { get; init; }

        public required DateTimeOffset ExpiresAt { get; init; }

        public required Guid FamilyId { get; init; }
    }
}
