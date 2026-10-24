namespace CafeHub.Infrastructure.Authentication
{
    public static class JwtConfigurationKeys
    {
        public const string SectionName = "Jwt";
        public const string IssuerKey = "Issuer";
        public const string AudienceKey = "Audience";
        public const string SigningKeyKey = "SigningKey";
        public const string AccessTokenLifetimeMinutesKey = "AccessTokenLifetimeMinutes";
        public const string ClockSkewSecondsKey = "ClockSkewSeconds";

        public const string IssuerPath = $"{SectionName}:{IssuerKey}";
        public const string AudiencePath = $"{SectionName}:{AudienceKey}";
        public const string SigningKeyPath = $"{SectionName}:{SigningKeyKey}";
        public const string AccessTokenLifetimeMinutesPath = $"{SectionName}:{AccessTokenLifetimeMinutesKey}";
        public const string ClockSkewSecondsPath = $"{SectionName}:{ClockSkewSecondsKey}";

        public const string SigningKeyEnvironmentVariable = "Jwt__SigningKey";
    }
}
