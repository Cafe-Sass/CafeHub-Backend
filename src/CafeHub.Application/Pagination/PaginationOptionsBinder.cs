using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CafeHub.Application.Pagination
{
    public static class PaginationOptionsBinder
    {
        public static PaginationOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(PaginationConfigurationKeys.SectionName);
            var options = new PaginationOptions();
            var problems = new List<string>();

            BindSize(section, PaginationConfigurationKeys.DefaultPageSizeKey, value => options.DefaultPageSize = value, problems);
            BindSize(section, PaginationConfigurationKeys.MaxPageSizeKey, value => options.MaxPageSize = value, problems);
            problems.AddRange(options.Validate());

            if (problems.Count > 0)
            {
                throw new InvalidPaginationConfigurationException(problems);
            }

            return options;
        }

        private static void BindSize(IConfiguration? section, string key, Action<int> assign, ICollection<string> problems)
        {
            var rawValue = section?[key];
            if (rawValue is null)
            {
                return;
            }

            if (!int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                problems.Add($"{PaginationConfigurationKeys.SectionName}:{key} is not a valid 32-bit integer.");
                return;
            }

            assign(value);
        }
    }
}
