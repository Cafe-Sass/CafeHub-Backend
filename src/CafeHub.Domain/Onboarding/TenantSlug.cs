using System.Text.RegularExpressions;

namespace CafeHub.Domain.Onboarding
{
    public sealed class TenantSlug
    {
        public const int MaxLength = 40;

        private static readonly Regex Pattern = new(
            "^[a-z0-9][a-z0-9-]{1,38}[a-z0-9]$",
            RegexOptions.CultureInvariant);

        private static readonly HashSet<string> ReservedWords = new(
            new[]
            {
                "admin", "api", "app", "www", "t", "login", "logout",
                "register", "signup", "onboarding", "static", "assets",
                "health", "status", "docs", "swagger", "openapi",
                "support", "billing", "platform", "system", "root",
                "null", "undefined",
            },
            StringComparer.Ordinal);

        private TenantSlug(string value)
        {
            Value = value;
        }

        public string Value { get; }

        public static TenantSlug Create(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException("The tenant slug is required.", nameof(value));
            }

            if (!Pattern.IsMatch(value))
            {
                throw new ArgumentException(
                    "The tenant slug must be 3 to 40 characters of lowercase letters and digits with optional internal hyphens, and must not start or end with a hyphen or contain consecutive hyphens.",
                    nameof(value));
            }

            if (value.Contains("--"))
            {
                throw new ArgumentException(
                    "The tenant slug must not contain consecutive hyphens.",
                    nameof(value));
            }

            if (ReservedWords.Contains(value))
            {
                throw new ArgumentException(
                    "The tenant slug is reserved and cannot be used.",
                    nameof(value));
            }

            return new TenantSlug(value);
        }

        public override string ToString()
        {
            return Value;
        }
    }
}
