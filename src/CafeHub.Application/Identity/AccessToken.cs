namespace CafeHub.Application.Identity
{
    public sealed class AccessToken
    {
        public required string Value { get; init; }

        public required DateTimeOffset ExpiresAt { get; init; }
    }
}
