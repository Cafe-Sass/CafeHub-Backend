using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Authentication
{
    public static class RefreshTokenOptionsBinder
    {
        public static RefreshTokenOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(RefreshTokenConfigurationKeys.SectionName);
            var options = new RefreshTokenOptions();
            var problems = new List<string>();

            BindInt(section, RefreshTokenConfigurationKeys.LifetimeDaysKey, value => options.LifetimeDays = value, problems);
            problems.AddRange(options.Validate());

            if (problems.Count > 0)
            {
                throw new InvalidRefreshTokenConfigurationException(problems);
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
                problems.Add($"{RefreshTokenConfigurationKeys.SectionName}:{key} is not a valid 32-bit integer.");
                return;
            }

            assign(value);
        }
    }
}
