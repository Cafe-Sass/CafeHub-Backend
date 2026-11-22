using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace CafeHub.Application.Orders
{
    public static class OrdersOptionsBinder
    {
        public static OrdersOptions Bind(IConfiguration? configuration)
        {
            var section = configuration?.GetSection(OrdersConfigurationKeys.SectionName);
            var options = new OrdersOptions();
            var problems = new List<string>();
            var rawValue = section?[OrdersConfigurationKeys.ResponseWindowSecondsKey];
            if (rawValue is not null)
            {
                if (int.TryParse(rawValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
                {
                    options.ResponseWindowSeconds = value;
                }
                else
                {
                    problems.Add(
                        $"{OrdersConfigurationKeys.ResponseWindowSecondsPath} is not a valid 32-bit integer.");
                }
            }

            problems.AddRange(options.Validate());
            if (problems.Count > 0)
            {
                throw new InvalidOrdersConfigurationException(problems);
            }

            return options;
        }
    }
}
