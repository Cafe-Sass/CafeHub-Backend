using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CafeHub.Infrastructure.Media
{
    /// <summary>
    /// Binds the media settings and fails closed: a missing, relative, or web-served root path aborts
    /// startup instead of writing uploads inside the application or a served folder.
    /// </summary>
    public static class MediaOptionsBinder
    {
        public static MediaOptions Bind(IConfiguration? configuration, string contentRootPath)
        {
            var section = configuration?.GetSection(MediaConfigurationKeys.SectionName);
            var options = new MediaOptions
            {
                RootPath = section?[MediaConfigurationKeys.RootPathKey] ?? string.Empty,
            };

            var problems = new List<string>();
            var maxBytes = section?[MediaConfigurationKeys.MaxBytesKey];
            if (!string.IsNullOrWhiteSpace(maxBytes))
            {
                if (int.TryParse(maxBytes, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedMaxBytes))
                {
                    options.MaxBytes = parsedMaxBytes;
                }
                else
                {
                    problems.Add($"{MediaConfigurationKeys.MaxBytesPath} is not a valid 32-bit integer.");
                }
            }

            problems.AddRange(options.Validate(contentRootPath));

            if (problems.Count > 0)
            {
                throw new InvalidMediaConfigurationException(problems);
            }

            return options;
        }
    }
}
