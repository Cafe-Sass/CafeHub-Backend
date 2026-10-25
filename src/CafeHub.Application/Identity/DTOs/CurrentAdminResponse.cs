namespace CafeHub.Application.Identity.DTOs
{
    public sealed class CurrentAdminResponse
    {
        public required Guid Id { get; init; }

        public required string Email { get; init; }

        public required IReadOnlyCollection<string> Roles { get; init; }
    }
}
