namespace CafeHub.Application.Identity
{
    public sealed class AuthenticatedAdmin
    {
        public required Guid Id { get; init; }

        public required string Email { get; init; }

        public required IReadOnlyCollection<string> Roles { get; init; }
    }
}
