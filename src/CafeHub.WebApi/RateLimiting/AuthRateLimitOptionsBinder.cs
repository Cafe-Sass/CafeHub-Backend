using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CafeHub.WebApi.RateLimiting
{
    /// <summary>
    /// Binds the admin authentication rate-limit settings and fails closed: an unparsable or out-of-range
    /// value aborts startup instead of silently disabling or weakening the limit. Missing settings keep the
    /// documented safe defaults.
    /// </summary>
    public static class AuthRateLimitOptionsBinder
    {
        public static AuthRateLimitOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(AuthRateLimitConfigurationKeys.SectionName);
            var options = new AuthRateLimitOptions();
            var problems = new List<string>();

            BindInt(section, AuthRateLimitConfigurationKeys.PermitLimitKey, value => options.PermitLimit = value, problems);
            BindInt(section, AuthRateLimitConfigurationKeys.WindowSecondsKey, value => options.WindowSeconds = value, problems);
            problems.AddRange(options.Validate());

            if (problems.Count > 0)
            {
                throw new InvalidAuthRateLimitConfigurationException(problems);
            }

            return options;
        }

        private static void BindInt(IConfiguration? section, string key, Action<int> assign, ICollection<string> problems)
        {
            var rawValue = section?[key];
            if (rawValue is null)
            {
                return;
            }

            if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                problems.Add($"{AuthRateLimitConfigurationKeys.SectionName}:{key} is not a valid 32-bit integer.");
                return;
            }

            assign(value);
        }
    }
}
