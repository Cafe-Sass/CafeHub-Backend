using System.Text;

namespace CafeHub.Infrastructure.Authentication
{
    public sealed class JwtOptions
    {
        public const int DefaultAccessTokenLifetimeMinutes = 15;
        public const int DefaultClockSkewSeconds = 30;
        public const int MinAccessTokenLifetimeMinutes = 1;
        public const int MaxAccessTokenLifetimeMinutes = 60;
        public const int MinSigningKeyBytes = 32;

        public string Issuer { get; set; } = string.Empty;

        public string Audience { get; set; } = string.Empty;

        public string SigningKey { get; set; } = string.Empty;

        public int AccessTokenLifetimeMinutes { get; set; } = DefaultAccessTokenLifetimeMinutes;

        public int ClockSkewSeconds { get; set; } = DefaultClockSkewSeconds;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            if (string.IsNullOrWhiteSpace(Issuer))
            {
                problems.Add($"{JwtConfigurationKeys.IssuerPath} must not be empty.");
            }

            if (string.IsNullOrWhiteSpace(Audience))
            {
                problems.Add($"{JwtConfigurationKeys.AudiencePath} must not be empty.");
            }

            if (AccessTokenLifetimeMinutes < MinAccessTokenLifetimeMinutes || AccessTokenLifetimeMinutes > MaxAccessTokenLifetimeMinutes)
            {
                problems.Add(
                    $"{JwtConfigurationKeys.AccessTokenLifetimeMinutesPath} must be between {MinAccessTokenLifetimeMinutes} and {MaxAccessTokenLifetimeMinutes} but was {AccessTokenLifetimeMinutes}.");
            }

            if (Encoding.UTF8.GetByteCount(SigningKey) < MinSigningKeyBytes)
            {
                problems.Add(
                    $"{JwtConfigurationKeys.SigningKeyPath} must be at least {MinSigningKeyBytes} bytes when UTF-8 encoded. Set it from User Secrets locally and from the {JwtConfigurationKeys.SigningKeyEnvironmentVariable} environment variable elsewhere; never from a tracked file.");
            }

            return problems;
        }
    }
}
