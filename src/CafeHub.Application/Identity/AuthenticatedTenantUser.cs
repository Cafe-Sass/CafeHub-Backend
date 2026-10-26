namespace CafeHub.Application.Identity
{
    public sealed class AuthenticatedTenantUser
    {
        public required Guid Id { get; init; }

        public required string Email { get; init; }

        public required string FullName { get; init; }

        public required string UserKind { get; init; }

        public required IReadOnlyCollection<string> Roles { get; init; }
    }
}
