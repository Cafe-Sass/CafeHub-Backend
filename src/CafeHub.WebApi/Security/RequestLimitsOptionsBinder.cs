using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CafeHub.WebApi.Security
{
    /// <summary>
    /// Binds the request-limit settings and fails closed: an out-of-range value aborts startup instead of
    /// silently disabling or weakening the limit. Missing settings keep the documented safe defaults, and
    /// messages never echo configured values.
    /// </summary>
    public static class RequestLimitsOptionsBinder
    {
        public static RequestLimitsOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(RequestLimitsConfigurationKeys.SectionName);
            var options = new RequestLimitsOptions();
            var problems = new List<string>();

            BindInt(section, RequestLimitsConfigurationKeys.MaxBodyBytesKey, value => options.MaxBodyBytes = value, problems);
            BindInt(section, RequestLimitsConfigurationKeys.MaxHeadersTotalBytesKey, value => options.MaxHeadersTotalBytes = value, problems);
            problems.AddRange(options.Validate());

            if (problems.Count > 0)
            {
                throw new InvalidRequestLimitsConfigurationException(problems);
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
                problems.Add($"{RequestLimitsConfigurationKeys.SectionName}:{key} is not a valid 32-bit integer.");
                return;
            }

            assign(value);
        }
    }
}
