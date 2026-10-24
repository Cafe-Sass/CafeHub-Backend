namespace CafeHub.Application.Identity
{
    public sealed class AccessTokenRequest
    {
        public required Guid AdministratorId { get; init; }

        public required string Email { get; init; }

        public required IReadOnlyCollection<string> Roles { get; init; }
    }
}
