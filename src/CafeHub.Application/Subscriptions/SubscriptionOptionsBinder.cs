using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CafeHub.Application.Subscriptions
{
    public static class SubscriptionOptionsBinder
    {
        public static SubscriptionOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(SubscriptionConfigurationKeys.SectionName);
            var options = new SubscriptionOptions();
            var problems = new List<string>();

            ReadInt(section, SubscriptionConfigurationKeys.GraceDaysKey, value => options.GraceDays = value, problems);
            ReadInt(
                section,
                SubscriptionConfigurationKeys.SuspendedRetentionDaysKey,
                value => options.SuspendedRetentionDays = value,
                problems);
            ReadInt(
                section,
                SubscriptionConfigurationKeys.RenewalRequestLifetimeDaysKey,
                value => options.RenewalRequestLifetimeDays = value,
                problems);

            problems.AddRange(options.Validate());
            if (problems.Count > 0)
            {
                throw new InvalidSubscriptionConfigurationException(problems);
            }

            return options;
        }

        private static void ReadInt(
            IConfigurationSection? section,
            string key,
            Action<int> apply,
            List<string> problems)
        {
            var rawValue = section?[key];
            if (rawValue is null)
            {
                return;
            }

            if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                apply(value);
            }
            else
            {
                problems.Add(
                    $"{SubscriptionConfigurationKeys.SectionName}:{key} is not a valid 32-bit integer.");
            }
        }
    }
}
