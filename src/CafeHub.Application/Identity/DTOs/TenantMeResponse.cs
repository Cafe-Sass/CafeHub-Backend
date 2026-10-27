namespace CafeHub.Application.Identity.DTOs
{
    public sealed class TenantMeResponse
    {
        public required Guid UserId { get; init; }

        public required string Email { get; init; }

        public required string FullName { get; init; }

        public required string UserKind { get; init; }

        public required IReadOnlyCollection<string> Roles { get; init; }

        public required string TenantSlug { get; init; }
    }
}
