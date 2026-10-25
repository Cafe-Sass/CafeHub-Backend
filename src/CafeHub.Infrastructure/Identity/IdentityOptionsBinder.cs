using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Identity
{
    public static class IdentityOptionsBinder
    {
        public static IdentityLockoutOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(IdentityConfigurationKeys.SectionName).GetSection(IdentityConfigurationKeys.LockoutKey);
            var options = new IdentityLockoutOptions();
            var problems = new List<string>();

            BindInt(section, IdentityConfigurationKeys.MaxFailedAccessAttemptsKey, value => options.MaxFailedAccessAttempts = value, problems);
            BindInt(section, IdentityConfigurationKeys.LockoutMinutesKey, value => options.LockoutMinutes = value, problems);
            problems.AddRange(options.Validate());

            if (problems.Count > 0)
            {
                throw new InvalidIdentityConfigurationException(problems);
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
                problems.Add($"{IdentityConfigurationKeys.SectionName}:{IdentityConfigurationKeys.LockoutKey}:{key} is not a valid 32-bit integer.");
                return;
            }

            assign(value);
        }
    }
}
