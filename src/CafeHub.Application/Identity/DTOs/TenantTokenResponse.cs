namespace CafeHub.Application.Identity.DTOs
{
    public sealed class TenantTokenResponse
    {
        public required string AccessToken { get; init; }

        public required DateTimeOffset AccessTokenExpiresAt { get; init; }

        public required string RefreshToken { get; init; }

        public required DateTimeOffset RefreshTokenExpiresAt { get; init; }
    }
}
