using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Authentication
{
    public static class JwtOptionsBinder
    {
        public static JwtOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(JwtConfigurationKeys.SectionName);
            var options = new JwtOptions();
            var problems = new List<string>();

            BindText(section, JwtConfigurationKeys.IssuerKey, value => options.Issuer = value, problems);
            BindText(section, JwtConfigurationKeys.AudienceKey, value => options.Audience = value, problems);
            BindText(section, JwtConfigurationKeys.SigningKeyKey, value => options.SigningKey = value, problems);
            BindInt(section, JwtConfigurationKeys.AccessTokenLifetimeMinutesKey, value => options.AccessTokenLifetimeMinutes = value, problems);
            BindInt(section, JwtConfigurationKeys.ClockSkewSecondsKey, value => options.ClockSkewSeconds = value, problems);
            problems.AddRange(options.Validate());

            if (problems.Count > 0)
            {
                throw new InvalidJwtConfigurationException(problems);
            }

            return options;
        }

        private static void BindText(IConfiguration? section, string key, Action<string> assign, ICollection<string> problems)
        {
            var rawValue = section?[key];
            if (rawValue is null)
            {
                return;
            }

            assign(rawValue);
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
                problems.Add($"{JwtConfigurationKeys.SectionName}:{key} is not a valid 32-bit integer.");
                return;
            }

            assign(value);
        }
    }
}
