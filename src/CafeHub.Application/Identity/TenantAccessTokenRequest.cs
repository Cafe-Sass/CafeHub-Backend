namespace CafeHub.Application.Identity
{
    public sealed class TenantAccessTokenRequest
    {
        public required Guid UserId { get; init; }

        public required string Email { get; init; }

        public required IReadOnlyCollection<string> Roles { get; init; }

        public required Guid TenantId { get; init; }
    }
}
