using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CafeHub.WebApi.Cors
{
    /// <summary>
    /// Binds the CORS settings and fails closed. Missing settings keep the documented safe defaults, and
    /// messages never echo configured values.
    /// </summary>
    public static class CorsOptionsBinder
    {
        public static CorsOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(CorsConfigurationKeys.SectionName);
            var options = new CorsOptions();
            var problems = new List<string>();

            BindStringList(section, CorsConfigurationKeys.AllowedOriginsKey, value => options.AllowedOrigins = value, problems);
            BindStringList(section, CorsConfigurationKeys.AllowedMethodsKey, value => options.AllowedMethods = value, problems);
            BindStringList(section, CorsConfigurationKeys.AllowedHeadersKey, value => options.AllowedHeaders = value, problems);
            BindStringList(section, CorsConfigurationKeys.ExposedHeadersKey, value => options.ExposedHeaders = value, problems);

            var allowCredentialsRaw = section?[CorsConfigurationKeys.AllowCredentialsKey];
            if (bool.TryParse(allowCredentialsRaw, out var allowCredentials))
            {
                options.AllowCredentials = allowCredentials;
            }

            BindInt(
                section,
                CorsConfigurationKeys.MaxAgeSecondsKey,
                value => options.MaxAgeSeconds = value,
                problems);

            problems.AddRange(options.Validate());

            if (problems.Count > 0)
            {
                throw new InvalidCorsConfigurationException(problems);
            }

            return options;
        }

        private static void BindStringList(IConfiguration? section, string key, Action<IReadOnlyList<string>> assign, ICollection<string> problems)
        {
            var arraySection = section?.GetSection(key);
            var children = arraySection?.GetChildren();
            if (children is null || !children.Any())
            {
                return;
            }

            var values = new List<string>();
            foreach (var child in children)
            {
                values.Add(child.Value ?? string.Empty);
            }

            assign(values);
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
                problems.Add($"{CorsConfigurationKeys.SectionName}:{key} is not a valid 32-bit integer.");
                return;
            }

            assign(value);
        }
    }
}
