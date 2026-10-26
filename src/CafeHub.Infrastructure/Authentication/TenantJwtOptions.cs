namespace CafeHub.Infrastructure.Authentication
{
    public sealed class TenantJwtOptions
    {
        public const int DefaultAccessTokenLifetimeMinutes = 15;

        public string Audience { get; set; } = string.Empty;

        public int AccessTokenLifetimeMinutes { get; set; } = DefaultAccessTokenLifetimeMinutes;

        public IReadOnlyList<string> Validate(string platformAudience)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Audience)) errors.Add("Jwt:Tenant:Audience must not be empty.");
            if (Audience == platformAudience) errors.Add("Jwt:Tenant:Audience must differ from Jwt:Audience.");
            if (AccessTokenLifetimeMinutes is < 1 or > 60) errors.Add("Jwt:Tenant:AccessTokenLifetimeMinutes must be between 1 and 60.");
            return errors;
        }
    }
}
