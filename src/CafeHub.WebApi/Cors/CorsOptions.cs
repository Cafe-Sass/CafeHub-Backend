using System.Net;

namespace CafeHub.WebApi.Cors
{
    /// <summary>
    /// Configuration for the configured CORS policy. Only the configured origins are allowed; the default
    /// allows no origin. A wildcard origin is rejected outright. Credentials require at least one allowed
    /// origin.
    /// </summary>
    public sealed class CorsOptions
    {
        public const int DefaultMaxAgeSeconds = 600;
        public const int MinMaxAgeSeconds = 0;
        public const int MaxMaxAgeSeconds = 86400;

        public CorsOptions()
        {
            AllowedOrigins = Array.Empty<string>();
            AllowedMethods = new[] { "GET", "POST", "PUT", "PATCH" };
            AllowedHeaders = new[] { "Authorization", "Content-Type", "Idempotency-Key", "X-Correlation-Id" };
            ExposedHeaders = new[] { "X-Correlation-Id", "Retry-After" };
        }

        public IReadOnlyList<string> AllowedOrigins { get; set; }

        public IReadOnlyList<string> AllowedMethods { get; set; }

        public IReadOnlyList<string> AllowedHeaders { get; set; }

        public IReadOnlyList<string> ExposedHeaders { get; set; }

        public bool AllowCredentials { get; set; }

        public int MaxAgeSeconds { get; set; } = DefaultMaxAgeSeconds;

        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();

            foreach (var origin in AllowedOrigins)
            {
                if (!IsAllowedOrigin(origin))
                {
                    problems.Add($"{CorsConfigurationKeys.AllowedOriginsPath} contains an entry that is not an absolute http or https origin without path, query, or trailing slash.");
                }
            }

            if (AllowedOrigins.Contains("*"))
            {
                problems.Add($"{CorsConfigurationKeys.AllowedOriginsPath}: A wildcard origin is not allowed.");
            }

            if (AllowCredentials && AllowedOrigins.Count == 0)
            {
                problems.Add($"{CorsConfigurationKeys.AllowCredentialsPath}: Credentials require at least one allowed origin.");
            }

            if (MaxAgeSeconds < MinMaxAgeSeconds || MaxAgeSeconds > MaxMaxAgeSeconds)
            {
                problems.Add(
                    $"{CorsConfigurationKeys.MaxAgeSecondsPath} must be between {MinMaxAgeSeconds} and {MaxMaxAgeSeconds}.");
            }

            return problems;
        }

        private static bool IsAllowedOrigin(string origin)
        {
            if (string.IsNullOrWhiteSpace(origin))
            {
                return false;
            }

            if (!Uri.TryCreate(origin, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (uri.Scheme != "http" && uri.Scheme != "https")
            {
                return false;
            }

            var pathAndQuery = uri.PathAndQuery;
            if (pathAndQuery != "/" && pathAndQuery != string.Empty)
            {
                return false;
            }

            if (uri.Fragment.Length > 0)
            {
                return false;
            }

            if (origin.EndsWith("/", StringComparison.Ordinal))
            {
                return false;
            }

            return true;
        }
    }
}
